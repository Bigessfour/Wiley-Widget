using Xunit;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Tests.Unit.Theming;

[Collection("SyncfusionTheme")]
public sealed class ThemeAssemblyLoadTests
{
    [StaFact]
    public void EnsureThemeAssemblyLoadedForTheme_DoesNotThrow_ForSupportedFamilies()
    {
        var ex = Record.Exception(() =>
        {
            ThemeColors.EnsureThemeAssemblyLoadedForTheme("Office2019Colorful");
            ThemeColors.EnsureThemeAssemblyLoadedForTheme("Office2016Colorful");
            ThemeColors.EnsureThemeAssemblyLoadedForTheme("HighContrastBlack");
        });

        Assert.Null(ex);
    }

    [StaFact]
    public void EnsureThemeAssemblyLoadedForTheme_DoesNotThrow_ForNullEmptyAndUnknown()
    {
        var ex = Record.Exception(() =>
        {
            ThemeColors.EnsureThemeAssemblyLoadedForTheme(null);
            ThemeColors.EnsureThemeAssemblyLoadedForTheme(string.Empty);
            ThemeColors.EnsureThemeAssemblyLoadedForTheme("UnknownThemeName");
        });

        Assert.Null(ex);
    }

    [StaFact]
    public void EnsureThemeAssemblyLoaded_IsIdempotent_OnRepeatedCalls()
    {
        var ex = Record.Exception(() =>
        {
            for (var i = 0; i < 5; i++)
            {
                ThemeColors.EnsureThemeAssemblyLoaded();
                ThemeColors.EnsureThemeAssemblyLoadedForTheme("Office2019Colorful");
            }
        });

        Assert.Null(ex);
    }
}
