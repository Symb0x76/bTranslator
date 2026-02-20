namespace bTranslator.Domain.Enums;

public enum TranslationErrorKind
{
    None = 0,
    Authentication = 1,
    RateLimit = 2,
    Transient = 3,
    Validation = 4,
    Fatal = 5
}

