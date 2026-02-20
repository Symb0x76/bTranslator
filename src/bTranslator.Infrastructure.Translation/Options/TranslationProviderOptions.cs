using bTranslator.Domain.Models;

namespace bTranslator.Infrastructure.Translation.Options;

public sealed class TranslationProviderOptions
{
    public IDictionary<string, ProviderEndpointOptions> Providers { get; init; } =
        new Dictionary<string, ProviderEndpointOptions>(StringComparer.OrdinalIgnoreCase);
}

public sealed class ProviderEndpointOptions
{
    public string? BaseUrl { get; init; }
    public string? Model { get; init; }
    public string? ApiKey { get; init; }
    public string? ApiSecret { get; init; }
    public string? Region { get; init; }
    public string? Organization { get; init; }
    public string? PromptTemplate { get; init; }
    public IDictionary<string, string> LanguageMap { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public ProviderCapabilities Capabilities { get; init; } = new();
}

