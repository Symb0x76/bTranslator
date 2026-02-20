using bTranslator.Domain.Models;

namespace bTranslator.Application.Abstractions;

public interface IPexToolchainService
{
    Task<PexDocument> LoadAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task ExportStringsAsync(
        PexDocument document,
        string outputPath,
        CancellationToken cancellationToken = default);
}

