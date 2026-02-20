using System.Text;
using bTranslator.Domain.Enums;
using bTranslator.Infrastructure.Bethesda.Models;

namespace bTranslator.Infrastructure.Bethesda.Services;

public sealed class RecordDefinitionCatalog
{
    private static readonly Dictionary<GameKind, string> FileNames = new()
    {
        [GameKind.Skyrim] = "Skyrim._recorddefs.txt",
        [GameKind.SkyrimSe] = "SkyrimSE._recorddefs.txt",
        [GameKind.Fallout4] = "Fallout4._recorddefs.txt",
        [GameKind.FalloutNv] = "FalloutNV._recorddefs.txt",
        [GameKind.Fallout76] = "Fallout76._recorddefs.txt",
        [GameKind.Starfield] = "Starfield._recorddefs.txt"
    };

    internal IReadOnlyList<RecordDefinitionRule> Load(GameKind game, string? explicitPath)
    {
        var path = ResolvePath(game, explicitPath);
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var rules = new List<RecordDefinitionRule>(lines.Length);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (!line.StartsWith("Def_:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line["Def_:".Length..];
            var sections = payload.Split('=', 3, StringSplitOptions.TrimEntries);
            if (sections.Length != 3)
            {
                continue;
            }

            var fieldSignature = NormalizeSignature(sections[0]);
            var recordSignature = NormalizeSignature(sections[1]);
            var options = sections[2];
            if (options.Length == 0 || options[0] is < '0' or > '2')
            {
                continue;
            }

            var listIndex = (byte)(options[0] - '0');
            var flagAndProc = options[1..];
            var processor = string.Empty;
            var dashIndex = flagAndProc.IndexOf("-", StringComparison.Ordinal);
            if (dashIndex >= 0)
            {
                processor = flagAndProc[(dashIndex + 1)..];
                flagAndProc = flagAndProc[..dashIndex];
            }

            rules.Add(new RecordDefinitionRule
            {
                FieldSignature = fieldSignature,
                RecordSignature = recordSignature,
                ListIndex = listIndex,
                NotNull = flagAndProc.Contains('*'),
                NoZero = flagAndProc.Contains('!'),
                Ignored = flagAndProc.Contains('?'),
                Processor = processor
            });
        }

        return rules;
    }

    private static string ResolvePath(GameKind game, string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        var fileName = FileNames[game];
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Defaults", "RecordDefs", fileName);
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        var repoPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "src",
            "bTranslator.Infrastructure.Bethesda",
            "Defaults",
            "RecordDefs",
            fileName));

        if (File.Exists(repoPath))
        {
            return repoPath;
        }

        throw new FileNotFoundException(
            $"Cannot locate record definition file for {game}.",
            fileName);
    }

    private static string NormalizeSignature(string value)
    {
        if (value.Length >= 4)
        {
            return value[..4];
        }

        return value.PadRight(4, '_');
    }
}

