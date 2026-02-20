namespace bTranslator.Domain.Models;

public sealed class BatchScript
{
    public string Name { get; init; } = "Legacy Batch";
    public IReadOnlyList<LegacyBatchRule> Rules { get; init; } = Array.Empty<LegacyBatchRule>();
}

