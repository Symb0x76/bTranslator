using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace bTranslator.Infrastructure.Bethesda.Services;

public sealed class PluginBinaryCodec
{
    internal PluginBinaryDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        var chunks = ReadChunks(reader, stream.Length);
        return new PluginBinaryDocument
        {
            Chunks = chunks
        };
    }

    internal void Save(string path, PluginBinaryDocument document)
    {
        var payload = document.BuildBytes();
        File.WriteAllBytes(path, payload);
    }

    private static List<PluginChunk> ReadChunks(BinaryReader reader, long endPosition)
    {
        var chunks = new List<PluginChunk>();
        while (reader.BaseStream.Position < endPosition)
        {
            var chunkStart = reader.BaseStream.Position;
            if (endPosition - chunkStart < 4)
            {
                throw new InvalidDataException("Unexpected trailing bytes while parsing plugin.");
            }

            var signature = ReadSignature(reader);
            if (signature == "GRUP")
            {
                if (endPosition - chunkStart < 24)
                {
                    throw new InvalidDataException("Invalid GRUP header size.");
                }

                var size = reader.ReadUInt32();
                var label = reader.ReadBytes(4);
                var groupType = reader.ReadInt32();
                var stamp = reader.ReadUInt16();
                var unknown1 = reader.ReadUInt16();
                var version = reader.ReadUInt16();
                var unknown2 = reader.ReadUInt16();
                var groupEnd = chunkStart + size;
                if (groupEnd > endPosition || groupEnd < reader.BaseStream.Position)
                {
                    throw new InvalidDataException("GRUP size is outside parent bounds.");
                }

                var children = ReadChunks(reader, groupEnd);
                reader.BaseStream.Position = groupEnd;
                chunks.Add(new PluginGroupChunk
                {
                    Label = label,
                    GroupType = groupType,
                    Stamp = stamp,
                    Unknown1 = unknown1,
                    Version = version,
                    Unknown2 = unknown2,
                    Children = children
                });
                continue;
            }

            if (endPosition - chunkStart < 24)
            {
                throw new InvalidDataException("Invalid record header size.");
            }

            var dataSize = reader.ReadUInt32();
            var flags = reader.ReadUInt32();
            var formId = reader.ReadUInt32();
            var revision = reader.ReadUInt32();
            var versionMajor = reader.ReadUInt16();
            var unknown = reader.ReadUInt16();
            if (reader.BaseStream.Position + dataSize > endPosition)
            {
                throw new InvalidDataException($"Record '{signature}' exceeds current group bounds.");
            }

            var rawPayload = reader.ReadBytes((int)dataSize);
            var record = new PluginRecordChunk
            {
                Signature = signature,
                Flags = flags,
                FormId = formId,
                Revision = revision,
                Version = versionMajor,
                Unknown = unknown,
                RawPayload = rawPayload
            };
            record.TryParseSubrecords();
            chunks.Add(record);
        }

        return chunks;
    }

    private static string ReadSignature(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (bytes.Length != 4)
        {
            throw new EndOfStreamException("Cannot read signature.");
        }

        return Encoding.ASCII.GetString(bytes);
    }
}

internal sealed class PluginBinaryDocument
{
    public required IList<PluginChunk> Chunks { get; init; }

    public IEnumerable<PluginRecordChunk> EnumerateRecords()
    {
        foreach (var chunk in Chunks)
        {
            foreach (var record in chunk.EnumerateRecords())
            {
                yield return record;
            }
        }
    }

    public byte[] BuildBytes()
    {
        using var stream = new MemoryStream();
        foreach (var chunk in Chunks)
        {
            var bytes = chunk.BuildBytes();
            stream.Write(bytes, 0, bytes.Length);
        }

        return stream.ToArray();
    }
}

internal abstract class PluginChunk
{
    public abstract IEnumerable<PluginRecordChunk> EnumerateRecords();
    public abstract byte[] BuildBytes();
}

internal sealed class PluginGroupChunk : PluginChunk
{
    public required byte[] Label { get; init; }
    public required int GroupType { get; init; }
    public required ushort Stamp { get; init; }
    public required ushort Unknown1 { get; init; }
    public required ushort Version { get; init; }
    public required ushort Unknown2 { get; init; }
    public required IList<PluginChunk> Children { get; init; }

    public override IEnumerable<PluginRecordChunk> EnumerateRecords()
    {
        foreach (var child in Children)
        {
            foreach (var record in child.EnumerateRecords())
            {
                yield return record;
            }
        }
    }

    public override byte[] BuildBytes()
    {
        using var body = new MemoryStream();
        foreach (var child in Children)
        {
            var bytes = child.BuildBytes();
            body.Write(bytes, 0, bytes.Length);
        }

        var groupSize = checked((uint)(24 + body.Length));
        using var output = new MemoryStream((int)groupSize);
        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("GRUP"));
        writer.Write(groupSize);
        writer.Write(Label.Length == 4 ? Label : Label.Concat(new byte[4]).Take(4).ToArray());
        writer.Write(GroupType);
        writer.Write(Stamp);
        writer.Write(Unknown1);
        writer.Write(Version);
        writer.Write(Unknown2);
        body.Position = 0;
        body.CopyTo(output);
        return output.ToArray();
    }
}

internal sealed class PluginRecordChunk : PluginChunk
{
    private const uint CompressedFlag = 0x0004_0000;

    public required string Signature { get; init; }
    public required uint Flags { get; init; }
    public required uint FormId { get; init; }
    public required uint Revision { get; init; }
    public required ushort Version { get; init; }
    public required ushort Unknown { get; init; }
    public required byte[] RawPayload { get; init; }
    public IList<PluginSubrecord> Subrecords { get; private set; } = new List<PluginSubrecord>();
    public bool CanRewriteSubrecords { get; private set; }

    private bool IsCompressed => (Flags & CompressedFlag) != 0;

    public void TryParseSubrecords()
    {
        var payload = DecodePayload(RawPayload, IsCompressed);
        var parsed = TryParseSubrecordPayload(payload, out var subrecords);
        CanRewriteSubrecords = parsed;
        if (parsed)
        {
            Subrecords = subrecords;
        }
    }

    public override IEnumerable<PluginRecordChunk> EnumerateRecords()
    {
        yield return this;
    }

    public override byte[] BuildBytes()
    {
        var rawPayload = RawPayload;
        if (CanRewriteSubrecords)
        {
            rawPayload = EncodePayload(BuildSubrecordPayload(Subrecords), IsCompressed);
        }

        using var stream = new MemoryStream(24 + rawPayload.Length);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes(Signature));
        writer.Write((uint)rawPayload.Length);
        writer.Write(Flags);
        writer.Write(FormId);
        writer.Write(Revision);
        writer.Write(Version);
        writer.Write(Unknown);
        writer.Write(rawPayload);
        return stream.ToArray();
    }

    private static byte[] BuildSubrecordPayload(IEnumerable<PluginSubrecord> subrecords)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        foreach (var subrecord in subrecords)
        {
            var signature = NormalizeSignature(subrecord.Signature);
            if (subrecord.Data.Length > ushort.MaxValue || subrecord.UsesExtendedSize)
            {
                writer.Write(Encoding.ASCII.GetBytes("XXXX"));
                writer.Write((ushort)4);
                writer.Write(subrecord.Data.Length);
                writer.Write(Encoding.ASCII.GetBytes(signature));
                writer.Write((ushort)0);
                writer.Write(subrecord.Data);
                continue;
            }

            writer.Write(Encoding.ASCII.GetBytes(signature));
            writer.Write((ushort)subrecord.Data.Length);
            writer.Write(subrecord.Data);
        }

        return stream.ToArray();
    }

    private static bool TryParseSubrecordPayload(
        byte[] payload,
        out IList<PluginSubrecord> subrecords)
    {
        var list = new List<PluginSubrecord>();
        var cursor = 0;
        while (cursor < payload.Length)
        {
            if (payload.Length - cursor < 6)
            {
                subrecords = [];
                return false;
            }

            var signature = Encoding.ASCII.GetString(payload, cursor, 4);
            cursor += 4;
            var size = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(cursor, 2));
            cursor += 2;

            if (signature == "XXXX")
            {
                if (size != 4 || payload.Length - cursor < 10)
                {
                    subrecords = [];
                    return false;
                }

                var extendedSize = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(cursor, 4));
                cursor += 4;
                var realSignature = Encoding.ASCII.GetString(payload, cursor, 4);
                cursor += 4;
                _ = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(cursor, 2));
                cursor += 2;
                if (extendedSize < 0 || payload.Length - cursor < extendedSize)
                {
                    subrecords = [];
                    return false;
                }

                var data = payload.AsSpan(cursor, extendedSize).ToArray();
                cursor += extendedSize;
                list.Add(new PluginSubrecord
                {
                    Signature = realSignature,
                    Data = data,
                    UsesExtendedSize = true
                });
                continue;
            }

            if (payload.Length - cursor < size)
            {
                subrecords = [];
                return false;
            }

            var subData = payload.AsSpan(cursor, size).ToArray();
            cursor += size;
            list.Add(new PluginSubrecord
            {
                Signature = signature,
                Data = subData
            });
        }

        subrecords = list;
        return true;
    }

    private static byte[] DecodePayload(byte[] payload, bool compressed)
    {
        if (!compressed)
        {
            return payload;
        }

        if (payload.Length < 4)
        {
            return payload;
        }

        var compressedData = payload.AsMemory(4);
        try
        {
            using var compressedStream = new MemoryStream(compressedData.ToArray(), writable: false);
            using var zlib = new ZLibStream(compressedStream, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            using var compressedStream = new MemoryStream(compressedData.ToArray(), writable: false);
            using var deflate = new DeflateStream(compressedStream, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }
    }

    private static byte[] EncodePayload(byte[] payload, bool compressed)
    {
        if (!compressed)
        {
            return payload;
        }

        using var output = new MemoryStream();
        output.Write(BitConverter.GetBytes(payload.Length), 0, sizeof(int));
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(payload, 0, payload.Length);
        }

        return output.ToArray();
    }

    private static string NormalizeSignature(string signature)
    {
        var source = signature ?? "____";
        if (source.Length >= 4)
        {
            return source[..4];
        }

        return source.PadRight(4, '_');
    }
}

internal sealed class PluginSubrecord
{
    public required string Signature { get; init; }
    public required byte[] Data { get; set; }
    public bool UsesExtendedSize { get; init; }
}

