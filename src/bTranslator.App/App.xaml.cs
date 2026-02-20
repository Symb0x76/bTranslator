using bTranslator.App.ViewModels;
using bTranslator.App.Views;
using bTranslator.Automation;
using bTranslator.Infrastructure.Bethesda;
using bTranslator.Infrastructure.Persistence;
using bTranslator.Infrastructure.Security;
using bTranslator.Infrastructure.Translation;
using bTranslator.Infrastructure.Translation.Options;
using bTranslator.Infrastructure.Translation.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace bTranslator.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly IHost _host;
    private Window? _window;

    public Window? MainWindow => _window;

    public App()
    {
        InitializeComponent();

        _host = Host
            .CreateDefaultBuilder()
            .UseSerilog((_, cfg) =>
            {
                cfg.WriteTo.Console();
                cfg.WriteTo.File("logs/bTranslator.log", rollingInterval: RollingInterval.Day);
            })
            .ConfigureServices(services =>
            {
                services.AddbTranslatorBethesda();
                services.AddbTranslatorSecurity();
                services.AddbTranslatorPersistence();
                services.AddbTranslatorAutomation();
                services.AddbTranslatorTranslation(options =>
                {
                    options.Providers[OpenAiCompatibleTranslationProvider.DefaultProviderId] = new ProviderEndpointOptions
                    {
                        BaseUrl = "https://api.openai.com/v1/chat/completions",
                        Model = "gpt-4o-mini"
                    };
                    options.Providers[OllamaTranslationProvider.DefaultProviderId] = new ProviderEndpointOptions
                    {
                        BaseUrl = "http://localhost:11434/api/chat",
                        Model = "qwen2.5:7b"
                    };
                    options.Providers[AzureTranslatorProvider.DefaultProviderId] = new ProviderEndpointOptions
                    {
                        BaseUrl = "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0"
                    };
                    options.Providers[DeepLTranslationProvider.DefaultProviderId] = new ProviderEndpointOptions
                    {
                        BaseUrl = "https://api-free.deepl.com/v2/translate"
                    };
                    options.Providers[GoogleCloudTranslationProvider.DefaultProviderId] = new ProviderEndpointOptions
                    {
                        BaseUrl = "https://translation.googleapis.com/language/translate/v2"
                    };
                    options.Providers[BaiduTranslationProvider.DefaultProviderId] = new ProviderEndpointOptions
                    {
                        BaseUrl = "https://fanyi-api.baidu.com/api/trans/vip/translate"
                    };
                    options.Providers[TencentTranslationProvider.DefaultProviderId] = new ProviderEndpointOptions
                    {
                        BaseUrl = "https://tmt.tencentcloudapi.com",
                        Region = "ap-guangzhou",
                        Model = "0"
                    };
                    options.Providers[VolcengineTranslationProvider.DefaultProviderId] = new ProviderEndpointOptions
                    {
                        BaseUrl = "https://ark.cn-beijing.volces.com/api/v3/chat/completions",
                        Model = "doubao-lite-32k"
                    };
                    options.Providers[AnthropicTranslationProvider.DefaultProviderId] = new ProviderEndpointOptions
                    {
                        BaseUrl = "https://api.anthropic.com/v1/messages",
                        Model = "claude-3-5-haiku-latest"
                    };
                    options.Providers[GeminiTranslationProvider.DefaultProviderId] = new ProviderEndpointOptions
                    {
                        BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent",
                        Model = "gemini-1.5-flash"
                    };
                });

                services.AddSingleton<MainViewModel>();
                services.AddTransient<MainPage>();
            })
            .Build();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window ??= new Window
        {
            Content = _host.Services.GetRequiredService<MainPage>()
        };

        _window.Activate();
    }
}

