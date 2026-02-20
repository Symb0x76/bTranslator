using bTranslator.Domain.Models;

namespace bTranslator.Application.Abstractions;

public interface ITranslationProvider
{
    string ProviderId { get; }
    ProviderCapabilities Capabilities { get; }

    Task<TranslationBatchResult> TranslateBatchAsync(
        TranslationBatchRequest request,
        CancellationToken cancellationToken = default);
}

