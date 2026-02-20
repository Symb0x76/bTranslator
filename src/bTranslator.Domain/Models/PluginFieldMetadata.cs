namespace bTranslator.Domain.Models;

public sealed class PluginFieldMetadata
{
    public required uint FormId { get; init; }
    public required string RecordSignature { get; init; }
    public required string FieldSignature { get; init; }
    public required int FieldIndex { get; init; }
    public required byte ListIndex { get; init; }
    public bool NotNull { get; init; }
    public bool NoZero { get; init; }
    public bool Ignored { get; init; }
    public bool HasTerminator { get; init; } = true;
    public string? Processor { get; init; }

    public string BuildKey()
    {
        return $"{RecordSignature}:{FormId:X8}:{FieldSignature}:{FieldIndex}";
    }
}

