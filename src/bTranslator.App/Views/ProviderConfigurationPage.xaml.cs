using bTranslator.App.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace bTranslator.App.Views;

public sealed partial class ProviderConfigurationPage : Page
{
    public ProviderConfigurationPage()
    {
        InitializeComponent();
    }

    public ProviderConfigurationPage(MainViewModel viewModel)
        : this()
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
    }

    public MainViewModel? ViewModel { get; }

    private static bool TryInitializePicker(FileOpenPicker picker)
    {
        if (Microsoft.UI.Xaml.Application.Current is not App app || app.MainWindow is null)
        {
            return false;
        }

        var hwnd = WindowNative.GetWindowHandle(app.MainWindow);
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        InitializeWithWindow.Initialize(picker, hwnd);
        return true;
    }

    private async void OnImportLegacyApiTranslatorConfigClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".ini");
        picker.FileTypeFilter.Add("*");

        if (!TryInitializePicker(picker))
        {
            ViewModel.StatusText = ViewModel.GetLocalizedString(
                "Status.FilePickerUnavailable",
                "Cannot open file picker: main window handle unavailable.");
            return;
        }

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        if (ViewModel.ImportLegacyApiTranslatorConfigCommand.CanExecute(file.Path))
        {
            await ViewModel.ImportLegacyApiTranslatorConfigCommand.ExecuteAsync(file.Path).ConfigureAwait(true);
        }
    }
}
