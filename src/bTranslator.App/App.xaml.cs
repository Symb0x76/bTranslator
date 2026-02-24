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
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Windows.Graphics;
using WinRT.Interop;

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
        LogStartupException("XAML", e.Exception);
    }

    private void OnDomainUnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
    {
        LogStartupException("DOMAIN", e.ExceptionObject);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            ApplyConfiguredUiLanguage();

            _window ??= new Window();
            _window.Content ??= _host.Services.GetRequiredService<MainPage>();
            _window.Activate();
            EnsureWindowVisible(_window);
        }
        catch (Exception ex)
        {
            LogStartupException("LAUNCH", ex);

            _window ??= new Window();
            _window.Content = BuildStartupErrorView(ex);
            _window.Activate();
        }
    }

    private static void EnsureWindowVisible(Window window)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Restore();
            }

            var currentRect = new RectInt32(
                appWindow.Position.X,
                appWindow.Position.Y,
                appWindow.Size.Width,
                appWindow.Size.Height);

            var intersects = RectanglesIntersect(currentRect, workArea);
            if (intersects && currentRect.Width > 0 && currentRect.Height > 0)
            {
                return;
            }

            const int preferredWidth = 1440;
            const int preferredHeight = 900;
            var targetWidth = Math.Min(preferredWidth, workArea.Width);
            var targetHeight = Math.Min(preferredHeight, workArea.Height);
            var targetX = workArea.X + Math.Max(0, (workArea.Width - targetWidth) / 2);
            var targetY = workArea.Y + Math.Max(0, (workArea.Height - targetHeight) / 2);

            appWindow.MoveAndResize(new RectInt32(targetX, targetY, targetWidth, targetHeight));
        }
        catch
        {
            // Ignore visibility correction failures.
        }
    }

    private static bool RectanglesIntersect(RectInt32 left, RectInt32 right)
    {
        return left.X < right.X + right.Width &&
               left.X + left.Width > right.X &&
               left.Y < right.Y + right.Height &&
               left.Y + left.Height > right.Y;
    }

    private static FrameworkElement BuildStartupErrorView(Exception ex)
    {
        return new ScrollViewer
        {
            Content = new TextBlock
            {
                Margin = new Thickness(16),
                TextWrapping = TextWrapping.Wrap,
                Text =
                    "bTranslator startup failed.\n\n" +
                    "See logs/startup-exception.log for details.\n\n" +
                    ex.ToString()
            }
        };
    }

    private static void LogStartupException(string source, Exception ex)
    {
        try
        {
            Directory.CreateDirectory("logs");
            File.AppendAllText(
                Path.Combine("logs", "startup-exception.log"),
                $"[{source}] {DateTimeOffset.Now:O}\n{ex}\n\n");
        }
        catch
        {
            // Ignore logging failures.
        }
    }

    private static void LogStartupException(string source, object? ex)
    {
        if (ex is Exception exception)
        {
            LogStartupException(source, exception);
            return;
        }

        try
        {
            Directory.CreateDirectory("logs");
            File.AppendAllText(
                Path.Combine("logs", "startup-exception.log"),
                $"[{source}] {DateTimeOffset.Now:O}\n{ex}\n\n");
        }
        catch
        {
            // Ignore logging failures.
        }
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

