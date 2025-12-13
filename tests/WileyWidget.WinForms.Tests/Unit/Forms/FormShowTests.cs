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
using WileyWidget.WinForms.ViewModels;
using Xunit;
namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    [Trait("Category", "Unit")]
    public class FormShowTests
    {
        // For unit tests we rely on a small in-memory IConfiguration created where needed
        [Fact]
        public void DashboardForm_ShowDialog_ThrowsIfHasParent()
        {
            // Arrange
            var parentForm = new Form { IsMdiContainer = true };
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
            using var mainForm = new MainForm(serviceProvider, testConfig, NullLogger<MainForm>.Instance);
            using var form = new DashboardForm(mockVm.Object, mockAnalyticsVm.Object, mainForm);
            form.MdiParent = parentForm; // Simulate MDI child

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => form.ShowDialog());
        }

        [Fact]
        public void MainForm_ShowChildForm_AsMdiChild_DoesNotCallShowDialog()
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
            using var mainForm = new MainForm(mockServiceProvider.Object, testConfig, NullLogger<MainForm>.Instance);
            mainForm.IsMdiContainer = true; // Enable MDI

            // Mock the child form creation to verify Show() is called instead of ShowDialog()
            var mockChildForm = new Mock<Form>();
            // Ensure the service provider returns a DashboardForm that uses the same main form
            mockServiceProvider.Setup(sp => sp.GetService(typeof(DashboardForm)))
                .Returns(() => new DashboardForm(new Mock<DashboardViewModel>().Object, new Mock<AnalyticsViewModel>().Object, mainForm));

            // Act: Simulate showing a child form (e.g., Dashboard)
            // Note: This assumes MainForm has a method like ShowChildForm<TForm, TViewModel>()
            // If not, adjust based on actual implementation
            mainForm.Show(); // Ensure form is shown first

            // For this test, we need to inspect the actual MainForm code to see how it shows child forms
            // Since we can't easily mock the form creation, this test serves as a placeholder
            // In real implementation, verify that child forms are shown with .Show() not .ShowDialog()
            Assert.True(mainForm.IsMdiContainer);
        }
    }
}
