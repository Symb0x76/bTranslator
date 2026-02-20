using System.Text.RegularExpressions;
using bTranslator.Application.Abstractions;
using bTranslator.Domain.Models;

namespace bTranslator.Infrastructure.Translation.Services;

public sealed class TagNumberPlaceholderProtector : IPlaceholderProtector
{
    private static readonly Regex TagRegex = new(@"<[^>\r\n]+>", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"(?<!\w)[+-]?\d+([.,]\d+)?(?!\w)", RegexOptions.Compiled);
    private static readonly Regex NewLineRegex = new(@"\r\n|\n", RegexOptions.Compiled);

    public ProtectedText Protect(string input)
    {
        var map = new PlaceholderMap();
        var index = 0;
        var stage = ReplaceTokens(input, TagRegex, "BT_TAG", map, index);
        stage = ReplaceTokens(stage.Text, NumberRegex, "BT_NUM", map, stage.NextIndex);
        stage = ReplaceTokens(stage.Text, NewLineRegex, "BT_LF", map, stage.NextIndex);

        return new ProtectedText
        {
            Text = stage.Text,
            Map = map
        };
    }

    public string Restore(string input, PlaceholderMap map)
    {
        var output = input;
        foreach (var kvp in map.Tokens.OrderByDescending(static x => x.Key.Length))
        {
            output = output.Replace(kvp.Key, kvp.Value, StringComparison.Ordinal);
        }

        return output;
    }

    private static (string Text, int NextIndex) ReplaceTokens(
        string input,
        Regex regex,
        string prefix,
        PlaceholderMap map,
        int startIndex)
    {
        var currentIndex = startIndex;
        var output = regex.Replace(input, match =>
        {
            var token = $"__{prefix}_{currentIndex++}__";
            map.Tokens[token] = match.Value;
            return token;
        });

        return (output, currentIndex);
    }
}

