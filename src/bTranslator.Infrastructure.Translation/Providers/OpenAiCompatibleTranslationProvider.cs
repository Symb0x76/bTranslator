using System.Text.Json;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Translation.Providers;

public sealed class OpenAiCompatibleTranslationProvider : BaseJsonHttpTranslationProvider
{
    public const string DefaultProviderId = "openai-compatible";

    public OpenAiCompatibleTranslationProvider(
        HttpClient httpClient,
        IOptions<bTranslator.Infrastructure.Translation.Options.TranslationProviderOptions> options)
        : base(DefaultProviderId, httpClient, options)
    {
    }

    public override async Task<TranslationBatchResult> TranslateBatchAsync(
        TranslationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = Options.BaseUrl?.TrimEnd('/') ?? "https://api.openai.com/v1/chat/completions";
        var model = Options.Model ?? "gpt-4o-mini";
        var prompt = Options.PromptTemplate ??
                     "Translate to %lang_dest%. Keep all tags, numbers and line breaks exactly as is.";

        var requestItems = request.Items.Select(text => new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = "You are a translation engine for Bethesda game mods." },
                new
                {
                    role = "user",
                    content = prompt
                        .Replace("%lang_source%", request.SourceLanguage, StringComparison.OrdinalIgnoreCase)
                        .Replace("%lang_dest%", request.TargetLanguage, StringComparison.OrdinalIgnoreCase)
                        + "\n\n" + text
                }
            }
        }).ToList();

        var translated = new List<string>(requestItems.Count);
        foreach (var payload in requestItems)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = JsonBody(payload)
            };

            AddAuthentication(httpRequest);
            try
            {
                using var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw ToProviderException(ProviderId, response, content);
                }

                translated.Add(ParseCompletion(content));
            }
            catch (TranslationProviderException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw ToTransient(ProviderId, ex);
            }
        }

        return new TranslationBatchResult
        {
            Items = translated
        };
    }

    private static string ParseCompletion(string content)
    {
        using var doc = JsonDocument.Parse(content);
        if (doc.RootElement.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var value))
        {
            return value.GetString() ?? string.Empty;
        }

        throw new TranslationProviderException(
            DefaultProviderId,
            Domain.Enums.TranslationErrorKind.Validation,
            "OpenAI compatible response did not contain choices[0].message.content.");
    }
}

