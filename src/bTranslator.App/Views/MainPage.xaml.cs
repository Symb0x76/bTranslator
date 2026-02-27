using bTranslator.App.Localization;
using bTranslator.App.ViewModels;
using System.ComponentModel;
using System.Globalization;
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
    private const double MinAiDrawerHeight = 200d;
    private const double MaxAiDrawerHeight = 620d;

    private Window? _aiProviderSetupWindow;
    private Window? _providerConfigurationWindow;
    private bool _titleBarConfigured;
    private bool _workspaceMenusInitialized;
    private bool _isAiDrawerExpanded;
    private bool _isAiDrawerResizing;
    private double _aiDrawerResizeStartY;
    private double _aiDrawerResizeStartHeight;

    public MainPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        ApplyAiDrawerHeight(ViewModel.AiDrawerHeight);
        SetAiDrawerExpanded(isExpanded: false);
        RebuildKeyboardAccelerators();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += async (_, _) =>
        {
            ConfigureTitleBar();
            await ViewModel.RefreshAsync();
            EnsureWorkspaceMenus();
            ApplyAiDrawerHeight(ViewModel.AiDrawerHeight);
            RebuildKeyboardAccelerators();
        };
        Unloaded += (_, _) => ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    public MainViewModel ViewModel { get; }

    private void OnAiDrawerToggleClicked(object sender, RoutedEventArgs e)
    {
        SetAiDrawerExpanded(!_isAiDrawerExpanded);
    }

    private void SetAiDrawerExpanded(bool isExpanded)
    {
        _isAiDrawerExpanded = isExpanded;
        AiDrawerContentGrid.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        if (isExpanded)
        {
            ApplyAiDrawerHeight(ViewModel.AiDrawerHeight);
        }

        AiDrawerToggleButton.Content = new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            Glyph = isExpanded ? "\uE70D" : "\uE70E"
        };
    }

    private void ApplyAiDrawerHeight(double height)
    {
        AiDrawerContentGrid.Height = ClampAiDrawerHeight(height);
    }

    private static double ClampAiDrawerHeight(double height)
    {
        return Math.Clamp(height, MinAiDrawerHeight, MaxAiDrawerHeight);
    }

    private void OnAiDrawerResizeHandlePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isAiDrawerResizing = true;
        _aiDrawerResizeStartY = e.GetCurrentPoint(this).Position.Y;
        _aiDrawerResizeStartHeight = AiDrawerContentGrid.Height;
        if (double.IsNaN(_aiDrawerResizeStartHeight) || _aiDrawerResizeStartHeight <= 0)
        {
            _aiDrawerResizeStartHeight = Math.Max(AiDrawerContentGrid.ActualHeight, ViewModel.AiDrawerHeight);
        }

        AiDrawerResizeHandle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnAiDrawerResizeHandlePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isAiDrawerResizing)
        {
            return;
        }

        var currentY = e.GetCurrentPoint(this).Position.Y;
        var delta = _aiDrawerResizeStartY - currentY;
        ApplyAiDrawerHeight(_aiDrawerResizeStartHeight + delta);
        e.Handled = true;
    }

    private void OnAiDrawerResizeHandlePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isAiDrawerResizing)
        {
            return;
        }

        FinishAiDrawerResize();
        e.Handled = true;
    }

    private void OnAiDrawerResizeHandlePointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (!_isAiDrawerResizing)
        {
            return;
        }

        FinishAiDrawerResize();
    }

    private void FinishAiDrawerResize()
    {
        _isAiDrawerResizing = false;
        _ = PersistAiDrawerHeightAsync();
    }

    private async Task PersistAiDrawerHeightAsync()
    {
        var currentHeight = ClampAiDrawerHeight(AiDrawerContentGrid.Height);
        ApplyAiDrawerHeight(currentHeight);
        await ViewModel.SaveAiDrawerHeightAsync(currentHeight).ConfigureAwait(true);
    }

    private void RebuildKeyboardAccelerators()
    {
        KeyboardAccelerators.Clear();

        var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mappings = ViewModel.GetEffectiveShortcutMappings();
        foreach (var pair in mappings)
        {
            if (!TryParseShortcutGesture(pair.Value, out var key, out var modifiers))
            {
                continue;
            }

            var signature = BuildShortcutSignature(key, modifiers);
            if (!registered.Add(signature))
            {
                continue;
            }

            var accelerator = new KeyboardAccelerator
            {
                Key = key,
                Modifiers = modifiers
            };

            if (!TryAttachShortcutHandler(pair.Key, accelerator))
            {
                continue;
            }

            KeyboardAccelerators.Add(accelerator);
        }
    }

    private bool TryAttachShortcutHandler(string actionId, KeyboardAccelerator accelerator)
    {
        switch (actionId)
        {
            case MainViewModel.ShortcutActionOpenWorkspace:
                accelerator.Invoked += OnOpenWorkspaceAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionSaveWorkspace:
                accelerator.Invoked += OnSaveWorkspaceAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionRunBatchTranslation:
                accelerator.Invoked += OnRunBatchAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionRefreshWorkspace:
                accelerator.Invoked += OnReloadAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionToggleTheme:
                accelerator.Invoked += OnToggleThemeAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionFocusSearch:
                accelerator.Invoked += OnFocusSearchAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionFocusRows:
                accelerator.Invoked += OnFocusRowsAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionFocusInspector:
                accelerator.Invoked += OnFocusInspectorAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionFocusAiInput:
                accelerator.Invoked += OnFocusAiInputAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionFocusModelFilter:
                accelerator.Invoked += OnFocusModelAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionClearAiChat:
                accelerator.Invoked += OnClearAiChatAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionApplyInspector:
                accelerator.Invoked += OnApplyInspectorAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionApplyAndNext:
                accelerator.Invoked += OnApplyAndNextAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionCopySource:
                accelerator.Invoked += OnCopySourceAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionToggleLock:
                accelerator.Invoked += OnToggleLockAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionMarkValidated:
                accelerator.Invoked += OnMarkValidatedAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionNextPending:
                accelerator.Invoked += OnNextPendingAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionPreviousPending:
                accelerator.Invoked += OnPreviousPendingAcceleratorInvoked;
                return true;
            case MainViewModel.ShortcutActionShowShortcutHelp:
                accelerator.Invoked += OnShortcutHelpAcceleratorInvoked;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseShortcutGesture(
        string? gesture,
        out VirtualKey key,
        out VirtualKeyModifiers modifiers)
    {
        key = VirtualKey.None;
        modifiers = VirtualKeyModifiers.None;

        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        var segments = gesture
            .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (!TryParseModifierToken(segments[i], out var modifier))
            {
                return false;
            }

            modifiers |= modifier;
        }

        return TryParseKeyToken(segments[^1], out key);
    }

    private static bool TryParseModifierToken(string token, out VirtualKeyModifiers modifier)
    {
        modifier = token.Trim().ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => VirtualKeyModifiers.Control,
            "SHIFT" => VirtualKeyModifiers.Shift,
            "ALT" => VirtualKeyModifiers.Menu,
            "WIN" or "WINDOWS" => VirtualKeyModifiers.Windows,
            _ => VirtualKeyModifiers.None
        };

        return modifier != VirtualKeyModifiers.None;
    }

    private static bool TryParseKeyToken(string token, out VirtualKey key)
    {
        var normalized = token.Trim().ToUpperInvariant();

        if (normalized.Length == 1)
        {
            var c = normalized[0];
            if (c is >= 'A' and <= 'Z')
            {
                key = (VirtualKey)((int)VirtualKey.A + (c - 'A'));
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                key = (VirtualKey)((int)VirtualKey.Number0 + (c - '0'));
                return true;
            }
        }

        if (normalized.StartsWith('F') &&
            int.TryParse(normalized[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var functionKey) &&
            functionKey is >= 1 and <= 24)
        {
            key = (VirtualKey)((int)VirtualKey.F1 + (functionKey - 1));
            return true;
        }

        key = normalized switch
        {
            "ENTER" => VirtualKey.Enter,
            "ESC" or "ESCAPE" => VirtualKey.Escape,
            "SPACE" => VirtualKey.Space,
            "TAB" => VirtualKey.Tab,
            "UP" => VirtualKey.Up,
            "DOWN" => VirtualKey.Down,
            "LEFT" => VirtualKey.Left,
            "RIGHT" => VirtualKey.Right,
            "HOME" => VirtualKey.Home,
            "END" => VirtualKey.End,
            "PAGEUP" => VirtualKey.PageUp,
            "PAGEDOWN" => VirtualKey.PageDown,
            "INSERT" => VirtualKey.Insert,
            "DELETE" or "DEL" => VirtualKey.Delete,
            _ => VirtualKey.None
        };

        if (key != VirtualKey.None)
        {
            return true;
        }

        return Enum.TryParse(normalized, ignoreCase: true, out key) && key != VirtualKey.None;
    }

    private static string BuildShortcutSignature(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        return $"{(int)modifiers}:{(int)key}";
    }

    private static string FormatShortcutGesture(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        var parts = new List<string>(4);
        if ((modifiers & VirtualKeyModifiers.Control) == VirtualKeyModifiers.Control)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & VirtualKeyModifiers.Shift) == VirtualKeyModifiers.Shift)
        {
            parts.Add("Shift");
        }

        if ((modifiers & VirtualKeyModifiers.Menu) == VirtualKeyModifiers.Menu)
        {
            parts.Add("Alt");
        }

        if ((modifiers & VirtualKeyModifiers.Windows) == VirtualKeyModifiers.Windows)
        {
            parts.Add("Win");
        }

        parts.Add(FormatShortcutKeyToken(key));
        return string.Join("+", parts);
    }

    private static string FormatShortcutKeyToken(VirtualKey key)
    {
        if (key is >= VirtualKey.A and <= VirtualKey.Z)
        {
            return ((char)('A' + ((int)key - (int)VirtualKey.A))).ToString(CultureInfo.InvariantCulture);
        }

        if (key is >= VirtualKey.Number0 and <= VirtualKey.Number9)
        {
            return ((char)('0' + ((int)key - (int)VirtualKey.Number0))).ToString(CultureInfo.InvariantCulture);
        }

        if (key is >= VirtualKey.F1 and <= VirtualKey.F24)
        {
            return $"F{(int)key - (int)VirtualKey.F1 + 1}";
        }

        return key switch
        {
            VirtualKey.Enter => "Enter",
            VirtualKey.Escape => "Esc",
            VirtualKey.Space => "Space",
            VirtualKey.Tab => "Tab",
            VirtualKey.Up => "Up",
            VirtualKey.Down => "Down",
            VirtualKey.Left => "Left",
            VirtualKey.Right => "Right",
            VirtualKey.Home => "Home",
            VirtualKey.End => "End",
            VirtualKey.PageUp => "PageUp",
            VirtualKey.PageDown => "PageDown",
            VirtualKey.Insert => "Insert",
            VirtualKey.Delete => "Delete",
            _ => key.ToString()
        };
    }

    private string LocalizedFormat(string resourceKey, string fallbackTemplate, params object?[] args)
    {
        var template = ViewModel.GetLocalizedString(resourceKey, fallbackTemplate);
        try
        {
            return string.Format(CultureInfo.CurrentUICulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

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

            BuildWorkspaceMenuGroup(
                ViewThemeMenu,
                ViewModel.ThemeOptions,
                ViewModel.SelectedTheme,
                "ViewThemeGroup",
                value => ViewModel.SelectedTheme = value);

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
            or nameof(MainViewModel.SelectedUiLanguageTag)
            or nameof(MainViewModel.SelectedTheme))
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

            SetAiDrawerExpanded(_isAiDrawerExpanded);
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
            BuildWorkspaceMenuGroup(
                ViewThemeMenu,
                ViewModel.ThemeOptions,
                ViewModel.SelectedTheme,
                "ViewThemeGroup",
                value => ViewModel.SelectedTheme = value);
            SyncWorkspaceMenuChecks();
        }

        if (e.PropertyName == nameof(MainViewModel.AiDrawerHeight))
        {
            ApplyAiDrawerHeight(ViewModel.AiDrawerHeight);
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
        SyncMenuCheck(ViewThemeMenu, ViewModel.SelectedTheme);
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

    private void OnOpenAiProviderSetupPageClicked(object sender, RoutedEventArgs e)
    {
        if (_aiProviderSetupWindow is not null)
        {
            _aiProviderSetupWindow.Activate();
            return;
        }

        _aiProviderSetupWindow = new Window
        {
            Title = ViewModel.GetLocalizedString("WindowTitle.AiProviderSetup", "AI Token / API Setup"),
            Content = new AiProviderSetupPage(ViewModel)
        };
        _aiProviderSetupWindow.Closed += (_, _) => _aiProviderSetupWindow = null;
        _aiProviderSetupWindow.Activate();
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
        if (!_isAiDrawerExpanded)
        {
            SetAiDrawerExpanded(isExpanded: true);
        }

        _ = AiChatInputTextBox.Focus(FocusState.Keyboard);
        if (selectAll && !string.IsNullOrWhiteSpace(AiChatInputTextBox.Text))
        {
            AiChatInputTextBox.SelectAll();
        }
    }

    private void FocusModelSelector(bool openDropdown)
    {
        _ = ModelSelectorComboBox.Focus(FocusState.Keyboard);
        if (openDropdown)
        {
            ModelSelectorComboBox.IsDropDownOpen = true;
        }
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

        if (e.Key == VirtualKey.Tab)
        {
            e.Handled = true;
            if (IsShiftPressed())
            {
                FocusInspectorEditor(selectAll: false);
            }
            else
            {
                FocusSearchBox(selectAll: true);
            }

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
        if (e.Key == VirtualKey.Tab)
        {
            e.Handled = true;
            if (IsShiftPressed())
            {
                FocusAiInput(selectAll: false);
            }
            else
            {
                FocusRowsView(ensureSelection: true);
            }

            return;
        }

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
        if (e.Key == VirtualKey.Tab)
        {
            e.Handled = true;
            if (IsShiftPressed())
            {
                FocusSearchBox(selectAll: true);
            }
            else
            {
                FocusInspectorEditor(selectAll: true);
            }

            return;
        }

        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        FocusInspectorEditor(selectAll: true);
    }

    private void OnInspectorTranslationTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Tab)
        {
            e.Handled = true;
            if (IsShiftPressed())
            {
                FocusRowsView(ensureSelection: true);
            }
            else
            {
                FocusAiInput(selectAll: false);
            }

            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            FocusRowsView(ensureSelection: true);
        }
    }

    private void OnFocusSearchAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        FocusSearchBox(selectAll: true);
    }

    private void OnToggleThemeAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.ToggleThemeShortcut();
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
        FocusModelSelector(openDropdown: true);
    }

    private void OnClearAiChatAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.ClearAiChatHistoryCommand.CanExecute(null))
        {
            ViewModel.ClearAiChatHistoryCommand.Execute(null);
        }
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
            PrimaryButtonText = ViewModel.GetLocalizedString("ShortcutHelp.ConfigureButton", "Configure..."),
            CloseButtonText = ViewModel.GetLocalizedString("ShortcutHelp.CloseButton", "Close"),
            DefaultButton = ContentDialogButton.Close,
            Content = contentGrid
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ShowShortcutMappingDialogAsync().ConfigureAwait(true);
        }
    }

    private async Task ShowShortcutMappingDialogAsync()
    {
        var bindingItems = ViewModel.GetShortcutBindingItems()
            .OrderBy(item => item.Group, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Description, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var descriptionByActionId = bindingItems.ToDictionary(
            static item => item.ActionId,
            static item => item.Description,
            StringComparer.OrdinalIgnoreCase);
        var editValues = new Dictionary<string, string>(
            ViewModel.GetEffectiveShortcutMappings(),
            StringComparer.OrdinalIgnoreCase);
        var editorByActionId = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
        var rowByActionId = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase);
        var defaultBorderBrushByActionId = new Dictionary<string, Brush?>(StringComparer.OrdinalIgnoreCase);
        var defaultBorderThicknessByActionId = new Dictionary<string, Thickness>(StringComparer.OrdinalIgnoreCase);
        var conflictBorderBrush = Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(
                "SystemFillColorCriticalBrush",
                out var criticalBrushObject)
            ? criticalBrushObject as Brush
            : new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);

        var searchBox = new TextBox
        {
            PlaceholderText = ViewModel.GetLocalizedString(
                "ShortcutConfig.SearchPlaceholder",
                "Filter actions or gestures")
        };

        var validationText = new TextBlock
        {
            Opacity = 0.9,
            TextWrapping = TextWrapping.WrapWholeWords,
            Visibility = Visibility.Collapsed
        };

        var rowsPanel = new StackPanel
        {
            Spacing = 8
        };

        void ResetGestureEditorHighlights()
        {
            foreach (var pair in editorByActionId)
            {
                if (defaultBorderBrushByActionId.TryGetValue(pair.Key, out var borderBrush))
                {
                    pair.Value.BorderBrush = borderBrush;
                }

                if (defaultBorderThicknessByActionId.TryGetValue(pair.Key, out var borderThickness))
                {
                    pair.Value.BorderThickness = borderThickness;
                }
            }
        }

        void HighlightConflictingGestureEditors(IReadOnlyList<string> conflictingActionIds)
        {
            var conflictActionIds = conflictingActionIds
                .Where(static actionId => !string.IsNullOrWhiteSpace(actionId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (conflictActionIds.Length == 0)
            {
                return;
            }

            var hasHiddenConflictRows = conflictActionIds.Any(actionId => !editorByActionId.ContainsKey(actionId));
            if (hasHiddenConflictRows && !string.IsNullOrWhiteSpace(searchBox.Text))
            {
                searchBox.Text = string.Empty;
                RenderRows(string.Empty);
            }

            TextBox? firstGestureEditor = null;
            FrameworkElement? firstConflictRow = null;
            foreach (var actionId in conflictActionIds)
            {
                if (editorByActionId.TryGetValue(actionId, out var gestureEditor))
                {
                    gestureEditor.BorderBrush = conflictBorderBrush;
                    gestureEditor.BorderThickness = new Thickness(2);
                    firstGestureEditor ??= gestureEditor;
                }

                if (firstConflictRow is null &&
                    rowByActionId.TryGetValue(actionId, out var conflictRow))
                {
                    firstConflictRow = conflictRow;
                }
            }

            firstConflictRow?.StartBringIntoView(new BringIntoViewOptions
            {
                AnimationDesired = true
            });
            if (firstGestureEditor is null)
            {
                return;
            }

            _ = firstGestureEditor.Focus(FocusState.Programmatic);
            firstGestureEditor.SelectAll();
        }

        void RenderRows(string? filterText)
        {
            var query = filterText?.Trim() ?? string.Empty;
            var filtered = bindingItems.Where(item =>
                    string.IsNullOrWhiteSpace(query) ||
                    item.Group.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Gesture.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (editValues.TryGetValue(item.ActionId, out var currentGesture) &&
                     currentGesture.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            rowsPanel.Children.Clear();
            editorByActionId.Clear();
            rowByActionId.Clear();
            defaultBorderBrushByActionId.Clear();
            defaultBorderThicknessByActionId.Clear();
            if (filtered.Count == 0)
            {
                rowsPanel.Children.Add(new TextBlock
                {
                    Opacity = 0.75,
                    Text = ViewModel.GetLocalizedString(
                        "ShortcutConfig.NoResults",
                        "No shortcut action matches current filter.")
                });
                return;
            }

            foreach (var group in filtered.GroupBy(item => item.Group))
            {
                rowsPanel.Children.Add(new TextBlock
                {
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI Variable Text Semibold"),
                    FontSize = 14,
                    Opacity = 0.95,
                    Text = group.Key
                });

                foreach (var item in group)
                {
                    if (!editValues.TryGetValue(item.ActionId, out var currentGesture))
                    {
                        currentGesture = item.Gesture;
                    }

                    var row = new Grid
                    {
                        ColumnSpacing = 12
                    };
                    rowByActionId[item.ActionId] = row;
                    row.ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = new GridLength(1, GridUnitType.Star)
                    });
                    row.ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = new GridLength(190)
                    });

                    var descriptionText = new TextBlock
                    {
                        Opacity = 0.92,
                        Text = item.Description,
                        TextWrapping = TextWrapping.WrapWholeWords
                    };
                    row.Children.Add(descriptionText);

                    var gestureEditor = new TextBox
                    {
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono"),
                        PlaceholderText = item.Gesture,
                        Text = currentGesture
                    };
                    editorByActionId[item.ActionId] = gestureEditor;
                    defaultBorderBrushByActionId[item.ActionId] = gestureEditor.BorderBrush;
                    defaultBorderThicknessByActionId[item.ActionId] = gestureEditor.BorderThickness;
                    gestureEditor.TextChanged += (_, _) =>
                    {
                        editValues[item.ActionId] = gestureEditor.Text.Trim();
                        ResetGestureEditorHighlights();
                        validationText.Text = string.Empty;
                        validationText.Visibility = Visibility.Collapsed;
                    };
                    Grid.SetColumn(gestureEditor, 1);
                    row.Children.Add(gestureEditor);

                    rowsPanel.Children.Add(row);
                }
            }
        }

        searchBox.TextChanged += (_, _) => RenderRows(searchBox.Text);
        RenderRows(string.Empty);

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
            Height = GridLength.Auto
        });
        contentGrid.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        contentGrid.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        var tipText = new TextBlock
        {
            Opacity = 0.78,
            TextWrapping = TextWrapping.WrapWholeWords,
            Text = ViewModel.GetLocalizedString(
                "ShortcutConfig.Instruction",
                "Use format like Ctrl+Shift+K. Save applies immediately.")
        };
        contentGrid.Children.Add(tipText);

        Grid.SetRow(searchBox, 1);
        contentGrid.Children.Add(searchBox);

        var scroller = new ScrollViewer
        {
            MaxHeight = 520,
            MinWidth = 760,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = rowsPanel
        };
        Grid.SetRow(scroller, 2);
        contentGrid.Children.Add(scroller);

        Grid.SetRow(validationText, 3);
        contentGrid.Children.Add(validationText);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = ViewModel.GetLocalizedString("ShortcutConfig.DialogTitle", "Configure Keyboard Shortcuts"),
            PrimaryButtonText = ViewModel.GetLocalizedString("ShortcutConfig.SaveButton", "Save"),
            SecondaryButtonText = ViewModel.GetLocalizedString("ShortcutConfig.ResetButton", "Reset Defaults"),
            CloseButtonText = ViewModel.GetLocalizedString("ShortcutConfig.CancelButton", "Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = contentGrid
        };

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                if (!TryNormalizeShortcutMappings(
                        bindingItems,
                        descriptionByActionId,
                        editValues,
                        out var normalizedMappings,
                        out var errorMessage,
                        out var conflictingActionIds))
                {
                    args.Cancel = true;
                    ResetGestureEditorHighlights();
                    HighlightConflictingGestureEditors(conflictingActionIds);
                    validationText.Text = errorMessage;
                    validationText.Visibility = Visibility.Visible;
                    return;
                }

                await ViewModel.SaveShortcutMappingsAsync(normalizedMappings).ConfigureAwait(true);
                RebuildKeyboardAccelerators();
                ViewModel.StatusText = ViewModel.GetLocalizedString(
                    "Status.ShortcutMappingsSaved",
                    "Keyboard shortcut mappings updated.");
            }
            finally
            {
                deferral.Complete();
            }
        };

        dialog.SecondaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                await ViewModel.ResetShortcutMappingsAsync().ConfigureAwait(true);
                RebuildKeyboardAccelerators();
                ViewModel.StatusText = ViewModel.GetLocalizedString(
                    "Status.ShortcutMappingsReset",
                    "Keyboard shortcut mappings reset to defaults.");
            }
            finally
            {
                deferral.Complete();
            }
        };

        _ = await dialog.ShowAsync();
    }

    private bool TryNormalizeShortcutMappings(
        IReadOnlyList<MainViewModel.ShortcutBindingItem> bindingItems,
        IReadOnlyDictionary<string, string> descriptionByActionId,
        IReadOnlyDictionary<string, string> editValues,
        out Dictionary<string, string> normalizedMappings,
        out string errorMessage,
        out string[] conflictingActionIds)
    {
        normalizedMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var signatureOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        conflictingActionIds = [];

        foreach (var item in bindingItems)
        {
            if (!editValues.TryGetValue(item.ActionId, out var rawGesture) ||
                string.IsNullOrWhiteSpace(rawGesture))
            {
                errorMessage = LocalizedFormat(
                    "ShortcutConfig.EmptyGestureError",
                    "Shortcut for '{0}' cannot be empty.",
                    item.Description);
                conflictingActionIds = [];
                return false;
            }

            if (!TryParseShortcutGesture(rawGesture, out var key, out var modifiers))
            {
                errorMessage = LocalizedFormat(
                    "ShortcutConfig.InvalidGestureError",
                    "Shortcut '{0}' for '{1}' is invalid.",
                    rawGesture.Trim(),
                    item.Description);
                conflictingActionIds = [];
                return false;
            }

            var normalizedGesture = FormatShortcutGesture(key, modifiers);
            var signature = BuildShortcutSignature(key, modifiers);
            if (signatureOwner.TryGetValue(signature, out var existingActionId))
            {
                var existingDescription = descriptionByActionId.TryGetValue(existingActionId, out var text)
                    ? text
                    : existingActionId;

                errorMessage = LocalizedFormat(
                    "ShortcutConfig.DuplicateGestureError",
                    "Shortcut '{0}' conflicts between '{1}' and '{2}'.",
                    normalizedGesture,
                    existingDescription,
                    item.Description);
                conflictingActionIds = [existingActionId, item.ActionId];
                return false;
            }

            signatureOwner[signature] = item.ActionId;
            normalizedMappings[item.ActionId] = normalizedGesture;
        }

        errorMessage = string.Empty;
        conflictingActionIds = [];
        return true;
    }
}

