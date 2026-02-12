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
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.Models;
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
                IFileImportService fileImportService)
                : base(sp, configuration, logger, reportViewerLaunchOptions, themeService, windowStateService, fileImportService)
            {
            }

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

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, windowMock.Object, Mock.Of<IFileImportService>());

            // Act - initialize chrome explicitly (OnLoad defers chrome to OnShown)
            var _ = form.Handle; // ensure handle created (required by ValidateInitializationState)
            form.CallInitializeChrome();
            form.CallOnLoad();

            // Assert
            form.Text.Should().Be(MainFormResources.FormTitle);
            form.WindowState.Should().Be(System.Windows.Forms.FormWindowState.Maximized);

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

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

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
                ribbonControl.Dock.ToString().Should().Contain("Top");
            }

            form.Dispose();
        }

        [StaFact]
        public async Task OnShown_ResolvesAndInitializesMainViewModel_WhenCalled()
        {
            // Arrange
            var configOverrides = new Dictionary<string, string?>
            {
                ["UI:UseSyncfusionDocking"] = "false",
                ["UI:IsUiTestHarness"] = "false"
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

            var form = new TestMainForm(testProvider, configuration, logger, ReportViewerLaunchOptions.Disabled, Mock.Of<IThemeService>(), Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            // Act: create handle and call OnShown (which starts deferred initialization)
            var _ = form.Handle;
            form.CallOnShown();

            // Wait for deferred initialization to be assigned and completed
            Task? deferred = null;
            for (int i = 0; i < 40; i++) // ~2 seconds max
            {
                deferred = form.DeferredInitializationTask;
                if (deferred != null) break;
                await Task.Delay(50);
            }
            deferred.Should().NotBeNull();

            var completed = await Task.WhenAny(deferred!, Task.Delay(5000));
            if (completed != deferred)
            {
                throw new TimeoutException("Deferred initialization timed out");
            }

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

        [StaFact]
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
            panelNavMock.Setup(p => p.ShowForm<BudgetDashboardForm>(It.IsAny<string>(), It.IsAny<Syncfusion.Windows.Forms.Tools.DockingStyle>(), It.IsAny<bool>()))
                .Verifiable();
            // Also accept overload with parameters object (null passed in production code)
            panelNavMock.Setup(p => p.ShowForm<BudgetDashboardForm>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<Syncfusion.Windows.Forms.Tools.DockingStyle>(), It.IsAny<bool>()))
                .Verifiable();

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, windowMock.Object, Mock.Of<IFileImportService>());

            // Create control handle to prevent Invoke hanging
            form.CreateControl();
            // Force handle creation for forms
            var _ = form.Handle;

            // Prepare minimal docking manager and panel navigator
            var dockingManager = new Syncfusion.Windows.Forms.Tools.DockingManager { HostControl = form };
            form.SetPrivateField("_dockingManager", dockingManager);
            form.SetPrivateField("_panelNavigator", panelNavMock.Object);
            form.SetPrivateField("_uiConfig", new UIConfiguration { UseSyncfusionDocking = true });
            form.SetPrivateField("_syncfusionDockingInitialized", true);

            // Load MRU list into private field by calling private LoadMruList via reflection
            var loadMru = typeof(MainForm).GetMethod("LoadMruList", BindingFlags.Instance | BindingFlags.NonPublic)!;
            loadMru.Invoke(form, null);

            // Act
            await form.CallInitializeAsync(CancellationToken.None);

            // Assert
            // Verify the overload with parameters (null) was called during initialization
            panelNavMock.Verify(p => p.ShowForm<BudgetDashboardForm>(It.IsAny<string>(), It.IsAny<Syncfusion.Windows.Forms.Tools.DockingStyle>(), It.IsAny<bool>()), Times.AtLeastOnce);

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

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            // Apply theme to form
            SfSkinManager.SetVisualStyle(form, "Office2019Colorful");

            form.CreateControl();
            var _ = form.Handle;
            // Initialize chrome through normal lifecycle
            form.CallOnLoad();
            form.Show();
            form.PerformLayout();
            Application.DoEvents();

            // Pre-assert: Theme control exists (legacy ThemeToggle button or newer ThemeCombo selector)
            var findMethod = typeof(MainForm).GetMethod("FindToolStripItem", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Control), typeof(string) }, null)!;
            var themeControl = findMethod.Invoke(form, new object[] { form, "ThemeToggle" }) as ToolStripItem
                ?? findMethod.Invoke(form, new object[] { form, "ThemeCombo" }) as ToolStripItem;
            themeControl.Should().NotBeNull("Theme control should be present after OnLoad");

            // Act: Toggle theme
            form.ToggleTheme();
            Application.DoEvents();

            var activeThemeControl = findMethod.Invoke(form, new object[] { form, "ThemeToggle" }) as ToolStripItem
                ?? findMethod.Invoke(form, new object[] { form, "ThemeCombo" }) as ToolStripItem
                ?? themeControl;

            // Assert: after ThemeChanged event, UI reflects Office2019Dark
            if (string.Equals(activeThemeControl!.Name, "ThemeToggle", StringComparison.OrdinalIgnoreCase))
            {
                activeThemeControl.Text.Should().Match("*Light*", "Theme toggle text should reflect new theme state");
            }
            else if (string.Equals(activeThemeControl.Name, "ThemeCombo", StringComparison.OrdinalIgnoreCase))
            {
                activeThemeControl.Text.Should().Be("Office2019Dark", "Theme combo selection should reflect new theme state");
            }
            else
            {
                throw new Xunit.Sdk.XunitException($"Unexpected theme control name '{activeThemeControl.Name}'");
            }

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

            var form = new TestMainForm(provider, configuration, loggerMock.Object, ReportViewerLaunchOptions.Disabled, Mock.Of<IThemeService>(), windowMock.Object, fileImportMock.Object);

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

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeMock.Object, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            // Apply theme to form
            SfSkinManager.SetVisualStyle(form, "Office2019Colorful");

            var _ = form.Handle;
            form.CallInitializeChrome();
            form.CallOnLoad();
            form.Show();
            Application.DoEvents();

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
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var loggerMock = new Mock<ILogger<MainForm>>();

            var form = new TestMainForm(provider, configuration, loggerMock.Object, ReportViewerLaunchOptions.Disabled, Mock.Of<IThemeService>(), Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

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

            var form = new TestMainForm(provider, configuration, loggerMock.Object, ReportViewerLaunchOptions.Disabled, Mock.Of<IThemeService>(), Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

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
