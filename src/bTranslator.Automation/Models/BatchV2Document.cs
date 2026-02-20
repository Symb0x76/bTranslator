namespace bTranslator.Automation.Models;

internal sealed class BatchV2Document
{
    public string Name { get; init; } = "Batch v2";
    public IList<BatchV2Rule> Rules { get; init; } = new List<BatchV2Rule>();
}

internal sealed class BatchV2Rule
{
    public string Search { get; init; } = string.Empty;
    public string Replace { get; init; } = string.Empty;
    public string Pattern { get; init; } = "%REPLACE% %ORIG%";
    public int Mode { get; init; } = 1;
    public int Select { get; init; } = 0;
    public bool CaseSensitive { get; init; }
    public bool AllLists { get; init; }
}

