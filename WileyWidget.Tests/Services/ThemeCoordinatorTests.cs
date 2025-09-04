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
        var coord = new ThemeCoordinator(settings);
        coord.Themes.Should().NotBeNull();
        var raised = false;
        coord.ThemeChanged += (_, newTheme) => { if (!string.IsNullOrEmpty(newTheme)) raised = true; };
        var first = coord.Themes[0];
        coord.Current = first; // should trigger change event & persist
        raised.Should().BeTrue();
        settings.Current.Theme.Should().NotBeNull();
    }
}
