namespace bTranslator.Domain.Models;

public readonly record struct TranslationDictionaryPrepareResult(
    string PreparedSource,
    IReadOnlyList<TranslationDictionaryTokenReplacement> Replacements);
