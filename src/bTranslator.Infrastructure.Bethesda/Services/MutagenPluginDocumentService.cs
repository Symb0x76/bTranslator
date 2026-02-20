using bTranslator.Application.Abstractions;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Models;

namespace bTranslator.Infrastructure.Bethesda.Services;

public sealed class MutagenPluginDocumentService : IPluginDocumentService
{
    private readonly IStringsCodec _stringsCodec;
    private readonly PluginBinaryCodec _pluginBinaryCodec;
    private readonly PluginRecordMapper _pluginRecordMapper;
    private readonly RecordDefinitionCatalog _recordDefinitionCatalog;

    public MutagenPluginDocumentService(
        IStringsCodec stringsCodec,
        PluginBinaryCodec pluginBinaryCodec,
        PluginRecordMapper pluginRecordMapper,
        RecordDefinitionCatalog recordDefinitionCatalog)
    {
        _stringsCodec = stringsCodec;
        _pluginBinaryCodec = pluginBinaryCodec;
        _pluginRecordMapper = pluginRecordMapper;
        _recordDefinitionCatalog = recordDefinitionCatalog;
    }

    public async Task<PluginDocument> OpenAsync(
        GameKind game,
        string pluginPath,
        PluginOpenOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pluginPath))
        {
            throw new FileNotFoundException("Plugin file not found.", pluginPath);
        }

        var recordItems = new List<TranslationItem>();
        if (options.LoadRecordFields)
        {
            var rules = _recordDefinitionCatalog.Load(game, options.RecordDefinitionsPath);
            var binary = _pluginBinaryCodec.Load(pluginPath);
            recordItems = _pluginRecordMapper
                .ExtractRecordItems(binary, rules, options.Encoding)
                .ToList();
        }

        var document = new PluginDocument
        {
            Game = game,
            PluginPath = pluginPath,
            PluginName = Path.GetFileNameWithoutExtension(pluginPath),
            StringTables = new Dictionary<StringsFileKind, IList<StringsEntry>>(),
            RecordItems = recordItems
        };

        if (!options.LoadStrings)
        {
            return document;
        }

        var stringsDirectory = options.StringsDirectory ??
                               Path.Combine(Path.GetDirectoryName(pluginPath)!, "Strings");

        foreach (var kind in Enum.GetValues<StringsFileKind>())
        {
            var filePath = BuildStringsPath(stringsDirectory, document.PluginName, options.Language, kind);
            if (!File.Exists(filePath))
            {
                continue;
            }

            var entries = await _stringsCodec.ReadAsync(
                filePath,
                kind,
                options.Encoding,
                cancellationToken).ConfigureAwait(false);

            document.StringTables[kind] = entries.ToList();
        }

        return document;
    }

    public async Task SaveAsync(
        PluginDocument document,
        string outputPath,
        PluginSaveOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(document.PluginPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.Copy(document.PluginPath, outputPath, overwrite: true);
        }

        if (options.SaveRecordFields && document.RecordItems.Count > 0)
        {
            var binary = _pluginBinaryCodec.Load(outputPath);
            _pluginRecordMapper.ApplyRecordItems(binary, document.RecordItems, options.Encoding);
            _pluginBinaryCodec.Save(outputPath, binary);
        }

        if (!options.SaveStrings)
        {
            return;
        }

        var stringsDirectory = options.OutputStringsDirectory ??
                               Path.Combine(Path.GetDirectoryName(outputPath)!, "Strings");
        Directory.CreateDirectory(stringsDirectory);

        foreach (var table in document.StringTables)
        {
            var filePath = BuildStringsPath(stringsDirectory, document.PluginName, options.Language, table.Key);
            await _stringsCodec.WriteAsync(
                filePath,
                table.Key,
                table.Value,
                options.Encoding,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildStringsPath(
        string directory,
        string pluginName,
        string language,
        StringsFileKind kind)
    {
        var extension = kind switch
        {
            StringsFileKind.Strings => ".strings",
            StringsFileKind.DlStrings => ".dlstrings",
            StringsFileKind.IlStrings => ".ilstrings",
            _ => ".strings"
        };

        return Path.Combine(directory, $"{pluginName}_{language}{extension}");
    }
}

