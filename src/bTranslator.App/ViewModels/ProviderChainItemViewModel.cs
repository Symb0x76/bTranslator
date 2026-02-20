using CommunityToolkit.Mvvm.ComponentModel;

namespace bTranslator.App.ViewModels;

public partial class ProviderChainItemViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string ProviderId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;
}

