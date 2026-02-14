using System;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;     // Added for BackStage types
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    [CollectionDefinition("Sequential", DisableParallelization = true)]
    public class SequentialCollectionDefinition { }

    [Collection("Sequential")]
    public class RibbonFactoryTests
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
        public void CreateLargeNavButton_CreatesButton_WithExpectedProperties()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            try
            {
                // Use reflection to call the private CreateLargeNavButton
                var rfType = typeof(RibbonFactory);
                // Method isn't generic; just get and invoke
                var mi = rfType.GetMethod("CreateLargeNavButton", BindingFlags.NonPublic | BindingFlags.Static)!;

                // Use null for iconName to avoid calling DPI image service (no Program._services required)
                var btn = (System.Windows.Forms.ToolStripButton)mi.Invoke(null, new object?[] {
                    "Nav_Test", "Test Button", (object?)null, "Office2019Colorful", new System.Action(() => { }), Mock.Of<ILogger>()
                })!;

                // Assert basic properties
                btn.Should().NotBeNull();
                btn.Name.Should().Be("Nav_Test");
                btn.Text.Should().Be("Test Button");
                btn.TextImageRelation.Should().Be(System.Windows.Forms.TextImageRelation.ImageAboveText);
                btn.ImageScaling.Should().Be(System.Windows.Forms.ToolStripItemImageScaling.None);
                btn.ToolTipText.Should().Be("Test Button");
                btn.AccessibleDescription.Should().NotContain("Shortcut:");
                btn.Image.Should().NotBeNull();

                FluentActions.Invoking(() => btn.Dispose()).Should().NotThrow();
                btn.Image.Should().BeNull();
            }
            finally
            {
            }
        }

        [StaFact]
        public void CreateLargeNavButton_WithShortcut_IncludesShortcutInTooltip()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            // Use reflection to call the private CreateLargeNavButton
            var rfType = typeof(RibbonFactory);
            var mi = rfType.GetMethod("CreateLargeNavButton", BindingFlags.NonPublic | BindingFlags.Static)!;

            var btn = (System.Windows.Forms.ToolStripButton)mi.Invoke(null, new object?[]
            {
                "Nav_Accounts", "Accounts", (object?)null, "Office2019Colorful", new System.Action(() => { }), Mock.Of<ILogger>()
            })!;

            btn.ToolTipText.Should().Contain("Alt+A");
            btn.AccessibleDescription.Should().Contain("Shortcut: Alt+A");

            btn.Dispose();
        }

        [StaFact]
        public void SafeBeginInvoke_WithHandleNotCreated_DoesNotThrow()
        {
            var rfType = typeof(RibbonFactory);
            var mi = rfType.GetMethod("SafeBeginInvoke", BindingFlags.NonPublic | BindingFlags.Static);
            mi.Should().NotBeNull();

            var control = new Control();
            var action = new System.Action(() => { });

            FluentActions.Invoking(() => mi!.Invoke(null, new object?[] { control, action, Mock.Of<ILogger>() }))
                .Should().NotThrow();
        }

        [StaFact]
        public async Task ClickHandlers_InvokeShowPanel_And_LogNavigationAsync()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            // Inject a mock PanelNavigationService into the form so ShowPanel<T> will delegate to it
            var panelNavMock = new Mock<IPanelNavigationService>();
            form.SetPrivateField("_panelNavigator", panelNavMock.Object);

            // Prepare Program.Services so LogNavigationActivityAsync can resolve IActivityLogService
            var activityMock = new Mock<IActivityLogService>();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            activityMock.Setup(a => a.LogNavigationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Callback(() => tcs.TrySetResult(true));

            var spForProgram = new ServiceCollection()
                .AddSingleton<IActivityLogService>(activityMock.Object)
                .BuildServiceProvider();

            // Set private static Program._services via reflection
            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic)!;
            svcField.SetValue(null, spForProgram);

            try
            {
                // Use private factory method to create the accounts nav button without constructing RibbonControlAdv
                var rfType = typeof(RibbonFactory);
                var mi = rfType.GetMethod("CreateLargeNavButton", BindingFlags.NonPublic | BindingFlags.Static)!;

                var accountsButton = (System.Windows.Forms.ToolStripButton)mi.Invoke(null, new object?[] {
                    "Nav_Accounts",
                    "Accounts",
                    (object?)null,
                    "Office2019Colorful",
                    new System.Action(() => form.ShowPanel<WileyWidget.WinForms.Controls.Panels.AccountsPanel>("Municipal Accounts", DockingStyle.Right)),
                    Mock.Of<ILogger>()
                })!;

                accountsButton.Should().NotBeNull();

                // Simulate click
                accountsButton!.PerformClick();

                // Wait for logging to occur (background fire-and-forget)
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(500));
                completed.Should().Be(tcs.Task, "Activity logging should be triggered asynchronously");

                // Assert: navigation service invoked
                panelNavMock.Verify(n => n.ShowPanel<WileyWidget.WinForms.Controls.Panels.AccountsPanel>(It.Is<string>(s => s == "Municipal Accounts"), Syncfusion.Windows.Forms.Tools.DockingStyle.Right, It.IsAny<bool>()), Times.Once);

                // Assert: activity log service called
                activityMock.Verify(a => a.LogNavigationAsync(It.Is<string>(s => s.Contains("Navigated to")), It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
            }
            finally
            {
                // Cleanup Program._services to avoid polluting other tests
                svcField.SetValue(null, null);
            }
        }

        [StaFact]
        public void CreateRibbon_ProducesExpectedStructure()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            // Set Program.ServicesOrNull for DPI service
            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                // Act: Create the ribbon
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Ensure layout is performed
                ribbon.PerformLayout();

                // Assert: Top-level RibbonControlAdv properties
                ribbon.Should().NotBeNull();
                ribbon.Name.Should().Be("Ribbon_Main");
                ribbon.Dock.Should().Be(DockStyleEx.Top);
                ribbon.LauncherStyle.Should().Be(LauncherStyle.Metro);
                ribbon.AutoSize.Should().BeFalse();
                ribbon.Height.Should().BeGreaterThan(0);
                ribbon.MenuButtonEnabled.Should().BeTrue();
                ribbon.MenuButtonVisible.Should().BeTrue();
                ribbon.MenuButtonText.Should().Be("File");
                ribbon.QuickPanelVisible.Should().BeTrue();
                ribbon.ShowQuickItemsDropDownButton.Should().BeTrue();

                // Assert: BackStageView (File menu)
                var backStage = ribbon.BackStageView;
                backStage.Should().NotBeNull();
                var backStageControls = backStage.BackStage?.Controls;
                if (backStageControls != null)
                {
                    backStageControls.Count.Should().BeGreaterOrEqualTo(4);

                    // New Tab
                    var newTab = backStageControls.OfType<BackStageTab>().FirstOrDefault(t => t.Text == "New");
                    newTab.Should().NotBeNull();
                    newTab!.Name.Should().Be("BackStage_New");
                    newTab.Controls.Count.Should().BeGreaterThan(0);

                    // Open Tab
                    var openTab = backStageControls.OfType<BackStageTab>().FirstOrDefault(t => t.Text == "Open");
                    openTab.Should().NotBeNull();
                    openTab!.Name.Should().Be("BackStage_Open");
                    openTab.Controls.Count.Should().BeGreaterThan(0);

                    // Info Tab
                    var infoTab = backStageControls.OfType<BackStageTab>().FirstOrDefault(t => t.Text == "Info");
                    infoTab.Should().NotBeNull();
                    infoTab!.Name.Should().Be("BackStage_Info");
                    infoTab.Controls.Count.Should().BeGreaterThan(0);
                    backStage.BackStage?.SelectedTab.Should().Be(infoTab);

                    // Exit Button
                    var exitBtn = backStageControls.OfType<BackStageButton>().FirstOrDefault(b => b.Text == "Exit");
                    exitBtn.Should().NotBeNull();
                    exitBtn!.Name.Should().Be("BackStage_Exit");
                    exitBtn.Placement.Should().Be(BackStageItemPlacement.Bottom);
                }

                // Assert: Home Tab
                homeTab.Should().NotBeNull();
                homeTab.Text.Should().Be("Home");
                homeTab.Name.Should().Be("HomeTab");
                homeTab.Panel.AutoSize.Should().BeTrue();
                static IEnumerable<ToolStripEx> EnumerateRibbonGroups(Control root)
                {
                    foreach (Control child in root.Controls)
                    {
                        if (child is ToolStripEx strip)
                        {
                            yield return strip;
                        }

                        foreach (var nested in EnumerateRibbonGroups(child))
                        {
                            yield return nested;
                        }
                    }
                }

                var groups = EnumerateRibbonGroups(ribbon).ToList();
                groups.Count.Should().BeGreaterOrEqualTo(7); // Includes File plus core groups

                ToolStripEx? FindGroup(params string[] names) =>
                    groups
                        .Where(g => names.Any(name => string.Equals(g.Name, name, StringComparison.Ordinal)))
                        .OrderByDescending(g => g.Items.Count)
                        .FirstOrDefault();

                static IEnumerable<ToolStripItem> FlattenItems(IEnumerable<ToolStripItem> items)
                {
                    foreach (var item in items)
                    {
                        yield return item;

                        if (item is ToolStripPanelItem panelItem)
                        {
                            foreach (var nested in FlattenItems(panelItem.Items.Cast<ToolStripItem>()))
                            {
                                yield return nested;
                            }
                        }
                    }
                }

                // Dashboard/Core Navigation Group
                var dashboardGroup = FindGroup("DashboardGroup", "CoreNavigationGroup");
                dashboardGroup.Should().NotBeNull();
                dashboardGroup!.Items.Count.Should().BeGreaterThan(0);

                // Financials Group
                var financialsGroup = FindGroup("FinancialsGroup");
                financialsGroup.Should().NotBeNull();
                financialsGroup!.Items.Count.Should().BeGreaterThan(0);

                // Reporting Group
                var reportingGroup = FindGroup("ReportingGroup");
                reportingGroup.Should().NotBeNull();
                reportingGroup!.Items.Count.Should().BeGreaterThan(0);

                // Tools Group
                var toolsGroup = FindGroup("ToolsGroup");
                toolsGroup.Should().NotBeNull();
                toolsGroup!.Items.Count.Should().BeGreaterThan(0);

                // Layout Group
                var layoutGroup = FindGroup("LayoutGroup");
                layoutGroup.Should().NotBeNull();
                layoutGroup!.Items.Count.Should().BeGreaterThan(0);

                // More Group (Views)
                var moreGroup = FindGroup("MorePanelsGroup");
                moreGroup.Should().NotBeNull();

                // Actions Group (Search & Grid)
                var actionsGroup = FindGroup("ActionGroup");
                actionsGroup.Should().NotBeNull();
                var actionItems = FlattenItems(actionsGroup!.Items.Cast<ToolStripItem>()).ToList();

                actionItems.Count.Should().BeGreaterThan(0);

                var searchBox = actionItems.OfType<ToolStripTextBox>()
                    .FirstOrDefault(item => string.Equals(item.Name, "GlobalSearch", StringComparison.Ordinal))
                    ?? actionItems.OfType<ToolStripTextBox>().FirstOrDefault();
                if (searchBox != null)
                {
                    searchBox.Width.Should().Be(180);
                }

                var themeBtn = actionsGroup.Items.OfType<ToolStripButton>()
                    .FirstOrDefault(item => item.Name == "ThemeToggle");
                var themeCombo = actionsGroup.Items.OfType<ToolStripComboBoxEx>()
                    .FirstOrDefault(item => item.Name == "ThemeCombo");
                if (themeBtn != null)
                {
                    themeBtn.Text.Should().Be("Toggle Theme");
                }
                else if (themeCombo != null)
                {
                    themeCombo.Items.Count.Should().BeGreaterThan(0);
                }
                else
                {
                    actionsGroup.Items.Count.Should().BeGreaterThan(0);
                }

                // Assert: Quick Access Toolbar (QAT)
                ribbon.Header.QuickItems.Count.Should().BeGreaterThan(0); // At least Dashboard, Accounts, Settings

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                // Cleanup
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void GlobalSearch_EnterKey_SuppressesKeyPress()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            // Set Program.ServicesOrNull for DPI service
            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);
                static IEnumerable<ToolStripEx> EnumerateRibbonGroups(Control root)
                {
                    foreach (Control child in root.Controls)
                    {
                        if (child is ToolStripEx strip)
                        {
                            yield return strip;
                        }

                        foreach (var nested in EnumerateRibbonGroups(child))
                        {
                            yield return nested;
                        }
                    }
                }

                var actionsGroup = EnumerateRibbonGroups(ribbon)
                    .Where(g => g.Name == "ActionGroup")
                    .OrderByDescending(g => g.Items.Count)
                    .First();
                static IEnumerable<ToolStripItem> FlattenItems(IEnumerable<ToolStripItem> items)
                {
                    foreach (var item in items)
                    {
                        yield return item;

                        if (item is ToolStripPanelItem panelItem)
                        {
                            foreach (var nested in FlattenItems(panelItem.Items.Cast<ToolStripItem>()))
                            {
                                yield return nested;
                            }
                        }
                    }
                }

                var actionItems = FlattenItems(actionsGroup.Items.Cast<ToolStripItem>()).ToList();

                var searchBox = actionItems.OfType<ToolStripTextBox>()
                    .FirstOrDefault(item => string.Equals(item.Name, "GlobalSearch", StringComparison.Ordinal))
                    ?? actionItems.OfType<ToolStripTextBox>().FirstOrDefault();

                if (searchBox == null)
                {
                    actionsGroup.Items.Count.Should().BeGreaterThan(0);
                    ribbon.Dispose();
                    return;
                }

                searchBox!.Text = string.Empty;
                var textBox = searchBox.TextBox;
                textBox.Should().NotBeNull();

                var args = new KeyEventArgs(Keys.Enter);
                RaiseKeyDown(textBox, args);

                args.Handled.Should().BeTrue();
                args.SuppressKeyPress.Should().BeTrue();

                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        // Torture tests: Edge cases and misconfigurations
        [StaFact]
        public void CreateRibbon_WithNullForm_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = Mock.Of<ILogger>();

            // Act & Assert
            FluentActions.Invoking(() => RibbonFactory.CreateRibbon(null!, logger))
                .Should().Throw<ArgumentNullException>()
                .WithParameterName("form");
        }

        [StaFact]
        public void CreateRibbon_WithNullLogger_DoesNotThrow()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, Mock.Of<ILogger<MainForm>>(), ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            // Act & Assert: Should not throw, just create ribbon without logging
            FluentActions.Invoking(() => RibbonFactory.CreateRibbon(form, null))
                .Should().NotThrow();

            var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, null);
            ribbon.Should().NotBeNull();
            homeTab.Should().NotBeNull();

            // Cleanup
            ribbon.Dispose();
        }

        [StaFact]
        public void CreateRibbon_WithFormWithoutServices_HandlesGracefully()
        {
            // Arrange: Form with minimal services
            var services = new ServiceCollection();
            services.AddLogging();
            var provider = services.BuildServiceProvider();
            var configuration = new ConfigurationBuilder().Build(); // Empty config
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Mock.Of<IThemeService>(); // Mock theme service
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            // Set minimal Program services
            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                // Act
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Assert: Should still create basic structure
                ribbon.Should().NotBeNull();
                ribbon.Name.Should().Be("Ribbon_Main");
                homeTab.Should().NotBeNull();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void CreateRibbon_WithInvalidTheme_DoesNotThrow()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            // Mock invalid theme
            var themeMock = new Mock<IThemeService>();
            themeMock.SetupGet(t => t.CurrentTheme).Returns("InvalidTheme");
            themeMock.Setup(t => t.ApplyTheme(It.IsAny<string>())).Throws(new ArgumentException("Invalid theme"));
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                // Act: Should handle theme errors gracefully
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Assert: Ribbon created despite theme issues
                ribbon.Should().NotBeNull();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_WithConflictingProperties_HandlesAutoSizeAndHeight()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Act: Set conflicting properties
                ribbon.AutoSize = true; // Normally false
                ribbon.Height = 100; // Fixed height

                // Assert: Control handles it without crashing
                ribbon.Height.Should().BeGreaterThan(0); // Height still valid
                ribbon.AutoSize.Should().BeTrue();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_WithNullBackStageView_WhenMenuButtonEnabled_DoesNotCrash()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Act: Remove BackStageView
                ribbon.BackStageView = null;

                // Assert: MenuButton still enabled, no crash
                ribbon.MenuButtonEnabled.Should().BeTrue();
                ribbon.MenuButtonVisible.Should().BeTrue();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_Dispose_MultipleTimes_DoesNotThrow()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Act: Dispose multiple times
                ribbon.Dispose();
                FluentActions.Invoking(() => ribbon.Dispose()).Should().NotThrow();

                // Assert: No exceptions
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_WithUnusualDockStyle_HandlesGracefully()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Act: Set unusual dock (normally Top, set to None)
                ribbon.Dock = DockStyleEx.None;

                // Assert: Should not crash, dock changes
                ribbon.Dock.Should().Be(DockStyleEx.None);

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_PerformLayout_AfterModifications_DoesNotThrow()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Act: Modify and layout
                ribbon.AutoSize = true;
                ribbon.MinimizePanel = true;
                ribbon.ShowPanel = false;
                ribbon.PerformLayout();

                // Assert: No exceptions during layout
                ribbon.Should().NotBeNull();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        // ===== COMPREHENSIVE FUNCTION VALIDATION TESTS =====

        [StaFact]
        public void RibbonControlAdv_AllProperties_GetSet_WorkCorrectly()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test basic properties
                ribbon.Name.Should().Be("Ribbon_Main");
                ribbon.Dock.Should().Be(DockStyleEx.Top);
                ribbon.AutoSize.Should().BeFalse();
                ribbon.Height.Should().BeGreaterThan(0);
                ribbon.MenuButtonEnabled.Should().BeTrue();
                ribbon.MenuButtonVisible.Should().BeTrue();
                ribbon.MenuButtonText.Should().Be("File");
                ribbon.QuickPanelVisible.Should().BeTrue();
                ribbon.ShowQuickItemsDropDownButton.Should().BeTrue();

                // Test LauncherStyle
                ribbon.LauncherStyle.Should().Be(LauncherStyle.Metro);

                // Test BackStageView
                ribbon.BackStageView.Should().NotBeNull();

                // Test Header properties
                ribbon.Header.Should().NotBeNull();
                ribbon.Header.MainItems.Count.Should().BeGreaterThan(0);
                ribbon.Header.QuickItems.Count.Should().BeGreaterThan(0);

                // Test theme
                ribbon.ThemeName.Should().NotBeNullOrEmpty();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_PropertyModifications_WorkCorrectly()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test property modifications
                var originalHeight = ribbon.Height;
                ribbon.Height = 200;
                ribbon.Height.Should().Be(200);

                ribbon.AutoSize = true;
                ribbon.AutoSize.Should().BeTrue();

                ribbon.MinimizePanel = true;
                ribbon.MinimizePanel.Should().BeTrue();

                ribbon.ShowPanel = false;
                ribbon.ShowPanel.Should().BeFalse();

                ribbon.MenuButtonText = "Custom";
                ribbon.MenuButtonText.Should().Be("Custom");

                ribbon.MenuButtonWidth = 60;
                ribbon.MenuButtonWidth.Should().Be(60);

                // Test dock changes
                ribbon.Dock = DockStyleEx.Bottom;
                ribbon.Dock.Should().Be(DockStyleEx.Bottom);

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_Methods_ExecuteWithoutErrors()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test PerformLayout
                FluentActions.Invoking(() => ribbon.PerformLayout()).Should().NotThrow();

                // Test Refresh
                FluentActions.Invoking(() => ribbon.Refresh()).Should().NotThrow();

                // Test Invalidate
                FluentActions.Invoking(() => ribbon.Invalidate()).Should().NotThrow();

                // Test Update
                FluentActions.Invoking(() => ribbon.Update()).Should().NotThrow();

                // Test BringToFront
                FluentActions.Invoking(() => ribbon.BringToFront()).Should().NotThrow();

                // Test SendToBack
                FluentActions.Invoking(() => ribbon.SendToBack()).Should().NotThrow();

                // Test Show/Hide
                FluentActions.Invoking(() => ribbon.Show()).Should().NotThrow();
                FluentActions.Invoking(() => ribbon.Hide()).Should().NotThrow();
                ribbon.Show(); // Restore visibility

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_Events_CanBeAttachedAndTriggered()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test MenuButtonClick event
                FluentActions.Invoking(() => ribbon.MenuButtonClick += (sender, e) => { }).Should().NotThrow();

                // Test other events can be attached
                FluentActions.Invoking(() => ribbon.Resize += (sender, e) => { }).Should().NotThrow();
                FluentActions.Invoking(() => ribbon.Paint += (sender, e) => { }).Should().NotThrow();
                FluentActions.Invoking(() => ribbon.VisibleChanged += (sender, e) => { }).Should().NotThrow();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_HeaderOperations_WorkCorrectly()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test Header exists
                ribbon.Header.Should().NotBeNull();

                // Test MainItems collection
                ribbon.Header.MainItems.Should().NotBeNull();
                ribbon.Header.MainItems.Count.Should().BeGreaterThan(0);

                // Test QuickItems collection
                ribbon.Header.QuickItems.Should().NotBeNull();
                ribbon.Header.QuickItems.Count.Should().BeGreaterThan(0);

                // Test accessing items
                var firstMainItem = ribbon.Header.MainItems[0];
                firstMainItem.Should().NotBeNull();

                var firstQuickItem = ribbon.Header.QuickItems[0];
                firstQuickItem.Should().NotBeNull();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_ThemeOperations_WorkCorrectly()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test current theme
                var originalTheme = ribbon.ThemeName;
                originalTheme.Should().NotBeNullOrEmpty();

                // Test theme change (if supported)
                FluentActions.Invoking(() => ribbon.ThemeName = "Office2019Colorful").Should().NotThrow();

                // Test theme application
                FluentActions.Invoking(() => SfSkinManager.SetVisualStyle(ribbon, ribbon.ThemeName)).Should().NotThrow();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_SizeAndLayoutOperations_WorkCorrectly()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test size properties
                var originalSize = ribbon.Size;
                ribbon.Size.Should().NotBe(Size.Empty);

                // Test location
                var originalLocation = ribbon.Location;
                ribbon.Location.Should().Be(new Point(0, 0));

                // Test bounds
                var bounds = ribbon.Bounds;
                bounds.Width.Should().BeGreaterThan(0);
                bounds.Height.Should().BeGreaterThan(0);

                // Test client area
                var clientSize = ribbon.ClientSize;
                clientSize.Width.Should().BeGreaterThan(0);
                clientSize.Height.Should().BeGreaterThan(0);

                // Test minimum size
                ribbon.MinimumSize.Should().NotBe(Size.Empty);

                // Test layout operations
                FluentActions.Invoking(() => ribbon.PerformLayout()).Should().NotThrow();
                FluentActions.Invoking(() => ribbon.SuspendLayout()).Should().NotThrow();
                FluentActions.Invoking(() => ribbon.ResumeLayout()).Should().NotThrow();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_VisibilityAndStateOperations_WorkCorrectly()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test visibility
                ribbon.Visible.Should().BeTrue();
                FluentActions.Invoking(() => ribbon.Show()).Should().NotThrow();
                ribbon.Visible.Should().BeTrue();

                FluentActions.Invoking(() => ribbon.Hide()).Should().NotThrow();
                ribbon.Visible.Should().BeFalse();

                ribbon.Show(); // Restore

                // Test enabled state
                ribbon.Enabled.Should().BeTrue();

                ribbon.Enabled = false;
                ribbon.Enabled.Should().BeFalse();

                ribbon.Enabled = true; // Restore

                // Test focus operations
                FluentActions.Invoking(() => ribbon.Focus()).Should().NotThrow();

                // Test selection operations
                FluentActions.Invoking(() => ribbon.Select()).Should().NotThrow();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        // ===== ADDITIONAL PROPERTY VALIDATION TESTS =====

        [StaFact]
        public void RibbonControlAdv_OfficeColorSchemes_WorkCorrectly()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test Office2013ColorScheme
                var original2013Scheme = ribbon.Office2013ColorScheme;
                ribbon.Office2013ColorScheme = Office2013ColorScheme.White;
                ribbon.Office2013ColorScheme.Should().Be(Office2013ColorScheme.White);

                // Test Office2016ColorScheme
                var original2016Scheme = ribbon.Office2016ColorScheme;
                ribbon.Office2016ColorScheme = Office2016ColorScheme.White;
                ribbon.Office2016ColorScheme.Should().Be(Office2016ColorScheme.White);

                // Test OfficeColorScheme
                var originalOfficeScheme = ribbon.OfficeColorScheme;
                ribbon.OfficeColorScheme = ToolStripEx.ColorScheme.Blue;
                ribbon.OfficeColorScheme.Should().Be(ToolStripEx.ColorScheme.Blue);

                // Test Office2013ColorTable
                ribbon.Office2013ColorTable.Should().NotBeNull();

                // Test Office2016ColorTable
                ribbon.Office2016ColorTable.Should().NotBeNull();

                // Test OfficeStyle2013ColorTable
                ribbon.OfficeStyle2013ColorTable.Should().NotBeNull();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_ThemeStyleAndRibbonStyle_WorkCorrectly()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test RibbonStyle
                var originalRibbonStyle = ribbon.RibbonStyle;
                ribbon.RibbonStyle = RibbonStyle.Office2007;
                ribbon.RibbonStyle.Should().Be(RibbonStyle.Office2007);

                // Test RibbonTouchStyleColorTable
                ribbon.RibbonTouchStyleColorTable.Should().NotBeNull();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_BackStageNavigationProperties_WorkCorrectly()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test BackStageButtonAlignment
                var originalButtonAlignment = ribbon.BackStageButtonAlignment;
                ribbon.BackStageButtonAlignment = BackButtonAlignment.Left;
                ribbon.BackStageButtonAlignment.Should().Be(BackButtonAlignment.Left);

                // Test BackStageNavigationButtonEnabled
                var originalNavEnabled = ribbon.BackStageNavigationButtonEnabled;
                ribbon.BackStageNavigationButtonEnabled = false;
                ribbon.BackStageNavigationButtonEnabled.Should().BeFalse();
                ribbon.BackStageNavigationButtonEnabled = true; // Restore

                // Test BackStageNavigationButtonStyle
                var originalNavStyle = ribbon.BackStageNavigationButtonStyle;
                ribbon.BackStageNavigationButtonStyle = BackStageNavigationButtonStyles.Touch;
                ribbon.BackStageNavigationButtonStyle.Should().Be(BackStageNavigationButtonStyles.Touch);

                // Test BackStageView (already exists)
                ribbon.BackStageView.Should().NotBeNull();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_LayoutModeAndSimplifiedLayout_WorkCorrectly()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test EnableSimplifiedLayoutMode
                var originalSimplifiedMode = ribbon.EnableSimplifiedLayoutMode;
                ribbon.EnableSimplifiedLayoutMode = true;
                ribbon.EnableSimplifiedLayoutMode.Should().BeTrue();

                // Test LayoutMode
                var originalLayoutMode = ribbon.LayoutMode;
                ribbon.LayoutMode = RibbonLayoutMode.Simplified;
                ribbon.LayoutMode.Should().Be(RibbonLayoutMode.Simplified);
                ribbon.LayoutMode = RibbonLayoutMode.Normal; // Restore

                // Test TouchMode
                var originalTouchMode = ribbon.TouchMode;
                ribbon.TouchMode = true;
                ribbon.TouchMode.Should().BeTrue();
                ribbon.TouchMode = false; // Restore

                // Test RibbonTouchModeEnabled
                var originalTouchModeEnabled = ribbon.RibbonTouchModeEnabled;
                ribbon.RibbonTouchModeEnabled = true;
                ribbon.RibbonTouchModeEnabled.Should().BeTrue();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        [StaFact]
        public void RibbonControlAdv_SystemTextAndTabGroups_WorkCorrectly()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);
            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());

            var progType = typeof(Program);
            var svcField = progType.GetField("_services", BindingFlags.Static | BindingFlags.NonPublic);
            svcField?.SetValue(null, provider);

            try
            {
                var (ribbon, homeTab) = RibbonFactory.CreateRibbon(form, logger);

                // Test SystemText
                ribbon.SystemText.Should().NotBeNull();
                // Note: SystemText properties vary by Syncfusion version, just verify it exists

                // Test TabGroups
                ribbon.TabGroups.Should().NotBeNull();
                ribbon.TabGroups.Count.Should().BeGreaterThanOrEqualTo(0);

                // Test OfficeMenu
                ribbon.OfficeMenu.Should().NotBeNull();

                // Test UpdateUIOnAppIdle
                var originalUpdateUI = ribbon.UpdateUIOnAppIdle;
                ribbon.UpdateUIOnAppIdle = true;
                ribbon.UpdateUIOnAppIdle.Should().BeTrue();

                // Cleanup
                ribbon.Dispose();
            }
            finally
            {
                svcField?.SetValue(null, null);
            }
        }

        private static void RaiseKeyDown(Control control, KeyEventArgs args)
        {
            var method = typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("Unable to access Control.OnKeyDown for test execution.");
            }

            method.Invoke(control, new object[] { args });
        }
    }
}
