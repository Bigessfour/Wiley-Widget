using System.Drawing;

namespace WileyWidget.WinForms.Themes;

/// <summary>
/// Helper colors tied to the Office2019Colorful theme for custom UI surfaces.
/// These semantic gradients/text colors are an approved WW0001 exception (chat-only, theme-aware) and
/// they remain static for now and are not used for global theming—SfSkinManager continues to own all other colors.
/// </summary>
internal static class AppThemeColors
{
    public static Color ChatIncomingStart => ColorTranslator.FromHtml("#E3F2FD");
    public static Color ChatIncomingEnd => ColorTranslator.FromHtml("#BBDEFB");
    public static Color ChatOutgoingStart => ColorTranslator.FromHtml("#E8F5E9");
    public static Color ChatOutgoingEnd => ColorTranslator.FromHtml("#C8E6C9");
    public static Color ChatIncomingText => ColorTranslator.FromHtml("#E3F2FD");
    public static Color ChatOutgoingText => ColorTranslator.FromHtml("#E8F5E9");
}

