using bTranslator.App.Localization;
using bTranslator.App.ViewModels;
using System.ComponentModel;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Windows.UI.Core;
using Windows.System;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace bTranslator.App.Views;

public partial class MainPage : Page
{
    private Window? _providerConfigurationWindow;
    private bool _titleBarConfigured;
    private bool _workspaceMenusInitialized;

    public MainPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += async (_, _) =>
        {
            ConfigureTitleBar();
            await ViewModel.RefreshAsync();
            EnsureWorkspaceMenus();
        };
        Unloaded += (_, _) => ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    public MainViewModel ViewModel { get; }

    private void ConfigureTitleBar()
    {
        if (_titleBarConfigured)
        {
            return;
        }

        if (Microsoft.UI.Xaml.Application.Current is not App app || app.MainWindow is null)
        {
            return;
        }

        app.MainWindow.ExtendsContentIntoTitleBar = true;
        app.MainWindow.SetTitleBar(TopTitleBarHost);
        app.MainWindow.Title = ViewModel.Ui.AppWindowTitle;
        _titleBarConfigured = true;
    }

    private void EnsureWorkspaceMenus()
    {
        if (!_workspaceMenusInitialized)
        {
            BuildWorkspaceMenuGroup(
                WorkspaceGameMenu,
                ViewModel.Games,
                ViewModel.SelectedGame,
                "WorkspaceGameGroup",
                value => ViewModel.SelectedGame = value);

            BuildWorkspaceMenuGroup(
                WorkspaceSourceLanguageMenu,
                ViewModel.AvailableLanguageOptions,
                ViewModel.SourceLanguage,
                "WorkspaceSourceLanguageGroup",
                value => ViewModel.SourceLanguage = value);

            BuildWorkspaceMenuGroup(
                WorkspaceTargetLanguageMenu,
                ViewModel.AvailableLanguageOptions,
                ViewModel.TargetLanguage,
                "WorkspaceTargetLanguageGroup",
                value => ViewModel.TargetLanguage = value);

            BuildWorkspaceMenuGroup(
                WorkspaceEncodingModeMenu,
                ViewModel.EncodingModeOptions,
                ViewModel.SelectedEncodingMode,
                "WorkspaceEncodingModeGroup",
                value => ViewModel.SelectedEncodingMode = value);

            BuildWorkspaceMenuGroup(
                WorkspaceEncodingMenu,
                ViewModel.AvailableEncodings,
                ViewModel.SelectedEncodingName,
                "WorkspaceEncodingGroup",
                value => ViewModel.SelectedEncodingName = value);

            BuildWorkspaceLanguageMenuGroup(
                WorkspaceUiLanguageMenu,
                ViewModel.UiLanguages,
                ViewModel.SelectedUiLanguageTag,
                "WorkspaceUiLanguageGroup",
                value => ViewModel.SelectedUiLanguageTag = value);

            _workspaceMenusInitialized = true;
        }

        SyncWorkspaceMenuChecks();
    }

    private static void BuildWorkspaceMenuGroup(
        MenuFlyoutSubItem menu,
        IEnumerable<string> items,
        string selected,
        string groupName,
        Action<string> onSelected)
    {
        menu.Items.Clear();

        foreach (var itemValue in items)
        {
            var value = itemValue;
            var menuItem = new RadioMenuFlyoutItem
            {
                Text = value,
                Tag = value,
                GroupName = groupName,
                IsChecked = string.Equals(value, selected, StringComparison.Ordinal)
            };

            menuItem.Click += (_, _) => onSelected(value);
            menu.Items.Add(menuItem);
        }
    }

    private static void BuildWorkspaceMenuGroup(
        MenuFlyoutSubItem menu,
        IEnumerable<OptionItem> items,
        string selectedValue,
        string groupName,
        Action<string> onSelected)
    {
        menu.Items.Clear();

        foreach (var item in items)
        {
            var value = item.Value;
            var menuItem = new RadioMenuFlyoutItem
            {
                Text = item.DisplayName,
                Tag = value,
                GroupName = groupName,
                IsChecked = string.Equals(value, selectedValue, StringComparison.Ordinal)
            };

            menuItem.Click += (_, _) => onSelected(value);
            menu.Items.Add(menuItem);
        }
    }

    private static void BuildWorkspaceLanguageMenuGroup(
        MenuFlyoutSubItem menu,
        IEnumerable<UiLanguageOption> items,
        string selectedLanguageTag,
        string groupName,
        Action<string> onSelected)
    {
        menu.Items.Clear();

        foreach (var item in items)
        {
            var option = item;
            var menuItem = new RadioMenuFlyoutItem
            {
                Text = option.DisplayName,
                GroupName = groupName,
                Tag = option.LanguageTag,
                IsChecked = string.Equals(option.LanguageTag, selectedLanguageTag, StringComparison.OrdinalIgnoreCase)
            };

            menuItem.Click += (_, _) => onSelected(option.LanguageTag);
            menu.Items.Add(menuItem);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_workspaceMenusInitialized)
        {
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.SelectedGame)
            or nameof(MainViewModel.SourceLanguage)
            or nameof(MainViewModel.TargetLanguage)
            or nameof(MainViewModel.SelectedEncodingMode)
            or nameof(MainViewModel.SelectedEncodingName)
            or nameof(MainViewModel.SelectedUiLanguageTag))
        {
            SyncWorkspaceMenuChecks();
        }

        if (e.PropertyName == nameof(MainViewModel.UiLanguageMenuVersion))
        {
            BuildWorkspaceLanguageMenuGroup(
                WorkspaceUiLanguageMenu,
                ViewModel.UiLanguages,
                ViewModel.SelectedUiLanguageTag,
                "WorkspaceUiLanguageGroup",
                value => ViewModel.SelectedUiLanguageTag = value);
            SyncWorkspaceMenuChecks();

            if (Microsoft.UI.Xaml.Application.Current is App app && app.MainWindow is not null)
            {
                app.MainWindow.Title = ViewModel.Ui.AppWindowTitle;
            }
        }

        if (e.PropertyName == nameof(MainViewModel.UiOptionMenuVersion))
        {
            BuildWorkspaceMenuGroup(
                WorkspaceSourceLanguageMenu,
                ViewModel.AvailableLanguageOptions,
                ViewModel.SourceLanguage,
                "WorkspaceSourceLanguageGroup",
                value => ViewModel.SourceLanguage = value);
            BuildWorkspaceMenuGroup(
                WorkspaceTargetLanguageMenu,
                ViewModel.AvailableLanguageOptions,
                ViewModel.TargetLanguage,
                "WorkspaceTargetLanguageGroup",
                value => ViewModel.TargetLanguage = value);
            BuildWorkspaceMenuGroup(
                WorkspaceEncodingModeMenu,
                ViewModel.EncodingModeOptions,
                ViewModel.SelectedEncodingMode,
                "WorkspaceEncodingModeGroup",
                value => ViewModel.SelectedEncodingMode = value);
            SyncWorkspaceMenuChecks();
        }

        if (e.PropertyName == nameof(MainViewModel.SelectedRow) && ViewModel.SelectedRow is not null)
        {
            RowsListView.ScrollIntoView(ViewModel.SelectedRow);
        }
    }

    private void SyncWorkspaceMenuChecks()
    {
        SyncMenuCheck(WorkspaceGameMenu, ViewModel.SelectedGame);
        SyncMenuCheck(WorkspaceSourceLanguageMenu, ViewModel.SourceLanguage);
        SyncMenuCheck(WorkspaceTargetLanguageMenu, ViewModel.TargetLanguage);
        SyncMenuCheck(WorkspaceEncodingModeMenu, ViewModel.SelectedEncodingMode);
        SyncMenuCheck(WorkspaceEncodingMenu, ViewModel.SelectedEncodingName);
        SyncMenuCheckByTag(WorkspaceUiLanguageMenu, ViewModel.SelectedUiLanguageTag);
    }

    private static void SyncMenuCheck(MenuFlyoutSubItem menu, string selected)
    {
        foreach (var item in menu.Items)
        {
            if (item is RadioMenuFlyoutItem radio)
            {
                var value = radio.Tag?.ToString() ?? radio.Text;
                radio.IsChecked = string.Equals(value, selected, StringComparison.Ordinal);
            }
        }
    }

    private static void SyncMenuCheckByTag(MenuFlyoutSubItem menu, string selectedTag)
    {
        foreach (var item in menu.Items)
        {
            if (item is RadioMenuFlyoutItem radio)
            {
                var tag = radio.Tag?.ToString() ?? string.Empty;
                radio.IsChecked = string.Equals(tag, selectedTag, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private IntPtr GetWindowHandle()
    {
        if (Microsoft.UI.Xaml.Application.Current is not App app || app.MainWindow is null)
        {
            return IntPtr.Zero;
        }

        return WindowNative.GetWindowHandle(app.MainWindow);
    }

    private bool TryInitializePicker(object picker)
    {
        var hwnd = GetWindowHandle();
        if (hwnd == IntPtr.Zero)
        {
            ViewModel.StatusText = ViewModel.GetLocalizedString(
                "Status.FilePickerUnavailable",
                "Cannot open file picker: main window handle unavailable.");
            return false;
        }

        switch (picker)
        {
            case FileOpenPicker fileOpenPicker:
                InitializeWithWindow.Initialize(fileOpenPicker, hwnd);
                return true;
            case FileSavePicker fileSavePicker:
                InitializeWithWindow.Initialize(fileSavePicker, hwnd);
                return true;
            case FolderPicker folderPicker:
                InitializeWithWindow.Initialize(folderPicker, hwnd);
                return true;
            default:
                return false;
        }
    }

    private async Task<bool> PickPluginPathAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".esp");
        picker.FileTypeFilter.Add(".esm");
        picker.FileTypeFilter.Add(".esl");
        picker.FileTypeFilter.Add("*");

        if (!TryInitializePicker(picker))
        {
            return false;
        }

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return false;
        }

        ViewModel.PluginPath = file.Path;
        if (string.IsNullOrWhiteSpace(ViewModel.OutputPluginPath))
        {
            ViewModel.OutputPluginPath = file.Path;
        }

        return true;
    }

    private async Task<bool> PickOutputPathAsync()
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = string.IsNullOrWhiteSpace(ViewModel.PluginPath)
                ? "translated"
                : Path.GetFileNameWithoutExtension(ViewModel.PluginPath)
        };
        picker.FileTypeChoices.Add("ESP Plugin", [".esp"]);
        picker.FileTypeChoices.Add("ESM Plugin", [".esm"]);
        picker.FileTypeChoices.Add("ESL Plugin", [".esl"]);
        picker.FileTypeChoices.Add("All Files", [".*"]);

        if (!TryInitializePicker(picker))
        {
            return false;
        }

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return false;
        }

        ViewModel.OutputPluginPath = file.Path;
        return true;
    }

    private async Task<bool> PickStringsDirectoryAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        if (!TryInitializePicker(picker))
        {
            return false;
        }

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return false;
        }

        ViewModel.StringsDirectoryPath = folder.Path;
        return true;
    }

    private async Task<bool> PickRecordDefinitionsPathAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".ini");
        picker.FileTypeFilter.Add("*");

        if (!TryInitializePicker(picker))
        {
            return false;
        }

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return false;
        }

        ViewModel.RecordDefinitionsPath = file.Path;
        return true;
    }

    private async Task<string?> PickApiTranslatorConfigPathAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".ini");
        picker.FileTypeFilter.Add("*");

        if (!TryInitializePicker(picker))
        {
            return null;
        }

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickDictionaryImportPathAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        picker.FileTypeFilter.Add("*");

        if (!TryInitializePicker(picker))
        {
            return null;
        }

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickDictionaryExportPathAsync()
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = "translation-dictionary"
        };
        picker.FileTypeChoices.Add("JSON", [".json"]);

        if (!TryInitializePicker(picker))
        {
            return null;
        }

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickCompareEspPathAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".esp");
        picker.FileTypeFilter.Add(".esm");
        picker.FileTypeFilter.Add(".esl");
        picker.FileTypeFilter.Add("*");

        if (!TryInitializePicker(picker))
        {
            return null;
        }

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickMcmImportPathAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".xml");
        picker.FileTypeFilter.Add("*");

        if (!TryInitializePicker(picker))
        {
            return null;
        }

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickMcmExportPathAsync()
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = "mcm-translation"
        };
        picker.FileTypeChoices.Add("MCM/XML", [".xml"]);

        if (!TryInitializePicker(picker))
        {
            return null;
        }

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickTxtImportPathAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".sst");
        picker.FileTypeFilter.Add(".sstx");
        picker.FileTypeFilter.Add(".json");
        picker.FileTypeFilter.Add("*");

        if (!TryInitializePicker(picker))
        {
            return null;
        }

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickTxtExportPathAsync()
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = "txt-translation"
        };
        picker.FileTypeChoices.Add("TXT", [".txt"]);
        picker.FileTypeChoices.Add("SST", [".sst"]);
        picker.FileTypeChoices.Add("SSTX/JSON", [".sstx"]);
        picker.FileTypeChoices.Add("JSON", [".json"]);

        if (!TryInitializePicker(picker))
        {
            return null;
        }

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickTxtBatchScriptPathAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".yaml");
        picker.FileTypeFilter.Add(".yml");
        picker.FileTypeFilter.Add("*");

        if (!TryInitializePicker(picker))
        {
            return null;
        }

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickPexPathAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".pex");
        picker.FileTypeFilter.Add("*");

        if (!TryInitializePicker(picker))
        {
            return null;
        }

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickPexExportPathAsync()
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = "pex-strings"
        };
        picker.FileTypeChoices.Add("TXT", [".txt"]);

        if (!TryInitializePicker(picker))
        {
            return null;
        }

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private async void OnOpenPluginFromMenuClicked(object sender, RoutedEventArgs e)
    {
        if (!await PickPluginPathAsync().ConfigureAwait(true))
        {
            return;
        }

        if (ViewModel.OpenWorkspaceCommand.CanExecute(null))
        {
            await ViewModel.OpenWorkspaceCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

    private async void OnOpenPluginFromDropdownClicked(object sender, RoutedEventArgs e)
    {
        if (!await PickPluginPathAsync().ConfigureAwait(true))
        {
            return;
        }

        if (ViewModel.OpenWorkspaceCommand.CanExecute(null))
        {
            await ViewModel.OpenWorkspaceCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

    private async void OnPluginSwitcherItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not PluginSwitcherItemViewModel item)
        {
            return;
        }

        if (ViewModel.ActivatePluginSwitcherItemCommand.CanExecute(item))
        {
            await ViewModel.ActivatePluginSwitcherItemCommand.ExecuteAsync(item).ConfigureAwait(true);
        }
    }

    private async void OnPluginSwitcherPinClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PluginSwitcherItemViewModel item })
        {
            return;
        }

        if (ViewModel.TogglePluginPinCommand.CanExecute(item))
        {
            await ViewModel.TogglePluginPinCommand.ExecuteAsync(item).ConfigureAwait(true);
        }
    }

    private async void OnOpenAndTranslateFromMenuClicked(object sender, RoutedEventArgs e)
    {
        if (!await PickPluginPathAsync().ConfigureAwait(true))
        {
            return;
        }

        if (ViewModel.OpenWorkspaceCommand.CanExecute(null))
        {
            await ViewModel.OpenWorkspaceCommand.ExecuteAsync(null).ConfigureAwait(true);
        }

        if (ViewModel.RunBatchTranslationCommand.CanExecute(null))
        {
            await ViewModel.RunBatchTranslationCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

    private async void OnBrowsePluginPathClicked(object sender, RoutedEventArgs e)
    {
        await PickPluginPathAsync().ConfigureAwait(true);
    }

    private async void OnBrowseOutputPathClicked(object sender, RoutedEventArgs e)
    {
        await PickOutputPathAsync().ConfigureAwait(true);
    }

    private async void OnBrowseStringsDirectoryClicked(object sender, RoutedEventArgs e)
    {
        await PickStringsDirectoryAsync().ConfigureAwait(true);
    }

    private async void OnBrowseRecordDefinitionsPathClicked(object sender, RoutedEventArgs e)
    {
        await PickRecordDefinitionsPathAsync().ConfigureAwait(true);
    }

    private async void OnImportLegacyApiTranslatorConfigClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickApiTranslatorConfigPathAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (ViewModel.ImportLegacyApiTranslatorConfigCommand.CanExecute(path))
        {
            await ViewModel.ImportLegacyApiTranslatorConfigCommand.ExecuteAsync(path).ConfigureAwait(true);
        }
    }

    private async void OnImportDictionaryClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickDictionaryImportPathAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (ViewModel.ImportDictionaryCommand.CanExecute(path))
        {
            await ViewModel.ImportDictionaryCommand.ExecuteAsync(path).ConfigureAwait(true);
        }
    }

    private async void OnEditDictionaryClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new DictionaryEditorDialog(ViewModel)
        {
            XamlRoot = XamlRoot
        };
        _ = await dialog.ShowAsync();
    }

    private async void OnCompareEspClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickCompareEspPathAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (ViewModel.CompareEspCommand.CanExecute(path))
        {
            await ViewModel.CompareEspCommand.ExecuteAsync(path).ConfigureAwait(true);
        }
    }

    private async void OnExportDictionaryClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickDictionaryExportPathAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (ViewModel.ExportDictionaryCommand.CanExecute(path))
        {
            await ViewModel.ExportDictionaryCommand.ExecuteAsync(path).ConfigureAwait(true);
        }
    }

    private async void OnOpenMcmModeClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickMcmImportPathAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (ViewModel.OpenMcmModeCommand.CanExecute(path))
        {
            await ViewModel.OpenMcmModeCommand.ExecuteAsync(path).ConfigureAwait(true);
        }
    }

    private async void OnExportMcmModeClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickMcmExportPathAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (ViewModel.ExportMcmModeCommand.CanExecute(path))
        {
            await ViewModel.ExportMcmModeCommand.ExecuteAsync(path).ConfigureAwait(true);
        }
    }

    private async void OnOpenTxtModeClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickTxtImportPathAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (ViewModel.OpenTxtModeCommand.CanExecute(path))
        {
            await ViewModel.OpenTxtModeCommand.ExecuteAsync(path).ConfigureAwait(true);
        }
    }

    private async void OnExportTxtModeClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickTxtExportPathAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (ViewModel.ExportTxtModeCommand.CanExecute(path))
        {
            await ViewModel.ExportTxtModeCommand.ExecuteAsync(path).ConfigureAwait(true);
        }
    }

    private async void OnRunTxtBatchScriptClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickTxtBatchScriptPathAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (ViewModel.RunTxtBatchScriptCommand.CanExecute(path))
        {
            await ViewModel.RunTxtBatchScriptCommand.ExecuteAsync(path).ConfigureAwait(true);
        }
    }

    private async void OnOpenPexModeClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickPexPathAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (ViewModel.OpenPexModeCommand.CanExecute(path))
        {
            await ViewModel.OpenPexModeCommand.ExecuteAsync(path).ConfigureAwait(true);
        }
    }

    private async void OnExportPexStringsClicked(object sender, RoutedEventArgs e)
    {
        var path = await PickPexExportPathAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (ViewModel.ExportPexStringsCommand.CanExecute(path))
        {
            await ViewModel.ExportPexStringsCommand.ExecuteAsync(path).ConfigureAwait(true);
        }
    }

    private void OnOpenProviderConfigurationPageClicked(object sender, RoutedEventArgs e)
    {
        if (_providerConfigurationWindow is not null)
        {
            _providerConfigurationWindow.Activate();
            return;
        }

        _providerConfigurationWindow = new Window
        {
            Title = ViewModel.GetLocalizedString("WindowTitle.ProviderConfiguration", "Provider Configuration"),
            Content = new ProviderConfigurationPage(ViewModel)
        };
        _providerConfigurationWindow.Closed += (_, _) => _providerConfigurationWindow = null;
        _providerConfigurationWindow.Activate();
    }

    private async Task OpenPluginAndWorkspaceAsync()
    {
        if (!await PickPluginPathAsync().ConfigureAwait(true))
        {
            return;
        }

        if (ViewModel.OpenWorkspaceCommand.CanExecute(null))
        {
            await ViewModel.OpenWorkspaceCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

    private void FocusSearchBox(bool selectAll)
    {
        _ = SearchTextBox.Focus(FocusState.Keyboard);
        if (selectAll)
        {
            SearchTextBox.SelectAll();
        }
    }

    private void FocusRowsView(bool ensureSelection)
    {
        if (ensureSelection && ViewModel.SelectedRow is null && ViewModel.Rows.Count > 0)
        {
            ViewModel.SelectedRow = ViewModel.Rows[0];
        }

        if (ViewModel.SelectedRow is not null)
        {
            RowsListView.ScrollIntoView(ViewModel.SelectedRow);
        }

        _ = RowsListView.Focus(FocusState.Keyboard);
    }

    private void FocusInspectorEditor(bool selectAll)
    {
        _ = InspectorTranslationTextBox.Focus(FocusState.Keyboard);
        if (selectAll)
        {
            InspectorTranslationTextBox.SelectAll();
        }
    }

    private void FocusAiInput(bool selectAll)
    {
        _ = AiChatInputTextBox.Focus(FocusState.Keyboard);
        if (selectAll && !string.IsNullOrWhiteSpace(AiChatInputTextBox.Text))
        {
            AiChatInputTextBox.SelectAll();
        }
    }

    private void FocusModelSelector()
    {
        _ = ModelSelectorComboBox.Focus(FocusState.Keyboard);
    }

    private bool IsElementOrDescendantFocused(DependencyObject element)
    {
        var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (focused is not null)
        {
            if (ReferenceEquals(focused, element))
            {
                return true;
            }

            focused = VisualTreeHelper.GetParent(focused);
        }

        return false;
    }

    private bool IsAiInputFocused()
    {
        return IsElementOrDescendantFocused(AiChatInputTextBox);
    }

    private static bool IsShiftPressed()
    {
        return (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private async void OnOpenWorkspaceAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await OpenPluginAndWorkspaceAsync().ConfigureAwait(true);
    }

    private async void OnSaveWorkspaceAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.SaveWorkspaceCommand.CanExecute(null))
        {
            await ViewModel.SaveWorkspaceCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

    private async void OnRunBatchAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.RunBatchTranslationCommand.CanExecute(null))
        {
            await ViewModel.RunBatchTranslationCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

    private async void OnReloadAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.RefreshCommand.CanExecute(null))
        {
            await ViewModel.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

    private async void OnAiChatInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            FocusRowsView(ensureSelection: true);
            return;
        }

        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        // Keep multiline editing: Shift+Enter inserts a new line.
        if (IsShiftPressed())
        {
            return;
        }

        e.Handled = true;
        if (ViewModel.SendAiChatMessageCommand.CanExecute(null))
        {
            await ViewModel.SendAiChatMessageCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

    private void OnSearchTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            FocusRowsView(ensureSelection: true);
            return;
        }

        if (e.Key == VirtualKey.Escape && !string.IsNullOrWhiteSpace(SearchTextBox.Text))
        {
            e.Handled = true;
            SearchTextBox.Text = string.Empty;
            ViewModel.SearchText = string.Empty;
        }
    }

    private void OnRowsListViewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        FocusInspectorEditor(selectAll: true);
    }

    private void OnFocusSearchAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        FocusSearchBox(selectAll: true);
    }

    private void OnFocusRowsAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        FocusRowsView(ensureSelection: true);
    }

    private void OnFocusInspectorAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        FocusInspectorEditor(selectAll: true);
    }

    private void OnFocusAiInputAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        FocusAiInput(selectAll: false);
    }

    private void OnFocusModelAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        FocusModelSelector();
    }

    private async void OnApplyInspectorAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsAiInputFocused())
        {
            args.Handled = true;
            if (ViewModel.SendAiChatMessageCommand.CanExecute(null))
            {
                await ViewModel.SendAiChatMessageCommand.ExecuteAsync(null).ConfigureAwait(true);
            }

            return;
        }

        args.Handled = true;
        if (ViewModel.ApplyInspectorToRowCommand.CanExecute(null))
        {
            ViewModel.ApplyInspectorToRowCommand.Execute(null);
        }
    }

    private void OnApplyAndNextAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsAiInputFocused())
        {
            return;
        }

        args.Handled = true;
        if (ViewModel.ApplyInspectorAndSelectNextPendingCommand.CanExecute(null))
        {
            ViewModel.ApplyInspectorAndSelectNextPendingCommand.Execute(null);
            FocusRowsView(ensureSelection: false);
        }
    }

    private void OnCopySourceAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsAiInputFocused())
        {
            return;
        }

        args.Handled = true;
        if (ViewModel.CopySourceToTargetCommand.CanExecute(null))
        {
            ViewModel.CopySourceToTargetCommand.Execute(null);
        }
    }

    private void OnToggleLockAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsAiInputFocused())
        {
            return;
        }

        args.Handled = true;
        if (ViewModel.ToggleSelectedLockCommand.CanExecute(null))
        {
            ViewModel.ToggleSelectedLockCommand.Execute(null);
        }
    }

    private void OnMarkValidatedAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsAiInputFocused())
        {
            return;
        }

        args.Handled = true;
        if (ViewModel.MarkSelectedValidatedCommand.CanExecute(null))
        {
            ViewModel.MarkSelectedValidatedCommand.Execute(null);
        }
    }

    private void OnNextPendingAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.SelectNextPendingRowCommand.CanExecute(null))
        {
            ViewModel.SelectNextPendingRowCommand.Execute(null);
            FocusRowsView(ensureSelection: false);
        }
    }

    private void OnPreviousPendingAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.SelectPreviousPendingRowCommand.CanExecute(null))
        {
            ViewModel.SelectPreviousPendingRowCommand.Execute(null);
            FocusRowsView(ensureSelection: false);
        }
    }

    private async void OnShortcutHelpAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowShortcutHelpDialogAsync().ConfigureAwait(true);
    }

    private async Task ShowShortcutHelpDialogAsync()
    {
        var allItems = ViewModel.GetShortcutHelpItems().ToList();
        var searchBox = new TextBox
        {
            PlaceholderText = ViewModel.GetLocalizedString(
                "ShortcutHelp.SearchPlaceholder",
                "Filter by key or description")
        };
        var resultPanel = new StackPanel
        {
            Spacing = 8
        };

        void RenderShortcutItems(string? filterText)
        {
            var query = filterText?.Trim() ?? string.Empty;
            var filtered = allItems.Where(item =>
                    string.IsNullOrWhiteSpace(query) ||
                    item.Group.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Gesture.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            resultPanel.Children.Clear();

            if (filtered.Count == 0)
            {
                resultPanel.Children.Add(new TextBlock
                {
                    Opacity = 0.75,
                    Text = ViewModel.GetLocalizedString(
                        "ShortcutHelp.NoResults",
                        "No shortcuts match current filter.")
                });
                return;
            }

            foreach (var group in filtered.GroupBy(item => item.Group))
            {
                resultPanel.Children.Add(new TextBlock
                {
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI Variable Text Semibold"),
                    FontSize = 14,
                    Opacity = 0.95,
                    Text = group.Key
                });

                foreach (var item in group)
                {
                    var row = new Grid
                    {
                        ColumnSpacing = 12
                    };
                    row.ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = GridLength.Auto
                    });
                    row.ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = new GridLength(1, GridUnitType.Star)
                    });

                    var keyText = new TextBlock
                    {
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono"),
                        FontSize = 13,
                        MinWidth = 120,
                        Text = item.Gesture
                    };
                    row.Children.Add(keyText);

                    var descText = new TextBlock
                    {
                        Opacity = 0.92,
                        TextWrapping = TextWrapping.WrapWholeWords,
                        Text = item.Description
                    };
                    Grid.SetColumn(descText, 1);
                    row.Children.Add(descText);

                    resultPanel.Children.Add(row);
                }
            }
        }

        searchBox.TextChanged += (_, _) => RenderShortcutItems(searchBox.Text);
        RenderShortcutItems(string.Empty);

        var contentGrid = new Grid
        {
            RowSpacing = 10
        };
        contentGrid.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        contentGrid.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        contentGrid.Children.Add(searchBox);

        var scroller = new ScrollViewer
        {
            MaxHeight = 500,
            MinWidth = 720,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = resultPanel
        };
        Grid.SetRow(scroller, 1);
        contentGrid.Children.Add(scroller);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = ViewModel.GetLocalizedString("ShortcutHelp.DialogTitle", "Keyboard Shortcuts"),
            CloseButtonText = ViewModel.GetLocalizedString("ShortcutHelp.CloseButton", "Close"),
            DefaultButton = ContentDialogButton.Close,
            Content = contentGrid
        };

        _ = await dialog.ShowAsync();
    }
}

