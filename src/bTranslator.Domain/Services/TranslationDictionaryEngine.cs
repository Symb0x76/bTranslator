using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using bTranslator.Domain.Models;

namespace bTranslator.Domain.Services;

public static class TranslationDictionaryEngine
{
    public static TranslationDictionaryPrepareResult PrepareSource(
        string sourceText,
        string editorId,
        string fieldSignature,
        IReadOnlyList<TranslationDictionaryEntry> dictionaryEntries,
        bool enabled = true)
    {
        if (!enabled || dictionaryEntries.Count == 0 || string.IsNullOrEmpty(sourceText))
        {
            return new TranslationDictionaryPrepareResult(sourceText, []);
        }

        var scopedEntries = dictionaryEntries
            .Where(entry => IsScopeMatch(entry, editorId, fieldSignature))
            .OrderByDescending(static entry => entry.Source.Length)
            .ToList();
        if (scopedEntries.Count == 0)
        {
            return new TranslationDictionaryPrepareResult(sourceText, []);
        }

        var prepared = sourceText;
        var replacements = new List<TranslationDictionaryTokenReplacement>();
        foreach (var entry in scopedEntries)
        {
            var pattern = entry.WholeWord
                ? $@"\b{Regex.Escape(entry.Source)}\b"
                : Regex.Escape(entry.Source);
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            var options = RegexOptions.CultureInvariant;
            if (!entry.MatchCase)
            {
                options |= RegexOptions.IgnoreCase;
            }

            prepared = Regex.Replace(
                prepared,
                pattern,
                _ =>
                {
                    var token = $"<BT_DICT_{Guid.NewGuid():N}>";
                    replacements.Add(new TranslationDictionaryTokenReplacement(token, entry.Target));
                    return token;
                },
                options);
        }

        return replacements.Count == 0
            ? new TranslationDictionaryPrepareResult(sourceText, [])
            : new TranslationDictionaryPrepareResult(prepared, replacements);
    }

    public static string RestoreTokens(
        string text,
        IReadOnlyList<TranslationDictionaryTokenReplacement> replacements)
    {
        var restored = text;
        foreach (var replacement in replacements)
        {
            restored = restored.Replace(replacement.Token, replacement.Target, StringComparison.Ordinal);
        }

        return restored;
    }

    public static bool IsScopeMatch(
        TranslationDictionaryEntry entry,
        string editorId,
        string fieldSignature)
    {
        return IsScopePatternMatch(entry.EditorIdPattern, editorId) &&
               IsScopePatternMatch(entry.FieldPattern, fieldSignature);
    }

    public static List<TranslationDictionaryEntry> DeserializeEntries(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        using var document = JsonDocument.Parse(content);
        List<TranslationDictionaryEntry>? entries = null;
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            entries = JsonSerializer.Deserialize<List<TranslationDictionaryEntry>>(content, options);
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            entries = JsonSerializer.Deserialize<TranslationDictionaryDocument>(content, options)?.Entries;
        }

        return NormalizeEntries(entries);
    }

    public static string SerializeEntries(IEnumerable<TranslationDictionaryEntry> entries)
    {
        var document = new TranslationDictionaryDocument
        {
            Version = 1,
            Entries = NormalizeEntries(entries)
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        return JsonSerializer.Serialize(document, options);
    }

    public static List<TranslationDictionaryEntry> NormalizeEntries(
        IEnumerable<TranslationDictionaryEntry>? entries)
    {
        var result = new List<TranslationDictionaryEntry>();
        if (entries is null)
        {
            return result;
        }

        var dedupe = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var source = entry.Source?.Trim() ?? string.Empty;
            var target = entry.Target?.Trim() ?? string.Empty;
            if (source.Length == 0 || target.Length == 0)
            {
                continue;
            }

            var normalized = new TranslationDictionaryEntry
            {
                Source = source,
                Target = target,
                EditorIdPattern = NormalizeOptionalPattern(entry.EditorIdPattern),
                FieldPattern = NormalizeOptionalPattern(entry.FieldPattern),
                MatchCase = entry.MatchCase,
                WholeWord = entry.WholeWord
            };

            var key = string.Join(
                "\u001f",
                normalized.Source,
                normalized.Target,
                normalized.EditorIdPattern ?? string.Empty,
                normalized.FieldPattern ?? string.Empty,
                normalized.MatchCase.ToString(CultureInfo.InvariantCulture),
                normalized.WholeWord.ToString(CultureInfo.InvariantCulture));
            if (!dedupe.Add(key))
            {
                continue;
            }

            result.Add(normalized);
        }

        return result;
    }

    public static string BuildScopeDisplay(
        TranslationDictionaryEntry entry,
        string globalLabel)
    {
        var segments = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(entry.EditorIdPattern))
        {
            segments.Add($"EDID: {entry.EditorIdPattern!.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(entry.FieldPattern))
        {
            segments.Add($"FIELD: {entry.FieldPattern!.Trim()}");
        }

        return segments.Count == 0
            ? globalLabel
            : string.Join(" | ", segments);
    }

    public static List<TranslationDictionaryEntry> ParseDelimitedEntries(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var entries = new List<TranslationDictionaryEntry>();
        var headerChecked = false;
        foreach (var rawLine in EnumerateLines(content))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var delimiter = rawLine.Contains('\t') ? '\t' : ',';
            var columns = ParseDelimitedLine(rawLine, delimiter);
            if (columns.Count == 0)
            {
                continue;
            }

            if (!headerChecked)
            {
                headerChecked = true;
                if (IsHeader(columns))
                {
                    continue;
                }
            }

            var source = NormalizeField(columns, 0);
            var target = NormalizeField(columns, 1);
            var editorIdPattern = NormalizeField(columns, 2);
            var fieldPattern = NormalizeField(columns, 3);
            var matchCase = ParseBoolean(columns, 4);
            var wholeWord = ParseBoolean(columns, 5);

            entries.Add(new TranslationDictionaryEntry
            {
                Source = source ?? string.Empty,
                Target = target ?? string.Empty,
                EditorIdPattern = editorIdPattern,
                FieldPattern = fieldPattern,
                MatchCase = matchCase,
                WholeWord = wholeWord
            });
        }

        return NormalizeEntries(entries);
    }

    private static bool IsScopePatternMatch(string? pattern, string value)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        var scopePattern = pattern.Trim();
        if (!scopePattern.Contains('*') && !scopePattern.Contains('?'))
        {
            return value.Contains(scopePattern, StringComparison.OrdinalIgnoreCase);
        }

        var regexPattern = "^" +
                           Regex.Escape(scopePattern)
                               .Replace(@"\*", ".*", StringComparison.Ordinal)
                               .Replace(@"\?", ".", StringComparison.Ordinal) +
                           "$";
        return Regex.IsMatch(
            value,
            regexPattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static IEnumerable<string> EnumerateLines(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return normalized.Split('\n');
    }

    private static List<string> ParseDelimitedLine(string line, char delimiter)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == delimiter && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(ch);
        }

        result.Add(sb.ToString());
        return result;
    }

    private static bool IsHeader(IReadOnlyList<string> columns)
    {
        var first = NormalizeField(columns, 0);
        var second = NormalizeField(columns, 1);
        if (first is null || second is null)
        {
            return false;
        }

        return string.Equals(first, "source", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(second, "target", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeField(IReadOnlyList<string> columns, int index)
    {
        if (index >= columns.Count)
        {
            return null;
        }

        var value = columns[index]
            .Trim()
            .Trim('\uFEFF')
            .Trim();
        return value.Length == 0 ? null : value;
    }

    private static bool ParseBoolean(IReadOnlyList<string> columns, int index)
    {
        var value = NormalizeField(columns, index);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" => true,
            "true" => true,
            "yes" => true,
            "y" => true,
            "on" => true,
            _ => false
        };
    }

    private static string? NormalizeOptionalPattern(string? pattern)
    {
        return string.IsNullOrWhiteSpace(pattern) ? null : pattern.Trim();
    }

    private sealed class TranslationDictionaryDocument
    {
        public int Version { get; init; }
        public List<TranslationDictionaryEntry> Entries { get; init; } = [];
    }
}
