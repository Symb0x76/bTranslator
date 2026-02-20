using bTranslator.Application.Abstractions;
using bTranslator.Infrastructure.Security.Options;
using bTranslator.Infrastructure.Security.Services;
using Microsoft.Extensions.DependencyInjection;

namespace bTranslator.Infrastructure.Security;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddbTranslatorSecurity(
        this IServiceCollection services,
        Action<CredentialStoreOptions>? configure = null)
    {
        services.AddOptions<CredentialStoreOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<ICredentialStore, DpapiCredentialStore>();
        return services;
    }
}

