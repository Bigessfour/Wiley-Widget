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
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.Services.Abstractions;
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
                SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                SfSkinManager.ApplicationVisualTheme = theme;
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
            }
            finally
            {
            }
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
                    new System.Action(() => form.ShowPanel<Controls.AccountsPanel>("Municipal Accounts", DockingStyle.Right)),
                    Mock.Of<ILogger>()
                })!;

                accountsButton.Should().NotBeNull();

                // Simulate click
                accountsButton!.PerformClick();

                // Wait for logging to occur (background fire-and-forget)
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(500));
                completed.Should().Be(tcs.Task, "Activity logging should be triggered asynchronously");

                // Assert: navigation service invoked
                panelNavMock.Verify(n => n.ShowPanel<Controls.AccountsPanel>(It.Is<string>(s => s == "Municipal Accounts"), Syncfusion.Windows.Forms.Tools.DockingStyle.Right, It.IsAny<bool>()), Times.Once);

                // Assert: activity log service called
                activityMock.Verify(a => a.LogNavigationAsync(It.Is<string>(s => s.Contains("Navigated to")), It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
            }
            finally
            {
                // Cleanup Program._services to avoid polluting other tests
                svcField.SetValue(null, null);
            }
        }
    }
}
