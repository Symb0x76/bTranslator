using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace bTranslator.App.ViewModels;

public partial class TranslationRowViewModel : ObservableObject
{
    private QualityLabelSet _qualityLabels = QualityLabelSet.Default;

    public string RowKey { get; init; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    public partial string EditorId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FieldSignature { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SourceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TranslatedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ListKind { get; set; } = "STRINGS";

    [ObservableProperty]
    public partial double LdScore { get; set; }

    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    [ObservableProperty]
    public partial bool IsValidated { get; set; }

    public bool IsUntranslated =>
        string.IsNullOrWhiteSpace(TranslatedText) ||
        string.Equals(SourceText, TranslatedText, StringComparison.Ordinal);

    public QualityStateKey QualityState => ResolveQualityState();

    public string QualityLabel
    {
        get
        {
            return ResolveQualityState() switch
            {
                QualityStateKey.Locked => _qualityLabels.Locked,
                QualityStateKey.Validated => _qualityLabels.Validated,
                QualityStateKey.Pending => _qualityLabels.Pending,
                _ => _qualityLabels.Draft
            };
        }
    }

    public string LdLabel => $"{LdScore:0.0}";

    public SolidColorBrush StatusBrush => new(GetStatusColor());

    public void ApplyQualityLabels(QualityLabelSet labels)
    {
        _qualityLabels = labels;
        OnPropertyChanged(nameof(QualityLabel));
    }

    partial void OnTranslatedTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsUntranslated));
        OnPropertyChanged(nameof(QualityState));
        OnPropertyChanged(nameof(QualityLabel));
        OnPropertyChanged(nameof(StatusBrush));
    }

    partial void OnIsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(QualityState));
        OnPropertyChanged(nameof(QualityLabel));
        OnPropertyChanged(nameof(StatusBrush));
    }

    partial void OnIsValidatedChanged(bool value)
    {
        OnPropertyChanged(nameof(QualityState));
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

    private QualityStateKey ResolveQualityState()
    {
        if (IsLocked)
        {
            return QualityStateKey.Locked;
        }

        if (IsValidated)
        {
            return QualityStateKey.Validated;
        }

        return IsUntranslated ? QualityStateKey.Pending : QualityStateKey.Draft;
    }

    public enum QualityStateKey
    {
        Locked,
        Validated,
        Pending,
        Draft
    }

    public readonly record struct QualityLabelSet(
        string Locked,
        string Validated,
        string Pending,
        string Draft)
    {
        public static QualityLabelSet Default { get; } = new(
            Locked: "Locked",
            Validated: "Validated",
            Pending: "Pending",
            Draft: "Draft");
    }
}

