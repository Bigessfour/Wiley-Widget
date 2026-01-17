# Intake
Confirmed lazy loading and perf counter requirements. Resolved initial DI and property name issues.

# Recon
Found remaining CS1061 errors in `MainForm.UI.cs` related to `DockStateChangeEventArgs`.
Syncfusion docs (via Assistant) were inconclusive on properties.

# Plan
1. Find exact structure of handlers in `MainForm.UI.cs` around line 3127.
2. Use `dotnet-doc` or similar if available, or just trial and error with common names if search fails.
3. Fix handlers to use correct event args properties.

# Implement
- [ ] Fix `DockStateChanged`
- [ ] Fix `DockVisibilityChanged`
- [ ] Fix `DockActivationChanged`

# Validate
- [ ] Run `dotnet build`
- [ ] Check Problems panel
