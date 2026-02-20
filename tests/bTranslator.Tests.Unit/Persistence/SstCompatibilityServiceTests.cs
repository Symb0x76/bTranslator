using bTranslator.Domain.Models;
using bTranslator.Infrastructure.Persistence.Compatibility;
using FluentAssertions;

namespace bTranslator.Tests.Unit.Persistence;

public class SstCompatibilityServiceTests
{
    [Fact]
    public async Task ExportAndImportLegacyV8_ShouldRoundTripMetadata()
    {
        var service = new SstCompatibilityService();
        var file = Path.Combine(Path.GetTempPath(), $"bTranslator-sst-{Guid.NewGuid():N}.sst");
        var items = new[]
        {
            new TranslationItem
            {
                Id = "100",
                SourceText = "Hello",
                TranslatedText = "你好",
                IsValidated = true,
                IsLocked = false,
                SstMetadata = new SstEntryMetadata
                {
                    ListIndex = 1,
                    CollaborationId = 3,
                    CollaborationLabel = "TeamA",
                    Flags = SstEntryFlags.Translated | SstEntryFlags.Validated,
                    Pointer = new SstRecordPointerLite
                    {
                        StringId = 100,
                        FormId = 0x12345678,
                        RecordSignature = "BOOK",
                        FieldSignature = "FULL",
                        Index = 2,
                        IndexMax = 7,
                        RecordHash = 0x99887766
                    }
                }
            }
        };

        await service.ExportAsync(file, items, version: 8);
        var imported = await service.ImportAsync(file);

        imported.Should().ContainSingle();
        var first = imported[0];
        first.SourceText.Should().Be("Hello");
        first.TranslatedText.Should().Be("你好");
        first.SstMetadata.Should().NotBeNull();
        first.SstMetadata!.ListIndex.Should().Be(1);
        first.SstMetadata.CollaborationId.Should().Be(3);
        first.SstMetadata.Pointer.RecordSignature.Should().Be("BOOK");
        first.SstMetadata.Pointer.FieldSignature.Should().Be("FULL");
        first.SstMetadata.Pointer.FormId.Should().Be(0x12345678);
    }
}

