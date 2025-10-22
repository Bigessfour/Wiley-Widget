# Wiley Widget Startup Error Diagnosis Plan

This document outlines a prioritized plan to diagnose and resolve the elusive startup runtime errors in the Wiley Widget WPF application. These steps are based on common WPF/Prism issues, the project's architecture (e.g., DI with Unity, Syncfusion, EF Core), and recent testing efforts. Follow them sequentially for efficient troubleshooting.

## Prioritized Next Steps

1. **Enable and Review Detailed Logging (Immediate, Low Effort)**
   **Why**: Startup errors are likely logged by Serilog. Check logs for unhandled exceptions or warnings.
   **How**:
   - Run the app in Debug mode and check console output or Serilog sinks (e.g., file/console).
   - Review log files (e.g., in `logs/` or configured output path).
   - Add temporary debug logging in `App.OnStartup()` and `RegisterTypes()` (e.g., `Log.Debug("Starting DI registration...")`).
   - Ensure Serilog is configured correctly in `appsettings.json`.
   **Expected Outcome**: Surface hidden exceptions (e.g., "Failed to resolve IGrokSupercomputer").
   **Time**: 15-30 minutes.

2. **Validate DI Container and Registrations (Next, Medium Effort)**
   **Why**: DI failures are common in Prism apps and might not surface in unit tests.
   **How**:
   - Add a try-catch in `RegisterTypes()` around each registration block and log exceptions.
   - Use the existing `ValidateContainerRegistrations()` method in `App.xaml.cs` (around line 738) to check for missing dependencies.
   - Temporarily disable external services (e.g., comment out AI/QuickBooks registrations) to isolate if they're causing issues.
   - Run the app with minimal config (e.g., set `WILEY_WIDGET_TESTMODE=1` to use in-memory DB and fakes).
   **Expected Outcome**: Identify failing registrations (e.g., "Cannot resolve IAIService").
   **Time**: 30-60 minutes.

3. **Debug Startup Sequence with Breakpoints/Exception Handling (If Logging Fails, Medium Effort)**
   **Why**: If logs are silent, attach a debugger to catch exceptions live.
   **How**:
   - Set breakpoints in `App.OnStartup()`, `RegisterTypes()`, and `MunicipalAccountViewModel.InitializeAsync()`.
   - Enable "Break on all exceptions" in VS Code (Debug > Exceptions > Check "Common Language Runtime Exceptions").
   - Run the app in Debug mode and note where it crashes.
   - If it crashes before UI shows, check the Output window for unhandled exceptions.
   **Expected Outcome**: Pinpoint the exact line/method throwing the exception.
   **Time**: 30-60 minutes.

4. **Isolate Components with Minimal App Mode (If Needed, Medium Effort)**
   **Why**: Test startup without heavy dependencies.
   **How**:
   - Create a "minimal mode" flag (e.g., `WILEY_WIDGET_MINIMAL=1`) that skips DB preflight, external services, and loads a simple view (e.g., just `MainWindow` without `MunicipalAccountView`).
   - If minimal mode works, gradually re-enable components to isolate the culprit.
   - Use the existing test-mode logic as a base.
   **Expected Outcome**: Narrow down if errors are in core WPF/Prism vs. business logic.
   **Time**: 60-90 minutes.

5. **Check Environment and Configuration (Parallel/As Needed)**
   **Why**: Misconfigurations can cause silent failures.
   **How**:
   - Validate `appsettings.json` (e.g., connection strings, Syncfusion keys).
   - Ensure SQL Server is running (or switch to SQLite for testing).
   - Test with clean environment (no custom env vars).
   **Expected Outcome**: Rule out config issues.
   **Time**: 15-30 minutes.

6. **Implement Fixes and Retest (Iterative)**
   **Why**: Once diagnosed, apply targeted fixes (e.g., add try-catch, fix registrations).
   **How**: Based on findings, update code (e.g., improve exception handling in startup).
   **Expected Outcome**: Stable startup.
   **Time**: Varies.

## Notes
- **Tracking**: Update the project todo list with progress on each step.
- **Tools**: Use VS Code debugger, Serilog logs, and PowerShell for environment testing.
- **Fallback**: If issues persist, consider enabling Application Insights for remote error tracking.
- **Contact**: If you encounter specific errors during these steps, share logs or screenshots for refined advice.

Save this as `docs/startup-diagnosis-plan.md` in your project for reference. Let me know if you'd like me to implement any step or adjust the plan! 🚀