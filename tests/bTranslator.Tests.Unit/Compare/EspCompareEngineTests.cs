using bTranslator.Domain.Models.Compare;
using bTranslator.Domain.Services.Compare;
using FluentAssertions;

namespace bTranslator.Tests.Unit.Compare;

public class EspCompareEngineTests
{
    [Fact]
    public void Compare_ShouldGenerateCandidates_WhenSourceMatchesAndTranslationDiffers()
    {
        EspCompareRow[] currentRows =
        [
            new EspCompareRow
            {
                StableKey = "recid:WEAP:001:FULL:0",
                EditorId = "WEAP:00000100",
                FieldSignature = "FULL",
                ListKind = "RECORD",
                SourceText = "Laser Rifle",
                TranslatedText = "激光步枪"
            }
        ];

        EspCompareRow[] incomingRows =
        [
            new EspCompareRow
            {
                StableKey = "recid:WEAP:001:FULL:0",
                EditorId = "WEAP:00000100",
                FieldSignature = "FULL",
                ListKind = "RECORD",
                SourceText = "Laser Rifle",
                TranslatedText = "镭射步枪"
            }
        ];

        var result = EspCompareEngine.Compare(currentRows, incomingRows);

        result.MatchedRowCount.Should().Be(1);
        result.TranslationDifferentCount.Should().Be(1);
        result.Candidates.Should().ContainSingle();
        result.Candidates[0].IncomingTranslatedText.Should().Be("镭射步枪");
    }

    [Fact]
    public void Compare_ShouldNotGenerateCandidate_WhenSourceTextChanged()
    {
        EspCompareRow[] currentRows =
        [
            new EspCompareRow
            {
                StableKey = "str:Strings:00000010",
                EditorId = "00000010",
                FieldSignature = "STRINGS",
                ListKind = "STRINGS",
                SourceText = "Open",
                TranslatedText = "打开"
            }
        ];

        EspCompareRow[] incomingRows =
        [
            new EspCompareRow
            {
                StableKey = "str:Strings:00000010",
                EditorId = "00000010",
                FieldSignature = "STRINGS",
                ListKind = "STRINGS",
                SourceText = "Open Door",
                TranslatedText = "打开门"
            }
        ];

        var result = EspCompareEngine.Compare(currentRows, incomingRows);

        result.SourceDifferentCount.Should().Be(1);
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public void Compare_ShouldCountMissingRowsInBothDirections()
    {
        EspCompareRow[] currentRows =
        [
            new EspCompareRow
            {
                StableKey = "a",
                EditorId = "A",
                FieldSignature = "FULL",
                ListKind = "RECORD",
                SourceText = "a",
                TranslatedText = "a"
            },
            new EspCompareRow
            {
                StableKey = "b",
                EditorId = "B",
                FieldSignature = "FULL",
                ListKind = "RECORD",
                SourceText = "b",
                TranslatedText = "b"
            }
        ];

        EspCompareRow[] incomingRows =
        [
            new EspCompareRow
            {
                StableKey = "a",
                EditorId = "A",
                FieldSignature = "FULL",
                ListKind = "RECORD",
                SourceText = "a",
                TranslatedText = "a2"
            },
            new EspCompareRow
            {
                StableKey = "c",
                EditorId = "C",
                FieldSignature = "FULL",
                ListKind = "RECORD",
                SourceText = "c",
                TranslatedText = "c"
            }
        ];

        var result = EspCompareEngine.Compare(currentRows, incomingRows);

        result.MissingInCompareCount.Should().Be(1);
        result.MissingInBaseCount.Should().Be(1);
        result.MatchedRowCount.Should().Be(1);
    }
}
