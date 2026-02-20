using bTranslator.Domain.Enums;

namespace bTranslator.Domain.Models;

public sealed class PluginDocument
{
    public required GameKind Game { get; init; }
    public required string PluginPath { get; init; }
    public required string PluginName { get; init; }
    public DateTimeOffset LoadedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public IDictionary<StringsFileKind, IList<StringsEntry>> StringTables { get; init; } =
        new Dictionary<StringsFileKind, IList<StringsEntry>>();
    public IList<TranslationItem> RecordItems { get; init; } = new List<TranslationItem>();
}

