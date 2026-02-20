using bTranslator.App.Localization;
using bTranslator.App.ViewModels;
using System.ComponentModel;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace bTranslator.App.Views;

public partial class MainPage : Page
{
    private Window? _workflowWindow;
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

    private void OnOpenAiWorkflowPageClicked(object sender, RoutedEventArgs e)
    {
        if (_workflowWindow is not null)
        {
            _workflowWindow.Activate();
            return;
        }

        _workflowWindow = new Window
        {
            Title = ViewModel.GetLocalizedString("WindowTitle.AiWorkflow", "AI Workflow Designer"),
            Content = new AiWorkflowPage(ViewModel)
        };
        _workflowWindow.Closed += (_, _) => _workflowWindow = null;
        _workflowWindow.Activate();
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

    private void OnFocusSearchAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _ = SearchTextBox.Focus(FocusState.Keyboard);
        SearchTextBox.SelectAll();
    }

    private void OnApplyInspectorAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.ApplyInspectorToRowCommand.CanExecute(null))
        {
            ViewModel.ApplyInspectorToRowCommand.Execute(null);
        }
    }

    private void OnApplyAndNextAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.ApplyInspectorAndSelectNextPendingCommand.CanExecute(null))
        {
            ViewModel.ApplyInspectorAndSelectNextPendingCommand.Execute(null);
        }
    }

    private void OnCopySourceAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.CopySourceToTargetCommand.CanExecute(null))
        {
            ViewModel.CopySourceToTargetCommand.Execute(null);
        }
    }

    private void OnToggleLockAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.ToggleSelectedLockCommand.CanExecute(null))
        {
            ViewModel.ToggleSelectedLockCommand.Execute(null);
        }
    }

    private void OnMarkValidatedAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
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
        }
    }

    private void OnPreviousPendingAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.SelectPreviousPendingRowCommand.CanExecute(null))
        {
            ViewModel.SelectPreviousPendingRowCommand.Execute(null);
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

