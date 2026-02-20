namespace bTranslator.Domain.Models;

public sealed class TranslationBatchResult
{
    public required IReadOnlyList<string> Items { get; init; }
    public string? RawResponse { get; init; }
}

