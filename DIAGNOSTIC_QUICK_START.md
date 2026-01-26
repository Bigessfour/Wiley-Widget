# Diagnostic Implementation - Quick Start

**Status:** ✅ Implementation Complete

## What Was Done

1. **Enhanced exception handlers** in `Program.cs`
   - Added AppDomain-level exception catcher
   - Enhanced ThreadException logging with full stack traces
   - Logs full exception chain before showing error dialog

2. **Added 15 diagnostic checkpoints** in MainForm initialization
   - OnLoad: 8 checkpoints (MRU, window state, chrome, z-order)
   - OnShown: 7 checkpoints (validation, docking, layout, theme)

3. **Created diagnostic runner script** `diagnostic-startup.ps1`
   - Builds project if needed
   - Runs app and captures console output
   - Displays last 50 lines of log with syntax highlighting
   - Identifies failure points automatically

## How to Run

```powershell
cd c:\Users\biges\Desktop\Wiley-Widget
.\diagnostic-startup.ps1
```

The script will:

1. Verify build
2. Run app (it will exit with code -1)
3. Show highlighted diagnostic output
4. Point you to the failure location

## What to Expect

**Success sequence in logs:**

```
[DIAGNOSTIC] MainForm.OnLoad START
[DIAGNOSTIC] OnLoad: Loading MRU list
[DIAGNOSTIC] OnLoad: Restoring window state
[DIAGNOSTIC] OnLoad: Starting UI chrome initialization
[DIAGNOSTIC] OnLoad: UI chrome initialization completed
[DIAGNOSTIC] OnLoad: Starting Z-order management
[DIAGNOSTIC] OnLoad: Z-order management completed
[DIAGNOSTIC] OnLoad: UI initialization COMPLETED SUCCESSFULLY
[DIAGNOSTIC] MainForm.OnShown START
...
```

**If it fails, you'll see:**

```
[DIAGNOSTIC] [Point before failure]
ERROR: [Point of failure]: [Exception Type]: [Exception Message]
Exit Code: -1
```

The error message will tell you exactly what went wrong and where.

## Expected Root Causes (In Order)

1. **InitializeChrome() throws exception** → Chrome (ribbon/menustrip) creation failed
2. **ValidateInitializationState() fails** → ServiceProvider/Theme/DockingManager null
3. **InitializeSyncfusionDocking() throws** → Docking manager/panel creation failed
4. **Z-order management fails** → BringToFront() called on disposed control

## Files Modified

- `src/WileyWidget.WinForms/Program.cs` - Enhanced exception handlers
- `src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs` - Added diagnostics
- `diagnostic-startup.ps1` (new) - Diagnostic runner

## Next Step

Run the diagnostic script and share the output showing where it exits.
