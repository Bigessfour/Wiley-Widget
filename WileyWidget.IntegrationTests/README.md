# WileyWidget Integration Tests

## Overview

This project contains integration tests for the Wiley Widget application using a real dependency injection container with test doubles for expensive external services.

## Architecture

### IntegrationTestBase

The `IntegrationTestBase` class provides:

- **Full Service Container**: Mirrors production DI configuration from `DependencyInjection.cs`
- **In-Memory Database**: Uses EF Core InMemory provider for isolated test data
- **Test Doubles**: Replaces expensive services (AI, telemetry) with null implementations
- **Automatic Cleanup**: Implements IDisposable for proper resource management

### Test Doubles

Located in `TestDoubles/` directory:

- **NullAIService**: Stub implementation of `IAIService` that returns dev messages
- **NullGrokSupercomputer**: Stub implementation of `IGrokSupercomputer` with empty data
- **NullTelemetryService**: No-op implementation of `ITelemetryService`

## Usage

### Creating Integration Tests

```csharp
public class MyServiceIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task MyTest()
    {
        // Arrange - get services from DI container
        var myService = GetService<IMyService>();
        var dbContext = GetDbContext();
        
        // Act
        var result = await myService.DoSomethingAsync();
        
        // Assert
        Assert.NotNull(result);
    }
}
```

### Service Resolution

```csharp
// Required service (throws if not registered)
var service = GetService<IMyService>();

// Optional service (returns null if not registered)
var optionalService = GetOptionalService<IOptionalService>();

// Database context
var dbContext = GetDbContext();
```

### Database Setup

Each test gets a fresh in-memory database:

```csharp
[Fact]
public async Task DatabaseTest()
{
    // Arrange
    var dbContext = GetDbContext();
    
    // Seed test data
    dbContext.Enterprises.Add(new Enterprise { Name = "Test" });
    await dbContext.SaveChangesAsync();
    
    // Act & Assert
    var count = await dbContext.Enterprises.CountAsync();
    Assert.Equal(1, count);
}
```

## Configuration

Tests use `appsettings.test.json` for configuration:

```json
{
  "XAI": {
    "ApiKey": "test-key",
    "BaseUrl": "https://api.test.com",
    "Model": "test-model"
  },
  "ConnectionStrings": {
    "DefaultConnection": "InMemory"
  }
}
```

## Benefits

✅ **Real DI Container**: Tests actual service wiring, not mocked containers  
✅ **Fast**: In-memory database, no external dependencies  
✅ **Isolated**: Each test gets fresh database instance  
✅ **Non-Whitewash**: Uses real service implementations (except expensive external calls)  
✅ **Maintainable**: Follows same patterns as production code  

## Anti-Patterns to Avoid

❌ **Don't** create manual service instances outside the container  
❌ **Don't** mock the entire service container  
❌ **Don't** share database state between tests  
❌ **Don't** make real API calls to external services  

## Running Tests

```bash
# Run all integration tests
dotnet test WileyWidget.IntegrationTests

# Run specific test class
dotnet test --filter FullyQualifiedName~AIServiceIntegrationTests

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

## Coverage Requirements

- ✅ 3+ test cases per service (happy path, error path, edge cases)
- ✅ Mock expensive dependencies (AI, external APIs)
- ✅ Verify service registration and resolution
- ✅ Test database interactions with real EF Core
- ✅ Validate configuration binding

## See Also

- [Testing Strategy](../docs/testing/PHASE-4-TESTING-ROADMAP.md)
- [Docker-Based Testing](../docs/testing/docker-based-testing.md)
- [Production DI Setup](../src/WileyWidget.WinUI/Configuration/DependencyInjection.cs)
