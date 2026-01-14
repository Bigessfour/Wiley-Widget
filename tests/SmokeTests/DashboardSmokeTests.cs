using System;
using System.Collections.ObjectModel;
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

namespace SmokeTests.ViewModels
{
    public class DashboardSmokeTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<IBudgetRepository> _mockBudgetRepository;
        private readonly Mock<IMunicipalAccountRepository> _mockAccountRepository;
        private readonly Mock<ILogger<DashboardViewModel>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;

        public DashboardSmokeTests()
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

        [Fact]
        public void Constructor_InitializesWithDefaults()
        {
            var viewModel = new DashboardViewModel(
                _mockBudgetRepository.Object,
                _mockAccountRepository.Object,
                _mockLogger.Object,
                _mockConfiguration.Object);

            viewModel.MunicipalityName.Should().Be("Town of Wiley");
            viewModel.FiscalYear.Should().Be("FY 2025-2026");
            viewModel.IsLoading.Should().BeFalse();
            viewModel.HasError.Should().BeFalse();
            viewModel.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public void PropertyChanged_RaisesForObservableProperties()
        {
            var viewModel = new DashboardViewModel();
            var propertiesChanged = new System.Collections.Generic.List<string>();

            viewModel.PropertyChanged += (s, e) => propertiesChanged.Add(e.PropertyName ?? "");

            viewModel.StatusText = "Updated";

            propertiesChanged.Should().Contain("StatusText");
        }

        [Fact]
        public void Metrics_InitializedAsEmptyObservableCollection()
        {
            var viewModel = new DashboardViewModel();

            viewModel.Metrics.Should().BeOfType<ObservableCollection<DashboardMetric>>();
            viewModel.Metrics.Should().BeEmpty();
        }

        [Fact]
        public void Dispose_CompletesSuccessfully()
        {
            var viewModel = new DashboardViewModel();
            Action disposeAction = () => viewModel.Dispose();
            disposeAction.Should().NotThrow();
        }
    }
}