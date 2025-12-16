using Xunit;
using Xunit.Sdk;
using Moq;
using System;
using System.Windows.Forms;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using WileyWidget.WinForms.Tests.Infrastructure;

namespace WileyWidget.WinForms.Tests.Unit.Forms;

/// <summary>
/// Tests for MDI child form defensive patterns.
/// Validates that child forms handle IsMdiContainer checks correctly per project guidelines.
/// </summary>
[Collection(WinFormsUiCollection.CollectionName)]
public class MdiChildFormTests : IDisposable
{
    private readonly WinFormsUiThreadFixture _ui;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<MainForm>> _mockLogger;
    private MainForm? _mainForm;
    private readonly List<Form> _formsToDispose = new();

    public MdiChildFormTests(WinFormsUiThreadFixture ui)
    {
        _ui = ui;
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<MainForm>>();

        // Setup required configuration values for MainForm constructor
        // MainForm.cs line 81 uses GetValue<T> with key lookups, so mock the section access
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("false");

        _mockConfiguration.Setup(c => c.GetSection("UI:UIMode")).Returns(mockSection.Object);
        _mockConfiguration.Setup(c => c.GetSection("UI:UseDockingManager")).Returns(mockSection.Object);
        _mockConfiguration.Setup(c => c.GetSection("UI:UseMdiMode")).Returns(mockSection.Object);
        _mockConfiguration.Setup(c => c.GetSection("UI:UseTabbedMdi")).Returns(mockSection.Object);
        _mockConfiguration.Setup(c => c.GetSection("UI:IsUiTestHarness")).Returns(mockSection.Object);

        // Also mock the key-value access for older config patterns
        _mockConfiguration.Setup(c => c["UI:IsUiTestHarness"]).Returns("true");
        _mockConfiguration.Setup(c => c["UI:UseMdiMode"]).Returns("false");
        _mockConfiguration.Setup(c => c["UI:UseTabbedMdi"]).Returns("false");
        _mockConfiguration.Setup(c => c["UI:UseDockingManager"]).Returns("false");
    }

    [Fact]
    public void SettingsForm_ShouldSetMdiParent_WhenIsMdiContainerIsTrue()
    {
        _ui.Run(() =>
        {
            // Arrange
            _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled)
            {
                IsMdiContainer = true
            };
            _formsToDispose.Add(_mainForm);

            var mockViewModel = new Mock<SettingsViewModel>();

            // Act
            var childForm = new SettingsForm(mockViewModel.Object, _mainForm);
            _formsToDispose.Add(childForm);

            // Assert
            childForm.MdiParent.Should().NotBeNull();
            childForm.MdiParent.Should().Be(_mainForm);
        });
    }

    [Fact]
    public void SettingsForm_ShouldNotThrow_WhenIsMdiContainerIsFalse()
    {
        _ui.Run(() =>
        {
            // Arrange
            _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled)
            {
                IsMdiContainer = false
            };
            _formsToDispose.Add(_mainForm);

            var mockViewModel = new Mock<SettingsViewModel>();

            // Act
            Action act = () =>
            {
                var childForm = new SettingsForm(mockViewModel.Object, _mainForm);
                _formsToDispose.Add(childForm);
            };

            // Assert - should not throw ArgumentException
            act.Should().NotThrow<ArgumentException>("defensive pattern should prevent exception when IsMdiContainer is false");
        });
    }

    [Fact]
    public void SettingsForm_ShouldHaveNullMdiParent_WhenIsMdiContainerIsFalse()
    {
        _ui.Run(() =>
        {
            // Arrange
            _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled)
            {
                IsMdiContainer = false
            };
            _formsToDispose.Add(_mainForm);

            var mockViewModel = new Mock<SettingsViewModel>();

            // Act
            var childForm = new SettingsForm(mockViewModel.Object, _mainForm);
            _formsToDispose.Add(childForm);

            // Assert
            childForm.MdiParent.Should().BeNull("MdiParent should not be set when IsMdiContainer is false");
        });
    }

    [Fact]
    public void ChartForm_ShouldSetMdiParent_WhenIsMdiContainerIsTrue()
    {
        _ui.Run(() =>
        {
            // Arrange
            _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled)
            {
                IsMdiContainer = true
            };
            _formsToDispose.Add(_mainForm);

            var mockLogger = new Mock<ILogger<ChartViewModel>>();
            var mockDashboardSvc = new Mock<IDashboardService>();
            var vm = new ChartViewModel(mockLogger.Object, mockDashboardSvc.Object);

            // Act
            var childForm = new ChartForm(vm, _mainForm);
            _formsToDispose.Add(childForm);

            // Assert
            childForm.MdiParent.Should().NotBeNull();
            childForm.MdiParent.Should().Be(_mainForm);
        });
    }

    [Fact]
    public void ChartForm_ShouldNotThrow_WhenIsMdiContainerIsFalse()
    {
        _ui.Run(() =>
        {
            // Arrange
            _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled)
            {
                IsMdiContainer = false
            };
            _formsToDispose.Add(_mainForm);

            var mockLogger = new Mock<ILogger<ChartViewModel>>();
            var mockDashboardSvc = new Mock<IDashboardService>();
            var vm = new ChartViewModel(mockLogger.Object, mockDashboardSvc.Object);

            // Act
            Action act = () =>
            {
                var childForm = new ChartForm(vm, _mainForm);
                _formsToDispose.Add(childForm);
            };

            // Assert - should not throw ArgumentException
            act.Should().NotThrow<ArgumentException>("defensive pattern should prevent exception when IsMdiContainer is false");
        });
    }

    [Fact]
    public void ChartForm_ShouldHaveNullMdiParent_WhenIsMdiContainerIsFalse()
    {
        _ui.Run(() =>
        {
            // Arrange
            _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled)
            {
                IsMdiContainer = false
            };
            _formsToDispose.Add(_mainForm);

            var mockLogger = new Mock<ILogger<ChartViewModel>>();
            var mockDashboardSvc = new Mock<IDashboardService>();
            ChartViewModel vm = NewMethod(mockLogger, mockDashboardSvc);

            // Act
            var childForm = new ChartForm(vm, _mainForm);
            _formsToDispose.Add(childForm);

            // Assert
            childForm.MdiParent.Should().BeNull("MdiParent should not be set when IsMdiContainer is false");
        });
    }

    private static ChartViewModel NewMethod(Mock<ILogger<ChartViewModel>> mockLogger, Mock<IDashboardService> mockDashboardSvc)
    {
        return new ChartViewModel(mockLogger.Object, mockDashboardSvc.Object);
    }

    [Fact]
    public void ReportsForm_ShouldSetMdiParent_WhenIsMdiContainerIsTrue()
    {
        _ui.Run(() =>
        {
            // Arrange
            _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled)
            {
                IsMdiContainer = true
            };
            _formsToDispose.Add(_mainForm);

            var mockReportSvc = new Mock<IReportService>();
            var mockAuditSvc = new Mock<IAuditService>();
            var mockLogger = new Mock<ILogger<ReportsViewModel>>();
            var vm = new ReportsViewModel(mockReportSvc.Object, mockLogger.Object, mockAuditSvc.Object);

            // Act
            var childForm = new ReportsForm(vm, new Mock<ILogger<ReportsForm>>().Object, _mainForm);
            _formsToDispose.Add(childForm);

            // Assert
            childForm.MdiParent.Should().NotBeNull();
            childForm.MdiParent.Should().Be(_mainForm);
        });
    }

    [Fact]
    public void ReportsForm_ShouldNotThrow_WhenIsMdiContainerIsFalse()
    {
        _ui.Run(() =>
        {
            // Arrange
            _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled)
            {
                IsMdiContainer = false
            };
            _formsToDispose.Add(_mainForm);

            var mockReportSvc = new Mock<IReportService>();
            var mockAuditSvc = new Mock<IAuditService>();
            var mockLogger = new Mock<ILogger<ReportsViewModel>>();
            var vm = new ReportsViewModel(mockReportSvc.Object, mockLogger.Object, mockAuditSvc.Object);

            // Act
            Action act = () =>
            {
                var childForm = new ReportsForm(vm, new Mock<ILogger<ReportsForm>>().Object, _mainForm);
                _formsToDispose.Add(childForm);
            };

            // Assert - should not throw ArgumentException
            act.Should().NotThrow<ArgumentException>("defensive pattern should prevent exception when IsMdiContainer is false");
        });
    }

    [Fact]
    public void ReportsForm_ShouldHaveNullMdiParent_WhenIsMdiContainerIsFalse()
    {
        _ui.Run(() =>
        {
            // Arrange
            _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled)
            {
                IsMdiContainer = false
            };
            _formsToDispose.Add(_mainForm);

            var mockReportSvc = new Mock<IReportService>();
            var mockAuditSvc = new Mock<IAuditService>();
            var mockLogger = new Mock<ILogger<ReportsViewModel>>();
            var vm = new ReportsViewModel(mockReportSvc.Object, mockLogger.Object, mockAuditSvc.Object);

            // Act
            var childForm = new ReportsForm(vm, new Mock<ILogger<ReportsForm>>().Object, _mainForm);
            _formsToDispose.Add(childForm);

            // Assert
            childForm.MdiParent.Should().BeNull("MdiParent should not be set when IsMdiContainer is false");
        });
    }

    // NOTE: AccountsForm test removed - AccountsViewModel requires AppDbContext
    // constructor parameter which cannot be mocked by Moq (requires concrete DB).
    // This validation belongs in integration tests with InMemory database.
    // See TEST_DESIGN_ANALYSIS.md Phase 2 for integration test roadmap.

    [Fact]
    public void MainForm_ShouldInitializeWithIsMdiContainer()
    {
        _ui.Run(() =>
        {
            // Arrange & Act
            _mainForm = new MainForm(_mockServiceProvider.Object, _mockConfiguration.Object, _mockLogger.Object, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
            _formsToDispose.Add(_mainForm);

            // Assert - MainForm should be configurable as MDI container
            Action act = () => _mainForm.IsMdiContainer = true;
            act.Should().NotThrow();
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var form in _formsToDispose)
            {
                try
                {
                    form?.Dispose();
                }
                catch
                {
                    // Ignore disposal errors in tests
                }
            }
            _formsToDispose.Clear();

            _mainForm?.Dispose();
            _mainForm = null;
        }
    }
}
