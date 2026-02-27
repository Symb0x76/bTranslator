using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using bTranslator.App.Localization;
using bTranslator.Application.Abstractions;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Models;
using bTranslator.Infrastructure.Translation.Options;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Options;
using CommunityToolkit.Mvvm.Input;

namespace bTranslator.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string PluginSwitcherStateKey = "workspace.plugin_switcher_items";
    private const string WorkspaceEncodingModeKey = "workspace.encoding_mode";
    private const string WorkspaceEncodingNameKey = "workspace.encoding_name";
    private const string WorkspaceEncodingEffectiveKey = "workspace.encoding_effective";
    private const string AiDrawerHeightKey = "ui.ai_drawer_height";
    private const string ShortcutMappingsSettingKey = "ui.shortcut_mappings.v1";
    private const string UiLanguageSettingKey = AppLocalizationService.UiLanguageSettingKey;
    private const string UiThemeSettingKey = "ui.theme";
    private const string ThemeDark = "dark";
    private const string ThemeLight = "light";
    private const string LanguageEnglish = "english";
    private const string LanguageChineseSimplified = "chinese (simplified)";
    private const string LanguageJapanese = "japanese";
    private const string LanguageKorean = "korean";
    private const string ListFilterAll = "all";
    private const string ListFilterStrings = "strings";
    private const string ListFilterDlStrings = "dlstrings";
    private const string ListFilterIlStrings = "ilstrings";
    private const string ListFilterRecord = "record";
    private const string AutoEncodingMode = "auto";
    private const string ManualEncodingMode = "manual";
    private const string DefaultEncodingDisplayName = "UTF-8";
    private const double DefaultAiDrawerHeight = 280d;
    private const double MinAiDrawerHeight = 200d;
    private const double MaxAiDrawerHeight = 620d;
    public const string ShortcutActionOpenWorkspace = "open_workspace";
    public const string ShortcutActionSaveWorkspace = "save_workspace";
    public const string ShortcutActionRunBatchTranslation = "run_batch_translation";
    public const string ShortcutActionRefreshWorkspace = "refresh_workspace";
    public const string ShortcutActionToggleTheme = "toggle_theme";
    public const string ShortcutActionFocusSearch = "focus_search";
    public const string ShortcutActionFocusRows = "focus_rows";
    public const string ShortcutActionFocusInspector = "focus_inspector";
    public const string ShortcutActionFocusAiInput = "focus_ai_input";
    public const string ShortcutActionFocusModelFilter = "focus_model_filter";
    public const string ShortcutActionClearAiChat = "clear_ai_chat";
    public const string ShortcutActionApplyInspector = "apply_inspector";
    public const string ShortcutActionApplyAndNext = "apply_and_next";
    public const string ShortcutActionCopySource = "copy_source";
    public const string ShortcutActionToggleLock = "toggle_lock";
    public const string ShortcutActionMarkValidated = "mark_validated";
    public const string ShortcutActionNextPending = "next_pending";
    public const string ShortcutActionPreviousPending = "previous_pending";
    public const string ShortcutActionShowShortcutHelp = "show_shortcut_help";
    private static readonly string[] SupportedLanguageValues =
    [
        LanguageEnglish,
        LanguageChineseSimplified,
        LanguageJapanese,
        LanguageKorean
    ];

    private static readonly string[] SupportedListFilterValues =
    [
        ListFilterAll,
        ListFilterStrings,
        ListFilterDlStrings,
        ListFilterIlStrings,
        ListFilterRecord
    ];

    private static readonly string[] SupportedThemeValues =
    [
        ThemeDark,
        ThemeLight
    ];

    private static readonly EncodingChoice[] WorkspaceEncodingChoices =
    [
        new("UTF-8", "utf-8"),
        new("UTF-16 LE", "utf-16"),
        new("UTF-16 BE", "utf-16BE"),
        new("Windows-1252", "windows-1252"),
        new("GB18030", "gb18030"),
        new("Shift-JIS", "shift_jis"),
        new("EUC-KR", "euc-kr"),
        new("Big5", "big5")
    ];
    private static readonly ShortcutActionDefinition[] ShortcutActionDefinitions =
    [
        new(
            ShortcutActionOpenWorkspace,
            "Ctrl+O",
            "ShortcutGroup.File",
            "File",
            "ShortcutHelp.Open",
            "Open plugin and load workspace",
            "Shortcut.Open",
            "{0} Open"),
        new(
            ShortcutActionSaveWorkspace,
            "Ctrl+S",
            "ShortcutGroup.File",
            "File",
            "ShortcutHelp.Save",
            "Save current workspace",
            "Shortcut.Save",
            "{0} Save"),
        new(
            ShortcutActionRunBatchTranslation,
            "Ctrl+Shift+T",
            "ShortcutGroup.Translate",
            "Translate",
            "ShortcutHelp.BatchTranslate",
            "Run batch translation via selected model",
            "Shortcut.AiBatch",
            "{0} AI Batch"),
        new(
            ShortcutActionRefreshWorkspace,
            "Ctrl+R",
            "ShortcutGroup.Navigation",
            "Navigation",
            "ShortcutHelp.Refresh",
            "Reload metadata and workspace settings",
            null,
            null),
        new(
            ShortcutActionToggleTheme,
            "Ctrl+Shift+L",
            "ShortcutGroup.Navigation",
            "Navigation",
            "ShortcutHelp.ToggleTheme",
            "Toggle app theme (dark/light)",
            "Shortcut.ToggleTheme",
            "{0} Theme"),
        new(
            ShortcutActionFocusSearch,
            "Ctrl+F",
            "ShortcutGroup.Navigation",
            "Navigation",
            "ShortcutHelp.FocusSearch",
            "Focus search box",
            "Shortcut.Search",
            "{0} Search"),
        new(
            ShortcutActionFocusRows,
            "Ctrl+G",
            "ShortcutGroup.Navigation",
            "Navigation",
            "ShortcutHelp.FocusRows",
            "Focus translation rows list",
            "Shortcut.FocusRows",
            "{0} Rows"),
        new(
            ShortcutActionFocusInspector,
            "Ctrl+I",
            "ShortcutGroup.Navigation",
            "Navigation",
            "ShortcutHelp.FocusInspector",
            "Focus inspector translation editor",
            "Shortcut.FocusInspector",
            "{0} Inspector"),
        new(
            ShortcutActionFocusAiInput,
            "Ctrl+K",
            "ShortcutGroup.Navigation",
            "Navigation",
            "ShortcutHelp.FocusAiInput",
            "Focus AI chat input",
            "Shortcut.FocusAiInput",
            "{0} Copilot"),
        new(
            ShortcutActionFocusModelFilter,
            "Ctrl+Shift+F",
            "ShortcutGroup.Navigation",
            "Navigation",
            "ShortcutHelp.FocusModel",
            "Focus model filter",
            "Shortcut.FocusModel",
            "{0} Model"),
        new(
            ShortcutActionClearAiChat,
            "Ctrl+Shift+K",
            "ShortcutGroup.Editing",
            "Editing",
            "ShortcutHelp.ClearAiChat",
            "Clear Copilot conversation history",
            "Shortcut.ClearAiChat",
            "{0} Clear Copilot"),
        new(
            ShortcutActionApplyInspector,
            "Ctrl+Enter",
            "ShortcutGroup.Editing",
            "Editing",
            "ShortcutHelp.ApplyInspector",
            "Apply inspector translation to current row",
            "Shortcut.Apply",
            "{0} Apply"),
        new(
            ShortcutActionApplyAndNext,
            "Ctrl+Shift+Enter",
            "ShortcutGroup.Editing",
            "Editing",
            "ShortcutHelp.ApplyAndNext",
            "Apply and jump to next pending row",
            "Shortcut.ApplyNext",
            "{0} Apply+Next"),
        new(
            ShortcutActionCopySource,
            "Ctrl+Shift+C",
            "ShortcutGroup.Editing",
            "Editing",
            "ShortcutHelp.CopySource",
            "Copy source text into translation box",
            "Shortcut.CopySource",
            "{0} CopySource"),
        new(
            ShortcutActionToggleLock,
            "Ctrl+L",
            "ShortcutGroup.Editing",
            "Editing",
            "ShortcutHelp.ToggleLock",
            "Toggle lock state for selected row",
            "Shortcut.ToggleLock",
            "{0} Lock"),
        new(
            ShortcutActionMarkValidated,
            "Ctrl+M",
            "ShortcutGroup.Editing",
            "Editing",
            "ShortcutHelp.MarkValidated",
            "Mark selected row as validated",
            "Shortcut.Validate",
            "{0} Validate"),
        new(
            ShortcutActionNextPending,
            "F8",
            "ShortcutGroup.Navigation",
            "Navigation",
            "ShortcutHelp.NextPending",
            "Select next pending row",
            "Shortcut.NextPending",
            "{0} NextPending"),
        new(
            ShortcutActionPreviousPending,
            "Shift+F8",
            "ShortcutGroup.Navigation",
            "Navigation",
            "ShortcutHelp.PreviousPending",
            "Select previous pending row",
            "Shortcut.PrevPending",
            "{0} PrevPending"),
        new(
            ShortcutActionShowShortcutHelp,
            "F1",
            "ShortcutGroup.Navigation",
            "Navigation",
            "ShortcutHelp.ShowHelp",
            "Open this keyboard shortcut help",
            "Shortcut.Help",
            "{0} Help")
    ];
    private static readonly IReadOnlyDictionary<string, ShortcutActionDefinition> ShortcutActionDefinitionMap =
        ShortcutActionDefinitions.ToDictionary(
            static definition => definition.ActionId,
            static definition => definition,
            StringComparer.OrdinalIgnoreCase);

    private readonly IEnumerable<ITranslationProvider> _providers;
    private readonly IStringsCodec _stringsCodec;
    private readonly IPlaceholderProtector _placeholderProtector;
    private readonly ISettingsStore _settingsStore;
    private readonly ICredentialStore _credentialStore;
    private readonly IAppLocalizationService _localizationService;
    private readonly TranslationProviderOptions _translationProviderOptions;
    private readonly IPluginDocumentService _pluginDocumentService;
    private readonly ITranslationOrchestrator _translationOrchestrator;
    private readonly IPexToolchainService _pexToolchainService;
    private readonly IXmlCompatibilityService _xmlCompatibilityService;
    private readonly ISstCompatibilityService _sstCompatibilityService;
    private readonly IBatchScriptEngine _batchScriptEngine;
    private readonly List<TranslationRowViewModel> _allRows = [];
    private readonly Dictionary<TranslationRowViewModel, StringsEntryBinding> _stringEntryBindings = new();
    private readonly Dictionary<TranslationRowViewModel, TranslationItem> _recordItemBindings = new();
    private readonly Dictionary<string, string> _shortcutGestureOverrides = new(StringComparer.OrdinalIgnoreCase);
    private PluginDocument? _currentDocument;
    private Encoding _activeEncoding = Encoding.UTF8;
    private string _activeEncodingDisplay = $"{DefaultEncodingDisplayName} (Auto)";
    private bool _isApplyingUiLanguage;
    private bool _isApplyingTheme;
    private bool _isLoadingWorkspaceSettings;
    private TranslationRowViewModel.QualityLabelSet _rowQualityLabels = TranslationRowViewModel.QualityLabelSet.Default;

    public MainViewModel(
        IEnumerable<ITranslationProvider> providers,
        IStringsCodec stringsCodec,
        IPlaceholderProtector placeholderProtector,
        ISettingsStore settingsStore,
        ICredentialStore credentialStore,
        IAppLocalizationService localizationService,
        IOptions<TranslationProviderOptions> translationProviderOptions,
        IPluginDocumentService pluginDocumentService,
        ITranslationOrchestrator translationOrchestrator,
        IPexToolchainService pexToolchainService,
        IXmlCompatibilityService xmlCompatibilityService,
        ISstCompatibilityService sstCompatibilityService,
        IBatchScriptEngine batchScriptEngine)
    {
        _providers = providers;
        _stringsCodec = stringsCodec;
        _placeholderProtector = placeholderProtector;
        _settingsStore = settingsStore;
        _credentialStore = credentialStore;
        _localizationService = localizationService;
        Ui = new LocalizedUiText(_localizationService);
        _translationProviderOptions = translationProviderOptions.Value;
        _pluginDocumentService = pluginDocumentService;
        _translationOrchestrator = translationOrchestrator;
        _pexToolchainService = pexToolchainService;
        _xmlCompatibilityService = xmlCompatibilityService;
        _sstCompatibilityService = sstCompatibilityService;
        _batchScriptEngine = batchScriptEngine;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AvailableLanguages = new ObservableCollection<string>(SupportedLanguageValues);
        Games = new ObservableCollection<string>(Enum.GetNames<GameKind>());
        ListFilters = new ObservableCollection<string>(SupportedListFilterValues);
        UiLanguages = new ObservableCollection<UiLanguageOption>();
        EncodingModes = new ObservableCollection<string>([AutoEncodingMode, ManualEncodingMode]);
        AvailableLanguageOptions = new ObservableCollection<OptionItem>();
        ListFilterOptions = new ObservableCollection<OptionItem>();
        EncodingModeOptions = new ObservableCollection<OptionItem>();
        ThemeOptions = new ObservableCollection<OptionItem>();
        AvailableEncodings = new ObservableCollection<string>(
            WorkspaceEncodingChoices.Select(static choice => choice.DisplayName));

        BatchQueue.Add("Open plugin and index rows");
        BatchQueue.Add("Filter untranslated + unlockable rows");
        BatchQueue.Add("Execute selected model");
        BatchQueue.Add("Write back records and strings");

        ScriptHints.Add("Scope respects current search and list filter.");
        ScriptHints.Add("Locked rows are skipped.");
        ScriptHints.Add("Placeholder normalization enabled by default.");
        ScriptHints.Add("Save writes both STRINGS and record fields.");

        GlossarySuggestions.Add("ChargeTime -> 蓄力时间");
        GlossarySuggestions.Add("Cooldown -> 冷却时间");
        GlossarySuggestions.Add("PowerArmor -> 动力装甲");
        GlossarySuggestions.Add("DamageResist -> 伤害抗性");

        _isApplyingUiLanguage = true;
        SelectedUiLanguageTag = ResolveCurrentUiLanguageTag();
        _isApplyingUiLanguage = false;
        RefreshUiLanguages();
        RefreshLocalizedOptionCollections();
        RefreshRowQualityLabels();

        WorkspaceTitle = Lf("WorkspaceTitle.Format", "{0} Workspace", SelectedGame);
        ActivePluginName = L("PluginSwitcher.CurrentPlugin", "Current Plugin");
        ProviderChainPreview = L("Status.NoProviderSelected", "No model selected.");
        StatusText = L("Status.Ready", "Ready");
        InspectorHint = L("InspectorHint.SelectRow", "Select a row to review placeholders and terminology consistency.");

        UpdateKeyboardShortcutHint();
        InitializeAiCopilot();
    }

    public ObservableCollection<TranslationRowViewModel> Rows { get; } = [];
    public ObservableCollection<string> AvailableLanguages { get; }
    public ObservableCollection<string> Games { get; }
    public ObservableCollection<string> ListFilters { get; }
    public ObservableCollection<OptionItem> AvailableLanguageOptions { get; }
    public ObservableCollection<OptionItem> ListFilterOptions { get; }
    public ObservableCollection<ProviderChainItemViewModel> ProviderOptions { get; } = [];
    public ObservableCollection<string> RegisteredProviders { get; } = [];
    public ObservableCollection<string> ActivityLogs { get; } = [];
    public ObservableCollection<string> BatchQueue { get; } = [];
    public ObservableCollection<string> ScriptHints { get; } = [];
    public ObservableCollection<string> GlossarySuggestions { get; } = [];
    public ObservableCollection<PluginSwitcherItemViewModel> PluginSwitcherItems { get; } = [];
    public ObservableCollection<PluginSwitcherItemViewModel> FilteredPluginSwitcherItems { get; } = [];
    public ObservableCollection<UiLanguageOption> UiLanguages { get; }
    public ObservableCollection<string> EncodingModes { get; }
    public ObservableCollection<OptionItem> EncodingModeOptions { get; }
    public ObservableCollection<OptionItem> ThemeOptions { get; }
    public ObservableCollection<string> AvailableEncodings { get; }
    public LocalizedUiText Ui { get; }

    [ObservableProperty]
    public partial string WorkspaceTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActivePluginName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedGame { get; set; } = nameof(GameKind.Fallout4);

    [ObservableProperty]
    public partial string PluginPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputPluginPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StringsDirectoryPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RecordDefinitionsPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool LoadStrings { get; set; } = true;

    [ObservableProperty]
    public partial bool LoadRecordFields { get; set; } = true;

    [ObservableProperty]
    public partial string SourceLanguage { get; set; } = LanguageEnglish;

    [ObservableProperty]
    public partial string TargetLanguage { get; set; } = LanguageChineseSimplified;

    [ObservableProperty]
    public partial string SelectedUiLanguageTag { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = ThemeDark;

    [ObservableProperty]
    public partial int UiLanguageMenuVersion { get; set; }

    [ObservableProperty]
    public partial int UiOptionMenuVersion { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedListFilter { get; set; } = ListFilterAll;

    [ObservableProperty]
    public partial bool ShowUntranslatedOnly { get; set; }

    [ObservableProperty]
    public partial bool ShowLocked { get; set; } = true;

    [ObservableProperty]
    public partial ProviderChainItemViewModel? SelectedProviderOption { get; set; }

    [ObservableProperty]
    public partial string PluginSwitcherSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProviderChainPreview { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalEntries { get; set; }

    [ObservableProperty]
    public partial int TranslatedEntries { get; set; }

    [ObservableProperty]
    public partial int PendingEntries { get; set; }

    [ObservableProperty]
    public partial double CompletionPercent { get; set; }

    [ObservableProperty]
    public partial TranslationRowViewModel? SelectedRow { get; set; }

    [ObservableProperty]
    public partial string InspectorRecordId { get; set; } = "-";

    [ObservableProperty]
    public partial string InspectorField { get; set; } = "-";

    [ObservableProperty]
    public partial string InspectorSourceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InspectorTranslatedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InspectorHint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedGlossarySuggestion { get; set; }

    [ObservableProperty]
    public partial string SelectedEncodingMode { get; set; } = AutoEncodingMode;

    [ObservableProperty]
    public partial string SelectedEncodingName { get; set; } = DefaultEncodingDisplayName;

    [ObservableProperty]
    public partial string EffectiveEncodingDisplay { get; set; } = $"{DefaultEncodingDisplayName} (Auto)";

    [ObservableProperty]
    public partial string KeyboardShortcutHint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double AiDrawerHeight { get; set; } = DefaultAiDrawerHeight;

    private PluginSwitcherItemViewModel? _selectedPluginSwitcherItem;

    public PluginSwitcherItemViewModel? SelectedPluginSwitcherItem
    {
        get => _selectedPluginSwitcherItem;
        set
        {
            if (SetProperty(ref _selectedPluginSwitcherItem, value) && value is not null)
            {
                ActivePluginName = value.DisplayName;
            }
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedListFilterChanged(string value) => ApplyFilters();
    partial void OnShowUntranslatedOnlyChanged(bool value) => ApplyFilters();
    partial void OnShowLockedChanged(bool value) => ApplyFilters();
    partial void OnSelectedUiLanguageTagChanged(string value) => _ = ApplyUiLanguageSelectionAsync(value);
    partial void OnSelectedThemeChanged(string value) => _ = ApplyThemeSelectionAsync(value);
    partial void OnPluginSwitcherSearchTextChanged(string value) => RefreshPluginSwitcherItems();
    partial void OnSelectedEncodingModeChanged(string value)
    {
        UpdateEncodingDisplayFallback();
        if (!_isLoadingWorkspaceSettings)
        {
            _ = PersistEncodingPreferenceAsync();
        }
    }

    partial void OnSelectedEncodingNameChanged(string value)
    {
        UpdateEncodingDisplayFallback();
        if (!_isLoadingWorkspaceSettings)
        {
            _ = PersistEncodingPreferenceAsync();
        }
    }

    partial void OnSelectedRowChanged(TranslationRowViewModel? value)
    {
        UpdateInspectorFromSelection(value);
        UpdateKeyboardShortcutHint();
        AddScopedDictionaryEntryFromSelectedRowCommand.NotifyCanExecuteChanged();
    }

    partial void OnInspectorTranslatedTextChanged(string value)
    {
        if (SelectedRow is null)
        {
            return;
        }

        if (!string.Equals(SelectedRow.TranslatedText, value, StringComparison.Ordinal))
        {
            SelectedRow.TranslatedText = value;
            SelectedRow.IsValidated = false;
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        LoadProviders();
        await LoadShortcutMappingsAsync().ConfigureAwait(false);
        await LoadAiDrawerHeightAsync().ConfigureAwait(false);
        await LoadWorkspaceSettingsAsync().ConfigureAwait(false);
        await LoadPluginSwitcherStateAsync().ConfigureAwait(false);
        await LoadPersistedProviderSettingsAsync().ConfigureAwait(false);
        await LoadSelectedProviderConfigurationAsync().ConfigureAwait(false);
        await LoadDictionaryStateAsync().ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(PluginPath))
        {
            var existing = PluginSwitcherItems.FirstOrDefault(item =>
                string.Equals(item.PluginPath, PluginPath, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                SelectedPluginSwitcherItem = existing;
            }
            else
            {
                var bootstrapItem = new PluginSwitcherItemViewModel
                {
                    PluginPath = PluginPath,
                    DisplayName = Path.GetFileName(PluginPath),
                    DirectoryPath = Path.GetDirectoryName(PluginPath) ?? string.Empty,
                    IsPinned = false,
                    LastOpenedUtc = DateTimeOffset.UtcNow
                };
                PluginSwitcherItems.Add(bootstrapItem);
                SortPluginSwitcherItemsInPlace();
                RefreshPluginSwitcherItems();
                SelectedPluginSwitcherItem = bootstrapItem;
                await SavePluginSwitcherStateAsync().ConfigureAwait(false);
            }
        }

        var sample = "Hello <Alias=Hero>, your score is 42.\nLine2";
        var protectedText = _placeholderProtector.Protect(sample);
        var restored = _placeholderProtector.Restore(protectedText.Text, protectedText.Map);

        if (restored != sample)
        {
            StatusText = L("Status.PlaceholderCheckFailed", "Placeholder pipeline check failed.");
            AddLog(StatusText);
            return;
        }

        StatusText = L("Status.MetadataRefreshed", "Metadata refreshed.");
        AddLog(StatusText);
    }

    [RelayCommand]
    private async Task OpenWorkspaceAsync()
    {
        if (string.IsNullOrWhiteSpace(PluginPath) || !File.Exists(PluginPath))
        {
            StatusText = L("Status.PluginPathMissing", "Plugin path is empty or file does not exist.");
            AddLog(StatusText);
            return;
        }

        try
        {
            var game = ParseGameKind(SelectedGame);
            var encodingResolution = await ResolveWorkspaceEncodingAsync(
                PluginPath,
                ToLanguageToken(SourceLanguage),
                NormalizeOptionalPath(StringsDirectoryPath)).ConfigureAwait(false);
            var options = new PluginOpenOptions
            {
                Language = ToLanguageToken(SourceLanguage),
                StringsDirectory = NormalizeOptionalPath(StringsDirectoryPath),
                RecordDefinitionsPath = NormalizeOptionalPath(RecordDefinitionsPath),
                LoadStrings = LoadStrings,
                LoadRecordFields = LoadRecordFields,
                Encoding = encodingResolution.Encoding
            };

            _currentDocument = await _pluginDocumentService.OpenAsync(game, PluginPath, options).ConfigureAwait(false);
            _activeEncoding = encodingResolution.Encoding;
            _activeEncodingDisplay = encodingResolution.Display;
            EffectiveEncodingDisplay = encodingResolution.Display;
            ActivePluginName = _currentDocument.PluginName + Path.GetExtension(_currentDocument.PluginPath);
            WorkspaceTitle = Lf("WorkspaceTitle.Format", "{0} Workspace", game);
            if (string.IsNullOrWhiteSpace(OutputPluginPath))
            {
                OutputPluginPath = PluginPath;
            }
            await RegisterPluginSwitcherItemAsync(
                _currentDocument.PluginPath,
                Path.GetFileName(_currentDocument.PluginPath))
                .ConfigureAwait(false);

            await _settingsStore.SetAsync("workspace.plugin_path", PluginPath).ConfigureAwait(false);
            await _settingsStore.SetAsync("workspace.output_path", OutputPluginPath).ConfigureAwait(false);
            await _settingsStore.SetAsync("workspace.game", SelectedGame).ConfigureAwait(false);
            await _settingsStore.SetAsync(WorkspaceEncodingModeKey, SelectedEncodingMode).ConfigureAwait(false);
            await _settingsStore.SetAsync(WorkspaceEncodingNameKey, SelectedEncodingName).ConfigureAwait(false);
            await _settingsStore.SetAsync(WorkspaceEncodingEffectiveKey, EffectiveEncodingDisplay).ConfigureAwait(false);

            RebuildRowsFromDocument(_currentDocument);
            StatusText = Lf(
                "Status.OpenedPlugin",
                "Opened '{0}' ({1}).",
                ActivePluginName,
                EffectiveEncodingDisplay);
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.OpenFailed", "Open failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task SaveWorkspaceAsync()
    {
        if (_currentDocument is null)
        {
            StatusText = L("Status.NoActivePlugin", "No active plugin document. Open a plugin first.");
            AddLog(StatusText);
            return;
        }

        try
        {
            ApplyRowsToDocument();
            var outputPath = string.IsNullOrWhiteSpace(OutputPluginPath)
                ? _currentDocument.PluginPath
                : OutputPluginPath;
            var saveEncodingResolution = ResolveWorkspaceSaveEncoding();
            var options = new PluginSaveOptions
            {
                Language = ToLanguageToken(TargetLanguage),
                OutputStringsDirectory = NormalizeOptionalPath(StringsDirectoryPath),
                SaveStrings = LoadStrings,
                SaveRecordFields = LoadRecordFields,
                Encoding = saveEncodingResolution.Encoding
            };

            await _pluginDocumentService.SaveAsync(_currentDocument, outputPath, options).ConfigureAwait(false);
            _activeEncoding = saveEncodingResolution.Encoding;
            _activeEncodingDisplay = saveEncodingResolution.Display;
            EffectiveEncodingDisplay = saveEncodingResolution.Display;
            PluginPath = outputPath;
            ActivePluginName = Path.GetFileName(outputPath);
            await RegisterPluginSwitcherItemAsync(outputPath, ActivePluginName).ConfigureAwait(false);
            await _settingsStore.SetAsync("workspace.output_path", outputPath).ConfigureAwait(false);
            await _settingsStore.SetAsync(WorkspaceEncodingModeKey, SelectedEncodingMode).ConfigureAwait(false);
            await _settingsStore.SetAsync(WorkspaceEncodingNameKey, SelectedEncodingName).ConfigureAwait(false);
            await _settingsStore.SetAsync(WorkspaceEncodingEffectiveKey, EffectiveEncodingDisplay).ConfigureAwait(false);

            StatusText = Lf(
                "Status.SavedWorkspace",
                "Saved workspace to '{0}' ({1}).",
                outputPath,
                EffectiveEncodingDisplay);
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.SaveFailed", "Save failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task RunBatchTranslation()
    {
        var candidates = _allRows
            .Where(static row => !row.IsLocked && row.IsUntranslated)
            .ToList();
        if (candidates.Count == 0)
        {
            StatusText = L("Status.NoPendingRows", "No pending rows to translate.");
            AddLog(StatusText);
            return;
        }

        var chain = BuildProviderChain();
        if (chain.Count == 0)
        {
            StatusText = L("Status.NoAvailableProvider", "No model selected or provider unavailable.");
            AddLog(StatusText);
            return;
        }

        try
        {
            var dictionaryReplacementMap = new Dictionary<string, IReadOnlyList<TranslationDictionaryTokenReplacement>>(
                StringComparer.Ordinal);
            var dictionaryPreparedRowCount = 0;
            var requestItems = new List<TranslationItem>(candidates.Count);
            foreach (var row in candidates)
            {
                var preparedSource = row.SourceText;
                if (IsDictionaryPreReplaceEnabled && DictionaryEntryCount > 0)
                {
                    var preReplacement = PrepareSourceWithDictionary(
                        row.SourceText,
                        row.EditorId,
                        row.FieldSignature);
                    preparedSource = preReplacement.PreparedSource;
                    if (preReplacement.Replacements.Count > 0)
                    {
                        dictionaryReplacementMap[row.RowKey] = preReplacement.Replacements;
                        dictionaryPreparedRowCount++;
                    }
                }

                requestItems.Add(new TranslationItem
                {
                    Id = row.RowKey,
                    SourceText = preparedSource,
                    TranslatedText = row.TranslatedText,
                    IsLocked = row.IsLocked,
                    IsValidated = row.IsValidated
                });
            }

            if (dictionaryPreparedRowCount > 0)
            {
                AddLog(Lf(
                    "Log.DictionaryPreReplaceRows",
                    "Dictionary pre-replace prepared {0} rows before AI translation.",
                    dictionaryPreparedRowCount));
            }

            var result = await _translationOrchestrator.ExecuteAsync(
                new TranslationJob
                {
                    SourceLanguage = SourceLanguage,
                    TargetLanguage = TargetLanguage,
                    ProviderChain = chain,
                    Items = requestItems,
                    NormalizePlaceholders = true
                },
                new OrchestratorPolicy
                {
                    FailOnAuthenticationError = false
                }).ConfigureAwait(false);

            var translatedMap = result.Items.ToDictionary(static x => x.Id, StringComparer.Ordinal);
            foreach (var row in candidates)
            {
                if (!translatedMap.TryGetValue(row.RowKey, out var item))
                {
                    continue;
                }

                var translated = item.TranslatedText ?? row.SourceText;
                if (dictionaryReplacementMap.TryGetValue(row.RowKey, out var replacements))
                {
                    translated = RestoreDictionaryTokens(translated, replacements);
                }

                row.TranslatedText = translated;
                row.IsValidated = false;
            }

            RecalculateMetrics();
            ApplyFilters();
            StatusText = Lf(
                "Status.BatchTranslationDone",
                "Batch translation done by '{0}', updated {1} rows.",
                result.ProviderId,
                candidates.Count);
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.BatchTranslationFailed", "Batch translation failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task ActivatePluginSwitcherItemAsync(PluginSwitcherItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (!File.Exists(item.PluginPath))
        {
            StatusText = Lf("Status.PluginFileNotFound", "Plugin file not found: '{0}'.", item.PluginPath);
            AddLog(StatusText);
            PluginSwitcherItems.Remove(item);
            RefreshPluginSwitcherItems();
            SelectedPluginSwitcherItem = PluginSwitcherItems.FirstOrDefault();
            await SavePluginSwitcherStateAsync().ConfigureAwait(false);
            return;
        }

        PluginPath = item.PluginPath;
        if (string.IsNullOrWhiteSpace(OutputPluginPath))
        {
            OutputPluginPath = item.PluginPath;
        }

        await OpenWorkspaceAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task TogglePluginPinAsync(PluginSwitcherItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        item.IsPinned = !item.IsPinned;
        SortPluginSwitcherItemsInPlace();
        RefreshPluginSwitcherItems();
        await SavePluginSwitcherStateAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private void ApplyInspectorToRow()
    {
        if (SelectedRow is null)
        {
            return;
        }

        SelectedRow.TranslatedText = InspectorTranslatedText;
        SelectedRow.IsValidated = false;
        AddLog(Lf("Log.AppliedManualEdit", "Applied manual edit to '{0}'.", SelectedRow.EditorId));
    }

    [RelayCommand]
    private void CopySourceToTarget()
    {
        if (SelectedRow is null)
        {
            return;
        }

        InspectorTranslatedText = SelectedRow.SourceText;
        AddLog(Lf("Log.CopiedSourceToTarget", "Copied source text to target for '{0}'.", SelectedRow.EditorId));
    }

    [RelayCommand]
    private void MarkSelectedValidated()
    {
        if (SelectedRow is null)
        {
            return;
        }

        SelectedRow.IsValidated = true;
        AddLog(Lf("Log.MarkedValidated", "Marked '{0}' as validated.", SelectedRow.EditorId));
    }

    [RelayCommand]
    private void ToggleSelectedLock()
    {
        if (SelectedRow is null)
        {
            return;
        }

        SelectedRow.IsLocked = !SelectedRow.IsLocked;
        AddLog(SelectedRow.IsLocked
            ? Lf("Log.RowLocked", "Locked '{0}'.", SelectedRow.EditorId)
            : Lf("Log.RowUnlocked", "Unlocked '{0}'.", SelectedRow.EditorId));
        RecalculateMetrics();
        ApplyFilters();
    }

    [RelayCommand]
    private void ApplyInspectorAndSelectNextPending()
    {
        ApplyInspectorToRow();
        SelectNextPendingRow();
    }

    [RelayCommand]
    private void SelectNextPendingRow()
    {
        SelectPendingRow(forward: true);
    }

    [RelayCommand]
    private void SelectPreviousPendingRow()
    {
        SelectPendingRow(forward: false);
    }

    [RelayCommand]
    private void ApplySelectedGlossarySuggestion()
    {
        if (SelectedRow is null || string.IsNullOrWhiteSpace(SelectedGlossarySuggestion))
        {
            return;
        }

        var parts = SelectedGlossarySuggestion.Split("->", 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            SelectedRow.SourceText.Contains(parts[0], StringComparison.OrdinalIgnoreCase))
        {
            InspectorTranslatedText = SelectedRow.TranslatedText.Replace(parts[0], parts[1], StringComparison.OrdinalIgnoreCase);
            AddLog(Lf(
                "Log.AppliedGlossarySuggestion",
                "Applied glossary suggestion '{0}' to '{1}'.",
                SelectedGlossarySuggestion,
                SelectedRow.EditorId));
            return;
        }

        InspectorHint = L(
            "InspectorHint.GlossaryMismatch",
            "Selected glossary term does not match current source text.");
    }

    private async Task LoadWorkspaceSettingsAsync()
    {
        _isLoadingWorkspaceSettings = true;
        try
        {
            PluginPath = await _settingsStore.GetAsync("workspace.plugin_path").ConfigureAwait(false) ?? PluginPath;
            OutputPluginPath = await _settingsStore.GetAsync("workspace.output_path").ConfigureAwait(false) ?? OutputPluginPath;
            var uiLanguageTag = await _settingsStore.GetAsync(UiLanguageSettingKey).ConfigureAwait(false);
            if (uiLanguageTag is not null)
            {
                var normalizedUiLanguage = _localizationService.NormalizeLanguageTag(uiLanguageTag);
                if (!string.Equals(SelectedUiLanguageTag, normalizedUiLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    _isApplyingUiLanguage = true;
                    SelectedUiLanguageTag = normalizedUiLanguage;
                    _isApplyingUiLanguage = false;
                }
            }

            var selectedTheme = await _settingsStore.GetAsync(UiThemeSettingKey).ConfigureAwait(false);
            SelectedTheme = NormalizeThemeValue(selectedTheme);

            var game = await _settingsStore.GetAsync("workspace.game").ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(game) && Games.Contains(game))
            {
                SelectedGame = game;
                WorkspaceTitle = Lf("WorkspaceTitle.Format", "{0} Workspace", SelectedGame);
            }

            var encodingMode = await _settingsStore.GetAsync(WorkspaceEncodingModeKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(encodingMode))
            {
                var normalizedEncodingMode = NormalizeEncodingModeValue(encodingMode);
                if (EncodingModes.Any(mode => string.Equals(mode, normalizedEncodingMode, StringComparison.Ordinal)))
                {
                    SelectedEncodingMode = normalizedEncodingMode;
                }
            }

            var encodingName = await _settingsStore.GetAsync(WorkspaceEncodingNameKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(encodingName) &&
                AvailableEncodings.Any(name => string.Equals(name, encodingName, StringComparison.Ordinal)))
            {
                SelectedEncodingName = encodingName;
            }

            var effective = await _settingsStore.GetAsync(WorkspaceEncodingEffectiveKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(effective))
            {
                _activeEncodingDisplay = effective;
            }

            UpdateEncodingDisplayFallback();
        }
        finally
        {
            _isLoadingWorkspaceSettings = false;
        }
    }

    private void LoadProviders()
    {
        RegisteredProviders.Clear();
        ProviderOptions.Clear();

        var orderedProviderIds = _providers
            .Select(static x => x.ProviderId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var preferred = orderedProviderIds.FirstOrDefault(static id =>
            string.Equals(id, "openai-compatible", StringComparison.OrdinalIgnoreCase)) ??
                        orderedProviderIds.FirstOrDefault(static id =>
                            string.Equals(id, "ollama", StringComparison.OrdinalIgnoreCase)) ??
                        orderedProviderIds.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            orderedProviderIds.RemoveAll(id => string.Equals(id, preferred, StringComparison.OrdinalIgnoreCase));
            orderedProviderIds.Insert(0, preferred);
        }

        foreach (var providerId in orderedProviderIds)
        {
            RegisteredProviders.Add(providerId);
            var option = new ProviderChainItemViewModel
            {
                ProviderId = providerId
            };
            ProviderOptions.Add(option);
        }

        RefreshProviderModelOptions();
        SelectedProviderOption = ProviderOptions.FirstOrDefault();
        UpdateProviderChainPreview();
        UpdateKeyboardShortcutHint();
    }

    private void RebuildRowsFromDocument(PluginDocument document)
    {
        ResetCompatibilityModeState();

        foreach (var row in _allRows)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        _allRows.Clear();
        _stringEntryBindings.Clear();
        _recordItemBindings.Clear();

        foreach (var pair in document.StringTables.OrderBy(static x => x.Key))
        {
            foreach (var entry in pair.Value.OrderBy(static x => x.Id))
            {
                var kindLabel = ToListLabel(pair.Key);
                var row = new TranslationRowViewModel
                {
                    RowKey = $"str:{pair.Key}:{entry.Id:X8}",
                    EditorId = $"{entry.Id:X8}",
                    FieldSignature = kindLabel,
                    SourceText = entry.Text,
                    TranslatedText = entry.Text,
                    ListKind = kindLabel,
                    LdScore = 0,
                    IsLocked = false,
                    IsValidated = false
                };
                row.ApplyQualityLabels(_rowQualityLabels);
                row.PropertyChanged += OnRowChanged;
                _allRows.Add(row);
                _stringEntryBindings[row] = new StringsEntryBinding
                {
                    Kind = pair.Key,
                    Entry = entry
                };
            }
        }

        var recordIndex = 0;
        foreach (var item in document.RecordItems)
        {
            var metadata = item.PluginFieldMetadata;
            var row = new TranslationRowViewModel
            {
                RowKey = $"rec:{recordIndex++}:{item.Id}",
                EditorId = metadata is null
                    ? item.Id
                    : $"{metadata.RecordSignature}:{metadata.FormId:X8}",
                FieldSignature = metadata?.FieldSignature ?? item.Id,
                SourceText = item.SourceText,
                TranslatedText = item.TranslatedText ?? item.SourceText,
                ListKind = ToListLabel(metadata?.ListIndex),
                LdScore = EstimateLd(item.SourceText, item.TranslatedText ?? item.SourceText),
                IsLocked = item.IsLocked,
                IsValidated = item.IsValidated
            };
            row.ApplyQualityLabels(_rowQualityLabels);
            row.PropertyChanged += OnRowChanged;
            _allRows.Add(row);
            _recordItemBindings[row] = item;
        }

        _espCompareCandidates.Clear();
        EspComparePendingReplacementCount = 0;
        EspCompareSummary = string.Empty;
        ApplyEspCompareReplacementsCommand.NotifyCanExecuteChanged();

        _allRows.Sort(static (x, y) => string.CompareOrdinal(x.EditorId, y.EditorId));
        RecalculateMetrics();
        ApplyFilters();
    }

    private void ApplyRowsToDocument()
    {
        foreach (var pair in _stringEntryBindings)
        {
            pair.Value.Entry.Text = pair.Key.TranslatedText;
        }

        foreach (var pair in _recordItemBindings)
        {
            pair.Value.TranslatedText = pair.Key.TranslatedText;
            pair.Value.IsLocked = pair.Key.IsLocked;
            pair.Value.IsValidated = pair.Key.IsValidated;
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TranslationRowViewModel.TranslatedText) or
            nameof(TranslationRowViewModel.IsLocked) or
            nameof(TranslationRowViewModel.IsValidated))
        {
            if (sender is TranslationRowViewModel row)
            {
                row.LdScore = EstimateLd(row.SourceText, row.TranslatedText);
            }

            RecalculateMetrics();
            if (ReferenceEquals(sender, SelectedRow))
            {
                UpdateInspectorFromSelection(SelectedRow);
            }
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allRows.Where(static _ => true);

        if (!string.Equals(SelectedListFilter, ListFilterAll, StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(row =>
                string.Equals(row.ListKind, SelectedListFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (ShowUntranslatedOnly)
        {
            filtered = filtered.Where(static row => row.IsUntranslated);
        }

        if (!ShowLocked)
        {
            filtered = filtered.Where(static row => !row.IsLocked);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(row =>
                row.EditorId.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                row.FieldSignature.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                row.SourceText.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                row.TranslatedText.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        Rows.Clear();
        foreach (var row in filtered)
        {
            Rows.Add(row);
        }

        if (Rows.Count == 0)
        {
            SelectedRow = null;
            return;
        }

        if (SelectedRow is null || !Rows.Contains(SelectedRow))
        {
            SelectedRow = Rows[0];
        }
    }

    private void RecalculateMetrics()
    {
        TotalEntries = _allRows.Count;
        TranslatedEntries = _allRows.Count(static row => !row.IsUntranslated);
        PendingEntries = TotalEntries - TranslatedEntries;
        CompletionPercent = TotalEntries == 0
            ? 0
            : Math.Round(TranslatedEntries * 100d / TotalEntries, 1);
        UpdateKeyboardShortcutHint();
    }

    private void UpdateInspectorFromSelection(TranslationRowViewModel? row)
    {
        if (row is null)
        {
            InspectorRecordId = "-";
            InspectorField = "-";
            InspectorSourceText = string.Empty;
            InspectorTranslatedText = string.Empty;
            InspectorHint = L(
                "InspectorHint.SelectRow",
                "Select a row to review placeholders and terminology consistency.");
            return;
        }

        InspectorRecordId = row.EditorId;
        InspectorField = row.FieldSignature;
        InspectorSourceText = row.SourceText;
        InspectorTranslatedText = row.TranslatedText;
        InspectorHint = row.IsLocked
            ? L("InspectorHint.RowLocked", "Current row is locked. Unlock it before writing translations.")
            : L("InspectorHint.KeepPlaceholders", "Keep tags and number placeholders unchanged during translation.");
    }

    private List<string> BuildProviderChain()
    {
        var selectedProviderId = SelectedProviderOption?.ProviderId;
        if (string.IsNullOrWhiteSpace(selectedProviderId))
        {
            selectedProviderId = ProviderOptions.FirstOrDefault()?.ProviderId;
        }

        return string.IsNullOrWhiteSpace(selectedProviderId)
            ? []
            : [selectedProviderId];
    }

    private void UpdateProviderChainPreview()
    {
        var selected = SelectedProviderOption ?? ProviderOptions.FirstOrDefault();
        ProviderChainPreview = selected is null
            ? L("Status.NoProviderSelected", "No model selected.")
            : selected.DisplayText;
    }

    private void RefreshProviderModelOptions()
    {
        foreach (var option in ProviderOptions)
        {
            var endpoint = GetOrCreateProviderOptions(option.ProviderId);
            option.ModelName = endpoint.Model ?? string.Empty;
        }
    }

    private void SelectPendingRow(bool forward)
    {
        if (Rows.Count == 0)
        {
            return;
        }

        var startIndex = SelectedRow is null ? -1 : Rows.IndexOf(SelectedRow);
        if (startIndex < -1)
        {
            startIndex = -1;
        }

        for (var step = 1; step <= Rows.Count; step++)
        {
            var offset = forward ? step : -step;
            var candidateIndex = (startIndex + offset) % Rows.Count;
            if (candidateIndex < 0)
            {
                candidateIndex += Rows.Count;
            }

            var candidate = Rows[candidateIndex];
            if (candidate.IsLocked || !candidate.IsUntranslated)
            {
                continue;
            }

            SelectedRow = candidate;
            return;
        }
    }

    private void UpdateKeyboardShortcutHint()
    {
        var hints = new List<string>
        {
            BuildShortcutHint("Shortcut.Open", "{0} Open", ShortcutActionOpenWorkspace),
            BuildShortcutHint("Shortcut.Save", "{0} Save", ShortcutActionSaveWorkspace),
            BuildShortcutHint("Shortcut.AiBatch", "{0} AI Batch", ShortcutActionRunBatchTranslation),
            BuildShortcutHint("Shortcut.Search", "{0} Search", ShortcutActionFocusSearch),
            BuildShortcutHint("Shortcut.ToggleTheme", "{0} Theme", ShortcutActionToggleTheme),
            BuildShortcutHint("Shortcut.FocusRows", "{0} Rows", ShortcutActionFocusRows),
            BuildShortcutHint("Shortcut.FocusAiInput", "{0} Copilot", ShortcutActionFocusAiInput),
            BuildShortcutHint("Shortcut.ClearAiChat", "{0} Clear Copilot", ShortcutActionClearAiChat),
            BuildShortcutHint("Shortcut.Help", "{0} Help", ShortcutActionShowShortcutHelp)
        };

        if (SelectedRow is not null)
        {
            hints.Add(BuildShortcutHint("Shortcut.FocusInspector", "{0} Inspector", ShortcutActionFocusInspector));
            hints.Add(BuildShortcutHint("Shortcut.Apply", "{0} Apply", ShortcutActionApplyInspector));
            hints.Add(BuildShortcutHint("Shortcut.ApplyNext", "{0} Apply+Next", ShortcutActionApplyAndNext));
            hints.Add(BuildShortcutHint("Shortcut.CopySource", "{0} CopySource", ShortcutActionCopySource));
            hints.Add(BuildShortcutHint("Shortcut.ToggleLock", "{0} Lock", ShortcutActionToggleLock));
            hints.Add(BuildShortcutHint("Shortcut.Validate", "{0} Validate", ShortcutActionMarkValidated));
        }

        if (ProviderOptions.Count > 0)
        {
            hints.Add(BuildShortcutHint("Shortcut.FocusModel", "{0} Model", ShortcutActionFocusModelFilter));
        }

        if (_allRows.Any(static row => !row.IsLocked && row.IsUntranslated))
        {
            hints.Add(BuildShortcutHint("Shortcut.NextPending", "{0} NextPending", ShortcutActionNextPending));
            hints.Add(BuildShortcutHint("Shortcut.PrevPending", "{0} PrevPending", ShortcutActionPreviousPending));
        }

        KeyboardShortcutHint = string.Join("  |  ", hints);
    }

    public IReadOnlyList<ShortcutHelpItem> GetShortcutHelpItems()
    {
        var items = ShortcutActionDefinitions
            .Select(definition => new ShortcutHelpItem(
                L(definition.GroupKey, definition.GroupFallback),
                GetShortcutGesture(definition.ActionId),
                L(definition.DescriptionKey, definition.DescriptionFallback)))
            .ToList();

        items.Add(new ShortcutHelpItem(
            L("ShortcutGroup.Editing", "Editing"),
            "Enter (in AI input)",
            L("ShortcutHelp.AiSend", "Send message from AI input")));
        items.Add(new ShortcutHelpItem(
            L("ShortcutGroup.Editing", "Editing"),
            "Shift+Enter (in AI input)",
            L("ShortcutHelp.AiNewLine", "Insert new line in AI input")));
        return items;
    }

    public IReadOnlyList<ShortcutBindingItem> GetShortcutBindingItems()
    {
        return ShortcutActionDefinitions
            .Select(definition => new ShortcutBindingItem(
                definition.ActionId,
                L(definition.GroupKey, definition.GroupFallback),
                GetShortcutGesture(definition.ActionId),
                L(definition.DescriptionKey, definition.DescriptionFallback)))
            .ToList();
    }

    public IReadOnlyDictionary<string, string> GetDefaultShortcutMappings()
    {
        return ShortcutActionDefinitions.ToDictionary(
            static definition => definition.ActionId,
            static definition => definition.DefaultGesture,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, string> GetEffectiveShortcutMappings()
    {
        return ShortcutActionDefinitions.ToDictionary(
            static definition => definition.ActionId,
            definition => GetShortcutGesture(definition.ActionId),
            StringComparer.OrdinalIgnoreCase);
    }

    public string GetShortcutGesture(string actionId)
    {
        if (!ShortcutActionDefinitionMap.TryGetValue(actionId, out var definition))
        {
            return string.Empty;
        }

        if (_shortcutGestureOverrides.TryGetValue(actionId, out var overrideGesture)
            && !string.IsNullOrWhiteSpace(overrideGesture))
        {
            return overrideGesture;
        }

        return definition.DefaultGesture;
    }

    public async Task SaveShortcutMappingsAsync(IReadOnlyDictionary<string, string> mappings)
    {
        _shortcutGestureOverrides.Clear();
        foreach (var definition in ShortcutActionDefinitions)
        {
            if (!mappings.TryGetValue(definition.ActionId, out var gesture))
            {
                continue;
            }

            var normalized = gesture.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (!string.Equals(normalized, definition.DefaultGesture, StringComparison.OrdinalIgnoreCase))
            {
                _shortcutGestureOverrides[definition.ActionId] = normalized;
            }
        }

        await PersistShortcutMappingsAsync().ConfigureAwait(false);
        UpdateKeyboardShortcutHint();
    }

    public async Task ResetShortcutMappingsAsync()
    {
        _shortcutGestureOverrides.Clear();
        await PersistShortcutMappingsAsync().ConfigureAwait(false);
        UpdateKeyboardShortcutHint();
    }

    private string BuildShortcutHint(string resourceKey, string fallbackTemplate, string actionId)
    {
        return Lf(resourceKey, fallbackTemplate, GetShortcutGesture(actionId));
    }

    private async Task LoadShortcutMappingsAsync()
    {
        _shortcutGestureOverrides.Clear();
        var raw = await _settingsStore.GetAsync(ShortcutMappingsSettingKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            UpdateKeyboardShortcutHint();
            return;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
            if (snapshot is null)
            {
                UpdateKeyboardShortcutHint();
                return;
            }

            foreach (var pair in snapshot)
            {
                if (!ShortcutActionDefinitionMap.TryGetValue(pair.Key, out var definition))
                {
                    continue;
                }

                var normalized = pair.Value?.Trim();
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (!string.Equals(normalized, definition.DefaultGesture, StringComparison.OrdinalIgnoreCase))
                {
                    _shortcutGestureOverrides[definition.ActionId] = normalized;
                }
            }
        }
        catch
        {
            // Ignore malformed shortcut mappings and keep defaults.
        }

        UpdateKeyboardShortcutHint();
    }

    private async Task PersistShortcutMappingsAsync()
    {
        var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in ShortcutActionDefinitions)
        {
            if (_shortcutGestureOverrides.TryGetValue(definition.ActionId, out var overrideGesture)
                && !string.IsNullOrWhiteSpace(overrideGesture))
            {
                snapshot[definition.ActionId] = overrideGesture;
            }
        }

        var json = JsonSerializer.Serialize(snapshot);
        await _settingsStore.SetAsync(ShortcutMappingsSettingKey, json).ConfigureAwait(false);
    }

    private async Task LoadAiDrawerHeightAsync()
    {
        var raw = await _settingsStore.GetAsync(AiDrawerHeightKey).ConfigureAwait(false);
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            AiDrawerHeight = DefaultAiDrawerHeight;
            return;
        }

        AiDrawerHeight = ClampAiDrawerHeight(parsed);
    }

    public async Task SaveAiDrawerHeightAsync(double height)
    {
        var normalized = ClampAiDrawerHeight(height);
        AiDrawerHeight = normalized;
        await _settingsStore.SetAsync(
            AiDrawerHeightKey,
            normalized.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
    }

    private static double ClampAiDrawerHeight(double height)
    {
        return Math.Clamp(height, MinAiDrawerHeight, MaxAiDrawerHeight);
    }

    public void ToggleThemeShortcut()
    {
        SelectedTheme = string.Equals(
            NormalizeThemeValue(SelectedTheme),
            ThemeDark,
            StringComparison.Ordinal)
            ? ThemeLight
            : ThemeDark;
    }

    private async Task ApplyUiLanguageSelectionAsync(string languageTag)
    {
        if (_isApplyingUiLanguage)
        {
            return;
        }

        var normalizedLanguage = _localizationService.NormalizeLanguageTag(languageTag);
        if (!string.Equals(languageTag, normalizedLanguage, StringComparison.Ordinal))
        {
            _isApplyingUiLanguage = true;
            SelectedUiLanguageTag = normalizedLanguage;
            _isApplyingUiLanguage = false;
            return;
        }

        try
        {
            _localizationService.ApplyLanguage(normalizedLanguage);
        }
        catch (Exception ex)
        {
            StatusText = Lf(
                "Status.UiLanguageChangeFailed",
                "UI language switch failed: {0}",
                ex.Message);
            AddLog(StatusText);
            return;
        }

        RefreshUiLanguages();
        RefreshLocalizedOptionCollections();
        RefreshRowQualityLabels();
        await _settingsStore.SetAsync(UiLanguageSettingKey, normalizedLanguage).ConfigureAwait(false);

        var languageLabel = UiLanguages.FirstOrDefault(option =>
            string.Equals(option.LanguageTag, normalizedLanguage, StringComparison.OrdinalIgnoreCase)).DisplayName;
        if (string.IsNullOrWhiteSpace(languageLabel))
        {
            languageLabel = normalizedLanguage;
        }

        WorkspaceTitle = Lf("WorkspaceTitle.Format", "{0} Workspace", SelectedGame);
        ActivePluginName = string.IsNullOrWhiteSpace(PluginPath)
            ? L("PluginSwitcher.CurrentPlugin", "Current Plugin")
            : ActivePluginName;
        UpdateKeyboardShortcutHint();

        StatusText = Lf(
            "Status.UiLanguageChanged",
            "UI language switched to '{0}'.",
            languageLabel);
        AddLog(StatusText);
    }

    private async Task ApplyThemeSelectionAsync(string themeValue)
    {
        if (_isApplyingTheme)
        {
            return;
        }

        var normalizedTheme = NormalizeThemeValue(themeValue);
        if (!string.Equals(themeValue, normalizedTheme, StringComparison.Ordinal))
        {
            _isApplyingTheme = true;
            SelectedTheme = normalizedTheme;
            _isApplyingTheme = false;
            return;
        }

        if (Microsoft.UI.Xaml.Application.Current is bTranslator.App.App app)
        {
            app.ApplyTheme(normalizedTheme);
        }

        if (_isLoadingWorkspaceSettings)
        {
            return;
        }

        await _settingsStore.SetAsync(UiThemeSettingKey, normalizedTheme).ConfigureAwait(false);

        var themeLabel = ThemeOptions.FirstOrDefault(option =>
            string.Equals(option.Value, normalizedTheme, StringComparison.OrdinalIgnoreCase))?.DisplayName;
        if (string.IsNullOrWhiteSpace(themeLabel))
        {
            themeLabel = normalizedTheme;
        }

        StatusText = Lf(
            "Status.ThemeChanged",
            "Theme switched to '{0}'.",
            themeLabel);
        AddLog(StatusText);
    }

    public string GetLocalizedString(string resourceKey, string fallback)
    {
        return L(resourceKey, fallback);
    }

    private string L(string resourceKey, string fallback)
    {
        return _localizationService.GetString(resourceKey, fallback);
    }

    private string Lf(string resourceKey, string fallback, params object?[] args)
    {
        var template = _localizationService.GetString(resourceKey, fallback);
        try
        {
            return string.Format(CultureInfo.CurrentUICulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    private string ResolveUiLanguageDisplayName(UiLanguageOption option)
    {
        if (string.IsNullOrWhiteSpace(option.LanguageTag))
        {
            return L("UiLanguage.SystemDefault", "System default");
        }

        if (string.Equals(option.LanguageTag, "en-US", StringComparison.OrdinalIgnoreCase))
        {
            return L("UiLanguage.English", "English");
        }

        if (string.Equals(option.LanguageTag, "zh-CN", StringComparison.OrdinalIgnoreCase))
        {
            return L("UiLanguage.ChineseSimplified", "简体中文");
        }

        return option.DisplayName;
    }

    private string ResolveCurrentUiLanguageTag()
    {
        try
        {
            return _localizationService.NormalizeLanguageTag(Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride);
        }
        catch
        {
            return _localizationService.NormalizeLanguageTag(string.Empty);
        }
    }

    private void RefreshUiLanguages()
    {
        UiLanguages.Clear();
        foreach (var option in _localizationService.SupportedLanguages)
        {
            UiLanguages.Add(new UiLanguageOption(option.LanguageTag, ResolveUiLanguageDisplayName(option)));
        }

        UiLanguageMenuVersion++;
    }

    private void RefreshLocalizedOptionCollections()
    {
        ReplaceOptionItems(
            AvailableLanguageOptions,
            SupportedLanguageValues.Select(value => new OptionItem(value, ResolveLanguageDisplayName(value))));
        ReplaceOptionItems(
            ListFilterOptions,
            SupportedListFilterValues.Select(value => new OptionItem(value, ResolveListFilterDisplayName(value))));
        ReplaceOptionItems(
            EncodingModeOptions,
            EncodingModes.Select(value => new OptionItem(value, ResolveEncodingModeDisplayName(value))));
        ReplaceOptionItems(
            ThemeOptions,
            SupportedThemeValues.Select(value => new OptionItem(value, ResolveThemeDisplayName(value))));

        SourceLanguage = NormalizeLanguageValue(SourceLanguage);
        TargetLanguage = NormalizeLanguageValue(TargetLanguage);
        SelectedListFilter = NormalizeListFilterValue(SelectedListFilter);
        SelectedEncodingMode = NormalizeEncodingModeValue(SelectedEncodingMode);
        SelectedTheme = NormalizeThemeValue(SelectedTheme);

        UiOptionMenuVersion++;
    }

    private void RefreshRowQualityLabels()
    {
        _rowQualityLabels = new TranslationRowViewModel.QualityLabelSet(
            Locked: L("RowQuality.Locked", "Locked"),
            Validated: L("RowQuality.Validated", "Validated"),
            Pending: L("RowQuality.Pending", "Pending"),
            Draft: L("RowQuality.Draft", "Draft"));

        ApplyRowQualityLabels();
    }

    private void ApplyRowQualityLabels()
    {
        foreach (var row in _allRows)
        {
            row.ApplyQualityLabels(_rowQualityLabels);
        }
    }

    private static void ReplaceOptionItems(
        ObservableCollection<OptionItem> target,
        IEnumerable<OptionItem> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private string ResolveLanguageDisplayName(string value)
    {
        return value switch
        {
            LanguageEnglish => L("Language.English", "English"),
            LanguageChineseSimplified => L("Language.ChineseSimplified", "Chinese (Simplified)"),
            LanguageJapanese => L("Language.Japanese", "Japanese"),
            LanguageKorean => L("Language.Korean", "Korean"),
            _ => value
        };
    }

    private string ResolveListFilterDisplayName(string value)
    {
        return value switch
        {
            ListFilterAll => L("ListFilter.All", "All"),
            ListFilterStrings => L("ListFilter.Strings", "STRINGS"),
            ListFilterDlStrings => L("ListFilter.DlStrings", "DLSTRINGS"),
            ListFilterIlStrings => L("ListFilter.IlStrings", "ILSTRINGS"),
            ListFilterRecord => L("ListFilter.Record", "RECORD"),
            _ => value
        };
    }

    private string ResolveEncodingModeDisplayName(string value)
    {
        return value switch
        {
            AutoEncodingMode => L("EncodingMode.AutoDetect", "Auto Detect"),
            ManualEncodingMode => L("EncodingMode.Manual", "Manual"),
            _ => value
        };
    }

    private string ResolveThemeDisplayName(string value)
    {
        return value switch
        {
            ThemeDark => L("Theme.Dark", "Dark"),
            ThemeLight => L("Theme.Light", "Light"),
            _ => value
        };
    }

    private static string NormalizeLanguageValue(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return LanguageEnglish;
        }

        var normalized = language.Trim().ToLowerInvariant();
        return normalized switch
        {
            "english" => LanguageEnglish,
            "chinese" => LanguageChineseSimplified,
            "chinese (simplified)" => LanguageChineseSimplified,
            "japanese" => LanguageJapanese,
            "korean" => LanguageKorean,
            _ => LanguageEnglish
        };
    }

    private static string NormalizeListFilterValue(string? listFilter)
    {
        if (string.IsNullOrWhiteSpace(listFilter))
        {
            return ListFilterAll;
        }

        return listFilter.Trim().ToUpperInvariant() switch
        {
            "ALL" => ListFilterAll,
            "STRINGS" => ListFilterStrings,
            "DLSTRINGS" => ListFilterDlStrings,
            "ILSTRINGS" => ListFilterIlStrings,
            "RECORD" => ListFilterRecord,
            _ => ListFilterAll
        };
    }

    private static string NormalizeEncodingModeValue(string? encodingMode)
    {
        if (string.IsNullOrWhiteSpace(encodingMode))
        {
            return AutoEncodingMode;
        }

        var normalized = encodingMode.Trim().ToLowerInvariant();
        return normalized switch
        {
            "manual" => ManualEncodingMode,
            "auto" => AutoEncodingMode,
            "auto detect" => AutoEncodingMode,
            _ => AutoEncodingMode
        };
    }

    private static string NormalizeThemeValue(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return ThemeDark;
        }

        return theme.Trim().ToLowerInvariant() switch
        {
            "light" => ThemeLight,
            "dark" => ThemeDark,
            _ => ThemeDark
        };
    }

    private async Task LoadPluginSwitcherStateAsync()
    {
        PluginSwitcherItems.Clear();
        FilteredPluginSwitcherItems.Clear();

        var raw = await _settingsStore.GetAsync(PluginSwitcherStateKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<List<PluginSwitcherStateSnapshot>>(raw);
            if (snapshot is null)
            {
                return;
            }

            foreach (var item in snapshot)
            {
                if (string.IsNullOrWhiteSpace(item.PluginPath))
                {
                    continue;
                }

                var normalizedPath = item.PluginPath.Trim();
                var lastOpenedUtc = item.LastOpenedUnixTime <= 0
                    ? DateTimeOffset.UtcNow
                    : DateTimeOffset.FromUnixTimeSeconds(item.LastOpenedUnixTime);

                PluginSwitcherItems.Add(new PluginSwitcherItemViewModel
                {
                    PluginPath = normalizedPath,
                    DisplayName = string.IsNullOrWhiteSpace(item.DisplayName)
                        ? Path.GetFileName(normalizedPath)
                        : item.DisplayName.Trim(),
                    DirectoryPath = Path.GetDirectoryName(normalizedPath) ?? string.Empty,
                    IsPinned = item.IsPinned,
                    LastOpenedUtc = lastOpenedUtc
                });
            }

            SortPluginSwitcherItemsInPlace();
            RefreshPluginSwitcherItems();
        }
        catch
        {
            // Ignore malformed history and continue with empty switcher state.
        }
    }

    private async Task SavePluginSwitcherStateAsync()
    {
        var snapshot = PluginSwitcherItems
            .Select(item => new PluginSwitcherStateSnapshot
            {
                PluginPath = item.PluginPath,
                DisplayName = item.DisplayName,
                IsPinned = item.IsPinned,
                LastOpenedUnixTime = item.LastOpenedUtc.ToUnixTimeSeconds()
            })
            .ToList();

        var json = JsonSerializer.Serialize(snapshot);
        await _settingsStore.SetAsync(PluginSwitcherStateKey, json).ConfigureAwait(false);
    }

    private async Task RegisterPluginSwitcherItemAsync(string pluginPath, string displayName)
    {
        if (string.IsNullOrWhiteSpace(pluginPath))
        {
            return;
        }

        var normalizedPath = pluginPath.Trim();
        var normalizedName = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(normalizedPath)
            : displayName.Trim();
        var nowUtc = DateTimeOffset.UtcNow;

        var existing = PluginSwitcherItems.FirstOrDefault(item =>
            string.Equals(item.PluginPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new PluginSwitcherItemViewModel
            {
                PluginPath = normalizedPath,
                DisplayName = normalizedName,
                DirectoryPath = Path.GetDirectoryName(normalizedPath) ?? string.Empty,
                IsPinned = false,
                LastOpenedUtc = nowUtc
            };
            PluginSwitcherItems.Add(existing);
        }
        else
        {
            existing.DisplayName = normalizedName;
            existing.DirectoryPath = Path.GetDirectoryName(normalizedPath) ?? string.Empty;
            existing.LastOpenedUtc = nowUtc;
        }

        SortPluginSwitcherItemsInPlace();
        RefreshPluginSwitcherItems();
        SelectedPluginSwitcherItem = existing;

        const int maxItems = 24;
        while (PluginSwitcherItems.Count > maxItems)
        {
            var removable = PluginSwitcherItems.LastOrDefault(item => !item.IsPinned) ??
                            PluginSwitcherItems.LastOrDefault();
            if (removable is null)
            {
                break;
            }

            if (ReferenceEquals(removable, SelectedPluginSwitcherItem))
            {
                break;
            }

            PluginSwitcherItems.Remove(removable);
        }

        RefreshPluginSwitcherItems();
        await SavePluginSwitcherStateAsync().ConfigureAwait(false);
    }

    private void SortPluginSwitcherItemsInPlace()
    {
        var ordered = PluginSwitcherItems
            .OrderByDescending(item => item.IsPinned)
            .ThenByDescending(item => item.LastOpenedUtc)
            .ToList();

        PluginSwitcherItems.Clear();
        foreach (var item in ordered)
        {
            PluginSwitcherItems.Add(item);
        }
    }

    private void RefreshPluginSwitcherItems()
    {
        IEnumerable<PluginSwitcherItemViewModel> query = PluginSwitcherItems;
        if (!string.IsNullOrWhiteSpace(PluginSwitcherSearchText))
        {
            query = query.Where(item =>
                item.DisplayName.Contains(PluginSwitcherSearchText, StringComparison.OrdinalIgnoreCase) ||
                item.PluginPath.Contains(PluginSwitcherSearchText, StringComparison.OrdinalIgnoreCase) ||
                item.DirectoryPath.Contains(PluginSwitcherSearchText, StringComparison.OrdinalIgnoreCase));
        }

        FilteredPluginSwitcherItems.Clear();
        foreach (var item in query)
        {
            FilteredPluginSwitcherItems.Add(item);
        }

        if (SelectedPluginSwitcherItem is null || !FilteredPluginSwitcherItems.Contains(SelectedPluginSwitcherItem))
        {
            SelectedPluginSwitcherItem = FilteredPluginSwitcherItems.FirstOrDefault() ?? PluginSwitcherItems.FirstOrDefault();
        }
    }

    private void UpdateEncodingDisplayFallback()
    {
        if (string.Equals(SelectedEncodingMode, ManualEncodingMode, StringComparison.Ordinal))
        {
            EffectiveEncodingDisplay = $"{SelectedEncodingName} (Manual)";
            return;
        }

        EffectiveEncodingDisplay = _activeEncodingDisplay;
    }

    private async Task PersistEncodingPreferenceAsync()
    {
        try
        {
            await _settingsStore.SetAsync(WorkspaceEncodingModeKey, SelectedEncodingMode).ConfigureAwait(false);
            await _settingsStore.SetAsync(WorkspaceEncodingNameKey, SelectedEncodingName).ConfigureAwait(false);
            await _settingsStore.SetAsync(WorkspaceEncodingEffectiveKey, EffectiveEncodingDisplay).ConfigureAwait(false);
        }
        catch
        {
            // Ignore persistence failures so UI interactions are not blocked.
        }
    }

    private (Encoding Encoding, string Display) ResolveWorkspaceSaveEncoding()
    {
        if (string.Equals(SelectedEncodingMode, ManualEncodingMode, StringComparison.Ordinal))
        {
            var manual = ResolveManualEncoding();
            _ = TryResolveEncoding(manual.DisplayName, out var encoding);
            return (encoding, $"{manual.DisplayName} (Manual)");
        }

        return (_activeEncoding, _activeEncodingDisplay);
    }

    private async Task<(Encoding Encoding, string Display)> ResolveWorkspaceEncodingAsync(
        string pluginPath,
        string languageToken,
        string? explicitStringsDirectory)
    {
        if (string.Equals(SelectedEncodingMode, ManualEncodingMode, StringComparison.Ordinal))
        {
            var manual = ResolveManualEncoding();
            _ = TryResolveEncoding(manual.DisplayName, out var manualEncoding);
            return (manualEncoding, $"{manual.DisplayName} (Manual)");
        }

        var detected = await TryDetectEncodingAsync(pluginPath, languageToken, explicitStringsDirectory).ConfigureAwait(false);
        if (detected is not null)
        {
            _ = TryResolveEncoding(detected.Value.DisplayName, out var detectedEncoding);
            return (detectedEncoding, $"{detected.Value.DisplayName} (Auto)");
        }

        var fallback = ResolveManualEncoding();
        _ = TryResolveEncoding(fallback.DisplayName, out var fallbackEncoding);
        return (fallbackEncoding, $"{fallback.DisplayName} (Auto/Fallback)");
    }

    private async Task<EncodingChoice?> TryDetectEncodingAsync(
        string pluginPath,
        string languageToken,
        string? explicitStringsDirectory)
    {
        var stringsDirectory = explicitStringsDirectory ??
                               Path.Combine(Path.GetDirectoryName(pluginPath)!, "Strings");
        var pluginName = Path.GetFileNameWithoutExtension(pluginPath);
        var stringFiles = ResolveStringsFilesForEncodingDetection(stringsDirectory, pluginName, languageToken);

        if (stringFiles.Length == 0)
        {
            return null;
        }

        var bomDetected = TryDetectEncodingFromBom(stringFiles);
        if (bomDetected is not null)
        {
            return bomDetected;
        }

        var bestChoice = default(EncodingChoice?);
        var bestScore = long.MaxValue;
        foreach (var choice in WorkspaceEncodingChoices)
        {
            if (!TryResolveEncoding(choice.DisplayName, out var candidateEncoding))
            {
                continue;
            }

            var totalScore = 0L;
            var failed = false;
            foreach (var file in stringFiles)
            {
                try
                {
                    var entries = await _stringsCodec.ReadAsync(
                        file.Path,
                        file.Kind,
                        candidateEncoding).ConfigureAwait(false);
                    totalScore += ScoreDecodedEntries(entries);
                }
                catch
                {
                    failed = true;
                    break;
                }
            }

            if (failed)
            {
                continue;
            }

            if (totalScore < bestScore ||
                (totalScore == bestScore &&
                 IsPreferredEncodingForLanguage(choice.DisplayName, bestChoice?.DisplayName, languageToken)))
            {
                bestScore = totalScore;
                bestChoice = choice;
            }
        }

        return bestChoice;
    }

    private static (StringsFileKind Kind, string Path)[] ResolveStringsFilesForEncodingDetection(
        string stringsDirectory,
        string pluginName,
        string languageToken)
    {
        if (!Directory.Exists(stringsDirectory))
        {
            return [];
        }

        var preferred = Enum
            .GetValues<StringsFileKind>()
            .Select(kind => (Kind: kind, Path: BuildStringsPath(stringsDirectory, pluginName, languageToken, kind)))
            .Where(static item => File.Exists(item.Path))
            .ToArray();
        if (preferred.Length > 0)
        {
            return preferred;
        }

        const int maxFilesPerKind = 4;
        var discovered = new List<(StringsFileKind Kind, string Path)>();
        var filePrefix = pluginName + "_";
        foreach (var kind in Enum.GetValues<StringsFileKind>())
        {
            var extension = kind switch
            {
                StringsFileKind.Strings => ".strings",
                StringsFileKind.DlStrings => ".dlstrings",
                StringsFileKind.IlStrings => ".ilstrings",
                _ => ".strings"
            };

            var files = Directory
                .EnumerateFiles(stringsDirectory, $"*{extension}", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .Take(maxFilesPerKind);
            foreach (var filePath in files)
            {
                discovered.Add((kind, filePath));
            }
        }

        if (discovered.Count == 0)
        {
            foreach (var kind in Enum.GetValues<StringsFileKind>())
            {
                var extension = kind switch
                {
                    StringsFileKind.Strings => ".strings",
                    StringsFileKind.DlStrings => ".dlstrings",
                    StringsFileKind.IlStrings => ".ilstrings",
                    _ => ".strings"
                };

                var fallbackPath = Path.Combine(stringsDirectory, pluginName + extension);
                if (File.Exists(fallbackPath))
                {
                    discovered.Add((kind, fallbackPath));
                }
            }
        }

        return discovered
            .GroupBy(static item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static EncodingChoice? TryDetectEncodingFromBom((StringsFileKind Kind, string Path)[] stringFiles)
    {
        EncodingChoice? detected = null;
        foreach (var file in stringFiles)
        {
            var bomEncoding = ReadBomEncodingChoice(file.Path);
            if (bomEncoding is null)
            {
                continue;
            }

            if (detected is null)
            {
                detected = bomEncoding;
                continue;
            }

            if (!string.Equals(detected.Value.DisplayName, bomEncoding.Value.DisplayName, StringComparison.Ordinal))
            {
                return null;
            }
        }

        return detected;
    }

    private static EncodingChoice? ReadBomEncodingChoice(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> bom = stackalloc byte[4];
            var read = stream.Read(bom);
            if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            {
                return new EncodingChoice("UTF-8", "utf-8");
            }

            if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
            {
                return new EncodingChoice("UTF-16 LE", "utf-16");
            }

            if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
            {
                return new EncodingChoice("UTF-16 BE", "utf-16BE");
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPreferredEncodingForLanguage(
        string candidateDisplayName,
        string? currentDisplayName,
        string languageToken)
    {
        if (currentDisplayName is null)
        {
            return true;
        }

        var candidateRank = GetEncodingPreferenceRank(candidateDisplayName, languageToken);
        var currentRank = GetEncodingPreferenceRank(currentDisplayName, languageToken);
        if (candidateRank != currentRank)
        {
            return candidateRank < currentRank;
        }

        return string.Equals(candidateDisplayName, DefaultEncodingDisplayName, StringComparison.Ordinal);
    }

    private static int GetEncodingPreferenceRank(string displayName, string languageToken)
    {
        var normalizedLanguage = languageToken.Trim().ToLowerInvariant();
        return normalizedLanguage switch
        {
            "chinese" => displayName switch
            {
                "GB18030" => 0,
                "Big5" => 1,
                "UTF-8" => 2,
                "UTF-16 LE" => 3,
                "UTF-16 BE" => 4,
                _ => 5
            },
            "japanese" => displayName switch
            {
                "Shift-JIS" => 0,
                "UTF-8" => 1,
                "UTF-16 LE" => 2,
                "UTF-16 BE" => 3,
                _ => 4
            },
            "korean" => displayName switch
            {
                "EUC-KR" => 0,
                "UTF-8" => 1,
                "UTF-16 LE" => 2,
                "UTF-16 BE" => 3,
                _ => 4
            },
            _ => displayName switch
            {
                "UTF-8" => 0,
                "UTF-16 LE" => 1,
                "UTF-16 BE" => 2,
                "Windows-1252" => 3,
                _ => 4
            }
        };
    }

    private static long ScoreDecodedEntries(IReadOnlyList<StringsEntry> entries)
    {
        long score = 0;
        foreach (var entry in entries)
        {
            score += ScoreDecodedText(entry.Text);
        }

        return score;
    }

    private static int ScoreDecodedText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var score = 0;
        foreach (var ch in text)
        {
            if (ch == '\uFFFD')
            {
                score += 200;
            }
            else if (ch == '\0')
            {
                score += 100;
            }
            else if (char.IsControl(ch) && ch is not '\r' and not '\n' and not '\t')
            {
                score += 20;
            }
            else if (!char.IsLetterOrDigit(ch) &&
                     !char.IsWhiteSpace(ch) &&
                     !char.IsPunctuation(ch) &&
                     !char.IsSymbol(ch))
            {
                score += 5;
            }
        }

        return score;
    }

    private EncodingChoice ResolveManualEncoding()
    {
        var manual = WorkspaceEncodingChoices.FirstOrDefault(choice =>
            string.Equals(choice.DisplayName, SelectedEncodingName, StringComparison.Ordinal));
        if (manual.DisplayName is null)
        {
            manual = WorkspaceEncodingChoices.FirstOrDefault(choice =>
                string.Equals(choice.DisplayName, DefaultEncodingDisplayName, StringComparison.Ordinal));
        }

        return manual.DisplayName is null
            ? new EncodingChoice(DefaultEncodingDisplayName, "utf-8")
            : manual;
    }

    private bool TryResolveEncoding(string displayName, out Encoding encoding)
    {
        var choice = WorkspaceEncodingChoices.FirstOrDefault(item =>
            string.Equals(item.DisplayName, displayName, StringComparison.Ordinal));
        if (choice.DisplayName is null)
        {
            encoding = Encoding.UTF8;
            return false;
        }

        try
        {
            encoding = Encoding.GetEncoding(choice.CodePageName);
            return true;
        }
        catch
        {
            encoding = Encoding.UTF8;
            return false;
        }
    }

    private static string BuildStringsPath(
        string directory,
        string pluginName,
        string language,
        StringsFileKind kind)
    {
        var extension = kind switch
        {
            StringsFileKind.Strings => ".strings",
            StringsFileKind.DlStrings => ".dlstrings",
            StringsFileKind.IlStrings => ".ilstrings",
            _ => ".strings"
        };

        return Path.Combine(directory, $"{pluginName}_{language}{extension}");
    }

    private static string ToListLabel(StringsFileKind kind)
    {
        return kind switch
        {
            StringsFileKind.Strings => "STRINGS",
            StringsFileKind.DlStrings => "DLSTRINGS",
            StringsFileKind.IlStrings => "ILSTRINGS",
            _ => "STRINGS"
        };
    }

    private static string ToListLabel(byte? listIndex)
    {
        return listIndex switch
        {
            0 => "STRINGS",
            1 => "DLSTRINGS",
            2 => "ILSTRINGS",
            _ => "RECORD"
        };
    }

    private static string ToLanguageToken(string language)
    {
        return NormalizeLanguageValue(language) switch
        {
            LanguageEnglish => "english",
            LanguageChineseSimplified => "chinese",
            LanguageJapanese => "japanese",
            LanguageKorean => "korean",
            _ => LanguageEnglish
        };
    }

    private static string? NormalizeOptionalPath(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static GameKind ParseGameKind(string selectedGame)
    {
        return Enum.TryParse<GameKind>(selectedGame, ignoreCase: true, out var game)
            ? game
            : GameKind.Fallout4;
    }

    private static double EstimateLd(string source, string translated)
    {
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return 0;
        }

        var lengthDiff = Math.Abs(source.Length - translated.Length);
        var baseScore = translated.Length == 0 ? source.Length : lengthDiff + 1;
        return Math.Min(baseScore, 99);
    }

    private void AddLog(string message)
    {
        ActivityLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private sealed class PluginSwitcherStateSnapshot
    {
        public string PluginPath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public long LastOpenedUnixTime { get; set; }
    }

    private sealed class StringsEntryBinding
    {
        public required StringsFileKind Kind { get; init; }
        public required StringsEntry Entry { get; init; }
    }

    public readonly record struct ShortcutHelpItem(string Group, string Gesture, string Description);
    public readonly record struct ShortcutBindingItem(string ActionId, string Group, string Gesture, string Description);
    private readonly record struct ShortcutActionDefinition(
        string ActionId,
        string DefaultGesture,
        string GroupKey,
        string GroupFallback,
        string DescriptionKey,
        string DescriptionFallback,
        string? HintKey,
        string? HintFallback);

    private readonly record struct EncodingChoice(string DisplayName, string CodePageName);
}

