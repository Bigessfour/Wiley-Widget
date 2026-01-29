using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Themes;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Themes
{
    /// <summary>
    /// Tests for DockingManagerThemeAdapter to ensure it uses ThemeColors.DefaultTheme
    /// instead of hardcoded "Default" string.
    /// </summary>
    public class DockingManagerThemeAdapterTests : IDisposable
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly DockingManager _dockingManager;
        private readonly DockingManagerThemeAdapter _adapter;

        public DockingManagerThemeAdapterTests()
        {
            _loggerMock = new Mock<ILogger>();
            _dockingManager = new DockingManager();
            _adapter = new DockingManagerThemeAdapter(_dockingManager, _loggerMock.Object);
        }

        [Fact]
        public void ApplyTheme_WhenThemeNameIsNull_ShouldUseThemeColorsDefaultTheme()
        {
            // Act
            _adapter.ApplyTheme(null);

            // Assert
            // The adapter should have attempted to apply ThemeColors.DefaultTheme ("Office2019White")
            // which maps to VisualStyle.Office2007 based on the ThemeMap
            _dockingManager.VisualStyle.Should().Be(Syncfusion.Windows.Forms.VisualStyle.Office2007,
                "because null theme should fallback to ThemeColors.DefaultTheme which is Office2019White");
        }

        [Fact]
        public void ApplyTheme_WhenThemeNameIsEmpty_ShouldUseThemeColorsDefaultTheme()
        {
            // Act
            _adapter.ApplyTheme("");

            // Assert
            _dockingManager.VisualStyle.Should().Be(Syncfusion.Windows.Forms.VisualStyle.Office2007,
                "because empty theme should fallback to ThemeColors.DefaultTheme which is Office2019White");
        }

        [Fact]
        public void ApplyTheme_WhenThemeNameIsWhitespace_ShouldUseThemeColorsDefaultTheme()
        {
            // Act
            _adapter.ApplyTheme("   ");

            // Assert
            _dockingManager.VisualStyle.Should().Be(Syncfusion.Windows.Forms.VisualStyle.Office2007,
                "because whitespace theme should fallback to ThemeColors.DefaultTheme which is Office2019White");
        }

        [Fact]
        public void ApplyTheme_WhenThemeNameIsOffice2019White_ShouldApplyOffice2007Style()
        {
            // Act
            _adapter.ApplyTheme("Office2019White");

            // Assert
            _dockingManager.VisualStyle.Should().Be(Syncfusion.Windows.Forms.VisualStyle.Office2007,
                "because Office2019White maps to Office2007 VisualStyle");
        }

        [Fact]
        public void ApplyTheme_WhenThemeNameIsUnknown_ShouldFallbackToOffice2010()
        {
            // Act
            _adapter.ApplyTheme("UnknownTheme");

            // Assert
            _dockingManager.VisualStyle.Should().Be(Syncfusion.Windows.Forms.VisualStyle.Office2010,
                "because unknown themes should fallback to Office2010");
        }

        [Fact]
        public void GetCurrentThemeName_WhenStyleIsUnknown_ShouldReturnThemeColorsDefaultTheme()
        {
            // Arrange - Set to a style that may not be in the map
            _dockingManager.VisualStyle = Syncfusion.Windows.Forms.VisualStyle.Default;

            // Act
            var themeName = _adapter.GetCurrentThemeName();

            // Assert
            themeName.Should().Be(ThemeColors.DefaultTheme,
                "because unknown styles should return ThemeColors.DefaultTheme, not hardcoded 'Default'");
        }

        [Fact]
        public void GetCurrentThemeName_WhenStyleIsOffice2007_ShouldReturnOffice2019White()
        {
            // Arrange
            _dockingManager.VisualStyle = Syncfusion.Windows.Forms.VisualStyle.Office2007;

            // Act
            var themeName = _adapter.GetCurrentThemeName();

            // Assert
            // Office2007 maps to Office2019White in the ThemeMap (first match wins)
            themeName.Should().BeOneOf("Office2019White", "FluentLight", ThemeColors.DefaultTheme,
                "because Office2007 style maps to one of these themes");
        }

        [Fact]
        public void Constructor_WhenDockingManagerIsNull_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => new DockingManagerThemeAdapter(null!, _loggerMock.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("dockingManager");
        }

        [Fact]
        public void ThemeColorsDefaultTheme_ShouldBeOffice2019White()
        {
            // This test validates the expected default theme value
            ThemeColors.DefaultTheme.Should().Be("Office2019White",
                "because the default theme should be Office2019White as defined in ThemeColors");
        }

        public void Dispose()
        {
            _dockingManager?.Dispose();
        }
    }
}
