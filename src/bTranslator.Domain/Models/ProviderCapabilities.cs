namespace bTranslator.Domain.Models;

public sealed class ProviderCapabilities
{
    public int MaxCharsPerRequest { get; init; } = 4000;
    public int MaxItemsPerBatch { get; init; } = 20;
    public int RequestsPerMinute { get; init; } = 30;
    public int CharsPerMinute { get; init; } = 30000;
    public bool SupportsBatch { get; init; } = true;
}

