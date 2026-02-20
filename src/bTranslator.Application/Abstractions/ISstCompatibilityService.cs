using bTranslator.Domain.Models;

namespace bTranslator.Application.Abstractions;

public interface ISstCompatibilityService
{
    Task<IReadOnlyList<TranslationItem>> ImportAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task ExportAsync(
        string path,
        IEnumerable<TranslationItem> items,
        int version = 8,
        CancellationToken cancellationToken = default);
}

