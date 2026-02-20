using bTranslator.Domain.Models;

namespace bTranslator.Application.Abstractions;

public interface IXmlCompatibilityService
{
    Task<IReadOnlyList<TranslationItem>> ImportAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task ExportAsync(
        string path,
        IEnumerable<TranslationItem> items,
        int formatVersion,
        CancellationToken cancellationToken = default);
}

