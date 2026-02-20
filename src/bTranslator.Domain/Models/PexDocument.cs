namespace bTranslator.Domain.Models;

public sealed class PexDocument
{
    public required uint Magic { get; init; }
    public required byte MajorVersion { get; init; }
    public required byte MinorVersion { get; init; }
    public string SourceFile { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public IList<PexStringEntry> Strings { get; init; } = new List<PexStringEntry>();
}

