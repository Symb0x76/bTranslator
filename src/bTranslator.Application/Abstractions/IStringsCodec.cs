using System.Text;
using bTranslator.Domain.Enums;
using bTranslator.Domain.Models;

namespace bTranslator.Application.Abstractions;

public interface IStringsCodec
{
    Task<IReadOnlyList<StringsEntry>> ReadAsync(
        string path,
        StringsFileKind kind,
        Encoding encoding,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        string path,
        StringsFileKind kind,
        IEnumerable<StringsEntry> entries,
        Encoding encoding,
        CancellationToken cancellationToken = default);
}

