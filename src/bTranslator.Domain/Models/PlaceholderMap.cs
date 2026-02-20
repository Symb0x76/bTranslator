namespace bTranslator.Domain.Models;

public sealed class PlaceholderMap
{
    public IDictionary<string, string> Tokens { get; init; } = new Dictionary<string, string>();
}

