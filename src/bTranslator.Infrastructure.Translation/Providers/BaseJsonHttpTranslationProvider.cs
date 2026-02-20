using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using bTranslator.Application.Abstractions;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using bTranslator.Infrastructure.Translation.Options;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Translation.Providers;

public abstract class BaseJsonHttpTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _providerId;
    private readonly TranslationProviderOptions _rootOptions;

    protected BaseJsonHttpTranslationProvider(
        string providerId,
        HttpClient httpClient,
        IOptions<TranslationProviderOptions> options)
    {
        _providerId = providerId;
        _httpClient = httpClient;
        _rootOptions = options.Value;
    }

    public string ProviderId => _providerId;
    public ProviderCapabilities Capabilities => Options.Capabilities;

    protected ProviderEndpointOptions Options =>
        _rootOptions.Providers.TryGetValue(_providerId, out var provider)
            ? provider
            : new ProviderEndpointOptions();
    protected HttpClient HttpClient => _httpClient;

    protected virtual void AddAuthentication(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(Options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.ApiKey);
        }
    }

    protected static StringContent JsonBody(object payload)
    {
        return new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
    }

    protected static TranslationProviderException ToProviderException(string providerId, HttpResponseMessage response, string? content)
    {
        var code = (int)response.StatusCode;
        var kind = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => TranslationErrorKind.Authentication,
            (System.Net.HttpStatusCode)429 => TranslationErrorKind.RateLimit,
            >= System.Net.HttpStatusCode.InternalServerError => TranslationErrorKind.Transient,
            _ => TranslationErrorKind.Fatal
        };

        return new TranslationProviderException(
            providerId,
            kind,
            $"Provider '{providerId}' returned {code}: {content}");
    }

    protected static TranslationProviderException ToTransient(string providerId, Exception ex)
    {
        return new TranslationProviderException(
            providerId,
            TranslationErrorKind.Transient,
            $"Provider '{providerId}' transient error: {ex.Message}",
            ex);
    }

    public abstract Task<TranslationBatchResult> TranslateBatchAsync(
        TranslationBatchRequest request,
        CancellationToken cancellationToken = default);
}

