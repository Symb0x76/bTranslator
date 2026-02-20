namespace bTranslator.Domain.Models;

[Flags]
public enum SstEntryFlags : byte
{
    None = 0,
    Translated = 1 << 0,
    LockedTranslation = 1 << 1,
    IncompleteTranslation = 1 << 2,
    Validated = 1 << 3,
    DeprecatedParam1 = 1 << 4,
    DeprecatedParam2 = 1 << 5,
    OldData = 1 << 6,
    Pending = 1 << 7
}

