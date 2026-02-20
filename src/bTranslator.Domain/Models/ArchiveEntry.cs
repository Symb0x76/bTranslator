using bTranslator.Domain.Enums;

namespace bTranslator.Domain.Models;

public sealed class ArchiveEntry
{
    public required ArchiveFormat Format { get; init; }
    public required string Path { get; init; }
    public required long Offset { get; init; }
    public required int Size { get; init; }
    public bool Compressed { get; init; }
}

