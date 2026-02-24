using bTranslator.Domain.Models.Compare;

namespace bTranslator.Domain.Services.Compare;

public static class EspCompareEngine
{
    public static EspCompareResult Compare(
        IReadOnlyList<EspCompareRow> baseRows,
        IReadOnlyList<EspCompareRow> compareRows)
    {
        ArgumentNullException.ThrowIfNull(baseRows);
        ArgumentNullException.ThrowIfNull(compareRows);

        var compareMap = compareRows
            .GroupBy(static row => row.StableKey, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        var baseMap = baseRows
            .GroupBy(static row => row.StableKey, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        var matched = 0;
        var missingInCompare = 0;
        var sourceDifferent = 0;
        var translationDifferent = 0;
        var candidates = new List<EspCompareCandidate>();

        foreach (var baseRow in baseMap.Values)
        {
            if (!compareMap.TryGetValue(baseRow.StableKey, out var incoming))
            {
                missingInCompare++;
                continue;
            }

            matched++;

            var sourceMatches = TextEquals(baseRow.SourceText, incoming.SourceText);
            if (!sourceMatches)
            {
                sourceDifferent++;
            }

            var translationMatches = TextEquals(baseRow.TranslatedText, incoming.TranslatedText);
            if (!translationMatches)
            {
                translationDifferent++;
            }

            if (!sourceMatches || translationMatches || string.IsNullOrWhiteSpace(incoming.TranslatedText))
            {
                continue;
            }

            candidates.Add(new EspCompareCandidate
            {
                StableKey = baseRow.StableKey,
                EditorId = baseRow.EditorId,
                FieldSignature = baseRow.FieldSignature,
                ListKind = baseRow.ListKind,
                SourceText = baseRow.SourceText,
                CurrentTranslatedText = baseRow.TranslatedText,
                IncomingTranslatedText = incoming.TranslatedText
            });
        }

        var missingInBase = compareMap.Keys.Count(key => !baseMap.ContainsKey(key));

        return new EspCompareResult
        {
            BaseRowCount = baseMap.Count,
            CompareRowCount = compareMap.Count,
            MatchedRowCount = matched,
            MissingInCompareCount = missingInCompare,
            MissingInBaseCount = missingInBase,
            SourceDifferentCount = sourceDifferent,
            TranslationDifferentCount = translationDifferent,
            Candidates = candidates
        };
    }

    private static bool TextEquals(string left, string right)
    {
        return string.Equals(
            NormalizeLineEndings(left),
            NormalizeLineEndings(right),
            StringComparison.Ordinal);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
