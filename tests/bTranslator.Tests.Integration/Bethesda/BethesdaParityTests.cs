using System.Text;
using System.IO.Compression;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Models;
using bTranslator.Infrastructure.Bethesda.Services;
using FluentAssertions;

namespace bTranslator.Tests.Integration.Bethesda;

public class BethesdaParityTests
{
    [Fact]
    public async Task PluginDocumentService_ShouldLoadAndSaveRecordFieldTranslations()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bTranslator-plugin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var pluginPath = Path.Combine(root, "sample.esp");
        var outputPath = Path.Combine(root, "sample_out.esp");
        var defsPath = Path.Combine(root, "_recorddefs.txt");

        await File.WriteAllBytesAsync(pluginPath, BuildPluginWithBookRecord("Original Title"));
        await File.WriteAllTextAsync(defsPath, "Def_:FULL=BOOK=0");

        var service = new MutagenPluginDocumentService(
            new BethesdaStringsCodec(),
            new PluginBinaryCodec(),
            new PluginRecordMapper(),
            new RecordDefinitionCatalog());

        var document = await service.OpenAsync(
            GameKind.SkyrimSe,
            pluginPath,
            new PluginOpenOptions
            {
                LoadStrings = false,
                LoadRecordFields = true,
                RecordDefinitionsPath = defsPath,
                Encoding = Encoding.UTF8
            });

        document.RecordItems.Should().ContainSingle();
        document.RecordItems[0].SourceText.Should().Be("Original Title");
        document.RecordItems[0].TranslatedText.Should().Be("Original Title");

        document.RecordItems[0].TranslatedText = "Translated Title";
        await service.SaveAsync(
            document,
            outputPath,
            new PluginSaveOptions
            {
                SaveStrings = false,
                SaveRecordFields = true,
                Encoding = Encoding.UTF8
            });

        var reopened = await service.OpenAsync(
            GameKind.SkyrimSe,
            outputPath,
            new PluginOpenOptions
            {
                LoadStrings = false,
                LoadRecordFields = true,
                RecordDefinitionsPath = defsPath,
                Encoding = Encoding.UTF8
            });

        reopened.RecordItems.Should().ContainSingle();
        reopened.RecordItems[0].SourceText.Should().Be("Translated Title");
    }

    [Fact]
    public async Task ArchiveToolchain_ShouldListAndExtractBa2Entries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bTranslator-archive-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var archivePath = Path.Combine(root, "sample.ba2");
        var outputPath = Path.Combine(root, "out", "test.txt");
        await File.WriteAllBytesAsync(archivePath, BuildSimpleBa2("test.txt", "hello"));

        var service = new BsaBa2ArchiveToolchainService();
        var entries = await service.ListEntriesAsync(archivePath);

        entries.Should().ContainSingle();
        entries[0].Path.Should().Be("test.txt");
        entries[0].Size.Should().Be(5);
        entries[0].Compressed.Should().BeFalse();

        await service.ExtractEntryAsync(archivePath, "test.txt", outputPath);
        var text = await File.ReadAllTextAsync(outputPath, Encoding.UTF8);
        text.Should().Be("hello");
    }

    [Fact]
    public async Task ArchiveToolchain_ShouldExtractBa2Dx10TextureEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bTranslator-archive-dx10-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var archivePath = Path.Combine(root, "sample_dx10.ba2");
        var outputPath = Path.Combine(root, "out", "textures", "sample.dds");
        var rawTexturePayload = new byte[] { 1, 2, 3, 4, 5, 6 };
        await File.WriteAllBytesAsync(archivePath, BuildSimpleBa2Dx10("textures/sample.dds", rawTexturePayload));

        var service = new BsaBa2ArchiveToolchainService();
        var entries = await service.ListEntriesAsync(archivePath);

        entries.Should().ContainSingle();
        entries[0].Path.Should().Be("textures/sample.dds");
        entries[0].Size.Should().Be(rawTexturePayload.Length);
        entries[0].Compressed.Should().BeFalse();

        await service.ExtractEntryAsync(archivePath, "textures/sample.dds", outputPath);
        var bytes = await File.ReadAllBytesAsync(outputPath);

        bytes.Length.Should().BeGreaterThan(rawTexturePayload.Length);
        bytes.Take(4).Should().Equal(Encoding.ASCII.GetBytes("DDS "));
        bytes.Skip(bytes.Length - rawTexturePayload.Length).Should().Equal(rawTexturePayload);
    }

    [Fact]
    public async Task ArchiveToolchain_ShouldExtractCompressedBsaEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bTranslator-archive-bsa-compressed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var archivePath = Path.Combine(root, "sample_compressed.bsa");
        var outputPath = Path.Combine(root, "out", "meshes", "test.txt");
        await File.WriteAllBytesAsync(archivePath, BuildSimpleCompressedBsa("meshes", "test.txt", "hello compressed bsa"));

        var service = new BsaBa2ArchiveToolchainService();
        var entries = await service.ListEntriesAsync(archivePath);

        entries.Should().ContainSingle();
        entries[0].Path.Should().Be("meshes/test.txt");
        entries[0].Compressed.Should().BeTrue();

        await service.ExtractEntryAsync(archivePath, "meshes/test.txt", outputPath);
        var text = await File.ReadAllTextAsync(outputPath, Encoding.UTF8);
        text.Should().Be("hello compressed bsa");
    }

    [Fact]
    public async Task PexToolchain_ShouldLoadAndExportStrings()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bTranslator-pex-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var pexPath = Path.Combine(root, "sample.pex");
        var exportPath = Path.Combine(root, "strings.txt");
        await File.WriteAllBytesAsync(pexPath, BuildSimplePex(["Hello", "World"]));

        var service = new PexToolchainService();
        var document = await service.LoadAsync(pexPath);
        document.Strings.Select(static x => x.Value).Should().Equal("Hello", "World");

        await service.ExportStringsAsync(document, exportPath);
        var lines = await File.ReadAllLinesAsync(exportPath, Encoding.UTF8);
        lines.Should().Contain("0\tHello");
        lines.Should().Contain("1\tWorld");
    }

    private static byte[] BuildPluginWithBookRecord(string fullText)
    {
        using var data = new MemoryStream();
        using (var dataWriter = new BinaryWriter(data, Encoding.ASCII, leaveOpen: true))
        {
            WriteSubrecord(dataWriter, "EDID", Encoding.UTF8.GetBytes("BookRecord\0"));
            WriteSubrecord(dataWriter, "FULL", Encoding.UTF8.GetBytes(fullText + "\0"));
        }

        var payload = data.ToArray();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("BOOK"));
            writer.Write((uint)payload.Length);
            writer.Write((uint)0); // flags
            writer.Write((uint)0x01020304);
            writer.Write((uint)0);
            writer.Write((ushort)1);
            writer.Write((ushort)0);
            writer.Write(payload);
        }

        return stream.ToArray();
    }

    private static byte[] BuildSimpleBa2(string path, string content)
    {
        var nameBytes = Encoding.UTF8.GetBytes(path);
        var contentBytes = Encoding.UTF8.GetBytes(content);
        const int headerSize = 24;
        const int fileRecordSize = 36;
        var nameTableOffset = headerSize + fileRecordSize;
        var dataOffset = nameTableOffset + 2 + nameBytes.Length;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("BTDX"));
        writer.Write((uint)1);
        writer.Write(Encoding.ASCII.GetBytes("GNRL"));
        writer.Write(1);
        writer.Write((long)nameTableOffset);

        writer.Write((uint)0); // hash
        writer.Write((uint)0); // ext
        writer.Write((uint)0); // dir hash
        writer.Write((uint)0); // flags
        writer.Write((long)dataOffset);
        writer.Write(0); // packed size = 0 (uncompressed)
        writer.Write(contentBytes.Length);
        writer.Write((uint)0); // align

        writer.Write((ushort)nameBytes.Length);
        writer.Write(nameBytes);
        writer.Write(contentBytes);
        return stream.ToArray();
    }

    private static byte[] BuildSimpleBa2Dx10(string path, byte[] rawTexturePayload)
    {
        var nameBytes = Encoding.UTF8.GetBytes(path);
        const int headerSize = 24;
        const int textureRecordSize = 24;
        const int textureChunkRecordSize = 24;
        var nameTableOffset = headerSize + textureRecordSize + textureChunkRecordSize;
        var dataOffset = nameTableOffset + 2 + nameBytes.Length;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("BTDX"));
        writer.Write((uint)1);
        writer.Write(Encoding.ASCII.GetBytes("DX10"));
        writer.Write(1);
        writer.Write((long)nameTableOffset);

        writer.Write((uint)0); // hash
        writer.Write((uint)0); // ext
        writer.Write((uint)0); // dir hash
        writer.Write((byte)0); // unknown
        writer.Write((byte)1); // chunk count
        writer.Write((ushort)24); // chunk header size
        writer.Write((ushort)1); // height
        writer.Write((ushort)1); // width
        writer.Write((byte)1); // mip count
        writer.Write((byte)28); // DXGI format (R8G8B8A8_UNORM)
        writer.Write((ushort)0); // not cubemap

        writer.Write((long)dataOffset);
        writer.Write(0); // packed size = 0 (uncompressed)
        writer.Write(rawTexturePayload.Length);
        writer.Write((ushort)0); // start mip
        writer.Write((ushort)0); // end mip
        writer.Write((uint)0); // align

        writer.Write((ushort)nameBytes.Length);
        writer.Write(nameBytes);
        writer.Write(rawTexturePayload);
        return stream.ToArray();
    }

    private static byte[] BuildSimpleCompressedBsa(string folderName, string fileName, string content)
    {
        var folderNameBytes = Encoding.UTF8.GetBytes(folderName + "\0");
        var fileNameBytes = Encoding.UTF8.GetBytes(fileName + "\0");
        var payloadBytes = Encoding.UTF8.GetBytes(content);
        var compressedBytes = CompressWithZlib(payloadBytes);

        byte[] entryPayload;
        using (var payloadStream = new MemoryStream())
        using (var payloadWriter = new BinaryWriter(payloadStream, Encoding.UTF8, leaveOpen: true))
        {
            payloadWriter.Write(payloadBytes.Length); // expected uncompressed size prefix
            payloadWriter.Write(compressedBytes);
            payloadWriter.Flush();
            entryPayload = payloadStream.ToArray();
        }

        const int headerSize = 36;
        const int folderRecordSize = 16;
        const int fileRecordSize = 16;
        var folderDataSize = 1 + folderNameBytes.Length + fileRecordSize;
        var fileNameTableSize = fileNameBytes.Length;
        var dataOffset = headerSize + folderRecordSize + folderDataSize + fileNameTableSize;
        var sizeWithFlags = 0x4000_0000u | (uint)entryPayload.Length;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("BSA\0"));
        writer.Write((uint)104); // Oblivion/Skyrim legacy-style
        writer.Write((uint)headerSize);
        writer.Write((uint)0x0000_0003); // include directory names + include file names
        writer.Write(1); // folder count
        writer.Write(1); // file count
        writer.Write((uint)folderNameBytes.Length);
        writer.Write((uint)fileNameTableSize);
        writer.Write((uint)0); // file flags

        writer.Write((ulong)0); // folder hash
        writer.Write(1); // files in folder
        writer.Write((uint)0); // folder offset (unused in parser)

        writer.Write((byte)folderNameBytes.Length);
        writer.Write(folderNameBytes);

        writer.Write((ulong)0); // file hash
        writer.Write(sizeWithFlags);
        writer.Write((uint)dataOffset);

        writer.Write(fileNameBytes);
        writer.Write(entryPayload);
        return stream.ToArray();
    }

    private static byte[] CompressWithZlib(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(payload, 0, payload.Length);
        }

        return output.ToArray();
    }

    private static byte[] BuildSimplePex(IReadOnlyList<string> strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(0xFA57C0DEu);
        writer.Write((byte)3);
        writer.Write((byte)9);
        writer.Write((byte)2);
        writer.Write((byte)0);
        writer.Write((ulong)0);
        WritePexString(writer, "sample.psc");
        WritePexString(writer, "tester");
        WritePexString(writer, "machine");
        writer.Write((ushort)strings.Count);
        foreach (var value in strings)
        {
            WritePexString(writer, value);
        }

        return stream.ToArray();
    }

    private static void WriteSubrecord(BinaryWriter writer, string signature, byte[] payload)
    {
        writer.Write(Encoding.ASCII.GetBytes(signature));
        writer.Write((ushort)payload.Length);
        writer.Write(payload);
    }

    private static void WritePexString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }
}

