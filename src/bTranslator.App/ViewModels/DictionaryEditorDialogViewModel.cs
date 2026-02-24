using bTranslator.App.Localization;
using bTranslator.Domain.Models;
using bTranslator.Domain.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace bTranslator.App.ViewModels;

public partial class DictionaryEditorDialogViewModel : ObservableObject
{
    private const string SortBySourceAsc = "source_asc";
    private const string SortByTargetAsc = "target_asc";
    private const string SortByScope = "scope";

    private readonly List<DictionaryEditorEntryViewModel> _entries = [];

    public DictionaryEditorDialogViewModel(
        LocalizedUiText ui,
        IEnumerable<TranslationDictionaryEntry> entries)
    {
        Ui = ui;
        SortOptions =
        [
            new OptionItem(SortBySourceAsc, Ui.DictionaryEditorSortBySourceText),
            new OptionItem(SortByTargetAsc, Ui.DictionaryEditorSortByTargetText),
            new OptionItem(SortByScope, Ui.DictionaryEditorSortByScopeText)
        ];

        foreach (var entry in entries)
        {
            var item = DictionaryEditorEntryViewModel.FromEntry(entry);
            item.SetScopeGlobalLabel(Ui.DictionaryEditorScopeGlobalText);
            AttachEntry(item);
            _entries.Add(item);
        }

        EntryCount = _entries.Count;
        SelectedSortOption = SortOptions[0];
        RefreshFilteredEntries();
    }

    public LocalizedUiText Ui { get; }

    public IReadOnlyList<OptionItem> SortOptions { get; }

    public ObservableCollection<DictionaryEditorEntryViewModel> FilteredEntries { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial OptionItem? SelectedSortOption { get; set; }

    [ObservableProperty]
    public partial DictionaryEditorEntryViewModel? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial int EntryCount { get; set; }

    [ObservableProperty]
    public partial string ScopePreviewText { get; set; } = string.Empty;

    public string EntryCountText => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        Ui.DictionaryEditorEntryCountText,
        EntryCount);

    partial void OnSearchTextChanged(string value) => RefreshFilteredEntries();
    partial void OnSelectedSortOptionChanged(OptionItem? value) => RefreshFilteredEntries();

    partial void OnSelectedEntryChanged(DictionaryEditorEntryViewModel? value)
    {
        ScopePreviewText = value is null
            ? string.Empty
            : BuildScopeDisplay(value);
    }

    partial void OnEntryCountChanged(int value) => OnPropertyChanged(nameof(EntryCountText));

    public void AddEntry()
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            SearchText = string.Empty;
        }

        var item = new DictionaryEditorEntryViewModel();
        item.SetScopeGlobalLabel(Ui.DictionaryEditorScopeGlobalText);
        AttachEntry(item);
        _entries.Add(item);
        EntryCount = _entries.Count;
        RefreshFilteredEntries();
        SelectedEntry = item;
    }

    public void DuplicateSelectedEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            SearchText = string.Empty;
        }

        var item = DictionaryEditorEntryViewModel.FromEntry(SelectedEntry.ToEntry());
        item.SetScopeGlobalLabel(Ui.DictionaryEditorScopeGlobalText);
        AttachEntry(item);
        _entries.Add(item);
        EntryCount = _entries.Count;
        RefreshFilteredEntries();
        SelectedEntry = item;
    }

    public void DeleteSelectedEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        if (!_entries.Remove(SelectedEntry))
        {
            return;
        }

        EntryCount = _entries.Count;
        RefreshFilteredEntries();
        SelectedEntry = FilteredEntries.FirstOrDefault();
    }

    public IReadOnlyList<TranslationDictionaryEntry> ExportEntries()
    {
        return _entries
            .Select(static item => item.ToEntry())
            .ToList();
    }

    public (int ParsedCount, int AddedCount) AddEntriesFromDelimitedText(string content)
    {
        var parsed = TranslationDictionaryEngine.ParseDelimitedEntries(content);
        if (parsed.Count == 0)
        {
            return (0, 0);
        }

        var before = _entries.Count;
        var merged = _entries
            .Select(static item => item.ToEntry())
            .Concat(parsed);
        ReplaceAllEntries(merged);

        return (parsed.Count, _entries.Count - before);
    }

    private void RefreshFilteredEntries()
    {
        IEnumerable<DictionaryEditorEntryViewModel> query = _entries;

        var keyword = SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item =>
                item.Source.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.Target.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.EditorIdPattern.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.FieldPattern.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                BuildScopeDisplay(item).Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        query = (SelectedSortOption?.Value ?? SortBySourceAsc) switch
        {
            SortByTargetAsc => query
                .OrderBy(static item => item.Target, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Source, StringComparer.OrdinalIgnoreCase),
            SortByScope => query
                .OrderBy(item => BuildScopeDisplay(item), StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Source, StringComparer.OrdinalIgnoreCase),
            _ => query
                .OrderBy(static item => item.Source, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Target, StringComparer.OrdinalIgnoreCase)
        };

        var currentSelection = SelectedEntry;

        FilteredEntries.Clear();
        foreach (var item in query)
        {
            FilteredEntries.Add(item);
        }

        if (currentSelection is not null && FilteredEntries.Contains(currentSelection))
        {
            SelectedEntry = currentSelection;
        }
        else
        {
            SelectedEntry = FilteredEntries.FirstOrDefault();
        }
    }

    private void AttachEntry(DictionaryEditorEntryViewModel item)
    {
        item.PropertyChanged += (_, _) => RefreshFilteredEntries();
    }

    private void ReplaceAllEntries(IEnumerable<TranslationDictionaryEntry> entries)
    {
        var normalized = TranslationDictionaryEngine.NormalizeEntries(entries);

        _entries.Clear();
        foreach (var entry in normalized)
        {
            var item = DictionaryEditorEntryViewModel.FromEntry(entry);
            item.SetScopeGlobalLabel(Ui.DictionaryEditorScopeGlobalText);
            AttachEntry(item);
            _entries.Add(item);
        }

        EntryCount = _entries.Count;
        RefreshFilteredEntries();
    }

    private string BuildScopeDisplay(DictionaryEditorEntryViewModel item)
    {
        return item.ScopeDisplay;
    }
}
