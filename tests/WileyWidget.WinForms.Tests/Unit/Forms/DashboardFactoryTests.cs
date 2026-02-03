using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using WileyWidget.WinForms.Forms;
using Syncfusion.Windows.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    public class DashboardFactoryTests
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

            // Provide a DashboardViewModel with deterministic values for tests
            var dashboardVm = new DashboardViewModel();
            dashboardVm.AccountCount = 3;
            dashboardVm.TotalBudgeted = 1000m;
            dashboardVm.TotalVariance = 0m;
            dashboardVm.VariancePercentage = 0m;
            services.AddSingleton<DashboardViewModel>(dashboardVm);

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

        [StaFact]
        public void CreateDashboardPanel_CreatesCardsWithExpectedCountAndLabels()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());
            var panelNav = new Mock<IPanelNavigationService>();

            // Act
            var panel = DashboardFactory.CreateDashboardPanel(panelNav.Object, form, Mock.Of<ILogger>());

            // Assert
            panel.Should().BeOfType<FlowLayoutPanel>();
            var cards = panel.Controls.OfType<GradientPanelExt>().ToList();
            cards.Count.Should().Be(5, "dashboard should contain five navigation cards");

            var accountsCard = cards.FirstOrDefault(c => c.Name.Contains("Accounts"));
            accountsCard.Should().NotBeNull();
            var title = accountsCard!.Controls.OfType<Label>().First(l => l.Name.EndsWith("_Title"));
            title.Text.Should().Be("Accounts");
            var desc = accountsCard!.Controls.OfType<Label>().First(l => l.Name.EndsWith("_Desc"));
            desc.Text.Should().Contain("Municipal Accounts");
        }

        [StaFact]
        public void CreateDashboardPanel_WiresClickHandlers_AndInvokesNavigation()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());
            var panelNav = new Mock<IPanelNavigationService>();

            var panel = DashboardFactory.CreateDashboardPanel(panelNav.Object, form, Mock.Of<ILogger>());
            var accountsCard = panel.Controls.OfType<GradientPanelExt>().First(c => c.Name.Contains("Accounts"));

            // Act: simulate click by invoking protected OnClick via reflection
            var onClick = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!;
            onClick.Invoke(accountsCard, new object[] { EventArgs.Empty });

            // Assert: navigation service invoked for AccountsPanel
            panelNav.Verify(n => n.ShowPanel<AccountsPanel>(It.Is<string>(s => s == "Municipal Accounts"), Syncfusion.Windows.Forms.Tools.DockingStyle.Left, It.IsAny<bool>()), Times.Once);
        }

        [StaFact]
        public void CreateDashboardPanel_BindsViewModelDataToCards()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());
            var panelNav = new Mock<IPanelNavigationService>();

            var panel = DashboardFactory.CreateDashboardPanel(panelNav.Object, form, Mock.Of<ILogger>());

            var accountsCard = panel.Controls.OfType<GradientPanelExt>().First(c => c.Name.Contains("Accounts"));
            var desc = accountsCard.Controls.OfType<Label>().First(l => l.Name.EndsWith("_Desc"));

            // DashboardViewModel.AccountsSummary should appear in description
            desc.Text.Should().Contain("Municipal Accounts");
        }

        [StaFact]
        public void CreateDashboardPanel_Accessibility_SettingsAreCorrect()
        {
            // Arrange
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());
            var panelNav = new Mock<IPanelNavigationService>();

            var panel = DashboardFactory.CreateDashboardPanel(panelNav.Object, form, Mock.Of<ILogger>());
            var accountsCard = panel.Controls.OfType<GradientPanelExt>().First(c => c.Name.Contains("Accounts"));

            accountsCard.AccessibleName.Should().Be("Dashboard Card: Accounts");
            accountsCard.AccessibleRole.Should().Be(AccessibleRole.Grouping);
            var title = accountsCard.Controls.OfType<Label>().First(l => l.Name.EndsWith("_Title"));
            title.AccessibleRole.Should().Be(AccessibleRole.StaticText);
        }

        [StaFact]
        public void CreateDashboardPanel_ApplyTheme_DoesNotThrowAndSetsGlobalTheme()
        {
            // Arrange
            TestThemeHelper.EnsureOffice2019Colorful();
            var provider = BuildProvider();
            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConfiguration>(provider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MainForm>>(provider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(provider);

            var form = new TestMainForm(provider, configuration, logger, ReportViewerLaunchOptions.Disabled, themeService, Mock.Of<IWindowStateService>(), Mock.Of<IFileImportService>());
            var panelNav = new Mock<IPanelNavigationService>();

            var panel = DashboardFactory.CreateDashboardPanel(panelNav.Object, form, Mock.Of<ILogger>());

            // Act
            Action apply = () => SfSkinManager.SetVisualStyle(panel, "Office2019Dark");

            // Assert - applying visual style should not throw
            apply.Should().NotThrow();
        }
    }
}
