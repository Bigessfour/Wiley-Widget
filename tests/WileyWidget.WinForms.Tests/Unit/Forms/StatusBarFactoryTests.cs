using System.Drawing;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.Windows.Forms.Tools;
using Xunit;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Configuration;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    public class StatusBarFactoryTests
    {
        [StaFact]
        public void CreateStatusBar_AddsExpectedPanels_And_ProgressControl()
        {
            // Arrange
            var logger = NullLogger.Instance;
            var form = new MainFormStub();

            // Act
            var statusBar = StatusBarFactory.CreateStatusBar(form, logger, useSyncfusionDocking: true);

            // Assert basic structure
            statusBar.Should().NotBeNull();
            var controls = statusBar!.Controls;
            controls.Count.Should().BeGreaterThanOrEqualTo(5);

            // Panels by name
            var names = controls.Cast<StatusBarAdvPanel>().Select(p => p.Name).ToList();
            names.Should().Contain(new[] { "StatusLabel", "StatusTextPanel", "StatePanel", "ProgressPanel", "ClockPanel" });

            // Progress panel contains ProgressBarAdv named statusBarProgressBar
            var progressPanel = controls.Cast<StatusBarAdvPanel>().First(p => p.Name == "ProgressPanel");
            progressPanel.Controls.Cast<object>().Should().ContainSingle();
            var prog = progressPanel.Controls.Cast<object>().First() as ProgressBarAdv;
            prog.Should().NotBeNull();
            prog!.Visible.Should().BeFalse();
            prog.Minimum.Should().Be(0);
            prog.Maximum.Should().Be(100);

            // Theming is handled by SfSkinManager; avoid asserting platform-dependent colors here.
        }

        // Small lightweight MainForm stub sufficient for status bar factory tests
        private sealed class MainFormStub : MainForm
        {
            public MainFormStub()
                : base(
                      new ServiceCollection().BuildServiceProvider(),
                      new ConfigurationBuilder().Build(),
                      NullLogger<MainForm>.Instance,
                      ReportViewerLaunchOptions.Disabled,
                      new TestThemeService(),
                      new TestWindowStateService(),
                      new TestFileImportService())
            {
            }
        }

        private sealed class TestThemeService : IThemeService
        {
            public event EventHandler<string> ThemeChanged = delegate { };
            public string CurrentTheme => "Office2019Colorful";
            public bool IsDark => false;
            public void ApplyTheme(string themeName) => ThemeChanged?.Invoke(this, themeName);
        }

        private sealed class TestWindowStateService : IWindowStateService
        {
            public void RestoreWindowState(System.Windows.Forms.Form form) { }
            public void SaveWindowState(System.Windows.Forms.Form form) { }
            public System.Collections.Generic.List<string> LoadMru() => new();
            public void SaveMru(System.Collections.Generic.List<string> mruList) { }
            public void AddToMru(string filePath) { }
            public void ClearMru() { }
        }

        private sealed class TestFileImportService : IFileImportService
        {
            public System.Threading.Tasks.Task<Result<T>> ImportDataAsync<T>(string filePath, System.Threading.CancellationToken ct = default) where T : class
                => System.Threading.Tasks.Task.FromResult(Result<T>.Failure("Not implemented in test"));

            public System.Threading.Tasks.Task<Result> ValidateImportFileAsync(string filePath, System.Threading.CancellationToken ct = default)
                => System.Threading.Tasks.Task.FromResult(Result.Failure("Not implemented in test"));
        }
    }
}
