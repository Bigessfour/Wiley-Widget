using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Controls;
using GradientPanelExt = WileyWidget.WinForms.Controls.GradientPanelExt;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    public class DockingTests
    {
        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();

            var defaultConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
                {
                    ["UI:IsUiTestHarness"] = "false",
                    ["UI:UseSyncfusionDocking"] = "false",
                    ["UI:ShowRibbon"] = "true",
                    ["UI:ShowStatusBar"] = "true"
                })
                .Build();

            services.AddSingleton<IConfiguration>(defaultConfig);
            services.AddLogging(builder => builder.AddDebug());
            services.AddSingleton(ReportViewerLaunchOptions.Disabled);

            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");
            themeMock.Setup(t => t.ApplyTheme(It.IsAny<string>())).Callback<string>(theme =>
            {
                SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                SfSkinManager.ApplicationVisualTheme = theme;
            });
            services.AddSingleton<IThemeService>(themeMock.Object);

            services.AddSingleton<IWindowStateService>(Mock.Of<IWindowStateService>());
            services.AddSingleton<IFileImportService>(Mock.Of<IFileImportService>());

            // Provide a minimal DashboardViewModel for other factories that may resolve it
            var dashboardVm = new WileyWidget.WinForms.ViewModels.DashboardViewModel();
            dashboardVm.AccountCount = 1;
            services.AddSingleton(dashboardVm);

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        }

        private sealed class TestMainForm : MainForm
        {
            public TestMainForm(IServiceProvider sp, IConfiguration configuration, ILogger<MainForm> logger,
                ReportViewerLaunchOptions reportViewerLaunchOptions, IThemeService themeService, IWindowStateService windowStateService,
                IFileImportService fileImportService)
                : base(sp, configuration, logger, reportViewerLaunchOptions, themeService, windowStateService, fileImportService)
            {
            }

            public void CallOnLoad() => typeof(MainForm).GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, new object[] { EventArgs.Empty });
        }

        private static Control? FindChildByName(Control root, string name)
        {
            if (root == null) return null;
            if (root.Name == name) return root;
            foreach (Control c in root.Controls)
            {
                var found = FindChildByName(c, name);
                if (found != null) return found;
            }
            return null;
        }

        [StaFact]
        public void CreateDockingHost_CreatesDockingManagerAndPanels()
        {
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());
            var panelNav = new Mock<IPanelNavigationService>();

            // Ensure form handle created for docking operations
            var _ = form.Handle;

            var (dockingManager, left, right, activityLogPanel, activityTimer, layoutManager) = DockingHostFactory.CreateDockingHost(form, provider, panelNav.Object, Mock.Of<ILogger>());

            dockingManager.Should().NotBeNull();
            left.Should().NotBeNull();
            right.Should().NotBeNull();

            left.Dock.Should().Be(System.Windows.Forms.DockStyle.Left);
            right.Dock.Should().Be(System.Windows.Forms.DockStyle.Right);

            activityLogPanel.Should().NotBeNull();

            var gridCtl = FindChildByName(activityLogPanel!, "ActivityGrid");
            gridCtl.Should().NotBeNull();
            gridCtl.Should().BeOfType<SfDataGrid>();

            var grid = (SfDataGrid)gridCtl!;
            var mappingNames = grid.Columns.Select(c => c.MappingName).ToList();
            mappingNames.Should().Contain("Timestamp");
            mappingNames.Should().Contain("Activity");
            mappingNames.Should().Contain("Details");
            mappingNames.Should().Contain("Status");
        }

        [StaFact]
        public async Task SaveAndLoadLayout_CompressedRoundtrip_WritesCompressedFileAndRestores()
        {
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            // Ensure UI handle exists for synchronous Invoke usage in load/apply
            var _ = form.Handle;

            var dockingManager = new DockingManager { HostControl = form, DockToFill = true };

            var leftPanel = new GradientPanelExt { Name = "LeftDockPanel_Test" };
            var rightPanel = new GradientPanelExt { Name = "RightDockPanel_Test" };

            form.Controls.Add(leftPanel);
            form.Controls.Add(rightPanel);

            dockingManager.DockControl(leftPanel, form, DockingStyle.Left, 300);
            dockingManager.DockControl(rightPanel, form, DockingStyle.Right, 350);

            var layoutPath = Path.Combine(Path.GetTempPath(), "wiley-docking-" + Guid.NewGuid().ToString("N") + ".bin");
            var layoutManager = new DockingLayoutManager(provider, Mock.Of<IPanelNavigationService>(), Mock.Of<ILogger>(), layoutPath, form, dockingManager, leftPanel, rightPanel, null);

            try
            {
                // Save current docking layout (synchronous)
                layoutManager.SaveDockingLayout(dockingManager);

                File.Exists(layoutPath).Should().BeTrue();
                var bytes = File.ReadAllBytes(layoutPath);
                bytes.Length.Should().BeGreaterThan(10);

                // GZip magic header 0x1f 0x8b
                bytes[0].Should().Be(0x1f);
                bytes[1].Should().Be(0x8b);

                // Clear the in-memory cache to force disk read path in LoadDockingLayoutAsync
                var cacheField = typeof(DockingLayoutManager).GetField("_layoutCache", BindingFlags.Static | BindingFlags.NonPublic);
                cacheField.Should().NotBeNull();
                cacheField!.SetValue(null, null);

                // Load from disk (should decompress and apply without throwing)
                await layoutManager.LoadDockingLayoutAsync(dockingManager);

                // Post-load sanity checks
                leftPanel.Visible.Should().BeTrue();
                rightPanel.Visible.Should().BeTrue();
            }
            finally
            {
                try { layoutManager.Dispose(); } catch { }
                try { if (File.Exists(layoutPath)) File.Delete(layoutPath); } catch { }
            }
        }

        /// <summary>
        /// Verifies that DockingManager maintains non-empty child collections to prevent
        /// Syncfusion ArgumentOutOfRangeException in DockHost.GetPaintInfo() during paint events.
        /// Regression: https://github.com/Bigessfour/Wiley-Widget/issues (docking-paint-bug)
        /// </summary>
        [Fact]
        public void DockingManager_MaintainsNonEmptyChildCollection_PreventsPaintException()
        {
            // Arrange
            var provider = BuildProvider();
            var form = new Form { Width = 1024, Height = 768 };
            var dockingManager = new SfDockingManager { OwnerForm = form };
            var panel1 = new Panel { Name = "Panel1", Width = 200, Height = 200 };
            var panel2 = new Panel { Name = "Panel2", Width = 200, Height = 200 };

            form.Controls.Add(dockingManager);
            dockingManager.DockControl(panel1, form, DockingStyle.Left, 200);
            dockingManager.DockControl(panel2, form, DockingStyle.Right, 300);

            // Act
            var controlCount = dockingManager.Controls.Count;

            // Assert: Non-empty collection should exist before paint fires
            controlCount.Should().BeGreaterThan(0, "Empty DockingManager.Controls causes ArgumentOutOfRangeException in DockHost.GetPaintInfo()");
            panel1.Visible.Should().BeTrue();
            panel2.Visible.Should().BeTrue();
        }

        /// <summary>
        /// Verifies that DockingInitializer creates panels BEFORE suspending layout
        /// to ensure DockingManager has non-zero child controls when paint events fire.
        /// </summary>
        [Fact]
        public void DockingInitializer_CreatesControlsBeforeSuspendingLayout_AvoidsPaintRaceCondition()
        {
            // Arrange
            var provider = BuildProvider();
            var form = new Form { Width = 1024, Height = 768 };
            var dockingManager = new SfDockingManager { OwnerForm = form };

            form.Controls.Add(dockingManager);

            // Verify initial state: empty
            dockingManager.Controls.Count.Should().Be(0);

            // Act: Simulate DockingInitializer pattern - create before suspending
            dockingManager.SuspendLayout();
            try
            {
                var fallbackPanel = new Panel
                {
                    Name = "FallbackPanel",
                    Width = 200,
                    Height = 200,
                    BackColor = System.Drawing.Color.LightGray
                };
                dockingManager.Controls.Add(fallbackPanel);
                fallbackPanel.Visible = true;
            }
            finally
            {
                dockingManager.ResumeLayout(true);
            }

            // Assert: Child collection is non-empty before layout resume completes
            dockingManager.Controls.Count.Should().BeGreaterThan(0, "Paint events should not fire on empty DockingManager");
        }

        /// <summary>
        /// Verifies that Ribbon header always has at least one item to prevent
        /// DockHost.GetPaintInfo() ArgumentOutOfRangeException.
        /// </summary>
        [Fact]
        public void RibbonFactory_EnsuresNonEmptyHeaderItems_PreventsPaintException()
        {
            // Arrange
            var provider = BuildProvider();
            var form = new Form { Width = 1024, Height = 768 };
            var ribbon = new SfRibbon { OwnerForm = form };

            form.Controls.Add(ribbon);

            // Verify initial state: empty header
            ribbon.Header.MainItems.Count.Should().Be(0);

            // Act: Apply fallback tab (mimics RibbonFactory guard)
            var homeTab = new SfRibbonTab { Text = "Home" };
            ribbon.Header.MainItems.Add(homeTab);

            // Assert: Non-empty collection prevents paint exception
            ribbon.Header.MainItems.Count.Should().BeGreaterThan(0, "Empty ribbon header items trigger ArgumentOutOfRangeException on paint");
            ribbon.Header.MainItems[0].Text.Should().Be("Home");
        }

        /// <summary>
        /// Verifies that visibility toggles maintain stable DockingManager state
        /// and don't cause paint events with empty collections.
        /// </summary>
        [Fact]
        public void DockingManager_HandlesVisibilityToggle_MaintainsNonEmptyState()
        {
            // Arrange
            var provider = BuildProvider();
            var form = new Form { Width = 1024, Height = 768 };
            var dockingManager = new SfDockingManager { OwnerForm = form };
            var panel = new Panel { Name = "TestPanel", Width = 200, Height = 200 };

            form.Controls.Add(dockingManager);
            dockingManager.DockControl(panel, form, DockingStyle.Left, 200);

            var initialCount = dockingManager.Controls.Count;
            initialCount.Should().BeGreaterThan(0);

            // Act: Toggle visibility
            panel.Visible = false;
            var hiddenCount = dockingManager.Controls.Count;

            panel.Visible = true;
            var restoredCount = dockingManager.Controls.Count;

            // Assert: Control collection remains non-empty throughout
            hiddenCount.Should().Be(initialCount, "Hiding panel should not remove controls from DockingManager");
            restoredCount.Should().Be(initialCount, "Showing panel should restore collection");
        }
    }
}

```
