using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
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

    private readonly IEnumerable<ITranslationProvider> _providers;
    private readonly IPlaceholderProtector _placeholderProtector;
    private readonly ISettingsStore _settingsStore;
    private readonly ICredentialStore _credentialStore;
    private readonly TranslationProviderOptions _translationProviderOptions;
    private readonly IPluginDocumentService _pluginDocumentService;
    private readonly ITranslationOrchestrator _translationOrchestrator;
    private readonly List<TranslationRowViewModel> _allRows = [];
    private readonly Dictionary<TranslationRowViewModel, StringsEntryBinding> _stringEntryBindings = new();
    private readonly Dictionary<TranslationRowViewModel, TranslationItem> _recordItemBindings = new();
    private PluginDocument? _currentDocument;

    public MainViewModel(
        IEnumerable<ITranslationProvider> providers,
        IPlaceholderProtector placeholderProtector,
        ISettingsStore settingsStore,
        ICredentialStore credentialStore,
        IOptions<TranslationProviderOptions> translationProviderOptions,
        IPluginDocumentService pluginDocumentService,
        ITranslationOrchestrator translationOrchestrator)
    {
        _providers = providers;
        _placeholderProtector = placeholderProtector;
        _settingsStore = settingsStore;
        _credentialStore = credentialStore;
        _translationProviderOptions = translationProviderOptions.Value;
        _pluginDocumentService = pluginDocumentService;
        _translationOrchestrator = translationOrchestrator;

        AvailableLanguages = new ObservableCollection<string>(["English", "Chinese (Simplified)", "Japanese", "Korean"]);
        Games = new ObservableCollection<string>(Enum.GetNames<GameKind>());
        ListFilters = new ObservableCollection<string>(["All", "STRINGS", "DLSTRINGS", "ILSTRINGS", "RECORD"]);

        BatchQueue.Add("Open plugin and index rows");
        BatchQueue.Add("Filter untranslated + unlockable rows");
        BatchQueue.Add("Execute provider chain");
        BatchQueue.Add("Write back records and strings");

        ScriptHints.Add("Scope respects current search and list filter.");
        ScriptHints.Add("Locked rows are skipped.");
        ScriptHints.Add("Placeholder normalization enabled by default.");
        ScriptHints.Add("Save writes both STRINGS and record fields.");

        GlossarySuggestions.Add("ChargeTime -> 蓄力时间");
        GlossarySuggestions.Add("Cooldown -> 冷却时间");
        GlossarySuggestions.Add("PowerArmor -> 动力装甲");
        GlossarySuggestions.Add("DamageResist -> 伤害抗性");
    }

    public ObservableCollection<TranslationRowViewModel> Rows { get; } = [];
    public ObservableCollection<string> AvailableLanguages { get; }
    public ObservableCollection<string> Games { get; }
    public ObservableCollection<string> ListFilters { get; }
    public ObservableCollection<string> ProviderChain { get; } = [];
    public ObservableCollection<ProviderChainItemViewModel> ProviderOptions { get; } = [];
    public ObservableCollection<string> RegisteredProviders { get; } = [];
    public ObservableCollection<string> ActivityLogs { get; } = [];
    public ObservableCollection<string> BatchQueue { get; } = [];
    public ObservableCollection<string> ScriptHints { get; } = [];
    public ObservableCollection<string> GlossarySuggestions { get; } = [];
    public ObservableCollection<PluginSwitcherItemViewModel> PluginSwitcherItems { get; } = [];
    public ObservableCollection<PluginSwitcherItemViewModel> FilteredPluginSwitcherItems { get; } = [];

    [ObservableProperty]
    private string workspaceTitle = "Fallout4 Workspace";

    [ObservableProperty]
    private string activePluginName = "Current Plugin";

    [ObservableProperty]
    private string selectedGame = nameof(GameKind.Fallout4);

    [ObservableProperty]
    private string pluginPath = string.Empty;

    [ObservableProperty]
    private string outputPluginPath = string.Empty;

    [ObservableProperty]
    private string stringsDirectoryPath = string.Empty;

    [ObservableProperty]
    private string recordDefinitionsPath = string.Empty;

    [ObservableProperty]
    private bool loadStrings = true;

    [ObservableProperty]
    private bool loadRecordFields = true;

    [ObservableProperty]
    private string sourceLanguage = "English";

    [ObservableProperty]
    private string targetLanguage = "Chinese (Simplified)";

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedListFilter = "All";

    [ObservableProperty]
    private bool showUntranslatedOnly;

    [ObservableProperty]
    private bool showLocked = true;

    [ObservableProperty]
    private ProviderChainItemViewModel? selectedProviderOption;

    [ObservableProperty]
    private string pluginSwitcherSearchText = string.Empty;

    [ObservableProperty]
    private string providerChainPreview = "No provider selected.";

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private int totalEntries;

    [ObservableProperty]
    private int translatedEntries;

    [ObservableProperty]
    private int pendingEntries;

    [ObservableProperty]
    private double completionPercent;

    [ObservableProperty]
    private TranslationRowViewModel? selectedRow;

    [ObservableProperty]
    private string inspectorRecordId = "-";

    [ObservableProperty]
    private string inspectorField = "-";

    [ObservableProperty]
    private string inspectorSourceText = string.Empty;

    [ObservableProperty]
    private string inspectorTranslatedText = string.Empty;

    [ObservableProperty]
    private string inspectorHint = "Select a row to review placeholders and terminology consistency.";

    [ObservableProperty]
    private string? selectedGlossarySuggestion;

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
    partial void OnPluginSwitcherSearchTextChanged(string value) => RefreshPluginSwitcherItems();

    partial void OnSelectedRowChanged(TranslationRowViewModel? value)
    {
        UpdateInspectorFromSelection(value);
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
        await LoadWorkspaceSettingsAsync().ConfigureAwait(false);
        await LoadPluginSwitcherStateAsync().ConfigureAwait(false);
        await LoadPersistedProviderSettingsAsync().ConfigureAwait(false);
        await LoadSelectedProviderConfigurationAsync().ConfigureAwait(false);

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
            StatusText = "Placeholder pipeline check failed.";
            AddLog(StatusText);
            return;
        }

        if (_allRows.Count == 0)
        {
            SeedRows();
        }

        StatusText = "Metadata refreshed.";
        AddLog(StatusText);
    }

    [RelayCommand]
    private async Task OpenWorkspaceAsync()
    {
        if (string.IsNullOrWhiteSpace(PluginPath) || !File.Exists(PluginPath))
        {
            StatusText = "Plugin path is empty or file does not exist.";
            AddLog(StatusText);
            return;
        }

        try
        {
            var game = ParseGameKind(SelectedGame);
            var options = new PluginOpenOptions
            {
                Language = ToLanguageToken(SourceLanguage),
                StringsDirectory = NormalizeOptionalPath(StringsDirectoryPath),
                RecordDefinitionsPath = NormalizeOptionalPath(RecordDefinitionsPath),
                LoadStrings = LoadStrings,
                LoadRecordFields = LoadRecordFields,
                Encoding = Encoding.UTF8
            };

            _currentDocument = await _pluginDocumentService.OpenAsync(game, PluginPath, options).ConfigureAwait(false);
            ActivePluginName = _currentDocument.PluginName + Path.GetExtension(_currentDocument.PluginPath);
            WorkspaceTitle = $"{game} Workspace";
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

            RebuildRowsFromDocument(_currentDocument);
            StatusText = $"Opened '{ActivePluginName}'.";
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = $"Open failed: {ex.Message}";
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task SaveWorkspaceAsync()
    {
        if (_currentDocument is null)
        {
            StatusText = "No active plugin document. Open a plugin first.";
            AddLog(StatusText);
            return;
        }

        try
        {
            ApplyRowsToDocument();
            var outputPath = string.IsNullOrWhiteSpace(OutputPluginPath)
                ? _currentDocument.PluginPath
                : OutputPluginPath;
            var options = new PluginSaveOptions
            {
                Language = ToLanguageToken(TargetLanguage),
                OutputStringsDirectory = NormalizeOptionalPath(StringsDirectoryPath),
                SaveStrings = LoadStrings,
                SaveRecordFields = LoadRecordFields,
                Encoding = Encoding.UTF8
            };

            await _pluginDocumentService.SaveAsync(_currentDocument, outputPath, options).ConfigureAwait(false);
            PluginPath = outputPath;
            ActivePluginName = Path.GetFileName(outputPath);
            await RegisterPluginSwitcherItemAsync(outputPath, ActivePluginName).ConfigureAwait(false);
            await _settingsStore.SetAsync("workspace.output_path", outputPath).ConfigureAwait(false);

            StatusText = $"Saved workspace to '{outputPath}'.";
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
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
            StatusText = "No pending rows to translate.";
            AddLog(StatusText);
            return;
        }

        var chain = BuildProviderChain();
        if (chain.Count == 0)
        {
            StatusText = "No available provider in chain.";
            AddLog(StatusText);
            return;
        }

        try
        {
            var requestItems = candidates.Select(static row => new TranslationItem
            {
                Id = row.RowKey,
                SourceText = row.SourceText,
                TranslatedText = row.TranslatedText,
                IsLocked = row.IsLocked,
                IsValidated = row.IsValidated
            }).ToList();

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

                row.TranslatedText = item.TranslatedText ?? row.SourceText;
                row.IsValidated = false;
            }

            RecalculateMetrics();
            ApplyFilters();
            StatusText = $"Batch translation done by '{result.ProviderId}', updated {candidates.Count} rows.";
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = $"Batch translation failed: {ex.Message}";
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
            StatusText = $"Plugin file not found: '{item.PluginPath}'.";
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
        AddLog($"Applied manual edit to '{SelectedRow.EditorId}'.");
    }

    [RelayCommand]
    private void CopySourceToTarget()
    {
        if (SelectedRow is null)
        {
            return;
        }

        InspectorTranslatedText = SelectedRow.SourceText;
        AddLog($"Copied source text to target for '{SelectedRow.EditorId}'.");
    }

    [RelayCommand]
    private void MarkSelectedValidated()
    {
        if (SelectedRow is null)
        {
            return;
        }

        SelectedRow.IsValidated = true;
        AddLog($"Marked '{SelectedRow.EditorId}' as validated.");
    }

    [RelayCommand]
    private void ToggleSelectedLock()
    {
        if (SelectedRow is null)
        {
            return;
        }

        SelectedRow.IsLocked = !SelectedRow.IsLocked;
        AddLog($"{(SelectedRow.IsLocked ? "Locked" : "Unlocked")} '{SelectedRow.EditorId}'.");
        RecalculateMetrics();
        ApplyFilters();
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
            AddLog($"Applied glossary suggestion '{SelectedGlossarySuggestion}' to '{SelectedRow.EditorId}'.");
            return;
        }

        InspectorHint = "Selected glossary term does not match current source text.";
    }

    [RelayCommand]
    private void MoveSelectedProviderUp()
    {
        if (SelectedProviderOption is null)
        {
            return;
        }

        var index = ProviderOptions.IndexOf(SelectedProviderOption);
        if (index <= 0)
        {
            return;
        }

        ProviderOptions.Move(index, index - 1);
        SyncProviderChainOrderFromOptions();
        UpdateProviderChainPreview();
    }

    [RelayCommand]
    private void MoveSelectedProviderDown()
    {
        if (SelectedProviderOption is null)
        {
            return;
        }

        var index = ProviderOptions.IndexOf(SelectedProviderOption);
        if (index < 0 || index >= ProviderOptions.Count - 1)
        {
            return;
        }

        ProviderOptions.Move(index, index + 1);
        SyncProviderChainOrderFromOptions();
        UpdateProviderChainPreview();
    }

    [RelayCommand]
    private void SelectAllProviders()
    {
        foreach (var option in ProviderOptions)
        {
            option.IsEnabled = true;
        }

        UpdateProviderChainPreview();
    }

    [RelayCommand]
    private void ClearProviderSelection()
    {
        foreach (var option in ProviderOptions)
        {
            option.IsEnabled = false;
        }

        UpdateProviderChainPreview();
    }

    private async Task LoadWorkspaceSettingsAsync()
    {
        PluginPath = await _settingsStore.GetAsync("workspace.plugin_path").ConfigureAwait(false) ?? PluginPath;
        OutputPluginPath = await _settingsStore.GetAsync("workspace.output_path").ConfigureAwait(false) ?? OutputPluginPath;
        var game = await _settingsStore.GetAsync("workspace.game").ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(game) && Games.Contains(game))
        {
            SelectedGame = game;
        }
    }

    private void LoadProviders()
    {
        foreach (var option in ProviderOptions)
        {
            option.PropertyChanged -= OnProviderOptionChanged;
        }

        RegisteredProviders.Clear();
        ProviderChain.Clear();
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
            ProviderChain.Add(providerId);
            var option = new ProviderChainItemViewModel
            {
                ProviderId = providerId,
                IsEnabled = true
            };
            option.PropertyChanged += OnProviderOptionChanged;
            ProviderOptions.Add(option);
        }

        SelectedProviderOption = ProviderOptions.FirstOrDefault();
        UpdateProviderChainPreview();
    }

    private void RebuildRowsFromDocument(PluginDocument document)
    {
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
            row.PropertyChanged += OnRowChanged;
            _allRows.Add(row);
            _recordItemBindings[row] = item;
        }

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

    private void SeedRows()
    {
        foreach (var row in _allRows)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        _allRows.Clear();
        _stringEntryBindings.Clear();
        _recordItemBindings.Clear();

        AddSampleRow("CROSSap_MatSwap", "KYWD FULL", "涂装替换", "涂装替换", "STRINGS", 0.0, false, true);
        AddSampleRow("CROSSap_PaletteIndex", "KYWD FULL", "色调替换", "色调替换", "STRINGS", 0.0, false, true);
        AddSampleRow("CROSSap_sol_CooldownMod", "KYWD FULL", "CROSSap_sol_CooldownMod", "", "STRINGS", 4.5, false, false);
        AddSampleRow("CROSSap_sol_ChargeTime", "KYWD FULL", "CROSSap_sol_ChargeTimeMod", "", "DLSTRINGS", 6.0, false, false);
        AddSampleRow("CROSSrace_sol_PowerArmor", "RACE FMRN", "动力装甲", "动力装甲", "STRINGS", 0.0, false, true);
        AddSampleRow("CROSSrace_sol_NPCComment", "INFO NAM1", "这个动作太慢了。", "", "ILSTRINGS", 8.1, false, false);

        RecalculateMetrics();
        ApplyFilters();
    }

    private void AddSampleRow(
        string editorId,
        string fieldSignature,
        string source,
        string translated,
        string listKind,
        double ld,
        bool locked,
        bool validated)
    {
        var row = new TranslationRowViewModel
        {
            RowKey = $"sample:{Guid.NewGuid():N}",
            EditorId = editorId,
            FieldSignature = fieldSignature,
            SourceText = source,
            TranslatedText = translated,
            ListKind = listKind,
            LdScore = ld,
            IsLocked = locked,
            IsValidated = validated
        };

        row.PropertyChanged += OnRowChanged;
        _allRows.Add(row);
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

    private void OnProviderOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProviderChainItemViewModel.IsEnabled))
        {
            UpdateProviderChainPreview();
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allRows.Where(static _ => true);

        if (!string.Equals(SelectedListFilter, "All", StringComparison.OrdinalIgnoreCase))
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
    }

    private void UpdateInspectorFromSelection(TranslationRowViewModel? row)
    {
        if (row is null)
        {
            InspectorRecordId = "-";
            InspectorField = "-";
            InspectorSourceText = string.Empty;
            InspectorTranslatedText = string.Empty;
            InspectorHint = "Select a row to review placeholders and terminology consistency.";
            return;
        }

        InspectorRecordId = row.EditorId;
        InspectorField = row.FieldSignature;
        InspectorSourceText = row.SourceText;
        InspectorTranslatedText = row.TranslatedText;
        InspectorHint = row.IsLocked
            ? "Current row is locked. Unlock it before writing translations."
            : "Keep tags and number placeholders unchanged during translation.";
    }

    private List<string> BuildProviderChain() =>
        ProviderOptions
            .Where(static option => option.IsEnabled)
            .Select(static option => option.ProviderId)
            .ToList();

    private void SyncProviderChainOrderFromOptions()
    {
        ProviderChain.Clear();
        foreach (var option in ProviderOptions)
        {
            ProviderChain.Add(option.ProviderId);
        }
    }

    private void UpdateProviderChainPreview()
    {
        var enabled = ProviderOptions
            .Where(static option => option.IsEnabled)
            .Select(static option => option.ProviderId)
            .ToList();

        ProviderChainPreview = enabled.Count == 0
            ? "No provider selected."
            : string.Join(" -> ", enabled);
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
        return language.Trim().ToLowerInvariant() switch
        {
            "english" => "english",
            "chinese (simplified)" => "chinese",
            "japanese" => "japanese",
            "korean" => "korean",
            _ => language.Trim().ToLowerInvariant().Replace(' ', '_')
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
}

