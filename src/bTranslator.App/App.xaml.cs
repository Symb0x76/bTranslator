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
    private const string UiThemeSettingKey = "ui.theme";
    private const string ThemeDark = "dark";
    private const string ThemeLight = "light";
    private bool _themeApplied;

    private static readonly IReadOnlyDictionary<string, string> DarkApplicationBrushPalette =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VsCodeBackgroundBrush"] = "#1E1E1E",
            ["VsCodeSurfaceBrush"] = "#252526",
            ["VsCodeSurfaceAltBrush"] = "#2D2D30",
            ["VsCodeBorderBrush"] = "#3C3C3C",
            ["VsCodeTextBrush"] = "#CCCCCC",
            ["VsCodeSubtleTextBrush"] = "#9D9D9D",
            ["VsCodeAccentBrush"] = "#0E639C",
            ["VsCodeAccentHoverBrush"] = "#1177BB",
            ["VsCodeAccentPressedBrush"] = "#0C4F7D",
            ["VsCodeSurfaceHoverBrush"] = "#343438",
            ["VsCodeSurfacePressedBrush"] = "#2A2B2E",
            ["VsCodeFocusBrush"] = "#0E639C",
            ["ButtonBackground"] = "#2D2D30",
            ["ButtonBackgroundPointerOver"] = "#343438",
            ["ButtonBackgroundPressed"] = "#2A2B2E",
            ["ButtonBorderBrush"] = "#3C3C3C",
            ["ButtonBorderBrushPointerOver"] = "#4A4F56",
            ["ButtonBorderBrushPressed"] = "#0E639C",
            ["ButtonForeground"] = "#CCCCCC",
            ["ButtonForegroundPointerOver"] = "#F3F3F3",
            ["ButtonForegroundPressed"] = "#FFFFFF",
            ["AccentButtonBackground"] = "#0E639C",
            ["AccentButtonBackgroundPointerOver"] = "#1177BB",
            ["AccentButtonBackgroundPressed"] = "#0C4F7D",
            ["AccentButtonBorderBrush"] = "#0E639C",
            ["AccentButtonBorderBrushPointerOver"] = "#1177BB",
            ["AccentButtonBorderBrushPressed"] = "#0C4F7D",
            ["AccentButtonForeground"] = "#FFFFFF",
            ["AccentButtonForegroundPointerOver"] = "#FFFFFF",
            ["AccentButtonForegroundPressed"] = "#FFFFFF",
            ["SubtleButtonBackground"] = "#2D2D30",
            ["SubtleButtonBackgroundPointerOver"] = "#343438",
            ["SubtleButtonBackgroundPressed"] = "#2A2B2E",
            ["SubtleButtonBorderBrush"] = "#3C3C3C",
            ["SubtleButtonBorderBrushPointerOver"] = "#4A4F56",
            ["SubtleButtonBorderBrushPressed"] = "#3C3C3C",
            ["SubtleButtonForeground"] = "#CCCCCC",
            ["SubtleButtonForegroundPointerOver"] = "#F3F3F3",
            ["SubtleButtonForegroundPressed"] = "#FFFFFF"
        };

    private static readonly IReadOnlyDictionary<string, string> LightApplicationBrushPalette =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VsCodeBackgroundBrush"] = "#F3F5F8",
            ["VsCodeSurfaceBrush"] = "#FFFFFF",
            ["VsCodeSurfaceAltBrush"] = "#EFF2F7",
            ["VsCodeBorderBrush"] = "#C9D3E1",
            ["VsCodeTextBrush"] = "#1F2937",
            ["VsCodeSubtleTextBrush"] = "#5F6B7C",
            ["VsCodeAccentBrush"] = "#0B63CE",
            ["VsCodeAccentHoverBrush"] = "#0A57B6",
            ["VsCodeAccentPressedBrush"] = "#084690",
            ["VsCodeSurfaceHoverBrush"] = "#E6ECF5",
            ["VsCodeSurfacePressedBrush"] = "#DCE4F1",
            ["VsCodeFocusBrush"] = "#0B63CE",
            ["ButtonBackground"] = "#EFF2F7",
            ["ButtonBackgroundPointerOver"] = "#E4EAF4",
            ["ButtonBackgroundPressed"] = "#D9E2F0",
            ["ButtonBorderBrush"] = "#C9D3E1",
            ["ButtonBorderBrushPointerOver"] = "#D7E0ED",
            ["ButtonBorderBrushPressed"] = "#0B63CE",
            ["ButtonForeground"] = "#1F2937",
            ["ButtonForegroundPointerOver"] = "#111827",
            ["ButtonForegroundPressed"] = "#0B1220",
            ["AccentButtonBackground"] = "#0B63CE",
            ["AccentButtonBackgroundPointerOver"] = "#0A57B6",
            ["AccentButtonBackgroundPressed"] = "#084690",
            ["AccentButtonBorderBrush"] = "#0B63CE",
            ["AccentButtonBorderBrushPointerOver"] = "#0A57B6",
            ["AccentButtonBorderBrushPressed"] = "#084690",
            ["AccentButtonForeground"] = "#FFFFFF",
            ["AccentButtonForegroundPointerOver"] = "#FFFFFF",
            ["AccentButtonForegroundPressed"] = "#FFFFFF",
            ["SubtleButtonBackground"] = "#F6F8FB",
            ["SubtleButtonBackgroundPointerOver"] = "#EEF3FA",
            ["SubtleButtonBackgroundPressed"] = "#E3EBF8",
            ["SubtleButtonBorderBrush"] = "#C9D3E1",
            ["SubtleButtonBorderBrushPointerOver"] = "#C4D1E3",
            ["SubtleButtonBorderBrushPressed"] = "#B9C8DE",
            ["SubtleButtonForeground"] = "#1F2937",
            ["SubtleButtonForegroundPointerOver"] = "#111827",
            ["SubtleButtonForegroundPressed"] = "#0B1220"
        };

    private static readonly IReadOnlyDictionary<string, string> DarkPageBrushPalette =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MainSurfaceBrush"] = "#232326",
            ["MainSurfaceAltBrush"] = "#1F2023",
            ["MainPanelBorderBrush"] = "#3A3D41",
            ["ListViewItemBackground"] = "#00000000",
            ["ListViewItemBackgroundPointerOver"] = "#2B2D31",
            ["ListViewItemBackgroundPressed"] = "#343840",
            ["ListViewItemBackgroundSelected"] = "#1E3447",
            ["ListViewItemBackgroundSelectedPointerOver"] = "#25425A",
            ["ListViewItemBackgroundSelectedPressed"] = "#2A4B66",
            ["ListViewItemForeground"] = "#CCCCCC",
            ["ListViewItemForegroundPointerOver"] = "#F0F0F0",
            ["ListViewItemForegroundPressed"] = "#FFFFFF",
            ["ListViewItemForegroundSelected"] = "#FFFFFF",
            ["ListViewItemForegroundSelectedPointerOver"] = "#FFFFFF",
            ["ListViewItemForegroundSelectedPressed"] = "#FFFFFF",
            ["ListViewItemFocusBorderBrush"] = "#0E639C",
            ["ListViewItemFocusSecondaryBorderBrush"] = "#1E1E1E",
            ["TextControlBackground"] = "#1F2023",
            ["TextControlBackgroundPointerOver"] = "#24262A",
            ["TextControlBackgroundFocused"] = "#252A30",
            ["TextControlBorderBrush"] = "#3A3D41",
            ["TextControlBorderBrushPointerOver"] = "#4A4F56",
            ["TextControlBorderBrushFocused"] = "#0E639C",
            ["TextControlForeground"] = "#CCCCCC",
            ["TextControlForegroundPointerOver"] = "#EFEFEF",
            ["TextControlForegroundFocused"] = "#FFFFFF",
            ["TextControlPlaceholderForeground"] = "#9D9D9D",
            ["TextControlPlaceholderForegroundPointerOver"] = "#A9A9A9",
            ["TextControlPlaceholderForegroundFocused"] = "#A9A9A9",
            ["TextControlSelectionHighlightColor"] = "#80489BD8",
            ["ComboBoxBackground"] = "#1F2023",
            ["ComboBoxBackgroundPointerOver"] = "#24262A",
            ["ComboBoxBackgroundPressed"] = "#2B2F35",
            ["ComboBoxBackgroundFocused"] = "#252A30",
            ["ComboBoxBorderBrush"] = "#3A3D41",
            ["ComboBoxBorderBrushPointerOver"] = "#4A4F56",
            ["ComboBoxBorderBrushPressed"] = "#0E639C",
            ["ComboBoxForeground"] = "#CCCCCC",
            ["ComboBoxForegroundPointerOver"] = "#EFEFEF",
            ["ComboBoxForegroundPressed"] = "#FFFFFF",
            ["ComboBoxForegroundFocused"] = "#FFFFFF",
            ["ComboBoxForegroundFocusedPressed"] = "#FFFFFF",
            ["ComboBoxBackgroundBorderBrushFocused"] = "#0E639C",
            ["ComboBoxBackgroundBorderBrushUnfocused"] = "#3A3D41"
        };

    private static readonly IReadOnlyDictionary<string, string> LightPageBrushPalette =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MainSurfaceBrush"] = "#FFFFFF",
            ["MainSurfaceAltBrush"] = "#F7F9FC",
            ["MainPanelBorderBrush"] = "#D5DFEC",
            ["ListViewItemBackground"] = "#00000000",
            ["ListViewItemBackgroundPointerOver"] = "#ECF2FA",
            ["ListViewItemBackgroundPressed"] = "#E2EBF8",
            ["ListViewItemBackgroundSelected"] = "#D8E8FC",
            ["ListViewItemBackgroundSelectedPointerOver"] = "#CDE1FA",
            ["ListViewItemBackgroundSelectedPressed"] = "#BFD7F8",
            ["ListViewItemForeground"] = "#1F2937",
            ["ListViewItemForegroundPointerOver"] = "#111827",
            ["ListViewItemForegroundPressed"] = "#0B1220",
            ["ListViewItemForegroundSelected"] = "#0B1220",
            ["ListViewItemForegroundSelectedPointerOver"] = "#0B1220",
            ["ListViewItemForegroundSelectedPressed"] = "#0B1220",
            ["ListViewItemFocusBorderBrush"] = "#0B63CE",
            ["ListViewItemFocusSecondaryBorderBrush"] = "#FFFFFF",
            ["TextControlBackground"] = "#F7F9FC",
            ["TextControlBackgroundPointerOver"] = "#EEF3FA",
            ["TextControlBackgroundFocused"] = "#FFFFFF",
            ["TextControlBorderBrush"] = "#D1DBE9",
            ["TextControlBorderBrushPointerOver"] = "#A8B9D3",
            ["TextControlBorderBrushFocused"] = "#0B63CE",
            ["TextControlForeground"] = "#1F2937",
            ["TextControlForegroundPointerOver"] = "#111827",
            ["TextControlForegroundFocused"] = "#111827",
            ["TextControlPlaceholderForeground"] = "#6B7280",
            ["TextControlPlaceholderForegroundPointerOver"] = "#5F6A78",
            ["TextControlPlaceholderForegroundFocused"] = "#5F6A78",
            ["TextControlSelectionHighlightColor"] = "#804A90E2",
            ["ComboBoxBackground"] = "#F7F9FC",
            ["ComboBoxBackgroundPointerOver"] = "#EEF3FA",
            ["ComboBoxBackgroundPressed"] = "#E2EBF8",
            ["ComboBoxBackgroundFocused"] = "#FFFFFF",
            ["ComboBoxBorderBrush"] = "#D1DBE9",
            ["ComboBoxBorderBrushPointerOver"] = "#A8B9D3",
            ["ComboBoxBorderBrushPressed"] = "#0B63CE",
            ["ComboBoxForeground"] = "#1F2937",
            ["ComboBoxForegroundPointerOver"] = "#111827",
            ["ComboBoxForegroundPressed"] = "#0B1220",
            ["ComboBoxForegroundFocused"] = "#111827",
            ["ComboBoxForegroundFocusedPressed"] = "#0B1220",
            ["ComboBoxBackgroundBorderBrushFocused"] = "#0B63CE",
            ["ComboBoxBackgroundBorderBrushUnfocused"] = "#D1DBE9"
        };

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
            ApplyConfiguredTheme();
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

    public void ApplyTheme(string? themeValue)
    {
        var normalizedTheme = NormalizeThemeValue(themeValue);
        var isLight = string.Equals(normalizedTheme, ThemeLight, StringComparison.Ordinal);

        ApplyBrushPalette(
            Resources,
            isLight ? LightApplicationBrushPalette : DarkApplicationBrushPalette);

        if (_window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = isLight
                ? Microsoft.UI.Xaml.ElementTheme.Light
                : Microsoft.UI.Xaml.ElementTheme.Dark;
            ApplyBrushPalette(
                root.Resources,
                isLight ? LightPageBrushPalette : DarkPageBrushPalette);
        }
    }

    private void ApplyConfiguredTheme()
    {
        if (_themeApplied)
        {
            return;
        }

        try
        {
            var settings = _host.Services.GetRequiredService<ISettingsStore>();
            var configuredTheme = settings.GetAsync(UiThemeSettingKey).GetAwaiter().GetResult();
            ApplyTheme(configuredTheme);
        }
        catch
        {
            ApplyTheme(ThemeDark);
        }

        _themeApplied = true;
    }

    private static void ApplyBrushPalette(
        Microsoft.UI.Xaml.ResourceDictionary resources,
        IReadOnlyDictionary<string, string> palette)
    {
        foreach (var pair in palette)
        {
            var color = ParseColor(pair.Value);
            if (resources.TryGetValue(pair.Key, out var existing)
                && existing is Microsoft.UI.Xaml.Media.SolidColorBrush brush)
            {
                brush.Color = color;
                continue;
            }

            resources[pair.Key] = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
        }
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        var normalized = hex.Trim();
        if (normalized.StartsWith("#", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        if (normalized.Length == 6)
        {
            normalized = "FF" + normalized;
        }

        if (normalized.Length != 8)
        {
            return Microsoft.UI.ColorHelper.FromArgb(255, 0, 0, 0);
        }

        var a = Convert.ToByte(normalized.Substring(0, 2), 16);
        var r = Convert.ToByte(normalized.Substring(2, 2), 16);
        var g = Convert.ToByte(normalized.Substring(4, 2), 16);
        var b = Convert.ToByte(normalized.Substring(6, 2), 16);
        return Microsoft.UI.ColorHelper.FromArgb(a, r, g, b);
    }

    private static string NormalizeThemeValue(string? themeValue)
    {
        if (string.IsNullOrWhiteSpace(themeValue))
        {
            return ThemeDark;
        }

        return themeValue.Trim().ToLowerInvariant() switch
        {
            "light" => ThemeLight,
            "dark" => ThemeDark,
            _ => ThemeDark
        };
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

