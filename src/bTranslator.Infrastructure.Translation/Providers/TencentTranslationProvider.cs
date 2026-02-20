using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Translation.Providers;

public sealed class TencentTranslationProvider : BaseJsonHttpTranslationProvider
{
    public const string DefaultProviderId = "tencent-tmt";
    private const string Service = "tmt";
    private const string Action = "TextTranslate";
    private const string Version = "2018-03-21";

    public TencentTranslationProvider(
        HttpClient httpClient,
        IOptions<bTranslator.Infrastructure.Translation.Options.TranslationProviderOptions> options)
        : base(DefaultProviderId, httpClient, options)
    {
    }

    public override async Task<TranslationBatchResult> TranslateBatchAsync(
        TranslationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var endpoint = Options.BaseUrl?.Trim() ?? "https://tmt.tencentcloudapi.com";
        var host = new Uri(endpoint, UriKind.Absolute).Host;
        var region = string.IsNullOrWhiteSpace(Options.Region) ? "ap-guangzhou" : Options.Region;
        var source = ProviderTextUtilities.ToTencentLanguage(request.SourceLanguage, Options.LanguageMap);
        var target = ProviderTextUtilities.ToTencentLanguage(request.TargetLanguage, Options.LanguageMap);
        var (secretId, secretKey) = ProviderTextUtilities.ParseCredentialPair(Options.ApiKey, Options.ApiSecret, ProviderId);
        var projectId = ParseProjectId(Options.Model);

        var translated = new List<string>(request.Items.Count);
        foreach (var item in request.Items)
        {
            var payload = JsonSerializer.Serialize(new
            {
                SourceText = item,
                Source = source,
                Target = target,
                ProjectId = projectId
            });
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            httpRequest.Headers.TryAddWithoutValidation("Host", host);
            httpRequest.Headers.TryAddWithoutValidation("X-TC-Action", Action);
            httpRequest.Headers.TryAddWithoutValidation("X-TC-Version", Version);
            httpRequest.Headers.TryAddWithoutValidation("X-TC-Region", region);
            httpRequest.Headers.TryAddWithoutValidation("X-TC-Timestamp", timestamp.ToString());
            if (Options.Metadata.TryGetValue("token", out var token) && !string.IsNullOrWhiteSpace(token))
            {
                httpRequest.Headers.TryAddWithoutValidation("X-TC-Token", token);
            }

            var authorization = BuildAuthorization(secretId, secretKey, date, timestamp, payload, host);
            httpRequest.Headers.TryAddWithoutValidation("Authorization", authorization);

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

    private static string BuildAuthorization(
        string secretId,
        string secretKey,
        string date,
        long timestamp,
        string payload,
        string host)
    {
        const string signedHeaders = "content-type;host;x-tc-action";
        const string contentType = "application/json; charset=utf-8";
        var canonicalHeaders = $"content-type:{contentType}\nhost:{host}\nx-tc-action:{Action.ToLowerInvariant()}\n";
        var hashedPayload = Sha256Hex(payload);
        var canonicalRequest = $"POST\n/\n\n{canonicalHeaders}\n{signedHeaders}\n{hashedPayload}";
        var credentialScope = $"{date}/{Service}/tc3_request";
        var stringToSign = $"TC3-HMAC-SHA256\n{timestamp}\n{credentialScope}\n{Sha256Hex(canonicalRequest)}";

        var secretDate = HmacSha256(Encoding.UTF8.GetBytes($"TC3{secretKey}"), date);
        var secretService = HmacSha256(secretDate, Service);
        var secretSigning = HmacSha256(secretService, "tc3_request");
        var signature = Convert.ToHexString(HmacSha256(secretSigning, stringToSign)).ToLowerInvariant();

        return $"TC3-HMAC-SHA256 Credential={secretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
    }

    private static string ParseResponse(string content)
    {
        using var doc = JsonDocument.Parse(content);
        if (!doc.RootElement.TryGetProperty("Response", out var responseNode))
        {
            throw new TranslationProviderException(
                DefaultProviderId,
                TranslationErrorKind.Validation,
                "Tencent response missing Response object.");
        }

        if (responseNode.TryGetProperty("Error", out var error))
        {
            var code = error.TryGetProperty("Code", out var codeElement)
                ? codeElement.GetString() ?? "Unknown"
                : "Unknown";
            var message = error.TryGetProperty("Message", out var msgElement)
                ? msgElement.GetString() ?? "Tencent translation failed."
                : "Tencent translation failed.";

            throw new TranslationProviderException(
                DefaultProviderId,
                MapError(code),
                $"Tencent error {code}: {message}");
        }

        if (!responseNode.TryGetProperty("TargetText", out var targetText))
        {
            throw new TranslationProviderException(
                DefaultProviderId,
                TranslationErrorKind.Validation,
                "Tencent response missing Response.TargetText.");
        }

        return targetText.GetString() ?? string.Empty;
    }

    private static TranslationErrorKind MapError(string code)
    {
        if (code.StartsWith("AuthFailure", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("UnauthorizedOperation", StringComparison.OrdinalIgnoreCase))
        {
            return TranslationErrorKind.Authentication;
        }

        if (code.Contains("LimitExceeded", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("RequestLimitExceeded", StringComparison.OrdinalIgnoreCase))
        {
            return TranslationErrorKind.RateLimit;
        }

        if (code.StartsWith("InternalError", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("ResourceUnavailable", StringComparison.OrdinalIgnoreCase))
        {
            return TranslationErrorKind.Transient;
        }

        return TranslationErrorKind.Fatal;
    }

    private static int ParseProjectId(string? value)
    {
        return int.TryParse(value, out var projectId) ? projectId : 0;
    }

    private static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }
}
