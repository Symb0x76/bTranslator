using System.Net;
using System.Text;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using bTranslator.Infrastructure.Translation.Options;
using bTranslator.Infrastructure.Translation.Providers;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace bTranslator.Tests.Unit.Translation;

public class ProviderAdaptersTests
{
    [Fact]
    public async Task AzureTranslatorProvider_ShouldTranslateBatch()
    {
        var handler = new CapturingHandler(_ => JsonResponse(
            """
            [
              { "translations": [ { "text": "你好" } ] },
              { "translations": [ { "text": "世界" } ] }
            ]
            """));
        using var httpClient = new HttpClient(handler);
        var provider = new AzureTranslatorProvider(
            httpClient,
            CreateOptions(AzureTranslatorProvider.DefaultProviderId, new ProviderEndpointOptions
            {
                ApiKey = "azure-key",
                Region = "eastasia",
                LanguageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["english"] = "en-US",
                    ["chinesesimplified"] = "zh-Hant"
                }
            }));

        var result = await provider.TranslateBatchAsync(new TranslationBatchRequest
        {
            SourceLanguage = "English",
            TargetLanguage = "Chinese (Simplified)",
            Items = ["Hello", "World"]
        });

        result.Items.Should().Equal("你好", "世界");
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Headers.Should().ContainKey("Ocp-Apim-Subscription-Key");
        handler.Requests[0].Url.Should().Contain("api-version=3.0");
        handler.Requests[0].Url.Should().Contain("from=en-US");
        handler.Requests[0].Url.Should().Contain("to=zh-Hant");
        handler.Requests[0].Body.Should().Contain("\"Text\":\"Hello\"");
    }

    [Fact]
    public async Task DeepLTranslationProvider_ShouldSendAuthHeaderAndParseResponse()
    {
        var handler = new CapturingHandler(_ => JsonResponse(
            """
            { "translations": [ { "text": "Bonjour" } ] }
            """));
        using var httpClient = new HttpClient(handler);
        var provider = new DeepLTranslationProvider(
            httpClient,
            CreateOptions(DeepLTranslationProvider.DefaultProviderId, new ProviderEndpointOptions
            {
                ApiKey = "deepl-key:fx"
            }));

        var result = await provider.TranslateBatchAsync(new TranslationBatchRequest
        {
            SourceLanguage = "en",
            TargetLanguage = "fr",
            Items = ["Hello"]
        });

        result.Items.Should().Equal("Bonjour");
        handler.Requests[0].Headers["Authorization"].Should().Contain("DeepL-Auth-Key");
        handler.Requests[0].Body.Should().Contain("source_lang=EN");
        handler.Requests[0].Body.Should().Contain("target_lang=FR");
    }

    [Fact]
    public async Task GoogleCloudTranslationProvider_ShouldDecodeHtmlEntities()
    {
        var handler = new CapturingHandler(_ => JsonResponse(
            """
            { "data": { "translations": [ { "translatedText": "A &amp; B" } ] } }
            """));
        using var httpClient = new HttpClient(handler);
        var provider = new GoogleCloudTranslationProvider(
            httpClient,
            CreateOptions(GoogleCloudTranslationProvider.DefaultProviderId, new ProviderEndpointOptions
            {
                ApiKey = "google-key"
            }));

        var result = await provider.TranslateBatchAsync(new TranslationBatchRequest
        {
            SourceLanguage = "en",
            TargetLanguage = "fr",
            Items = ["A & B"]
        });

        result.Items.Should().Equal("A & B");
        handler.Requests[0].Url.Should().Contain("key=google-key");
    }

    [Fact]
    public async Task BaiduTranslationProvider_ShouldMapAuthErrors()
    {
        var handler = new CapturingHandler(_ => JsonResponse(
            """
            { "error_code": "52003", "error_msg": "UNAUTHORIZED USER" }
            """));
        using var httpClient = new HttpClient(handler);
        var provider = new BaiduTranslationProvider(
            httpClient,
            CreateOptions(BaiduTranslationProvider.DefaultProviderId, new ProviderEndpointOptions
            {
                ApiKey = "appid",
                ApiSecret = "secret"
            }));

        var action = async () => await provider.TranslateBatchAsync(new TranslationBatchRequest
        {
            SourceLanguage = "en",
            TargetLanguage = "zh",
            Items = ["Hello"]
        });

        var exception = await action.Should().ThrowAsync<TranslationProviderException>();
        exception.Which.ErrorKind.Should().Be(TranslationErrorKind.Authentication);
    }

    [Fact]
    public async Task AnthropicTranslationProvider_ShouldParseTextBlock()
    {
        var handler = new CapturingHandler(_ => JsonResponse(
            """
            { "content": [ { "type": "text", "text": "翻译结果" } ] }
            """));
        using var httpClient = new HttpClient(handler);
        var provider = new AnthropicTranslationProvider(
            httpClient,
            CreateOptions(AnthropicTranslationProvider.DefaultProviderId, new ProviderEndpointOptions
            {
                ApiKey = "anthropic-key"
            }));

        var result = await provider.TranslateBatchAsync(new TranslationBatchRequest
        {
            SourceLanguage = "en",
            TargetLanguage = "zh",
            Items = ["Test"]
        });

        result.Items.Should().Equal("翻译结果");
        handler.Requests[0].Headers.Should().ContainKey("x-api-key");
    }

    [Fact]
    public async Task GeminiTranslationProvider_ShouldParseCandidates()
    {
        var handler = new CapturingHandler(_ => JsonResponse(
            """
            { "candidates": [ { "content": { "parts": [ { "text": "输出文本" } ] } } ] }
            """));
        using var httpClient = new HttpClient(handler);
        var provider = new GeminiTranslationProvider(
            httpClient,
            CreateOptions(GeminiTranslationProvider.DefaultProviderId, new ProviderEndpointOptions
            {
                ApiKey = "gemini-key",
                BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent",
                Model = "gemini-1.5-flash"
            }));

        var result = await provider.TranslateBatchAsync(new TranslationBatchRequest
        {
            SourceLanguage = "en",
            TargetLanguage = "zh",
            Items = ["abc"]
        });

        result.Items.Should().Equal("输出文本");
        handler.Requests[0].Url.Should().Contain("key=gemini-key");
        handler.Requests[0].Url.Should().Contain("gemini-1.5-flash");
    }

    [Fact]
    public async Task VolcengineTranslationProvider_ShouldParseOpenAiShapeResponse()
    {
        var handler = new CapturingHandler(_ => JsonResponse(
            """
            { "choices": [ { "message": { "content": "volc output" } } ] }
            """));
        using var httpClient = new HttpClient(handler);
        var provider = new VolcengineTranslationProvider(
            httpClient,
            CreateOptions(VolcengineTranslationProvider.DefaultProviderId, new ProviderEndpointOptions
            {
                ApiKey = "volc-key"
            }));

        var result = await provider.TranslateBatchAsync(new TranslationBatchRequest
        {
            SourceLanguage = "en",
            TargetLanguage = "zh",
            Items = ["abc"]
        });

        result.Items.Should().Equal("volc output");
        handler.Requests[0].Headers["Authorization"].Should().Contain("Bearer volc-key");
    }

    [Fact]
    public async Task TencentTranslationProvider_ShouldSignAndParseResponse()
    {
        var handler = new CapturingHandler(_ => JsonResponse(
            """
            { "Response": { "TargetText": "腾讯输出", "RequestId": "rid-1" } }
            """));
        using var httpClient = new HttpClient(handler);
        var provider = new TencentTranslationProvider(
            httpClient,
            CreateOptions(TencentTranslationProvider.DefaultProviderId, new ProviderEndpointOptions
            {
                ApiKey = "secret-id",
                ApiSecret = "secret-key",
                Region = "ap-guangzhou"
            }));

        var result = await provider.TranslateBatchAsync(new TranslationBatchRequest
        {
            SourceLanguage = "en",
            TargetLanguage = "zh",
            Items = ["abc"]
        });

        result.Items.Should().Equal("腾讯输出");
        handler.Requests[0].Headers["Authorization"].Should().StartWith("TC3-HMAC-SHA256");
        handler.Requests[0].Headers.Should().ContainKey("X-TC-Action");
    }

    private static IOptions<TranslationProviderOptions> CreateOptions(string providerId, ProviderEndpointOptions providerOptions)
    {
        return Options.Create(new TranslationProviderOptions
        {
            Providers = new Dictionary<string, ProviderEndpointOptions>(StringComparer.OrdinalIgnoreCase)
            {
                [providerId] = providerOptions
            }
        });
    }

    private static HttpResponseMessage JsonResponse(string payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private sealed class CapturingHandler(Func<CapturedRequest, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private readonly Func<CapturedRequest, HttpResponseMessage> _responseFactory = responseFactory;

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }

            string? body = null;
            if (request.Content is not null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers[header.Key] = string.Join(",", header.Value);
                }

                body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            var captured = new CapturedRequest(
                request.Method.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                headers,
                body);
            Requests.Add(captured);

            return _responseFactory(captured);
        }
    }

    private sealed record CapturedRequest(
        string Method,
        string Url,
        IReadOnlyDictionary<string, string> Headers,
        string? Body);
}
