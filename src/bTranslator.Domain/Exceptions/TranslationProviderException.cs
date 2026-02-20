using bTranslator.Domain.Enums;

namespace bTranslator.Domain.Exceptions;

public sealed class TranslationProviderException : Exception
{
    public TranslationProviderException(
        string providerId,
        TranslationErrorKind errorKind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderId = providerId;
        ErrorKind = errorKind;
    }

    public string ProviderId { get; }
    public TranslationErrorKind ErrorKind { get; }
}

