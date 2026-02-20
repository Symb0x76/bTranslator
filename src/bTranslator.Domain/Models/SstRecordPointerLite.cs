namespace bTranslator.Domain.Models;

public sealed class SstRecordPointerLite
{
    public int StringId { get; init; }
    public uint FormId { get; init; }
    public string RecordSignature { get; init; } = "****";
    public string FieldSignature { get; init; } = "****";
    public ushort Index { get; init; }
    public ushort IndexMax { get; init; }
    public uint RecordHash { get; init; }
}

