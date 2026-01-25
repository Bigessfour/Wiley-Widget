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
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Syncfusion.WinForms.Themes;
using Syncfusion.WinForms.Controls;
using Xunit;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
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
                SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                SfSkinManager.ApplicationVisualTheme = theme;
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
            SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
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
            SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
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
            SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
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
            var tabControl = (TabControl)rightDockPanel.Controls[0];
            tabControl.SelectedTab.Should().NotBeNull();
            tabControl.SelectedTab!.Name.Should().Be("JARVISChatTab");
            var jarvisTab = tabControl.TabPages.Cast<TabPage>().First(tp => tp.Name == "JARVISChatTab");
            jarvisTab.Visible.Should().BeTrue();

            // Verify logger got at least one Information call for the switch
            factoryLogger.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);

            form.Dispose();
        }

        [StaFact]
        public async Task JARVISChatHostForm_SendsInitialPrompt_AfterDelay()
        {
            // Arrange
            SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton(ReportViewerLaunchOptions.Disabled);
            services.AddWindowsFormsBlazorWebView();
            services.AddSingleton<IThemeService>(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider)!);
            services.AddSingleton<IChatBridgeService>(_ =>
            {
                var mock = new Mock<IChatBridgeService>();
                mock.Setup(m => m.RequestExternalPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
                return mock.Object;
            });
            services.AddLogging(builder => builder.AddDebug());

            var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            var chatBridge = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IChatBridgeService>(sp);
            var chatBridgeMock = Mock.Get(chatBridge);

            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider)!;
            var logger = new Mock<ILogger<JARVISChatHostForm>>();

            var form = new JARVISChatHostForm(sp, themeService, logger.Object)
            {
                InitialPrompt = "Hello JARVIS"
            };

            // Act: ensure handle exists and call OnShown to trigger initial prompt schedule
            var _ = form.Handle;
            form.Visible = true;
            typeof(JARVISChatHostForm).GetMethod("OnShown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, new object[] { EventArgs.Empty });

            // Wait for up to ~4 seconds for the RequestExternalPromptAsync to be called
            var called = false;
            for (int i = 0; i < 40; i++)
            {
                Application.DoEvents();
                try
                {
                    chatBridgeMock.Verify(m => m.RequestExternalPromptAsync(It.Is<string>(s => s.Contains("Hello JARVIS")), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
                    called = true;
                    break;
                }
                catch (MockException)
                {
                    await Task.Delay(100);
                }
            }

            called.Should().BeTrue("the initial prompt should be sent after the short UI-ready delay");

            form.Dispose();
        }

        [StaFact]
        public async Task JARVISChatHostForm_ClosingBeforePrompt_DoesNotSendPrompt_AndLogsClosing()
        {
            // Arrange
            SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton(ReportViewerLaunchOptions.Disabled);
            services.AddWindowsFormsBlazorWebView();
            services.AddSingleton<IThemeService>(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider)!);
            var chatBridgeMock = new Mock<IChatBridgeService>();
            chatBridgeMock.Setup(m => m.RequestExternalPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            services.AddSingleton<IChatBridgeService>(_ => chatBridgeMock.Object);
            services.AddLogging(builder => builder.AddDebug());

            var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider)!;
            var logger = new Mock<ILogger<JARVISChatHostForm>>();

            var form = new JARVISChatHostForm(sp, themeService, logger.Object)
            {
                InitialPrompt = "Will be cancelled"
            };

            // Act: ensure handle exists and call OnShown to trigger initial prompt schedule
            var _ = form.Handle;
            form.Visible = true;
            typeof(JARVISChatHostForm).GetMethod("OnShown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, new object[] { EventArgs.Empty });

            // Close immediately before the 1500ms delay completes
            form.Close();

            // Allow time for the invoked delegate to run and honor IsDisposed check
            for (int i = 0; i < 30; i++)
            {
                Application.DoEvents();
                await Task.Delay(50);
            }

            // Assert: chat bridge not called and closing was logged
            chatBridgeMock.Verify(m => m.RequestExternalPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            logger.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
        }
    }
}
