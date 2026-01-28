using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Services;
using Xunit;

namespace WileyWidget.ViewModels.Tests
{
    public class SettingsViewModelTests
    {
        private readonly Mock<ILogger<SettingsViewModel>> _loggerMock;
        private readonly Mock<ISettingsService> _settingsServiceMock;
        private readonly Mock<IThemeService> _themeServiceMock;

        public SettingsViewModelTests()
        {
            _loggerMock = new Mock<ILogger<SettingsViewModel>>();
            _settingsServiceMock = new Mock<ISettingsService>();
            _themeServiceMock = new Mock<IThemeService>();
        }

        [Fact]
        public void Constructor_InitializesWithDefaults()
        {
            var vm = new SettingsViewModel(_loggerMock.Object, _settingsServiceMock.Object, _themeServiceMock.Object);

            vm.AppTitle.Should().Be("Wiley Widget Settings");
            vm.SelectedTheme.Should().Be("Office2019Colorful");
            vm.EnableAi.Should().BeFalse();
        }

        [Fact]
        public void ValidateSettings_WithValidSettings_ReturnsTrue()
        {
            var vm = new SettingsViewModel(_loggerMock.Object, _settingsServiceMock.Object, _themeServiceMock.Object)
            {
                AppTitle = "Test App",
                DateFormat = "MM/dd/yyyy",
                CurrencyFormat = "C2",
                DefaultExportPath = @"C:\Exports"
            };

            var result = vm.ValidateSettings();

            result.Should().BeTrue();
            vm.GetValidationSummary().Should().BeEmpty();
        }

        [Fact]
        public void ValidateSettings_WithEmptyAppTitle_ReturnsFalse()
        {
            var vm = new SettingsViewModel(_loggerMock.Object, _settingsServiceMock.Object, _themeServiceMock.Object)
            {
                AppTitle = string.Empty,
                DateFormat = "MM/dd/yyyy",
                CurrencyFormat = "C2",
                DefaultExportPath = @"C:\Exports"
            };

            var result = vm.ValidateSettings();

            result.Should().BeFalse();
            vm.GetValidationSummary().Should().Contain("Application title cannot be empty.");
        }

        [Fact]
        public void ValidateSettings_WithAiEnabled_RequiresApiKey()
        {
            var vm = new SettingsViewModel(_loggerMock.Object, _settingsServiceMock.Object, _themeServiceMock.Object)
            {
                AppTitle = "Test App",
                DateFormat = "MM/dd/yyyy",
                CurrencyFormat = "C2",
                DefaultExportPath = @"C:\Exports",
                EnableAi = true,
                XaiApiKey = string.Empty,
                XaiApiEndpoint = "https://api.x.ai/v1/chat/completions"
            };

            var result = vm.ValidateSettings();

            result.Should().BeFalse();
            vm.GetValidationSummary().Should().Contain("API key is required when AI is enabled.");
        }

        [Fact]
        public void ValidateSettings_WithAiEnabled_RequiresValidEndpoint()
        {
            var vm = new SettingsViewModel(_loggerMock.Object, _settingsServiceMock.Object, _themeServiceMock.Object)
            {
                AppTitle = "Test App",
                DateFormat = "MM/dd/yyyy",
                CurrencyFormat = "C2",
                DefaultExportPath = @"C:\Exports",
                EnableAi = true,
                XaiApiKey = "test-key",
                XaiApiEndpoint = "invalid-url"
            };

            var result = vm.ValidateSettings();

            result.Should().BeFalse();
            vm.GetValidationSummary().Should().Contain("Valid XAI API endpoint is required when AI is enabled.");
        }

        [Fact]
        public void ValidateSettings_WithAiDisabled_SkipsAiValidation()
        {
            var vm = new SettingsViewModel(_loggerMock.Object, _settingsServiceMock.Object, _themeServiceMock.Object)
            {
                AppTitle = "Test App",
                DateFormat = "MM/dd/yyyy",
                CurrencyFormat = "C2",
                DefaultExportPath = @"C:\Exports",
                EnableAi = false,
                XaiApiKey = string.Empty,
                XaiApiEndpoint = "invalid-url"
            };

            var result = vm.ValidateSettings();

            result.Should().BeTrue();
        }
    }
}