using Xunit;
using Moq;
using FluentAssertions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Tests.Unit.ViewModels
{
    public sealed class SettingsViewModelTests
    {
        private readonly Mock<ILogger<SettingsViewModel>> _logger = new();

        [Fact]
        public async Task LoadCommand_PopulatesXaiSettingsFromSettingsService()
        {
            // Arrange
            var appSettings = new AppSettings
            {
                EnableAI = true,
                XaiApiKey = "test-key-123",
                XaiApiEndpoint = "https://api.x.ai/v1",
                XaiModel = "grok-beta",
                XaiTimeout = 45,
                XaiMaxTokens = 1200,
                XaiTemperature = 0.4
            };

            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.LoadAsync()).Returns(Task.CompletedTask);
            mockSettings.SetupGet(s => s.Current).Returns(appSettings);

            var vm = new SettingsViewModel(_logger.Object, mockSettings.Object);

            // Act
            await vm.LoadCommand.ExecuteAsync(null);

            // Assert
            vm.EnableAi.Should().BeTrue();
            vm.XaiApiKey.Should().Be("test-key-123");
            vm.XaiApiEndpoint.Should().Be("https://api.x.ai/v1");
            vm.XaiModel.Should().Be("grok-beta");
            vm.XaiTimeout.Should().Be(45);
            vm.XaiMaxTokens.Should().Be(1200);
            vm.XaiTemperature.Should().BeApproximately(0.4, 0.0001);
        }

        [Fact]
        public void SaveCommand_PersistsSettings_ToSettingsService()
        {
            // Arrange
            var appSettings = new AppSettings();
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.SetupGet(s => s.Current).Returns(appSettings);

            var vm = new SettingsViewModel(_logger.Object, mockSettings.Object);

            // Act
            vm.EnableAi = true;
            vm.XaiApiKey = "persist-key";
            vm.XaiApiEndpoint = "https://api.x.ai/v1";
            vm.XaiModel = "grok-4-0709";
            vm.XaiTimeout = 20;
            vm.XaiMaxTokens = 800;
            vm.XaiTemperature = 0.25;

            vm.SaveCommand.Execute(null);

            // Assert
            mockSettings.Verify(s => s.Save(), Times.Once);
            mockSettings.Object.Current.EnableAI.Should().BeTrue();
            mockSettings.Object.Current.XaiApiKey.Should().Be("persist-key");
            mockSettings.Object.Current.XaiApiEndpoint.Should().Be("https://api.x.ai/v1");
            mockSettings.Object.Current.XaiModel.Should().Be("grok-4-0709");
            mockSettings.Object.Current.XaiTimeout.Should().Be(20);
            mockSettings.Object.Current.XaiMaxTokens.Should().Be(800);
            mockSettings.Object.Current.XaiTemperature.Should().BeApproximately(0.25, 0.0001);
        }

        [Fact]
        public void ResetAiCommand_ResetsToDefaultsAndMarksDirty()
        {
            // Arrange
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.SetupGet(s => s.Current).Returns(new AppSettings());

            var vm = new SettingsViewModel(_logger.Object, mockSettings.Object);

            // Make non-default values
            vm.XaiApiKey = "foo";
            vm.XaiModel = "grok-x";
            vm.XaiApiEndpoint = "http://bad";
            vm.XaiTimeout = 99;
            vm.XaiMaxTokens = 5000;
            vm.XaiTemperature = 0.9;
            vm.HasUnsavedChanges.Should().BeFalse();

            // Act
            vm.ResetAiCommand.Execute(null);

            // Assert
            vm.XaiApiKey.Should().BeEmpty();
            vm.XaiModel.Should().Be("grok-4-0709");
            vm.XaiApiEndpoint.Should().Be("https://api.x.ai/v1");
            vm.XaiTimeout.Should().Be(30);
            vm.XaiMaxTokens.Should().Be(2000);
            vm.XaiTemperature.Should().BeApproximately(0.7, 0.0001);
            vm.HasUnsavedChanges.Should().BeTrue();
        }
    }
}
