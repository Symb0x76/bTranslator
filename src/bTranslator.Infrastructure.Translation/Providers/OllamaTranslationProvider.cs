using System.Text.Json;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Translation.Providers;

public sealed class OllamaTranslationProvider : BaseJsonHttpTranslationProvider
{
    public const string DefaultProviderId = "ollama";

    public OllamaTranslationProvider(
        HttpClient httpClient,
        IOptions<bTranslator.Infrastructure.Translation.Options.TranslationProviderOptions> options)
        : base(DefaultProviderId, httpClient, options)
    {
    }

    protected override void AddAuthentication(HttpRequestMessage request)
    {
        // Ollama commonly runs locally without auth.
    }

    public override async Task<TranslationBatchResult> TranslateBatchAsync(
        TranslationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = Options.BaseUrl?.TrimEnd('/') ?? "http://localhost:11434/api/chat";
        var model = Options.Model ?? "qwen2.5:7b";
        var prompt = Options.PromptTemplate ??
                     "Translate to %lang_dest%. Keep tags, numbers and line breaks untouched.";

        var output = new List<string>(request.Items.Count);
        foreach (var text in request.Items)
        {
            var payload = new
            {
                model,
                stream = false,
                messages = new object[]
                {
                    new { role = "system", content = "You are a deterministic translation engine." },
                    new
                    {
                        role = "user",
                        content = prompt
                            .Replace("%lang_source%", request.SourceLanguage, StringComparison.OrdinalIgnoreCase)
                            .Replace("%lang_dest%", request.TargetLanguage, StringComparison.OrdinalIgnoreCase)
                            + "\n\n" + text
                    }
                }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = JsonBody(payload)
            };

            try
            {
                using var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw ToProviderException(ProviderId, response, content);
                }

                output.Add(ParseResponse(content));
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
            Items = output
        };
    }

    private static string ParseResponse(string content)
    {
        using var doc = JsonDocument.Parse(content);
        if (doc.RootElement.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var value))
        {
            return value.GetString() ?? string.Empty;
        }

        throw new TranslationProviderException(
            DefaultProviderId,
            Domain.Enums.TranslationErrorKind.Validation,
            "Ollama response missing message.content.");
    }
}

