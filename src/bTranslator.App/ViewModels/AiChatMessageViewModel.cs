using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace bTranslator.App.ViewModels;

public sealed class AiChatMessageViewModel
{
    public AiChatMessageViewModel(
        string role,
        string roleDisplayName,
        string messageText,
        DateTimeOffset timestamp)
    {
        Role = role;
        RoleDisplayName = roleDisplayName;
        MessageText = messageText;
        TimestampText = timestamp.ToLocalTime().ToString("HH:mm:ss");

        var isUser = string.Equals(role, UserRole, StringComparison.OrdinalIgnoreCase);
        BubbleAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        BubbleBrush = new SolidColorBrush(
            isUser
                ? ColorHelper.FromArgb(0x33, 0x38, 0xBD, 0xF8)
                : ColorHelper.FromArgb(0x26, 0x94, 0xA3, 0xB8));
    }

    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    public string Role { get; }

    public string RoleDisplayName { get; }

    public string MessageText { get; }

    public string TimestampText { get; }

    public HorizontalAlignment BubbleAlignment { get; }

    public SolidColorBrush BubbleBrush { get; }
}
