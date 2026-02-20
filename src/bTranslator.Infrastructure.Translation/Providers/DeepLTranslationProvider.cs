using System.Net.Http.Headers;
using System.Text.Json;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Translation.Providers;

public sealed class DeepLTranslationProvider : BaseJsonHttpTranslationProvider
{
    public const string DefaultProviderId = "deepl";

    public DeepLTranslationProvider(
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

        request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", Options.ApiKey);
    }

    public override async Task<TranslationBatchResult> TranslateBatchAsync(
        TranslationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var endpoint = Options.BaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = Options.ApiKey?.EndsWith(":fx", StringComparison.OrdinalIgnoreCase) == true
                ? "https://api-free.deepl.com/v2/translate"
                : "https://api.deepl.com/v2/translate";
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("source_lang", ProviderTextUtilities.ToDeepLLanguage(request.SourceLanguage, isTarget: false, Options.LanguageMap)),
            new("target_lang", ProviderTextUtilities.ToDeepLLanguage(request.TargetLanguage, isTarget: true, Options.LanguageMap)),
            new("split_sentences", "1"),
            new("preserve_formatting", "1")
        };

        foreach (var item in request.Items)
        {
            form.Add(new KeyValuePair<string, string>("text", item));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(form)
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
        if (!doc.RootElement.TryGetProperty("translations", out var translations) ||
            translations.ValueKind != JsonValueKind.Array)
        {
            throw new TranslationProviderException(
                DefaultProviderId,
                TranslationErrorKind.Validation,
                "DeepL response missing translations array.");
        }

        var output = new List<string>(translations.GetArrayLength());
        foreach (var item in translations.EnumerateArray())
        {
            if (!item.TryGetProperty("text", out var text))
            {
                throw new TranslationProviderException(
                    DefaultProviderId,
                    TranslationErrorKind.Validation,
                    "DeepL response item missing text.");
            }

            output.Add(text.GetString() ?? string.Empty);
        }

        return output;
    }
}
