using bTranslator.Domain.Models;
using bTranslator.Domain.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace bTranslator.App.ViewModels;

public partial class DictionaryEditorEntryViewModel : ObservableObject
{
    private string _scopeGlobalLabel = "Global";

    [ObservableProperty]
    public partial string Source { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Target { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditorIdPattern { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FieldPattern { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool MatchCase { get; set; }

    [ObservableProperty]
    public partial bool WholeWord { get; set; }

    public string ScopeDisplay
    {
        get
        {
            return TranslationDictionaryEngine.BuildScopeDisplay(
                ToEntry(),
                _scopeGlobalLabel);
        }
    }

    public string FlagsDisplay
    {
        get
        {
            return $"{(MatchCase ? "Case" : "IgnoreCase")} | {(WholeWord ? "WholeWord" : "Substring")}";
        }
    }

    partial void OnSourceChanged(string value) => NotifyDisplayChanged();
    partial void OnTargetChanged(string value) => NotifyDisplayChanged();
    partial void OnEditorIdPatternChanged(string value) => NotifyDisplayChanged();
    partial void OnFieldPatternChanged(string value) => NotifyDisplayChanged();
    partial void OnMatchCaseChanged(bool value) => NotifyDisplayChanged();
    partial void OnWholeWordChanged(bool value) => NotifyDisplayChanged();

    public TranslationDictionaryEntry ToEntry()
    {
        return new TranslationDictionaryEntry
        {
            Source = Source,
            Target = Target,
            EditorIdPattern = string.IsNullOrWhiteSpace(EditorIdPattern) ? null : EditorIdPattern,
            FieldPattern = string.IsNullOrWhiteSpace(FieldPattern) ? null : FieldPattern,
            MatchCase = MatchCase,
            WholeWord = WholeWord
        };
    }

    public static DictionaryEditorEntryViewModel FromEntry(TranslationDictionaryEntry entry)
    {
        return new DictionaryEditorEntryViewModel
        {
            Source = entry.Source,
            Target = entry.Target,
            EditorIdPattern = entry.EditorIdPattern ?? string.Empty,
            FieldPattern = entry.FieldPattern ?? string.Empty,
            MatchCase = entry.MatchCase,
            WholeWord = entry.WholeWord
        };
    }

    public void SetScopeGlobalLabel(string label)
    {
        var normalized = string.IsNullOrWhiteSpace(label) ? "Global" : label.Trim();
        if (string.Equals(_scopeGlobalLabel, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _scopeGlobalLabel = normalized;
        NotifyDisplayChanged();
    }

    private void NotifyDisplayChanged()
    {
        OnPropertyChanged(nameof(ScopeDisplay));
        OnPropertyChanged(nameof(FlagsDisplay));
    }
}
