using System.Text;

namespace bTranslator.Domain.Models;

public sealed class PluginSaveOptions
{
    public string Language { get; init; } = "english";
    public string? OutputStringsDirectory { get; init; }
    public bool SaveStrings { get; init; } = true;
    public bool SaveRecordFields { get; init; } = true;
    public Encoding Encoding { get; init; } = Encoding.UTF8;
}

