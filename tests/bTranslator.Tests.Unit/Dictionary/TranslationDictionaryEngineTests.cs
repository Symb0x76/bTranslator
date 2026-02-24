using bTranslator.Domain.Models;
using bTranslator.Domain.Services;
using FluentAssertions;

namespace bTranslator.Tests.Unit.Dictionary;

public class TranslationDictionaryEngineTests
{
    [Fact]
    public void PrepareSource_ShouldRespectWildcardScopeMatch()
    {
        var entries = new List<TranslationDictionaryEntry>
        {
            new()
            {
                Source = "Hero",
                Target = "英雄",
                EditorIdPattern = "QUST:*",
                FieldPattern = "FULL"
            }
        };

        var matched = TranslationDictionaryEngine.PrepareSource(
            "Hero arrives",
            "QUST:00535DBC",
            "FULL",
            entries,
            enabled: true);

        matched.Replacements.Should().HaveCount(1);
        matched.PreparedSource.Should().Contain("<BT_DICT_");
        TranslationDictionaryEngine.RestoreTokens(matched.PreparedSource, matched.Replacements)
            .Should().Be("英雄 arrives");

        var notMatched = TranslationDictionaryEngine.PrepareSource(
            "Hero arrives",
            "NPC_:00112233",
            "FULL",
            entries,
            enabled: true);

        notMatched.Replacements.Should().BeEmpty();
        notMatched.PreparedSource.Should().Be("Hero arrives");
    }

    [Fact]
    public void PrepareSource_ShouldRespectWholeWordMode()
    {
        var entries = new List<TranslationDictionaryEntry>
        {
            new()
            {
                Source = "cat",
                Target = "猫",
                WholeWord = true,
                MatchCase = false
            }
        };

        var result = TranslationDictionaryEngine.PrepareSource(
            "cat scatter concatenate CAT",
            "",
            "",
            entries,
            enabled: true);

        var restored = TranslationDictionaryEngine.RestoreTokens(result.PreparedSource, result.Replacements);

        restored.Should().Be("猫 scatter concatenate 猫");
    }

    [Fact]
    public void PrepareSource_ShouldRespectMatchCaseMode()
    {
        var entries = new List<TranslationDictionaryEntry>
        {
            new()
            {
                Source = "PowerArmor",
                Target = "动力装甲",
                MatchCase = true
            }
        };

        var result = TranslationDictionaryEngine.PrepareSource(
            "PowerArmor powerarmor POWERARMOR",
            "",
            "",
            entries,
            enabled: true);

        result.Replacements.Should().HaveCount(1);

        var restored = TranslationDictionaryEngine.RestoreTokens(result.PreparedSource, result.Replacements);
        restored.Should().Be("动力装甲 powerarmor POWERARMOR");
    }

    [Fact]
    public void NormalizeEntries_ShouldTrimAndDedupeEntries()
    {
        var normalized = TranslationDictionaryEngine.NormalizeEntries(
        [
            new TranslationDictionaryEntry
            {
                Source = " Damage ",
                Target = " 伤害 ",
                EditorIdPattern = " QUST:* ",
                FieldPattern = " FULL "
            },
            new TranslationDictionaryEntry
            {
                Source = "Damage",
                Target = "伤害",
                EditorIdPattern = "QUST:*",
                FieldPattern = "FULL"
            },
            new TranslationDictionaryEntry
            {
                Source = "Damage",
                Target = "伤害",
                EditorIdPattern = "QUST:*",
                FieldPattern = "FULL",
                MatchCase = true
            },
            new TranslationDictionaryEntry
            {
                Source = "",
                Target = "invalid"
            }
        ]);

        normalized.Should().HaveCount(2);
        normalized[0].Source.Should().Be("Damage");
        normalized[0].Target.Should().Be("伤害");
        normalized[0].EditorIdPattern.Should().Be("QUST:*");
        normalized[0].FieldPattern.Should().Be("FULL");
        normalized[1].MatchCase.Should().BeTrue();
    }

    [Fact]
    public void SerializeAndDeserialize_ShouldKeepNormalizedEntries()
    {
        var content = TranslationDictionaryEngine.SerializeEntries(
        [
            new TranslationDictionaryEntry
            {
                Source = "Potion",
                Target = "药水",
                EditorIdPattern = "ALCH:*",
                WholeWord = true
            },
            new TranslationDictionaryEntry
            {
                Source = "Potion",
                Target = "药水",
                EditorIdPattern = "ALCH:*",
                WholeWord = true
            }
        ]);

        var restored = TranslationDictionaryEngine.DeserializeEntries(content);

        restored.Should().HaveCount(1);
        restored[0].Source.Should().Be("Potion");
        restored[0].WholeWord.Should().BeTrue();
    }

    [Fact]
    public void DeserializeEntries_ShouldAcceptArrayPayload()
    {
        const string content = """
[
  {
    "source": "Laser",
    "target": "镭射",
    "editorIdPattern": "WEAP:*",
    "fieldPattern": "FULL"
  },
  {
    "source": "Laser",
    "target": "镭射",
    "editorIdPattern": "WEAP:*",
    "fieldPattern": "FULL"
  }
]
""";

        var restored = TranslationDictionaryEngine.DeserializeEntries(content);

        restored.Should().HaveCount(1);
        restored[0].Source.Should().Be("Laser");
        restored[0].Target.Should().Be("镭射");
        restored[0].EditorIdPattern.Should().Be("WEAP:*");
    }

    [Fact]
    public void ParseDelimitedEntries_ShouldParseCsvWithHeaderAndFlags()
    {
        const string content = """
source,target,editorIdPattern,fieldPattern,matchCase,wholeWord
Power Armor,动力装甲,ARMO:*,FULL,1,0
"Laser, Rifle",镭射步枪,WEAP:*,DESC,true,true
""";

        var entries = TranslationDictionaryEngine.ParseDelimitedEntries(content);

        entries.Should().HaveCount(2);
        entries[0].Source.Should().Be("Power Armor");
        entries[0].Target.Should().Be("动力装甲");
        entries[0].EditorIdPattern.Should().Be("ARMO:*");
        entries[0].FieldPattern.Should().Be("FULL");
        entries[0].MatchCase.Should().BeTrue();
        entries[0].WholeWord.Should().BeFalse();

        entries[1].Source.Should().Be("Laser, Rifle");
        entries[1].WholeWord.Should().BeTrue();
    }

    [Fact]
    public void ParseDelimitedEntries_ShouldParseTsvAndDedupe()
    {
        const string content = """
Source	Target	EditorIdPattern	FieldPattern	MatchCase	WholeWord
Damage	伤害	QUST:*	FULL	0	1
Damage	伤害	QUST:*	FULL	0	1
""";

        var entries = TranslationDictionaryEngine.ParseDelimitedEntries(content);

        entries.Should().HaveCount(1);
        entries[0].Source.Should().Be("Damage");
        entries[0].Target.Should().Be("伤害");
        entries[0].EditorIdPattern.Should().Be("QUST:*");
        entries[0].FieldPattern.Should().Be("FULL");
        entries[0].MatchCase.Should().BeFalse();
        entries[0].WholeWord.Should().BeTrue();
    }
}
