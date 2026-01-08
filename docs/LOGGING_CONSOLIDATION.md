# Logging Consolidation Guide

## Problem

The application previously had multiple loggers configured to write to different folders throughout the project directory, causing logs to be scattered and difficult to find:

- ❌ `src/logs/` - MainForm async diagnostics logger (old location)
- ❌ `bin/Debug/...` - Bootstrap logger (AppContext.BaseDirectory based)
- ❌ Multiple separate LoggerConfiguration instances across the codebase

This made troubleshooting difficult as logs were fragmented across different locations.

## Solution: Centralized Logging

All logging now routes to a **single unified location**: `<PROJECT_ROOT>/logs/`

### Key Changes

#### 1. Bootstrap Logger (Program.cs - ConfigureBootstrapLogger)
**Before:**
```csharp
var basePath = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
var logsPath = Path.Combine(basePath, "logs");  // ❌ Could be bin/Debug or other paths
```

**After:**
```csharp
var projectRoot = Directory.GetCurrentDirectory();  // Always project root
var logsPath = Path.Combine(projectRoot, "logs");  // ✓ {PROJECT_ROOT}/logs
```

#### 2. Main Logger Configuration (Program.cs - ConfigureLogging)
The Serilog logger configured via `ReadFrom.Configuration()` now explicitly writes to the same centralized directory with proper path resolution.

#### 3. MainForm Async Diagnostics Logger (MainForm.cs - OnShown)
**Before:**
```csharp
var baseDir = AppDomain.CurrentDomain.BaseDirectory;
var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));  // ❌ Complex path traversal
var logsDirectory = Path.Combine(repoRoot, "logs");
```

**After:**
```csharp
var projectRoot = Directory.GetCurrentDirectory();  // ✓ Direct to project root
var logsDirectory = Path.Combine(projectRoot, "logs");  // ✓ {PROJECT_ROOT}/logs
```

### Log Files Structure

All logs are now consolidated in `<PROJECT_ROOT>/logs/`:

```
WileyWidget/
└── logs/
    ├── app-20260107.log              # Main application log (daily rolling)
    ├── mainform-diagnostics-20260107.log  # MainForm async diagnostics (daily rolling)
    └── (additional daily files as they age...)
```

### Logger Instances

| Logger | File Pattern | Purpose | Configured In |
|--------|--------------|---------|---------------|
| **Bootstrap Logger** | `app-.log` | Startup & early messages | `Program.ConfigureBootstrapLogger()` |
| **Main Logger** | `app-.log` | Application-wide logging | `Program.ConfigureLogging()` |
| **MainForm Async** | `mainform-diagnostics-.log` | Form initialization diagnostics | `MainForm.OnShown()` |

All write to **`<PROJECT_ROOT>/logs/`**

### Environment Variable Configuration

If you need to override the default log location, you can use the existing environment variable support:

```powershell
# Enable SQL query logging
$env:WILEYWIDGET_LOG_SQL = "true"

# Run the application
dotnet run
```

### Log File Rotation

- **Rolling Interval**: Daily (midnight UTC)
- **File Pattern**: `app-{Date:yyyyMMdd}.log`
- **Retention**: 30 days (automatically cleaned up)
- **Max File Size**: 10 MB per file
- **Roll on Size**: Enabled (creates `app-20260107_001.log`, etc. if exceeds 10 MB)

### Verification

1. **Run the application:**
   ```powershell
   cd src/WileyWidget.WinForms
   dotnet run
   ```

2. **Check the logs directory:**
   ```powershell
   ls logs/
   # Should show:
   # app-20260107.log
   # mainform-diagnostics-20260107.log
   ```

3. **Verify content:**
   ```powershell
   Get-Content logs/app-20260107.log | head -20
   # Should show bootstrap and main logger messages like:
   # "✓ Bootstrap logger initialized - CENTRALIZED LOGS..."
   # "✓ CENTRALIZED LOGGING INITIALIZED - All logs write to: ..."
   ```

### Breaking Changes (None)

This consolidation is **backward compatible**:
- All existing log reading code continues to work
- The log directory structure remains the same
- Only the internal logger instances are centralized

### Future Logging Additions

When adding new loggers, **always use this pattern:**

```csharp
var projectRoot = Directory.GetCurrentDirectory();
var logsDirectory = Path.Combine(projectRoot, "logs");
Directory.CreateDirectory(logsDirectory);
var logPath = Path.Combine(logsDirectory, "mylogger-.log");  // Use rolling interval

var logger = new LoggerConfiguration()
    .WriteTo.File(logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true)  // Important: shared=true for multiple loggers
    .CreateLogger();
```

### Troubleshooting

**Q: I don't see any logs in `/logs` directory**

A: Check that `Directory.GetCurrentDirectory()` returns the project root:
```powershell
# In PowerShell, check what GetCurrentDirectory returns
[System.Environment]::CurrentDirectory
# Should be: C:\Users\biges\Desktop\Wiley-Widget (or your project path)

# If running from bin/Debug/net10.0-windows, change to src/WileyWidget.WinForms first
cd src/WileyWidget.WinForms
dotnet run
```

**Q: Still seeing logs in multiple locations?**

A: Search for any other `LoggerConfiguration()` instances:
```powershell
grep -r "LoggerConfiguration\|WriteTo.File\|logs/" src/ --include="*.cs" | grep -v "obj/"
```

Ensure all use the centralized path pattern above.

**Q: Logs are empty or not appearing?**

A: Verify the main logger is initialized:
```powershell
# Look for the checkmark message
Get-Content logs/app-20260107.log | Select-String "CENTRALIZED LOGGING"
```

If missing, check for exceptions in `ConfigureLogging()`.

---

**Last Updated:** 2026-01-07  
**Status:** ✓ Consolidated - All logs centralized to `<PROJECT_ROOT>/logs`
