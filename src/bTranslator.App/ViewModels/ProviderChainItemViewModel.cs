using CommunityToolkit.Mvvm.ComponentModel;

namespace bTranslator.App.ViewModels;

public partial class ProviderChainItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string providerId = string.Empty;

    [ObservableProperty]
    private bool isEnabled = true;
}

