using bTranslator.Application.Abstractions;
using bTranslator.Infrastructure.Bethesda.Services;
using Microsoft.Extensions.DependencyInjection;

namespace bTranslator.Infrastructure.Bethesda;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddbTranslatorBethesda(this IServiceCollection services)
    {
        services.AddSingleton<RecordDefinitionCatalog>();
        services.AddSingleton<PluginBinaryCodec>();
        services.AddSingleton<PluginRecordMapper>();
        services.AddSingleton<IStringsCodec, BethesdaStringsCodec>();
        services.AddSingleton<IPluginDocumentService, MutagenPluginDocumentService>();
        services.AddSingleton<IArchiveToolchainService, BsaBa2ArchiveToolchainService>();
        services.AddSingleton<IPexToolchainService, PexToolchainService>();
        return services;
    }
}

