using System.Net;
using System.Text.Json;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Translation.Providers;

public sealed class GoogleCloudTranslationProvider : BaseJsonHttpTranslationProvider
{
    public const string DefaultProviderId = "google-cloud-translate";

    public GoogleCloudTranslationProvider(
        HttpClient httpClient,
        IOptions<bTranslator.Infrastructure.Translation.Options.TranslationProviderOptions> options)
        : base(DefaultProviderId, httpClient, options)
    {
    }

    public override async Task<TranslationBatchResult> TranslateBatchAsync(
        TranslationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var endpoint = Options.BaseUrl?.Trim() ??
                       "https://translation.googleapis.com/language/translate/v2";
        if (!string.IsNullOrWhiteSpace(Options.ApiKey))
        {
            endpoint = ProviderTextUtilities.AppendQueryParameter(endpoint, "key", Options.ApiKey);
        }

        var payload = new
        {
            q = request.Items,
            source = ProviderTextUtilities.ToGoogleLanguage(request.SourceLanguage, Options.LanguageMap),
            target = ProviderTextUtilities.ToGoogleLanguage(request.TargetLanguage, Options.LanguageMap),
            format = "text"
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
        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("translations", out var translations) ||
            translations.ValueKind != JsonValueKind.Array)
        {
            throw new TranslationProviderException(
                DefaultProviderId,
                TranslationErrorKind.Validation,
                "Google Translate response missing data.translations.");
        }

        var output = new List<string>(translations.GetArrayLength());
        foreach (var item in translations.EnumerateArray())
        {
            if (!item.TryGetProperty("translatedText", out var text))
            {
                throw new TranslationProviderException(
                    DefaultProviderId,
                    TranslationErrorKind.Validation,
                    "Google Translate response item missing translatedText.");
            }

            output.Add(WebUtility.HtmlDecode(text.GetString() ?? string.Empty));
        }

        return output;
    }
}
