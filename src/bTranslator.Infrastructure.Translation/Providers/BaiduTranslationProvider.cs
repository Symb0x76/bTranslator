using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Translation.Providers;

public sealed class BaiduTranslationProvider : BaseJsonHttpTranslationProvider
{
    public const string DefaultProviderId = "baidu";

    public BaiduTranslationProvider(
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
                       "https://fanyi-api.baidu.com/api/trans/vip/translate";
        var (appId, secret) = ProviderTextUtilities.ParseCredentialPair(Options.ApiKey, Options.ApiSecret, ProviderId);

        var source = ProviderTextUtilities.ToBaiduLanguage(request.SourceLanguage, Options.LanguageMap);
        var target = ProviderTextUtilities.ToBaiduLanguage(request.TargetLanguage, Options.LanguageMap);
        var output = new List<string>(request.Items.Count);

        foreach (var item in request.Items)
        {
            var salt = RandomNumberGenerator.GetInt32(10000, 99999999).ToString();
            var sign = BuildSign(appId, item, salt, secret);
            var form = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("q", item),
                new KeyValuePair<string, string>("from", source),
                new KeyValuePair<string, string>("to", target),
                new KeyValuePair<string, string>("appid", appId),
                new KeyValuePair<string, string>("salt", salt),
                new KeyValuePair<string, string>("sign", sign)
            ]);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = form
            };

            try
            {
                using var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw ToProviderException(ProviderId, response, content);
                }

                output.Add(ParseSingleResult(content));
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
            Items = output
        };
    }

    private static string BuildSign(string appId, string text, string salt, string secret)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes($"{appId}{text}{salt}{secret}");
        var hash = md5.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ParseSingleResult(string content)
    {
        using var doc = JsonDocument.Parse(content);
        if (doc.RootElement.TryGetProperty("error_code", out var errorCode))
        {
            var errorCodeValue = errorCode.GetString() ?? "unknown";
            var message = doc.RootElement.TryGetProperty("error_msg", out var errorMessage)
                ? errorMessage.GetString() ?? "Baidu translation failed."
                : "Baidu translation failed.";

            throw new TranslationProviderException(
                DefaultProviderId,
                MapError(errorCodeValue),
                $"Baidu error {errorCodeValue}: {message}");
        }

        if (!doc.RootElement.TryGetProperty("trans_result", out var transResult) ||
            transResult.ValueKind != JsonValueKind.Array ||
            transResult.GetArrayLength() == 0 ||
            !transResult[0].TryGetProperty("dst", out var dst))
        {
            throw new TranslationProviderException(
                DefaultProviderId,
                TranslationErrorKind.Validation,
                "Baidu response missing trans_result[0].dst.");
        }

        return dst.GetString() ?? string.Empty;
    }

    private static TranslationErrorKind MapError(string code)
    {
        return code switch
        {
            "52003" or "54001" => TranslationErrorKind.Authentication,
            "54003" or "54005" or "58003" => TranslationErrorKind.RateLimit,
            "52001" or "52002" or "52005" => TranslationErrorKind.Transient,
            _ => TranslationErrorKind.Fatal
        };
    }
}
