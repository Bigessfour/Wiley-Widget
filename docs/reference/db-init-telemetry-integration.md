# Database Initialization Telemetry Integration

## Overview

The `DatabaseInitializer` class has been enhanced with comprehensive logging and telemetry tracking following the established patterns in `App.Telemetry.cs` and `HealthReportingService.cs`.

## Integration Summary

### 1. **SigNoz Activity Tracking**

The database initialization process is now wrapped in a SigNoz activity span:

```csharp
_startupActivity = _telemetryService?.StartActivity("DB.Initialization");
_startupActivity?.SetTag("db.operation", "migrate");
```

### 2. **Memory Monitoring (Aligned with VerifyAndApplyTheme)**

Similar to Syncfusion theme application, DB initialization now tracks memory usage:

```csharp
var gcMemInfo = GC.GetGCMemoryInfo();
var availableMemoryMB = gcMemInfo.TotalAvailableMemoryBytes / (1024 * 1024);
var currentMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);

_startupActivity?.SetTag("memory.available_mb", availableMemoryMB);
_startupActivity?.SetTag("memory.used_mb", currentMemoryMB);

// Warn if low memory
if (availableMemoryMB < 256)
{
    Log.Warning("⚠️ Low memory detected ({AvailableMB}MB) before DB initialization", availableMemoryMB);
    _startupActivity?.SetTag("memory.warning", "low_available_memory");
}
```

### 3. **Comprehensive Event Tracking**

All major DB operations emit ActivityEvent markers:

| Event                      | Description                  | Tags                          |
| -------------------------- | ---------------------------- | ----------------------------- |
| `DB.Context.Created`       | DbContext instantiated       | None                          |
| `DB.Backup.Start`          | Backup operation starting    | None                          |
| `DB.Backup.Success`        | Backup completed             | None                          |
| `DB.Backup.Failed`         | Backup failed (non-fatal)    | `error.message`               |
| `DB.Migrate.Start`         | Migration starting           | None                          |
| `DB.Migrate.Success`       | Migration completed          | `duration_ms`                 |
| `DB.Migrate.Failed`        | Migration failed             | `error.message`, `error.type` |
| `DB.EnsureCreated.Start`   | Fallback EnsureCreated       | None                          |
| `DB.EnsureCreated.Success` | Fallback succeeded           | None                          |
| `DB.EnsureCreated.Failed`  | Both migrate & ensure failed | `error.message`, `error.type` |
| `DB.HealthCheck.Start`     | Post-migration connectivity  | None                          |
| `DB.HealthCheck.Success`   | CanConnect() passed          | None                          |
| `DB.HealthCheck.Failed`    | CanConnect() returned false  | `reason`                      |
| `DB.HealthCheck.Exception` | CanConnect() threw           | `error.message`, `error.type` |

### 4. **SigNoz Tag Examples**

#### Success Path Tags:

```csharp
_startupActivity?.SetTag("db.operation", "migrate");
_startupActivity?.SetTag("db.operation.result", "success");
_startupActivity?.SetTag("db.health", "healthy");
_startupActivity?.SetTag("db.schema_snapshot", versionInfo);
_startupActivity?.SetTag("db.init.duration_ms", stopwatch.ElapsedMilliseconds);
_startupActivity?.SetTag("db.init.status", "success");
_startupActivity?.SetTag("memory.final_mb", finalMemoryMB);
_startupActivity?.SetTag("memory.delta_mb", finalMemoryMB - currentMemoryMB);
```

#### Degraded/Error Path Tags:

```csharp
_startupActivity?.SetTag("db.health", "degraded");
_startupActivity?.SetTag("db.operation.result", "failed");
_startupActivity?.SetTag("db.init.status", "failed");
_startupActivity?.SetTag("error.message", ex.Message);
_startupActivity?.SetTag("error.type", ex.GetType().Name);
_startupActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
```

### 5. **Health Check Integration (HealthReportingService Pattern)**

Post-migration validation now includes:

1. **CanConnect() validation** - Verifies database connectivity
2. **Schema snapshot retrieval** - Captures DB version via `GenerateCreateScript()`
3. **Health status tagging** - Sets `db.health` to `healthy`, `degraded`, or `error`

```csharp
var canConnect = await context.Database.CanConnectAsync(cancellationToken);

if (canConnect)
{
    Log.Information("✅ Post-migration health check: Database connection verified");

    var dbVersion = context.Database.GenerateCreateScript();
    _startupActivity?.SetTag("db.schema_snapshot", versionInfo);
    _startupActivity?.SetTag("db.health", "healthy");
}
else
{
    Log.Warning("⚠️ Post-migration health check: Database connection test failed - degraded state");
    _startupActivity?.SetTag("db.health", "degraded");
}
```

### 6. **Serilog Levels (Aligned with Manifest Guidelines)**

| Scenario      | Serilog Level | Example                                                             |
| ------------- | ------------- | ------------------------------------------------------------------- |
| Success       | `Information` | `✅ Database migrated successfully in {ElapsedMs}ms`                |
| Degraded      | `Warning`     | `⚠️ Low memory detected ({AvailableMB}MB) before DB initialization` |
| Degraded Conn | `Warning`     | `⚠️ Post-migration health check: Database connection test failed`   |
| Failure       | `Error`       | `❌ Database initialization failed after {ElapsedMs}ms`             |

### 7. **Exception Handling**

All exceptions are recorded with comprehensive context:

```csharp
_telemetryService?.RecordException(ex,
    ("operation", "db_initialization"),
    ("duration_ms", stopwatch.ElapsedMilliseconds));
```

The `RecordException` method automatically:

- Sets `ActivityStatusCode.Error`
- Adds `exception.type`, `exception.message`, `exception.stacktrace` tags
- Logs to Serilog with `LogError`

### 8. **Non-Blocking Failure Handling**

Database initialization failures **do not terminate the application**:

```csharp
catch (Exception ex)
{
    Log.Error(ex, "❌ Database initialization failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
    // ... telemetry tracking ...
    // Don't throw - allow startup to continue with degraded DB functionality
}
```

This aligns with the startup resilience pattern where non-critical failures are logged but don't prevent app launch.

## SigNoz Query Examples

### Query 1: Find all DB initialization attempts

```
service.name="wiley-widget" AND span.name="DB.Initialization"
```

### Query 2: Find failed migrations

```
service.name="wiley-widget" AND span.name="DB.Initialization" AND db.init.status="failed"
```

### Query 3: Find slow migrations (>5s)

```
service.name="wiley-widget" AND span.name="DB.Initialization" AND db.init.duration_ms>5000
```

### Query 4: Find degraded DB health

```
service.name="wiley-widget" AND span.name="DB.Initialization" AND db.health="degraded"
```

### Query 5: Memory warnings during DB init

```
service.name="wiley-widget" AND span.name="DB.Initialization" AND memory.warning EXISTS
```

## Observability Benefits

1. **Startup Performance**: Track DB migration duration across deployments
2. **Memory Correlation**: Identify if memory constraints affect DB initialization
3. **Health Monitoring**: Real-time visibility into DB connectivity post-migration
4. **Failure Analysis**: Comprehensive error context for troubleshooting
5. **Schema Tracking**: Capture DB version snapshots for audit trails

## Constructor Injection Update

The `DatabaseInitializer` now accepts an optional `SigNozTelemetryService`:

```csharp
public DatabaseInitializer(
    DbContextOptions<AppDbContext> options,
    IConfiguration configuration,
    ILogger<DatabaseInitializer> logger,
    SigNozTelemetryService? telemetryService = null)
```

This maintains backward compatibility while enabling telemetry when available.

## Alignment with Project Patterns

| Pattern            | Source File                                             | Implementation                                        |
| ------------------ | ------------------------------------------------------- | ----------------------------------------------------- |
| Memory Checks      | `App.Resources.cs:VerifyAndApplyTheme()`                | `GC.GetGCMemoryInfo()` before DB operations           |
| Activity Events    | `App.Telemetry.cs:InitializeSigNozTelemetry()`          | `_startupActivity?.AddEvent(new ActivityEvent(...))`  |
| Exception Handling | `App.ExceptionHandling.cs:DispatcherUnhandledException` | Wrapped in try-catch with full telemetry              |
| Health Checks      | `HealthReportingService.cs:UpdateLatestHealthReport()`  | `context.Database.CanConnect()` validation            |
| Serilog Warnings   | Project standard                                        | Warning level for degraded states, Error for failures |

## Related Files

- `src/WileyWidget/Startup/DatabaseInitializer.cs` - Updated implementation
- `src/WileyWidget/App.Telemetry.cs` - Telemetry patterns reference
- `src/WileyWidget/App.Resources.cs` - Memory check pattern reference
- `src/WileyWidget/Services/Startup/HealthReportingService.cs` - Health check pattern reference
- `src/WileyWidget.Services/SigNozTelemetryService.cs` - Telemetry service API
