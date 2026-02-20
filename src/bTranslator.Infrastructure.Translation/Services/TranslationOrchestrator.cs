using System.Threading.RateLimiting;
using bTranslator.Application.Abstractions;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using Microsoft.Extensions.Logging;

namespace bTranslator.Infrastructure.Translation.Services;

public sealed class TranslationOrchestrator : ITranslationOrchestrator
{
    private readonly IReadOnlyDictionary<string, ITranslationProvider> _providers;
    private readonly IPlaceholderProtector _placeholderProtector;
    private readonly ILogger<TranslationOrchestrator> _logger;

    public TranslationOrchestrator(
        IEnumerable<ITranslationProvider> providers,
        IPlaceholderProtector placeholderProtector,
        ILogger<TranslationOrchestrator> logger)
    {
        _providers = providers.ToDictionary(x => x.ProviderId, StringComparer.OrdinalIgnoreCase);
        _placeholderProtector = placeholderProtector;
        _logger = logger;
    }

    public async Task<TranslationJobResult> ExecuteAsync(
        TranslationJob job,
        OrchestratorPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        policy ??= new OrchestratorPolicy();
        if (job.ProviderChain.Count == 0)
        {
            throw new ArgumentException("Provider chain is empty.", nameof(job));
        }

        var writable = job.Items.Select(item => new TranslationItem
        {
            Id = item.Id,
            SourceText = item.SourceText,
            TranslatedText = item.TranslatedText,
            IsLocked = item.IsLocked,
            IsValidated = item.IsValidated
        }).ToList();

        var lastError = default(Exception);
        foreach (var providerId in job.ProviderChain)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_providers.TryGetValue(providerId, out var provider))
            {
                _logger.LogWarning("Provider '{ProviderId}' not registered, skipping.", providerId);
                continue;
            }

            try
            {
                await TranslateWithProviderAsync(provider, job, writable, policy, cancellationToken).ConfigureAwait(false);
                return new TranslationJobResult
                {
                    ProviderId = provider.ProviderId,
                    Items = writable
                };
            }
            catch (TranslationProviderException ex) when (ex.ErrorKind == TranslationErrorKind.Authentication && !policy.FailOnAuthenticationError)
            {
                _logger.LogWarning(ex, "Provider '{ProviderId}' auth failed, moving to next provider.", provider.ProviderId);
                lastError = ex;
            }
            catch (TranslationProviderException ex) when (ex.ErrorKind != TranslationErrorKind.Authentication)
            {
                _logger.LogWarning(ex, "Provider '{ProviderId}' failed, moving to fallback.", provider.ProviderId);
                lastError = ex;
            }
        }

        throw new InvalidOperationException("No provider in chain completed translation.", lastError);
    }

    private async Task TranslateWithProviderAsync(
        ITranslationProvider provider,
        TranslationJob job,
        IList<TranslationItem> writableItems,
        OrchestratorPolicy policy,
        CancellationToken cancellationToken)
    {
        var chunks = BuildChunks(writableItems, provider.Capabilities);
        var limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(provider.Capabilities.RequestsPerMinute, 1),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 512,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var lease = await limiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                throw new TranslationProviderException(
                    provider.ProviderId,
                    TranslationErrorKind.RateLimit,
                    $"Rate limit not acquired for provider '{provider.ProviderId}'.");
            }

            var source = chunk.Select(static x => x.SourceText).ToArray();
            var protectedMap = new PlaceholderMap[source.Length];
            if (job.NormalizePlaceholders)
            {
                for (var i = 0; i < source.Length; i++)
                {
                    var protectedText = _placeholderProtector.Protect(source[i]);
                    source[i] = protectedText.Text;
                    protectedMap[i] = protectedText.Map;
                }
            }

            var request = new TranslationBatchRequest
            {
                SourceLanguage = job.SourceLanguage,
                TargetLanguage = job.TargetLanguage,
                Items = source
            };

            var result = await ExecuteWithRetryAsync(
                provider,
                request,
                policy,
                cancellationToken).ConfigureAwait(false);

            if (result.Items.Count != chunk.Count)
            {
                throw new TranslationProviderException(
                    provider.ProviderId,
                    TranslationErrorKind.Validation,
                    $"Provider '{provider.ProviderId}' returned {result.Items.Count} items, expected {chunk.Count}.");
            }

            for (var i = 0; i < chunk.Count; i++)
            {
                var translated = result.Items[i];
                if (job.NormalizePlaceholders)
                {
                    translated = _placeholderProtector.Restore(translated, protectedMap[i]);
                }

                chunk[i].TranslatedText = translated;
            }
        }
    }

    private static List<List<TranslationItem>> BuildChunks(
        IList<TranslationItem> items,
        ProviderCapabilities capabilities)
    {
        var chunks = new List<List<TranslationItem>>();
        var current = new List<TranslationItem>();
        var chars = 0;
        var maxItems = Math.Max(capabilities.MaxItemsPerBatch, 1);
        var maxChars = Math.Max(capabilities.MaxCharsPerRequest, 1);

        foreach (var item in items.Where(static x => !x.IsLocked))
        {
            var length = Math.Max(item.SourceText.Length, 1);
            var overSize = current.Count >= maxItems || (chars + length) > maxChars;
            if (overSize && current.Count > 0)
            {
                chunks.Add(current);
                current = new List<TranslationItem>();
                chars = 0;
            }

            current.Add(item);
            chars += length;
        }

        if (current.Count > 0)
        {
            chunks.Add(current);
        }

        return chunks;
    }

    private static async Task<TranslationBatchResult> ExecuteWithRetryAsync(
        ITranslationProvider provider,
        TranslationBatchRequest request,
        OrchestratorPolicy policy,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await provider.TranslateBatchAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (TranslationProviderException ex) when (
                ex.ErrorKind is TranslationErrorKind.RateLimit or TranslationErrorKind.Transient &&
                attempt < policy.MaxRetries)
            {
                var delay = TimeSpan.FromMilliseconds(policy.InitialBackoff.TotalMilliseconds * Math.Pow(2, attempt));
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(50, 150));
                await Task.Delay(delay + jitter, cancellationToken).ConfigureAwait(false);
                attempt++;
            }
        }
    }
}

