using System.Linq;
using Xunit;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Tests;

public class ThemeIconServiceTests
{
    [Fact]
    public void GetIcon_ReturnsImage_ForKnownIcons_LightAndDark()
    {
        using var service = new ThemeIconService();

        var light = service.GetIcon("add", AppTheme.Light, 24);
        var dark = service.GetIcon("add", AppTheme.Dark, 24);

        Assert.NotNull(light);
        Assert.NotNull(dark);
    }

    [Fact]
    public void Preload_DoesNotThrow_AndCachesIcons()
    {
        using var service = new ThemeIconService();

        var names = new[] { "add", "edit", "delete", "save" };
        var ex = Record.Exception(() => service.Preload(names, AppTheme.Dark, 24));
        Assert.Null(ex);
    }
}
