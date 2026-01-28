# UI Components Architecture

**Status**: Active on "polish" branch | **Last Updated**: 2026-01-25

## Overview

Wiley-Widget uses a **N-tier MVVM architecture** with Syncfusion Windows Forms components. All theming flows through `SfSkinManager` exclusively; no manual color assignments are permitted (except semantic status colors: `Color.Red`, `Color.Green`, `Color.Orange`).

## Core UI Layer Structure

```
src/WileyWidget.WinForms/
├── Forms/
│   ├── MainForm.cs                 # Primary application window
│   ├── RightPanel.cs               # Blazor WebView host for JARVIS chat
│   ├── SplashForm.cs               # Startup splash screen
│   └── ...
├── Controls/
│   ├── GradientPanelExt.cs         # Themed gradient panels
│   ├── ControlExtensions.cs        # Control theming utilities
│   └── ScopedPanelBase.cs          # Base for all scoped panels
├── UI/
│   ├── Factories/
│   │   ├── DockingFactoryBase.cs   # Base docking orchestration
│   │   ├── DockingInitializer.cs   # Docking layout restoration
│   │   ├── RibbonFactory.cs        # Ribbon initialization
│   │   ├── StatusBarFactory.cs     # Status bar setup
│   │   └── DashboardFactory.cs     # Dashboard panel creation
│   ├── Components/
│   │   ├── JARVISAssist.razor      # Blazor chat component
│   │   └── ...
│   └── Panels/
│       ├── DashboardPanel.cs       # Analytics & reports
│       ├── QuickBooksPanel.cs      # QuickBooks integration UI
│       ├── AccountsPanel.cs        # Accounts management
│       └── ActivityLogPanel.cs     # Activity/audit log
├── Docking/
│   ├── DockingManager.cs           # Syncfusion DockingManager wrapper
│   ├── DockingLayoutManager.cs     # Layout persistence
│   └── DockingLayoutRestorer.cs    # Async layout restoration
├── Themes/
│   ├── ThemeColors.cs              # SfSkinManager orchestrator (SINGLE SOURCE OF TRUTH)
│   └── ThemeService.cs             # IThemeService implementation
└── Services/
    ├── PanelNavigationService.cs   # Panel visibility/routing
    └── ...
```

---

## Panel Architecture

### **Base Pattern: ScopedPanelBase**

All scoped panels (Dashboard, QuickBooks, Accounts, etc.) inherit from `ScopedPanelBase`:

```csharp
public abstract class ScopedPanelBase : GradientPanelExt
{
    protected ScopedPanelBase(IServiceProvider serviceProvider,
                              IPanelNavigationService navigationService,
                              ILogger<ScopedPanelBase> logger)
    {
        _serviceProvider = serviceProvider;
        _navigationService = navigationService;
        _logger = logger;

        // Theme cascade: parent form's theme applies automatically
        SuspendLayout();
        try
        {
            // Initialize child controls before resuming
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    /// Called when panel becomes visible
    public virtual void OnActivate() { }

    /// Called when panel is hidden
    public virtual void OnDeactivate() { }
}
```

**Key Responsibilities:**

- ✅ Defer heavy initialization to `OnActivate()`
- ✅ Inherit theme from parent form (SfSkinManager cascade)
- ✅ Register with `IPanelNavigationService` for routing
- ✅ Implement `IAsyncInitializable` for async startup work after MainForm shown

### **DashboardPanel** (Analytics & Reporting)

**File:** `src/WileyWidget.WinForms/UI/Panels/DashboardPanel.cs`

**Components:**

- **SfDataGrid**: Budget data grid with sorting/filtering/export
- **Chart Controls**: Syncfusion SfChart (budget trends, expense breakdown)
- **KPI Displays**: Budget vs. actual, variance %
- **Period Selector**: Fiscal period (FY2025, etc.) dropdown

**Responsive Behavior:**

- `OnResize` recalculates grid column widths and chart bounds
- `TableLayoutPanel` with percentage-based row/column sizing
- Minimum width: 600px (triggers horizontal scrolling below)

**Theme Application:**

```csharp
public override void OnActivate()
{
    // Theme cascade from parent already applied
    // Controls inherit via SfSkinManager.SetVisualStyle()
    base.OnActivate();
}
```

### **QuickBooksPanel** (Integration UI)

**File:** `src/WileyWidget.WinForms/UI/Panels/QuickBooksPanel.cs`

**Components:**

- **QB Sync Status**: Connection status, last sync time
- **Import Dialog**: Select QBO entities (invoices, expenses, etc.)
- **Sync Log**: SfDataGrid with sync history
- **Error Display**: Rich text box for error details

**Features:**

- Async import with progress reporting
- Cancellation token support
- Audit trail for all sync events (via ActivityLogPanel)

### **RightPanel** (Blazor WebView - JARVIS Chat)

**File:** `src/WileyWidget.WinForms/Forms/RightPanel.cs`

**Architecture:**

```csharp
public class RightPanel : Form
{
    private WindowsFormsBlazorWebView _blazorWebView;
    private RootComponent _rootComponent;

    public RightPanel(IServiceProvider serviceProvider)
    {
        // 1. Create BlazorWebView
        _blazorWebView = new WindowsFormsBlazorWebView
        {
            Dock = DockStyle.Fill
        };

        // 2. Register Blazor DI services (WebView scope)
        _blazorWebView.Services.AddWindowsFormsBlazorWebView();
        _blazorWebView.Services.AddSyncfusionBlazor();
        _blazorWebView.Services.AddScoped<IUserContext, UserContext>();

        // 3. Set root component
        _blazorWebView.RootComponents.Add(new RootComponent
        {
            ComponentType = typeof(JARVISAssist),
            Selector = "#app"
        });
    }
}
```

**Blazor Component:** `src/WileyWidget.WinForms/UI/Components/JARVISAssist.razor`

```razor
@inject IUserContext UserContext
@inject IChatBridgeService ChatBridge

<div class="jarvis-container">
    <div class="chat-messages" @ref="messagesDiv">
        @foreach (var msg in messages)
        {
            @if (msg.IsUserMessage)
            {
                <div class="user-msg">@msg.Text</div>
            }
            else
            {
                <div class="bot-msg">@msg.Text</div>
                @if (msg.IsLoading)
                {
                    <div class="typing-indicator">
                        <span></span><span></span><span></span>
                    </div>
                }
            }
        }
    </div>
    <textarea @bind="userInput" placeholder="Ask JARVIS..." />
    <button @onclick="SendMessage">Send</button>
</div>

<style>
.typing-indicator {
    display: flex;
    gap: 4px;
}
.typing-indicator span {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: currentColor;
    animation: bounce 1.4s infinite;
}
.typing-indicator span:nth-child(2) { animation-delay: 0.2s; }
.typing-indicator span:nth-child(3) { animation-delay: 0.4s; }

@keyframes bounce {
    0%, 80%, 100% { opacity: 0.4; }
    40% { opacity: 1; }
}
</style>
```

**Lifecycle:**

1. `RightPanel.OnLoad` → Creates BlazorWebView
2. `JARVISAssist.OnInitialized()` → Injects `IUserContext`, subscribes to chat bridge
3. `SendMessage()` → Calls xAI Grok API via `IChatBridgeService`
4. Response flows back via `IChatBridgeService.OnMessageReceived` event

---

## Docking Manager (Layout & Paint)

### **Critical: Paint Exception Mitigation**

**Problem:** Syncfusion `DockHost.GetPaintInfo()` throws `ArgumentOutOfRangeException` when `DockingManager.Controls.Count == 0` during paint events (e.g., layout restoration, visibility toggles).

**Solution Pattern (DockingInitializer):**

```csharp
public class DockingInitializer
{
    public void InitializeDocking(SfDockingManager dockingManager, Form form)
    {
        // CRITICAL: Create panels BEFORE suspending layout
        var leftPanel = new GradientPanelExt { Name = "LeftPanel" };
        dockingManager.Controls.Add(leftPanel);  // ✅ Non-empty before suspend

        dockingManager.SuspendLayout();
        try
        {
            // Dock controls
            dockingManager.DockControl(leftPanel, form, DockingStyle.Left, 200);

            // Apply theme AFTER docking structure is set
            string themeName = themeService.GetCurrentTheme();
            ThemeColors.ApplyTheme(form, themeName);
        }
        finally
        {
            dockingManager.ResumeLayout(true);  // ✅ Paint fires with non-empty collection
        }
    }
}
```

**Tests:** See `tests/WileyWidget.WinForms.Tests/Unit/Forms/DockingTests.cs`

- `DockingManager_MaintainsNonEmptyChildCollection_PreventsPaintException()`
- `DockingInitializer_CreatesControlsBeforeSuspendingLayout_AvoidsPaintRaceCondition()`
- `RibbonFactory_EnsuresNonEmptyHeaderItems_PreventsPaintException()`
- `DockingManager_HandlesVisibilityToggle_MaintainsNonEmptyState()`

### **Layout Persistence (DockingLayoutManager)**

Docking layouts are saved/loaded asynchronously to support theme restoration:

```csharp
public class DockingLayoutManager : IDisposable
{
    // Synchronous save to file
    public void SaveDockingLayout(SfDockingManager dockingManager)
    {
        var state = dockingManager.GetDockingLayout();
        var compressed = GZipCompress(state);
        File.WriteAllBytes(_layoutPath, compressed);
    }

    // Asynchronous load from file
    public async Task LoadDockingLayoutAsync(SfDockingManager dockingManager)
    {
        var bytes = await File.ReadAllBytesAsync(_layoutPath);
        var state = GZipDecompress(bytes);
        dockingManager.SetDockingLayout(state);
    }
}
```

**Lifecycle:**

1. App startup → `DockingLayoutRestorer.RestoreAsync()` checks for saved layout
2. If found → Load asynchronously after MainForm is shown (prevents UI freeze)
3. If not found → Use default layout from `DockingFactory`
4. Before app exit → Save current layout via `DockingLayoutManager.SaveDockingLayout()`

---

## Theme System (SfSkinManager)

### **MANDATORY: Single Source of Truth**

All theming flows exclusively through `SfSkinManager`. Manual color assignments are **forbidden** except for semantic status colors.

**Authorized Locations:**

1. **Program.cs - Startup (ONLY place to call SkinManager.LoadAssembly):**

   ```csharp
   public static void InitializeTheme()
   {
       var themeName = themeService.GetCurrentTheme();  // e.g., "Office2019Colorful"
       var themeAssembly = themeService.ResolveAssembly(themeName);
       SkinManager.LoadAssembly(themeAssembly);
       SfSkinManager.ApplicationVisualTheme = themeName;
   }
   ```

2. **MainForm Constructor (apply theme to primary form):**

   ```csharp
   public partial class MainForm : Form
   {
       public MainForm(IThemeService themeService, ...)
       {
           var themeName = themeService.GetCurrentTheme();
           ThemeColors.ApplyTheme(this, themeName);  // Cascades to all children
       }
   }
   ```

3. **Dynamic Control Addition (when control created AFTER form load):**
   ```csharp
   var newControl = new SfDataGrid();
   SfSkinManager.SetVisualStyle(newControl, themeName);
   ```

### **ThemeColors.cs (Orchestrator)**

```csharp
public static class ThemeColors
{
    /// Single method for all theme application
    public static void ApplyTheme(Control control, string themeName)
    {
        // 1. Load theme assembly if not loaded
        var assembly = ResolveAssembly(themeName);
        SkinManager.LoadAssembly(assembly);

        // 2. Apply to form and cascade to children
        SfSkinManager.SetVisualStyle(control, themeName);

        // 3. Set ThemeName on all Syncfusion controls
        foreach (var child in GetAllControls(control))
        {
            if (child is SfDataGrid grid)
                grid.ThemeName = themeName;
            else if (child is SfChart chart)
                chart.ThemeName = themeName;
            // ... etc for all Syncfusion controls
        }
    }

    /// Theme switching at runtime
    public static void SwitchTheme(Form form, string newThemeName)
    {
        ApplyTheme(form, newThemeName);
        form.Refresh();
    }
}
```

### **Available Themes**

| Theme              | Assembly                              | UseCase                    |
| ------------------ | ------------------------------------- | -------------------------- |
| Office2019Colorful | Syncfusion.Office2019Theme.WinForms   | Default (modern, colorful) |
| Office2019Black    | Syncfusion.Office2019Theme.WinForms   | Dark mode alternative      |
| HighContrastBlack  | Syncfusion.HighContrastTheme.WinForms | Accessibility (dark)       |
| HighContrastWhite  | Syncfusion.HighContrastTheme.WinForms | Accessibility (light)      |

**FORBIDDEN Anti-Patterns:**

```csharp
// ❌ VIOLATION: Manual BackColor
myPanel.BackColor = Color.White;

// ❌ VIOLATION: Custom color properties
public static Color Background => Color.FromArgb(240, 240, 240);

// ❌ VIOLATION: Custom theme manager
public class CustomThemeManager { ... }

// ❌ EXCEPTION: Even for status, don't use custom colors
statusLabel.ForeColor = ThemeColors.ErrorColor;  // Use Color.Red instead
```

**ALLOWED Exception: Semantic Status Colors**

```csharp
// ✅ ALLOWED: Standard .NET color for semantic meaning
statusLabel.ForeColor = hasError ? Color.Red : Color.Green;
warningIcon.ForeColor = Color.Orange;
```

---

## Responsive Resizing

All major panels implement `OnResize` to maintain responsive behavior across DPI and screen size variations.

### **Pattern: Resize Handler**

```csharp
public class DashboardPanel : ScopedPanelBase
{
    private SfDataGrid _grid;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        // Recalculate grid column widths
        if (_grid != null && Width > 0)
        {
            var availableWidth = Width - 40;  // Padding
            _grid.Columns[0].Width = (int)(availableWidth * 0.4);
            _grid.Columns[1].Width = (int)(availableWidth * 0.3);
            _grid.Columns[2].Width = (int)(availableWidth * 0.3);
            _grid.Refresh();
        }

        // Reposition child controls
        if (_chartControl != null)
        {
            _chartControl.Left = 0;
            _chartControl.Top = _grid.Height + 10;
            _chartControl.Width = Width;
            _chartControl.Height = Height - _grid.Height - 10;
        }
    }
}
```

### **DPI Awareness**

Windows Forms DPI scaling is handled automatically via `System.Windows.Forms.AutoScaleMode`:

```csharp
public partial class MainForm : Form
{
    public MainForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;  // Scale based on DPI
        AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
    }
}
```

**Testing Responsiveness:**

- Windows 100% DPI (96 DPI): Standard
- Windows 125% DPI (120 DPI): Verify grid/panel rescaling
- Windows 150% DPI (144 DPI): Test on Surface/high-DPI monitors
- Resize window: Verify column widths adjust proportionally

---

## Chat UI Animations

### **Typing Indicator**

Located in: `src/WileyWidget.WinForms/UI/Components/JARVISAssist.razor`

**Implementation:**

```razor
<div class="typing-indicator" @if(isLoading)>
    <span></span>
    <span></span>
    <span></span>
</div>

<style>
.typing-indicator {
    display: flex;
    gap: 4px;
}

.typing-indicator span {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: currentColor;
    animation: bounce 1.4s infinite;
}

.typing-indicator span:nth-child(1) { animation-delay: 0s; }
.typing-indicator span:nth-child(2) { animation-delay: 0.2s; }
.typing-indicator span:nth-child(3) { animation-delay: 0.4s; }

@keyframes bounce {
    0%, 80%, 100% { opacity: 0.4; transform: translateY(0); }
    40% { opacity: 1; transform: translateY(-8px); }
}
</style>
```

**Performance:** CSS animations run at 60fps on GPU; no JavaScript overhead.

### **Message Fade-In**

```css
@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.chat-message {
  animation: fadeIn 0.3s ease-out;
}
```

### **Future Enhancements (Polish Branch)**

- Scroll-to-latest-message animation
- Code syntax highlighting with animation
- Markdown rendering with LaTeX equation support
- Attachment upload progress bar

---

## Testing & Validation

### **Unit Tests**

Located in: `tests/WileyWidget.WinForms.Tests/Unit/Forms/`

**Key Test Files:**
| File | Focus |
|------|-------|
| `DockingTests.cs` | Docking manager, layout persistence, paint bug mitigation |
| `MainFormTests.cs` | Form initialization, theme application, ribbon/status bar setup |
| `RibbonFactoryTests.cs` | Ribbon structure, tab creation, commands |
| `DashboardFactoryTests.cs` | Dashboard panel creation, grid binding |

### **UI Validation Checklist**

Before committing UI changes:

- [ ] **Theme Consistency**: All Syncfusion controls have `ThemeName` set
- [ ] **Paint Exceptions**: DockingManager.Controls.Count > 0 before paint events
- [ ] **Responsive Resizing**: Window resize maintains proportional layout
- [ ] **DPI Scaling**: Test at 100%, 125%, 150% DPI
- [ ] **Accessibility**: Semantic colors for status (not custom colors)
- [ ] **Animation Performance**: No jank at 60fps on standard hardware
- [ ] **Blazor Interop**: Chat messages update without flicker
- [ ] **Layout Persistence**: Saved/restored docking layouts match user expectations

### **Run Tests**

```powershell
# Run all UI tests
dotnet test tests/WileyWidget.WinForms.Tests/Unit/Forms/ --verbosity normal

# Run docking tests only
dotnet test tests/WileyWidget.WinForms.Tests/Unit/Forms/DockingTests.cs

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

---

## Troubleshooting

### **DockingManager Paint Exception**

**Error:** `ArgumentOutOfRangeException in Syncfusion.Windows.Forms.Tools.DockHost.GetPaintInfo()`

**Root Cause:** Empty `Controls` collection during paint event

**Fix:**

1. Verify panels are added before `SuspendLayout()`
2. Add fallback panel if needed (see `DockingInitializer.cs`)
3. Run `DockingTests.cs` to validate

### **Theme Not Applying to Controls**

**Symptom:** Some controls show old theme colors

**Root Cause:** Missing `ThemeName` property on Syncfusion control

**Fix:**

```csharp
// Ensure all Syncfusion controls have ThemeName set
foreach (var control in GetAllControls(form))
{
    if (control is SfDataGrid grid)
        grid.ThemeName = themeName;
    // ... repeat for other Syncfusion types
}
```

### **Responsive Layout Broken on Resize**

**Symptom:** Grid columns shrink to zero width

**Root Cause:** `OnResize` calculation uses `Width - 40` with negative results

**Fix:**
eme Not Applying to Controls**

**Symptom:** Some controls show old theme colors

**Root Cause:** Missing `ThemeName` property on Syncfusion control

**Fix:**

```csharp
// Ensure all Syncfusion controls have ThemeName set
foreach (var control in GetAllControls(form))
{
    if (control is SfDataGrid grid)
        grid.ThemeName = themeName;
    // ... repeat for other Syncfusion types
}
```

### **Responsive Layout Broken on Resize**

**Symptom:** Grid columns shrink to zero width

**Root Cause:** `OnResize` calculation uses `Width - 40` with negative results

**Fix:**

```csharp
protected override void OnResize(EventArgs e)
{
    base.OnResize(e);
    int availableWidth = Math.Max(Width - 40, 200);  // Minimum 200px
    _grid.Columns[0].Width = (int)(availableWidth * 0.4);
}
```

---

## References

- **Syncfusion WinForms**: <https://help.syncfusion.com/windowsforms/overview>
- **Blazor Integration**: [BLAZOR_INTEGRATION.md](BLAZOR_INTEGRATION.md)
- **Docking Manager API**: <https://help.syncfusion.com/windowsforms/docking-manager/getting-started>
- **Theme Management**: [Approved Workflow - Theme Enforcement](./.vscode/copilot-instructions.md#sfskinmanager-theme-enforcement)
