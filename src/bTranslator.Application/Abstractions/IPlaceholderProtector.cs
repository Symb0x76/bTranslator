using bTranslator.Domain.Models;

namespace bTranslator.Application.Abstractions;

public interface IPlaceholderProtector
{
    ProtectedText Protect(string input);
    string Restore(string input, PlaceholderMap map);
}

