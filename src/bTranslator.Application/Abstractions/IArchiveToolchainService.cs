using bTranslator.Domain.Models;

namespace bTranslator.Application.Abstractions;

public interface IArchiveToolchainService
{
    Task<IReadOnlyList<ArchiveEntry>> ListEntriesAsync(
        string archivePath,
        CancellationToken cancellationToken = default);

    Task ExtractEntryAsync(
        string archivePath,
        string entryPath,
        string outputPath,
        CancellationToken cancellationToken = default);
}

