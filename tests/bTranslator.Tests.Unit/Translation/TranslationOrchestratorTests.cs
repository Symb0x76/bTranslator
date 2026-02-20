using bTranslator.Application.Abstractions;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Exceptions;
using bTranslator.Domain.Models;
using bTranslator.Infrastructure.Translation.Services;
using bTranslator.Infrastructure.Translation.Testing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace bTranslator.Tests.Unit.Translation;

public class TranslationOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldFallbackToSecondProvider()
    {
        var providers = new ITranslationProvider[]
        {
            new FailingProvider("broken"),
            new EchoTranslationProvider("echo")
        };
        var orchestrator = new TranslationOrchestrator(
            providers,
            new TagNumberPlaceholderProtector(),
            NullLogger<TranslationOrchestrator>.Instance);

        var result = await orchestrator.ExecuteAsync(new TranslationJob
        {
            SourceLanguage = "en",
            TargetLanguage = "fr",
            ProviderChain = ["broken", "echo"],
            Items =
            [
                new TranslationItem { Id = "1", SourceText = "Hello" }
            ]
        });

        result.ProviderId.Should().Be("echo");
        result.Items.Single().TranslatedText.Should().Be("[fr] Hello");
    }

    private sealed class FailingProvider : ITranslationProvider
    {
        public FailingProvider(string providerId)
        {
            ProviderId = providerId;
        }

        public string ProviderId { get; }
        public ProviderCapabilities Capabilities { get; } = new();

        public Task<TranslationBatchResult> TranslateBatchAsync(
            TranslationBatchRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new TranslationProviderException(
                ProviderId,
                TranslationErrorKind.Transient,
                "fail");
        }
    }
}

