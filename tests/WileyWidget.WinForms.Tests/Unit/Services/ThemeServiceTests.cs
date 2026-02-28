using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Syncfusion.WinForms.Controls;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Services;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services
{
    public sealed class ThemeServiceTests : System.IDisposable
    {
        private readonly string? _originalTheme;

        public ThemeServiceTests()
        {
            _originalTheme = SfSkinManager.ApplicationVisualTheme;
        }

        public void Dispose()
        {
            SfSkinManager.ApplicationVisualTheme = _originalTheme;
            System.GC.SuppressFinalize(this);
        }

        [StaFact]
        public void ApplyTheme_SetsApplicationVisualTheme_RaisesEvent_AndPersistsToSettings()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:Theme"] = "Office2019Colorful"
                })
                .Build();

            var settings = new Mock<ISettingsService>();
            var appSettings = new AppSettings { Theme = "Office2019Colorful" };
            settings.SetupGet(s => s.Current).Returns(appSettings);

            var service = new ThemeService(configuration, NullLogger<ThemeService>.Instance, settings.Object);

            string? raisedTheme = null;
            service.ThemeChanged += (_, theme) => raisedTheme = theme;

            // Act
            service.ApplyTheme("Office2019Dark");

            // Assert
            service.CurrentTheme.Should().Be("Office2019Dark");
            SfSkinManager.ApplicationVisualTheme.Should().Be("Office2019Dark");
            raisedTheme.Should().Be("Office2019Dark");
            appSettings.Theme.Should().Be("Office2019Dark");
            settings.Verify(s => s.Save(), Times.Once);
        }
    }
}
