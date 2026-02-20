using System.Xml.Linq;
using bTranslator.Application.Abstractions;
using bTranslator.Domain.Models;

namespace bTranslator.Infrastructure.Persistence.Compatibility;

public sealed class XmlCompatibilityService : IXmlCompatibilityService
{
    public Task<IReadOnlyList<TranslationItem>> ImportAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var doc = XDocument.Load(path);
        var items = doc
            .Descendants("String")
            .Select(static node => new TranslationItem
            {
                Id = (node.Element("EDID")?.Value ?? node.Element("REC")?.Value ?? Guid.NewGuid().ToString("N")),
                SourceText = node.Element("Source")?.Value ?? string.Empty,
                TranslatedText = node.Element("Dest")?.Value ?? string.Empty
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<TranslationItem>>(items);
    }

    public Task ExportAsync(
        string path,
        IEnumerable<TranslationItem> items,
        int formatVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = new XElement("SSTXMLRessources",
            new XElement("Params",
                new XElement("Version", formatVersion)),
            new XElement("Content",
                items.Select(item => new XElement("String",
                    new XElement("EDID", item.Id),
                    new XElement("Source", item.SourceText),
                    new XElement("Dest", item.TranslatedText ?? string.Empty)))));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        doc.Save(path);
        return Task.CompletedTask;
    }
}

