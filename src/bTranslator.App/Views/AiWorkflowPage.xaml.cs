using bTranslator.App.ViewModels;

namespace bTranslator.App.Views;

public sealed partial class AiWorkflowPage : Page
{
    public AiWorkflowPage()
    {
        InitializeComponent();
    }

    public AiWorkflowPage(MainViewModel viewModel)
        : this()
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
    }

    public MainViewModel? ViewModel { get; }
}
