namespace bTranslator.Domain.Models;

public sealed class ProtectedText
{
    public required string Text { get; init; }
    public required PlaceholderMap Map { get; init; }
}

