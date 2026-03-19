using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.Runtime.Serialization;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    public class MainFormTests
    {
        private static ServiceProvider BuildProvider(Dictionary<string, string?>? overrides = null)
        {
            var services = new ServiceCollection();

            var defaultConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:IsUiTestHarness"] = "false",
                    ["UI:UseSyncfusionDocking"] = "false",
                    ["UI:ShowRibbon"] = "true",
                    ["UI:ShowStatusBar"] = "true"
                })
                .Build();

            var configuration = overrides == null
                ? defaultConfig
                : new ConfigurationBuilder().AddInMemoryCollection(overrides).Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging(builder => builder.AddDebug());

            // Minimal services used by MainForm and MainViewModel
            services.AddSingleton(ReportViewerLaunchOptions.Disabled);
            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");
            themeMock.Setup(t => t.ApplyTheme(It.IsAny<string>())).Callback<string>(theme =>
            {
                // Avoid mutating global theme during tests
            });
            services.AddSingleton<IThemeService>(themeMock.Object);
            services.AddSingleton<IWindowStateService>(Mock.Of<IWindowStateService>());
            services.AddSingleton<IFileImportService>(Mock.Of<IFileImportService>());

            // Register MainViewModel dependencies with benign defaults so InitializeAsync can run
            services.AddScoped<IDashboardService>(sp =>
            {
                var mock = new Mock<IDashboardService>();
                mock.Setup(s => s.GetDashboardItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Enumerable.Empty<DashboardItem>());
                return mock.Object;
            });
            services.AddScoped<IAILoggingService>(sp => Mock.Of<IAILoggingService>());
            services.AddScoped<IQuickBooksService>(sp => Mock.Of<IQuickBooksService>());
            services.AddScoped<IGlobalSearchService>(sp => Mock.Of<IGlobalSearchService>());
            services.AddScoped<WileyWidget.WinForms.ViewModels.MainViewModel>();

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        }

        /// <summary>
        /// Test helper — a small test subclass to expose protected/private behaviors via reflection wrappers.
        /// </summary>
        private sealed class TestMainForm : MainForm
        {
            public TestMainForm(IServiceProvider sp, IConfiguration configuration, ILogger<MainForm> logger,
                ReportViewerLaunchOptions reportViewerLaunchOptions, IThemeService themeService, IWindowStateService windowStateService,
                IFileImportService fileImportService, SyncfusionControlFactory controlFactory)
                : base(sp, configuration, logger, reportViewerLaunchOptions, themeService, windowStateService, fileImportService, controlFactory)
            {
            }

            public new int GetQATItemCount() => base.GetQATItemCount();
            public new void SaveCurrentLayout() => base.SaveCurrentLayout();
            public new void ResetLayout() => base.ResetLayout();

            public void CallOnLoad() => typeof(MainForm).GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, new object[] { EventArgs.Empty });
            public void CallOnShown() => typeof(MainForm).GetMethod("OnShown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, new object[] { EventArgs.Empty });
            public bool CallProcessCmdKey(Keys keys)
            {
                var mi = typeof(MainForm).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
                if (mi == null) return false;
                var msg = Message.Create(Handle, 0, IntPtr.Zero, IntPtr.Zero);
                object[] args = { msg, keys };
                var result = mi.Invoke(this, args);
                return result is bool b && b;
            }

            public void CallInitializeChrome() => typeof(MainForm).GetMethod("InitializeChrome", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, null);
            public void CallInitializeMdiManager() => typeof(MainForm).GetMethod("InitializeMDIManager", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, null);
            public void CallSaveMainFormState(AppStateSerializer serializer) => typeof(MainForm).GetMethod("SaveMainFormState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, new object[] { serializer });
            public void CallSaveMdiDocumentState(AppStateSerializer serializer) => typeof(MainForm).GetMethod("SaveMDIDocumentState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, new object[] { serializer });

            public Task CallInitializeAsync(CancellationToken ct)
            {
                var mi = typeof(MainForm).GetMethod("InitializeAsync", BindingFlags.Instance | BindingFlags.Public);
                var result = mi?.Invoke(this, new object[] { ct }) as Task;
                return result ?? Task.CompletedTask;
            }

            public Task CallProcessDroppedFiles(string[] files)
            {
                var mi = typeof(MainForm).GetMethod("ProcessDroppedFiles", BindingFlags.Instance | BindingFlags.NonPublic);
                var result = mi?.Invoke(this, new object[] { files, CancellationToken.None }) as Task;
                return result ?? Task.CompletedTask;
            }

            public Task? DeferredInitializationTask
            {
                get => typeof(MainForm).GetField("_deferredInitializationTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this) as Task;
            }

            public object? GetPrivateField(string name) => typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this);
            public void SetPrivateField(string name, object? value)
            {
                var field = typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                    throw new ArgumentException($"Field '{name}' not found in MainForm.", nameof(name));
                field.SetValue(this, value);
            }
        }

        private static TItem? FindToolStripItem<TItem>(Syncfusion.Windows.Forms.Tools.RibbonControlAdv? ribbon, string name)
            where TItem : ToolStripItem
        {
            if (ribbon?.Header?.MainItems == null)
            {
                return null;
            }

            foreach (var tab in ribbon.Header.MainItems.OfType<Syncfusion.Windows.Forms.Tools.ToolStripTabItem>())
            {
                if (tab.Panel == null)
                {
                    continue;
                }

                foreach (var strip in tab.Panel.Controls.OfType<Syncfusion.Windows.Forms.Tools.ToolStripEx>())
                {
                    var found = FindToolStripItem<TItem>(strip.Items, name);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static TItem? FindToolStripItem<TItem>(ToolStripItemCollection items, string name)
            where TItem : ToolStripItem
        {
            foreach (ToolStripItem item in items)
            {
                if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) && item is TItem typedItem)
                {
                    return typedItem;
                }

                if (item is ToolStripDropDownItem dropDown)
                {
                    var found = FindToolStripItem<TItem>(dropDown.DropDownItems, name);
                    if (found != null)
                    {
                        return found;
                    }
                }

                if (item is Syncfusion.Windows.Forms.Tools.ToolStripPanelItem panelItem)
                {
                    var found = FindToolStripItem<TItem>(panelItem.Items, name);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private sealed class ProbeThemableControl : UserControl, WileyWidget.WinForms.Controls.Base.IThemable
        {
            public string? AppliedTheme { get; private set; }

            public void ApplyTheme(string themeName)
            {
                AppliedTheme = themeName;
            }
        }

        private static void PumpMessages(int timeoutMs, int intervalMs = 25)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                Application.DoEvents();
                Thread.Sleep(intervalMs);
            }

            Application.DoEvents();
        }

        [StaFact]
        public void ConstructorAndOnLoad_SetsFormTitleAndWindowState()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var windowMock = new Mock<IWindowStateService>();
            windowMock.Setup(w => w.RestoreWindowState(It.IsAny<Form>())).Callback<Form>(f => f.WindowState = System.Windows.Forms.FormWindowState.Maximized);

            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, windowMock.Object, Mock.Of<IFileImportService>(), new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            // Act - initialize chrome explicitly (OnLoad defers chrome to OnShown)
            var _ = form.Handle; // ensure handle created (required by ValidateInitializationState)
            form.CallInitializeChrome();
            form.CallOnLoad();
            form.Show();
            PumpMessages(100);

            // Assert
            form.Text.Should().Be(MainFormResources.FormTitle);
            windowMock.Verify(w => w.RestoreWindowState(form), Times.Once);

            form.Dispose();
        }

        [StaFact]
        public void InitializeChrome_CreatesRibbonAndStatusBar()
        {
            // Arrange
            TestThemeHelper.EnsureOffice2019Colorful();
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");
            themeMock.Setup(t => t.ApplyTheme(It.IsAny<string>())).Callback<string>(theme =>
            {
                TestThemeHelper.EnsureOffice2019Colorful();
            });

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>(), new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            // Apply theme to form
            SfSkinManager.SetVisualStyle(form, "Office2019Colorful");

            var _ = form.Handle;

            // Act
            form.CallInitializeChrome();
            form.CallOnLoad();
            form.Show();

            // Assert: _ribbon and _statusBar internal fields exist and are added to Controls
            var ribbon = form.GetPrivateField("_ribbon");
            var statusBar = form.GetPrivateField("_statusBar");

            ribbon.Should().NotBeNull("Ribbon should be initialized in OnLoad");
            statusBar.Should().NotBeNull("StatusBar should be initialized in OnLoad");

            // Ribbon should be docked to top (try to inspect its Dock property if available)
            var ribbonControl = ribbon as Syncfusion.Windows.Forms.Tools.RibbonControlAdv;
            if (ribbonControl != null)
            {
                ribbonControl.Dock.ToString().Should().MatchRegex("Top|Fill");

                var strips = ribbonControl.Header?.MainItems
                    .OfType<Syncfusion.Windows.Forms.Tools.ToolStripTabItem>()
                    .SelectMany(tab => tab.Panel?.Controls.OfType<Syncfusion.Windows.Forms.Tools.ToolStripEx>() ?? Enumerable.Empty<Syncfusion.Windows.Forms.Tools.ToolStripEx>())
                    .ToList();

                strips.Should().NotBeNull();
                strips.Should().NotBeEmpty();
                strips!.Where(strip => strip.Items.Count > 0)
                    .Should().OnlyContain(strip => !string.IsNullOrWhiteSpace(strip.CollapsedDropDownButtonText));
            }

            form.Dispose();
        }

        [StaFact]
        public void InitializeChrome_UiTestRuntime_PreservesRibbonSafeAppearance()
        {
            TestThemeHelper.EnsureOffice2019Colorful();

            var provider = BuildProvider(new Dictionary<string, string?>
            {
                ["UI:IsUiTestHarness"] = "true",
                ["UI:UseSyncfusionDocking"] = "false",
                ["UI:ShowRibbon"] = "true",
                ["UI:ShowStatusBar"] = "true"
            });

            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>(), new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            var _ = form.Handle;
            form.CallInitializeChrome();
            form.CallOnLoad();

            var ribbon = form.GetPrivateField("_ribbon") as Syncfusion.Windows.Forms.Tools.RibbonControlAdv;
            ribbon.Should().NotBeNull();

            ribbon!.ShowCaption.Should().BeFalse();
            ribbon.ShowRibbonDisplayOptionButton.Should().BeFalse();
            ribbon.MenuButtonVisible.Should().BeFalse();
            ribbon.MenuButtonEnabled.Should().BeFalse();

            form.Dispose();
        }

        [StaFact]
        public void InitializeChrome_DefaultRuntime_CreatesUnifiedNavigationDropdown()
        {
            TestThemeHelper.EnsureOffice2019Colorful();

            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");

            using var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>(), new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            _ = form.Handle;
            form.CallInitializeChrome();

            var ribbon = form.GetPrivateField("_ribbon") as Syncfusion.Windows.Forms.Tools.RibbonControlAdv;
            var unifiedDropDown = FindToolStripItem<ToolStripDropDownButton>(ribbon, "Nav_UnifiedDropdown");
            var accountsMenuItem = FindToolStripItem<ToolStripMenuItem>(ribbon, "NavMenuItem_MunicipalAccounts");
            var jarvisMenuItem = FindToolStripItem<ToolStripMenuItem>(ribbon, "NavMenuItem_JARVISChat");

            ribbon.Should().NotBeNull();
            unifiedDropDown.Should().NotBeNull();
            accountsMenuItem.Should().NotBeNull();
            jarvisMenuItem.Should().NotBeNull();
        }

        [StaFact]
        public void InitializeChrome_HideLegacyRibbonNavigation_ShowsOnlyHomeTabWithUnifiedDropdown()
        {
            TestThemeHelper.EnsureOffice2019Colorful();

            var provider = BuildProvider(new Dictionary<string, string?>
            {
                ["UI:HideLegacyRibbonNavigation"] = "true",
                ["UI:ShowUnifiedNavigationDropdown"] = "true"
            });

            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");

            using var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>(), new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            _ = form.Handle;
            form.CallInitializeChrome();

            var ribbon = form.GetPrivateField("_ribbon") as Syncfusion.Windows.Forms.Tools.RibbonControlAdv;
            var tabNames = ribbon!.Header.MainItems.OfType<Syncfusion.Windows.Forms.Tools.ToolStripTabItem>().Select(tab => tab.Name).ToList();
            var unifiedDropDown = FindToolStripItem<ToolStripDropDownButton>(ribbon, "Nav_UnifiedDropdown");
            var legacyHomeButton = FindToolStripItem<ToolStripButton>(ribbon, "Nav_EnterpriseVitalSigns");
            var legacyFinancialsButton = FindToolStripItem<ToolStripButton>(ribbon, "Nav_MunicipalAccounts");

            tabNames.Should().Contain("HomeTab");
            tabNames.Should().Contain("LayoutTab");
            tabNames.Should().NotContain("FinancialsTab");
            tabNames.Should().NotContain("AnalyticsTab");
            tabNames.Should().NotContain("UtilitiesTab");
            tabNames.Should().NotContain("AdministrationTab");
            unifiedDropDown.Should().NotBeNull();
            legacyHomeButton.Should().BeNull();
            legacyFinancialsButton.Should().BeNull();
        }

        [StaFact]
        public async Task OnShown_ResolvesAndInitializesMainViewModel_WhenCalled()
        {
            var previousTestsFlag = Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS");
            var previousUiTestsFlag = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");
            Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "false");
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "false");

            try
            {
                // Arrange
                var configOverrides = new Dictionary<string, string?>
                {
                    ["UI:UseSyncfusionDocking"] = "false",
                    ["UI:IsUiTestHarness"] = "false",
                    ["UI:ShowRibbon"] = "false",
                    ["UI:ShowStatusBar"] = "false"
                };

                var provider = BuildProvider(configOverrides);
                var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
                var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

                // Provide a dashboard service that returns known data to observe InitializeAsync impact
                var dashboardMock = new Mock<IDashboardService>();
                var sampleItems = new List<DashboardItem>
            {
                new DashboardItem { Title = "A", Value = "1", Category = "activity" }
            };
                dashboardMock.Setup(d => d.GetDashboardDataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(sampleItems);

                // rebuild provider with test dashboard
                var services = new ServiceCollection();
                services.AddSingleton<IConfiguration>(configuration);
                services.AddLogging(builder => builder.AddDebug());
                services.AddSingleton(ReportViewerLaunchOptions.Disabled);
                services.AddSingleton<IThemeService>(Mock.Of<IThemeService>(m => m.CurrentTheme == "Office2019Colorful"));
                services.AddSingleton<IWindowStateService>(Mock.Of<IWindowStateService>());
                services.AddSingleton<IFileImportService>(Mock.Of<IFileImportService>());
                services.AddScoped<IDashboardService>(_ => dashboardMock.Object);
                services.AddScoped<IAILoggingService>(_ => Mock.Of<IAILoggingService>());
                services.AddScoped<IQuickBooksService>(_ => Mock.Of<IQuickBooksService>());
                services.AddScoped<IGlobalSearchService>(_ => Mock.Of<IGlobalSearchService>());
                services.AddScoped<WileyWidget.WinForms.ViewModels.MainViewModel>();

                var testProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

                var form = new TestMainForm(testProvider, configuration, logger, ReportViewerLaunchOptions.Disabled, Mock.Of<IThemeService>(), Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>(), new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

                // Act: run the real WinForms show lifecycle so OnShown executes in-order.
                var _ = form.Handle;
                form.Show();
                Application.DoEvents();

                // Wait for deferred initialization to be assigned and completed
                Task? deferred = null;
                for (int i = 0; i < 40; i++) // ~2 seconds max
                {
                    PumpMessages(75);
                    deferred = form.DeferredInitializationTask;
                    if (deferred != null) break;
                }
                deferred.Should().NotBeNull();

                var timeoutAt = DateTime.UtcNow.AddSeconds(5);
                while (!deferred!.IsCompleted && DateTime.UtcNow < timeoutAt)
                {
                    PumpMessages(75);
                }

                if (!deferred.IsCompleted)
                {
                    throw new TimeoutException("Deferred initialization timed out");
                }

                await deferred;

                // Trigger data load since MainViewModel uses lazy loading
                if (form.MainViewModel != null)
                {
                    await form.MainViewModel.OnVisibilityChangedAsync(true);
                }

                // Assert: MainViewModel resolved and initialized with sample items
                form.MainViewModel.Should().NotBeNull();
                form.MainViewModel!.ActivityItems.Should().ContainSingle();

                form.Dispose();
            }
            finally
            {
                Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", previousTestsFlag);
                Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previousUiTestsFlag);
            }
        }

        [StaFact(Skip = "Flaky in headless STA testhost (message pump starvation/host cancellation). Covered by integration tests for MainForm initialization and docking theming.")]
        public async Task InitializeAsync_LoadsPanelsAndAppliesTheme_WhenDockingAndPanelNavigatorPresent()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");

            var windowMock = new Mock<IWindowStateService>();
            windowMock.Setup(w => w.LoadMru()).Returns(new List<string> { "file1", "file2" });

            var panelNavMock = new Mock<IPanelNavigationService>();
            var dashboardRequested = false;
            panelNavMock.Setup(p => p.ShowPanel<WileyWidget.WinForms.Controls.Panels.EnterpriseVitalSignsPanel>(It.IsAny<string>(), It.IsAny<Syncfusion.Windows.Forms.Tools.DockingStyle>(), It.IsAny<bool>()))
                .Callback(() => dashboardRequested = true)
                .Verifiable();
            // Also accept overload with parameters object (null passed in production code)
            panelNavMock.Setup(p => p.ShowPanel<WileyWidget.WinForms.Controls.Panels.EnterpriseVitalSignsPanel>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<Syncfusion.Windows.Forms.Tools.DockingStyle>(), It.IsAny<bool>()))
                .Callback(() => dashboardRequested = true)
                .Verifiable();

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, windowMock.Object, Mock.Of<IFileImportService>(), new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            // Create control handle to prevent Invoke hanging
            form.CreateControl();
            // Force handle creation for forms
            var handle = form.Handle;

            // Use real docking initialization path; injecting a minimal DockingManager can block readiness checks.
            form.SetPrivateField("_panelNavigator", panelNavMock.Object);
            form.SetPrivateField("_uiConfig", new UIConfiguration { UseSyncfusionDocking = true, AutoShowDashboard = true });

            var initializeDocking = typeof(MainForm).GetMethod("InitializeSyncfusionDocking", BindingFlags.Instance | BindingFlags.NonPublic)!;
            initializeDocking.Invoke(form, null);
            form.SetPrivateField("_syncfusionDockingInitialized", true);

            // Load MRU list into private field by calling private LoadMruList via reflection
            var loadMru = typeof(MainForm).GetMethod("LoadMruList", BindingFlags.Instance | BindingFlags.NonPublic)!;
            loadMru.Invoke(form, null);

            // Act
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var initializeTask = form.CallInitializeAsync(cts.Token);
            var timeoutAt = DateTime.UtcNow.AddSeconds(12);
            while (!dashboardRequested && DateTime.UtcNow < timeoutAt)
            {
                Application.DoEvents();
                await Task.Delay(25);
            }

            if (!dashboardRequested)
            {
                throw new TimeoutException("InitializeAsync did not request dashboard panel within timeout window.");
            }

            cts.Cancel();
            await System.Threading.Tasks.Task.WhenAny(initializeTask, System.Threading.Tasks.Task.Delay(2000));

            // Assert
            // Verify the overload with parameters (null) was called during initialization
            panelNavMock.Verify(p => p.ShowPanel<WileyWidget.WinForms.Controls.Panels.EnterpriseVitalSignsPanel>(It.IsAny<string>(), It.IsAny<Syncfusion.Windows.Forms.Tools.DockingStyle>(), It.IsAny<bool>()), Times.AtLeastOnce);

            var mruList = form.GetPrivateField("_mruList") as List<string>;
            mruList.Should().NotBeNull();
            mruList!.Should().Contain("file1");

            form.Dispose();
        }

        [StaFact]
        public void ToggleTheme_UpdatesThemeButtonText_WhenThemeServiceRaised()
        {
            // Arrange
            TestThemeHelper.EnsureOffice2019Colorful();
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var themeMock = new Mock<IThemeService>();
            var currentTheme = "Office2019Colorful";
            themeMock.SetupGet(t => t.CurrentTheme).Returns(() => currentTheme);
            themeMock.Setup(t => t.ApplyTheme(It.IsAny<string>())).Callback<string>(theme =>
            {
                currentTheme = theme;
                TestThemeHelper.EnsureOffice2019Colorful();
                themeMock.Raise(tm => tm.ThemeChanged += null, themeMock.Object, theme);
            });

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>(), new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            // Apply theme to form
            SfSkinManager.SetVisualStyle(form, "Office2019Colorful");

            form.CreateControl();
            var _ = form.Handle;
            // Initialize chrome through normal lifecycle
            form.CallInitializeChrome();
            form.CallOnLoad();
            form.Show();
            form.PerformLayout();
            PumpMessages(100);

            // Pre-assert: Theme control exists (legacy ThemeToggle button or newer ThemeCombo selector)
            var ribbon = form.GetPrivateField("_ribbon") as Syncfusion.Windows.Forms.Tools.RibbonControlAdv;
            var themeControl = FindToolStripItem<ToolStripItem>(ribbon, "ThemeToggle")
                ?? FindToolStripItem<ToolStripItem>(ribbon, "ThemeCombo");
            themeControl.Should().NotBeNull("Theme control should be present after OnLoad");

            // Act: Toggle theme
            form.ToggleTheme();
            PumpMessages(100);

            themeMock.Verify(t => t.ApplyTheme("Office2019Dark"), Times.AtLeastOnce);

            var activeThemeControl = FindToolStripItem<ToolStripItem>(ribbon, "ThemeToggle")
                ?? FindToolStripItem<ToolStripItem>(ribbon, "ThemeCombo")
                ?? themeControl;

            // Assert: after ThemeChanged event, UI reflects Office2019Dark
            if (string.Equals(activeThemeControl!.Name, "ThemeToggle", StringComparison.OrdinalIgnoreCase))
            {
                activeThemeControl.Text.Should().Match("*Light*", "Theme toggle text should reflect new theme state");
            }
            else if (activeThemeControl is Syncfusion.Windows.Forms.Tools.ToolStripComboBoxEx comboEx)
            {
                comboEx.ComboBox.Text.Should().Be("Office2019Dark", "Theme combo selection should reflect the active theme after a toggle");
            }
            else
            {
                throw new Xunit.Sdk.XunitException($"Unexpected theme control name '{activeThemeControl.Name}'");
            }

            form.Dispose();
        }

        [StaFact]
        public void OnThemeServiceChanged_ReplaysThemeToOwnedForms_AndThemableChildren()
        {
            TestThemeHelper.EnsureOffice2019Colorful();

            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");

            var form = new TestMainForm(
                provider,
                configuration,
                logger,
                ReportViewerLaunchOptions.Disabled,
                themeMock.Object,
                Mock.Of<IWindowStateService>(),
                Mock.Of<IFileImportService>(),
                new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            SfSkinManager.SetVisualStyle(form, "Office2019Colorful");

            var _ = form.Handle;
            form.CallInitializeChrome();
            form.CallOnLoad();
            form.Show();
            Application.DoEvents();

            using var ownedForm = new Form
            {
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-2000, -2000)
            };

            form.AddOwnedForm(ownedForm);

            var probe = new ProbeThemableControl { Dock = DockStyle.Fill };
            ownedForm.Controls.Add(probe);
            ownedForm.Show();
            PumpMessages(100);

            var onThemeServiceChanged = typeof(MainForm).GetMethod("OnThemeServiceChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
            onThemeServiceChanged.Invoke(form, new object?[] { form, "Office2019Dark" });
            PumpMessages(100);

            probe.AppliedTheme.Should().Be("Office2019Dark", "owned floating forms should replay runtime theme changes to their themable children");

            ownedForm.Close();
            form.Dispose();
        }

        [StaFact]
        public async Task ProcessDroppedFiles_ShowsErrorDialog_OnImportException()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var loggerMock = new Mock<ILogger<MainForm>>();

            var fileImportMock = new Mock<IFileImportService>();
            fileImportMock.Setup(f => f.ImportDataAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("bad file"));

            var windowMock = new Mock<IWindowStateService>();
            windowMock.Setup(w => w.AddToMru(It.IsAny<string>()));
            windowMock.Setup(w => w.LoadMru()).Returns(new List<string>());

            var form = new TestMainForm(provider, configuration, loggerMock.Object, ReportViewerLaunchOptions.Disabled, Mock.Of<IThemeService>(), windowMock.Object, fileImportMock.Object, new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            // Create temp csv file
            var tmp = System.IO.Path.GetTempFileName() + ".csv";
            System.IO.File.WriteAllText(tmp, "a,b,c\n1,2,3");

            // Act
            await form.CallProcessDroppedFiles(new[] { tmp });

            // Assert: logger should have recorded an error due to import exception
            loggerMock.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);

            // Cleanup
            try { System.IO.File.Delete(tmp); } catch { }
            form.Dispose();
        }

        [StaFact]
        public void ProcessCmdKey_ReturnsTrue_AndFocusesSearch_OnCtrlF()
        {
            // Arrange
            TestThemeHelper.EnsureOffice2019Colorful();
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");
            themeMock.Setup(t => t.ApplyTheme(It.IsAny<string>())).Callback<string>(theme =>
            {
                TestThemeHelper.EnsureOffice2019Colorful();
            });

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>(), new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            // Apply theme to form
            SfSkinManager.SetVisualStyle(form, "Office2019Colorful");

            var _ = form.Handle;
            form.CallInitializeChrome();
            form.CallOnLoad();

            // Act
            var result = form.CallProcessCmdKey(Keys.Control | Keys.F);

            // Assert
            result.Should().BeTrue("ProcessCmdKey should return true for handled shortcut");

            form.Dispose();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types")]
        [StaFact]
        public void OnFirstChanceException_IgnoresThemeExceptions_AndLogsOthers()
        {
            // Arrange
            var provider = BuildProvider(new Dictionary<string, string?>
            {
                ["Diagnostics:VerboseFirstChanceExceptions"] = "true"
            });
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var loggerMock = new Mock<ILogger<MainForm>>();

            var form = new TestMainForm(provider, configuration, loggerMock.Object, ReportViewerLaunchOptions.Disabled, Mock.Of<IThemeService>(), Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>(), new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            // Act & Assert for theme exception (should be ignored)
            var themeEx = new NullReferenceException("Theme error", new ArgumentException("SfSkinManager"));
            var onFirstChance = typeof(MainForm).GetMethod("MainForm_FirstChanceException", BindingFlags.Instance | BindingFlags.NonPublic);
            onFirstChance?.Invoke(form, new object[] { form, new System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs(themeEx) });

            // Act & Assert for other exception (should log)
            var otherEx = new InvalidOperationException("Other error");
            onFirstChance?.Invoke(form, new object[] { form, new System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs(otherEx) });

            loggerMock.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);

            form.Dispose();
        }

        [StaFact(Skip = "Obsolete: RightPanelMode enum no longer exists")]
        public void SwitchRightPanel_ToJarvisChat_SelectsTab_AndLogs()
        {
            // Arrange
            TestThemeHelper.EnsureOffice2019Colorful();
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var loggerMock = new Mock<ILogger<MainForm>>();

            var form = new TestMainForm(provider, configuration, loggerMock.Object, ReportViewerLaunchOptions.Disabled, Mock.Of<IThemeService>(), Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>(), new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            var _ = form.Handle;
            form.Visible = true;
            form.CallOnLoad();

            // Real GradientPanelExt for right panel
            var rightPanel = new Syncfusion.Windows.Forms.Tools.GradientPanelExt(); // { Tag = RightDockPanelFactory.RightPanelMode.ActivityLog };
            form.SetPrivateField("_rightDockPanel", rightPanel);

            // Act
            var switchMethod = typeof(MainForm).GetMethod("SwitchRightPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            // switchMethod?.Invoke(form, new object[] { RightDockPanelFactory.RightPanelMode.JarvisChat });

            // Assert
            // loggerMock.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);

            form.Dispose();
        }

        [StaFact]
        public void LayoutPersistence_SavesOpenDocumentIdentity_WithoutPersistingNativeMdiGeometry()
        {
            TestThemeHelper.EnsureOffice2019Colorful();
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");

            using var form = new TestMainForm(
                provider,
                configuration,
                logger,
                ReportViewerLaunchOptions.Disabled,
                themeMock.Object,
                Mock.Of<IWindowStateService>(),
                Mock.Of<IFileImportService>(),
                new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            var tempFile = Path.Combine(Path.GetTempPath(), $"mdi-layout-{Guid.NewGuid():N}.xml");

            try
            {
                _ = form.Handle;
                form.CallInitializeMdiManager();

                using var child = new Form
                {
                    Text = "Document A",
                    MdiParent = form,
                    ShowInTaskbar = false
                };

                child.Show();
                Application.DoEvents();

                var serializer = new AppStateSerializer(SerializeMode.XMLFile, tempFile);
                form.CallSaveMainFormState(serializer);
                form.CallSaveMdiDocumentState(serializer);
                serializer.PersistNow();

                var xml = File.ReadAllText(tempFile);
                var persistedSerializer = new AppStateSerializer(SerializeMode.XMLFile, tempFile);
                var openPanelsObject = persistedSerializer.DeserializeObject("MDI.OpenPanels", Array.Empty<string>());
                var openPanels = (string[])(openPanelsObject ?? Array.Empty<string>());

                xml.Should().Contain("MDI.OpenPanels");
                openPanels.Should().Contain("Document A");
                xml.Should().NotContain("MainForm.IsMDIContainer");
                xml.Should().NotContain("MDIDoc.");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
