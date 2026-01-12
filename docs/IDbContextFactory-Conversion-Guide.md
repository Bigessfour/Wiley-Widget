# IDbContextFactory Pattern Conversion Guide

## Overview

Convert repositories from direct AppDbContext injection to IDbContextFactory<AppDbContext> pattern to prevent ObjectDisposedException.

## Files to Update

### 1. src/WileyWidget.Data/AccountsRepository.cs

**Changes:**

```csharp
// BEFORE
private readonly AppDbContext _dbContext;

public AccountsRepository(AppDbContext dbContext, ILogger<AccountsRepository> logger)
{
    _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}

// AFTER
private readonly IDbContextFactory<AppDbContext> _contextFactory;

public AccountsRepository(IDbContextFactory<AppDbContext> contextFactory, ILogger<AccountsRepository> logger)
{
    _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

**Method Pattern:**

```csharp
// BEFORE
var result = await _dbContext.Set<MunicipalAccount>()
    .OrderBy(a => a.AccountNumber_Value)
    .ToListAsync(cancellationToken);

// AFTER
await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
var result = await context.Set<MunicipalAccount>()
    .AsNoTracking()  // Add for read-only queries
    .OrderBy(a => a.AccountNumber_Value)
    .ToListAsync(cancellationToken);
```

### 2. src/WileyWidget.Data/BudgetRepository.cs

Same pattern as above, but has 30+ methods. Each method using `_context` needs:

1. Create context: `await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);`
2. Replace `_context.` with `context.`
3. Add `.AsNoTracking()` for read queries

### 3. src/WileyWidget.Data/MunicipalAccountRepository.cs

This file has both `AppDbContext _context` field AND uses compiled queries. Keep compiled queries, but update:

- Constructor to inject `IDbContextFactory<AppDbContext>`
- Store factory in field
- Create context in methods that use `_context`

### 4. src/WileyWidget.Data/DatabaseSeeder.cs

Simple update - same constructor pattern, create context in SeedAsync method.

### 5. src/WileyWidget.WinForms/Services/BudgetCategoryService.cs

Service layer file - update to use factory pattern like repositories.

## Key Rules

1. **Always use `await using`** for context disposal
2. **Always add `.AsNoTracking()`** for read-only queries (prevents tracking overhead)
3. **Pass `cancellationToken`** to `CreateDbContextAsync(ct)`
4. **One context per operation** - don't share contexts across methods
5. **Don't expose IQueryable** - materialize with `.ToListAsync()` before returning

## Testing After Changes

Run these tests to verify:

```powershell
# Build
dotnet build src/WileyWidget.Data/WileyWidget.Data.csproj

# Run repository tests
dotnet test tests/WileyWidget.Services.Tests/

# Run full application and watch for ObjectDisposedException
dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
```

## Expected Benefits

✅ No more ObjectDisposedException from disposed contexts
✅ Each operation gets its own properly-scoped context
✅ Reduced memory usage with .AsNoTracking()
✅ Better async/await patterns with proper disposal
✅ Aligns with EF Core best practices for WinForms apps
