using System.Text.Json;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Translation.Providers;

public sealed class AnthropicTranslationProvider : BaseJsonHttpTranslationProvider
{
    public const string DefaultProviderId = "anthropic";

    public AnthropicTranslationProvider(
        HttpClient httpClient,
        IOptions<bTranslator.Infrastructure.Translation.Options.TranslationProviderOptions> options)
        : base(DefaultProviderId, httpClient, options)
    {
    }

    protected override void AddAuthentication(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(Options.ApiKey))
        {
            throw new TranslationProviderException(
                ProviderId,
                TranslationErrorKind.Authentication,
                $"Provider '{ProviderId}' requires ApiKey.");
        }

        request.Headers.TryAddWithoutValidation("x-api-key", Options.ApiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
    }

    public override async Task<TranslationBatchResult> TranslateBatchAsync(
        TranslationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var endpoint = Options.BaseUrl?.TrimEnd('/') ?? "https://api.anthropic.com/v1/messages";
        var model = Options.Model ?? "claude-3-5-haiku-latest";
        var prompt = ProviderTextUtilities.BuildPrompt(
            request,
            Options.PromptTemplate,
            "Translate to %lang_dest%. Keep all tags, numbers and line breaks exactly as is.");

        var translated = new List<string>(request.Items.Count);
        foreach (var item in request.Items)
        {
            var payload = new
            {
                model,
                max_tokens = 1024,
                temperature = 0,
                system = "You are a translation engine for Bethesda game mods.",
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = ProviderTextUtilities.BuildTextPrompt(prompt, item)
                    }
                }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonBody(payload)
            };
            AddAuthentication(httpRequest);

            try
            {
                using var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw ToProviderException(ProviderId, response, content);
                }

                translated.Add(ParseResponse(content));
            }
            catch (TranslationProviderException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw ToTransient(ProviderId, ex);
            }
        }

        return new TranslationBatchResult
        {
            Items = translated
        };
    }

    private static string ParseResponse(string content)
    {
        using var doc = JsonDocument.Parse(content);
        if (!doc.RootElement.TryGetProperty("content", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            throw new TranslationProviderException(
                DefaultProviderId,
                TranslationErrorKind.Validation,
                "Anthropic response missing content array.");
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.TryGetProperty("type", out var type) &&
                string.Equals(type.GetString(), "text", StringComparison.OrdinalIgnoreCase) &&
                entry.TryGetProperty("text", out var textValue))
            {
                return textValue.GetString() ?? string.Empty;
            }
        }

        throw new TranslationProviderException(
            DefaultProviderId,
            TranslationErrorKind.Validation,
            "Anthropic response missing content text block.");
    }
}
