namespace bTranslator.Domain.Models.Compare;

public sealed class EspCompareResult
{
    public int BaseRowCount { get; init; }
    public int CompareRowCount { get; init; }
    public int MatchedRowCount { get; init; }
    public int MissingInCompareCount { get; init; }
    public int MissingInBaseCount { get; init; }
    public int SourceDifferentCount { get; init; }
    public int TranslationDifferentCount { get; init; }
    public IReadOnlyList<EspCompareCandidate> Candidates { get; init; } = Array.Empty<EspCompareCandidate>();
}
