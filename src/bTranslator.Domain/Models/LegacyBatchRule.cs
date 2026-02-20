using bTranslator.Domain.Enums;

namespace bTranslator.Domain.Models;

public sealed class LegacyBatchRule
{
    public required string Search { get; init; }
    public required string Replace { get; init; }
    public string Pattern { get; init; } = "%REPLACE% %ORIG%";
    public BatchSelectionMode SelectionMode { get; init; } = BatchSelectionMode.All;
    public BatchRuleMode Mode { get; init; } = BatchRuleMode.SourceDrivenReplace;
    public bool CaseSensitive { get; init; }
    public bool AllLists { get; init; }
}

