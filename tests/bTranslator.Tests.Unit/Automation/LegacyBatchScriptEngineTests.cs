using bTranslator.Automation.Services;
using bTranslator.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace bTranslator.Tests.Unit.Automation;

public class LegacyBatchScriptEngineTests
{
    [Fact]
    public async Task ParseLegacyAndRun_ShouldApplyRule()
    {
        var content = """
                      StartRule
                      Search=(Medicine)
                      Replace=(Soins)
                      Pattern=%REPLACE% %ORIG%
                      mode=1
                      select=0
                      EndRule
                      """;

        var engine = new LegacyBatchScriptEngine(NullLogger<LegacyBatchScriptEngine>.Instance);
        var script = engine.ParseLegacy(content);

        var items = new List<TranslationItem>
        {
            new()
            {
                Id = "001",
                SourceText = "(Medicine) Super Stimpack",
                TranslatedText = ""
            }
        };

        var result = await engine.RunAsync(script, new BatchExecutionScope { Items = items });

        result.ChangedItems.Should().Be(1);
        items[0].TranslatedText.Should().Be("(Soins) (Medicine) Super Stimpack");
    }
}

