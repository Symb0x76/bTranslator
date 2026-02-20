namespace bTranslator.Domain.Models;

public sealed class BatchExecutionResult
{
    public required int ChangedItems { get; init; }
    public required IReadOnlyList<string> Logs { get; init; }
}

