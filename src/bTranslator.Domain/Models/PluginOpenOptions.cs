using System.Text;

namespace bTranslator.Domain.Models;

public sealed class PluginOpenOptions
{
    public string Language { get; init; } = "english";
    public string? StringsDirectory { get; init; }
    public string? RecordDefinitionsPath { get; init; }
    public bool LoadStrings { get; init; } = true;
    public bool LoadRecordFields { get; init; } = true;
    public Encoding Encoding { get; init; } = Encoding.UTF8;
}

