using System.IO.Compression;
using System.Text;
using bTranslator.Application.Abstractions;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Models;
using K4os.Compression.LZ4;

namespace bTranslator.Infrastructure.Bethesda.Services;

public sealed class BsaBa2ArchiveToolchainService : IArchiveToolchainService
{
    public Task<IReadOnlyList<ArchiveEntry>> ListEntriesAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = File.OpenRead(archivePath);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var descriptors = ReadArchiveDescriptors(reader, archivePath);
        var result = descriptors
            .Select(static d => new ArchiveEntry
            {
                Format = d.Format,
                Path = d.Path,
                Offset = d.Offset,
                Size = d.Size,
                Compressed = d.Compressed
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<ArchiveEntry>>(result);
    }

    public async Task ExtractEntryAsync(
        string archivePath,
        string entryPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = File.OpenRead(archivePath);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var descriptors = ReadArchiveDescriptors(reader, archivePath);
        var descriptor = descriptors.FirstOrDefault(
            x => string.Equals(NormalizePath(x.Path), NormalizePath(entryPath), StringComparison.OrdinalIgnoreCase));
        if (descriptor is null)
        {
            throw new FileNotFoundException($"Entry '{entryPath}' not found in archive.", entryPath);
        }

        var bytes = descriptor.Format switch
        {
            ArchiveFormat.Ba2 => ReadBa2Entry(reader, descriptor),
            ArchiveFormat.Bsa => ReadBsaEntry(reader, descriptor),
            _ => throw new NotSupportedException("Unsupported archive format.")
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<ArchiveEntryDescriptor> ReadArchiveDescriptors(BinaryReader reader, string archivePath)
    {
        reader.BaseStream.Position = 0;
        var signature = Encoding.ASCII.GetString(reader.ReadBytes(4));
        reader.BaseStream.Position = 0;
        return signature switch
        {
            "BTDX" => ReadBa2Descriptors(reader),
            "BSA\0" => ReadBsaDescriptors(reader),
            _ => throw new NotSupportedException($"Unsupported archive format for file '{archivePath}'.")
        };
    }

    private static IReadOnlyList<ArchiveEntryDescriptor> ReadBa2Descriptors(BinaryReader reader)
    {
        _ = reader.ReadUInt32(); // BTDX
        var version = reader.ReadUInt32();
        var type = Encoding.ASCII.GetString(reader.ReadBytes(4));
        var fileCount = reader.ReadInt32();
        var nameTableOffset = reader.ReadInt64();
        if (fileCount < 0)
        {
            throw new InvalidDataException("Invalid BA2 file count.");
        }

        var descriptors = new List<ArchiveEntryDescriptor>(fileCount);
        if (string.Equals(type, "GNRL", StringComparison.Ordinal))
        {
            for (var i = 0; i < fileCount; i++)
            {
                _ = reader.ReadUInt32(); // hash
                _ = reader.ReadUInt32(); // extension
                _ = reader.ReadUInt32(); // directory hash
                _ = reader.ReadUInt32(); // flags
                var offset = reader.ReadInt64();
                var packedSize = reader.ReadInt32();
                var unpackedSize = reader.ReadInt32();
                _ = reader.ReadUInt32(); // align
                descriptors.Add(new ArchiveEntryDescriptor
                {
                    Format = ArchiveFormat.Ba2,
                    Path = string.Empty,
                    Offset = offset,
                    Size = packedSize == 0 ? unpackedSize : packedSize,
                    Compressed = packedSize > 0,
                    UnpackedSize = unpackedSize,
                    ArchiveType = type,
                    Version = version
                });
            }
        }
        else if (string.Equals(type, "DX10", StringComparison.Ordinal))
        {
            var textureRecords = new List<Ba2Dx10EntryDescriptor>(fileCount);
            for (var i = 0; i < fileCount; i++)
            {
                _ = reader.ReadUInt32(); // hash
                _ = reader.ReadUInt32(); // extension
                _ = reader.ReadUInt32(); // directory hash
                var unknown = reader.ReadByte();
                var chunkCount = reader.ReadByte();
                var chunkHeaderSize = reader.ReadUInt16();
                var height = reader.ReadUInt16();
                var width = reader.ReadUInt16();
                var mipCount = reader.ReadByte();
                var dxgiFormat = reader.ReadByte();
                var isCubemap = reader.ReadUInt16();
                textureRecords.Add(new Ba2Dx10EntryDescriptor
                {
                    Unknown = unknown,
                    ChunkCount = chunkCount,
                    ChunkHeaderSize = chunkHeaderSize,
                    Height = height,
                    Width = width,
                    MipCount = mipCount,
                    DxgiFormat = dxgiFormat,
                    IsCubemap = isCubemap
                });
            }

            foreach (var textureRecord in textureRecords)
            {
                var chunks = new List<Ba2TextureChunkDescriptor>(textureRecord.ChunkCount);
                for (var chunkIndex = 0; chunkIndex < textureRecord.ChunkCount; chunkIndex++)
                {
                    var offset = reader.ReadInt64();
                    var packedSize = reader.ReadInt32();
                    var unpackedSize = reader.ReadInt32();
                    var startMip = reader.ReadUInt16();
                    var endMip = reader.ReadUInt16();
                    _ = reader.ReadUInt32(); // align
                    if (packedSize < 0 || unpackedSize < 0)
                    {
                        throw new InvalidDataException("Invalid BA2 DX10 chunk size.");
                    }

                    var descriptor = new Ba2TextureChunkDescriptor
                    {
                        Offset = offset,
                        PackedSize = packedSize,
                        UnpackedSize = unpackedSize,
                        StartMip = startMip,
                        EndMip = endMip
                    };

                    if (descriptor.StoredSize < 0)
                    {
                        throw new InvalidDataException("Invalid BA2 DX10 stored chunk size.");
                    }

                    chunks.Add(descriptor);
                }

                var packedTotal = chunks.Sum(static x => (long)x.StoredSize);
                var unpackedTotal = chunks.Sum(static x => (long)x.UnpackedSize);
                descriptors.Add(new ArchiveEntryDescriptor
                {
                    Format = ArchiveFormat.Ba2,
                    Path = string.Empty,
                    Offset = chunks.Count == 0 ? 0 : chunks.Min(static x => x.Offset),
                    Size = packedTotal > int.MaxValue ? int.MaxValue : (int)packedTotal,
                    Compressed = chunks.Any(static x => x.PackedSize > 0),
                    UnpackedSize = unpackedTotal > int.MaxValue ? int.MaxValue : (int)unpackedTotal,
                    ArchiveType = type,
                    Version = version,
                    TextureMetadata = textureRecord,
                    TextureChunks = chunks
                });
            }
        }
        else
        {
            throw new NotSupportedException($"Unsupported BA2 archive type '{type}'.");
        }

        reader.BaseStream.Position = nameTableOffset;
        for (var i = 0; i < fileCount; i++)
        {
            var nameLength = reader.ReadUInt16();
            var bytes = reader.ReadBytes(nameLength);
            if (bytes.Length != nameLength)
            {
                throw new InvalidDataException("Unexpected BA2 name table length.");
            }

            descriptors[i].Path = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }

        return descriptors;
    }

    private static IReadOnlyList<ArchiveEntryDescriptor> ReadBsaDescriptors(BinaryReader reader)
    {
        _ = reader.ReadUInt32(); // BSA\0
        var version = reader.ReadUInt32();
        _ = reader.ReadUInt32(); // offset
        var archiveFlags = reader.ReadUInt32();
        var folderCount = reader.ReadInt32();
        var fileCount = reader.ReadInt32();
        _ = reader.ReadUInt32(); // total folder name length
        _ = reader.ReadUInt32(); // total file name length
        _ = reader.ReadUInt32(); // file flags
        if (folderCount < 0 || fileCount < 0)
        {
            throw new InvalidDataException("Invalid BSA metadata.");
        }

        var hasEmbeddedFileNames = (archiveFlags & 0x0000_0100) != 0;
        var baseCompressed = (archiveFlags & 0x0000_0004) != 0;
        var folderRecords = new List<BsaFolderRecord>(folderCount);
        for (var i = 0; i < folderCount; i++)
        {
            _ = reader.ReadUInt64(); // folder hash
            var filesInFolder = reader.ReadInt32();
            _ = reader.ReadUInt32(); // folder offset
            if (version >= 105)
            {
                _ = reader.ReadUInt32(); // unknown
            }

            folderRecords.Add(new BsaFolderRecord
            {
                FileCount = filesInFolder
            });
        }

        var fileEntries = new List<ArchiveEntryDescriptor>(fileCount);
        foreach (var folder in folderRecords)
        {
            var folderNameLength = reader.ReadByte();
            var folderNameBytes = reader.ReadBytes(folderNameLength);
            var folderName = Encoding.UTF8.GetString(folderNameBytes).TrimEnd('\0');

            for (var i = 0; i < folder.FileCount; i++)
            {
                _ = reader.ReadUInt64(); // hash
                var sizeWithFlags = reader.ReadUInt32();
                var offset = reader.ReadUInt32();
                var compressedToggle = (sizeWithFlags & 0x4000_0000) != 0;
                var compressed = baseCompressed ? !compressedToggle : compressedToggle;
                var size = (int)(sizeWithFlags & 0x3FFF_FFFF);
                fileEntries.Add(new ArchiveEntryDescriptor
                {
                    Format = ArchiveFormat.Bsa,
                    Path = folderName,
                    Offset = offset,
                    Size = size,
                    Compressed = compressed,
                    UnpackedSize = compressed ? 0 : size,
                    Version = version,
                    HasEmbeddedFileName = hasEmbeddedFileNames
                });
            }
        }

        for (var i = 0; i < fileEntries.Count; i++)
        {
            var fileName = ReadZeroTerminated(reader);
            fileEntries[i].Path = Path.Combine(fileEntries[i].Path, fileName).Replace('\\', '/');
        }

        return fileEntries;
    }

    private static byte[] ReadBa2Entry(BinaryReader reader, ArchiveEntryDescriptor descriptor)
    {
        if (string.Equals(descriptor.ArchiveType, "GNRL", StringComparison.Ordinal))
        {
            reader.BaseStream.Position = descriptor.Offset;
            var bytes = reader.ReadBytes(descriptor.Size);
            if (bytes.Length != descriptor.Size)
            {
                throw new InvalidDataException("Unexpected BA2 GNRL entry length.");
            }

            return descriptor.Compressed
                ? DecodeCompressedPayload(bytes, descriptor.UnpackedSize, includeUnpackedPrefix: false)
                : bytes;
        }

        if (!string.Equals(descriptor.ArchiveType, "DX10", StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unsupported BA2 archive type '{descriptor.ArchiveType}'.");
        }

        if (descriptor.TextureChunks.Count == 0)
        {
            throw new InvalidDataException("BA2 DX10 entry has no chunk metadata.");
        }

        using var payload = new MemoryStream();
        foreach (var chunk in descriptor.TextureChunks.OrderBy(static x => x.StartMip))
        {
            reader.BaseStream.Position = chunk.Offset;
            var chunkBytes = reader.ReadBytes(chunk.StoredSize);
            if (chunkBytes.Length != chunk.StoredSize)
            {
                throw new InvalidDataException("Unexpected BA2 DX10 chunk length.");
            }

            var decoded = chunk.PackedSize > 0
                ? DecodeCompressedPayload(chunkBytes, chunk.UnpackedSize, includeUnpackedPrefix: false)
                : chunkBytes;
            payload.Write(decoded, 0, decoded.Length);
        }

        return ComposeDx10Texture(descriptor, payload.ToArray());
    }

    private static byte[] ReadBsaEntry(BinaryReader reader, ArchiveEntryDescriptor descriptor)
    {
        reader.BaseStream.Position = descriptor.Offset;
        var remaining = descriptor.Size;
        if (remaining < 0)
        {
            throw new InvalidDataException("Invalid BSA entry size.");
        }

        if (descriptor.HasEmbeddedFileName)
        {
            if (remaining <= 0)
            {
                throw new InvalidDataException("Invalid BSA embedded filename length.");
            }

            var embeddedLength = reader.ReadByte();
            remaining -= 1;
            if (embeddedLength < 0 || embeddedLength > remaining)
            {
                throw new InvalidDataException("Invalid BSA embedded filename payload.");
            }

            _ = reader.ReadBytes(embeddedLength);
            remaining -= embeddedLength;
        }

        var bytes = reader.ReadBytes(remaining);
        if (bytes.Length != remaining)
        {
            throw new InvalidDataException("Unexpected BSA entry length.");
        }

        return descriptor.Compressed
            ? DecodeCompressedPayload(bytes, descriptor.UnpackedSize, includeUnpackedPrefix: true)
            : bytes;
    }

    private static byte[] ComposeDx10Texture(ArchiveEntryDescriptor descriptor, byte[] payload)
    {
        if (!descriptor.Path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
        {
            return payload;
        }

        if (payload.Length >= 4 &&
            payload[0] == (byte)'D' &&
            payload[1] == (byte)'D' &&
            payload[2] == (byte)'S' &&
            payload[3] == (byte)' ')
        {
            return payload;
        }

        if (descriptor.TextureMetadata is null)
        {
            return payload;
        }

        var header = BuildDdsDx10Header(descriptor.TextureMetadata, payload.Length);
        var result = new byte[header.Length + payload.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(payload, 0, result, header.Length, payload.Length);
        return result;
    }

    private static byte[] BuildDdsDx10Header(Ba2Dx10EntryDescriptor metadata, int payloadSize)
    {
        var mipCount = Math.Max(1, (int)metadata.MipCount);
        const uint ddsdCaps = 0x00000001;
        const uint ddsdHeight = 0x00000002;
        const uint ddsdWidth = 0x00000004;
        const uint ddsdPixelFormat = 0x00001000;
        const uint ddsdMipMapCount = 0x00020000;
        const uint ddsdLinearSize = 0x00080000;
        var flags = ddsdCaps | ddsdHeight | ddsdWidth | ddsdPixelFormat | ddsdLinearSize;
        if (mipCount > 1)
        {
            flags |= ddsdMipMapCount;
        }

        const uint ddpfFourCc = 0x00000004;
        const uint ddsCapsTexture = 0x00001000;
        const uint ddsCapsComplex = 0x00000008;
        const uint ddsCapsMipmap = 0x00400000;
        const uint ddsCaps2CubemapAllFaces = 0x0000FE00;
        var caps = ddsCapsTexture;
        if (mipCount > 1)
        {
            caps |= ddsCapsComplex | ddsCapsMipmap;
        }

        var caps2 = metadata.IsCubemap == 0 ? 0u : ddsCaps2CubemapAllFaces;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("DDS "));
        writer.Write(124u); // dwSize
        writer.Write(flags);
        writer.Write((uint)Math.Max(1, (int)metadata.Height));
        writer.Write((uint)Math.Max(1, (int)metadata.Width));
        writer.Write((uint)Math.Max(payloadSize, 0));
        writer.Write(0u); // depth
        writer.Write((uint)mipCount);
        for (var i = 0; i < 11; i++)
        {
            writer.Write(0u);
        }

        writer.Write(32u); // pixel format size
        writer.Write(ddpfFourCc);
        writer.Write(0x30315844u); // "DX10"
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);

        writer.Write(caps);
        writer.Write(caps2);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u); // reserved2

        // DDS_HEADER_DXT10
        writer.Write((uint)(metadata.DxgiFormat == 0 ? 28 : metadata.DxgiFormat));
        writer.Write(3u); // D3D10_RESOURCE_DIMENSION_TEXTURE2D
        writer.Write(metadata.IsCubemap == 0 ? 0u : 0x4u);
        writer.Write(1u); // array size
        writer.Write(0u); // misc flags 2
        return stream.ToArray();
    }

    private static byte[] DecodeCompressedPayload(byte[] bytes, int expectedUnpackedSize, bool includeUnpackedPrefix)
    {
        if (bytes.Length == 0)
        {
            return Array.Empty<byte>();
        }

        if (includeUnpackedPrefix && bytes.Length >= 4)
        {
            var prefixedExpectedSize = BitConverter.ToInt32(bytes, 0);
            var compressed = bytes.AsSpan(4);
            var decoded = TryDecodeCompressedPayload(
                compressed,
                prefixedExpectedSize > 0 ? prefixedExpectedSize : expectedUnpackedSize);
            if (decoded is not null)
            {
                return decoded;
            }
        }

        var fallback = TryDecodeCompressedPayload(bytes, expectedUnpackedSize);
        if (fallback is not null)
        {
            return fallback;
        }

        throw new InvalidDataException("Archive entry decompression failed.");
    }

    private static byte[]? TryDecodeCompressedPayload(ReadOnlySpan<byte> compressedBytes, int expectedUnpackedSize)
    {
        if (TryDecodeCompressedStream(compressedBytes, expectedUnpackedSize, useZlib: true, out var zlib))
        {
            return zlib;
        }

        if (TryDecodeCompressedStream(compressedBytes, expectedUnpackedSize, useZlib: false, out var deflate))
        {
            return deflate;
        }

        if (expectedUnpackedSize > 0 && TryDecodeLz4(compressedBytes, expectedUnpackedSize, out var lz4))
        {
            return lz4;
        }

        return null;
    }

    private static bool TryDecodeCompressedStream(
        ReadOnlySpan<byte> compressedBytes,
        int expectedUnpackedSize,
        bool useZlib,
        out byte[] decoded)
    {
        try
        {
            using var compressed = new MemoryStream(compressedBytes.ToArray(), writable: false);
            using Stream decoder = useZlib
                ? new ZLibStream(compressed, CompressionMode.Decompress)
                : new DeflateStream(compressed, CompressionMode.Decompress);
            using var output = new MemoryStream();
            decoder.CopyTo(output);
            var result = output.ToArray();
            if (expectedUnpackedSize > 0 && result.Length != expectedUnpackedSize)
            {
                decoded = Array.Empty<byte>();
                return false;
            }

            decoded = result;
            return true;
        }
        catch
        {
            decoded = Array.Empty<byte>();
            return false;
        }
    }

    private static bool TryDecodeLz4(ReadOnlySpan<byte> compressedBytes, int expectedUnpackedSize, out byte[] decoded)
    {
        try
        {
            var buffer = new byte[expectedUnpackedSize];
            var decodedSize = LZ4Codec.Decode(compressedBytes, buffer);
            if (decodedSize <= 0)
            {
                decoded = Array.Empty<byte>();
                return false;
            }

            if (decodedSize != buffer.Length)
            {
                Array.Resize(ref buffer, decodedSize);
            }

            decoded = buffer;
            return true;
        }
        catch
        {
            decoded = Array.Empty<byte>();
            return false;
        }
    }

    private static string ReadZeroTerminated(BinaryReader reader)
    {
        using var stream = new MemoryStream();
        while (true)
        {
            var b = reader.ReadByte();
            if (b == 0)
            {
                break;
            }

            stream.WriteByte(b);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string NormalizePath(string value)
    {
        return value.Replace('\\', '/');
    }

    private sealed class BsaFolderRecord
    {
        public required int FileCount { get; init; }
    }

    private sealed class ArchiveEntryDescriptor
    {
        public required ArchiveFormat Format { get; init; }
        public required string Path { get; set; }
        public required long Offset { get; init; }
        public required int Size { get; init; }
        public required bool Compressed { get; init; }
        public required int UnpackedSize { get; init; }
        public uint Version { get; init; }
        public bool HasEmbeddedFileName { get; init; }
        public string ArchiveType { get; init; } = string.Empty;
        public Ba2Dx10EntryDescriptor? TextureMetadata { get; init; }
        public IReadOnlyList<Ba2TextureChunkDescriptor> TextureChunks { get; init; } = Array.Empty<Ba2TextureChunkDescriptor>();
    }

    private sealed class Ba2Dx10EntryDescriptor
    {
        public required byte Unknown { get; init; }
        public required byte ChunkCount { get; init; }
        public required ushort ChunkHeaderSize { get; init; }
        public required ushort Height { get; init; }
        public required ushort Width { get; init; }
        public required byte MipCount { get; init; }
        public required byte DxgiFormat { get; init; }
        public required ushort IsCubemap { get; init; }
    }

    private sealed class Ba2TextureChunkDescriptor
    {
        public required long Offset { get; init; }
        public required int PackedSize { get; init; }
        public required int UnpackedSize { get; init; }
        public required ushort StartMip { get; init; }
        public required ushort EndMip { get; init; }
        public int StoredSize => PackedSize == 0 ? UnpackedSize : PackedSize;
    }
}

