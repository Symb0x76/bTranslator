using System.Text.Json;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Translation.Providers;

public sealed class VolcengineTranslationProvider : BaseJsonHttpTranslationProvider
{
    public const string DefaultProviderId = "volcengine";

    public VolcengineTranslationProvider(
        HttpClient httpClient,
        IOptions<bTranslator.Infrastructure.Translation.Options.TranslationProviderOptions> options)
        : base(DefaultProviderId, httpClient, options)
    {
    }

    public override async Task<TranslationBatchResult> TranslateBatchAsync(
        TranslationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var endpoint = Options.BaseUrl?.TrimEnd('/') ?? "https://ark.cn-beijing.volces.com/api/v3/chat/completions";
        var model = Options.Model ?? "doubao-lite-32k";
        var prompt = ProviderTextUtilities.BuildPrompt(
            request,
            Options.PromptTemplate,
            "Translate to %lang_dest%. Keep all tags, numbers and line breaks exactly as is.");

        var translated = new List<string>(request.Items.Count);
        foreach (var item in request.Items)
        {
            var payload = new
            {
                model,
                messages = new object[]
                {
                    new { role = "system", content = "You are a translation engine for Bethesda game mods." },
                    new
                    {
                        role = "user",
                        content = ProviderTextUtilities.BuildTextPrompt(prompt, item)
                    }
                }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
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

                translated.Add(ParseResponse(content));
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

    private static string ParseResponse(string content)
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
            TranslationErrorKind.Validation,
            "Volcengine response did not contain choices[0].message.content.");
    }
}
