using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WileyWidget.WinForms.Tests.Infrastructure;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.WinForms.Themes;
using Syncfusion.WinForms.Controls;
using Xunit;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    public class RightPanelTests
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
            // Register Blazor WebView services required by BlazorWebView and root components
            services.AddWindowsFormsBlazorWebView();

            // Minimal services required by controls created by the factory
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

            // Register minimal scoped services used by panels
            services.AddScoped<IDashboardService>(_ => Mock.Of<IDashboardService>());
            services.AddScoped<IAILoggingService>(_ => Mock.Of<IAILoggingService>());
            services.AddScoped<IQuickBooksService>(_ => Mock.Of<IQuickBooksService>());
            services.AddScoped<IGlobalSearchService>(_ => Mock.Of<IGlobalSearchService>());

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        }

        /// <summary>
        /// Small test subclass to make it easy to invoke protected lifecycle methods when needed.
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
        public void CreateRightDockPanel_HasActivityLogAndJarvisTabs_AndDefaultMode()
        {
            // Arrange
                TestThemeHelper.EnsureOffice2019Colorful();
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var loggerForForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var form = new TestMainForm(provider, configuration, loggerForForm, ReportViewerLaunchOptions.Disabled,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider)!,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IWindowStateService>(provider)!,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IFileImportService>(provider)!);

            var factoryLogger = new Mock<ILogger>();

            // Act
            var (rightDockPanel, activityLogPanel, initialMode) = RightDockPanelFactory.CreateRightDockPanel(form, provider, factoryLogger.Object);

            // Assert
            rightDockPanel.Should().NotBeNull();
            initialMode.Should().Be(RightDockPanelFactory.RightPanelMode.ActivityLog);
            rightDockPanel.Tag.Should().Be(RightDockPanelFactory.RightPanelMode.ActivityLog);

            rightDockPanel.Controls.Count.Should().BeGreaterThan(0);
            rightDockPanel.Controls[0].Should().BeOfType<TabControl>();
            var tabControl = (TabControl)rightDockPanel.Controls[0];
            tabControl.TabPages.Cast<TabPage>().Any(tp => tp.Name == "ActivityLogTab").Should().BeTrue();
            tabControl.TabPages.Cast<TabPage>().Any(tp => tp.Name == "JARVISChatTab").Should().BeTrue();
            activityLogPanel.Should().NotBeNull();
            activityLogPanel.Name.Should().Be("ActivityLogPanel");

            form.Dispose();
        }

        [StaFact]
        public void GetSetMode_TracksTagCorrectly()
        {
            // Arrange
                TestThemeHelper.EnsureOffice2019Colorful();
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var loggerForForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var form = new TestMainForm(provider, configuration, loggerForForm, ReportViewerLaunchOptions.Disabled,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider)!,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IWindowStateService>(provider)!,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IFileImportService>(provider)!);

            var factoryLogger = new Mock<ILogger>();
            var (rightDockPanel, _, _) = RightDockPanelFactory.CreateRightDockPanel(form, provider, factoryLogger.Object);

            // Act
            RightDockPanelFactory.SetMode(rightDockPanel, RightDockPanelFactory.RightPanelMode.JarvisChat);

            // Assert
            RightDockPanelFactory.GetCurrentMode(rightDockPanel).Should().Be(RightDockPanelFactory.RightPanelMode.JarvisChat);
            rightDockPanel.Tag.Should().Be(RightDockPanelFactory.RightPanelMode.JarvisChat);

            form.Dispose();
        }

        [StaFact]
        public void SwitchRightPanelContent_SelectsTabAndLogs()
        {
            // Arrange
                TestThemeHelper.EnsureOffice2019Colorful();
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var loggerForForm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);

            var form = new TestMainForm(provider, configuration, loggerForForm, ReportViewerLaunchOptions.Disabled,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider)!,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IWindowStateService>(provider)!,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IFileImportService>(provider)!);

            var factoryLogger = new Mock<ILogger>();
            var (rightDockPanel, _, _) = RightDockPanelFactory.CreateRightDockPanel(form, provider, factoryLogger.Object);

            // Act
            RightDockPanelFactory.SwitchRightPanelContent(rightDockPanel, RightDockPanelFactory.RightPanelMode.JarvisChat, factoryLogger.Object);

            // Assert
            rightDockPanel.Controls.Count.Should().BeGreaterThan(0, "RightDockPanel should contain a TabControl");
            var tabControl = (TabControl)rightDockPanel.Controls[0];
            tabControl.SelectedTab.Should().NotBeNull();
            tabControl.SelectedTab!.Name.Should().Be("JARVISChatTab");
            var jarvisTab = tabControl.TabPages.Cast<TabPage>().First(tp => tp.Name == "JARVISChatTab");
            jarvisTab.Visible.Should().BeTrue();

            // Verify logger got at least one Information call for the switch
            factoryLogger.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);

            form.Dispose();
        }

    }
}
