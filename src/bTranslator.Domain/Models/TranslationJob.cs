namespace bTranslator.Domain.Models;

public sealed class TranslationJob
{
    public required string SourceLanguage { get; init; }
    public required string TargetLanguage { get; init; }
    public IReadOnlyList<string> ProviderChain { get; init; } = Array.Empty<string>();
    public IReadOnlyList<TranslationItem> Items { get; init; } = Array.Empty<TranslationItem>();
    public bool NormalizePlaceholders { get; init; } = true;
}

