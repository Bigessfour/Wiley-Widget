using System;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;
using FluentAssertions;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    [Trait("Category", "Unit")]
    [Collection(WinFormsUiCollection.CollectionName)]
    public class DockingHostFactoryTests
    {
        private readonly WinFormsUiThreadFixture _ui;

        public DockingHostFactoryTests(WinFormsUiThreadFixture ui)
        {
            _ui = ui;
        }

        [Fact]
        public void CreateDockingHost_WithNullMainForm_ThrowsArgumentNullException()
        {
            var sp = new ServiceCollection().BuildServiceProvider();

            Action act = () => DockingHostFactory.CreateDockingHost(null!, sp, null, NullLogger.Instance);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void CreateDockingHost_ReturnsExpectedComponents()
        {
            var sp = new ServiceCollection().BuildServiceProvider();

            _ui.Run(() =>
            {
                // Configure for test harness to avoid desktop dependencies
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["UI:IsUiTestHarness"] = "true",
                        ["UI:MinimumFormSize"] = "800,600",
                        ["UI:UseSyncfusionDocking"] = "true"
                    })
                    .Build();

                using var mainForm = new MainForm(sp, config, NullLogger<MainForm>.Instance, ReportViewerLaunchOptions.Disabled);

                var navigator = new Mock<IPanelNavigationService>();
                var (dm, leftPanel, rightPanel, activityGrid, activityTimer) = DockingHostFactory.CreateDockingHost(mainForm, sp, navigator.Object, NullLogger.Instance);

                dm.Should().NotBeNull();
                leftPanel.Should().NotBeNull();
                rightPanel.Should().NotBeNull();
                activityGrid.Should().NotBeNull();
                activityTimer.Should().NotBeNull();

                mainForm.IsHandleCreated.Should().BeTrue("CreateDockingHost should force MainForm handle creation");

                // Dock enablement should be true after configuration
                dm.GetEnableDocking(leftPanel).Should().BeTrue();
                dm.GetEnableDocking(rightPanel).Should().BeTrue();

                // Caption and button visibility
                dm.ShowCaption.Should().BeTrue();
                dm.ShowCaptionImages.Should().BeTrue();
                dm.MaximizeButtonEnabled.Should().BeTrue();

                dm.GetCloseButtonVisibility(leftPanel).Should().BeTrue();
                dm.GetAutoHideButtonVisibility(leftPanel).Should().BeTrue();
                dm.GetMenuButtonVisibility(leftPanel).Should().BeTrue();

                dm.GetCloseButtonVisibility(rightPanel).Should().BeTrue();
                dm.GetAutoHideButtonVisibility(rightPanel).Should().BeTrue();
                dm.GetMenuButtonVisibility(rightPanel).Should().BeTrue();

                leftPanel.Name.Should().Be("LeftDockPanel");
                rightPanel.Name.Should().Be("RightDockPanel");
            });
        }

        [Fact]
        public void CreateDockingHost_ActivityGrid_PopulatesFallbackData()
        {
            var sp = new ServiceCollection().BuildServiceProvider();

            Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme = null; // Ensure defensive theme application path

            Syncfusion.WinForms.DataGrid.SfDataGrid? grid = null;

            _ui.Run(() =>
            {
                // Configure for test harness to avoid desktop dependencies
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["UI:IsUiTestHarness"] = "true",
                        ["UI:MinimumFormSize"] = "800,600",
                        ["UI:UseSyncfusionDocking"] = "true"
                    })
                    .Build();

                using var mainForm = new MainForm(sp, config, NullLogger<MainForm>.Instance, ReportViewerLaunchOptions.Disabled);
                var (dm, leftPanel, rightPanel, activityGrid, activityTimer) = DockingHostFactory.CreateDockingHost(mainForm, sp, null, NullLogger.Instance);
                grid = activityGrid;
                // At this point LoadActivityDataAsync is started in the background; actual population happens asynchronously
            });

            // Poll until grid.DataSource is populated or timeout
            var deadline = DateTime.UtcNow.AddSeconds(5);
            bool populated = false;
            while (DateTime.UtcNow < deadline)
            {
                _ui.Run(() =>
                {
                    if (grid != null && grid.DataSource != null)
                    {
                        populated = true;
                    }
                });

                if (populated) break;
                Thread.Sleep(100);
            }

            populated.Should().BeTrue("Fallback activity data should populate the activity grid when repository is not available");
        }

        [Fact]
        public void EnsureCaptionButtonsVisible_EnablesButtonsForDockedPanels()
        {
            var sp = new ServiceCollection().BuildServiceProvider();

            _ui.Run(() =>
            {
                // Configure for test harness to avoid desktop dependencies
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["UI:IsUiTestHarness"] = "true",
                        ["UI:MinimumFormSize"] = "800,600",
                        ["UI:UseSyncfusionDocking"] = "true"
                    })
                    .Build();

                using var mainForm = new MainForm(sp, config, NullLogger<MainForm>.Instance, ReportViewerLaunchOptions.Disabled);
                var navigator = new Mock<IPanelNavigationService>();
                var (dm, leftPanel, rightPanel, activityGrid, activityTimer) = DockingHostFactory.CreateDockingHost(mainForm, sp, navigator.Object, NullLogger.Instance);

                // Simulate a state where LoadDockState may have cleared button visibility
                try { dm.SetCloseButtonVisibility(leftPanel, false); dm.SetAutoHideButtonVisibility(leftPanel, false); dm.SetMenuButtonVisibility(leftPanel, false); } catch { }
                try { dm.SetCloseButtonVisibility(rightPanel, false); dm.SetAutoHideButtonVisibility(rightPanel, false); dm.SetMenuButtonVisibility(rightPanel, false); } catch { }

                // Invoke the private method via reflection to simulate post-load repair
                var layoutManager = new DockingLayoutManager(sp, null, NullLogger.Instance);
                var method = typeof(DockingLayoutManager).GetMethod("EnsureCaptionButtonsVisible", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method.Invoke(layoutManager, new object[] { dm, mainForm });

                // Verify buttons are visible again
                dm.GetCloseButtonVisibility(leftPanel).Should().BeTrue();
                dm.GetAutoHideButtonVisibility(leftPanel).Should().BeTrue();
                dm.GetMenuButtonVisibility(leftPanel).Should().BeTrue();

                dm.GetCloseButtonVisibility(rightPanel).Should().BeTrue();
                dm.GetAutoHideButtonVisibility(rightPanel).Should().BeTrue();
                dm.GetMenuButtonVisibility(rightPanel).Should().BeTrue();
            });
        }
    }
}
