using System.Text;
using bTranslator.Application.Abstractions;
using bTranslator.Domain.Models;

namespace bTranslator.Infrastructure.Bethesda.Services;

public sealed class PexToolchainService : IPexToolchainService
{
    public Task<PexDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var magic = reader.ReadUInt32();
        var major = reader.ReadByte();
        var minor = reader.ReadByte();
        _ = reader.ReadByte(); // game id
        _ = reader.ReadByte(); // compilation type
        _ = reader.ReadUInt64(); // timestamp
        var sourceFile = ReadPexString(reader);
        var userName = ReadPexString(reader);
        var machineName = ReadPexString(reader);
        var stringCount = reader.ReadUInt16();
        var strings = new List<PexStringEntry>(stringCount);
        for (var i = 0; i < stringCount; i++)
        {
            strings.Add(new PexStringEntry
            {
                Index = i,
                Value = ReadPexString(reader)
            });
        }

        var document = new PexDocument
        {
            Magic = magic,
            MajorVersion = major,
            MinorVersion = minor,
            SourceFile = sourceFile,
            UserName = userName,
            MachineName = machineName,
            Strings = strings
        };
        return Task.FromResult(document);
    }

    public async Task ExportStringsAsync(
        PexDocument document,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var lines = document.Strings
            .Select(static x => $"{x.Index}\t{x.Value}")
            .ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllLinesAsync(outputPath, lines, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private static string ReadPexString(BinaryReader reader)
    {
        var length = reader.ReadUInt16();
        if (length == 0)
        {
            return string.Empty;
        }

        var bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }
}

