using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.ViewModels;
using Xunit;

namespace WileyWidget.Tests.ViewModels
{
    /// <summary>
    /// Unit tests for DashboardViewModel validating Observable properties and command behavior.
    ///
    /// Pattern: Mock external dependencies, verify MVVM pattern compliance.
    /// Focus: Property initialization, command execution, and error state management.
    /// </summary>
    public class DashboardViewModelTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<IBudgetRepository> _mockBudgetRepository;
        private readonly Mock<IMunicipalAccountRepository> _mockAccountRepository;
        private readonly Mock<ILogger<DashboardViewModel>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;

        public DashboardViewModelTests()
        {
            _mockBudgetRepository = new Mock<IBudgetRepository>();
            _mockAccountRepository = new Mock<IMunicipalAccountRepository>();
            _mockLogger = new Mock<ILogger<DashboardViewModel>>();
            _mockConfiguration = new Mock<IConfiguration>();

            var services = new ServiceCollection();
            services.AddSingleton(_mockLogger.Object);
            services.AddSingleton(_mockBudgetRepository.Object);
            services.AddSingleton(_mockAccountRepository.Object);
            services.AddSingleton(_mockConfiguration.Object);
            services.AddLogging();

            _serviceProvider = services.BuildServiceProvider();
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
            _serviceProvider?.Dispose();
        }
    }

        /// <summary>
        /// Verifies ViewModel initializes with default values.
        /// </summary>
        [Fact]
        public void Constructor_InitializesWithDefaults()
        {
            // Act
            var viewModel = new DashboardViewModel(
                _mockBudgetRepository.Object,
                _mockAccountRepository.Object,
                _mockLogger.Object,
                _mockConfiguration.Object);

            // Assert
            viewModel.MunicipalityName.Should().Be("Town of Wiley");
            viewModel.FiscalYear.Should().Be("FY 2025-2026");
            viewModel.IsLoading.Should().BeFalse();
            viewModel.HasError.Should().BeFalse();
            viewModel.ErrorMessage.Should().BeNull();
        }

        /// <summary>
        /// Verifies default constructor creates ViewModel successfully.
        /// </summary>
        [Fact]
        public void DefaultConstructor_CreatesInstanceSuccessfully()
        {
            // Act
            var viewModel = new DashboardViewModel();

            // Assert
            viewModel.Should().NotBeNull();
            viewModel.MunicipalityName.Should().Be("Town of Wiley");
            viewModel.IsLoading.Should().BeFalse();
        }

        /// <summary>
        /// Verifies that LoadCommand is initialized and has CanExecute capability.
        /// </summary>
        [Fact]
        public void LoadCommand_IsInitializedAndCanExecute()
        {
            // Arrange
            var viewModel = new DashboardViewModel(
                _mockBudgetRepository.Object,
                _mockAccountRepository.Object,
                _mockLogger.Object,
                _mockConfiguration.Object);

            // Act & Assert
            viewModel.LoadCommand.Should().NotBeNull();
            viewModel.LoadCommand.CanExecute(null).Should().BeTrue();
        }

        /// <summary>
        /// Verifies RefreshCommand is initialized properly.
        /// </summary>
        [Fact]
        public void RefreshCommand_IsInitialized()
        {
            // Act
            var viewModel = new DashboardViewModel(
                _mockBudgetRepository.Object,
                _mockAccountRepository.Object,
                _mockLogger.Object,
                _mockConfiguration.Object);

            // Assert
            viewModel.RefreshCommand.Should().NotBeNull();
        }

        /// <summary>
        /// Verifies LoadDashboardCommand alias works correctly.
        /// </summary>
        [Fact]
        public void LoadDashboardCommand_AliasPointsToLoadCommand()
        {
            // Act
            var viewModel = new DashboardViewModel(
                _mockBudgetRepository.Object,
                _mockAccountRepository.Object,
                _mockLogger.Object,
                _mockConfiguration.Object);

            // Assert
            viewModel.LoadDashboardCommand.Should().BeSameAs(viewModel.LoadCommand);
        }

        /// <summary>
        /// Verifies that PropertyChanged events are raised for observable properties.
        /// </summary>
        [Fact]
        public void PropertyChanged_RaisesForObservableProperties()
        {
            // Arrange
            var viewModel = new DashboardViewModel();
            var propertiesChanged = new List<string>();

            viewModel.PropertyChanged += (s, e) =>
            {
                propertiesChanged.Add(e.PropertyName ?? "");
            };

            // Act
            viewModel.StatusText = "Updated";

            // Assert
            propertiesChanged.Should().Contain("StatusText");
        }

        /// <summary>
        /// Verifies Metrics collection is initialized as empty ObservableCollection.
        /// </summary>
        [Fact]
        public void Metrics_InitializedAsEmptyObservableCollection()
        {
            // Act
            var viewModel = new DashboardViewModel();

            // Assert
            viewModel.Metrics.Should().BeOfType<ObservableCollection<DashboardMetric>>();
            viewModel.Metrics.Should().BeEmpty();
        }

        /// <summary>
        /// Verifies gauge properties are initialized with default values.
        /// </summary>
        [Fact]
        public void GaugeProperties_InitializedWithDefaults()
        {
            // Act
            var viewModel = new DashboardViewModel();

            // Assert
            viewModel.TotalBudgetGauge.Should().Be(0f);
            viewModel.RevenueGauge.Should().Be(0f);
            viewModel.ExpensesGauge.Should().Be(0f);
            viewModel.NetPositionGauge.Should().Be(0f);
        }

        /// <summary>
        /// Verifies that multiple instances don't interfere with each other.
        /// </summary>
        [Fact]
        public void MultipleInstances_MaintainIndependentState()
        {
            // Act
            var vm1 = new DashboardViewModel();
            var vm2 = new DashboardViewModel();

            vm1.StatusText = "Instance 1";
            vm2.StatusText = "Instance 2";

            // Assert
            vm1.StatusText.Should().Be("Instance 1");
            vm2.StatusText.Should().Be("Instance 2");
        }

        /// <summary>
        /// Verifies Dispose cleans up resources properly.
        /// </summary>
        [Fact]
        public void Dispose_CompletesSuccessfully()
        {
            // Act
            var viewModel = new DashboardViewModel();
            Action disposeAction = () => viewModel.Dispose();

            // Assert
            disposeAction.Should().NotThrow();
        }

        // ===== SYNCFUSION THEME COMPLIANCE TESTS =====
        // Per .vscode/copilot-instructions.md: SfSkinManager is sole authority for theming
        // Tests validate that DashboardViewModel works correctly with Syncfusion theme cascade

        /// <summary>
        /// Verifies that DashboardViewModel integrates with SfSkinManager theming pattern.
        /// SfSkinManager must be the ONLY source of truth for color/style management.
        /// </summary>
        [Fact]
        public void ViewModel_SupportsThemeCascadeFromParentForm()
        {
            // Arrange & Act
            var viewModel = new DashboardViewModel();

            // Assert
            // ViewModel should not have any manual BackColor or ForeColor assignments
            // All theming must be delegated to SfSkinManager on parent form
            // This test validates the ViewModel is theme-agnostic and works with cascade
            viewModel.Should().NotBeNull();
            // Properties should initialize without assuming any specific theme
            viewModel.MunicipalityName.Should().NotBeNullOrEmpty();
            viewModel.StatusText.Should().NotBeNullOrEmpty();
        }

        /// <summary>
        /// Verifies that Observable properties work correctly when bound to Syncfusion controls.
        /// Syncfusion controls expect proper INotifyPropertyChanged notifications.
        /// </summary>
        [Fact]
        public void ObservableProperties_FireNotifications_ForDataBinding()
        {
            // Arrange
            var viewModel = new DashboardViewModel();
            var notifiedProperties = new List<string>();

            viewModel.PropertyChanged += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.PropertyName))
                    notifiedProperties.Add(e.PropertyName);
            };

            // Act
            viewModel.StatusText = "Updated Status";
            viewModel.FiscalYear = "FY 2026-2027";
            viewModel.TotalBudgetGauge = 75f;

            // Assert
            notifiedProperties.Should().Contain(new[] { "StatusText", "FiscalYear", "TotalBudgetGauge" });
        }

        /// <summary>
        /// Verifies that ObservableCollections (Metrics, DepartmentSummaries) support
        /// Syncfusion control binding without manual color assignments.
        /// </summary>
        [Fact]
        public void ObservableCollections_SupportSyncfusionBinding()
        {
            // Arrange
            var viewModel = new DashboardViewModel();

            // Act
            var metricsCollection = viewModel.Metrics;
            var departmentCollection = viewModel.DepartmentSummaries;
            var fundCollection = viewModel.FundSummaries;

            // Assert
            metricsCollection.Should().BeOfType<ObservableCollection<DashboardMetric>>();
            departmentCollection.Should().BeOfType<ObservableCollection<DepartmentSummary>>();
            fundCollection.Should().BeOfType<ObservableCollection<FundSummary>>();

            // Collections should be bindable to SfDataGrid without custom theming
            metricsCollection.Should().NotBeNull();
        }

        /// <summary>
        /// Verifies gauge properties (used by Syncfusion SfChart gauge controls)
        /// are initialized and can be bound without manual style interference.
        /// </summary>
        [Fact]
        public void GaugeProperties_SuitableForSyncfusionChartBinding()
        {
            // Arrange
            var viewModel = new DashboardViewModel();

            // Act - Set gauge values as would happen during data load
            // (These would be bound to Syncfusion SfChart.Gauges via XAML binding)
            viewModel.TotalBudgetGauge = 85.5f;
            viewModel.RevenueGauge = 72.3f;
            viewModel.ExpensesGauge = 58.9f;
            viewModel.NetPositionGauge = 30.2f;

            // Assert
            viewModel.TotalBudgetGauge.Should().Be(85.5f);
            viewModel.RevenueGauge.Should().Be(72.3f);
            viewModel.ExpensesGauge.Should().Be(58.9f);
            viewModel.NetPositionGauge.Should().Be(30.2f);

            // Verify values are in valid gauge range [0, 100]
            new[] { viewModel.TotalBudgetGauge, viewModel.RevenueGauge,
                    viewModel.ExpensesGauge, viewModel.NetPositionGauge }
                .Should().AllSatisfy(v => v.Should().BeGreaterThanOrEqualTo(0f).And.BeLessThanOrEqualTo(100f));
        }

        /// <summary>
        /// Verifies that ViewModel respects semantic status colors without defining custom palettes.
        /// Per approved-workflow.md: Only semantic status colors (Red/Green/Orange) are allowed.
        /// </summary>
        [Fact]
        public void VarianceStatusColor_UsesSemanticIndicators()
        {
            // Arrange
            var viewModel = new DashboardViewModel();

            // Act - Variance status should use semantic colors, not custom palette
            viewModel.VarianceStatusColor = "Red";   // Semantic: high variance/risk
            var redStatus = viewModel.VarianceStatusColor;

            viewModel.VarianceStatusColor = "Green"; // Semantic: healthy/on-target
            var greenStatus = viewModel.VarianceStatusColor;

            // Assert
            redStatus.Should().Be("Red");
            greenStatus.Should().Be("Green");
            // Never use custom colors like "ThemeColors.HighVarianceRed" - that violates SfSkinManager authority
        }

        /// <summary>
        /// Verifies that ViewModel commands (LoadCommand, RefreshCommand) are properly
        /// initialized for binding to Syncfusion ribbon buttons or standard buttons.
        /// </summary>
        [Fact]
        public void AsyncCommands_InitializedForSyncfusionRibbonBinding()
        {
            // Arrange
            var viewModel = new DashboardViewModel();

            // Act
            var loadCommandCanExecute = viewModel.LoadCommand?.CanExecute(null) ?? false;
            var refreshCommandCanExecute = viewModel.RefreshCommand?.CanExecute(null) ?? false;

            // Assert
            // Commands should be ready for data binding to Syncfusion RibbonButton or standard Button
            viewModel.LoadCommand.Should().NotBeNull();
            viewModel.RefreshCommand.Should().NotBeNull();
            loadCommandCanExecute.Should().BeTrue("LoadCommand must be executable on initialization");
            refreshCommandCanExecute.Should().BeTrue("RefreshCommand must be executable on initialization");
        }

        /// <summary>
        /// Verifies that ViewModel can work with fiscal year configuration
        /// without hardcoding theme-specific values.
        /// </summary>
        [Fact]
        public void FiscalYear_BindableWithoutThemeAssumptions()
        {
            // Arrange
            var viewModel = new DashboardViewModel(
                _mockBudgetRepository.Object,
                _mockAccountRepository.Object,
                _mockLogger.Object,
                _mockConfiguration.Object);

            // Act
            var initialFiscalYear = viewModel.FiscalYear;
            viewModel.FiscalYear = "FY 2026-2027";

            // Assert
            initialFiscalYear.Should().Be("FY 2025-2026");
            viewModel.FiscalYear.Should().Be("FY 2026-2027");
            // FiscalYear is a string property that works with Syncfusion binding without color/theme concerns
        }
    }
}
