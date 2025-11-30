# WileyWidget.Services.Tests

## Overview

This test project contains **meaningful, high-value tests** for the Wiley Widget application, focusing on:

- **ViewModels**: Testing MVVM patterns, property notifications, and command execution
- **Services**: Testing QuickBooks API integration, error handling, and token management
- **Integration**: Testing cross-component interactions (planned)

## Philosophy: Value Over Volume

For a solo developer project, we prioritize:

1. **Tests that catch real bugs** in critical areas (ViewModels, API integrations)
2. **Low maintenance overhead** - avoid brittle tests
3. **Fast execution** - all tests should run in under 30 seconds
4. **Clear failures** - when a test fails, it's obvious what broke

**Target Coverage**: 20-30 meaningful tests (not 100% coverage)

## Project Structure

```
WileyWidget.Services.Tests/
├── ViewModelTests/
│   └── DashboardViewModelTests.cs    # MVVM property change tests
├── ServiceTests/
│   └── QuickBooksServiceTests.cs     # API integration with mocks
├── IntegrationTests/                 # Future: cross-component tests
└── README.md                         # This file
```

## Running Tests

### Local Execution

```bash
# From solution root
dotnet test

# With detailed output
dotnet test --logger "console;verbosity=detailed"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Visual Studio Test Explorer

1. Open Test Explorer: `Test > Test Explorer`
2. Click "Run All" or right-click individual tests
3. View live results and coverage

### Local CI/CD Integration

Tests run locally via Trunk CLI and can be executed manually:

```powershell
# Run all tests
dotnet test --configuration Release --collect:"XPlat Code Coverage"

# Or use Trunk for comprehensive validation
trunk check --ci
```

## Test Categories

### 1. ViewModel Tests (DashboardViewModelTests.cs)

**Focus**: Data binding and INotifyPropertyChanged behavior

| Test                                                      | Purpose                      | Why It Matters                     |
| --------------------------------------------------------- | ---------------------------- | ---------------------------------- |
| `Constructor_InitializesWelcomeMessage`                   | Verify default state         | Catches initialization bugs        |
| `WelcomeMessage_SetProperty_RaisesPropertyChanged`        | Test MVVM binding            | Syncfusion controls depend on this |
| `WelcomeMessage_SetSameValue_DoesNotRaisePropertyChanged` | Avoid unnecessary UI updates | Performance optimization           |
| `WelcomeMessage_SetVariousValues_UpdatesCorrectly`        | Edge case handling           | Prevents binding errors            |

**Real-World Value**: These tests prevent UI binding issues with Syncfusion controls that would otherwise require manual testing in the app.

### 2. Service Tests (QuickBooksServiceTests.cs)

**Focus**: OAuth2 flows, token management, and API error handling

| Test                                                           | Purpose                     | Why It Matters                              |
| -------------------------------------------------------------- | --------------------------- | ------------------------------------------- |
| `GetAuthorizationUrlAsync_ReturnsValidUrl`                     | Verify OAuth URL generation | Catches config errors before user sees them |
| `ExchangeCodeForTokenAsync_WithValidCode_ReturnsTrue`          | Test token exchange         | Critical for QuickBooks integration         |
| `ExchangeCodeForTokenAsync_WithInvalidCode_ReturnsFalse`       | Error handling              | Prevents crashes on auth failures           |
| `RefreshAccessTokenAsync_WithValidRefreshToken_ReturnsTrue`    | Token refresh logic         | Keeps API connection alive                  |
| `RefreshAccessTokenAsync_WithExpiredRefreshToken_ReturnsFalse` | Graceful degradation        | User gets clear error message               |

**Mocking Strategy**:

- Uses `Moq` to mock HTTP responses (no real API calls)
- Uses `Mock<ISecretVaultService>` to simulate token storage
- Fast, isolated, repeatable tests

**Real-World Value**: These tests catch OAuth flow bugs that are painful to debug manually (e.g., token expiry handling).

## Adding New Tests

### Best Practices

1. **Name tests clearly**: `MethodName_Scenario_ExpectedBehavior`
2. **Use Arrange-Act-Assert pattern**:

   ```csharp
   [Fact]
   public void MyTest()
   {
       // Arrange: Setup test data and mocks
       var service = CreateService();

       // Act: Execute the method under test
       var result = service.DoSomething();

       // Assert: Verify expected outcome
       result.Should().BeTrue();
   }
   ```

3. **Use FluentAssertions** for readable assertions:
   ```csharp
   result.Should().NotBeNull();
   result.Should().BeEquivalentTo(expected);
   exception.Should().BeOfType<InvalidOperationException>();
   ```
4. **Mock external dependencies**:
   ```csharp
   var mockLogger = new Mock<ILogger<MyService>>();
   var mockHttp = new Mock<HttpMessageHandler>();
   ```

### Example: Adding a New ViewModel Test

```csharp
[Fact]
public void LoadDataCommand_ExecutesSuccessfully()
{
    // Arrange
    var mockEventAggregator = new Mock<IEventAggregator>();
    var vm = new DashboardViewModel(mockEventAggregator.Object);

    // Act
    vm.LoadDataCommand.Execute(null);

    // Assert
    vm.DataCollection.Should().NotBeEmpty();
}
```

### Example: Adding a New Service Test

```csharp
[Fact]
public async Task GetInvoicesAsync_WithValidToken_ReturnsInvoices()
{
    // Arrange
    var service = CreateService();
    SetupHttpResponse(HttpStatusCode.OK,
        JsonSerializer.Serialize(new[] { new Invoice { Id = "123" } }));

    // Act
    var invoices = await service.GetInvoicesAsync();

    // Assert
    invoices.Should().HaveCount(1);
    invoices[0].Id.Should().Be("123");
}
```

## Test Data and Mocking

### QuickBooks Sandbox

For **integration tests** (not yet implemented), use Intuit's sandbox:

```csharp
// Setup sandbox credentials
var service = new QuickBooksService(
    clientId: "sandbox-client-id",
    environment: "sandbox");

// Test against real sandbox API
var customers = await service.GetCustomersAsync();
```

**Note**: Current tests use mocks to avoid sandbox dependency.

### Mock HTTP Responses

```csharp
private void SetupHttpResponse(HttpStatusCode statusCode, string content)
{
    _mockHttpHandler
        .Protected()
        .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(content)
        });
}
```

## Coverage Expectations

**Not aiming for 100% coverage** - focus on:

- ✅ **80% coverage on ViewModels** (business logic)
- ✅ **70% coverage on Services** (API integration)
- ❌ Skip: Auto-generated code, trivial properties, UI event handlers

### Viewing Coverage

```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# View coverage in Visual Studio
# Tools > Options > Text Editor > All Languages > CodeLens
# Enable "Show Code Coverage" in CodeLens
```

## Troubleshooting

### Tests Not Appearing in Test Explorer

1. Rebuild solution: `Ctrl+Shift+B`
2. Clean test cache: `Test > Configure Run Settings > Clear Test Results`
3. Check NuGet packages are restored: `dotnet restore`

### Tests Failing Locally but Passing in CI

- Check for hardcoded paths (use `Path.Combine` with `Path.GetTempPath()`)
- Verify environment variables are set correctly
- Check for timezone-dependent tests (use `DateTimeOffset.UtcNow`)

### Mock Setup Errors

```
Moq.MockException: Expected invocation on the mock at least once, but was never performed
```

**Fix**: Verify your test actually calls the mocked method:

```csharp
// Incorrect - mock not called
_mockService.Setup(s => s.DoSomething()).ReturnsAsync(true);
// Missing: await service.DoSomething();

// Correct
_mockService.Setup(s => s.DoSomething()).ReturnsAsync(true);
var result = await service.DoSomething(); // Now mock is called
Assert.True(result);
```

## CI/CD Integration

Tests are part of the **CI/CD Feedback Loop** documented in `copilot-instructions.md`:

```yaml
# ci-optimized.yml snippet
- name: Build & Test Matrix
  run: |
    dotnet build --no-restore --configuration Release
    dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"
```

**Test Failure Policy**:

- ❌ Any test failure = CI fails
- ⚠️ Warnings logged but don't block merge
- ✅ 100% success rate target across all CI runs

## Future Enhancements

Planned test categories (not yet implemented):

- [ ] **Integration Tests**: Real sandbox API calls with rate limiting
- [ ] **UI Tests**: Uno.UITest for WinUI navigation flows
- [ ] **Performance Tests**: Measure ViewModel initialization time
- [ ] **Database Tests**: In-memory SQLite for Data layer

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Quick Start](https://github.com/moq/moq4/wiki/Quickstart)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [QuickBooks API Best Practices](https://developer.intuit.com/app/developer/qbo/docs/best-practices)
- [Microsoft Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

## Contact

For questions about testing strategy, see `docs/PHASE-4-TESTING-ROADMAP.md` or open a discussion in GitHub.
