using bTranslator.App.ViewModels;
using bTranslator.App.Views;
using bTranslator.App.Localization;
using bTranslator.Automation;
using bTranslator.Application.Abstractions;
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
    private bool _uiLanguageApplied;

    public Window? MainWindow => _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

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
                services.AddSingleton<IAppLocalizationService, AppLocalizationService>();
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

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory("logs");
            File.AppendAllText(
                Path.Combine("logs", "startup-exception.log"),
                $"[XAML] {DateTimeOffset.Now:O}\n{e.Exception}\n\n");
        }
        catch
        {
            // Ignore logging failures.
        }
    }

    private void OnDomainUnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory("logs");
            File.AppendAllText(
                Path.Combine("logs", "startup-exception.log"),
                $"[DOMAIN] {DateTimeOffset.Now:O}\n{e.ExceptionObject}\n\n");
        }
        catch
        {
            // Ignore logging failures.
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ApplyConfiguredUiLanguage();

        _window ??= new Window
        {
            Content = _host.Services.GetRequiredService<MainPage>()
        };

        _window.Activate();
    }

    private void ApplyConfiguredUiLanguage()
    {
        if (_uiLanguageApplied)
        {
            return;
        }

        try
        {
            var localizer = _host.Services.GetRequiredService<IAppLocalizationService>();
            var settings = _host.Services.GetRequiredService<ISettingsStore>();
            var configuredLanguage =
                settings.GetAsync(AppLocalizationService.UiLanguageSettingKey).GetAwaiter().GetResult();
            localizer.ApplyLanguage(configuredLanguage ?? string.Empty);
        }
        catch
        {
            // Fall back to system language if settings are unavailable.
        }

        _uiLanguageApplied = true;
    }
}

