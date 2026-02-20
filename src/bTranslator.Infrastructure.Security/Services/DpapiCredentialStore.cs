using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using bTranslator.Application.Abstractions;
using bTranslator.Infrastructure.Security.Options;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Security.Services;

public sealed class DpapiCredentialStore : ICredentialStore
{
    private const string FileName = "secrets.json";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("bTranslator.DPAPI.v1");
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DpapiCredentialStore(IOptions<CredentialStoreOptions> options)
    {
        Directory.CreateDirectory(options.Value.RootDirectory);
        _filePath = Path.Combine(options.Value.RootDirectory, FileName);
    }

    public async Task SetSecretAsync(
        string provider,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var doc = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var providerBucket = doc.TryGetValue(provider, out var existing)
                ? new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(value),
                Entropy,
                DataProtectionScope.CurrentUser);
            providerBucket[key] = Convert.ToBase64String(encrypted);
            doc[provider] = providerBucket;
            await SaveAsync(doc, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> GetSecretAsync(
        string provider,
        string key,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var doc = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!doc.TryGetValue(provider, out var providerBucket) ||
                !providerBucket.TryGetValue(key, out var encoded))
            {
                return null;
            }

            var ciphertext = Convert.FromBase64String(encoded);
            var plaintext = ProtectedData.Unprotect(ciphertext, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, Dictionary<string, string>>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = File.OpenRead(_filePath);
        var data = await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, string>>>(
            stream,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return data ?? new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task SaveAsync(
        Dictionary<string, Dictionary<string, string>> data,
        CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, data, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}

