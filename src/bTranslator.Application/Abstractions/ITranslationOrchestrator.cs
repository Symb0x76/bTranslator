using bTranslator.Domain.Models;

namespace bTranslator.Application.Abstractions;

public interface ITranslationOrchestrator
{
    Task<TranslationJobResult> ExecuteAsync(
        TranslationJob job,
        OrchestratorPolicy? policy = null,
        CancellationToken cancellationToken = default);
}

