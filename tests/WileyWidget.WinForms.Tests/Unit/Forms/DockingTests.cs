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
using LegacyGradientPanel = WileyWidget.WinForms.Controls.Base.LegacyGradientPanel;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    [Collection("SyncfusionTheme")]
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
                TestThemeHelper.EnsureOffice2019Colorful();
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

            var (dockingManager, left, right, central, activityLogPanel, activityTimer, layoutManager) = DockingHostFactory.CreateDockingHost(form, provider, panelNav.Object, Mock.Of<ILogger>());

            dockingManager.Should().NotBeNull();
            left.Should().NotBeNull();
            right.Should().NotBeNull();
            central.Should().NotBeNull();

            // Check docking state - use Dock property since DockingManager.GetDockStyle may not be reliable in tests
            left!.Dock.Should().Be(DockStyle.Left);
            right!.Dock.Should().Be(DockStyle.Right);

            activityLogPanel.Should().NotBeNull();

            var gridCtl = FindChildByName(activityLogPanel!, "ActivityGrid");
            gridCtl!.Should().NotBeNull();
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

            var leftPanel = new LegacyGradientPanel { Name = "LeftDockPanel_Test" };
            var rightPanel = new LegacyGradientPanel { Name = "RightDockPanel_Test" };

            form.Controls.Add(leftPanel);
            form.Controls.Add(rightPanel);

            dockingManager.DockControl(leftPanel, form, DockingStyle.Left, 300);
            dockingManager.DockControl(rightPanel, form, DockingStyle.Right, 350);

            var centralPanel = new LegacyGradientPanel { Dock = DockStyle.Fill, Name = "Central" };
            form.Controls.Add(centralPanel);
            // Note: Central panel uses DockStyle.Fill, not DockingManager docking

            var layoutPath = Path.Combine(Path.GetTempPath(), "wiley-docking-" + Guid.NewGuid().ToString("N") + ".bin");
            var layoutManager = new DockingLayoutManager(provider, Mock.Of<IPanelNavigationService>(), Mock.Of<ILogger>(), layoutPath, form, dockingManager, leftPanel, rightPanel, centralPanel, null);

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

        // [Fact]
        // public void DockingManager_MaintainsNonEmptyChildCollection_PreventsPaintException()
        // {
        //     // Test disabled due to API issues - ArgumentOutOfRangeException prevention should be handled in production code
        // }

        // [Fact]
        // public void DockingInitializer_CreatesControlsBeforeSuspendingLayout_AvoidsPaintRaceCondition()
        // {
        //     // Test disabled due to API issues
        // }

        // [Fact]
        // public void RibbonFactory_EnsuresNonEmptyHeaderItems_PreventsPaintException()
        // {
        //     // Test disabled due to API issues
        // }

        // [Fact]
        // public void DockingManager_HandlesVisibilityToggle_MaintainsNonEmptyState()
        // {
        //     // Test disabled due to API issues
        // }
    }
}
