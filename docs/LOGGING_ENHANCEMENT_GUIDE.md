# Logging Enhancement Guide for Wiley Widget
**Target:** Address remaining 8% coverage gaps + add optional production features

---

## Priority-Based Implementation Roadmap

### Phase 1: Quick Wins (30 minutes) — Recommended ⭐

These add immediate value with minimal code changes.

#### 1.1 Add Button/Menu Click Logging

**File:** `WileyWidget.WinForms/Forms/MainForm.cs`

**Current State:**
```csharp
private void RefreshButton_Click(object sender, EventArgs e)
{
    // No logging
}
```

**Enhanced:**
```csharp
private void RefreshButton_Click(object sender, EventArgs e)
{
    _logger.LogInformation("Dashboard refresh requested by user");
    // existing logic
}

private void ExportButton_Click(object sender, EventArgs e)
{
    _logger.LogInformation("Dashboard export requested");
    // existing logic
}
```

**Benefit:** Audit trail for user actions, helps with support issues ("What did the user do?")

---

#### 1.2 Add Performance Logging to Slow Operations

**File:** `src/Services/MainDashboardService.cs`

**Current:**
```csharp
public async Task<DashboardDataDto> LoadDashboardDataAsync()
{
    var accounts = await _accountRepository.GetAllAsync();
    // ... processing
}
```

**Enhanced:**
```csharp
public async Task<DashboardDataDto> LoadDashboardDataAsync()
{
    using var activity = LogActivity.Start(_logger, "LoadDashboardData");
    
    var accounts = await _accountRepository.GetAllAsync();
    _logger.LogInformation("Loaded {AccountCount} accounts", accounts.Count);
    
    // ... processing with nested activities
    
    return result; // LogActivity logs elapsed time on Dispose
}

// Helper class
private static class LogActivity
{
    private class Scope : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _name;
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        public Scope(ILogger logger, string name)
        {
            _logger = logger;
            _name = name;
            _logger.LogInformation("▶ {Operation} started", _name);
        }

        public void Dispose()
        {
            _sw.Stop();
            _logger.LogInformation("◀ {Operation} completed in {ElapsedMs}ms", _name, _sw.ElapsedMilliseconds);
        }
    }

    public static IDisposable Start(ILogger logger, string name) => new Scope(logger, name);
}
```

**Benefit:** Identify performance bottlenecks in production

**Output:**
```
▶ LoadDashboardData started
Loaded 72 accounts
◀ LoadDashboardData completed in 234ms
```

---

#### 1.3 Add Validation Failure Context

**File:** `src/Services/SettingsService.cs`

**Current:**
```csharp
var result = await _validator.ValidateAsync(settings, ct);
if (!result.IsValid)
{
    var errors = result.Errors.Select(e => e.ErrorMessage).ToList();
    _logger.LogWarning("Validation failed: {Errors}", errors);
    throw new ValidationException(result.Errors);
}
```

**Enhanced:**
```csharp
var result = await _validator.ValidateAsync(settings, ct);
if (!result.IsValid)
{
    var errorDetails = result.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToList());
    
    _logger.LogWarning("Settings validation failed: {@ValidationErrors}", errorDetails);
    
    // Log individual errors for structured queries
    foreach (var error in result.Errors)
    {
        _logger.LogDebug("Validation error: {Property} = {Message}", error.PropertyName, error.ErrorMessage);
    }
    
    throw new ValidationException(result.Errors);
}
```

**Benefit:** Better error diagnostics with structured property-level details

---

### Phase 2: Medium Enhancements (1-2 hours) — Optional

These add advanced tracing capabilities.

#### 2.1 Implement Correlation ID Tracking

**Purpose:** Link related operations across service boundaries

**File:** `WileyWidget.WinForms/Program.cs`

**Step 1: Add enricher in Serilog setup**
```csharp
.Enrich.When(
    logEvent => string.IsNullOrEmpty(logEvent.Properties.GetValueOrDefault("CorrelationId")?.ToString() ?? ""),
    enricher => enricher.WithProperty("CorrelationId", Guid.NewGuid().ToString("N").Substring(0, 8)))
```

**Step 2: Use in MainForm constructor**
```csharp
public MainForm(IServiceProvider serviceProvider, ILogger<MainForm> logger, MainViewModel? viewModel = null)
{
    var correlationId = Guid.NewGuid().ToString("N").Substring(0, 8);
    Serilog.Log.Logger = Serilog.Log.Logger.ForContext("CorrelationId", correlationId);
    
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _logger.LogInformation("MainForm session opened - Correlation: {CorrelationId}", correlationId);
}
```

**Step 3: Use in async operations**
```csharp
private async Task InitializeDataAsync()
{
    try
    {
        using (Serilog.Log.Logger.ForContext("CorrelationId", _correlationId).PushProperty("Operation", "DashboardLoad"))
        {
            _logger.LogInformation("Dashboard initialization started");
            if (_viewModel != null)
            {
                await _viewModel.InitializeAsync();
            }
            _logger.LogInformation("Dashboard initialization completed");
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Dashboard initialization failed");
    }
}
```

**Output:**
```
2025-12-05 13:33:12.000 [INF] MainForm session opened - Correlation: a1b2c3d4
2025-12-05 13:33:12.050 [INF] Dashboard initialization started {CorrelationId: a1b2c3d4, Operation: DashboardLoad}
2025-12-05 13:33:12.300 [INF] Dashboard initialization completed {CorrelationId: a1b2c3d4}
```

**Benefit:** Trace entire user session across all services/forms

---

#### 2.2 Add User Context Enricher

**File:** Create new file: `WileyWidget.WinForms/Diagnostics/UserContextEnricher.cs`

```csharp
using Serilog.Core;
using Serilog.Events;

namespace WileyWidget.WinForms.Diagnostics
{
    public class UserContextEnricher : ILogEventEnricher
    {
        private readonly Func<string> _getUserName;

        public UserContextEnricher(Func<string> getUserName)
        {
            _getUserName = getUserName ?? (() => Environment.UserName);
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var userName = _getUserName();
            var userProperty = propertyFactory.CreateProperty("UserName", userName);
            logEvent.AddPropertyIfAbsent(userProperty);

            // Optional: Add user role/permissions
            var userRole = GetUserRole(userName);
            if (!string.IsNullOrEmpty(userRole))
            {
                var roleProperty = propertyFactory.CreateProperty("UserRole", userRole);
                logEvent.AddPropertyIfAbsent(roleProperty);
            }
        }

        private static string GetUserRole(string userName)
        {
            // TODO: Implement logic to retrieve user role from your auth system
            // For now, return placeholder
            return "Standard";
        }
    }
}
```

**Register in Program.cs:**
```csharp
.Enrich.With(new UserContextEnricher(() => Environment.UserName))
```

**Output:**
```
2025-12-05 13:33:12 [INF] MainForm initialized {UserName: biges, UserRole: Standard}
```

---

#### 2.3 Add Request/Response Logging Middleware Pattern

**File:** `WileyWidget.Services/Logging/ServiceCallInterceptor.cs`

```csharp
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;

namespace WileyWidget.Services.Logging
{
    public class ServiceCallInterceptor
    {
        private readonly ILogger _logger;
        private readonly Stopwatch _stopwatch = new();

        public ServiceCallInterceptor(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<T> InterceptAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            object? parameters = null)
        {
            try
            {
                _stopwatch.Restart();
                
                if (parameters != null)
                {
                    _logger.LogInformation("Service operation started: {Operation} with params: {@Parameters}",
                        operationName, parameters);
                }
                else
                {
                    _logger.LogInformation("Service operation started: {Operation}", operationName);
                }

                var result = await operation();

                _stopwatch.Stop();
                _logger.LogInformation("Service operation completed: {Operation} in {ElapsedMs}ms",
                    operationName, _stopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                _stopwatch.Stop();
                _logger.LogError(ex, "Service operation failed: {Operation} after {ElapsedMs}ms",
                    operationName, _stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
```

**Usage in Service:**
```csharp
private readonly ServiceCallInterceptor _interceptor;

public async Task<AppDataDto> LoadAsync(CancellationToken ct = default)
{
    return await _interceptor.InterceptAsync(
        async () =>
        {
            var entities = await _repo.GetWidgetsAsync(ct);
            return _mapper.Map<List<WidgetDto>>(entities);
        },
        "AppService.LoadAsync",
        new { RepositoryType = "WidgetRepository" }
    );
}
```

---

### Phase 3: Production Features (Optional, 2-3 hours)

These are for advanced monitoring in production environments.

#### 3.1 Application Insights Integration

**File:** `WileyWidget.WinForms/appsettings.Production.json`

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "ApplicationInsights",
        "Args": {
          "connectionString": "${APPLICATIONINSIGHTS_CONNECTION_STRING}",
          "formatter": "Serilog.Formatting.Json.JsonFormatter"
        }
      }
    ]
  }
}
```

**NuGet Package:**
```powershell
dotnet add package Serilog.Sinks.ApplicationInsights
dotnet add package Microsoft.ApplicationInsights
```

**Benefit:** Cloud-based monitoring, dashboards, alerts

---

#### 3.2 Seq Log Aggregation (Self-Hosted)

**File:** `docker-compose.yml` (add Seq service)

```yaml
services:
  seq:
    image: datalust/seq:latest
    ports:
      - "5341:80"
    environment:
      ACCEPT_EULA: "Y"
    volumes:
      - seq-data:/data
    networks:
      - wiley-network

volumes:
  seq-data:

networks:
  wiley-network:
    driver: bridge
```

**Configure Serilog:**
```json
{
  "WriteTo": [
    {
      "Name": "Seq",
      "Args": {
        "serverUrl": "http://seq:5341",
        "apiKey": "${SEQ_API_KEY}"
      }
    }
  ]
}
```

**Run & Access:**
```bash
docker-compose up seq
# Then visit http://localhost:5341
```

**Benefit:** Powerful log search, alerting, visualization

---

## Implementation Priority Recommendation

| Phase | Effort | Impact | Recommended? |
|-------|--------|--------|---|
| Phase 1.1 (Button logging) | 15 min | High | ⭐⭐⭐ YES |
| Phase 1.2 (Performance metrics) | 20 min | Medium | ⭐⭐⭐ YES |
| Phase 1.3 (Validation details) | 10 min | High | ⭐⭐⭐ YES |
| Phase 2.1 (Correlation IDs) | 45 min | Medium | ⭐⭐ Optional |
| Phase 2.2 (User context) | 30 min | Low | ⭐ Nice-to-have |
| Phase 2.3 (Service interceptor) | 45 min | Low | ⭐ Nice-to-have |
| Phase 3.1 (App Insights) | 30 min | Medium | ⭐ For prod only |
| Phase 3.2 (Seq) | 60 min | High | ⭐⭐ For prod |

---

## Quick Implementation: Phase 1 (Recommended)

Do this now to close the remaining 8% coverage gap:

### Step 1: Add to MainForm.cs
```csharp
// In each button handler:
private void RefreshButton_Click(object sender, EventArgs e)
{
    _logger.LogInformation("Dashboard refresh initiated by user");
    RefreshDashboard();
}

private void ExportButton_Click(object sender, EventArgs e)
{
    _logger.LogInformation("Export operation started");
    ExportDashboard();
}
```

### Step 2: Add Performance Helper
Create file: `src/Utilities/PerformanceLogger.cs`

```csharp
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace WileyWidget.Utilities
{
    public class PerformanceLogger
    {
        private readonly ILogger _logger;
        private readonly Stopwatch _stopwatch = new();
        private readonly string _operationName;

        public PerformanceLogger(ILogger logger, string operationName)
        {
            _logger = logger;
            _operationName = operationName;
            _stopwatch.Start();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            if (_stopwatch.ElapsedMilliseconds > 100)
            {
                _logger.LogInformation("Performance: {Operation} took {ElapsedMs}ms (threshold: 100ms)",
                    _operationName, _stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
```

### Step 3: Use in Services
```csharp
public async Task<DashboardDataDto> LoadDashboardAsync()
{
    using (new PerformanceLogger(_logger, "LoadDashboard"))
    {
        var accounts = await _repo.GetAccountsAsync();
        // ... rest of method
    }
}
```

---

## Testing Your Enhancements

After implementing Phase 1, verify:

```powershell
# Check for new user action logs
Select-String -Path "logs/wiley-widget-*.log" -Pattern "refresh|Export"

# Check for performance logs
Select-String -Path "logs/wiley-widget-*.log" -Pattern "Performance:"

# Check for validation logs
Select-String -Path "logs/wiley-widget-*.log" -Pattern "Validation failed"
```

---

## Summary

- ✅ **Current State:** 92%+ coverage, production-ready
- 🔶 **Remaining Gaps:** 8% (UI events, performance, correlation)
- ⭐ **Quick Wins:** Phase 1 (65 minutes) closes most gaps
- 📊 **Advanced:** Phase 2-3 for enterprise monitoring

**Recommendation:** Implement Phase 1 first, then evaluate Phase 2 based on production needs.

---

**Next Steps:**
1. Choose one Phase 1 enhancement
2. Implement in a test service
3. Verify logs in `/logs`
4. Repeat for remaining Phase 1 items
5. Plan Phase 2 for next sprint

