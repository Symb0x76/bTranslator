using bTranslator.App.ViewModels;
using System.ComponentModel;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace bTranslator.App.Views;

public partial class MainPage : Page
{
    private Window? _workflowWindow;
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
        app.MainWindow.Title = "bTranslator";
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
                ViewModel.AvailableLanguages,
                ViewModel.SourceLanguage,
                "WorkspaceSourceLanguageGroup",
                value => ViewModel.SourceLanguage = value);

            BuildWorkspaceMenuGroup(
                WorkspaceTargetLanguageMenu,
                ViewModel.AvailableLanguages,
                ViewModel.TargetLanguage,
                "WorkspaceTargetLanguageGroup",
                value => ViewModel.TargetLanguage = value);

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
                GroupName = groupName,
                IsChecked = string.Equals(value, selected, StringComparison.Ordinal)
            };

            menuItem.Click += (_, _) => onSelected(value);
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
            or nameof(MainViewModel.TargetLanguage))
        {
            SyncWorkspaceMenuChecks();
        }
    }

    private void SyncWorkspaceMenuChecks()
    {
        SyncMenuCheck(WorkspaceGameMenu, ViewModel.SelectedGame);
        SyncMenuCheck(WorkspaceSourceLanguageMenu, ViewModel.SourceLanguage);
        SyncMenuCheck(WorkspaceTargetLanguageMenu, ViewModel.TargetLanguage);
    }

    private static void SyncMenuCheck(MenuFlyoutSubItem menu, string selected)
    {
        foreach (var item in menu.Items)
        {
            if (item is RadioMenuFlyoutItem radio)
            {
                radio.IsChecked = string.Equals(radio.Text, selected, StringComparison.Ordinal);
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
            ViewModel.StatusText = "Cannot open file picker: main window handle unavailable.";
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
            Title = "AI Workflow Designer",
            Content = new AiWorkflowPage(ViewModel)
        };
        _workflowWindow.Closed += (_, _) => _workflowWindow = null;
        _workflowWindow.Activate();
    }
}

