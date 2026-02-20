namespace bTranslator.Domain.Models;

public sealed class SstEntryMetadata
{
    public byte ListIndex { get; init; }
    public byte CollaborationId { get; init; }
    public string? CollaborationLabel { get; init; }
    public SstEntryFlags Flags { get; init; } = SstEntryFlags.None;
    public SstRecordPointerLite Pointer { get; init; } = new();
}

