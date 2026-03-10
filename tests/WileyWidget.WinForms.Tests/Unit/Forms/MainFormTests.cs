using System;
using System.Collections.Generic;
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
            public void CallInitializeRibbon() => typeof(MainForm).GetMethod("InitializeRibbon", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, null);
            public bool CallShouldAutoOpenJarvisOnStartup()
                => (bool)(typeof(MainForm).GetMethod("ShouldAutoOpenJarvisOnStartup", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(this, null) ?? false);

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
                get
                {
                    var deferredField = typeof(MainForm).GetField("_deferredInitializationTask", BindingFlags.Instance | BindingFlags.NonPublic);
                    return deferredField?.GetValue(this) as Task;
                }
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

        private sealed class ProbeThemableControl : UserControl, WileyWidget.WinForms.Controls.Base.IThemable
        {
            public string? AppliedTheme { get; private set; }

            public void ApplyTheme(string themeName)
            {
                AppliedTheme = themeName;
            }
        }

        [StaFact]
        public void ShouldAutoOpenJarvisOnStartup_IsFalse_ByDefault_ForNormalInteractiveRuns()
        {
            var previousAutoOpen = Environment.GetEnvironmentVariable("WILEYWIDGET_AUTO_OPEN_JARVIS");
            var previousDisable = Environment.GetEnvironmentVariable("WILEYWIDGET_DISABLE_STARTUP_JARVIS");
            var previousUiTests = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");
            var previousUiAutomationJarvis = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS");

            Environment.SetEnvironmentVariable("WILEYWIDGET_AUTO_OPEN_JARVIS", null);
            Environment.SetEnvironmentVariable("WILEYWIDGET_DISABLE_STARTUP_JARVIS", null);
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "false");
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS", "false");

            try
            {
                using var provider = BuildProvider();
                var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
                var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
                var form = new TestMainForm(
                    provider,
                    configuration,
                    logger,
                    ReportViewerLaunchOptions.Disabled,
                    Mock.Of<IThemeService>(),
                    Mock.Of<IWindowStateService>(),
                    Mock.Of<IFileImportService>(),
                    new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

                try
                {
                    form.CallShouldAutoOpenJarvisOnStartup().Should().BeFalse();
                }
                finally
                {
                    form.Dispose();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("WILEYWIDGET_AUTO_OPEN_JARVIS", previousAutoOpen);
                Environment.SetEnvironmentVariable("WILEYWIDGET_DISABLE_STARTUP_JARVIS", previousDisable);
                Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previousUiTests);
                Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS", previousUiAutomationJarvis);
            }
        }

        [StaFact]
        public void ShouldAutoOpenJarvisOnStartup_IsTrue_WhenExplicitOverrideIsSet()
        {
            var previousAutoOpen = Environment.GetEnvironmentVariable("WILEYWIDGET_AUTO_OPEN_JARVIS");
            var previousUiTests = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");

            Environment.SetEnvironmentVariable("WILEYWIDGET_AUTO_OPEN_JARVIS", "true");
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "false");

            try
            {
                using var provider = BuildProvider();
                var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
                var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
                var form = new TestMainForm(
                    provider,
                    configuration,
                    logger,
                    ReportViewerLaunchOptions.Disabled,
                    Mock.Of<IThemeService>(),
                    Mock.Of<IWindowStateService>(),
                    Mock.Of<IFileImportService>(),
                    new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

                try
                {
                    form.CallShouldAutoOpenJarvisOnStartup().Should().BeTrue();
                }
                finally
                {
                    form.Dispose();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("WILEYWIDGET_AUTO_OPEN_JARVIS", previousAutoOpen);
                Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previousUiTests);
            }
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

            // Assert
            form.Text.Should().Be(MainFormResources.FormTitle);
            windowMock.Verify(w => w.RestoreWindowState(It.IsAny<Form>()), Times.Once,
                "OnLoad should restore persisted state through IWindowStateService");

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
            form.CallInitializeRibbon();

            // Assert: _ribbon and _statusBar internal fields exist and are added to Controls
            var ribbon = form.GetPrivateField("_ribbon");
            var statusBar = form.GetPrivateField("_statusBar");

            ribbon.Should().NotBeNull("Ribbon should be initialized after deferred OnShown path");
            statusBar.Should().NotBeNull("StatusBar should be initialized in OnLoad");

            // Ribbon should be docked to top (try to inspect its Dock property if available)
            var ribbonControl = ribbon as Syncfusion.Windows.Forms.Tools.RibbonControlAdv;
            if (ribbonControl != null)
            {
                ribbonControl.Dock.ToString().Should().MatchRegex("Top|Fill");
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
            form.CallInitializeRibbon();

            var ribbon = form.GetPrivateField("_ribbon") as Syncfusion.Windows.Forms.Tools.RibbonControlAdv;
            ribbon.Should().NotBeNull();

            ribbon!.ShowCaption.Should().BeFalse();
            ribbon.ShowRibbonDisplayOptionButton.Should().BeFalse();
            ribbon.MenuButtonVisible.Should().BeFalse();
            ribbon.MenuButtonEnabled.Should().BeFalse();

            form.Dispose();
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

                // Act: run show lifecycle, then execute deferred init directly to avoid timer/stability flakiness.
                var _ = form.Handle;
                form.Show();
                Application.DoEvents();

                var runDeferredInitialization = typeof(MainForm).GetMethod("RunDeferredInitializationAsync", BindingFlags.Instance | BindingFlags.NonPublic);
                runDeferredInitialization.Should().NotBeNull();
                var deferredTask = runDeferredInitialization!.Invoke(form, new object[] { CancellationToken.None }) as Task;
                deferredTask.Should().NotBeNull("deferred startup method should return a Task");
                await deferredTask!;

                form.MainViewModel.Should().NotBeNull("deferred startup should resolve MainViewModel");

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
            form.SetPrivateField("_uiConfig", new UIConfiguration { AutoShowDashboard = true });

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
            themeMock.SetupGet(t => t.CurrentTheme).Returns("Office2019Colorful");
            themeMock.Setup(t => t.ApplyTheme(It.IsAny<string>())).Callback<string>(theme =>
            {
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
            form.CallOnShown();
            form.PerformLayout();

            for (int i = 0; i < 30; i++)
            {
                Application.DoEvents();
                if (form.GetPrivateField("_ribbon") != null)
                {
                    break;
                }

                Thread.Sleep(25);
            }

            var ribbonControl = form.GetPrivateField("_ribbon") as Syncfusion.Windows.Forms.Tools.RibbonControlAdv;
            ribbonControl.Should().NotBeNull("Ribbon should be initialized before querying theme controls");

            // Pre-assert: Theme control exists (legacy ThemeToggle button or newer ThemeCombo selector)
            var findMethod = typeof(MainForm).GetMethod(
                "FindToolStripItem",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Syncfusion.Windows.Forms.Tools.RibbonControlAdv), typeof(string) },
                null)!;

            var themeControl = findMethod.Invoke(form, new object[] { ribbonControl!, "ThemeToggle" }) as ToolStripItem
                ?? findMethod.Invoke(form, new object[] { ribbonControl!, "ThemeCombo" }) as ToolStripItem;
            themeControl.Should().NotBeNull("Theme control should be present after OnLoad");

            // Act: Toggle theme
            form.ToggleTheme();
            Application.DoEvents();

            var activeThemeControl = findMethod.Invoke(form, new object[] { ribbonControl!, "ThemeToggle" }) as ToolStripItem
                ?? findMethod.Invoke(form, new object[] { ribbonControl!, "ThemeCombo" }) as ToolStripItem
                ?? themeControl;

            // Assert: after ThemeChanged event, UI reflects a valid next supported theme
            if (string.Equals(activeThemeControl!.Name, "ThemeToggle", StringComparison.OrdinalIgnoreCase))
            {
                activeThemeControl.Text.Should().NotBeNullOrWhiteSpace("Theme toggle text should reflect new theme state");
            }
            else if (string.Equals(activeThemeControl.Name, "ThemeCombo", StringComparison.OrdinalIgnoreCase))
            {
                activeThemeControl.Text.Should().NotBeNullOrWhiteSpace("Theme combo should display the active theme");
                WileyWidget.WinForms.Themes.ThemeColors.GetSupportedThemes()
                    .Should().Contain(activeThemeControl.Text, "Theme combo should reflect a supported theme value");
                activeThemeControl.Text.Should().NotBe("Office2019Colorful", "ToggleTheme should advance to the next theme");
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
            form.CallOnLoad();
            form.Show();
            Application.DoEvents();

            using var ownedForm = new Form
            {
                Owner = form,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-2000, -2000)
            };

            var probe = new ProbeThemableControl { Dock = DockStyle.Fill };
            ownedForm.Controls.Add(probe);
            ownedForm.Show();
            Application.DoEvents();

            var onThemeServiceChanged = typeof(MainForm).GetMethod("OnThemeServiceChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
            onThemeServiceChanged.Invoke(form, new object?[] { form, "Office2016Black" });
            Application.DoEvents();

            probe.AppliedTheme.Should().Be("Office2016Black", "owned floating forms should replay runtime theme changes to their themable children");

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
            form.CallInitializeRibbon();

            var getSearchBox = typeof(MainForm).GetMethod("GetGlobalSearchTextBox", BindingFlags.Instance | BindingFlags.NonPublic);
            var searchBox = getSearchBox?.Invoke(form, null) as ToolStripTextBox;

            // Act
            var result = form.CallProcessCmdKey(Keys.Control | Keys.F);

            // Assert
            if (searchBox != null)
            {
                result.Should().BeTrue("Ctrl+F should be handled when GlobalSearch is available");
            }
            else
            {
                result.Should().BeFalse("Ctrl+F should bubble when no GlobalSearch control can be resolved");
            }

            form.Dispose();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types")]
        [StaFact]
        public void OnFirstChanceException_Handler_IsNoOp_AndDoesNotLog()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var loggerMock = new Mock<ILogger<MainForm>>();

            var form = new TestMainForm(provider, configuration, loggerMock.Object, ReportViewerLaunchOptions.Disabled, Mock.Of<IThemeService>(), Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>(), new SyncfusionControlFactory(NullLogger<SyncfusionControlFactory>.Instance));

            // Act & Assert for theme exception (should be ignored)
            var themeEx = new NullReferenceException("Theme error", new ArgumentException("SfSkinManager"));
            var onFirstChance = typeof(MainForm).GetMethod("MainForm_FirstChanceException", BindingFlags.Instance | BindingFlags.NonPublic);
            onFirstChance?.Invoke(form, new object[] { form, new System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs(themeEx) });

            // Act with a non-theme exception as well
            var otherEx = new InvalidOperationException("Other error");
            onFirstChance?.Invoke(form, new object[] { form, new System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs(otherEx) });

            loggerMock.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never,
                "MainForm_FirstChanceException is currently implemented as a no-op");

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
    }
}
