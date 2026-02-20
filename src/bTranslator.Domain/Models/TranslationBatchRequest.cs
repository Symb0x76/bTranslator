namespace bTranslator.Domain.Models;

public sealed class TranslationBatchRequest
{
    public required string SourceLanguage { get; init; }
    public required string TargetLanguage { get; init; }
    public required IReadOnlyList<string> Items { get; init; }
    public string? PromptTemplate { get; init; }
}

