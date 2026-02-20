namespace bTranslator.Domain.Models;

public sealed class BatchExecutionScope
{
    public required IList<TranslationItem> Items { get; init; }
    public bool DryRun { get; init; }
}

