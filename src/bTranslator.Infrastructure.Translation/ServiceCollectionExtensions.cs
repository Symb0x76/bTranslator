using bTranslator.Application.Abstractions;
using bTranslator.Infrastructure.Translation.Options;
using bTranslator.Infrastructure.Translation.Providers;
using bTranslator.Infrastructure.Translation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace bTranslator.Infrastructure.Translation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddbTranslatorTranslation(
        this IServiceCollection services,
        Action<TranslationProviderOptions>? configure = null)
    {
        services.AddOptions<TranslationProviderOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IPlaceholderProtector, TagNumberPlaceholderProtector>();
        services.AddSingleton<ITranslationOrchestrator, TranslationOrchestrator>();

        services.AddHttpClient<OpenAiCompatibleTranslationProvider>();
        services.AddHttpClient<OllamaTranslationProvider>();
        services.AddHttpClient<AzureTranslatorProvider>();
        services.AddHttpClient<DeepLTranslationProvider>();
        services.AddHttpClient<GoogleCloudTranslationProvider>();
        services.AddHttpClient<BaiduTranslationProvider>();
        services.AddHttpClient<TencentTranslationProvider>();
        services.AddHttpClient<VolcengineTranslationProvider>();
        services.AddHttpClient<AnthropicTranslationProvider>();
        services.AddHttpClient<GeminiTranslationProvider>();

        services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<OpenAiCompatibleTranslationProvider>());
        services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<OllamaTranslationProvider>());
        services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<AzureTranslatorProvider>());
        services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<DeepLTranslationProvider>());
        services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<GoogleCloudTranslationProvider>());
        services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<BaiduTranslationProvider>());
        services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<TencentTranslationProvider>());
        services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<VolcengineTranslationProvider>());
        services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<AnthropicTranslationProvider>());
        services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<GeminiTranslationProvider>());

        return services;
    }
}

