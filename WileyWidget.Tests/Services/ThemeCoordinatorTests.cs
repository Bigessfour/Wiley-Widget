using FluentAssertions;
using WileyWidget.Services;
using WileyWidget.Configuration;
using Xunit;
using System;

namespace WileyWidget.Tests.Services;

public class ThemeCoordinatorTests
{
    [Fact]
    public void Themes_ExposesList_And_CurrentReflectsChange()
    {
        var settings = SettingsService.Instance; settings.ResetForTests();
        var themeService = new WileyWidget.Services.ThemeService();
        var coord = new ThemeCoordinator(settings, themeService);
        Assert.NotNull(coord.Themes);
        
        // Just test that setting Current works
        coord.Current = "FluentLight";
        Assert.Equal("FluentLight", coord.Current);
        Assert.Equal("FluentLight", settings.Current.Theme);
    }
}
