using System.Globalization;
using System.Text;
using bTranslator.Domain.Models;

namespace bTranslator.Infrastructure.Translation.Providers;

internal static class ProviderTextUtilities
{
    private static readonly IReadOnlyDictionary<string, string> CommonLanguageMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["english"] = "en",
            ["en"] = "en",
            ["french"] = "fr",
            ["fr"] = "fr",
            ["german"] = "de",
            ["de"] = "de",
            ["italian"] = "it",
            ["it"] = "it",
            ["spanish"] = "es",
            ["es"] = "es",
            ["portuguese"] = "pt",
            ["pt"] = "pt",
            ["portuguese (brazil)"] = "pt-BR",
            ["pt-br"] = "pt-BR",
            ["russian"] = "ru",
            ["ru"] = "ru",
            ["japanese"] = "ja",
            ["ja"] = "ja",
            ["korean"] = "ko",
            ["ko"] = "ko",
            ["chinese"] = "zh",
            ["cn"] = "zh",
            ["zh"] = "zh",
            ["chinese (simplified)"] = "zh-Hans",
            ["zh-hans"] = "zh-Hans",
            ["zhhans"] = "zh-Hans",
            ["chinese (traditional)"] = "zh-Hant",
            ["zh-hant"] = "zh-Hant",
            ["zhhant"] = "zh-Hant",
            ["czech"] = "cs",
            ["cs"] = "cs",
            ["polish"] = "pl",
            ["pl"] = "pl",
            ["danish"] = "da",
            ["da"] = "da",
            ["finnish"] = "fi",
            ["fi"] = "fi",
            ["greek"] = "el",
            ["el"] = "el",
            ["norwegian"] = "no",
            ["no"] = "no",
            ["swedish"] = "sv",
            ["sv"] = "sv",
            ["turkish"] = "tr",
            ["tr"] = "tr",
            ["hungarian"] = "hu",
            ["hu"] = "hu",
            ["arabic"] = "ar",
            ["ar"] = "ar",
            ["estonian"] = "et",
            ["et"] = "et",
            ["ukrainian"] = "uk",
            ["uk"] = "uk"
        };

    private static readonly IReadOnlyDictionary<string, string> BaiduLanguageMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "en",
            ["fr"] = "fra",
            ["de"] = "de",
            ["it"] = "it",
            ["es"] = "spa",
            ["pt"] = "pt",
            ["ru"] = "ru",
            ["ja"] = "jp",
            ["ko"] = "kor",
            ["zh"] = "zh",
            ["cs"] = "cs",
            ["pl"] = "pl",
            ["da"] = "dan",
            ["fi"] = "fin",
            ["el"] = "el",
            ["sv"] = "swe",
            ["hu"] = "hu",
            ["ar"] = "ara",
            ["no"] = "nor",
            ["tr"] = "tr"
        };

    public static string BuildPrompt(
        TranslationBatchRequest request,
        string? configuredTemplate,
        string fallbackTemplate)
    {
        var template = string.IsNullOrWhiteSpace(request.PromptTemplate)
            ? configuredTemplate
            : request.PromptTemplate;

        template = string.IsNullOrWhiteSpace(template)
            ? fallbackTemplate
            : template;

        return template
            .Replace("%lang_source%", request.SourceLanguage, StringComparison.OrdinalIgnoreCase)
            .Replace("%lang_dest%", request.TargetLanguage, StringComparison.OrdinalIgnoreCase);
    }

    public static string ToAzureLanguage(string language, IDictionary<string, string>? languageMap = null)
    {
        var mapped = ResolveLanguageMapping(language, languageMap);
        if (!string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        var code = ToCommonLanguageCode(language);
        return string.Equals(code, "zh", StringComparison.OrdinalIgnoreCase)
            ? "zh-Hans"
            : code;
    }

    public static string ToGoogleLanguage(string language, IDictionary<string, string>? languageMap = null)
    {
        var mapped = ResolveLanguageMapping(language, languageMap);
        if (!string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        return ToCommonLanguageCode(language);
    }

    public static string ToTencentLanguage(string language, IDictionary<string, string>? languageMap = null)
    {
        var mapped = ResolveLanguageMapping(language, languageMap);
        if (!string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        var code = ToCommonLanguageCode(language);
        return code switch
        {
            "zh-Hans" => "zh",
            "zh-Hant" => "zh-TW",
            _ => code
        };
    }

    public static string ToDeepLLanguage(
        string language,
        bool isTarget,
        IDictionary<string, string>? languageMap = null)
    {
        var mapped = ResolveLanguageMapping(language, languageMap);
        if (!string.IsNullOrWhiteSpace(mapped))
        {
            return mapped.ToUpperInvariant();
        }

        var code = ToCommonLanguageCode(language);
        var normalized = code switch
        {
            "zh-Hans" or "zh-Hant" or "zh" => "ZH",
            "pt-BR" => "PT-BR",
            "en-US" => "EN-US",
            "en-GB" => "EN-GB",
            _ when code.Length == 2 => code.ToUpperInvariant(),
            _ => code.ToUpperInvariant()
        };

        if (!isTarget && (normalized is "EN-US" or "EN-GB"))
        {
            return "EN";
        }

        return normalized;
    }

    public static string ToBaiduLanguage(string language, IDictionary<string, string>? languageMap = null)
    {
        var mapped = ResolveLanguageMapping(language, languageMap);
        if (!string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        var code = ToCommonLanguageCode(language);
        if (string.Equals(code, "zh-Hans", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "zh-Hant", StringComparison.OrdinalIgnoreCase))
        {
            code = "zh";
        }

        if (BaiduLanguageMap.TryGetValue(code, out var baiduMapped))
        {
            return baiduMapped;
        }

        return code.Length > 3 ? code[..3].ToLowerInvariant() : code.ToLowerInvariant();
    }

    public static string ToCommonLanguageCode(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "auto";
        }

        var normalized = language.Trim()
            .Replace('_', '-')
            .Replace("  ", " ", StringComparison.Ordinal);

        if (CommonLanguageMap.TryGetValue(normalized, out var mapped))
        {
            return mapped;
        }

        var lowercase = normalized.ToLowerInvariant();
        if (CommonLanguageMap.TryGetValue(lowercase, out mapped))
        {
            return mapped;
        }

        if (lowercase.Length == 2)
        {
            return lowercase;
        }

        var culture = TryCreateCulture(normalized);
        if (culture is not null)
        {
            return culture.Name;
        }

        return lowercase;
    }

    private static string? ResolveLanguageMapping(string language, IDictionary<string, string>? languageMap)
    {
        if (languageMap is null || languageMap.Count == 0)
        {
            return null;
        }

        foreach (var key in BuildLanguageLookupKeys(language))
        {
            if (languageMap.TryGetValue(key, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
            {
                return mapped.Trim();
            }
        }

        return null;
    }

    private static IEnumerable<string> BuildLanguageLookupKeys(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            yield break;
        }

        var raw = language.Trim();
        var lowerRaw = raw.ToLowerInvariant();
        yield return lowerRaw;

        var compact = new string(lowerRaw.Where(char.IsLetterOrDigit).ToArray());
        if (!string.Equals(compact, lowerRaw, StringComparison.Ordinal))
        {
            yield return compact;
        }

        var common = ToCommonLanguageCode(raw);
        var lowerCommon = common.ToLowerInvariant();
        if (!string.Equals(lowerCommon, lowerRaw, StringComparison.Ordinal))
        {
            yield return lowerCommon;
            var compactCommon = new string(lowerCommon.Where(char.IsLetterOrDigit).ToArray());
            if (!string.Equals(compactCommon, lowerCommon, StringComparison.Ordinal))
            {
                yield return compactCommon;
            }
        }

        var paren = lowerRaw.IndexOf('(');
        if (paren > 0)
        {
            var baseName = lowerRaw[..paren].Trim();
            if (baseName.Length > 0)
            {
                yield return baseName;
            }
        }
    }

    public static (string key, string secret) ParseCredentialPair(
        string? apiKey,
        string? apiSecret,
        string providerId)
    {
        var key = apiKey?.Trim();
        var secret = apiSecret?.Trim();

        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(secret))
        {
            return (key, secret);
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            var separators = new[] { ':', '|', ';', ',' };
            foreach (var separator in separators)
            {
                var idx = key.IndexOf(separator);
                if (idx <= 0 || idx >= key.Length - 1)
                {
                    continue;
                }

                var first = key[..idx].Trim();
                var second = key[(idx + 1)..].Trim();
                if (first.Length > 0 && second.Length > 0)
                {
                    return (first, second);
                }
            }
        }

        throw new bTranslator.Domain.Exceptions.TranslationProviderException(
            providerId,
            bTranslator.Domain.Enums.TranslationErrorKind.Authentication,
            $"Provider '{providerId}' requires both key and secret.");
    }

    public static string AppendQueryParameter(string url, string name, string value)
    {
        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{separator}{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}";
    }

    public static string BuildTextPrompt(string prompt, string input)
    {
        var builder = new StringBuilder(prompt.Length + input.Length + 2);
        builder.Append(prompt);
        builder.AppendLine();
        builder.AppendLine();
        builder.Append(input);
        return builder.ToString();
    }

    private static CultureInfo? TryCreateCulture(string value)
    {
        try
        {
            return CultureInfo.GetCultureInfo(value);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
