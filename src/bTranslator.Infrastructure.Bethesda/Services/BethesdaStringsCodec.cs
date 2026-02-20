using System.Text;
using bTranslator.Application.Abstractions;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Models;

namespace bTranslator.Infrastructure.Bethesda.Services;

public sealed class BethesdaStringsCodec : IStringsCodec
{
    public Task<IReadOnlyList<StringsEntry>> ReadAsync(
        string path,
        StringsFileKind kind,
        Encoding encoding,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var fs = File.OpenRead(path);
        using var reader = new BinaryReader(fs);
        var count = reader.ReadUInt32();
        _ = reader.ReadUInt32(); // total data size, informational

        var index = new (uint Id, uint Offset)[count];
        for (var i = 0; i < count; i++)
        {
            index[i] = (reader.ReadUInt32(), reader.ReadUInt32());
        }

        var dataStart = fs.Position;
        var result = new List<StringsEntry>((int)count);
        foreach (var item in index)
        {
            fs.Seek(dataStart + item.Offset, SeekOrigin.Begin);
            var text = kind switch
            {
                StringsFileKind.Strings => ReadNullTerminated(reader, encoding),
                _ => ReadLengthPrefixed(reader, encoding)
            };

            result.Add(new StringsEntry
            {
                Id = item.Id,
                Text = text
            });
        }

        return Task.FromResult<IReadOnlyList<StringsEntry>>(result);
    }

    public async Task WriteAsync(
        string path,
        StringsFileKind kind,
        IEnumerable<StringsEntry> entries,
        Encoding encoding,
        CancellationToken cancellationToken = default)
    {
        var ordered = entries.OrderBy(static x => x.Id).ToArray();
        await using var outStream = File.Create(path);
        await using var bodyStream = new MemoryStream();
        await using var indexStream = new MemoryStream();
        using var indexWriter = new BinaryWriter(indexStream, Encoding.UTF8, leaveOpen: true);
        using var bodyWriter = new BinaryWriter(bodyStream, Encoding.UTF8, leaveOpen: true);

        foreach (var entry in ordered)
        {
            var offset = checked((uint)bodyStream.Position);
            indexWriter.Write(entry.Id);
            indexWriter.Write(offset);

            var bytes = encoding.GetBytes(entry.Text);
            if (kind == StringsFileKind.Strings)
            {
                bodyWriter.Write(bytes);
                bodyWriter.Write((byte)0);
            }
            else
            {
                bodyWriter.Write(bytes.Length + 1);
                bodyWriter.Write(bytes);
                bodyWriter.Write((byte)0);
            }
        }

        await using var header = new MemoryStream();
        using (var writer = new BinaryWriter(header, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((uint)ordered.Length);
            writer.Write((uint)bodyStream.Length);
        }

        header.Position = 0;
        indexStream.Position = 0;
        bodyStream.Position = 0;

        await header.CopyToAsync(outStream, cancellationToken).ConfigureAwait(false);
        await indexStream.CopyToAsync(outStream, cancellationToken).ConfigureAwait(false);
        await bodyStream.CopyToAsync(outStream, cancellationToken).ConfigureAwait(false);
    }

    private static string ReadNullTerminated(BinaryReader reader, Encoding encoding)
    {
        var bytes = new List<byte>(64);
        while (true)
        {
            var next = reader.ReadByte();
            if (next == 0)
            {
                break;
            }

            bytes.Add(next);
        }

        return encoding.GetString(bytes.ToArray());
    }

    private static string ReadLengthPrefixed(BinaryReader reader, Encoding encoding)
    {
        var length = reader.ReadInt32();
        if (length <= 0)
        {
            return string.Empty;
        }

        var bytes = reader.ReadBytes(length);
        if (bytes.Length > 0 && bytes[^1] == 0)
        {
            bytes = bytes[..^1];
        }

        return encoding.GetString(bytes);
    }
}

