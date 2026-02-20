namespace bTranslator.Infrastructure.Security.Options;

public sealed class CredentialStoreOptions
{
    public string RootDirectory { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "bTranslator");
}

