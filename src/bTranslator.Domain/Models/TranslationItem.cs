namespace bTranslator.Domain.Models;

public sealed class TranslationItem
{
    public required string Id { get; init; }
    public required string SourceText { get; init; }
    public string? TranslatedText { get; set; }
    public bool IsValidated { get; set; }
    public bool IsLocked { get; set; }
    public SstEntryMetadata? SstMetadata { get; init; }
    public PluginFieldMetadata? PluginFieldMetadata { get; init; }
}

