namespace bTranslator.Domain.Models.Compare;

public sealed class EspCompareRow
{
    public required string StableKey { get; init; }
    public required string EditorId { get; init; }
    public required string FieldSignature { get; init; }
    public required string ListKind { get; init; }
    public required string SourceText { get; init; }
    public required string TranslatedText { get; init; }
}
