namespace bTranslator.Domain.Models;

public sealed class TranslationDictionaryEntry
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? EditorIdPattern { get; set; }
    public string? FieldPattern { get; set; }
    public bool MatchCase { get; set; }
    public bool WholeWord { get; set; }
}

