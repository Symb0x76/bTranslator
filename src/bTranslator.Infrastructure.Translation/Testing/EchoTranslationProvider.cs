using bTranslator.Application.Abstractions;
using bTranslator.Domain.Models;

namespace bTranslator.Infrastructure.Translation.Testing;

public sealed class EchoTranslationProvider : ITranslationProvider
{
    public EchoTranslationProvider(string providerId = "echo")
    {
        ProviderId = providerId;
    }

    public string ProviderId { get; }
    public ProviderCapabilities Capabilities { get; } = new();

    public Task<TranslationBatchResult> TranslateBatchAsync(
        TranslationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var output = request.Items.Select(text => $"[{request.TargetLanguage}] {text}").ToArray();
        return Task.FromResult(new TranslationBatchResult
        {
            Items = output
        });
    }
}

