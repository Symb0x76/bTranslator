using System.Text.RegularExpressions;
using bTranslator.Application.Abstractions;
using bTranslator.Automation.Models;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Models;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace bTranslator.Automation.Services;

public sealed class LegacyBatchScriptEngine : IBatchScriptEngine
{
    private static readonly Regex RuleStartRegex = new(@"^\s*StartRule\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RuleEndRegex = new(@"^\s*EndRule\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly ILogger<LegacyBatchScriptEngine> _logger;
    private readonly IDeserializer _yamlDeserializer;

    public LegacyBatchScriptEngine(ILogger<LegacyBatchScriptEngine> logger)
    {
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    public BatchScript ParseLegacy(string content)
    {
        var lines = SplitLines(content);
        var rules = new List<LegacyBatchRule>();
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inRule = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (RuleStartRegex.IsMatch(line))
            {
                inRule = true;
                current.Clear();
                continue;
            }

            if (RuleEndRegex.IsMatch(line))
            {
                if (!inRule)
                {
                    continue;
                }

                rules.Add(CreateRule(current));
                inRule = false;
                current.Clear();
                continue;
            }

            if (!inRule)
            {
                continue;
            }

            var idx = line.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            current[key] = value;
        }

        return new BatchScript
        {
            Name = "LegacyBatch",
            Rules = rules
        };
    }

    public BatchScript ParseV2(string yamlContent)
    {
        var v2 = _yamlDeserializer.Deserialize<BatchV2Document>(yamlContent) ?? new BatchV2Document();
        var rules = v2.Rules.Select(rule => new LegacyBatchRule
        {
            Search = rule.Search,
            Replace = rule.Replace,
            Pattern = rule.Pattern,
            Mode = Enum.IsDefined(typeof(BatchRuleMode), rule.Mode) ? (BatchRuleMode)rule.Mode : BatchRuleMode.SourceDrivenReplace,
            SelectionMode = Enum.IsDefined(typeof(BatchSelectionMode), rule.Select) ? (BatchSelectionMode)rule.Select : BatchSelectionMode.All,
            CaseSensitive = rule.CaseSensitive,
            AllLists = rule.AllLists
        }).ToList();

        return new BatchScript
        {
            Name = v2.Name,
            Rules = rules
        };
    }

    public Task<BatchExecutionResult> RunAsync(
        BatchScript script,
        BatchExecutionScope scope,
        CancellationToken cancellationToken = default)
    {
        var changed = 0;
        var logs = new List<string>();

        foreach (var rule in script.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var item in scope.Items)
            {
                if (item.IsLocked)
                {
                    continue;
                }

                var original = item.TranslatedText ?? string.Empty;
                var updated = ApplyRule(rule, item);
                if (updated == original)
                {
                    continue;
                }

                changed++;
                logs.Add($"{item.Id}: rule '{rule.Search}' applied");
                if (!scope.DryRun)
                {
                    item.TranslatedText = updated;
                }
            }
        }

        _logger.LogInformation("Batch script '{ScriptName}' finished. Changed={Changed}", script.Name, changed);
        return Task.FromResult(new BatchExecutionResult
        {
            ChangedItems = changed,
            Logs = logs
        });
    }

    private static IReadOnlyList<string> SplitLines(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }

    private static LegacyBatchRule CreateRule(IReadOnlyDictionary<string, string> values)
    {
        var mode = ParseInt(values, "mode", 1);
        var select = ParseInt(values, "select", 0);

        return new LegacyBatchRule
        {
            Search = Get(values, "Search"),
            Replace = Get(values, "Replace"),
            Pattern = Get(values, "Pattern", "%REPLACE% %ORIG%"),
            Mode = Enum.IsDefined(typeof(BatchRuleMode), mode) ? (BatchRuleMode)mode : BatchRuleMode.SourceDrivenReplace,
            SelectionMode = Enum.IsDefined(typeof(BatchSelectionMode), select) ? (BatchSelectionMode)select : BatchSelectionMode.All,
            CaseSensitive = ParseBool(values, "casesensitive"),
            AllLists = ParseBool(values, "alllists")
        };
    }

    private static string ApplyRule(LegacyBatchRule rule, TranslationItem item)
    {
        var translated = item.TranslatedText ?? string.Empty;
        var comparison = rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var hasSearch = translated.Contains(rule.Search, comparison);
        var sourceHasSearch = item.SourceText.Contains(rule.Search, comparison);
        var pattern = rule.Pattern
            .Replace("%REPLACE%", rule.Replace, StringComparison.Ordinal)
            .Replace("%ORIG%", item.SourceText, StringComparison.Ordinal);

        return rule.Mode switch
        {
            BatchRuleMode.ReplaceTranslation when hasSearch => Replace(translated, rule.Search, rule.Replace, rule.CaseSensitive),
            BatchRuleMode.SourceDrivenReplace when sourceHasSearch => pattern,
            BatchRuleMode.PatternOnly => pattern,
            _ => translated
        };
    }

    private static string Replace(string input, string search, string replace, bool caseSensitive)
    {
        if (caseSensitive)
        {
            return input.Replace(search, replace, StringComparison.Ordinal);
        }

        var regex = Regex.Escape(search);
        return Regex.Replace(input, regex, replace, RegexOptions.IgnoreCase);
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
    }

    private static int ParseInt(IReadOnlyDictionary<string, string> values, string key, int fallback)
    {
        return values.TryGetValue(key, out var value) && int.TryParse(value, out var result) ? result : fallback;
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key, string fallback = "")
    {
        return values.TryGetValue(key, out var value) ? value : fallback;
    }
}

