namespace bTranslator.Infrastructure.Persistence.Options;

public sealed class PersistenceOptions
{
    public string RootDirectory { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "bTranslator");

    public string DatabaseName { get; init; } = "bTranslator.db";
}

