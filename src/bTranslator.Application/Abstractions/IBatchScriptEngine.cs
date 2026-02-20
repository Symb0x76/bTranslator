using bTranslator.Domain.Models;

namespace bTranslator.Application.Abstractions;

public interface IBatchScriptEngine
{
    BatchScript ParseLegacy(string content);
    BatchScript ParseV2(string yamlContent);
    Task<BatchExecutionResult> RunAsync(
        BatchScript script,
        BatchExecutionScope scope,
        CancellationToken cancellationToken = default);
}

