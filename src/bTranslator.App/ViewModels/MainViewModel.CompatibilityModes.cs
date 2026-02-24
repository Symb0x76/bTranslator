using bTranslator.Domain.Models;
using CommunityToolkit.Mvvm.Input;

namespace bTranslator.App.ViewModels;

public partial class MainViewModel
{
    private CompatibilityMode _activeCompatibilityMode;
    private PexDocument? _activePexDocument;
    private readonly Dictionary<TranslationRowViewModel, CompatibilityRowBinding> _compatibilityRowBindings = new();

    [RelayCommand]
    private async Task OpenMcmModeAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = L("Status.McmPathMissing", "MCM/XML path is empty or file does not exist.");
            AddLog(StatusText);
            return;
        }

        try
        {
            var items = await _xmlCompatibilityService.ImportAsync(path).ConfigureAwait(false);
            LoadCompatibilityRows(items, path, CompatibilityMode.McmXml);

            StatusText = Lf(
                "Status.McmModeOpened",
                "Opened MCM/XML mode from '{0}' ({1} rows).",
                path,
                _allRows.Count);
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.McmModeOpenFailed", "Open MCM/XML mode failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task ExportMcmModeAsync(string path)
    {
        if (_activeCompatibilityMode != CompatibilityMode.McmXml || _allRows.Count == 0)
        {
            StatusText = L(
                "Status.McmModeNotActive",
                "MCM/XML mode is not active. Open an MCM/XML file first.");
            AddLog(StatusText);
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = L("Status.McmPathMissing", "MCM/XML path is empty or file does not exist.");
            AddLog(StatusText);
            return;
        }

        try
        {
            EnsureParentDirectory(path);
            var items = BuildCompatibilityItemsSnapshot();
            await _xmlCompatibilityService.ExportAsync(path, items, formatVersion: 8).ConfigureAwait(false);

            StatusText = Lf(
                "Status.McmModeExported",
                "Exported MCM/XML mode to '{0}' ({1} rows).",
                path,
                items.Count);
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.McmModeExportFailed", "Export MCM/XML mode failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task OpenTxtModeAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = L("Status.TxtPathMissing", "TXT/SST path is empty or file does not exist.");
            AddLog(StatusText);
            return;
        }

        try
        {
            var items = await _sstCompatibilityService.ImportAsync(path).ConfigureAwait(false);
            LoadCompatibilityRows(items, path, CompatibilityMode.TxtSst);

            StatusText = Lf(
                "Status.TxtModeOpened",
                "Opened TXT/SST mode from '{0}' ({1} rows).",
                path,
                _allRows.Count);
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.TxtModeOpenFailed", "Open TXT/SST mode failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task ExportTxtModeAsync(string path)
    {
        if (_activeCompatibilityMode != CompatibilityMode.TxtSst || _allRows.Count == 0)
        {
            StatusText = L(
                "Status.TxtModeNotActive",
                "TXT/SST mode is not active. Open a TXT/SST file first.");
            AddLog(StatusText);
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = L("Status.TxtPathMissing", "TXT/SST path is empty or file does not exist.");
            AddLog(StatusText);
            return;
        }

        try
        {
            EnsureParentDirectory(path);
            var items = BuildCompatibilityItemsSnapshot();
            await _sstCompatibilityService.ExportAsync(path, items, version: 8).ConfigureAwait(false);

            StatusText = Lf(
                "Status.TxtModeExported",
                "Exported TXT/SST mode to '{0}' ({1} rows).",
                path,
                items.Count);
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.TxtModeExportFailed", "Export TXT/SST mode failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task RunTxtBatchScriptAsync(string path)
    {
        if (_activeCompatibilityMode != CompatibilityMode.TxtSst || _allRows.Count == 0)
        {
            StatusText = L(
                "Status.TxtModeNotActive",
                "TXT/SST mode is not active. Open a TXT/SST file first.");
            AddLog(StatusText);
            return;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = L("Status.TxtBatchScriptPathMissing", "TXT batch script path is empty or file does not exist.");
            AddLog(StatusText);
            return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var script = extension is ".yml" or ".yaml"
                ? _batchScriptEngine.ParseV2(content)
                : _batchScriptEngine.ParseLegacy(content);

            var scopeItems = _allRows
                .Select(row => new TranslationItem
                {
                    Id = row.RowKey,
                    SourceText = row.SourceText,
                    TranslatedText = row.TranslatedText,
                    IsLocked = row.IsLocked,
                    IsValidated = row.IsValidated
                })
                .ToList();

            var result = await _batchScriptEngine
                .RunAsync(
                    script,
                    new BatchExecutionScope
                    {
                        Items = scopeItems,
                        DryRun = false
                    })
                .ConfigureAwait(false);

            for (var i = 0; i < _allRows.Count; i++)
            {
                var row = _allRows[i];
                var item = scopeItems[i];
                var translated = item.TranslatedText ?? row.SourceText;
                if (string.Equals(row.TranslatedText, translated, StringComparison.Ordinal))
                {
                    continue;
                }

                row.TranslatedText = translated;
                row.IsValidated = false;
            }

            RecalculateMetrics();
            ApplyFilters();

            StatusText = Lf(
                "Status.TxtBatchScriptApplied",
                "TXT batch script '{0}' applied, changed {1} rows.",
                script.Name,
                result.ChangedItems);
            AddLog(StatusText);

            foreach (var sample in result.Logs.Take(5))
            {
                AddLog(Lf("Log.TxtBatchSample", "TXT batch: {0}", sample));
            }
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.TxtBatchScriptFailed", "TXT batch script failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task OpenPexModeAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = L("Status.PexPathMissing", "PEX path is empty or file does not exist.");
            AddLog(StatusText);
            return;
        }

        try
        {
            var document = await _pexToolchainService.LoadAsync(path).ConfigureAwait(false);
            LoadPexRows(document, path);

            StatusText = Lf(
                "Status.PexModeOpened",
                "Opened PEX mode from '{0}' ({1} strings).",
                path,
                _allRows.Count);
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.PexModeOpenFailed", "Open PEX mode failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    [RelayCommand]
    private async Task ExportPexStringsAsync(string path)
    {
        if (_activeCompatibilityMode != CompatibilityMode.Pex ||
            _activePexDocument is null ||
            _allRows.Count == 0)
        {
            StatusText = L("Status.PexNoActiveDocument", "PEX mode is not active. Open a PEX file first.");
            AddLog(StatusText);
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = L("Status.PexPathMissing", "PEX path is empty or file does not exist.");
            AddLog(StatusText);
            return;
        }

        try
        {
            EnsureParentDirectory(path);

            foreach (var pair in _compatibilityRowBindings)
            {
                if (pair.Value.PexString is null)
                {
                    continue;
                }

                pair.Value.PexString.Value = pair.Key.TranslatedText;
            }

            await _pexToolchainService.ExportStringsAsync(_activePexDocument, path).ConfigureAwait(false);

            StatusText = Lf(
                "Status.PexStringsExported",
                "Exported PEX strings to '{0}' ({1} rows).",
                path,
                _allRows.Count);
            AddLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.PexStringsExportFailed", "Export PEX strings failed: {0}", ex.Message);
            AddLog(StatusText);
        }
    }

    private void LoadCompatibilityRows(
        IReadOnlyList<TranslationItem> items,
        string sourcePath,
        CompatibilityMode mode)
    {
        foreach (var row in _allRows)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        _allRows.Clear();
        _stringEntryBindings.Clear();
        _recordItemBindings.Clear();
        _compatibilityRowBindings.Clear();

        var listLabel = mode switch
        {
            CompatibilityMode.McmXml => "MCM",
            CompatibilityMode.TxtSst => "TXT",
            _ => "COMPAT"
        };

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var editorId = string.IsNullOrWhiteSpace(item.Id)
                ? $"ITEM_{index + 1:D6}"
                : item.Id.Trim();
            var source = item.SourceText ?? string.Empty;
            var translated = item.TranslatedText ?? source;
            var row = new TranslationRowViewModel
            {
                RowKey = $"compat:{mode}:{index:D6}",
                EditorId = editorId,
                FieldSignature = listLabel,
                SourceText = source,
                TranslatedText = translated,
                ListKind = listLabel,
                LdScore = EstimateLd(source, translated),
                IsLocked = item.IsLocked,
                IsValidated = item.IsValidated
            };
            row.ApplyQualityLabels(_rowQualityLabels);
            row.PropertyChanged += OnRowChanged;
            _allRows.Add(row);
            _compatibilityRowBindings[row] = new CompatibilityRowBinding
            {
                ExternalId = editorId
            };
        }

        _currentDocument = null;
        _activePexDocument = null;
        _activeCompatibilityMode = mode;
        ActivePluginName = Path.GetFileName(sourcePath);
        WorkspaceTitle = Lf(
            "WorkspaceTitle.CompatibilityModeFormat",
            "{0} Mode",
            ResolveCompatibilityModeName(mode));
        ResetEspCompareState();
        RecalculateMetrics();
        ApplyFilters();
    }

    private void LoadPexRows(PexDocument document, string sourcePath)
    {
        foreach (var row in _allRows)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        _allRows.Clear();
        _stringEntryBindings.Clear();
        _recordItemBindings.Clear();
        _compatibilityRowBindings.Clear();

        foreach (var entry in document.Strings.OrderBy(static item => item.Index))
        {
            var row = new TranslationRowViewModel
            {
                RowKey = $"compat:pex:{entry.Index:D6}",
                EditorId = entry.Index.ToString("D6"),
                FieldSignature = "STRING",
                SourceText = entry.Value,
                TranslatedText = entry.Value,
                ListKind = "PEX",
                LdScore = 0,
                IsLocked = false,
                IsValidated = false
            };
            row.ApplyQualityLabels(_rowQualityLabels);
            row.PropertyChanged += OnRowChanged;
            _allRows.Add(row);
            _compatibilityRowBindings[row] = new CompatibilityRowBinding
            {
                ExternalId = entry.Index.ToString(),
                PexString = entry
            };
        }

        _currentDocument = null;
        _activePexDocument = document;
        _activeCompatibilityMode = CompatibilityMode.Pex;
        ActivePluginName = Path.GetFileName(sourcePath);
        WorkspaceTitle = Lf(
            "WorkspaceTitle.CompatibilityModeFormat",
            "{0} Mode",
            ResolveCompatibilityModeName(CompatibilityMode.Pex));
        ResetEspCompareState();
        RecalculateMetrics();
        ApplyFilters();
    }

    private List<TranslationItem> BuildCompatibilityItemsSnapshot()
    {
        var result = new List<TranslationItem>(_allRows.Count);
        foreach (var row in _allRows)
        {
            var id = _compatibilityRowBindings.TryGetValue(row, out var binding)
                ? binding.ExternalId
                : row.EditorId;
            if (string.IsNullOrWhiteSpace(id))
            {
                id = row.RowKey;
            }

            result.Add(new TranslationItem
            {
                Id = id,
                SourceText = row.SourceText,
                TranslatedText = row.TranslatedText,
                IsLocked = row.IsLocked,
                IsValidated = row.IsValidated
            });
        }

        return result;
    }

    private void ResetCompatibilityModeState()
    {
        _activeCompatibilityMode = CompatibilityMode.None;
        _activePexDocument = null;
        _compatibilityRowBindings.Clear();
    }

    private void ResetEspCompareState()
    {
        _espCompareCandidates.Clear();
        EspComparePendingReplacementCount = 0;
        EspCompareSummary = string.Empty;
        ApplyEspCompareReplacementsCommand.NotifyCanExecuteChanged();
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private string ResolveCompatibilityModeName(CompatibilityMode mode)
    {
        return mode switch
        {
            CompatibilityMode.McmXml => L("CompatibilityMode.McmXml", "MCM/XML"),
            CompatibilityMode.TxtSst => L("CompatibilityMode.TxtSst", "TXT/SST"),
            CompatibilityMode.Pex => L("CompatibilityMode.Pex", "PEX"),
            _ => L("CompatibilityMode.None", "Workspace")
        };
    }

    private enum CompatibilityMode
    {
        None,
        McmXml,
        TxtSst,
        Pex
    }

    private sealed class CompatibilityRowBinding
    {
        public required string ExternalId { get; init; }
        public PexStringEntry? PexString { get; init; }
    }
}
