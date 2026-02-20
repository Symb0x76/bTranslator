using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace bTranslator.App.ViewModels;

public partial class TranslationRowViewModel : ObservableObject
{
    public string RowKey { get; init; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string editorId = string.Empty;

    [ObservableProperty]
    private string fieldSignature = string.Empty;

    [ObservableProperty]
    private string sourceText = string.Empty;

    [ObservableProperty]
    private string translatedText = string.Empty;

    [ObservableProperty]
    private string listKind = "STRINGS";

    [ObservableProperty]
    private double ldScore;

    [ObservableProperty]
    private bool isLocked;

    [ObservableProperty]
    private bool isValidated;

    public bool IsUntranslated =>
        string.IsNullOrWhiteSpace(TranslatedText) ||
        string.Equals(SourceText, TranslatedText, StringComparison.Ordinal);

    public string QualityLabel
    {
        get
        {
            if (IsLocked)
            {
                return "Locked";
            }

            if (IsValidated)
            {
                return "Validated";
            }

            return IsUntranslated ? "Pending" : "Draft";
        }
    }

    public string LdLabel => $"{LdScore:0.0}";

    public SolidColorBrush StatusBrush => new(GetStatusColor());

    partial void OnTranslatedTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsUntranslated));
        OnPropertyChanged(nameof(QualityLabel));
        OnPropertyChanged(nameof(StatusBrush));
    }

    partial void OnIsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(QualityLabel));
        OnPropertyChanged(nameof(StatusBrush));
    }

    partial void OnIsValidatedChanged(bool value)
    {
        OnPropertyChanged(nameof(QualityLabel));
        OnPropertyChanged(nameof(StatusBrush));
    }

    partial void OnLdScoreChanged(double value)
    {
        OnPropertyChanged(nameof(LdLabel));
    }

    private Color GetStatusColor()
    {
        if (IsLocked)
        {
            return ColorHelper.FromArgb(0xFF, 0x94, 0xA3, 0xB8);
        }

        if (IsValidated)
        {
            return ColorHelper.FromArgb(0xFF, 0x34, 0xD3, 0x99);
        }

        if (IsUntranslated)
        {
            return ColorHelper.FromArgb(0xFF, 0xFB, 0xBF, 0x24);
        }

        return ColorHelper.FromArgb(0xFF, 0x38, 0xBD, 0xF8);
    }
}

