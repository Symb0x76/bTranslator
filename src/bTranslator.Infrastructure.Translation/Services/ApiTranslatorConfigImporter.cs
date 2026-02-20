using bTranslator.Domain.Models;
using bTranslator.Infrastructure.Translation.Options;

namespace bTranslator.Infrastructure.Translation.Services;

public static class ApiTranslatorConfigImporter
{
    private const string AzureDefaultEndpoint = "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0";
    private const string GoogleDefaultEndpoint = "https://translation.googleapis.com/language/translate/v2";
    private const string DeepLFreeDefaultEndpoint = "https://api-free.deepl.com/v2/translate";
    private const string DeepLProDefaultEndpoint = "https://api.deepl.com/v2/translate";
    private const string BaiduDefaultEndpoint = "https://fanyi-api.baidu.com/api/trans/vip/translate";

    public static TranslationProviderOptions ImportFromIniLikeContent(string content)
    {
        var options = new TranslationProviderOptions();
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var idx = trimmed.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = trimmed[..idx].Trim();
            var value = trimmed[(idx + 1)..].Trim();

            var split = key.Split('_', 2);
            if (split.Length != 2)
            {
                continue;
            }

            var provider = NormalizeProviderId(split[0]);
            if (!options.Providers.TryGetValue(provider, out var existing))
            {
                existing = new ProviderEndpointOptions();
            }

            existing = ApplyKeyValue(existing, split[1], value, provider);
            options.Providers[provider] = existing;
        }

        return options;
    }

    private static string NormalizeProviderId(string raw)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
            "openai" or "openaicompatible" => "openai-compatible",
            "mstranslate" => "azure-translator",
            "google" => "google-cloud-translate",
            "deepl" => "deepl",
            "baidu" => "baidu",
            "youdao" => "youdao",
            "tencent" or "tencenttmt" or "tmt" => "tencent-tmt",
            "volcengine" or "volcano" => "volcengine",
            "anthropic" or "claude" => "anthropic",
            "gemini" => "gemini",
            "ollama" => "ollama",
            _ => raw.Trim().ToLowerInvariant()
        };
    }

    private static ProviderEndpointOptions ApplyKeyValue(
        ProviderEndpointOptions options,
        string keySuffix,
        string value,
        string providerId)
    {
        var normalizedSuffix = keySuffix.ToLowerInvariant();

        if (normalizedSuffix.StartsWith("model", StringComparison.Ordinal))
        {
            var shouldUseModel = normalizedSuffix == "model0" || string.IsNullOrWhiteSpace(options.Model);
            return shouldUseModel ? With(options, model: value) : options;
        }

        return normalizedSuffix switch
        {
            "apiurl" => With(options, baseUrl: NormalizeLegacyApiUrl(providerId, normalizedSuffix, value)),
            "proapiurl" => string.Equals(providerId, "deepl", StringComparison.OrdinalIgnoreCase)
                ? With(options, baseUrl: NormalizeLegacyApiUrl(providerId, normalizedSuffix, value))
                : options,
            "defaultquery" or "query" or "prompttemplate" => With(options, promptTemplate: value),
            "organization" or "org" => With(options, organization: value),
            "azureclientsecret" => With(options, apiKey: value),
            "apikey" => With(options, apiKey: value),
            "key" => providerId switch
            {
                "baidu" => With(options, apiSecret: value),
                _ => With(options, apiKey: value)
            },
            "appid" => With(options, apiKey: value),
            "secretkey" or "secret" => With(options, apiSecret: value),
            "region" => With(options, region: value),
            "token" => WithMetadata(options, "token", value),
            "charlimit" => WithCapabilities(options, maxCharsPerRequest: ParseIntOrDefault(value, options.Capabilities.MaxCharsPerRequest)),
            "arraylimit" => WithCapabilities(options, maxItemsPerBatch: ParseIntOrDefault(value, options.Capabilities.MaxItemsPerBatch)),
            "arraymaxcharpermin" => WithCapabilities(options, charsPerMinute: ParseIntOrDefault(value, options.Capabilities.CharsPerMinute)),
            "arraytimepause" => WithCapabilities(options, requestsPerMinute: ParseRequestsPerMinute(value, options.Capabilities.RequestsPerMinute)),
            _ => TryApplyLanguageMapping(options, normalizedSuffix, value)
        };
    }

    private static string NormalizeLegacyApiUrl(string providerId, string keySuffix, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        var hasLegacyTemplate = trimmed.Contains("%s", StringComparison.OrdinalIgnoreCase) ||
                                trimmed.Contains("{text}", StringComparison.OrdinalIgnoreCase);

        if (!hasLegacyTemplate)
        {
            return trimmed;
        }

        return providerId switch
        {
            "azure-translator" => AzureDefaultEndpoint,
            "google-cloud-translate" => GoogleDefaultEndpoint,
            "deepl" => string.Equals(keySuffix, "proapiurl", StringComparison.OrdinalIgnoreCase)
                ? DeepLProDefaultEndpoint
                : DeepLFreeDefaultEndpoint,
            "baidu" => BaiduDefaultEndpoint,
            _ => NormalizeToPath(trimmed)
        };
    }

    private static string NormalizeToPath(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        return uri.GetLeftPart(UriPartial.Path);
    }

    private static ProviderEndpointOptions TryApplyLanguageMapping(
        ProviderEndpointOptions options,
        string keySuffix,
        string value)
    {
        if (!IsLanguageMappingCandidate(keySuffix, value))
        {
            return options;
        }

        var updated = new Dictionary<string, string>(options.LanguageMap, StringComparer.OrdinalIgnoreCase)
        {
            [keySuffix] = value
        };

        return With(options, languageMap: updated);
    }

    private static bool IsLanguageMappingCandidate(string keySuffix, string value)
    {
        if (string.IsNullOrWhiteSpace(keySuffix) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (keySuffix.Contains("url", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("query", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("model", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("key", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("appid", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("enabled", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("label", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("powered", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("limit", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("count", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("pause", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("region", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("organization", StringComparison.OrdinalIgnoreCase) ||
            keySuffix.Contains("token", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static ProviderEndpointOptions With(
        ProviderEndpointOptions options,
        string? baseUrl = null,
        string? model = null,
        string? apiKey = null,
        string? apiSecret = null,
        string? region = null,
        string? organization = null,
        string? promptTemplate = null,
        IDictionary<string, string>? languageMap = null,
        IDictionary<string, string>? metadata = null)
    {
        return new ProviderEndpointOptions
        {
            BaseUrl = baseUrl ?? options.BaseUrl,
            Model = model ?? options.Model,
            ApiKey = apiKey ?? options.ApiKey,
            ApiSecret = apiSecret ?? options.ApiSecret,
            Region = region ?? options.Region,
            Organization = organization ?? options.Organization,
            PromptTemplate = promptTemplate ?? options.PromptTemplate,
            LanguageMap = languageMap ?? new Dictionary<string, string>(options.LanguageMap, StringComparer.OrdinalIgnoreCase),
            Metadata = metadata ?? new Dictionary<string, string>(options.Metadata, StringComparer.OrdinalIgnoreCase),
            Capabilities = options.Capabilities
        };
    }

    private static ProviderEndpointOptions WithMetadata(
        ProviderEndpointOptions options,
        string key,
        string value)
    {
        var metadata = new Dictionary<string, string>(options.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            [key] = value
        };

        return With(options, metadata: metadata);
    }

    private static ProviderEndpointOptions WithCapabilities(
        ProviderEndpointOptions options,
        int? maxCharsPerRequest = null,
        int? maxItemsPerBatch = null,
        int? requestsPerMinute = null,
        int? charsPerMinute = null)
    {
        return new ProviderEndpointOptions
        {
            BaseUrl = options.BaseUrl,
            Model = options.Model,
            ApiKey = options.ApiKey,
            ApiSecret = options.ApiSecret,
            Region = options.Region,
            Organization = options.Organization,
            PromptTemplate = options.PromptTemplate,
            LanguageMap = new Dictionary<string, string>(options.LanguageMap, StringComparer.OrdinalIgnoreCase),
            Metadata = new Dictionary<string, string>(options.Metadata, StringComparer.OrdinalIgnoreCase),
            Capabilities = new ProviderCapabilities
            {
                MaxCharsPerRequest = maxCharsPerRequest ?? options.Capabilities.MaxCharsPerRequest,
                MaxItemsPerBatch = maxItemsPerBatch ?? options.Capabilities.MaxItemsPerBatch,
                RequestsPerMinute = requestsPerMinute ?? options.Capabilities.RequestsPerMinute,
                CharsPerMinute = charsPerMinute ?? options.Capabilities.CharsPerMinute,
                SupportsBatch = options.Capabilities.SupportsBatch
            }
        };
    }

    private static int ParseIntOrDefault(string value, int fallback)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static int ParseRequestsPerMinute(string pauseSeconds, int fallback)
    {
        if (!double.TryParse(pauseSeconds, out var seconds) || seconds <= 0)
        {
            return fallback;
        }

        var rpm = (int)Math.Floor(60d / seconds);
        return Math.Max(1, rpm);
    }
}
