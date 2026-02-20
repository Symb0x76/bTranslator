using bTranslator.Domain.Enums;
using bTranslator.Domain.Models;

namespace bTranslator.Application.Abstractions;

public interface IPluginDocumentService
{
    Task<PluginDocument> OpenAsync(
        GameKind game,
        string pluginPath,
        PluginOpenOptions options,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        PluginDocument document,
        string outputPath,
        PluginSaveOptions options,
        CancellationToken cancellationToken = default);
}

