using CommunityToolkit.Mvvm.ComponentModel;

namespace bTranslator.App.ViewModels;

public partial class ProviderChainItemViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string ProviderId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModelName { get; set; } = string.Empty;

    public string DisplayText => string.IsNullOrWhiteSpace(ModelName)
        ? ProviderId
        : $"{ProviderId} / {ModelName}";

    partial void OnProviderIdChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnModelNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }
}

