using bTranslator.Application.Abstractions;
using bTranslator.Infrastructure.Persistence.Compatibility;
using bTranslator.Infrastructure.Persistence.Options;
using bTranslator.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;

namespace bTranslator.Infrastructure.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddbTranslatorPersistence(
        this IServiceCollection services,
        Action<PersistenceOptions>? configure = null)
    {
        services.AddOptions<PersistenceOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        services.AddSingleton<ISstCompatibilityService, SstCompatibilityService>();
        services.AddSingleton<IXmlCompatibilityService, XmlCompatibilityService>();
        return services;
    }
}

