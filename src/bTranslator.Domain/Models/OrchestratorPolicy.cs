namespace bTranslator.Domain.Models;

public sealed class OrchestratorPolicy
{
    public int MaxRetries { get; init; } = 3;
    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromMilliseconds(400);
    public bool FailOnAuthenticationError { get; init; } = true;
}

