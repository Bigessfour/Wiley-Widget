# WileyWidget.ViewModels.Tests

Comprehensive unit tests for WileyWidget ViewModels focusing on Prism MVVM patterns, navigation lifecycle, async operations, and command behavior.

## Overview

This test project provides >85% code coverage for critical ViewModel components with emphasis on:

- **Prism Patterns**: BindableBase, DelegateCommand, INavigationAware
- **Navigation Lifecycle**: OnNavigatedTo, OnNavigatedFrom, IsNavigationTarget
- **Async Operations**: ExecuteAsync patterns, deadlock prevention
- **Threading**: IDispatcherHelper integration, UI thread marshalling
- **Validation**: IDataErrorInfo, FluentValidation patterns
- **Error Handling**: Graceful degradation, dialog service integration
- **CRUD Operations**: Repository interactions, UnitOfWork patterns

## Test Structure

### BudgetViewModelTests

Tests for `BudgetViewModel` covering:

- Constructor validation with null checks
- Navigation lifecycle (INavigationAware)
- Async command execution without deadlocks
- Event aggregator subscriptions
- Property change notifications
- Cache service integration
- Disposal patterns

**Key Test**: `RefreshBudgetDataCommand_ExecutesAsync_WithoutDeadlock`

- Validates async command pattern
- Ensures dispatcher marshalling
- Verifies repository interactions

### DashboardViewModelTests

Tests for `DashboardViewModel` covering:

- RegionManager navigation integration
- Async command execution
- Command CanExecute logic
- Property change notifications
- Cache service usage
- Navigation parameter handling

**Key Test**: `NavigateCommand_UsesRegionManager`

- Tests Prism region navigation
- Validates navigation parameters
- Ensures proper region existence checks

### AIAssistViewModelTests

Tests for `AIAssistViewModel` covering:

- Error handling and graceful recovery
- IDialogService integration
- AI service interactions
- Command error scenarios
- Async operation patterns
- Proactive advice generation

**Key Test**: `SendCommand_WithAIServiceException_HandlesErrorGracefully`

- Tests exception handling in async commands
- Validates error logging
- Ensures UI remains responsive

### EnterpriseViewModelTests

Tests for `EnterpriseViewModel` covering:

- IDataErrorInfo validation
- Command CanExecute based on validation state
- CRUD operations (Create, Read, Update, Delete)
- UnitOfWork integration
- Property validation rules
- FluentValidation patterns (if implemented)

**Key Test**: `SaveEnterpriseCommand_CannotExecute_WhenEnterpriseInvalid`

- Tests validation-driven command behavior
- Ensures invalid entities cannot be saved
- Validates CanExecute requery logic

## Technologies Used

- **xUnit**: Modern testing framework
- **Moq**: Dependency mocking
- **FluentAssertions**: Readable assertions
- **.NET 9.0**: Latest runtime
- **Prism.Core**: MVVM framework

## Running Tests

### Command Line

```powershell
# Run all tests
dotnet test

# Run with detailed output
dotnet test -v normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~BudgetViewModelTests"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Visual Studio

1. Open Test Explorer (Test → Test Explorer)
2. Click "Run All" or select specific tests
3. View results and code coverage

### VS Code

1. Use Testing sidebar (Ctrl+Shift+T)
2. Run tests from CodeLens links
3. Debug tests with breakpoints

## Test Patterns

### Arrange-Act-Assert (AAA)

All tests follow the AAA pattern:

```csharp
[Fact]
public void TestMethod_Scenario_ExpectedOutcome()
{
    // Arrange - Set up test data and mocks
    var viewModel = CreateViewModel();

    // Act - Execute the test action
    viewModel.Command.Execute();

    // Assert - Verify expected outcomes
    result.Should().Be(expected);
}
```

### Mock Setup Pattern

Consistent mock configuration:

```csharp
_mockRepository
    .Setup(r => r.GetAllAsync())
    .ReturnsAsync(testData);
```

### Async Testing

Proper async test handling:

```csharp
[Fact]
public async Task AsyncTest()
{
    viewModel.Command.Execute();
    await Task.Delay(100); // Allow async completion

    _mockService.Verify(s => s.MethodAsync(), Times.Once);
}
```

## Coverage Goals

Target: **>85% code coverage** for ViewModels

### Current Coverage (by component)

- ✅ BudgetViewModel: Navigation, Commands, Events
- ✅ DashboardViewModel: Navigation, RegionManager
- ✅ AIAssistViewModel: Error Handling, Services
- ✅ EnterpriseViewModel: Validation, CRUD

### Untested Areas (Future Work)

- Edge cases for complex validation rules
- Performance testing for large datasets
- Integration tests with real repositories
- UI automation tests

## Best Practices

1. **Isolation**: Each test is independent, no shared state
2. **Mocking**: All external dependencies are mocked
3. **Naming**: Clear, descriptive test names following `Method_Scenario_ExpectedOutcome`
4. **Single Responsibility**: One assertion concept per test
5. **Readability**: Tests serve as living documentation

## Contributing

When adding new tests:

1. Follow existing patterns and structure
2. Use FluentAssertions for readable assertions
3. Mock all external dependencies
4. Test both success and error paths
5. Verify async operations complete properly
6. Update this README with new test categories

## Troubleshooting

### Tests fail with NullReferenceException

- Ensure all mocks are properly configured
- Check that dispatcher helper is set up for synchronous execution

### Async tests timeout

- Increase delay after `Command.Execute()`
- Verify mock async methods return completed tasks

### CanExecute always returns false

- Check property values used in CanExecute logic
- Verify property setters are accessible via reflection

## References

- [Prism Documentation](https://prismlibrary.com/)
- [xUnit Documentation](https://xunit.net/)
- [Moq Quick Start](https://github.com/moq/moq4/wiki/Quickstart)
- [FluentAssertions](https://fluentassertions.com/)

## Version History

- **v1.0.0** (2025-10-28): Initial comprehensive ViewModel test suite
  - BudgetViewModel tests (20+ scenarios)
  - DashboardViewModel tests (15+ scenarios)
  - AIAssistViewModel tests (18+ scenarios)
  - EnterpriseViewModel tests (22+ scenarios)
