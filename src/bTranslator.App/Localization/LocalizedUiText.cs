using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace bTranslator.App.Localization;

public sealed class LocalizedUiText : ObservableObject
{
    private readonly IAppLocalizationService _localizationService;

    private static readonly IReadOnlyDictionary<string, (string ResourceKey, string Fallback)> ResourceMap =
        new Dictionary<string, (string ResourceKey, string Fallback)>(StringComparer.Ordinal)
        {
            [nameof(AppWindowTitle)] = ("App.WindowTitle", "bTranslator"),
            [nameof(MainMenuFileTitle)] = ("MainMenuFile.Title", "File"),
            [nameof(MainMenuTranslateTitle)] = ("MainMenuTranslate.Title", "Translate"),
            [nameof(MainMenuProvidersTitle)] = ("MainMenuProviders.Title", "Providers"),
            [nameof(MainMenuViewTitle)] = ("MainMenuView.Title", "View"),
            [nameof(MainMenuActionsTitle)] = ("MainMenuActions.Title", "Actions"),
            [nameof(MainMenuWorkspaceTitle)] = ("MainMenuWorkspace.Title", "Workspace"),
            [nameof(MenuOpenPlugin)] = ("Menu.OpenPlugin", "Open Plugin..."),
            [nameof(MenuSave)] = ("Menu.Save", "Save"),
            [nameof(MenuOpenAndAutoTranslate)] = ("Menu.OpenAndAutoTranslate", "Open + Auto Translate"),
            [nameof(MenuPaths)] = ("Menu.Paths", "Paths"),
            [nameof(MenuOptional)] = ("Menu.Optional", "Optional"),
            [nameof(MenuPluginPath)] = ("Menu.PluginPath", "Plugin Path..."),
            [nameof(MenuOutputPath)] = ("Menu.OutputPath", "Output Path..."),
            [nameof(MenuStringsDirectory)] = ("Menu.StringsDirectory", "Strings Directory..."),
            [nameof(MenuRecordDefsPath)] = ("Menu.RecordDefsPath", "RecordDefs Path..."),
            [nameof(MenuRunBatchTranslation)] = ("Menu.RunBatchTranslation", "Run Batch Translation"),
            [nameof(MenuScope)] = ("Menu.Scope", "Scope"),
            [nameof(MenuSelection)] = ("Menu.Selection", "Selection"),
            [nameof(MenuLoadStrings)] = ("Menu.LoadStrings", "Load STRINGS"),
            [nameof(MenuLoadRecordFields)] = ("Menu.LoadRecordFields", "Load Record Fields"),
            [nameof(MenuOnlyPendingRows)] = ("Menu.OnlyPendingRows", "Only Pending Rows"),
            [nameof(MenuIncludeLockedRows)] = ("Menu.IncludeLockedRows", "Include Locked Rows"),
            [nameof(MenuCopySourceToTranslation)] = ("Menu.CopySourceToTranslation", "Copy Source to Translation"),
            [nameof(MenuApplyInspectorEdit)] = ("Menu.ApplyInspectorEdit", "Apply Inspector Edit"),
            [nameof(MenuMarkSelectedValidated)] = ("Menu.MarkSelectedValidated", "Mark Selected Validated"),
            [nameof(MenuToggleLock)] = ("Menu.ToggleLock", "Toggle Lock"),
            [nameof(MenuReloadMetadata)] = ("Menu.ReloadMetadata", "Reload Metadata"),
            [nameof(MenuAiWorkflow)] = ("Menu.AiWorkflow", "AI Workflow"),
            [nameof(MenuOpenWorkflowDesigner)] = ("Menu.OpenWorkflowDesigner", "Open Workflow Designer"),
            [nameof(MenuChain)] = ("Menu.Chain", "Chain"),
            [nameof(MenuMoveSelectedUp)] = ("Menu.MoveSelectedUp", "Move Selected Up"),
            [nameof(MenuMoveSelectedDown)] = ("Menu.MoveSelectedDown", "Move Selected Down"),
            [nameof(MenuEnableAll)] = ("Menu.EnableAll", "Enable All"),
            [nameof(MenuDisableAll)] = ("Menu.DisableAll", "Disable All"),
            [nameof(MenuCurrentChain)] = ("Menu.CurrentChain", "Current Chain"),
            [nameof(MenuConfiguration)] = ("Menu.Configuration", "Configuration"),
            [nameof(MenuOpenConfigurationWindow)] = ("Menu.OpenConfigurationWindow", "Open Configuration Window"),
            [nameof(MenuSaveSelectedProvider)] = ("Menu.SaveSelectedProvider", "Save Selected Provider"),
            [nameof(MenuReloadSelectedProvider)] = ("Menu.ReloadSelectedProvider", "Reload Selected Provider"),
            [nameof(MenuTestSelectedProvider)] = ("Menu.TestSelectedProvider", "Test Selected Provider"),
            [nameof(MenuImportApiTranslator)] = ("Menu.ImportApiTranslator", "Import ApiTranslator.txt..."),
            [nameof(MenuDictionary)] = ("Menu.Dictionary", "Dictionary"),
            [nameof(MenuEditDictionary)] = ("Menu.EditDictionary", "Edit Dictionary..."),
            [nameof(MenuAddSelectedRowToDictionary)] = ("Menu.AddSelectedRowToDictionary", "Add Selected Row to Dictionary"),
            [nameof(MenuEspCompare)] = ("Menu.EspCompare", "ESP Compare"),
            [nameof(MenuCompareWithEsp)] = ("Menu.CompareWithEsp", "Compare With ESP..."),
            [nameof(MenuApplyEspCompareReplacements)] = ("Menu.ApplyEspCompareReplacements", "Apply Compare Replacements"),
            [nameof(MenuImportDictionary)] = ("Menu.ImportDictionary", "Import Dictionary..."),
            [nameof(MenuExportDictionary)] = ("Menu.ExportDictionary", "Export Dictionary..."),
            [nameof(MenuEnableDictionaryPreReplace)] = ("Menu.EnableDictionaryPreReplace", "Enable Dictionary Pre-Replace"),
            [nameof(DictionaryEditorDialogTitle)] = ("DictionaryEditor.DialogTitle", "Dictionary Editor"),
            [nameof(DictionaryEditorSearchHeaderText)] = ("DictionaryEditor.Search.Header", "Search"),
            [nameof(DictionaryEditorSearchPlaceholderText)] = ("DictionaryEditor.Search.PlaceholderText", "Source / Target / EDID Scope / Field Scope"),
            [nameof(DictionaryEditorSortHeaderText)] = ("DictionaryEditor.Sort.Header", "Sort"),
            [nameof(DictionaryEditorSortBySourceText)] = ("DictionaryEditor.Sort.SourceAsc", "Source (A-Z)"),
            [nameof(DictionaryEditorSortByTargetText)] = ("DictionaryEditor.Sort.TargetAsc", "Target (A-Z)"),
            [nameof(DictionaryEditorSortByScopeText)] = ("DictionaryEditor.Sort.Scope", "Scope"),
            [nameof(DictionaryEditorEntryCountText)] = ("DictionaryEditor.EntryCount.Text", "Entries: {0}"),
            [nameof(DictionaryEditorSourceHeaderText)] = ("DictionaryEditor.Source.Header", "Source"),
            [nameof(DictionaryEditorTargetHeaderText)] = ("DictionaryEditor.Target.Header", "Target"),
            [nameof(DictionaryEditorEditorIdScopeHeaderText)] = ("DictionaryEditor.EditorIdScope.Header", "EDID Scope"),
            [nameof(DictionaryEditorFieldScopeHeaderText)] = ("DictionaryEditor.FieldScope.Header", "Field Scope"),
            [nameof(DictionaryEditorMatchCaseHeaderText)] = ("DictionaryEditor.MatchCase.Header", "Match Case"),
            [nameof(DictionaryEditorWholeWordHeaderText)] = ("DictionaryEditor.WholeWord.Header", "Whole Word"),
            [nameof(DictionaryEditorScopePreviewHeaderText)] = ("DictionaryEditor.ScopePreview.Header", "Scope Preview"),
            [nameof(DictionaryEditorScopeGlobalText)] = ("DictionaryEditor.ScopeGlobal.Text", "Global"),
            [nameof(DictionaryEditorAddButtonContent)] = ("DictionaryEditor.AddButton.Content", "Add"),
            [nameof(DictionaryEditorDuplicateButtonContent)] = ("DictionaryEditor.DuplicateButton.Content", "Duplicate"),
            [nameof(DictionaryEditorDeleteButtonContent)] = ("DictionaryEditor.DeleteButton.Content", "Delete"),
            [nameof(DictionaryEditorApplyButtonContent)] = ("DictionaryEditor.ApplyButton.Content", "Apply"),
            [nameof(DictionaryEditorCloseButtonContent)] = ("DictionaryEditor.CloseButton.Content", "Close"),
            [nameof(DictionaryEditorPasteButtonContent)] = ("DictionaryEditor.PasteButton.Content", "Paste CSV/TSV"),
            [nameof(DictionaryEditorPasteDialogTitle)] = ("DictionaryEditor.PasteDialog.Title", "Paste CSV/TSV Dictionary Entries"),
            [nameof(DictionaryEditorPastePlaceholderText)] = ("DictionaryEditor.Paste.PlaceholderText", "Paste CSV/TSV lines here. Columns: source,target,editorIdPattern,fieldPattern,matchCase,wholeWord"),
            [nameof(DictionaryEditorPasteApplyButtonContent)] = ("DictionaryEditor.PasteApplyButton.Content", "Parse And Add"),
            [nameof(DictionaryEditorPasteCancelButtonContent)] = ("DictionaryEditor.PasteCancelButton.Content", "Cancel"),
            [nameof(MenuRefreshWorkspace)] = ("Menu.RefreshWorkspace", "Refresh Workspace"),
            [nameof(MenuFilter)] = ("Menu.Filter", "Filter"),
            [nameof(MenuShowLockedRows)] = ("Menu.ShowLockedRows", "Show Locked Rows"),
            [nameof(WorkspaceMenuUiLanguageText)] = ("WorkspaceMenuUiLanguage.Text", "UI Language"),
            [nameof(WorkspaceMenuGameText)] = ("WorkspaceMenuGame.Text", "Game"),
            [nameof(WorkspaceMenuSourceLanguageText)] = ("WorkspaceMenuSourceLanguage.Text", "Source Language"),
            [nameof(WorkspaceMenuTargetLanguageText)] = ("WorkspaceMenuTargetLanguage.Text", "Target Language"),
            [nameof(WorkspaceMenuEncodingModeText)] = ("WorkspaceMenuEncodingMode.Text", "Encoding Mode"),
            [nameof(WorkspaceMenuEncodingManualText)] = ("WorkspaceMenuEncodingManual.Text", "Encoding (Manual)"),
            [nameof(WorkspaceMenuApplyEncodingText)] = ("WorkspaceMenuApplyEncoding.Text", "Apply Encoding (Reopen)"),
            [nameof(PluginSwitcherTitleText)] = ("PluginSwitcherTitle.Text", "Plugin Switcher"),
            [nameof(PluginSwitcherSearchPlaceholder)] = ("PluginSwitcherSearchBox.PlaceholderText", "Search by plugin name or path"),
            [nameof(PluginSwitcherPinTooltip)] = ("PluginSwitcherPinButton.ToolTipService.ToolTip", "Pin or unpin plugin"),
            [nameof(PluginSwitcherOpenButtonContent)] = ("PluginSwitcherOpenButton.Content", "Open Plugin..."),
            [nameof(PluginSwitcherSaveButtonContent)] = ("PluginSwitcherSaveButton.Content", "Save"),
            [nameof(MetricsTotalRowsLabelText)] = ("MetricsTotalRowsLabel.Text", "Total Rows"),
            [nameof(MetricsTranslatedLabelText)] = ("MetricsTranslatedLabel.Text", "Translated"),
            [nameof(MetricsPendingLabelText)] = ("MetricsPendingLabel.Text", "Pending"),
            [nameof(MetricsCompletionLabelText)] = ("MetricsCompletionLabel.Text", "Completion"),
            [nameof(MainSearchBoxHeader)] = ("MainSearchBox.Header", "Search"),
            [nameof(MainSearchBoxPlaceholderText)] = ("MainSearchBox.PlaceholderText", "EDID / Source / Translation / Field"),
            [nameof(MainListFilterBoxHeader)] = ("MainListFilterBox.Header", "List"),
            [nameof(MainOnlyPendingToggleHeader)] = ("MainOnlyPendingToggle.Header", "Only Pending"),
            [nameof(MainIncludeLockedToggleHeader)] = ("MainIncludeLockedToggle.Header", "Include Locked"),
            [nameof(MainTableHeaderEdidText)] = ("MainTableHeaderEdid.Text", "EDID"),
            [nameof(MainTableHeaderFieldText)] = ("MainTableHeaderField.Text", "Field"),
            [nameof(MainTableHeaderSourceText)] = ("MainTableHeaderSource.Text", "Source"),
            [nameof(MainTableHeaderTranslationText)] = ("MainTableHeaderTranslation.Text", "Translation"),
            [nameof(InspectorSectionTitleText)] = ("InspectorSectionTitle.Text", "Row Inspector"),
            [nameof(InspectorEdidLabelText)] = ("InspectorEdidLabel.Text", "EDID"),
            [nameof(InspectorFieldLabelText)] = ("InspectorFieldLabel.Text", "Field"),
            [nameof(InspectorSourceBoxHeader)] = ("InspectorSourceBox.Header", "Source"),
            [nameof(InspectorTranslationBoxHeader)] = ("InspectorTranslationBox.Header", "Translation"),
            [nameof(InspectorApplyButtonContent)] = ("InspectorApplyButton.Content", "Apply"),
            [nameof(InspectorCopySourceButtonContent)] = ("InspectorCopySourceButton.Content", "Copy Source"),
            [nameof(InspectorMarkValidatedButtonContent)] = ("InspectorMarkValidatedButton.Content", "Mark Validated"),
            [nameof(InspectorToggleLockButtonContent)] = ("InspectorToggleLockButton.Content", "Toggle Lock"),
            [nameof(InspectorAddToDictionaryButtonContent)] = ("InspectorAddToDictionaryButton.Content", "Add To Dictionary"),
            [nameof(InspectorProviderMovedHintText)] = ("InspectorProviderMovedHint.Text", "Provider configuration has moved to Actions -> Provider -> Configuration -> Open Configuration Window."),
            [nameof(AiWorkflowPageTitleText)] = ("AiWorkflowPageTitle.Text", "AI Workflow Designer"),
            [nameof(AiWorkflowChainBuilderTitleText)] = ("AiWorkflowChainBuilderTitle.Text", "Provider Chain Builder"),
            [nameof(AiWorkflowMoveUpButtonContent)] = ("AiWorkflowMoveUpButton.Content", "Up"),
            [nameof(AiWorkflowMoveDownButtonContent)] = ("AiWorkflowMoveDownButton.Content", "Down"),
            [nameof(AiWorkflowAllButtonContent)] = ("AiWorkflowAllButton.Content", "All"),
            [nameof(AiWorkflowNoneButtonContent)] = ("AiWorkflowNoneButton.Content", "None"),
            [nameof(AiWorkflowProfileTitleText)] = ("AiWorkflowProfileTitle.Text", "Workflow Profile"),
            [nameof(AiWorkflowSourceLanguageBoxHeader)] = ("AiWorkflowSourceLanguageBox.Header", "Source Language"),
            [nameof(AiWorkflowTargetLanguageBoxHeader)] = ("AiWorkflowTargetLanguageBox.Header", "Target Language"),
            [nameof(AiWorkflowLoadStringsToggleHeader)] = ("AiWorkflowLoadStringsToggle.Header", "Load STRINGS"),
            [nameof(AiWorkflowLoadFieldsToggleHeader)] = ("AiWorkflowLoadFieldsToggle.Header", "Load Record Fields"),
            [nameof(AiWorkflowCurrentChainBoxHeader)] = ("AiWorkflowCurrentChainBox.Header", "Current Workflow Chain"),
            [nameof(AiWorkflowRunButtonContent)] = ("AiWorkflowRunButton.Content", "Run Workflow"),
            [nameof(AiWorkflowTipTextText)] = ("AiWorkflowTipText.Text", "Tip: Enable/disable providers and reorder them to define fallback sequence. The first available provider runs first, failures fall through automatically."),
            [nameof(ProviderConfigPageTitleText)] = ("ProviderConfigPageTitle.Text", "Provider Configuration"),
            [nameof(ProviderConfigProviderBoxHeader)] = ("ProviderConfigProviderBox.Header", "Provider"),
            [nameof(ProviderConfigSelectedProviderTitleText)] = ("ProviderConfigSelectedProviderTitle.Text", "Selected Provider"),
            [nameof(ProviderConfigBaseUrlBoxHeader)] = ("ProviderConfigBaseUrlBox.Header", "Base Url"),
            [nameof(ProviderConfigModelBoxHeader)] = ("ProviderConfigModelBox.Header", "Model"),
            [nameof(ProviderConfigRegionBoxHeader)] = ("ProviderConfigRegionBox.Header", "Region"),
            [nameof(ProviderConfigOrganizationBoxHeader)] = ("ProviderConfigOrganizationBox.Header", "Organization"),
            [nameof(ProviderConfigApiKeyBoxHeader)] = ("ProviderConfigApiKeyBox.Header", "API Key (DPAPI)"),
            [nameof(ProviderConfigApiKeyBoxPlaceholderText)] = ("ProviderConfigApiKeyBox.PlaceholderText", "Stored securely via DPAPI"),
            [nameof(ProviderConfigApiSecretBoxHeader)] = ("ProviderConfigApiSecretBox.Header", "API Secret (DPAPI)"),
            [nameof(ProviderConfigApiSecretBoxPlaceholderText)] = ("ProviderConfigApiSecretBox.PlaceholderText", "For Baidu/Tencent dual-key providers"),
            [nameof(ProviderConfigSessionTokenBoxHeader)] = ("ProviderConfigSessionTokenBox.Header", "Session Token (optional, DPAPI)"),
            [nameof(ProviderConfigPromptTemplateBoxHeader)] = ("ProviderConfigPromptTemplateBox.Header", "Prompt Template"),
            [nameof(ProviderConfigLanguageMapBoxHeader)] = ("ProviderConfigLanguageMapBox.Header", "Language Map (key=value per line)"),
            [nameof(ProviderConfigSaveButtonContent)] = ("ProviderConfigSaveButton.Content", "Save Config"),
            [nameof(ProviderConfigReloadButtonContent)] = ("ProviderConfigReloadButton.Content", "Reload"),
            [nameof(ProviderConfigTestButtonContent)] = ("ProviderConfigTestButton.Content", "Test"),
            [nameof(ProviderConfigImportButtonContent)] = ("ProviderConfigImportButton.Content", "Import ApiTranslator.txt")
        };

    public LocalizedUiText(IAppLocalizationService localizationService)
    {
        _localizationService = localizationService;
        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    public string AppWindowTitle => Get();
    public string MainMenuFileTitle => Get();
    public string MainMenuTranslateTitle => Get();
    public string MainMenuProvidersTitle => Get();
    public string MainMenuViewTitle => Get();
    public string MainMenuActionsTitle => Get();
    public string MainMenuWorkspaceTitle => Get();
    public string MenuOpenPlugin => Get();
    public string MenuSave => Get();
    public string MenuOpenAndAutoTranslate => Get();
    public string MenuPaths => Get();
    public string MenuOptional => Get();
    public string MenuPluginPath => Get();
    public string MenuOutputPath => Get();
    public string MenuStringsDirectory => Get();
    public string MenuRecordDefsPath => Get();
    public string MenuRunBatchTranslation => Get();
    public string MenuScope => Get();
    public string MenuSelection => Get();
    public string MenuLoadStrings => Get();
    public string MenuLoadRecordFields => Get();
    public string MenuOnlyPendingRows => Get();
    public string MenuIncludeLockedRows => Get();
    public string MenuCopySourceToTranslation => Get();
    public string MenuApplyInspectorEdit => Get();
    public string MenuMarkSelectedValidated => Get();
    public string MenuToggleLock => Get();
    public string MenuReloadMetadata => Get();
    public string MenuAiWorkflow => Get();
    public string MenuOpenWorkflowDesigner => Get();
    public string MenuChain => Get();
    public string MenuMoveSelectedUp => Get();
    public string MenuMoveSelectedDown => Get();
    public string MenuEnableAll => Get();
    public string MenuDisableAll => Get();
    public string MenuCurrentChain => Get();
    public string MenuConfiguration => Get();
    public string MenuOpenConfigurationWindow => Get();
    public string MenuSaveSelectedProvider => Get();
    public string MenuReloadSelectedProvider => Get();
    public string MenuTestSelectedProvider => Get();
    public string MenuImportApiTranslator => Get();
    public string MenuDictionary => Get();
    public string MenuEditDictionary => Get();
    public string MenuAddSelectedRowToDictionary => Get();
    public string MenuEspCompare => Get();
    public string MenuCompareWithEsp => Get();
    public string MenuApplyEspCompareReplacements => Get();
    public string MenuImportDictionary => Get();
    public string MenuExportDictionary => Get();
    public string MenuEnableDictionaryPreReplace => Get();
    public string DictionaryEditorDialogTitle => Get();
    public string DictionaryEditorSearchHeaderText => Get();
    public string DictionaryEditorSearchPlaceholderText => Get();
    public string DictionaryEditorSortHeaderText => Get();
    public string DictionaryEditorSortBySourceText => Get();
    public string DictionaryEditorSortByTargetText => Get();
    public string DictionaryEditorSortByScopeText => Get();
    public string DictionaryEditorEntryCountText => Get();
    public string DictionaryEditorSourceHeaderText => Get();
    public string DictionaryEditorTargetHeaderText => Get();
    public string DictionaryEditorEditorIdScopeHeaderText => Get();
    public string DictionaryEditorFieldScopeHeaderText => Get();
    public string DictionaryEditorMatchCaseHeaderText => Get();
    public string DictionaryEditorWholeWordHeaderText => Get();
    public string DictionaryEditorScopePreviewHeaderText => Get();
    public string DictionaryEditorScopeGlobalText => Get();
    public string DictionaryEditorAddButtonContent => Get();
    public string DictionaryEditorDuplicateButtonContent => Get();
    public string DictionaryEditorDeleteButtonContent => Get();
    public string DictionaryEditorApplyButtonContent => Get();
    public string DictionaryEditorCloseButtonContent => Get();
    public string DictionaryEditorPasteButtonContent => Get();
    public string DictionaryEditorPasteDialogTitle => Get();
    public string DictionaryEditorPastePlaceholderText => Get();
    public string DictionaryEditorPasteApplyButtonContent => Get();
    public string DictionaryEditorPasteCancelButtonContent => Get();
    public string MenuRefreshWorkspace => Get();
    public string MenuFilter => Get();
    public string MenuShowLockedRows => Get();
    public string WorkspaceMenuUiLanguageText => Get();
    public string WorkspaceMenuGameText => Get();
    public string WorkspaceMenuSourceLanguageText => Get();
    public string WorkspaceMenuTargetLanguageText => Get();
    public string WorkspaceMenuEncodingModeText => Get();
    public string WorkspaceMenuEncodingManualText => Get();
    public string WorkspaceMenuApplyEncodingText => Get();
    public string PluginSwitcherTitleText => Get();
    public string PluginSwitcherSearchPlaceholder => Get();
    public string PluginSwitcherPinTooltip => Get();
    public string PluginSwitcherOpenButtonContent => Get();
    public string PluginSwitcherSaveButtonContent => Get();
    public string MetricsTotalRowsLabelText => Get();
    public string MetricsTranslatedLabelText => Get();
    public string MetricsPendingLabelText => Get();
    public string MetricsCompletionLabelText => Get();
    public string MainSearchBoxHeader => Get();
    public string MainSearchBoxPlaceholderText => Get();
    public string MainListFilterBoxHeader => Get();
    public string MainOnlyPendingToggleHeader => Get();
    public string MainIncludeLockedToggleHeader => Get();
    public string MainTableHeaderEdidText => Get();
    public string MainTableHeaderFieldText => Get();
    public string MainTableHeaderSourceText => Get();
    public string MainTableHeaderTranslationText => Get();
    public string InspectorSectionTitleText => Get();
    public string InspectorEdidLabelText => Get();
    public string InspectorFieldLabelText => Get();
    public string InspectorSourceBoxHeader => Get();
    public string InspectorTranslationBoxHeader => Get();
    public string InspectorApplyButtonContent => Get();
    public string InspectorCopySourceButtonContent => Get();
    public string InspectorMarkValidatedButtonContent => Get();
    public string InspectorToggleLockButtonContent => Get();
    public string InspectorAddToDictionaryButtonContent => Get();
    public string InspectorProviderMovedHintText => Get();
    public string AiWorkflowPageTitleText => Get();
    public string AiWorkflowChainBuilderTitleText => Get();
    public string AiWorkflowMoveUpButtonContent => Get();
    public string AiWorkflowMoveDownButtonContent => Get();
    public string AiWorkflowAllButtonContent => Get();
    public string AiWorkflowNoneButtonContent => Get();
    public string AiWorkflowProfileTitleText => Get();
    public string AiWorkflowSourceLanguageBoxHeader => Get();
    public string AiWorkflowTargetLanguageBoxHeader => Get();
    public string AiWorkflowLoadStringsToggleHeader => Get();
    public string AiWorkflowLoadFieldsToggleHeader => Get();
    public string AiWorkflowCurrentChainBoxHeader => Get();
    public string AiWorkflowRunButtonContent => Get();
    public string AiWorkflowTipTextText => Get();
    public string ProviderConfigPageTitleText => Get();
    public string ProviderConfigProviderBoxHeader => Get();
    public string ProviderConfigSelectedProviderTitleText => Get();
    public string ProviderConfigBaseUrlBoxHeader => Get();
    public string ProviderConfigModelBoxHeader => Get();
    public string ProviderConfigRegionBoxHeader => Get();
    public string ProviderConfigOrganizationBoxHeader => Get();
    public string ProviderConfigApiKeyBoxHeader => Get();
    public string ProviderConfigApiKeyBoxPlaceholderText => Get();
    public string ProviderConfigApiSecretBoxHeader => Get();
    public string ProviderConfigApiSecretBoxPlaceholderText => Get();
    public string ProviderConfigSessionTokenBoxHeader => Get();
    public string ProviderConfigPromptTemplateBoxHeader => Get();
    public string ProviderConfigLanguageMapBoxHeader => Get();
    public string ProviderConfigSaveButtonContent => Get();
    public string ProviderConfigReloadButtonContent => Get();
    public string ProviderConfigTestButtonContent => Get();
    public string ProviderConfigImportButtonContent => Get();

    private string Get([CallerMemberName] string propertyName = "")
    {
        if (!ResourceMap.TryGetValue(propertyName, out var entry))
        {
            return propertyName;
        }

        return _localizationService.GetString(entry.ResourceKey, entry.Fallback);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var propertyName in ResourceMap.Keys)
        {
            OnPropertyChanged(propertyName);
        }
    }
}
