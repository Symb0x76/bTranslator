using bTranslator.Domain.Enums;
using bTranslator.Domain.Models;
using bTranslator.Domain.Models.Compare;
using bTranslator.Domain.Services.Compare;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace bTranslator.App.ViewModels;

public partial class MainViewModel
{
    private Dictionary<string, EspCompareCandidate> _espCompareCandidates = new(StringComparer.Ordinal);

    [ObservableProperty]
    public partial int EspComparePendingReplacementCount { get; set; }

    [ObservableProperty]
    public partial string EspCompareSummary { get; set; } = string.Empty;

    [RelayCommand]
    private async Task CompareEspAsync(string path)
    {
        if (_currentDocument is null)
        {
            StatusText = L(
                "Status.EspCompareNoActiveWorkspace",
                "No active workspace. Open a plugin before ESP compare.");
            AddLog(StatusText);
            return;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = L(
                "Status.EspComparePathMissing",
                "Compare ESP path is empty or file does not exist.");
            AddLog(StatusText);
            return;
        }

        try
        {
            var game = ParseGameKind(SelectedGame);
            var options = new PluginOpenOptions
            {
                Language = ToLanguageToken(SourceLanguage),
                StringsDirectory = NormalizeOptionalPath(StringsDirectoryPath),
                RecordDefinitionsPath = NormalizeOptionalPath(RecordDefinitionsPath),
                LoadStrings = LoadStrings,
                LoadRecordFields = LoadRecordFields,
                Encoding = _activeEncoding
            };

            var compareDocument = await _pluginDocumentService
                .OpenAsync(game, path, options)
                .ConfigureAwait(false);

            var currentRows = BuildCurrentEspCompareRows();
            var incomingRows = BuildEspCompareRowsFromDocument(compareDocument);
            var result = EspCompareEngine.Compare(currentRows, incomingRows);

            _espCompareCandidates = result.Candidates
                .ToDictionary(static item => item.StableKey, StringComparer.Ordinal);
            EspComparePendingReplacementCount = _espCompareCandidates.Count;
            ApplyEspCompareReplacementsCommand.NotifyCanExecuteChanged();

            EspCompareSummary = Lf(
                "Status.EspCompareCompleted",
                "ESP compare done. Matched {0}, source diff {1}, translation diff {2}, replaceable {3}, missing in compare {4}, missing in current {5}.",
                result.MatchedRowCount,
                result.SourceDifferentCount,
                result.TranslationDifferentCount,
                result.Candidates.Count,
                result.MissingInCompareCount,
                result.MissingInBaseCount);
            StatusText = EspCompareSummary;
            AddLog(StatusText);

            foreach (var sample in result.Candidates.Take(5))
            {
                AddLog(Lf(
                    "Log.EspCompareCandidateSample",
                    "ESP candidate: {0}/{1} | '{2}' -> '{3}'",
                    sample.EditorId,
                    sample.FieldSignature,
                    sample.CurrentTranslatedText,
                    sample.IncomingTranslatedText));
            }
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.EspCompareFailed", "ESP compare failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyEspCompareReplacements))]
    private void ApplyEspCompareReplacements()
    {
        if (_espCompareCandidates.Count == 0)
        {
            StatusText = L(
                "Status.EspCompareNoPendingReplacements",
                "No pending replacements from ESP compare.");
            AddLog(StatusText);
            return;
        }

        var updated = 0;
        var skippedLocked = 0;
        var skippedSourceMismatch = 0;

        foreach (var row in _allRows)
        {
            var stableKey = BuildStableCompareKeyFromRow(row);
            if (!_espCompareCandidates.TryGetValue(stableKey, out var candidate))
            {
                continue;
            }

            if (row.IsLocked)
            {
                skippedLocked++;
                continue;
            }

            if (!string.Equals(row.SourceText, candidate.SourceText, StringComparison.Ordinal))
            {
                skippedSourceMismatch++;
                continue;
            }

            if (string.Equals(row.TranslatedText, candidate.IncomingTranslatedText, StringComparison.Ordinal))
            {
                continue;
            }

            row.TranslatedText = candidate.IncomingTranslatedText;
            row.IsValidated = false;
            updated++;
        }

        RecalculateMetrics();
        ApplyFilters();

        StatusText = Lf(
            "Status.EspCompareApplied",
            "Applied {0} replacements ({1} locked skipped, {2} source mismatch skipped).",
            updated,
            skippedLocked,
            skippedSourceMismatch);
        AddLog(StatusText);
    }

    private bool CanApplyEspCompareReplacements()
    {
        return EspComparePendingReplacementCount > 0;
    }

    private List<EspCompareRow> BuildCurrentEspCompareRows()
    {
        var result = new List<EspCompareRow>(_allRows.Count);
        foreach (var row in _allRows)
        {
            result.Add(new EspCompareRow
            {
                StableKey = BuildStableCompareKeyFromRow(row),
                EditorId = row.EditorId,
                FieldSignature = row.FieldSignature,
                ListKind = row.ListKind,
                SourceText = row.SourceText,
                TranslatedText = row.TranslatedText
            });
        }

        return result;
    }

    private static List<EspCompareRow> BuildEspCompareRowsFromDocument(PluginDocument document)
    {
        var result = new List<EspCompareRow>();

        foreach (var pair in document.StringTables.OrderBy(static item => item.Key))
        {
            var listKind = ToListLabel(pair.Key);
            foreach (var entry in pair.Value.OrderBy(static item => item.Id))
            {
                result.Add(new EspCompareRow
                {
                    StableKey = BuildStableCompareKeyFromStrings(pair.Key, entry.Id),
                    EditorId = $"{entry.Id:X8}",
                    FieldSignature = listKind,
                    ListKind = listKind,
                    SourceText = entry.Text,
                    TranslatedText = entry.Text
                });
            }
        }

        foreach (var item in document.RecordItems)
        {
            var metadata = item.PluginFieldMetadata;
            result.Add(new EspCompareRow
            {
                StableKey = BuildStableCompareKeyFromRecord(item.Id),
                EditorId = metadata is null
                    ? item.Id
                    : $"{metadata.RecordSignature}:{metadata.FormId:X8}",
                FieldSignature = metadata?.FieldSignature ?? item.Id,
                ListKind = ToListLabel(metadata?.ListIndex),
                SourceText = item.SourceText,
                TranslatedText = item.TranslatedText ?? item.SourceText
            });
        }

        return result;
    }

    private static string BuildStableCompareKeyFromStrings(StringsFileKind kind, uint id)
    {
        return $"str:{kind}:{id:X8}";
    }

    private static string BuildStableCompareKeyFromRecord(string itemId)
    {
        return $"recid:{itemId}";
    }

    private static string BuildStableCompareKeyFromRow(TranslationRowViewModel row)
    {
        if (row.RowKey.StartsWith("str:", StringComparison.Ordinal))
        {
            return row.RowKey;
        }

        if (row.RowKey.StartsWith("rec:", StringComparison.Ordinal))
        {
            var parts = row.RowKey.Split(':', 3, StringSplitOptions.None);
            if (parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[2]))
            {
                return BuildStableCompareKeyFromRecord(parts[2]);
            }
        }

        return string.Join("\u001f", row.ListKind, row.EditorId, row.FieldSignature);
    }
}
