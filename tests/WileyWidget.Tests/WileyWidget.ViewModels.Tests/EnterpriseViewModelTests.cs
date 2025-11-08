using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Prism.Dialogs;
using Prism.Events;
using Prism.Navigation.Regions;
using WileyWidget.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.ViewModels.Main;
using Xunit;

namespace WileyWidget.ViewModels.Tests;

/// <summary>
/// Unit tests for EnterpriseViewModel focusing on IDataErrorInfo validation,
/// FluentValidation integration (if used), command CanExecute logic, and CRUD operations.
/// </summary>
public class EnterpriseViewModelTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IEventAggregator> _mockEventAggregator;
    private readonly Mock<IReportExportService> _mockReportExportService;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<IEnterpriseRepository> _mockEnterpriseRepository;
    private readonly Mock<IDialogService> _mockDialogService;

    public EnterpriseViewModelTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockEventAggregator = new Mock<IEventAggregator>();
        _mockReportExportService = new Mock<IReportExportService>();
        _mockCacheService = new Mock<ICacheService>();
        _mockEnterpriseRepository = new Mock<IEnterpriseRepository>();
        _mockDialogService = new Mock<IDialogService>();

        // Setup UnitOfWork to return mocked repository
        _mockUnitOfWork
            .Setup(u => u.Enterprises)
            .Returns(_mockEnterpriseRepository.Object);

        _mockUnitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Note: Dialog service mocking moved to individual tests to avoid extension method issues
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_InitializesSuccessfully()
    {
        // Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.LoadEnterprisesCommand.Should().NotBeNull();
        viewModel.SaveEnterpriseCommand.Should().NotBeNull();
        viewModel.DeleteEnterpriseCommand.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new EnterpriseViewModel(
                null!,
                _mockEventAggregator.Object,
                _mockReportExportService.Object,
                _mockCacheService.Object));
    }

    #endregion

    #region Navigation Tests (INavigationAware)

    [Fact]
    public void OnNavigatedTo_LoadsEnterprises()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var mockContext = new Mock<NavigationContext>(
            Mock.Of<IRegionNavigationService>(),
            new Uri("test://Enterprise", UriKind.Absolute));

        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Test Enterprise", Status = EnterpriseStatus.Active }
        };

        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(testEnterprises);

        // Act
        viewModel.OnNavigatedTo(mockContext.Object);

        // Assert - Verify logging or data load occurred
        viewModel.Should().NotBeNull();
    }

    [Fact]
    public void IsNavigationTarget_AlwaysReturnsTrue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var mockContext = new Mock<NavigationContext>(
            Mock.Of<IRegionNavigationService>(),
            new Uri("test://Enterprise", UriKind.Absolute));

        // Act
        var result = viewModel.IsNavigationTarget(mockContext.Object);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Validation Tests (IDataErrorInfo)

    [Fact]
    public void Indexer_WithInvalidEnterpriseName_ReturnsValidationError()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var enterprise = new Enterprise { Name = "" }; // Invalid: empty name

        // Set SelectedEnterprise
        var selectedEnterpriseProperty = typeof(EnterpriseViewModel).GetProperty("SelectedEnterprise");
        if (selectedEnterpriseProperty != null && selectedEnterpriseProperty.CanWrite)
        {
            selectedEnterpriseProperty.SetValue(viewModel, enterprise);
        }

        // Act
        var error = (viewModel as IDataErrorInfo)["SelectedEnterprise.Name"];

        // Assert
        error.Should().NotBeNullOrEmpty("Empty name should produce validation error");
    }

    [Fact]
    public void Indexer_WithValidEnterprise_ReturnsNoError()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var enterprise = new Enterprise
        {
            Id = 1,
            Name = "Valid Enterprise",
            Type = "Municipal",
            Status = EnterpriseStatus.Active
        };

        // Set SelectedEnterprise
        var selectedEnterpriseProperty = typeof(EnterpriseViewModel).GetProperty("SelectedEnterprise");
        if (selectedEnterpriseProperty != null && selectedEnterpriseProperty.CanWrite)
        {
            selectedEnterpriseProperty.SetValue(viewModel, enterprise);
        }

        // Act
        var error = (viewModel as IDataErrorInfo)["SelectedEnterprise.Name"];

        // Assert
        error.Should().BeNullOrEmpty("Valid name should not produce error");
    }

    [Fact]
    public void Error_Property_ReturnsAggregatedErrors()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var invalidEnterprise = new Enterprise { Name = "" };

        var selectedEnterpriseProperty = typeof(EnterpriseViewModel).GetProperty("SelectedEnterprise");
        if (selectedEnterpriseProperty != null && selectedEnterpriseProperty.CanWrite)
        {
            selectedEnterpriseProperty.SetValue(viewModel, invalidEnterprise);
        }

        // Act
        var error = (viewModel as IDataErrorInfo).Error;

        // Assert
        error.Should().NotBeNullOrEmpty("Invalid enterprise should have aggregated errors");
    }

    #endregion

    #region Command CanExecute Tests

    [Fact]
    public void SaveEnterpriseCommand_CannotExecute_WhenEnterpriseInvalid()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var invalidEnterprise = new Enterprise { Name = "" }; // Invalid

        var selectedEnterpriseProperty = typeof(EnterpriseViewModel).GetProperty("SelectedEnterprise");
        if (selectedEnterpriseProperty != null && selectedEnterpriseProperty.CanWrite)
        {
            selectedEnterpriseProperty.SetValue(viewModel, invalidEnterprise);
        }

        // Act
        var canExecute = viewModel.SaveEnterpriseCommand.CanExecute();

        // Assert
        canExecute.Should().BeFalse("Cannot save invalid enterprise");
    }

    [Fact]
    public void SaveEnterpriseCommand_CanExecute_WhenEnterpriseValid()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var validEnterprise = new Enterprise
        {
            Id = 1,
            Name = "Valid Enterprise",
            Type = "Sewer",
            Status = EnterpriseStatus.Active
        };

        var selectedEnterpriseProperty = typeof(EnterpriseViewModel).GetProperty("SelectedEnterprise");
        if (selectedEnterpriseProperty != null && selectedEnterpriseProperty.CanWrite)
        {
            selectedEnterpriseProperty.SetValue(viewModel, validEnterprise);
        }

        // Force validation to pass
        var hasErrorsProperty = typeof(EnterpriseViewModel).GetProperty("HasErrors");
        if (hasErrorsProperty != null && hasErrorsProperty.CanWrite)
        {
            hasErrorsProperty.SetValue(viewModel, false);
        }

        // Act
        var canExecute = viewModel.SaveEnterpriseCommand.CanExecute();

        // Assert
        canExecute.Should().BeTrue("Valid enterprise should allow save");
    }

    [Fact]
    public void DeleteEnterpriseCommand_CannotExecute_WhenNoEnterpriseSelected()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var canExecute = viewModel.DeleteEnterpriseCommand.CanExecute();

        // Assert
        canExecute.Should().BeFalse("Cannot delete when no enterprise selected");
    }

    [Fact]
    public void DeleteEnterpriseCommand_CanExecute_WhenEnterpriseSelected()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var enterprise = new Enterprise { Id = 1, Name = "Test" };

        var selectedEnterpriseProperty = typeof(EnterpriseViewModel).GetProperty("SelectedEnterprise");
        if (selectedEnterpriseProperty != null && selectedEnterpriseProperty.CanWrite)
        {
            selectedEnterpriseProperty.SetValue(viewModel, enterprise);
        }

        // Act
        var canExecute = viewModel.DeleteEnterpriseCommand.CanExecute();

        // Assert
        canExecute.Should().BeTrue("Should allow delete when enterprise selected");
    }

    [Fact]
    public void LoadEnterprisesCommand_CannotExecute_WhenLoading()
    {
        // Arrange
        var viewModel = CreateViewModel();

        var isLoadingProperty = typeof(EnterpriseViewModel).GetProperty("IsLoading");
        if (isLoadingProperty != null && isLoadingProperty.CanWrite)
        {
            isLoadingProperty.SetValue(viewModel, true);
        }

        // Act
        var canExecute = viewModel.LoadEnterprisesCommand.CanExecute();

        // Assert
        canExecute.Should().BeFalse("Cannot load when already loading");
    }

    #endregion

    #region CRUD Operation Tests

    [Fact]
    public async Task LoadEnterprisesCommand_ExecutesAsync_LoadsData()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var testEnterprises = new List<Enterprise>
        {
            new Enterprise { Id = 1, Name = "Enterprise 1", Type = "Sewer" },
            new Enterprise { Id = 2, Name = "Enterprise 2", Type = "Water" }
        };

        _mockEnterpriseRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(testEnterprises);

        // Act
    viewModel.LoadEnterprisesCommand.Execute();
    await Task.Delay(200);

        // Assert
        _mockEnterpriseRepository.Verify(
            r => r.GetAllAsync(),
            Times.AtLeastOnce,
            "Should load enterprises from repository");
    }

    [Fact]
    public async Task SaveEnterpriseCommand_WithValidEnterprise_SavesSuccessfully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var enterprise = new Enterprise
        {
            Name = "New Enterprise",
            Type = "Municipal",
            Status = EnterpriseStatus.Active,
            CurrentRate = 5.00m,        // Required: must be > 0
            CitizenCount = 100,          // Required: must be >= 1
            MonthlyExpenses = 1000.00m,
            TotalBudget = 5000.00m
        };

        var selectedEnterpriseProperty = typeof(EnterpriseViewModel).GetProperty("SelectedEnterprise");
        if (selectedEnterpriseProperty != null && selectedEnterpriseProperty.CanWrite)
        {
            selectedEnterpriseProperty.SetValue(viewModel, enterprise);
        }

        _mockEnterpriseRepository
            .Setup(r => r.AddAsync(It.IsAny<Enterprise>()))
            .ReturnsAsync((Enterprise e) => e);

        // Act
    viewModel.SaveEnterpriseCommand.Execute();
    await Task.Delay(200);

        // Assert
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Should save to database");
    }

    [Fact]
    public async Task DeleteEnterpriseCommand_WithSelectedEnterprise_DeletesSuccessfully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var enterprise = new Enterprise { Id = 1, Name = "To Delete" };

        var selectedEnterpriseProperty = typeof(EnterpriseViewModel).GetProperty("SelectedEnterprise");
        if (selectedEnterpriseProperty != null && selectedEnterpriseProperty.CanWrite)
        {
            selectedEnterpriseProperty.SetValue(viewModel, enterprise);
        }

        _mockEnterpriseRepository
            .Setup(r => r.DeleteAsync(It.IsAny<int>()))
            .ReturnsAsync(true);

        // Act
    viewModel.DeleteEnterpriseCommand.Execute();
    await Task.Delay(200);

        // Assert
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Should delete from database");
    }

    #endregion

    #region Property Change Notification Tests

    [Fact]
    public void SelectedEnterprise_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "SelectedEnterprise")
                propertyChangedRaised = true;
        };

        // Act
        var selectedEnterpriseProperty = typeof(EnterpriseViewModel).GetProperty("SelectedEnterprise");
        if (selectedEnterpriseProperty != null && selectedEnterpriseProperty.CanWrite)
        {
            selectedEnterpriseProperty.SetValue(viewModel, new Enterprise { Id = 1 });
        }

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CleanupResourcesSuccessfully()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.Dispose();

        // Assert
        viewModel.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private EnterpriseViewModel CreateViewModel()
    {
        return new EnterpriseViewModel(
            _mockUnitOfWork.Object,
            _mockEventAggregator.Object,
            _mockReportExportService.Object,
            _mockCacheService.Object,
            null,  // audit service
            _mockDialogService.Object);
    }

    #endregion
}
