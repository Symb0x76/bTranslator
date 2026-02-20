using System.Globalization;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using bTranslator.Infrastructure.Translation.Options;
using bTranslator.Infrastructure.Translation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace bTranslator.App.ViewModels;

public partial class MainViewModel
{
    private const string ApiKeySecretName = "api_key";
    private const string ApiSecretSecretName = "api_secret";
    private const string SessionTokenSecretName = "session_token";

    [ObservableProperty]
    private string providerConfigProviderId = "-";

    [ObservableProperty]
    private string providerBaseUrl = string.Empty;

    [ObservableProperty]
    private string providerModel = string.Empty;

    [ObservableProperty]
    private string providerRegion = string.Empty;

    [ObservableProperty]
    private string providerOrganization = string.Empty;

    [ObservableProperty]
    private string providerPromptTemplate = string.Empty;

    [ObservableProperty]
    private string providerApiKey = string.Empty;

    [ObservableProperty]
    private string providerApiSecret = string.Empty;

    [ObservableProperty]
    private string providerSessionToken = string.Empty;

    [ObservableProperty]
    private string providerLanguageMap = string.Empty;

    partial void OnSelectedProviderOptionChanged(ProviderChainItemViewModel? value)
    {
        _ = LoadSelectedProviderConfigurationAsync();
    }

    [RelayCommand]
    private async Task SaveSelectedProviderConfigurationAsync()
    {
        var providerId = SelectedProviderOption?.ProviderId;
        if (string.IsNullOrWhiteSpace(providerId))
        {
            StatusText = "No provider selected.";
            AddLog(StatusText);
            return;
        }

        try
        {
            var existing = GetOrCreateProviderOptions(providerId);
            var updated = BuildProviderOptionsFromEditor(existing);
            _translationProviderOptions.Providers[providerId] = updated;
            await PersistProviderSettingsAsync(providerId, updated, persistSecrets: true).ConfigureAwait(false);

            StatusText = $"Saved provider settings for '{providerId}'.";
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = $"Save provider settings failed: {ex.Message}";
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task ReloadSelectedProviderConfigurationAsync()
    {
        await LoadSelectedProviderConfigurationAsync().ConfigureAwait(false);
        StatusText = $"Reloaded provider settings for '{ProviderConfigProviderId}'.";
        AddLog(StatusText);
    }

    [RelayCommand]
    private async Task TestSelectedProviderConnectionAsync()
    {
        var providerId = SelectedProviderOption?.ProviderId;
        if (string.IsNullOrWhiteSpace(providerId))
        {
            StatusText = "No provider selected.";
            AddLog(StatusText);
            return;
        }

        try
        {
            var existing = GetOrCreateProviderOptions(providerId);
            var updated = BuildProviderOptionsFromEditor(existing);
            _translationProviderOptions.Providers[providerId] = updated;

            var provider = _providers.FirstOrDefault(p =>
                string.Equals(p.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
            if (provider is null)
            {
                StatusText = $"Provider '{providerId}' is not registered.";
                AddLog(StatusText);
                return;
            }

            var result = await provider.TranslateBatchAsync(
                new TranslationBatchRequest
                {
                    SourceLanguage = SourceLanguage,
                    TargetLanguage = TargetLanguage,
                    Items = ["Connectivity check"]
                }).ConfigureAwait(false);

            var preview = result.Items.FirstOrDefault() ?? string.Empty;
            if (preview.Length > 64)
            {
                preview = preview[..64] + "...";
            }

            StatusText = $"Provider '{providerId}' connectivity OK.";
            AddLog($"{StatusText} Sample: {preview}");
        }
        catch (TranslationProviderException ex)
        {
            StatusText = $"Provider '{providerId}' test failed ({ex.ErrorKind}): {ex.Message}";
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = $"Provider '{providerId}' test failed: {ex.Message}";
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task ImportLegacyApiTranslatorConfigAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = "ApiTranslator config path is empty or file does not exist.";
            AddLog(StatusText);
            return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var imported = ApiTranslatorConfigImporter.ImportFromIniLikeContent(content);
            if (imported.Providers.Count == 0)
            {
                StatusText = "No provider settings found in ApiTranslator config.";
                AddLog(StatusText);
                return;
            }

            var applied = 0;
            foreach (var pair in imported.Providers)
            {
                var merged = MergeImportedProviderOptions(pair.Key, pair.Value);
                _translationProviderOptions.Providers[pair.Key] = merged;
                await PersistProviderSettingsAsync(pair.Key, merged, persistSecrets: true).ConfigureAwait(false);
                applied++;
            }

            await LoadSelectedProviderConfigurationAsync().ConfigureAwait(false);
            StatusText = $"Imported ApiTranslator config from '{Path.GetFileName(path)}' ({applied} providers).";
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = $"Import ApiTranslator config failed: {ex.Message}";
            AddLog(StatusText);
        }
    }

    private async Task LoadPersistedProviderSettingsAsync()
    {
        foreach (var providerId in RegisteredProviders)
        {
            var existing = GetOrCreateProviderOptions(providerId);
            var languageMapText = await ReadOptionalSettingAsync(providerId, "language_map").ConfigureAwait(false);
            var languageMap = ParseLanguageMap(languageMapText, existing.LanguageMap);

            var metadata = new Dictionary<string, string>(existing.Metadata, StringComparer.OrdinalIgnoreCase);
            var token = NormalizeOptional(await _credentialStore.GetSecretAsync(providerId, SessionTokenSecretName).ConfigureAwait(false));
            if (token is null)
            {
                metadata.Remove("token");
            }
            else
            {
                metadata["token"] = token;
            }

            var maxCharsPerRequest = await ReadOptionalIntSettingAsync(providerId, "max_chars_per_request").ConfigureAwait(false);
            var maxItemsPerBatch = await ReadOptionalIntSettingAsync(providerId, "max_items_per_batch").ConfigureAwait(false);
            var requestsPerMinute = await ReadOptionalIntSettingAsync(providerId, "requests_per_minute").ConfigureAwait(false);
            var charsPerMinute = await ReadOptionalIntSettingAsync(providerId, "chars_per_minute").ConfigureAwait(false);

            var capabilities = new ProviderCapabilities
            {
                MaxCharsPerRequest = maxCharsPerRequest ?? existing.Capabilities.MaxCharsPerRequest,
                MaxItemsPerBatch = maxItemsPerBatch ?? existing.Capabilities.MaxItemsPerBatch,
                RequestsPerMinute = requestsPerMinute ?? existing.Capabilities.RequestsPerMinute,
                CharsPerMinute = charsPerMinute ?? existing.Capabilities.CharsPerMinute,
                SupportsBatch = existing.Capabilities.SupportsBatch
            };

            _translationProviderOptions.Providers[providerId] = new ProviderEndpointOptions
            {
                BaseUrl = await ReadOptionalSettingAsync(providerId, "base_url").ConfigureAwait(false) ?? existing.BaseUrl,
                Model = await ReadOptionalSettingAsync(providerId, "model").ConfigureAwait(false) ?? existing.Model,
                ApiKey = NormalizeOptional(await _credentialStore.GetSecretAsync(providerId, ApiKeySecretName).ConfigureAwait(false)) ?? existing.ApiKey,
                ApiSecret = NormalizeOptional(await _credentialStore.GetSecretAsync(providerId, ApiSecretSecretName).ConfigureAwait(false)) ?? existing.ApiSecret,
                Region = await ReadOptionalSettingAsync(providerId, "region").ConfigureAwait(false) ?? existing.Region,
                Organization = await ReadOptionalSettingAsync(providerId, "organization").ConfigureAwait(false) ?? existing.Organization,
                PromptTemplate = await ReadOptionalSettingAsync(providerId, "prompt_template").ConfigureAwait(false) ?? existing.PromptTemplate,
                LanguageMap = languageMap,
                Metadata = metadata,
                Capabilities = capabilities
            };
        }
    }

    private async Task LoadSelectedProviderConfigurationAsync()
    {
        var providerId = SelectedProviderOption?.ProviderId;
        if (string.IsNullOrWhiteSpace(providerId))
        {
            ClearProviderEditor();
            return;
        }

        try
        {
            var options = GetOrCreateProviderOptions(providerId);
            var apiKey = NormalizeOptional(await _credentialStore.GetSecretAsync(providerId, ApiKeySecretName).ConfigureAwait(false)) ?? options.ApiKey;
            var apiSecret = NormalizeOptional(await _credentialStore.GetSecretAsync(providerId, ApiSecretSecretName).ConfigureAwait(false)) ?? options.ApiSecret;
            var token = NormalizeOptional(await _credentialStore.GetSecretAsync(providerId, SessionTokenSecretName).ConfigureAwait(false));

            ProviderConfigProviderId = providerId;
            ProviderBaseUrl = options.BaseUrl ?? string.Empty;
            ProviderModel = options.Model ?? string.Empty;
            ProviderRegion = options.Region ?? string.Empty;
            ProviderOrganization = options.Organization ?? string.Empty;
            ProviderPromptTemplate = options.PromptTemplate ?? string.Empty;
            ProviderApiKey = apiKey ?? string.Empty;
            ProviderApiSecret = apiSecret ?? string.Empty;
            ProviderSessionToken = token ?? (options.Metadata.TryGetValue("token", out var metadataToken) ? metadataToken : string.Empty);
            ProviderLanguageMap = SerializeLanguageMap(options.LanguageMap);
        }
        catch (Exception ex)
        {
            StatusText = $"Load provider settings failed: {ex.Message}";
            AddLog(StatusText);
        }
    }

    private ProviderEndpointOptions BuildProviderOptionsFromEditor(ProviderEndpointOptions existing)
    {
        var languageMap = ParseLanguageMap(ProviderLanguageMap, fallback: null);
        var metadata = new Dictionary<string, string>(existing.Metadata, StringComparer.OrdinalIgnoreCase);
        var token = NormalizeOptional(ProviderSessionToken);
        if (token is null)
        {
            metadata.Remove("token");
        }
        else
        {
            metadata["token"] = token;
        }

        return new ProviderEndpointOptions
        {
            BaseUrl = NormalizeOptional(ProviderBaseUrl),
            Model = NormalizeOptional(ProviderModel),
            ApiKey = NormalizeOptional(ProviderApiKey),
            ApiSecret = NormalizeOptional(ProviderApiSecret),
            Region = NormalizeOptional(ProviderRegion),
            Organization = NormalizeOptional(ProviderOrganization),
            PromptTemplate = NormalizeOptional(ProviderPromptTemplate),
            LanguageMap = languageMap,
            Metadata = metadata,
            Capabilities = existing.Capabilities
        };
    }

    private ProviderEndpointOptions MergeImportedProviderOptions(
        string providerId,
        ProviderEndpointOptions imported)
    {
        var existing = GetOrCreateProviderOptions(providerId);

        var languageMap = new Dictionary<string, string>(existing.LanguageMap, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in imported.LanguageMap)
        {
            languageMap[pair.Key] = pair.Value;
        }

        var metadata = new Dictionary<string, string>(existing.Metadata, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in imported.Metadata)
        {
            metadata[pair.Key] = pair.Value;
        }

        return new ProviderEndpointOptions
        {
            BaseUrl = NormalizeOptional(imported.BaseUrl) ?? existing.BaseUrl,
            Model = NormalizeOptional(imported.Model) ?? existing.Model,
            ApiKey = NormalizeOptional(imported.ApiKey) ?? existing.ApiKey,
            ApiSecret = NormalizeOptional(imported.ApiSecret) ?? existing.ApiSecret,
            Region = NormalizeOptional(imported.Region) ?? existing.Region,
            Organization = NormalizeOptional(imported.Organization) ?? existing.Organization,
            PromptTemplate = NormalizeOptional(imported.PromptTemplate) ?? existing.PromptTemplate,
            LanguageMap = languageMap,
            Metadata = metadata,
            Capabilities = new ProviderCapabilities
            {
                MaxCharsPerRequest = imported.Capabilities.MaxCharsPerRequest,
                MaxItemsPerBatch = imported.Capabilities.MaxItemsPerBatch,
                RequestsPerMinute = imported.Capabilities.RequestsPerMinute,
                CharsPerMinute = imported.Capabilities.CharsPerMinute,
                SupportsBatch = imported.Capabilities.SupportsBatch
            }
        };
    }

    private async Task PersistProviderSettingsAsync(
        string providerId,
        ProviderEndpointOptions options,
        bool persistSecrets)
    {
        await _settingsStore.SetAsync(BuildProviderSettingKey(providerId, "base_url"), options.BaseUrl ?? string.Empty).ConfigureAwait(false);
        await _settingsStore.SetAsync(BuildProviderSettingKey(providerId, "model"), options.Model ?? string.Empty).ConfigureAwait(false);
        await _settingsStore.SetAsync(BuildProviderSettingKey(providerId, "region"), options.Region ?? string.Empty).ConfigureAwait(false);
        await _settingsStore.SetAsync(BuildProviderSettingKey(providerId, "organization"), options.Organization ?? string.Empty).ConfigureAwait(false);
        await _settingsStore.SetAsync(BuildProviderSettingKey(providerId, "prompt_template"), options.PromptTemplate ?? string.Empty).ConfigureAwait(false);
        await _settingsStore.SetAsync(BuildProviderSettingKey(providerId, "language_map"), SerializeLanguageMap(options.LanguageMap)).ConfigureAwait(false);

        await _settingsStore.SetAsync(BuildProviderSettingKey(providerId, "max_chars_per_request"), options.Capabilities.MaxCharsPerRequest.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        await _settingsStore.SetAsync(BuildProviderSettingKey(providerId, "max_items_per_batch"), options.Capabilities.MaxItemsPerBatch.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        await _settingsStore.SetAsync(BuildProviderSettingKey(providerId, "requests_per_minute"), options.Capabilities.RequestsPerMinute.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        await _settingsStore.SetAsync(BuildProviderSettingKey(providerId, "chars_per_minute"), options.Capabilities.CharsPerMinute.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);

        if (persistSecrets)
        {
            await _credentialStore.SetSecretAsync(providerId, ApiKeySecretName, options.ApiKey ?? string.Empty).ConfigureAwait(false);
            await _credentialStore.SetSecretAsync(providerId, ApiSecretSecretName, options.ApiSecret ?? string.Empty).ConfigureAwait(false);

            if (options.Metadata.TryGetValue("token", out var token) && !string.IsNullOrWhiteSpace(token))
            {
                await _credentialStore.SetSecretAsync(providerId, SessionTokenSecretName, token).ConfigureAwait(false);
            }
            else
            {
                await _credentialStore.SetSecretAsync(providerId, SessionTokenSecretName, string.Empty).ConfigureAwait(false);
            }
        }
    }

    private ProviderEndpointOptions GetOrCreateProviderOptions(string providerId)
    {
        if (!_translationProviderOptions.Providers.TryGetValue(providerId, out var options))
        {
            options = new ProviderEndpointOptions();
            _translationProviderOptions.Providers[providerId] = options;
        }

        return options;
    }

    private static string BuildProviderSettingKey(string providerId, string suffix)
    {
        return $"providers.{providerId}.{suffix}";
    }

    private async Task<string?> ReadOptionalSettingAsync(string providerId, string suffix)
    {
        var value = await _settingsStore.GetAsync(BuildProviderSettingKey(providerId, suffix)).ConfigureAwait(false);
        return NormalizeOptional(value);
    }

    private async Task<int?> ReadOptionalIntSettingAsync(string providerId, string suffix)
    {
        var raw = await _settingsStore.GetAsync(BuildProviderSettingKey(providerId, suffix)).ConfigureAwait(false);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Dictionary<string, string> ParseLanguageMap(
        string? content,
        IDictionary<string, string>? fallback)
    {
        var result = fallback is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(fallback, StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(content))
        {
            return result;
        }

        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0)
            {
                continue;
            }

            result[key] = value;
        }

        return result;
    }

    private static string SerializeLanguageMap(IDictionary<string, string> map)
    {
        if (map.Count == 0)
        {
            return string.Empty;
        }

        var lines = map
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key}={pair.Value}");
        return string.Join(Environment.NewLine, lines);
    }

    private void ClearProviderEditor()
    {
        ProviderConfigProviderId = "-";
        ProviderBaseUrl = string.Empty;
        ProviderModel = string.Empty;
        ProviderRegion = string.Empty;
        ProviderOrganization = string.Empty;
        ProviderPromptTemplate = string.Empty;
        ProviderApiKey = string.Empty;
        ProviderApiSecret = string.Empty;
        ProviderSessionToken = string.Empty;
        ProviderLanguageMap = string.Empty;
    }
}
