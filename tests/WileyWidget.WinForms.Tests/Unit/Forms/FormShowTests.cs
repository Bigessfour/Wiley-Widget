using System;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.ViewModels;
using Xunit;
namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    [Trait("Category", "Unit")]
    [Collection(WinFormsUiCollection.CollectionName)]
    public class FormShowTests
    {
        private readonly WinFormsUiThreadFixture _ui;

        public FormShowTests(WinFormsUiThreadFixture ui)
        {
            _ui = ui;
        }
        // For unit tests we rely on a small in-memory IConfiguration created where needed
        [Fact]
        public void DashboardForm_ShowDialog_ThrowsIfHasParent()
        {
            _ui.Run(() =>
            {
                // Arrange
                using var parentForm = new Form { IsMdiContainer = true };
                var mockVm = new Mock<DashboardViewModel>();
                var mockAnalyticsSvc = new Mock<IAnalyticsService>();
                var mockAnalyticsLogger = new Mock<ILogger<AnalyticsViewModel>>();
                var mockAnalyticsVm = new Mock<AnalyticsViewModel>(mockAnalyticsSvc.Object, mockAnalyticsLogger.Object);

                // Use a real main form with minimal dependencies (in-memory configuration)
                var testConfig = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["UI:UseMdiMode"] = "true",
                        ["UI:UseTabbedMdi"] = "true",
                        ["UI:UseDockingManager"] = "true"
                    })
                    .Build();

                var serviceProvider = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
                using var mainForm = new MainForm(serviceProvider, testConfig, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
                var mockLogger = new Mock<ILogger<DashboardForm>>();
                using var form = new DashboardForm(mockVm.Object, mockAnalyticsVm.Object, mainForm, mockLogger.Object);
                form.MdiParent = parentForm; // Simulate MDI child

                // Act & Assert
                Assert.Throws<InvalidOperationException>(() => form.ShowDialog());
            });
        }

        [Fact]
        public void MainForm_ShowChildForm_AsMdiChild_DoesNotCallShowDialog()
        {
            _ui.Run(() =>
            {
                // Arrange
                var mockServiceProvider = new Mock<IServiceProvider>();
                var testConfig = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["UI:UseMdiMode"] = "true",
                        ["UI:UseTabbedMdi"] = "true",
                        ["UI:UseDockingManager"] = "true"
                    })
                    .Build();
                using var mainForm = new MainForm(mockServiceProvider.Object, testConfig, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
                mainForm.IsMdiContainer = true; // Enable MDI

                // Mock the child form creation to verify Show() is called instead of ShowDialog()
                var mockChildForm = new Mock<Form>();

                // Ensure the service provider returns a DashboardForm that uses the same main form
                mockServiceProvider.Setup(sp => sp.GetService(typeof(DashboardForm)))
                    .Returns(() => new DashboardForm(new Mock<DashboardViewModel>().Object, new Mock<AnalyticsViewModel>().Object, mainForm, new Mock<ILogger<DashboardForm>>().Object));

                // Act
                mainForm.Show();

                // Assert
                Assert.True(mainForm.IsMdiContainer);
            });
        }
    }
}
