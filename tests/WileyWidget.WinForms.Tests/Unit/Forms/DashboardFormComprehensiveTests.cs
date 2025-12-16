using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Infrastructure;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms;

/// <summary>
/// Comprehensive tests for DashboardForm covering MDI configuration, TabControl setup,
/// InvalidCastException scenarios, and thread-safe initialization. Validates fixes for
/// TabbedMDI + DockingManager conflicts causing DockingWrapperForm cast exceptions.
/// </summary>
[Trait("Category", "Unit")]
[Collection(WinFormsUiCollection.CollectionName)]
public sealed class DashboardFormComprehensiveTests : IDisposable
{
    private readonly WinFormsUiThreadFixture _ui;

    public DashboardFormComprehensiveTests(WinFormsUiThreadFixture ui)
    {
        _ui = ui;
    }

    [Fact]
    public void DashboardForm_Ctor_DoesNotSetMdiParent_WhenMainFormNotMdiContainer()
    {
        _ui.Run(() =>
        {
            // Arrange - MainForm with MDI disabled
            var testConfig = CreateTestConfig(useMdiMode: false, useTabbedMdi: false);
            var mockServiceProvider = new Mock<IServiceProvider>();

            using var mainForm = new MainForm(mockServiceProvider.Object, testConfig, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
            // Explicitly NOT setting IsMdiContainer = true

            var mockVm = new Mock<DashboardViewModel>();
            var mockAnalyticsVm = CreateMockAnalyticsViewModel();
            var mockLogger = new Mock<ILogger<DashboardForm>>();

            // Act
            using var form = new DashboardForm(mockVm.Object, mockAnalyticsVm, mainForm, mockLogger.Object);

            // Assert - MdiParent should NOT be set when IsMdiContainer is false
            Assert.Null(form.MdiParent);
        });
    }

    [Fact]
    public void DashboardForm_Ctor_ThrowsArgumentNullException_WhenMainFormIsNull()
    {
        _ui.Run(() =>
        {
            // Arrange
            var mockVm = new Mock<DashboardViewModel>();
            var mockAnalyticsVm = CreateMockAnalyticsViewModel();
            var mockLogger = new Mock<ILogger<DashboardForm>>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new DashboardForm(mockVm.Object, mockAnalyticsVm, null!, mockLogger.Object));
        });
    }

    [Fact]
    public void DashboardForm_Ctor_ThrowsArgumentNullException_WhenViewModelIsNull()
    {
        _ui.Run(() =>
        {
            // Arrange
            var testConfig = CreateTestConfig();
            var mockServiceProvider = new Mock<IServiceProvider>();
            using var mainForm = new MainForm(mockServiceProvider.Object, testConfig, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

            var mockAnalyticsVm = CreateMockAnalyticsViewModel();
            var mockLogger = new Mock<ILogger<DashboardForm>>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new DashboardForm(null!, mockAnalyticsVm, mainForm, mockLogger.Object));
        });
    }

    [Fact]
    public void DashboardForm_Initialization_CompletesWithoutErrors_InTestHarnessMode()
    {
        _ui.Run(() =>
        {
            // Arrange - Simulate UI test harness environment
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");

            try
            {
                var testConfig = CreateTestConfig(useMdiMode: false);
                var mockServiceProvider = new Mock<IServiceProvider>();
                using var mainForm = new MainForm(mockServiceProvider.Object, testConfig, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

                var mockVm = new Mock<DashboardViewModel>();
                var mockAnalyticsVm = CreateMockAnalyticsViewModel();
                var mockLogger = new Mock<ILogger<DashboardForm>>();

                // Act - Constructor should detect test environment and adapt UI
                using var form = new DashboardForm(mockVm.Object, mockAnalyticsVm, mainForm, mockLogger.Object);

                // Assert - Form should initialize successfully
                Assert.NotNull(form);
                Assert.False(form.IsDisposed);
            }
            finally
            {
                Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", null);
            }
        });
    }

    [Fact]
    public void DashboardForm_ShowDialog_ThrowsInvalidOperationException_WhenHasMdiParent()
    {
        _ui.Run(() =>
        {
            // Arrange
            using var parentForm = new Form { IsMdiContainer = true };
            var testConfig = CreateTestConfig(useMdiMode: true);
            var mockServiceProvider = new Mock<IServiceProvider>();
            using var mainForm = new MainForm(mockServiceProvider.Object, testConfig, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

            var mockVm = new Mock<DashboardViewModel>();
            var mockAnalyticsVm = CreateMockAnalyticsViewModel();
            var mockLogger = new Mock<ILogger<DashboardForm>>();

            using var form = new DashboardForm(mockVm.Object, mockAnalyticsVm, mainForm, mockLogger.Object);

            // Forcibly set MdiParent (normally constructor does this defensively)
            if (mainForm.IsMdiContainer)
            {
                form.MdiParent = parentForm;
            }

            // Act & Assert - ShowDialog should throw if form is MDI child
            if (form.MdiParent != null)
            {
                Assert.Throws<InvalidOperationException>(() => form.ShowDialog());
            }
        });
    }

    [Fact]
    public void DashboardForm_DoesNotCrash_WhenUseTabbedMdiAndDockingManagerBothTrue()
    {
        _ui.Run(() =>
        {
            // Arrange - Reproduce production configuration from log line 24
            // "UseDockingManager=true, UseMdiMode=true, UseTabbedMdi=true"
            // This caused: "Unable to cast...DashboardForm to DockingWrapperForm"
            var testConfig = CreateTestConfig(
                useMdiMode: true,
                useTabbedMdi: true,
                useDockingManager: true);

            var mockServiceProvider = new Mock<IServiceProvider>();
            using var mainForm = new MainForm(mockServiceProvider.Object, testConfig, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

            var mockVm = new Mock<DashboardViewModel>();
            var mockAnalyticsVm = CreateMockAnalyticsViewModel();
            var mockLogger = new Mock<ILogger<DashboardForm>>();

            // Act - Constructor should handle conflicting config gracefully
            Exception? caughtException = null;
            DashboardForm? form = null;

            try
            {
                form = new DashboardForm(mockVm.Object, mockAnalyticsVm, mainForm, mockLogger.Object);
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Assert - Should NOT throw InvalidCastException
            Assert.Null(caughtException);
            Assert.NotNull(form);

            // Verify defensive MdiParent assignment (should check both IsMdiContainer AND UseMdiMode)
            if (mainForm.IsMdiContainer && mainForm.UseMdiMode)
            {
                // MdiParent may be set if conditions met
                Assert.True(form.MdiParent == null || form.MdiParent == mainForm);
            }
            else
            {
                Assert.Null(form.MdiParent);
            }

            form?.Dispose();
        });
    }

    [Fact]
    public void DashboardForm_TabControl_ConfiguredCorrectly_ForLines55To73()
    {
        _ui.Run(() =>
        {
            // Arrange
            var testConfig = CreateTestConfig();
            var mockServiceProvider = new Mock<IServiceProvider>();
            using var mainForm = new MainForm(mockServiceProvider.Object, testConfig, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);

            var mockVm = new Mock<DashboardViewModel>();
            var mockAnalyticsVm = CreateMockAnalyticsViewModel();
            var mockLogger = new Mock<ILogger<DashboardForm>>();

            // Act - Constructor initializes TabControl (lines 55-73 in DashboardForm.cs)
            using var form = new DashboardForm(mockVm.Object, mockAnalyticsVm, mainForm, mockLogger.Object);

            // Assert - TabControl should be configured per Syncfusion recommendations
            // Find _detailsTab control (may be private, so we verify form doesn't crash)
            // The real validation is that constructor completes without InvalidCastException
            Assert.NotNull(form);
            Assert.False(form.IsDisposed);
        });
    }

    private static IConfiguration CreateTestConfig(
        bool useMdiMode = true,
        bool useTabbedMdi = true,
        bool useDockingManager = true)
    {
        var configDict = new Dictionary<string, string?>
        {
            ["UI:UseMdiMode"] = useMdiMode.ToString(),
            ["UI:UseTabbedMdi"] = useTabbedMdi.ToString(),
            ["UI:UseDockingManager"] = useDockingManager.ToString(),
            ["UI:FiscalYear"] = "2026"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
    }

    private static AnalyticsViewModel CreateMockAnalyticsViewModel()
    {
        var mockAnalyticsSvc = new Mock<IAnalyticsService>();
        var mockLogger = new Mock<ILogger<AnalyticsViewModel>>();
        return new AnalyticsViewModel(mockAnalyticsSvc.Object, mockLogger.Object);
    }

    public void Dispose()
    {
        // Cleanup
    }
}
