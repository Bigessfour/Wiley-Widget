# Logging Consolidation - Changes Summary

## Overview

Fixed the logging system where multiple loggers were writing to different folders throughout the project. All logging is now **centralized** to a single location: `<PROJECT_ROOT>/logs/`

## Files Modified

### 1. `src/WileyWidget.WinForms/Program.cs`

#### ConfigureBootstrapLogger() - Line 62-82

- **Changed**: Bootstrap logger now uses `Directory.GetCurrentDirectory()` instead of `AppContext.BaseDirectory`
- **Result**: Logs write to `{PROJECT_ROOT}/logs/` instead of `bin/Debug/net10.0-windows/logs/`
- **Impact**: Bootstrap logger messages now appear in the main `app-.log` file

#### ConfigureLogging() - Line 1100-1160

- **Added**: Explicit logging that confirms CENTRALIZED LOGGING is active
- **Changed**: Added clear log messages indicating all logs go to the centralized directory
- **Result**: Better visibility into where logs are being written

### 2. `src/WileyWidget.WinForms/Forms/MainForm.cs`

#### OnShown() - Line 697-720

- **Changed**: MainForm async diagnostics logger now uses `Directory.GetCurrentDirectory()` instead of traversing `AppContext.BaseDirectory` up 4 levels
- **Before**:
  ```csharp
  var baseDir = AppDomain.CurrentDomain.BaseDirectory;
  var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
  var logsDirectory = Path.Combine(repoRoot, "logs");  // ❌ src/logs
  var asyncLogPath = Path.Combine(logsDirectory, "mainform-async-.log");
  ```
- **After**:
  ```csharp
  var projectRoot = Directory.GetCurrentDirectory();
  var logsDirectory = Path.Combine(projectRoot, "logs");  // ✓ {PROJECT_ROOT}/logs
  var asyncLogPath = Path.Combine(logsDirectory, "mainform-diagnostics-.log");
  ```
- **Result**: MainForm diagnostics now write to `mainform-diagnostics-{DATE}.log` in centralized location
- **File Rename**: `mainform-async-.log` → `mainform-diagnostics-.log` (clearer purpose)

## Summary of Logger Instances

| Logger             | Location    | Path Pattern                | Daily Rolling |
| ------------------ | ----------- | --------------------------- | ------------- |
| **Bootstrap**      | Program.cs  | `app-.log`                  | ✓             |
| **Main**           | Program.cs  | `app-.log`                  | ✓             |
| **MainForm Async** | MainForm.cs | `mainform-diagnostics-.log` | ✓             |

**All three now write to:** `<PROJECT_ROOT>/logs/`

## New Documentation Files

### 1. `docs/LOGGING_CONSOLIDATION.md`

Comprehensive guide covering:

- What changed and why
- How the new logging works
- Log file structure
- Verification steps
- Troubleshooting
- Future guidelines

### 2. `scripts/verify-logging-setup.ps1`

PowerShell utility to:

- Audit all LoggerConfiguration instances
- Verify centralized path usage
- Check for logs in old locations
- Report current logging setup
- Run with: `.\scripts\verify-logging-setup.ps1`

## Verification Steps

### Step 1: Run the application

```powershell
cd src/WileyWidget.WinForms
dotnet run
```

### Step 2: Check the logs

```powershell
# List log files
Get-ChildItem logs/ | Where-Object { $_.Extension -eq ".log" }

# Expected output:
# - app-20260107.log
# - mainform-diagnostics-20260107.log
```

### Step 3: Verify bootstrap logger message

```powershell
# Should see:
Get-Content logs/app-*.log | Select-String "CENTRALIZED LOGS"
Get-Content logs/app-*.log | Select-String "Bootstrap logger initialized"
```

### Step 4: Run the verification script

```powershell
.\scripts\verify-logging-setup.ps1
```

## Benefits

✅ **Single Centralized Location**: All logs in `{PROJECT_ROOT}/logs/`  
✅ **Easier Debugging**: No more searching multiple directories  
✅ **Better Organization**: Clear file names indicate log purpose  
✅ **Consistent Pattern**: Future loggers will follow same pattern  
✅ **Backward Compatible**: Existing code continues to work

## Backward Compatibility

✅ **No Breaking Changes**: All changes are internal  
✅ **File Format**: Same Serilog format, same structure  
✅ **Config**: No changes needed to `appsettings.json`  
✅ **API**: Public logger instances unchanged

## Future Maintenance

When adding new loggers or modifying existing ones:

1. Always use: `Directory.GetCurrentDirectory()` to get project root
2. Always create: `logs/` subdirectory
3. Always use: Rolling file sink with daily interval
4. Always include: Clear initialization message
5. Always set: `shared: true` for multiple loggers

Example pattern:

```csharp
var projectRoot = Directory.GetCurrentDirectory();
var logsPath = Path.Combine(projectRoot, "logs");
Directory.CreateDirectory(logsPath);
var logPath = Path.Combine(logsPath, "mylogger-.log");

var logger = new LoggerConfiguration()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, shared: true)
    .CreateLogger();

logger.Information("✓ My logger initialized - path: {LogPath}", logPath);
```

---

**Completed**: 2026-01-07  
**Status**: ✓ All loggers consolidated to centralized location
