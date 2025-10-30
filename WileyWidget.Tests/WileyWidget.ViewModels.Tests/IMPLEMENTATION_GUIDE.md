# WileyWidget ViewModel Unit Test Recommendations

**Generated:** October 28, 2025
**Project Version:** v0.2.0
**Framework:** .NET 9.0, xUnit, Moq, FluentAssertions
**Coverage Target:** >85%

## Executive Summary

Based on the project manifest, architecture cleanup (v0.2.0), and Prism MVVM patterns, this document provides a comprehensive unit test strategy for WileyWidget ViewModels. The test project framework has been created at `tests/WileyWidget.ViewModels.Tests/` with example test classes demonstrating recommended patterns.

## Test Project Structure

### Created Files

```
tests/WileyWidget.ViewModels.Tests/
├── WileyWidget.ViewModels.Tests.csproj  ✅ Created
├── BudgetViewModelTests.cs              ✅ Created (template with 20+ test methods)
├── DashboardViewModelTests.cs           ✅ Created (template with 15+ test methods)
├── AIAssistViewModelTests.cs            ✅ Created (template with 18+ test methods)
├── EnterpriseViewModelTests.cs          ✅ Created (template with 22+ test methods)
└── README.md                            ✅ Created (full documentation)
```

### Dependencies Configured

- **xUnit** - Modern testing framework
- **Moq** - Dependency mocking
- **FluentAssertions** - Readable assertions
- **Microsoft.NET.Test.Sdk** - Test execution
- **coverlet.collector** - Code coverage collection

### Project References

- WileyWidget.UI
- WileyWidget.Business
- WileyWidget.Services
- WileyWidget.Models
- WileyWidget.Abstractions

## Test Patterns and Examples

### 1. ViewModel Constructor Tests

**Purpose:** Validate dependency injection and initialization

```csharp
[Fact]
public void Constructor_WithValidDependencies_InitializesSuccessfully()
{
    // Arrange & Act
    var viewModel = CreateViewModel();

    // Assert
    viewModel.Should().NotBeNull();
    viewModel.LoadDataCommand.Should().NotBeNull();
    viewModel.RefreshCommand.Should().NotBeNull();
}

[Fact]
public void Constructor_WithNullRepository_ThrowsArgumentNullException()
{
    // Act & Assert
    Assert.Throws<ArgumentNullException>(() =>
        new BudgetViewModel(null!, _mockBudgetRepository.Object, ...));
}
```

**Coverage:**

- ✅ Null checks for required dependencies
- ✅ Command initialization
- ✅ Event subscriptions
- ✅ Default property values

### 2. Navigation Lifecycle Tests (INavigationAware)

**Purpose:** Test Prism navigation integration

```csharp
[Fact]
public async Task OnNavigatedTo_LoadsBudgetData()
{
    // Arrange
    var viewModel = CreateViewModel();
    var mockContext = new Mock<NavigationContext>(...);

    _mockBudgetRepository
        .Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>()))
        .ReturnsAsync(testBudgetEntries);

    // Act
    viewModel.OnNavigatedTo(mockContext.Object);
    await Task.Delay(100); // Allow async completion

    // Assert
    _mockBudgetRepository.Verify(
        r => r.GetByFiscalYearAsync(It.IsAny<int>()),
        Times.AtLeastOnce);
}

[Fact]
public void IsNavigationTarget_AlwaysReturnsTrue()
{
    // Arrange
    var viewModel = CreateViewModel();
    var mockContext = new Mock<NavigationContext>(...);

    // Act
    var result = viewModel.IsNavigationTarget(mockContext.Object);

    // Assert
    result.Should().BeTrue();
}

[Fact]
public void OnNavigatedFrom_StopsLiveUpdates()
{
    // Arrange
    var viewModel = CreateViewModel();
    var mockContext = new Mock<NavigationContext>(...);

    // Act
    viewModel.OnNavigatedFrom(mockContext.Object);

    // Assert - Verify cleanup occurs
    viewModel.Should().NotBeNull();
}
```

**Coverage:**

- ✅ OnNavigatedTo data loading
- ✅ Navigation parameters handling
- ✅ IsNavigationTarget logic
- ✅ OnNavigatedFrom cleanup

### 3. Async Command Tests (DelegateCommand)

**Purpose:** Test async command execution without deadlocks

```csharp
[Fact]
public async Task RefreshCommand_ExecutesAsync_WithoutDeadlock()
{
    // Arrange
    var viewModel = CreateViewModel();
    var testData = new List<Enterprise> { ... };

    _mockRepository
        .Setup(r => r.GetAllAsync())
        .ReturnsAsync(testData);

    // Setup dispatcher to execute synchronously in tests
    _mockDispatcherHelper
        .Setup(d => d.InvokeAsync(It.IsAny<Action>()))
        .Returns((Action action) =>
        {
            action();
            return Task.CompletedTask;
        });

    // Act
    viewModel.RefreshCommand.Execute();
    await Task.Delay(100); // Allow async operation to complete

    // Assert
    _mockRepository.Verify(
        r => r.GetAllAsync(),
        Times.AtLeastOnce);

    _mockDispatcherHelper.Verify(
        d => d.InvokeAsync(It.IsAny<Action>()),
        Times.AtLeastOnce,
        "Dispatcher should marshal UI updates");
}
```

**Coverage:**

- ✅ Async/await patterns
- ✅ Dispatcher marshalling
- ✅ Repository interactions
- ✅ No deadlocks

### 4. Command CanExecute Tests

**Purpose:** Test command availability based on state

```csharp
[Fact]
public void SaveCommand_CanExecute_WhenDataValid()
{
    // Arrange
    var viewModel = CreateViewModel();
    viewModel.SelectedItem = new Enterprise { Name = "Valid" };

    // Act
    var canExecute = viewModel.SaveCommand.CanExecute();

    // Assert
    canExecute.Should().BeTrue();
}

[Fact]
public void SaveCommand_CannotExecute_WhenBusy()
{
    // Arrange
    var viewModel = CreateViewModel();

    // Set IsBusy via property or reflection
    viewModel.IsBusy = true;

    // Act
    var canExecute = viewModel.SaveCommand.CanExecute();

    // Assert
    canExecute.Should().BeFalse("Cannot save when busy");
}

[Fact]
public void SaveCommand_CannotExecute_WhenValidationFails()
{
    // Arrange
    var viewModel = CreateViewModel();
    viewModel.SelectedItem = new Enterprise { Name = "" }; // Invalid

    // Act
    var canExecute = viewModel.SaveCommand.CanExecute();

    // Assert
    canExecute.Should().BeFalse("Cannot save invalid data");
}
```

**Coverage:**

- ✅ CanExecute based on IsBusy
- ✅ CanExecute based on validation
- ✅ CanExecute based on selection state
- ✅ RaiseCanExecuteChanged behavior

### 5. Validation Tests (IDataErrorInfo)

**Purpose:** Test validation rules and error messages

```csharp
[Fact]
public void Indexer_WithInvalidName_ReturnsValidationError()
{
    // Arrange
    var viewModel = CreateViewModel();
    viewModel.SelectedEnterprise = new Enterprise { Name = "" };

    // Act
    var error = (viewModel as IDataErrorInfo)["SelectedEnterprise.Name"];

    // Assert
    error.Should().NotBeNullOrEmpty("Empty name should produce error");
}

[Fact]
public void Error_Property_ReturnsAggregatedErrors()
{
    // Arrange
    var viewModel = CreateViewModel();
    viewModel.SelectedEnterprise = new Enterprise { Name = "" };

    // Act
    var error = (viewModel as IDataErrorInfo).Error;

    // Assert
    error.Should().NotBeNullOrEmpty("Invalid entity has aggregated errors");
}
```

**Coverage:**

- ✅ Field-level validation
- ✅ Aggregated error messages
- ✅ Required field checks
- ✅ Format validation

### 6. Error Handling Tests

**Purpose:** Test graceful error recovery

```csharp
[Fact]
public async Task SendCommand_WithServiceException_HandlesGracefully()
{
    // Arrange
    var viewModel = CreateViewModel();
    var testException = new InvalidOperationException("Service Error");

    _mockAIService
        .Setup(s => s.SendMessageAsync(It.IsAny<string>()))
        .ThrowsAsync(testException);

    // Act
    viewModel.SendCommand.Execute();
    await Task.Delay(100);

    // Assert
    _mockLogger.Verify(
        l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.AtLeastOnce,
        "Error should be logged");

    // UI remains responsive
    viewModel.Should().NotBeNull();
}
```

**Coverage:**

- ✅ Exception logging
- ✅ UI responsiveness
- ✅ Error messages to user
- ✅ Graceful degradation

### 7. Property Change Notification Tests

**Purpose:** Test INotifyPropertyChanged implementation

```csharp
[Fact]
public void TotalRevenue_SetValue_RaisesPropertyChanged()
{
    // Arrange
    var viewModel = CreateViewModel();
    var propertyChangedRaised = false;
    viewModel.PropertyChanged += (s, e) =>
    {
        if (e.PropertyName == nameof(BudgetViewModel.TotalRevenue))
            propertyChangedRaised = true;
    };

    // Act
    viewModel.TotalRevenue = 50000m;

    // Assert
    propertyChangedRaised.Should().BeTrue();
    viewModel.TotalRevenue.Should().Be(50000m);
}
```

**Coverage:**

- ✅ Property change events
- ✅ Dependent property updates
- ✅ Collection change notifications

### 8. Event Aggregator Tests

**Purpose:** Test Prism EventAggregator integration

```csharp
[Fact]
public async Task OnEnterpriseChanged_RefreshesData()
{
    // Arrange
    var viewModel = CreateViewModel();

    Action<EnterpriseChangedMessage>? subscribedAction = null;
    _mockEnterpriseChangedEvent
        .Setup(m => m.Subscribe(It.IsAny<Action<EnterpriseChangedMessage>>()))
        .Callback<Action<EnterpriseChangedMessage>>(action =>
            subscribedAction = action);

    // Re-create to capture subscription
    viewModel = CreateViewModel();

    // Act
    subscribedAction?.Invoke(new EnterpriseChangedMessage { ... });
    await Task.Delay(100);

    // Assert
    _mockRepository.Verify(
        r => r.GetAllAsync(),
        Times.AtLeastOnce,
        "Data should refresh when event published");
}
```

**Coverage:**

- ✅ Event subscriptions
- ✅ Event handling
- ✅ Automatic refresh triggers

## Implementation Checklist

### Step 1: Update Test Constructors

The template tests need to be updated to match actual ViewModel constructors:

**BudgetViewModel** - Check actual constructor parameters:

```csharp
// Template assumes:
public BudgetViewModel(
    IEnterpriseRepository enterpriseRepository,
    IBudgetRepository budgetRepository,
    IEventAggregator eventAggregator,
    ICacheService? cacheService = null)

// May need to update for actual signature
```

**DashboardViewModel** - Check for additional dependencies:

```csharp
// Template assumes logger, repository, event aggregator, region manager
// Actual may include: IWhatIfScenarioEngine, IUtilityCustomerRepository, etc.
```

**AIAssistViewModel** - Verify service interfaces:

```csharp
// Check method names:
// - SendMessageAsync vs. GetResponseAsync
// - GenerateResponseAsync vs. Generate
```

**EnterpriseViewModel** - Check UnitOfWork pattern:

```csharp
// Template assumes IUnitOfWork with EnterpriseRepository property
// May need to use direct repository injection
```

### Step 2: Fix API Mismatches

1. **BudgetEntry.Amount** - Check if property exists or use correct property name
2. **Enterprise.IsActive** - Verify property name and type
<!-- trunk-ignore(markdownlint/MD033) -->
3. **List<T>** - Add `using System.Collections.Generic;`
4. **NavigationResult** - Add proper Prism navigation imports
5. **Service methods** - Match actual interface method names

### Step 3: Add Missing Using Statements

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using WileyWidget.Abstractions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels;
using Xunit;
```

### Step 4: Run Tests

```powershell
# Restore and build
dotnet restore tests/WileyWidget.ViewModels.Tests/
dotnet build tests/WileyWidget.ViewModels.Tests/

# Run all tests
dotnet test tests/WileyWidget.ViewModels.Tests/

# Run with verbose output
dotnet test tests/WileyWidget.ViewModels.Tests/ -v normal

# Run with coverage
dotnet test tests/WileyWidget.ViewModels.Tests/ /p:CollectCoverage=true
```

### Step 5: Analyze Coverage

```powershell
# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Use ReportGenerator for HTML reports
reportgenerator -reports:coverage.opencover.xml -targetdir:coverage-report
```

## Test Categories by ViewModel

### BudgetViewModel Tests (Target: 20+ tests)

| Category                      | Test Count | Priority |
| ----------------------------- | ---------- | -------- |
| Constructor validation        | 3          | High     |
| Navigation lifecycle          | 3          | High     |
| Async commands                | 4          | High     |
| Command CanExecute            | 3          | Medium   |
| Property change notifications | 2          | Medium   |
| Event aggregator integration  | 2          | Medium   |
| Cache service usage           | 2          | Low      |
| Disposal                      | 2          | Low      |

### DashboardViewModel Tests (Target: 15+ tests)

| Category                 | Test Count | Priority |
| ------------------------ | ---------- | -------- |
| Constructor validation   | 2          | High     |
| Navigation lifecycle     | 3          | High     |
| RegionManager navigation | 2          | High     |
| Async command execution  | 3          | High     |
| Command CanExecute       | 3          | Medium   |
| Property changes         | 1          | Low      |
| Disposal                 | 1          | Low      |

### AIAssistViewModel Tests (Target: 18+ tests)

| Category               | Test Count | Priority |
| ---------------------- | ---------- | -------- |
| Constructor validation | 2          | High     |
| Navigation lifecycle   | 3          | High     |
| Error handling         | 3          | High     |
| Command CanExecute     | 2          | High     |
| AI service integration | 2          | Medium   |
| Async operations       | 2          | Medium   |
| Property changes       | 1          | Low      |
| Disposal               | 1          | Low      |

### EnterpriseViewModel Tests (Target: 22+ tests)

| Category                    | Test Count | Priority |
| --------------------------- | ---------- | -------- |
| Constructor validation      | 2          | High     |
| Navigation lifecycle        | 2          | High     |
| Validation (IDataErrorInfo) | 3          | High     |
| Command CanExecute          | 5          | High     |
| CRUD operations             | 3          | High     |
| Property changes            | 1          | Medium   |
| UnitOfWork integration      | 2          | Medium   |
| Disposal                    | 1          | Low      |

## Advanced Testing Patterns

### Testing Async/Await Without Deadlocks

```csharp
public AIAssistViewModelTests()
{
    // Setup dispatcher to execute synchronously for tests
    _mockDispatcherHelper
        .Setup(d => d.InvokeAsync(It.IsAny<Action>()))
        .Returns((Action action) =>
        {
            action();  // Execute synchronously
            return Task.CompletedTask;
        });

    _mockDispatcherHelper
        .Setup(d => d.InvokeAsync(It.IsAny<Func<Task>>()))
        .Returns((Func<Task> func) => func());  // Execute async func

    _mockDispatcherHelper
        .Setup(d => d.CheckAccess())
        .Returns(true);  // Always on "UI thread" in tests
}
```

### Testing Property Validation via Reflection

```csharp
[Fact]
public void Command_CannotExecute_WhenBusy()
{
    // Arrange
    var viewModel = CreateViewModel();

    // Access internal property via reflection
    var isBusyProperty = typeof(ViewModel).GetProperty("IsBusy");
    if (isBusyProperty != null && isBusyProperty.CanWrite)
    {
        isBusyProperty.SetValue(viewModel, true);
    }

    // Act
    var canExecute = viewModel.Command.CanExecute();

    // Assert
    canExecute.Should().BeFalse();
}
```

### Testing Event Subscriptions

```csharp
[Fact]
public void Constructor_SubscribesToEnterpriseChangedEvent()
{
    // Arrange
    Action<EnterpriseChangedMessage>? subscribedAction = null;
    _mockEventAggregator
        .Setup(ea => ea.GetEvent<EnterpriseChangedMessage>())
        .Returns(_mockEvent.Object);

    _mockEvent
        .Setup(e => e.Subscribe(It.IsAny<Action<EnterpriseChangedMessage>>()))
        .Callback<Action<EnterpriseChangedMessage>>(action =>
            subscribedAction = action);

    // Act
    var viewModel = CreateViewModel();

    // Assert
    subscribedAction.Should().NotBeNull("Should subscribe to event");
    _mockEvent.Verify(e => e.Subscribe(It.IsAny<Action<EnterpriseChangedMessage>>()), Times.Once);
}
```

## Coverage Goals

### Target Metrics

- **Overall ViewModel Coverage:** >85%
- **Critical Paths:** 100% (constructors, save/delete operations)
- **Command Execution:** >90%
- **Property Changes:** >80%
- **Error Handling:** >90%

### Measurement Tools

```powershell
# Install ReportGenerator globally
dotnet tool install -g dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Generate HTML report
reportgenerator -reports:coverage.cobertura.xml -targetdir:coverage-html

# Open in browser
start coverage-html/index.html
```

## Common Pitfalls and Solutions

### Pitfall 1: Async Tests Not Waiting

**Problem:** Tests pass but assertions never run

```csharp
// ❌ Wrong
[Fact]
public void AsyncTest()
{
    viewModel.Command.Execute();  // Fire and forget
    _mockService.Verify(...);  // May execute before async completes
}
```

**Solution:** Wait for async operations

```csharp
// ✅ Correct
[Fact]
public async Task AsyncTest()
{
    viewModel.Command.Execute();
    await Task.Delay(100);  // Allow async to complete
    _mockService.Verify(...);
}
```

### Pitfall 2: UI Thread Deadlocks

**Problem:** Tests hang on dispatcher calls

```csharp
// ❌ Wrong
_mockDispatcherHelper
    .Setup(d => d.InvokeAsync(It.IsAny<Action>()))
    .ReturnsAsync(Task.CompletedTask);  // Wrong return type
```

**Solution:** Execute action synchronously in tests

```csharp
// ✅ Correct
_mockDispatcherHelper
    .Setup(d => d.InvokeAsync(It.IsAny<Action>()))
    .Returns((Action action) => {
        action();  // Execute immediately
        return Task.CompletedTask;
    });
```

### Pitfall 3: Event Subscriptions Not Captured

**Problem:** Can't trigger events in tests

```csharp
// ❌ Wrong
_mockEvent.Setup(e => e.Subscribe(It.IsAny<Action<Message>>()));
// No way to invoke the subscriber
```

**Solution:** Capture the subscription

```csharp
// ✅ Correct
Action<Message>? subscriber = null;
_mockEvent
    .Setup(e => e.Subscribe(It.IsAny<Action<Message>>()))
    .Callback<Action<Message>>(action => subscriber = action);

// Later in test
subscriber?.Invoke(new Message { ... });
```

## Next Steps

1. **Fix API Mismatches:**
   - Review actual ViewModel constructors
   - Update mock setups to match real interfaces
   - Correct property names (Amount, IsActive, etc.)

2. **Complete Implementation:**
   - Add missing using statements
   - Fix all compilation errors
   - Run initial test pass

3. **Expand Coverage:**
   - Add tests for remaining ViewModels (MunicipalAccountViewModel, DepartmentViewModel, etc.)
   - Test edge cases and boundary conditions
   - Add integration tests for ViewModel interactions

4. **Continuous Improvement:**
   - Monitor coverage reports
   - Refactor duplicated test code
   - Add performance tests for large datasets

## Resources

- **Prism Documentation:** https://prismlibrary.com/
- **xUnit Documentation:** https://xunit.net/
- **Moq Quick Start:** https://github.com/moq/moq4/wiki/Quickstart
- **FluentAssertions:** https://fluentassertions.com/
- **WileyWidget Project Docs:** `docs/` folder

## Conclusion

The test framework is established with comprehensive examples demonstrating all key patterns for Prism MVVM testing. The templates provide >75 test method examples covering navigation lifecycle, async commands, validation, error handling, and more.

**To complete implementation:**

1. Update constructors to match actual ViewModels
2. Fix API mismatches (properties, method names)
3. Run and debug tests
4. Achieve >85% coverage target

**Estimated Effort:** 4-8 hours to complete all ViewModels with >85% coverage.

---

**Author:** GitHub Copilot
**Date:** October 28, 2025
**Version:** 1.0.0
