using System.Text;
using bTranslator.Domain.Models;
using bTranslator.Infrastructure.Bethesda.Models;

namespace bTranslator.Infrastructure.Bethesda.Services;

public sealed class PluginRecordMapper
{
    static PluginRecordMapper()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    internal IList<TranslationItem> ExtractRecordItems(
        PluginBinaryDocument document,
        IReadOnlyList<RecordDefinitionRule> rules,
        Encoding encoding)
    {
        var result = new List<TranslationItem>();
        foreach (var record in document.EnumerateRecords())
        {
            if (!record.CanRewriteSubrecords)
            {
                continue;
            }

            for (var index = 0; index < record.Subrecords.Count; index++)
            {
                var subrecord = record.Subrecords[index];
                var rule = ResolveRule(rules, record.Signature, subrecord.Signature, record.Subrecords, index);
                if (rule is null || rule.Ignored)
                {
                    continue;
                }

                var hasTerminator = !rule.NoZero && Array.IndexOf(subrecord.Data, (byte)0) >= 0;
                var source = DecodeFieldText(subrecord.Data, encoding, rule.NoZero);
                if (source.Length == 0 && !rule.NotNull)
                {
                    continue;
                }

                var metadata = new PluginFieldMetadata
                {
                    FormId = record.FormId,
                    RecordSignature = record.Signature,
                    FieldSignature = subrecord.Signature,
                    FieldIndex = index,
                    ListIndex = rule.ListIndex,
                    NotNull = rule.NotNull,
                    NoZero = rule.NoZero,
                    Ignored = rule.Ignored,
                    HasTerminator = hasTerminator,
                    Processor = rule.Processor
                };

                result.Add(new TranslationItem
                {
                    Id = metadata.BuildKey(),
                    SourceText = source,
                    TranslatedText = source,
                    PluginFieldMetadata = metadata
                });
            }
        }

        return result;
    }

    internal void ApplyRecordItems(
        PluginBinaryDocument document,
        IEnumerable<TranslationItem> items,
        Encoding encoding)
    {
        var itemMap = items
            .Where(static item => item.PluginFieldMetadata is not null)
            .ToDictionary(
                static item => item.PluginFieldMetadata!.BuildKey(),
                static item => item,
                StringComparer.Ordinal);

        foreach (var record in document.EnumerateRecords())
        {
            if (!record.CanRewriteSubrecords)
            {
                continue;
            }

            for (var index = 0; index < record.Subrecords.Count; index++)
            {
                var subrecord = record.Subrecords[index];
                var key = BuildKey(record.Signature, record.FormId, subrecord.Signature, index);
                if (!itemMap.TryGetValue(key, out var item))
                {
                    continue;
                }

                var metadata = item.PluginFieldMetadata!;
                var text = item.TranslatedText ?? item.SourceText;
                if (metadata.NotNull && string.IsNullOrEmpty(text))
                {
                    text = " ";
                }

                subrecord.Data = EncodeFieldText(
                    text,
                    encoding,
                    metadata.NoZero,
                    metadata.HasTerminator);
            }
        }
    }

    private static RecordDefinitionRule? ResolveRule(
        IReadOnlyList<RecordDefinitionRule> rules,
        string recordSignature,
        string fieldSignature,
        IList<PluginSubrecord> subrecords,
        int fieldIndex)
    {
        foreach (var rule in rules)
        {
            if (!string.Equals(rule.FieldSignature, fieldSignature, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(rule.RecordSignature, "****", StringComparison.Ordinal) &&
                !string.Equals(rule.RecordSignature, recordSignature, StringComparison.Ordinal))
            {
                continue;
            }

            if (!EvaluateProcessor(rule.Processor, subrecords, fieldIndex))
            {
                continue;
            }

            return rule;
        }

        return null;
    }

    private static bool EvaluateProcessor(string processor, IList<PluginSubrecord> subrecords, int index)
    {
        if (string.IsNullOrWhiteSpace(processor))
        {
            return true;
        }

        return processor switch
        {
            "proc1" => EvaluateProc1Gmst(subrecords),
            "proc2" => EvaluateProc2PerkEpfd(subrecords, index),
            "proc3" => EvaluateProc3Note(subrecords),
            "proc4" => EvaluateProc4PerkEpf2(subrecords, index),
            "proc5" => EvaluateProc5DoorCnam(subrecords, index),
            _ => true
        };
    }

    private static bool EvaluateProc1Gmst(IList<PluginSubrecord> subrecords)
    {
        foreach (var subrecord in subrecords)
        {
            if (!string.Equals(subrecord.Signature, "EDID", StringComparison.Ordinal))
            {
                continue;
            }

            return subrecord.Data.Length > 0 && subrecord.Data[0] == (byte)'s';
        }

        return false;
    }

    private static bool EvaluateProc2PerkEpfd(IList<PluginSubrecord> subrecords, int index)
    {
        for (var i = 0; i < subrecords.Count; i++)
        {
            if (!string.Equals(subrecords[i].Signature, "EPFT", StringComparison.Ordinal))
            {
                continue;
            }

            if (subrecords[i].Data.Length != 1 || subrecords[i].Data[0] != 7)
            {
                continue;
            }

            if (index >= i + 1 && index <= Math.Min(subrecords.Count - 1, i + 3))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EvaluateProc4PerkEpf2(IList<PluginSubrecord> subrecords, int index)
    {
        for (var i = 0; i < subrecords.Count; i++)
        {
            if (!string.Equals(subrecords[i].Signature, "EPFT", StringComparison.Ordinal))
            {
                continue;
            }

            if (subrecords[i].Data.Length != 1 || subrecords[i].Data[0] != 4)
            {
                continue;
            }

            if (index >= i + 1 && index <= Math.Min(subrecords.Count - 1, i + 2))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EvaluateProc3Note(IList<PluginSubrecord> subrecords)
    {
        foreach (var subrecord in subrecords)
        {
            if (!string.Equals(subrecord.Signature, "DATA", StringComparison.Ordinal))
            {
                continue;
            }

            if (subrecord.Data.Length == 1 && subrecord.Data[0] != 3)
            {
                return true;
            }
        }

        return false;
    }

    private static bool EvaluateProc5DoorCnam(IList<PluginSubrecord> subrecords, int index)
    {
        for (var i = index; i >= Math.Max(0, index - 3); i--)
        {
            if (string.Equals(subrecords[i].Signature, "BFCB", StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(subrecords[i].Signature, "BFCE", StringComparison.Ordinal))
            {
                break;
            }
        }

        return true;
    }

    private static string DecodeFieldText(byte[] data, Encoding encoding, bool noZero)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        if (noZero)
        {
            return encoding.GetString(data);
        }

        var zeroIndex = Array.IndexOf(data, (byte)0);
        if (zeroIndex < 0)
        {
            return encoding.GetString(data);
        }

        return zeroIndex == 0 ? string.Empty : encoding.GetString(data, 0, zeroIndex);
    }

    private static byte[] EncodeFieldText(string text, Encoding encoding, bool noZero, bool hasTerminator)
    {
        var raw = encoding.GetBytes(text);
        if (noZero)
        {
            return raw;
        }

        if (!hasTerminator)
        {
            return raw;
        }

        var output = new byte[raw.Length + 1];
        Buffer.BlockCopy(raw, 0, output, 0, raw.Length);
        output[^1] = 0;
        return output;
    }

    private static string BuildKey(string recordSignature, uint formId, string fieldSignature, int fieldIndex)
    {
        return $"{recordSignature}:{formId:X8}:{fieldSignature}:{fieldIndex}";
    }
}

