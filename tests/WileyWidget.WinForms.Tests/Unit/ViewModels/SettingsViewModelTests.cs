using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void Constructor_ExposesOnlySupportedThemes()
    {
        var logger = new Mock<ILogger<SettingsViewModel>>();

        var viewModel = new SettingsViewModel(logger.Object);

        viewModel.AvailableThemes.Should().Equal(ThemeColors.GetSupportedThemes());
        viewModel.AvailableThemes.Should().NotContain("FluentDark");
        viewModel.AvailableThemes.Should().NotContain("Bootstrap5Dark");
        viewModel.AvailableThemes.Should().NotContain("Office2019White");
    }

    [Fact]
    public void Constructor_NormalizesThemeServiceCurrentTheme_ToSupportedTheme()
    {
        var logger = new Mock<ILogger<SettingsViewModel>>();
        var themeService = new Mock<IThemeService>();
        themeService.SetupGet(service => service.CurrentTheme).Returns("FluentDark");

        var viewModel = new SettingsViewModel(
            logger.Object,
            settingsService: Mock.Of<ISettingsService>(),
            themeService: themeService.Object);

        viewModel.SelectedTheme.Should().Be(ThemeColors.DefaultTheme);
        viewModel.AvailableThemes.Should().Contain(viewModel.SelectedTheme);
    }

    [Fact]
    public void ValidateSettings_WhenExportPathWasCleared_RestoresDefaultExportPath()
    {
        var logger = new Mock<ILogger<SettingsViewModel>>();
        var viewModel = new SettingsViewModel(logger.Object)
        {
            EnableAi = false,
            DefaultExportPath = string.Empty
        };

        var isValid = viewModel.ValidateSettings();

        isValid.Should().BeTrue();
        viewModel.DefaultExportPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Save_WhenExportPathIsBlank_PersistsDefaultExportPath()
    {
        var logger = new Mock<ILogger<SettingsViewModel>>();
        var settingsState = new AppSettings { DefaultExportPath = string.Empty };
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Current).Returns(settingsState);

        var viewModel = new SettingsViewModel(
            logger.Object,
            settingsService: settingsService.Object)
        {
            EnableAi = false,
            DefaultExportPath = string.Empty
        };

        viewModel.SaveCommand.Execute(null);

        settingsService.Verify(service => service.Save(), Times.Once);
        settingsState.DefaultExportPath.Should().NotBeNullOrWhiteSpace();
    }
}
