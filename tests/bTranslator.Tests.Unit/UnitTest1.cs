using bTranslator.Infrastructure.Translation.Services;
using FluentAssertions;

namespace bTranslator.Tests.Unit;

public class PlaceholderProtectorTests
{
    [Fact]
    public void ProtectAndRestore_ShouldRoundTripTagsNumbersAndLineBreaks()
    {
        var protector = new TagNumberPlaceholderProtector();
        var input = "Hi <Alias=Hero>, score=42\nLine2";

        var protectedText = protector.Protect(input);
        var restored = protector.Restore(protectedText.Text, protectedText.Map);

        restored.Should().Be(input);
        protectedText.Text.Should().NotBe(input);
    }
}

