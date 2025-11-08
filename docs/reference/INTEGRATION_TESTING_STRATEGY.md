# Integration Testing Strategy - Wiley Widget

## Overview

The Wiley Widget integration testing infrastructure provides comprehensive testing capabilities for database operations, focusing on SQL Server integration via TestContainers, concurrency handling, relationship integrity, and performance benchmarking.

## Table of Contents

1. [Architecture](#architecture)
2. [Test Project Structure](#test-project-structure)
3. [TestContainers Integration](#testcontainers-integration)
4. [Test Categories](#test-categories)
5. [Running Tests](#running-tests)
6. [Performance Benchmarking](#performance-benchmarking)
7. [Best Practices](#best-practices)

---

## Architecture

### Multi-Database Testing Strategy

The integration tests support two database backends:

1. **SQL Server (via TestContainers)** - Primary integration testing target
   - Real SQL Server instance running in Docker
   - Tests SQL Server-specific features (row versioning, transactions)
   - Ensures production-like behavior

2. **SQLite (In-Memory)** - Fast unit/integration testing
   - Lightweight for quick feedback loops
   - Useful for relationship and basic query testing
   - Performance comparison baseline

### Key Design Principles

- **Isolation**: Each test gets a clean database state
- **Real Dependencies**: Use actual SQL Server via TestContainers
- **Performance Aware**: Benchmark tests compare SQLite vs SQL Server
- **EF Core Native**: Test EF Core features like concurrency, change tracking, relationships

---

## Test Project Structure

```
WileyWidget.IntegrationTests/
├── WileyWidget.IntegrationTests.csproj
├── Infrastructure/
│   ├── SqlServerTestBase.cs          # Base class for SQL Server tests
│   ├── SqliteTestBase.cs             # Base class for SQLite tests
│   └── TestDataBuilder.cs            # Helper for creating test entities
├── Concurrency/
│   └── ConcurrencyConflictTests.cs   # Row versioning & optimistic concurrency tests
├── Relationships/
│   └── ForeignKeyIntegrityTests.cs   # Foreign key, cascade, navigation tests
└── Performance/
    ├── DatabasePerformanceTests.cs    # xUnit performance comparison tests
    └── DatabasePerformanceBenchmarks.cs # BenchmarkDotNet tests
```

---

## TestContainers Integration

### What is TestContainers?

[TestContainers](https://dotnet.testcontainers.org/) is a .NET library that provides throwaway instances of databases in Docker containers. It ensures tests run against real database engines.

### SQL Server TestContainer Configuration

```csharp
_sqlContainer = new MsSqlBuilder()
    .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
    .WithPassword("YourStrong!Passw0rd")
    .WithCleanUp(true)
    .Build();

await _sqlContainer.StartAsync();
ConnectionString = _sqlContainer.GetConnectionString();
```

### Benefits

- ✅ **Real SQL Server**: Tests run against actual SQL Server 2022
- ✅ **Isolated**: Each test class gets its own container
- ✅ **Automatic Cleanup**: Containers are disposed after tests
- ✅ **CI/CD Ready**: Works in GitHub Actions with Docker

### Prerequisites

- **Docker Desktop** must be running
- **Docker Engine** accessible to test runner
- **Sufficient memory** allocated to Docker (4GB+ recommended)

### Base Test Class Usage

Inherit from `SqlServerTestBase` for tests requiring SQL Server:

```csharp
public class MyConcurrencyTests : SqlServerTestBase
{
    [Fact]
    public async Task TestConcurrency()
    {
        // CreateDbContext() provided by base class
        using var context = CreateDbContext();

        // Test logic here
    }
}
```

The base class handles:

- Container startup (`InitializeAsync`)
- Container cleanup (`DisposeAsync`)
- Database creation (`CreateDbContext`)
- Database reset (`ResetDatabaseAsync`)

---

## Test Categories

### 1. Concurrency Conflict Tests

**Location**: `Concurrency/ConcurrencyConflictTests.cs`

**Purpose**: Test EF Core's optimistic concurrency control using row versioning.

**Key Scenarios**:

- ✅ Successful updates with matching row version
- ✅ Concurrent update detection (throws `DbUpdateConcurrencyException`)
- ✅ Reload and retry after conflict
- ✅ Client-wins conflict resolution strategy
- ✅ Server-wins conflict resolution strategy
- ✅ Merge strategy for non-conflicting properties
- ✅ Delete with concurrent update detection

**Example Test**:

```csharp
[Fact]
public async Task UpdateAccount_WhenConcurrentUpdate_ShouldThrowDbUpdateConcurrencyException()
{
    // Load same entity in two contexts
    var account2 = await context2.MunicipalAccounts.FindAsync(accountId);
    var account3 = await context3.MunicipalAccounts.FindAsync(accountId);

    // First update succeeds
    account2.Balance = 2000m;
    await context2.SaveChangesAsync();

    // Second update should fail due to stale row version
    account3.Balance = 3000m;
    await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
}
```

**When to Run**:

- After modifying entities with `[Timestamp]` or concurrency tokens
- Before deploying changes to production
- As part of CI/CD pipeline

---

### 2. Foreign Key & Relationship Tests

**Location**: `Relationships/ForeignKeyIntegrityTests.cs`

**Purpose**: Verify database relationship integrity, foreign key constraints, and navigation properties.

**Key Scenarios**:

- ✅ Cascade delete behavior
- ✅ Foreign key constraint violations
- ✅ Navigation property loading (Include/ThenInclude)
- ✅ Bidirectional navigation
- ✅ Bulk delete with relationships
- ✅ Nullable foreign keys (optional relationships)

**Example Test**:

```csharp
[Fact]
public async Task DeleteAccount_WithTransactions_ShouldCascadeDelete()
{
    // Create account with transactions
    var account = CreateMunicipalAccount("Test", "001");
    var transaction1 = CreateTransaction(account.Id, 100m);

    // Delete parent
    context.Remove(account);
    await context.SaveChangesAsync();

    // Child transactions should be cascade deleted
    var deletedTx = await context.Transactions.FindAsync(transaction1.Id);
    deletedTx.Should().BeNull();
}
```

**When to Run**:

- After modifying entity relationships or navigation properties
- When changing cascade delete behavior
- Before database migrations

---

### 3. Performance Benchmarking

**Location**: `Performance/DatabasePerformanceTests.cs` and `DatabasePerformanceBenchmarks.cs`

**Purpose**: Compare performance between SQLite and SQL Server for common operations.

#### A. xUnit Performance Tests

**File**: `DatabasePerformanceTests.cs`

Simple performance comparison tests using xUnit and `Stopwatch`.

**Operations Tested**:

- Single record insert
- Bulk insert (100 records)
- Simple query with filter
- Update operations
- Complex queries with joins

**Example**:

```csharp
[Fact]
public async Task CompareInsertPerformance_BulkRecords()
{
    var sqliteTime = await MeasureSqliteInsert(100);
    var sqlServerTime = await MeasureSqlServerInsert(100);

    _output.WriteLine($"SQLite: {sqliteTime}ms");
    _output.WriteLine($"SQL Server: {sqlServerTime}ms");
    _output.WriteLine($"Ratio: {(double)sqliteTime / sqlServerTime:F2}x");
}
```

**Output**: Results displayed in test output window.

#### B. BenchmarkDotNet Tests

**File**: `DatabasePerformanceBenchmarks.cs`

Professional benchmarking using BenchmarkDotNet with:

- Memory diagnostics
- Statistical analysis (min, max, mean, median)
- Warmup and multiple iterations
- Detailed HTML reports

**Example**:

```csharp
[Benchmark]
public async Task InsertSingleAccount_SQLite() { ... }

[Benchmark]
public async Task InsertSingleAccount_SQLServer() { ... }
```

**Running**:

```powershell
dotnet run -c Release --project WileyWidget.IntegrationTests
```

**Output**: HTML report in `BenchmarkDotNet.Artifacts/` directory.

---

## Running Tests

### Prerequisites

1. **Docker Desktop** running (for SQL Server TestContainers)
2. **.NET 9.0 SDK** installed
3. **xUnit** test runner

### Run All Integration Tests

```powershell
dotnet test WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj
```

### Run Specific Test Category

```powershell
# Concurrency tests only
dotnet test WileyWidget.IntegrationTests --filter "FullyQualifiedName~Concurrency"

# Relationship tests only
dotnet test WileyWidget.IntegrationTests --filter "FullyQualifiedName~Relationships"

# Performance tests only
dotnet test WileyWidget.IntegrationTests --filter "FullyQualifiedName~Performance"
```

### Run with Coverage

```powershell
dotnet test WileyWidget.IntegrationTests `
    --collect:"XPlat Code Coverage" `
    --results-directory:TestResults/IntegrationCoverage
```

### Run BenchmarkDotNet Tests

```powershell
cd WileyWidget.IntegrationTests
dotnet run -c Release
```

### VS Code Tasks

Add to `.vscode/tasks.json`:

```json
{
  "label": "test-integration",
  "type": "shell",
  "command": "dotnet",
  "args": ["test", "${workspaceFolder}/WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj"],
  "group": "test"
}
```

---

## Performance Benchmarking

### Understanding Results

#### SQLite vs SQL Server: When to Use Each

**SQLite Advantages**:

- ⚡ Faster startup (no container overhead)
- 💾 Lower memory footprint
- 🚀 Excellent for unit tests and quick iterations
- 📦 No external dependencies

**SQL Server Advantages**:

- 🏢 Production-like behavior
- 🔒 Advanced features (row versioning, complex transactions)
- 📊 Better for bulk operations at scale
- 🎯 Real-world performance characteristics

#### Typical Performance Profiles

| Operation         | SQLite (Fast) | SQL Server (Robust) |
| ----------------- | ------------- | ------------------- |
| Single Insert     | 1-5ms         | 5-15ms              |
| Bulk Insert (100) | 10-30ms       | 20-50ms             |
| Simple Query      | 1-3ms         | 3-10ms              |
| Complex Join      | 5-10ms        | 10-20ms             |
| Update            | 2-5ms         | 5-15ms              |

**Note**: TestContainers adds container startup overhead (~5-10 seconds) but ensures accurate SQL Server behavior.

---

## Best Practices

### 1. Test Isolation

✅ **DO**: Use `ResetDatabaseAsync()` between tests if needed

```csharp
[Fact]
public async Task FirstTest()
{
    await ResetDatabaseAsync();
    // Test logic
}
```

❌ **DON'T**: Share state between tests

### 2. Container Management

✅ **DO**: Inherit from `SqlServerTestBase` to get automatic container management

```csharp
public class MyTests : SqlServerTestBase { }
```

❌ **DON'T**: Manually create TestContainers in every test

### 3. Performance Testing

✅ **DO**: Seed sufficient data for realistic performance tests

```csharp
await SeedDatabaseWithRelationships(100); // Realistic data size
```

❌ **DON'T**: Test performance with empty or trivial datasets

### 4. Concurrency Testing

✅ **DO**: Use separate DbContext instances for concurrent operations

```csharp
using var context1 = CreateDbContext();
using var context2 = CreateDbContext();
```

❌ **DON'T**: Reuse the same context to simulate concurrency

### 5. Relationship Testing

✅ **DO**: Test both directions of navigation properties

```csharp
// Parent to child
var account = await context.Accounts.Include(a => a.Transactions).FirstAsync();

// Child to parent
var transaction = await context.Transactions.Include(t => t.Account).FirstAsync();
```

❌ **DON'T**: Assume bidirectional navigation works if one direction passes

### 6. CI/CD Integration

✅ **DO**: Ensure Docker is available in CI environment

```yaml
# GitHub Actions example
- name: Start Docker
  run: docker ps

- name: Run Integration Tests
  run: dotnet test WileyWidget.IntegrationTests
```

❌ **DON'T**: Skip integration tests in CI due to Docker requirements

---

## Troubleshooting

### Docker Not Running

**Error**: "Cannot connect to Docker daemon"

**Solution**:

```powershell
# Start Docker Desktop
Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"

# Wait for Docker to be ready
docker ps
```

### Container Startup Timeout

**Error**: "Container failed to start within timeout"

**Solution**:

- Increase Docker memory allocation (Settings > Resources)
- Check Docker logs: `docker logs <container-id>`
- Verify SQL Server image is downloaded: `docker images`

### Port Conflicts

**Error**: "Port already in use"

**Solution**:

- TestContainers automatically assigns random ports
- If issues persist, stop other SQL Server instances
- Check running containers: `docker ps`

### Performance Test Variance

**Issue**: High variance in performance results

**Solution**:

- Run benchmarks multiple times
- Ensure no other intensive processes running
- Use BenchmarkDotNet for statistical analysis
- Consider warmup iterations

---

## Future Enhancements

### Phase 3 Roadmap

- [ ] **Respawn Integration**: Fast database cleanup between tests
- [ ] **Snapshot Isolation Tests**: Test SQL Server transaction isolation levels
- [ ] **Migration Tests**: Verify EF Core migrations work correctly
- [ ] **Parallel Test Execution**: Multiple containers for faster test runs
- [ ] **Azure SQL Database Tests**: Cloud database integration testing

---

## Resources

- [TestContainers for .NET](https://dotnet.testcontainers.org/)
- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [EF Core Testing](https://learn.microsoft.com/en-us/ef/core/testing/)
- [xUnit Documentation](https://xunit.net/)

---

## Summary

The Wiley Widget integration testing infrastructure provides:

✅ **Real SQL Server testing** via TestContainers  
✅ **Comprehensive concurrency tests** with multiple resolution strategies  
✅ **Relationship integrity verification** with cascade and constraint testing  
✅ **Performance benchmarking** comparing SQLite vs SQL Server  
✅ **CI/CD ready** with Docker-based testing  
✅ **Best practices** for isolation, performance, and maintainability

**Phase 2 Complete!** 🎉 The testing infrastructure is production-ready and integrated into the development workflow.
