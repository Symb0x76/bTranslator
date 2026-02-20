using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using bTranslator.Application.Abstractions;
using bTranslator.Domain.Models;

namespace bTranslator.Infrastructure.Persistence.Compatibility;

public sealed class SstCompatibilityService : ISstCompatibilityService
{
    private static readonly Encoding SstEncoding = Encoding.Unicode;

    private static readonly Dictionary<uint, int> HeaderToVersion = new()
    {
        [0x53535531] = 1, // SSU1
        [0x53535532] = 2, // SSU2
        [0x53535533] = 3, // SSU3
        [0x53535534] = 4, // SSU4
        [0x53535535] = 5, // SSU5
        [0x53535536] = 6, // SSU6
        [0x53535537] = 7, // SSU7
        [0x39555353] = 8 // SSU8
    };

    private static readonly uint[] VersionHeaders =
    {
        0,
        0x53535531,
        0x53535532,
        0x53535533,
        0x53535534,
        0x53535535,
        0x53535536,
        0x53535537,
        0x39555353
    };

    public async Task<IReadOnlyList<TranslationItem>> ImportAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var version = await TryReadHeaderVersionAsync(stream, cancellationToken).ConfigureAwait(false);
        stream.Position = 0;
        if (version > 0)
        {
            return await ImportLegacyBinaryAsync(stream, version, cancellationToken).ConfigureAwait(false);
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".json" or ".sstx")
        {
            var data = await JsonSerializer.DeserializeAsync<List<SstEntryDto>>(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return (data ?? []).Select(static x => new TranslationItem
            {
                Id = x.Id,
                SourceText = x.Source,
                TranslatedText = x.Target,
                IsValidated = x.IsValidated,
                IsLocked = x.IsLocked
            }).ToList();
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var result = new List<TranslationItem>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length < 3)
            {
                continue;
            }

            result.Add(new TranslationItem
            {
                Id = parts[0],
                SourceText = parts[1],
                TranslatedText = parts[2]
            });
        }

        return result;
    }

    public async Task ExportAsync(
        string path,
        IEnumerable<TranslationItem> items,
        int version = 8,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".json" or ".sstx")
        {
            var payload = items.Select(static x => new SstEntryDto
            {
                Id = x.Id,
                Source = x.SourceText,
                Target = x.TranslatedText ?? string.Empty,
                IsValidated = x.IsValidated,
                IsLocked = x.IsLocked
            }).ToArray();

            await using var jsonStream = File.Create(path);
            await JsonSerializer.SerializeAsync(jsonStream, payload, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        if (version is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Legacy SST version must be between 1 and 8.");
        }

        await using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, SstEncoding, leaveOpen: true);
        WriteLegacyBinary(writer, version, items);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> TryReadHeaderVersionAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.Length < sizeof(uint))
        {
            return 0;
        }

        var headerBytes = new byte[sizeof(uint)];
        await stream.ReadExactlyAsync(headerBytes, cancellationToken).ConfigureAwait(false);
        var header = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes);
        return HeaderToVersion.GetValueOrDefault(header);
    }

    private static Task<IReadOnlyList<TranslationItem>> ImportLegacyBinaryAsync(
        Stream stream,
        int version,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var reader = new BinaryReader(stream, SstEncoding, leaveOpen: true);

        _ = reader.ReadUInt32(); // header
        if (version > 3)
        {
            _ = reader.ReadByte(); // v4+ placeholder flag
        }

        if (version > 7)
        {
            var masterCount = reader.ReadInt32();
            for (var i = 0; i < masterCount; i++)
            {
                _ = ReadSizedString(reader);
            }
        }

        var colabLabels = new Dictionary<byte, string>();
        if (version > 6)
        {
            var colabCount = reader.ReadInt32();
            for (var i = 0; i < colabCount; i++)
            {
                var colabIdRaw = reader.ReadInt32();
                var label = ReadSizedString(reader);
                if (colabIdRaw is >= byte.MinValue and <= byte.MaxValue)
                {
                    colabLabels[(byte)colabIdRaw] = label;
                }
            }
        }

        var result = new List<TranslationItem>();
        while (stream.Position < stream.Length)
        {
            var listIndex = reader.ReadByte();
            var pointer = new SstRecordPointerLite
            {
                RecordSignature = "****",
                FieldSignature = "****"
            };

            byte colabId = 0;
            if (version > 1)
            {
                var stringId = reader.ReadInt32();
                var formId = reader.ReadUInt32();
                var recordSignature = version > 4 ? ReadSignature(reader) : "****";
                var fieldSignature = ReadSignature(reader);
                pointer = new SstRecordPointerLite
                {
                    StringId = stringId,
                    FormId = formId,
                    RecordSignature = recordSignature,
                    FieldSignature = fieldSignature,
                    Index = version > 2 ? reader.ReadUInt16() : (ushort)0,
                    IndexMax = version > 3 ? reader.ReadUInt16() : (ushort)0,
                    RecordHash = version > 3 ? reader.ReadUInt32() : 0
                };

                if (version > 5)
                {
                    colabId = reader.ReadByte();
                }
            }

            var flags = (SstEntryFlags)reader.ReadByte();
            if (version < 4)
            {
                flags &= ~(SstEntryFlags.LockedTranslation | SstEntryFlags.DeprecatedParam1 | SstEntryFlags.DeprecatedParam2);
            }

            var source = ReadSizedString(reader);
            var target = ReadSizedString(reader);
            if (source.Length == 0 && target.Length == 0)
            {
                continue;
            }

            result.Add(new TranslationItem
            {
                Id = BuildItemId(pointer, listIndex),
                SourceText = source,
                TranslatedText = target,
                IsValidated = flags.HasFlag(SstEntryFlags.Validated),
                IsLocked = flags.HasFlag(SstEntryFlags.LockedTranslation),
                SstMetadata = new SstEntryMetadata
                {
                    ListIndex = listIndex,
                    CollaborationId = colabId,
                    CollaborationLabel = colabLabels.GetValueOrDefault(colabId),
                    Flags = flags,
                    Pointer = pointer
                }
            });
        }

        return Task.FromResult<IReadOnlyList<TranslationItem>>(result);
    }

    private static void WriteLegacyBinary(BinaryWriter writer, int version, IEnumerable<TranslationItem> items)
    {
        writer.Write(VersionHeaders[version]);

        if (version > 3)
        {
            writer.Write((byte)0); // v4 placeholder flag
        }

        if (version > 7)
        {
            writer.Write(0); // master list count (kept empty in this implementation)
        }

        var colabLabels = items
            .Select(static item => item.SstMetadata)
            .Where(static meta => meta is not null && meta.CollaborationId > 0 && !string.IsNullOrWhiteSpace(meta.CollaborationLabel))
            .GroupBy(static meta => meta!.CollaborationId)
            .ToDictionary(static g => g.Key, static g => g.First()!.CollaborationLabel!);

        if (version > 6)
        {
            writer.Write(colabLabels.Count);
            foreach (var pair in colabLabels.OrderBy(static x => x.Key))
            {
                writer.Write((int)pair.Key);
                WriteSizedString(writer, pair.Value);
            }
        }

        foreach (var item in items)
        {
            var meta = item.SstMetadata;
            var listIndex = meta?.ListIndex ?? 0;
            writer.Write(listIndex);

            var pointer = BuildPointer(item, meta);
            if (version > 1)
            {
                writer.Write(pointer.StringId);
                writer.Write(pointer.FormId);

                if (version > 4)
                {
                    WriteSignature(writer, pointer.RecordSignature);
                }

                WriteSignature(writer, pointer.FieldSignature);

                if (version > 2)
                {
                    writer.Write(pointer.Index);
                }

                if (version > 3)
                {
                    writer.Write(pointer.IndexMax);
                    writer.Write(pointer.RecordHash);
                }

                if (version > 5)
                {
                    writer.Write(meta?.CollaborationId ?? (byte)0);
                }
            }

            var flags = meta?.Flags ?? DeriveFlags(item);
            flags &= ~SstEntryFlags.Validated; // xTranslator strips this bit while writing.
            writer.Write((byte)flags);
            WriteSizedString(writer, item.SourceText);
            WriteSizedString(writer, item.TranslatedText ?? string.Empty);
        }
    }

    private static SstRecordPointerLite BuildPointer(TranslationItem item, SstEntryMetadata? meta)
    {
        if (meta is not null)
        {
            return meta.Pointer;
        }

        var parsed = int.TryParse(item.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stringId)
            ? stringId
            : 0;
        return new SstRecordPointerLite
        {
            StringId = parsed,
            RecordSignature = "****",
            FieldSignature = "****"
        };
    }

    private static SstEntryFlags DeriveFlags(TranslationItem item)
    {
        var flags = SstEntryFlags.None;
        if (!string.IsNullOrWhiteSpace(item.TranslatedText) &&
            !string.Equals(item.SourceText, item.TranslatedText, StringComparison.Ordinal))
        {
            flags |= SstEntryFlags.Translated;
        }

        if (item.IsValidated)
        {
            flags |= SstEntryFlags.Validated;
        }

        if (item.IsLocked)
        {
            flags |= SstEntryFlags.LockedTranslation;
        }

        return flags;
    }

    private static string BuildItemId(SstRecordPointerLite pointer, byte listIndex)
    {
        if (pointer.StringId > 0)
        {
            return pointer.StringId.ToString(CultureInfo.InvariantCulture);
        }

        return $"{pointer.RecordSignature}:{pointer.FormId:X8}:{pointer.FieldSignature}:{listIndex}:{pointer.Index}";
    }

    private static string ReadSizedString(BinaryReader reader)
    {
        var byteSize = reader.ReadInt32();
        if (byteSize <= 0)
        {
            return string.Empty;
        }

        var bytes = reader.ReadBytes(byteSize);
        return SstEncoding.GetString(bytes);
    }

    private static void WriteSizedString(BinaryWriter writer, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            writer.Write(0);
            return;
        }

        var bytes = SstEncoding.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadSignature(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (bytes.Length < 4)
        {
            return "****";
        }

        return Encoding.ASCII.GetString(bytes);
    }

    private static void WriteSignature(BinaryWriter writer, string signature)
    {
        var padded = (signature ?? "****").PadRight(4, '_');
        var bytes = Encoding.ASCII.GetBytes(padded[..4]);
        writer.Write(bytes);
    }

    private sealed class SstEntryDto
    {
        public string Id { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
        public bool IsValidated { get; init; }
        public bool IsLocked { get; init; }
    }
}

