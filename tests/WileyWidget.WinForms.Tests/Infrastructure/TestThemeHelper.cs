using Syncfusion.WinForms.Controls;

namespace WileyWidget.WinForms.Tests.Infrastructure;

internal static class TestThemeHelper
{
    internal static void EnsureOffice2019Colorful()
    {
        if (string.IsNullOrWhiteSpace(SfSkinManager.ApplicationVisualTheme))
        {
            SfSkinManager.ApplicationVisualTheme = "Office2019Colorful";
        }
    }
}
