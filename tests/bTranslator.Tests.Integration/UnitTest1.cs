using System.Text;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Models;
using bTranslator.Infrastructure.Bethesda.Services;
using FluentAssertions;

namespace bTranslator.Tests.Integration;

public class StringsCodecRoundTripTests
{
    [Theory]
    [InlineData(StringsFileKind.Strings)]
    [InlineData(StringsFileKind.DlStrings)]
    [InlineData(StringsFileKind.IlStrings)]
    public async Task RoundTrip_ShouldKeepEntries(StringsFileKind kind)
    {
        var codec = new BethesdaStringsCodec();
        var tempFile = Path.Combine(Path.GetTempPath(), $"bTranslator-{Guid.NewGuid():N}.bin");
        var entries = new[]
        {
            new StringsEntry { Id = 1, Text = "Hello" },
            new StringsEntry { Id = 2, Text = "World <Alias=Hero> 42" }
        };

        await codec.WriteAsync(tempFile, kind, entries, Encoding.UTF8);
        var loaded = await codec.ReadAsync(tempFile, kind, Encoding.UTF8);

        loaded.Select(static x => x.Id).Should().Equal(1u, 2u);
        loaded.Select(static x => x.Text).Should().Equal("Hello", "World <Alias=Hero> 42");
    }
}

