namespace bTranslator.Application.Abstractions;

public interface ICredentialStore
{
    Task SetSecretAsync(
        string provider,
        string key,
        string value,
        CancellationToken cancellationToken = default);

    Task<string?> GetSecretAsync(
        string provider,
        string key,
        CancellationToken cancellationToken = default);
}

