using System.Globalization;
using System.Text;
using bTranslator.Domain.Models;
using bTranslator.Domain.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace bTranslator.App.ViewModels;

public partial class MainViewModel
{
    private const string WorkspaceDictionaryStateKey = "workspace.translation_dictionary";
    private const string WorkspaceDictionaryEnabledKey = "workspace.dictionary_pre_replace_enabled";

    private readonly List<TranslationDictionaryEntry> _dictionaryEntries = [];
    private bool _isLoadingDictionaryState;

    [ObservableProperty]
    public partial bool IsDictionaryPreReplaceEnabled { get; set; } = true;

    [ObservableProperty]
    public partial int DictionaryEntryCount { get; set; }

    partial void OnIsDictionaryPreReplaceEnabledChanged(bool value)
    {
        if (_isLoadingDictionaryState)
        {
            return;
        }

        _ = PersistDictionaryEnabledAsync(value);
    }

    public IReadOnlyList<TranslationDictionaryEntry> GetDictionaryEntriesSnapshot()
    {
        return _dictionaryEntries
            .Select(static entry => new TranslationDictionaryEntry
            {
                Source = entry.Source,
                Target = entry.Target,
                EditorIdPattern = entry.EditorIdPattern,
                FieldPattern = entry.FieldPattern,
                MatchCase = entry.MatchCase,
                WholeWord = entry.WholeWord
            })
            .ToList();
    }

    public async Task ReplaceDictionaryEntriesAsync(IEnumerable<TranslationDictionaryEntry> entries)
    {
        await ReplaceDictionaryEntriesCoreAsync(entries).ConfigureAwait(false);

        StatusText = Lf(
            "Status.DictionaryUpdated",
            "Dictionary updated ({0} entries).",
            DictionaryEntryCount);
        AddLog(StatusText);
    }

    [RelayCommand(CanExecute = nameof(CanAddScopedDictionaryEntryFromSelectedRow))]
    private async Task AddScopedDictionaryEntryFromSelectedRowAsync()
    {
        if (SelectedRow is null)
        {
            StatusText = L("Status.DictionarySeedRowMissing", "No selected row for dictionary entry.");
            AddLog(StatusText);
            return;
        }

        var source = SelectedRow.SourceText?.Trim() ?? string.Empty;
        var target = SelectedRow.TranslatedText?.Trim() ?? string.Empty;
        if (source.Length == 0 || target.Length == 0)
        {
            StatusText = L(
                "Status.DictionarySeedMissingSourceOrTarget",
                "Selected row source/translation is empty, cannot create dictionary entry.");
            AddLog(StatusText);
            return;
        }

        var entry = new TranslationDictionaryEntry
        {
            Source = source,
            Target = target,
            EditorIdPattern = string.IsNullOrWhiteSpace(SelectedRow.EditorId) ? null : SelectedRow.EditorId.Trim(),
            FieldPattern = string.IsNullOrWhiteSpace(SelectedRow.FieldSignature) ? null : SelectedRow.FieldSignature.Trim(),
            MatchCase = false,
            WholeWord = false
        };

        var before = _dictionaryEntries.Count;
        var merged = TranslationDictionaryEngine.NormalizeEntries(_dictionaryEntries.Concat([entry]));
        _dictionaryEntries.Clear();
        _dictionaryEntries.AddRange(merged);
        DictionaryEntryCount = _dictionaryEntries.Count;

        await SaveDictionaryStateAsync().ConfigureAwait(false);

        if (DictionaryEntryCount > before)
        {
            StatusText = Lf(
                "Status.DictionarySeedAdded",
                "Added scoped dictionary entry from selected row: {0} / {1}.",
                entry.EditorIdPattern ?? "*",
                entry.FieldPattern ?? "*");
        }
        else
        {
            StatusText = L(
                "Status.DictionarySeedDuplicate",
                "Scoped dictionary entry already exists.");
        }

        AddLog(StatusText);
    }

    [RelayCommand]
    private async Task ImportDictionaryAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = L("Status.DictionaryPathMissing", "Dictionary path is empty or file does not exist.");
            AddLog(StatusText);
            return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(path, Encoding.UTF8).ConfigureAwait(false);
            var imported = TranslationDictionaryEngine.DeserializeEntries(content);

            await ReplaceDictionaryEntriesCoreAsync(imported).ConfigureAwait(false);

            StatusText = Lf(
                "Status.DictionaryImported",
                "Imported dictionary from '{0}' ({1} entries).",
                path,
                DictionaryEntryCount);
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.DictionaryImportFailed", "Import dictionary failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task ExportDictionaryAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = L("Status.DictionaryPathMissing", "Dictionary path is empty or file does not exist.");
            AddLog(StatusText);
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var content = TranslationDictionaryEngine.SerializeEntries(_dictionaryEntries);
            await File.WriteAllTextAsync(path, content, Encoding.UTF8).ConfigureAwait(false);

            StatusText = Lf(
                "Status.DictionaryExported",
                "Exported dictionary to '{0}' ({1} entries).",
                path,
                _dictionaryEntries.Count);
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.DictionaryExportFailed", "Export dictionary failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    private async Task LoadDictionaryStateAsync()
    {
        _isLoadingDictionaryState = true;
        try
        {
            var enabledRaw = await _settingsStore.GetAsync(WorkspaceDictionaryEnabledKey).ConfigureAwait(false);
            if (bool.TryParse(enabledRaw, out var enabled))
            {
                IsDictionaryPreReplaceEnabled = enabled;
            }

            _dictionaryEntries.Clear();
            var content = await _settingsStore.GetAsync(WorkspaceDictionaryStateKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(content))
            {
                var restored = TranslationDictionaryEngine.DeserializeEntries(content);
                _dictionaryEntries.AddRange(restored);
            }

            DictionaryEntryCount = _dictionaryEntries.Count;
        }
        catch
        {
            _dictionaryEntries.Clear();
            DictionaryEntryCount = 0;
        }
        finally
        {
            _isLoadingDictionaryState = false;
        }
    }

    private async Task SaveDictionaryStateAsync()
    {
        var content = TranslationDictionaryEngine.SerializeEntries(_dictionaryEntries);
        await _settingsStore.SetAsync(WorkspaceDictionaryStateKey, content).ConfigureAwait(false);
        await _settingsStore.SetAsync(
            WorkspaceDictionaryEnabledKey,
            IsDictionaryPreReplaceEnabled.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
    }

    private async Task ReplaceDictionaryEntriesCoreAsync(IEnumerable<TranslationDictionaryEntry> entries)
    {
        _dictionaryEntries.Clear();
        _dictionaryEntries.AddRange(TranslationDictionaryEngine.NormalizeEntries(entries));
        DictionaryEntryCount = _dictionaryEntries.Count;

        await SaveDictionaryStateAsync().ConfigureAwait(false);
    }

    private async Task PersistDictionaryEnabledAsync(bool value)
    {
        try
        {
            await _settingsStore.SetAsync(
                WorkspaceDictionaryEnabledKey,
                value.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        }
        catch
        {
            // Ignore settings persistence failures from a toggle-only interaction.
        }
    }

    private TranslationDictionaryPrepareResult PrepareSourceWithDictionary(
        string sourceText,
        string editorId,
        string fieldSignature)
    {
        return TranslationDictionaryEngine.PrepareSource(
            sourceText,
            editorId,
            fieldSignature,
            _dictionaryEntries,
            IsDictionaryPreReplaceEnabled);
    }

    private static string RestoreDictionaryTokens(
        string text,
        IReadOnlyList<TranslationDictionaryTokenReplacement> replacements)
    {
        return TranslationDictionaryEngine.RestoreTokens(text, replacements);
    }

    private bool CanAddScopedDictionaryEntryFromSelectedRow()
    {
        return SelectedRow is not null;
    }
}
