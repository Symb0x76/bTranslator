namespace bTranslator.Infrastructure.Bethesda.Models;

internal sealed class RecordDefinitionRule
{
    public required string FieldSignature { get; init; }
    public required string RecordSignature { get; init; }
    public required byte ListIndex { get; init; }
    public bool NotNull { get; init; }
    public bool NoZero { get; init; }
    public bool Ignored { get; init; }
    public string Processor { get; init; } = string.Empty;
}

