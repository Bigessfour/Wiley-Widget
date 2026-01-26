# Diagnostic Implementation Summary

**Date:** January 25, 2026
**Status:** IMPLEMENTATION COMPLETE ✅
**Next Step:** Run diagnostics and analyze exit code -1

---

## Changes Implemented

### 1. **Enhanced Global Exception Handlers** (Program.cs)

**Location:** `src/WileyWidget.WinForms/Program.cs` lines 83-124

**What was added:**

- **AppDomain-level handler** - Catches exceptions that ThreadException might miss
- **Enhanced ThreadException logging** - Logs full exception chain (5 levels deep) with stack traces before process termination
- **Pre-display log flushing** - Ensures all diagnostics are written to disk before showing error dialog
- **Exit code -1 confirmation** - Explicitly notes in logs when application terminates with -1

**Why this matters:**

- AppDomain handler fires for exceptions on background threads
- Stack trace capture shows exact line where exception occurred
- Pre-flushing ensures logs aren't lost even if error dialog fails

---

### 2. **OnLoad Diagnostics** (MainForm.cs)

**Location:** `src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs` lines 322-380

**Diagnostic checkpoints added:**

```
[DIAGNOSTIC] MainForm.OnLoad START
  → base.OnLoad completed
  → Loading MRU list
  → Restoring window state
  → Starting UI chrome initialization
    → UI chrome initialization completed
       OR InitializeChrome FAILED (with exception type/message)
  → Starting Z-order management
    → Z-order management completed
[DIAGNOSTIC] OnLoad: UI initialization COMPLETED SUCCESSFULLY
```

**How to use:**

- If logs stop at "UI chrome initialization", the failure is in **InitializeChrome()**
- If logs stop at "Z-order management", the failure is in **z-order setup**
- If you see "COMPLETED SUCCESSFULLY", OnLoad passed ✅

---

### 3. **OnShown Diagnostics** (MainForm.cs)

**Location:** `src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs` lines 398-480

**Diagnostic checkpoints added:**

```
[DIAGNOSTIC] MainForm.OnShown START
  → Validation state check PASSED
    OR Validation state check FAILED (with error message)
       → Application exits cleanly via Application.Exit()
  → Starting Syncfusion docking initialization
  → Syncfusion docking initialized, refreshing UI
  → Loading docking layout
  → Applying theme to docking panels
  → Docking layout and theme applied
[DIAGNOSTIC] OnShown: Starting deferred initialization
```

**How to use:**

- If logs stop at "Validation state check FAILED", check the error message - likely ServiceProvider/Theme/DockingManager issue
- If logs stop at "Syncfusion docking initialization", InitializeSyncfusionDocking() threw an exception
- If you see "deferred initialization", OnShown passed ✅

---

## How to Run Diagnostics

### Quick Start (Recommended)

```powershell
cd c:\Users\biges\Desktop\Wiley-Widget
.\diagnostic-startup.ps1
```

This script will:

1. ✅ Verify build is current
2. ✅ Run the app and capture console output
3. ✅ Wait for app to exit (with code -1)
4. ✅ Display last 50 lines of log file with syntax highlighting
5. ✅ Highlight all [DIAGNOSTIC] and ERROR lines
6. ✅ Show summary with next steps

**Expected output:**

- Green "[DIAGNOSTIC]" messages showing successful checkpoints
- Red "[DIAGNOSTIC]" followed by ERROR showing where it failed
- Exit code -1 with timestamp

---

## What to Look For in the Output

### Ideal Success Sequence

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
[DIAGNOSTIC] OnShown: Validating initialization state
[DIAGNOSTIC] OnShown: Validation state check PASSED
[DIAGNOSTIC] OnShown: Starting Syncfusion docking initialization
...
```

### Diagnostic Failure Points (In Order of Probability)

**1. InitializeChrome() Fails** 🔴

```
[DIAGNOSTIC] OnLoad: Starting UI chrome initialization
[DIAGNOSTIC] OnLoad: InitializeChrome FAILED - [ExceptionType]: [Message]
Exit Code: -1
```

→ Check `InitializeChrome()` in `MainForm.Chrome.cs` for:

- Ribbon creation failures
- MenuStrip initialization errors
- Status bar setup exceptions

**2. ValidateInitializationState() Fails** 🟡

```
[DIAGNOSTIC] OnShown: Validating initialization state
[DIAGNOSTIC] OnShown: Initialization state validation FAILED - [Message]
```

→ Likely one of:

- ServiceProvider is null
- IThemeService not resolved
- Theme name not configured
- Form handle not created

**3. InitializeSyncfusionDocking() Fails** 🔴

```
[DIAGNOSTIC] OnShown: Starting Syncfusion docking initialization
[DIAGNOSTIC] OnShown: Syncfusion docking initialization FAILED - [ExceptionType]: [Message]
```

→ Check `InitializeSyncfusionDocking()` in `MainForm.Docking.cs` for:

- DockingManager creation errors
- Panel host failures
- Layout manager exceptions

**4. Z-order Management Fails** 🔴

```
[DIAGNOSTIC] OnLoad: Starting Z-order management
[DIAGNOSTIC] OnLoad failed during z-order configuration - [ExceptionType]: [Message]
Exit Code: -1
```

→ BringToFront() or Refresh() called on disposed/null controls

---

## Critical Information Captured

**Each diagnostic message includes:**

- ✅ Exact lifecycle phase (OnLoad/OnShown/Chrome/Docking/etc.)
- ✅ Exception type (NullReferenceException, InvalidOperationException, etc.)
- ✅ Exception message (what went wrong)
- ✅ Stack trace (where in the code it failed)

**If failure occurs:**

1. Find the [DIAGNOSTIC] message right before the error
2. Note the exception type and message
3. Go to that line in the code (stack trace shows it)
4. Check for null references, disposed objects, or missing services

---

## Example Analysis

### Scenario: App exits with code -1 after showing UI

**You see in logs:**

```
[DIAGNOSTIC] OnLoad: UI initialization COMPLETED SUCCESSFULLY
[DIAGNOSTIC] MainForm.OnShown START
[DIAGNOSTIC] OnShown: Validating initialization state
ERROR: OnShown: Initialization state validation FAILED - ServiceProvider not initialized
Exit Code: -1
```

**Diagnosis:** ServiceProvider is null in OnShown
**Next step:** Check MainForm constructor - ensure `_serviceProvider` is assigned from DI

---

## Files Modified

| File                                                  | Lines   | Change                                                    |
| ----------------------------------------------------- | ------- | --------------------------------------------------------- |
| `src/WileyWidget.WinForms/Program.cs`                 | 83-124  | Enhanced global exception handlers with AppDomain support |
| `src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs` | 322-380 | Added OnLoad diagnostics (8 checkpoints)                  |
| `src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs` | 398-480 | Added OnShown diagnostics (7 checkpoints)                 |
| `diagnostic-startup.ps1` (new)                        | N/A     | PowerShell diagnostic runner script                       |

**Total diagnostic breakpoints added:** 15

---

## Next Immediate Steps

1. **Run diagnostic script:**

   ```powershell
   .\diagnostic-startup.ps1
   ```

2. **Capture the full output** - Copy all text from the PowerShell console

3. **Find the failure point:**
   - Look for the last successful `[DIAGNOSTIC]` message
   - Look for the first `ERROR` or `CRITICAL` message after it
   - That's the root cause

4. **Share the results** with:
   - Last successful [DIAGNOSTIC] message
   - First ERROR message with exception type and stack trace
   - Exit code and runtime duration

---

## Expected Run Time

| Phase             | Estimated Duration        |
| ----------------- | ------------------------- |
| Build check       | < 2 seconds               |
| App startup       | 1-2 seconds (before exit) |
| Log file analysis | 1 second                  |
| **Total**         | **~5 seconds**            |

---

## Troubleshooting the Diagnostics

**Q: No console output appears?**
A: EXE might not exist or build failed. Script will auto-build - check console for errors.

**Q: Logs not found in %APPDATA%\WileyWidget\logs?**
A: Check if path exists. Logs may be in different location - check Program.cs EnsureLogDirectoryExists().

**Q: Still showing "exit code -1" with no diagnostics?**
A: Exception might be occurring before logging is initialized. Check:

- Event log (Windows Event Viewer)
- Crash dump (if system generated one)
- Run with debugger: `dotnet run --project src/WileyWidget.WinForms/`

---

## Key Takeaway

You now have **15 diagnostic checkpoints** from Program.Main() startup through MainForm.OnShown completion. When you run the app, the logs will show exactly where and why it exits with code -1.

**Run the diagnostic script and share the output - the root cause will be visible in the logs.**
