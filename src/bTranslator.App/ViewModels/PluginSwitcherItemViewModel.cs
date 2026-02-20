namespace bTranslator.App.ViewModels;

public sealed class PluginSwitcherItemViewModel
{
    public required string PluginPath { get; init; }

    public required string DisplayName { get; set; }

    public string DirectoryPath { get; set; } = string.Empty;

    public bool IsPinned { get; set; }

    public DateTimeOffset LastOpenedUtc { get; set; }

    public string LastOpenedLabel => LastOpenedUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

    public string PinGlyph => IsPinned ? "\uE840" : "\uE718";
}
