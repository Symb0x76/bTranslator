namespace bTranslator.App.ViewModels;

public sealed class OptionItem
{
    public OptionItem(string value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public string Value { get; }

    public string DisplayName { get; }
}
