namespace bTranslator.Domain.Models;

public sealed class TranslationJobResult
{
    public required string ProviderId { get; init; }
    public required IReadOnlyList<TranslationItem> Items { get; init; }
}

