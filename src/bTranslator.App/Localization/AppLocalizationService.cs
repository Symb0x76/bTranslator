using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace bTranslator.App.Localization;

public readonly record struct UiLanguageOption(string LanguageTag, string DisplayName);

public interface IAppLocalizationService
{
    string DefaultLanguageTag { get; }

    IReadOnlyList<UiLanguageOption> SupportedLanguages { get; }

    event EventHandler? LanguageChanged;

    string NormalizeLanguageTag(string? languageTag);

    void ApplyLanguage(string languageTag);

    string GetString(string key);

    string GetString(string key, string fallback);
}

public sealed class AppLocalizationService : IAppLocalizationService
{
    public const string UiLanguageSettingKey = "ui.language";

    private static readonly UiLanguageOption[] Languages =
    [
        new(string.Empty, "System default"),
        new("en-US", "English"),
        new("zh-CN", "简体中文")
    ];

    private ResourceLoader? _resourceLoader;
    private bool _resourceLoaderUnavailable;

    public string DefaultLanguageTag => "en-US";

    public IReadOnlyList<UiLanguageOption> SupportedLanguages => Languages;

    public event EventHandler? LanguageChanged;

    public string NormalizeLanguageTag(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return string.Empty;
        }

        var normalized = languageTag.Trim();
        foreach (var option in Languages)
        {
            if (string.Equals(option.LanguageTag, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return option.LanguageTag;
            }
        }

        if (normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-CN";
        }

        if (normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return "en-US";
        }

        return DefaultLanguageTag;
    }

    public void ApplyLanguage(string languageTag)
    {
        var normalized = NormalizeLanguageTag(languageTag);
        ApplicationLanguages.PrimaryLanguageOverride = normalized;

        CultureInfo culture;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            culture = CultureInfo.InstalledUICulture;
        }
        else
        {
            culture = CultureInfo.GetCultureInfo(normalized);
        }

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var loader = GetOrCreateResourceLoader();
        if (loader is null)
        {
            return key;
        }

        string value;
        try
        {
            value = loader.GetString(key);
        }
        catch
        {
            _resourceLoaderUnavailable = true;
            _resourceLoader = null;
            return key;
        }

        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    public string GetString(string key, string fallback)
    {
        var value = GetString(key);
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private ResourceLoader? GetOrCreateResourceLoader()
    {
        if (_resourceLoaderUnavailable)
        {
            return null;
        }

        if (_resourceLoader is not null)
        {
            return _resourceLoader;
        }

        try
        {
            _resourceLoader = new ResourceLoader();
            return _resourceLoader;
        }
        catch
        {
            _resourceLoaderUnavailable = true;
            _resourceLoader = null;
            return null;
        }
    }
}
