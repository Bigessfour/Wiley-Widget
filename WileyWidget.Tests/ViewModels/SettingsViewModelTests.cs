using FluentAssertions;
using Moq;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.Configuration;
using Xunit;

namespace WileyWidget.Tests.ViewModels;

public class SettingsViewModelTests
{

    [Fact]
    public void Load_PopulatesPropertiesFromService()
    {
        // Arrange
        var settings = SettingsService.Instance;
        settings.ResetForTests();
        settings.Current.XaiTimeoutSeconds = 55;
        settings.Current.XaiCacheTtlMinutes = 77;
        settings.Current.XaiDailyBudget = 12.34m;
        settings.Current.XaiMonthlyBudget = 987.65m;
        settings.Current.Theme = "FluentDark";
        settings.Current.XaiModel = "grok-beta";

        var apiFacade = new Mock<IApiKeyFacade>();
        apiFacade.Setup(f => f.Info()).Returns(new ApiKeyInfo { IsValid = true });
        var themeCoord = new Mock<IThemeCoordinator>();
        themeCoord.Setup(c => c.Current).Returns("FluentDark");

        var vm = new SettingsViewModel(settings, apiFacade.Object, themeCoord.Object);

        // Act
        vm.LoadCommand.Execute(null);

        // Assert
        vm.TimeoutSeconds.Should().Be(55);
        vm.CacheTtlMinutes.Should().Be(77);
        vm.DailyBudget.Should().Be(12.34m);
        vm.MonthlyBudget.Should().Be(987.65m);
        vm.SelectedTheme.Should().Be("FluentDark");
        vm.SelectedModel.Should().Be("grok-beta");
        vm.StatusMessage.Should().Contain("securely");
    }

    [Fact]
    public void Save_PersistsChanges()
    {
        var settings = SettingsService.Instance;
        settings.ResetForTests();
        var apiFacade = new Mock<IApiKeyFacade>();
        apiFacade.Setup(f => f.Info()).Returns(new ApiKeyInfo());
        var themeCoord = new Mock<IThemeCoordinator>();
        themeCoord.SetupProperty(c => c.Current, "FluentLight");

        var vm = new SettingsViewModel(settings, apiFacade.Object, themeCoord.Object)
        {
            TimeoutSeconds = 99,
            CacheTtlMinutes = 123,
            DailyBudget = 45.67m,
            MonthlyBudget = 890.12m,
            SelectedTheme = "FluentLight",
            SelectedModel = "grok-vision-beta"
        };

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        settings.Current.XaiTimeoutSeconds.Should().Be(99);
        settings.Current.XaiCacheTtlMinutes.Should().Be(123);
        settings.Current.XaiDailyBudget.Should().Be(45.67m);
        settings.Current.XaiMonthlyBudget.Should().Be(890.12m);
        settings.Current.Theme.Should().Be("FluentLight");
        settings.Current.XaiModel.Should().Be("grok-vision-beta");
        vm.StatusMessage.Should().Contain("saved");
    }
}
