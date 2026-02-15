# Navigation Debugging Flow Diagram

```
                    USER CLICKS RIBBON BUTTON
                              |
                              v
┌─────────────────────────────────────────────────────────────────┐
│                     🔴 BREAKPOINT 1 (BP1)                       │
│              ShowPanel<TPanel>() entry point                     │
│                                                                  │
│  Check:                                                          │
│    - panelName value                                             │
│    - panelType                                                   │
│    - preferredStyle (Left/Right/etc)                             │
│                                                                  │
│  Optional: Uncomment to break here                               │
└─────────────────────────────────────────────────────────────────┘
                              |
                              v
                  resolvedPanelName calculated
                              |
                              v
┌─────────────────────────────────────────────────────────────────┐
│                     🔴 BREAKPOINT 2 (BP2)                       │
│          ExecuteDockedNavigation() orchestrator                  │
│                                                                  │
│  Check:                                                          │
│    - IsDisposed (should be False)                                │
│    - InvokeRequired (should be False)                            │
│    - navigationTarget value                                      │
│                                                                  │
│  Optional: Uncomment to break here                               │
└─────────────────────────────────────────────────────────────────┘
                              |
                              v
                  InvokeRequired check
                              |
         ┌────────────────────┴────────────────────┐
         │                                         │
    InvokeRequired?                          InvokeRequired?
       YES                                        NO
         │                                         │
         v                                         v
    BeginInvoke to UI thread              EnsurePanelNavigatorInitialized()
    (marshals call)                                │
         │                                         v
         └──────────────────────┬──────────────────┘
                                v
                    Check: _panelNavigator != null?
                                |
         ┌──────────────────────┴──────────────────────┐
         │                                             │
    _panelNavigator                              _panelNavigator
       == null                                      != null
         │                                             │
         v                                             v
┌─────────────────────────────────────┐    ForceMarkDockingReadyIfOperational()
│  🔴 BREAKPOINT 3 (BP3) - CRITICAL  │               |
│    PanelNavigator is NULL           │               v
│                                     │    Check: IsDockingManagerReady()?
│  ALWAYS BREAKS - Critical Error!    │               |
│                                     │               v
│  Check:                             │    ┌─────────────────────────────┐
│    - _dockingManager != null        │    │  🔴 BREAKPOINT 4 (BP4)     │
│    - _serviceProvider != null       │    │  Before navigation action   │
│    - _centralDocumentPanel != null  │    │                             │
│                                     │    │  Check:                     │
│  This is a critical initialization  │    │    - readiness = true?      │
│  failure. Navigation cannot work!   │    │    - _panelNavigator ready  │
│                                     │    │                             │
└─────────────────────────────────────┘    │  Optional: Uncomment        │
         │                                 └─────────────────────────────┘
         v                                              |
  RecoverDockingState()                                 v
         │                                    Execute navigationAction()
         v                                    (calls PanelNavigationService)
   return false                                         |
   (navigation failed)                                  v
                                         EnsureDockingSurfaceVisible()
                                                      |
                                                      v
                                         IsNavigationTargetActive()?
                                                      |
         ┌────────────────────────────────────────────┴────────────────────┐
         │                                                                  │
    Target Active?                                                   Target Active?
       YES                                                               NO
         │                                                                  │
         v                                                                  v
┌────────────────────────────────────┐         ┌─────────────────────────────────────────┐
│  🔴 BREAKPOINT 5 (BP5)            │         │   🔴 BREAKPOINT 6 (BP6) - CRITICAL    │
│  Navigation Success                │         │   Navigation Failed                     │
│                                    │         │                                         │
│  Just logs - NO BREAK              │         │   ALWAYS BREAKS                         │
│  Panel activated successfully!     │         │                                         │
│                                    │         │   Check:                                │
└────────────────────────────────────┘         │     - navigationTarget value            │
         │                                     │     - activePanelName (what IS active?) │
         v                                     │     - Compare: why don't they match?    │
    return true                                │                                         │
    (success!)                                 │   This means navigation executed but    │
                                               │   panel didn't become active.           │
                                               └─────────────────────────────────────────┘
                                                              |
                                                              v
                                                   RecoverDockingState()
                                                              |
                                                              v
                                                        return false
                                                        (failed)


┌─────────────────────────────────────────────────────────────────────────────┐
│                   🔴 BREAKPOINT 7 (BP7) - CRITICAL                          │
│                   Exception During Navigation                                │
│                                                                              │
│   ALWAYS BREAKS when exception occurs                                        │
│                                                                              │
│   Check:                                                                     │
│     - Exception type (ArgumentException, InvalidOperationException, etc)     │
│     - Exception message                                                      │
│     - Call stack (how did we get here?)                                      │
│     - Inner exception details                                                │
│                                                                              │
│   This catches ANY exception during the navigation flow                      │
└─────────────────────────────────────────────────────────────────────────────┘


═══════════════════════════════════════════════════════════════════════════════
                              BREAKPOINT SUMMARY
═══════════════════════════════════════════════════════════════════════════════

┌─────────┬────────────────────────────────┬──────────────┬─────────────────┐
│ BP ID   │ Location                       │ Always On?   │ Purpose         │
├─────────┼────────────────────────────────┼──────────────┼─────────────────┤
│ BP1     │ ShowPanel entry                │ ⏸️ Optional  │ Verify called   │
│ BP2     │ ExecuteDockedNavigation start  │ ⏸️ Optional  │ Check state     │
│ BP3     │ _panelNavigator == null        │ ✅ CRITICAL  │ Init failure    │
│ BP4     │ Before navigationAction()      │ ⏸️ Optional  │ Pre-execute     │
│ BP5     │ Navigation success             │ Never        │ Just logs       │
│ BP6     │ Navigation failed              │ ✅ CRITICAL  │ Panel not active│
│ BP7     │ Exception handler              │ ✅ CRITICAL  │ Any exception   │
└─────────┴────────────────────────────────┴──────────────┴─────────────────┘


═══════════════════════════════════════════════════════════════════════════════
                           DECISION FLOWCHART
═══════════════════════════════════════════════════════════════════════════════

START: User clicks Ribbon button
        ↓
    [BP1] ShowPanel called?
        ↓
    YES → Continue
    NO  → Check Ribbon event wiring
        ↓
    [BP2] ExecuteDockedNavigation reached?
        ↓
    YES → Continue
    NO  → Check ShowPanel logic
        ↓
    [BP3] Is PanelNavigator null?
        ↓
    YES → ❌ CRITICAL: Fix initialization
    NO  → Continue
        ↓
    [BP4] About to execute navigation
        ↓
    Navigation Action Executed
        ↓
    [BP6] Is target panel active?
        ↓
    YES → ✅ SUCCESS! (BP5 logs)
    NO  → ❌ CRITICAL: Check PanelNavigationService
        ↓
    [BP7] Did exception occur?
        ↓
    YES → ❌ CRITICAL: Check exception details
    NO  → Check navigation logic


═══════════════════════════════════════════════════════════════════════════════
                            COMMON ERROR PATHS
═══════════════════════════════════════════════════════════════════════════════

ERROR PATH 1: Initialization Failure
    BP1 ✅ → BP2 ✅ → BP3 ❌ (PanelNavigator null)

    DIAGNOSIS:
    - Check: _dockingManager created?
    - Check: _serviceProvider available?
    - Check: Form.OnLoad/OnShown sequence

    FIX:
    - Ensure EnsurePanelNavigatorInitialized() is called
    - Verify InitializeSyncfusionDocking() completed
    - Check startup order in MainForm


ERROR PATH 2: Navigation Execution Failure
    BP1 ✅ → BP2 ✅ → BP3 ✅ → BP4 ✅ → BP6 ❌ (Panel not active)

    DIAGNOSIS:
    - Check: navigationTarget vs activePanelName
    - Check: Panel registered in DockingManager?
    - Check: Panel visibility

    FIX:
    - Verify PanelNavigationService.ShowPanel logic
    - Check DockingManager.ActivateControl() called
    - Ensure panel is properly docked


ERROR PATH 3: Exception During Navigation
    BP1 ✅ → BP2 ✅ → BP3 ✅ → BP7 ❌ (Exception thrown)

    DIAGNOSIS:
    - Check exception type and message
    - Check call stack
    - Check what was happening when exception occurred

    FIX:
    - Fix the specific exception cause
    - Add try-catch if needed
    - Validate inputs before operation


ERROR PATH 4: No Breakpoints Hit
    (User clicks button but BP1 never hits)

    DIAGNOSIS:
    - Ribbon button Click event not wired?
    - Button event calls wrong method?
    - Button disabled/hidden?

    FIX:
    - Check RibbonFactory button creation
    - Verify event handler attachment
    - Test button.Enabled and button.Visible


═══════════════════════════════════════════════════════════════════════════════
                         VISUAL STUDIO WINDOWS LAYOUT
═══════════════════════════════════════════════════════════════════════════════

When debugging, arrange your windows like this:

┌──────────────────────────────────────────────────────────────────────┐
│                         Main Code Window                              │
│  (src/WileyWidget.WinForms/Forms/MainForm/MainForm.Navigation.cs)    │
│                                                                        │
│  Shows breakpoint line with yellow arrow → indicating current line    │
└──────────────────────────────────────────────────────────────────────┘

┌────────────────────────────┬─────────────────────────────────────────┐
│      Locals Window         │         Watch Window                     │
│  (Ctrl+Alt+V, L)           │     (Ctrl+Alt+W, 1)                      │
│                            │                                          │
│  navigationTarget          │  ? _panelNavigator != null              │
│  _panelNavigator           │  ? _dockingManager != null              │
│  _dockingManager           │  ? this.IsDisposed                      │
│  readiness                 │  ? navigationTarget                     │
└────────────────────────────┴─────────────────────────────────────────┘

┌────────────────────────────┬─────────────────────────────────────────┐
│    Output Window           │       Immediate Window                   │
│  (Ctrl+Alt+O)              │     (Ctrl+Alt+I)                         │
│                            │                                          │
│  [BP1] ShowPanel called    │  ? NavigationDebugger.                  │
│  [BP2] ExecuteDockedNav    │    ValidateInfrastructure(this, ...)   │
│  [BP3] ❌ PanelNav NULL    │                                          │
└────────────────────────────┴─────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│                         Call Stack Window                             │
│                        (Ctrl+Alt+C)                                   │
│                                                                        │
│  MainForm.ExecuteDockedNavigation() ← You are here                    │
│  MainForm.ShowPanel<AccountsPanel>()                                  │
│  AccountsButton_Click()                                               │
│  RibbonControlAdv.OnClick()                                           │
└──────────────────────────────────────────────────────────────────────┘


═══════════════════════════════════════════════════════════════════════════════
                              QUICK ACTIONS
═══════════════════════════════════════════════════════════════════════════════

While stopped at breakpoint:

┌──────────────┬────────────────────────────────────────────────────────┐
│ Press        │ Action                                                 │
├──────────────┼────────────────────────────────────────────────────────┤
│ F10          │ Execute current line and move to next                  │
│ F11          │ Step into method call (go inside)                      │
│ Shift+F11    │ Step out of current method                             │
│ F5           │ Continue to next breakpoint                            │
│ Shift+F5     │ Stop debugging                                         │
│ Ctrl+Alt+I   │ Open Immediate Window (run code)                       │
│ Ctrl+Alt+O   │ Open Output Window (see logs)                          │
│ Ctrl+Alt+V,L │ Open Locals Window (see variables)                     │
│ Ctrl+Alt+W,1 │ Open Watch Window (monitor expressions)                │
│ Ctrl+Alt+C   │ Open Call Stack (see call hierarchy)                   │
└──────────────┴────────────────────────────────────────────────────────┘


═══════════════════════════════════════════════════════════════════════════════

For complete documentation, see:
  - README_BREAKPOINTS_INSTALLED.md (Quick start)
  - docs/DEBUG_NAVIGATION_BREAKPOINTS.md (Full reference)
  - scripts/Debug-Navigation.ps1 (Control script)

═══════════════════════════════════════════════════════════════════════════════
```
