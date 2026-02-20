using System.Text.Json;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Translation.Providers;

public sealed class AzureTranslatorProvider : BaseJsonHttpTranslationProvider
{
    public const string DefaultProviderId = "azure-translator";

    public AzureTranslatorProvider(
        HttpClient httpClient,
        IOptions<bTranslator.Infrastructure.Translation.Options.TranslationProviderOptions> options)
        : base(DefaultProviderId, httpClient, options)
    {
    }

    protected override void AddAuthentication(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(Options.ApiKey))
        {
            throw new TranslationProviderException(
                ProviderId,
                TranslationErrorKind.Authentication,
                $"Provider '{ProviderId}' requires ApiKey.");
        }

        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", Options.ApiKey);
        if (!string.IsNullOrWhiteSpace(Options.Region))
        {
            request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Region", Options.Region);
        }
    }

    public override async Task<TranslationBatchResult> TranslateBatchAsync(
        TranslationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var endpoint = Options.BaseUrl?.Trim() ??
                       "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0";
        endpoint = ProviderTextUtilities.AppendQueryParameter(endpoint, "from", ProviderTextUtilities.ToAzureLanguage(request.SourceLanguage, Options.LanguageMap));
        endpoint = ProviderTextUtilities.AppendQueryParameter(endpoint, "to", ProviderTextUtilities.ToAzureLanguage(request.TargetLanguage, Options.LanguageMap));

        var payload = request.Items.Select(static text => new { Text = text }).ToArray();
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

            var translated = ParseResponse(content);
            if (translated.Count != request.Items.Count)
            {
                throw new TranslationProviderException(
                    ProviderId,
                    TranslationErrorKind.Validation,
                    $"Provider '{ProviderId}' returned {translated.Count} items, expected {request.Items.Count}.");
            }

            return new TranslationBatchResult
            {
                Items = translated,
                RawResponse = content
            };
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

    private static IReadOnlyList<string> ParseResponse(string content)
    {
        using var doc = JsonDocument.Parse(content);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new TranslationProviderException(
                DefaultProviderId,
                TranslationErrorKind.Validation,
                "Azure translator response root must be an array.");
        }

        var output = new List<string>(doc.RootElement.GetArrayLength());
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("translations", out var translations) ||
                translations.ValueKind != JsonValueKind.Array ||
                translations.GetArrayLength() == 0 ||
                !translations[0].TryGetProperty("text", out var translated))
            {
                throw new TranslationProviderException(
                    DefaultProviderId,
                    TranslationErrorKind.Validation,
                    "Azure translator response missing translations[0].text.");
            }

            output.Add(translated.GetString() ?? string.Empty);
        }

        return output;
    }
}
