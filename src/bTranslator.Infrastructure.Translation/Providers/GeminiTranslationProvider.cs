using System.Text.Json;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Translation.Providers;

public sealed class GeminiTranslationProvider : BaseJsonHttpTranslationProvider
{
    public const string DefaultProviderId = "gemini";

    public GeminiTranslationProvider(
        HttpClient httpClient,
        IOptions<bTranslator.Infrastructure.Translation.Options.TranslationProviderOptions> options)
        : base(DefaultProviderId, httpClient, options)
    {
    }

    public override async Task<TranslationBatchResult> TranslateBatchAsync(
        TranslationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Options.ApiKey))
        {
            throw new TranslationProviderException(
                ProviderId,
                TranslationErrorKind.Authentication,
                $"Provider '{ProviderId}' requires ApiKey.");
        }

        var model = Options.Model ?? "gemini-1.5-flash";
        var endpointTemplate = Options.BaseUrl?.TrimEnd('/') ??
                               "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        var endpoint = endpointTemplate.Contains("{model}", StringComparison.OrdinalIgnoreCase)
            ? endpointTemplate.Replace("{model}", Uri.EscapeDataString(model), StringComparison.OrdinalIgnoreCase)
            : $"{endpointTemplate}/{Uri.EscapeDataString(model)}:generateContent";
        endpoint = ProviderTextUtilities.AppendQueryParameter(endpoint, "key", Options.ApiKey);

        var prompt = ProviderTextUtilities.BuildPrompt(
            request,
            Options.PromptTemplate,
            "Translate to %lang_dest%. Keep tags, numbers and line breaks untouched.");

        var translated = new List<string>(request.Items.Count);
        foreach (var item in request.Items)
        {
            var payload = new
            {
                contents = new object[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = ProviderTextUtilities.BuildTextPrompt(prompt, item) }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0
                }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonBody(payload)
            };

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
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0 ||
            !candidates[0].TryGetProperty("content", out var message) ||
            !message.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array ||
            parts.GetArrayLength() == 0 ||
            !parts[0].TryGetProperty("text", out var text))
        {
            throw new TranslationProviderException(
                DefaultProviderId,
                TranslationErrorKind.Validation,
                "Gemini response missing candidates[0].content.parts[0].text.");
        }

        return text.GetString() ?? string.Empty;
    }
}
