using bTranslator.App.ViewModels;

namespace bTranslator.App.Views;

public sealed partial class AiProviderSetupPage : Page
{
    public AiProviderSetupPage()
    {
        InitializeComponent();
    }

    public AiProviderSetupPage(MainViewModel viewModel)
        : this()
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
    }

    public MainViewModel? ViewModel { get; }
}
