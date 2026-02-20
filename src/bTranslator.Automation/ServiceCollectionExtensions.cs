using bTranslator.Application.Abstractions;
using bTranslator.Automation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace bTranslator.Automation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddbTranslatorAutomation(this IServiceCollection services)
    {
        services.AddSingleton<IBatchScriptEngine, LegacyBatchScriptEngine>();
        return services;
    }
}

