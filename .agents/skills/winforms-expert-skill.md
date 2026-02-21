# WinForms Expert Skill - Wiley Widget Implementation

**Agent:** WinForms Expert (`.vscode/Agents/WinFormsExpert.agent.md`)  
**Target Projects:** WileyWidget.WinForms (net10.0-windows)  
**Framework:** .NET 10 with Syncfusion Windows Forms Controls v32.x+

---

## Activation Context

This skill activates when:

- Working in `.cs` or `.designer.cs` files in the WinForms project
- Creating or modifying Forms, UserControls, or WinForms UI components
- Implementing data binding, MVVM patterns, or async event handlers
- Configuring Syncfusion controls (DockingManager, Ribbon, DataGrid, etc.)

---

## WinForms Expert Agent Skills

### 1. Designer File Validation

**Scope:** `.designer.cs` files and `InitializeComponent` methods

**Rules:**

- ✅ Simple control instantiation and property assignment only
- ✅ `SuspendLayout()` / `ResumeLayout()` for layout control
- ✅ Direct method calls (`BringToFront()`, `PerformLayout()`)
- ❌ NO control flow (`if`, `for`, `foreach`, `while`)
- ❌ NO ternary operators (`?:`) or null coalescing (`??`)
- ❌ NO lambdas or local functions
- ❌ NO collection expressions
- ❌ NO `nameof()` or complex expressions

**Example (✅ CORRECT):**

```csharp
private void InitializeComponent()
{
    _button1 = new Button();
    _label1 = new Label();
    components = new Container();

    ((ISupportInitialize)_button1).BeginInit();
    SuspendLayout();

    _button1.Location = new Point(10, 10);
    _button1.Size = new Size(100, 30);
    _button1.Text = "Click Me";
    _button1.Click += Button1_Click;  // ✅ OK - method reference

    Controls.Add(_button1);
    ClientSize = new Size(200, 100);
    Name = "MyForm";

    ResumeLayout(false);
    PerformLayout();
}
```

---

### 2. Modern C# Best Practices (Regular .cs Files)

**Scope:** Event handlers, business logic, Form/UserControl implementations

**Apply Modern C# 11-14 features:**

- ✅ Target-typed `new()` expressions: `Button button = new();`
- ✅ Nullable reference types: `object? sender`, `EventHandler?`
- ✅ Pattern matching: `if (sender is not Button button) return;`
- ✅ Switch expressions: `color = state switch { ... }`
- ✅ File-scoped namespaces: `namespace WileyWidget.WinForms.Forms;`
- ✅ Global using directives (assumed active)
- ✅ Argument validation with throw helpers: `ArgumentNullException.ThrowIfNull(control);`

**Example (✅ CORRECT):**

```csharp
namespace WileyWidget.WinForms.Forms;

public partial class MyForm : Form
{
    private void Button_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        var state = DetermineState();
        var color = state switch
        {
            ButtonState.Normal => SystemColors.Control,
            ButtonState.Hover => SystemColors.ControlLight,
            _ => SystemColors.Control
        };

        button.BackColor = color;
    }
}
```

---

### 3. Syncfusion Control Patterns

**Scope:** DockingManager, Ribbon, DataGrid, TreeGrid, etc.

**Critical Rules (from SfSkinManager enforcement):**

- ✅ **Single Theme Source:** `SfSkinManager` is authoritative
- ✅ **Theme Cascade:** Parent themes automatically cascade to children
- ✅ **Per-Control Theme (when needed):** `SfSkinManager.SetVisualStyle(control, themeName)`
- ✅ **Global Theme (Program.cs):** `SfSkinManager.ApplicationVisualTheme = themeName`
- ❌ **NO manual colors** (except semantic status: `Color.Red`, `Color.Green`, `Color.Orange`)
- ❌ **NO custom color properties or dictionaries**
- ❌ **NO competing theme managers**

**DockingManager Example:**

```csharp
// In Program.cs (startup)
var themeAssembly = themeService.ResolveAssembly("Office2019Colorful");
SkinManager.LoadAssembly(themeAssembly);
SfSkinManager.ApplicationVisualTheme = "Office2019Colorful";

// In Form (automatically cascades)
// DockingManager and child controls inherit theme
```

---

### 4. Data Binding & MVVM (.NET 8+)

**Pattern:** BindingSource + INotifyPropertyChanged + MVVM CommunityToolkit

**Key Bindings:**

- ✅ Create `.datasource` file in `Properties/DataSources/` for ViewModel discovery
- ✅ Use `BindingSource` as mediator between View and ViewModel
- ✅ Bind properties: `textBox.DataBindings.Add(new Binding("Text", bindingSource, "PropertyName", true))`
- ✅ Bind commands (new in .NET 8+): `button.DataBindings.Add(new Binding("Command", bindingSource, "CommandName", true))`
- ✅ Use `Parse` and `Format` events for custom value conversion (IValueConverter workaround)

**Example:**

```csharp
private BindingSource _mainViewModelBindingSource;

private void InitializeComponent()
{
    _mainViewModelBindingSource = new BindingSource(components);
    _mainViewModelBindingSource.DataSource = typeof(MyApp.ViewModels.MainViewModel);

    // Bind property
    _txtName.DataBindings.Add(
        new Binding("Text", _mainViewModelBindingSource, "PersonName", true));

    // Bind command
    _btnSave.DataBindings.Add(
        new Binding("Command", _mainViewModelBindingSource, "SaveCommand", true));
    _btnSave.CommandParameter = "Person";
}
```

---

### 5. Async Patterns (.NET 9+)

**Pattern:** `Control.InvokeAsync` for UI thread marshaling

**Rule Selection:**

- `InvokeAsync(Action)` - Sync action, no return
- `InvokeAsync(Func<T>)` - Sync function, returns T
- `InvokeAsync(Func<CancellationToken, ValueTask>)` - Async, no return
- `InvokeAsync<T>(Func<CancellationToken, ValueTask<T>>)` - Async, returns T

**Critical:** Avoid fire-and-forget patterns; always await the returned Task.

**Example (✅ CORRECT):**

```csharp
private async void Button_Click(object? sender, EventArgs e)
{
    try
    {
        // Async work + return result
        var result = await InvokeAsync(
            async (ct) => await LoadDataAsync(ct),
            CancellationToken.None);

        _label.Text = result;
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}");
    }
}

public async Task ShowFormAsync()
{
    using var form = new MyDialog();
    await form.ShowAsync();  // Completes when form closes
}
```

---

### 6. Exception Handling in Async Event Handlers

**Rule:** ALWAYS nest `await` calls in `try/catch` in async void event handlers.

**Critical:** Unhandled exceptions in async void handlers crash the process!

**Example (✅ CORRECT):**

```csharp
private async void Form_Load(object? sender, EventArgs e)
{
    try
    {
        await InitializeAsync();
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Initialization failed: {ex.Message}");
        // Optionally close form on fatal error
    }
}

private async Task InitializeAsync()
{
    // Async work here - exceptions propagate to caller
}
```

---

### 7. Property Pattern Anti-Patterns

**CRITICAL - Common Bug Source!**

| Pattern                | Behavior                          | Result                    |
| ---------------------- | --------------------------------- | ------------------------- |
| `=> new Type()`        | Creates NEW instance EVERY access | ⚠️ MEMORY LEAK            |
| `{ get; } = new()`     | Creates ONCE at construction      | ✅ CORRECT                |
| `=> _field ?? Default` | Dynamic/computed value            | ✅ CORRECT if intentional |

**Example (❌ WRONG - Memory Leak):**

```csharp
// Creates new Brush EVERY access!
public Brush BackgroundBrush => new SolidBrush(BackColor);
```

**Example (✅ CORRECT - Cached):**

```csharp
// Created once, reused
public Brush BackgroundBrush { get; } = new SolidBrush(Color.White);
```

---

### 8. HighDPI & DarkMode Configuration

**In Program.cs:**

```csharp
// Enable DarkMode (.NET 9+)
Application.SetColorMode(SystemColorMode.System);  // or Dark, Classic

// Set HighDPI mode (SystemAware is standard)
Application.SetHighDpiMode(HighDpiMode.SystemAware);

// For multi-monitor HighDPI scenarios, use:
// Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
```

---

### 9. Designer Compatibility

**When to Use Designer:**

- ✅ Simple form layouts with standard controls
- ✅ Property configuration that doesn't require logic
- ✅ Control docking/anchoring setup

**When to Avoid Designer:**

- ❌ Complex dynamic layouts
- ❌ Conditional control creation
- ❌ Event binding to lambdas
- ❌ Advanced Syncfusion configuration (use factories instead)

**Wiley Widget Pattern:** Use `DockingHostFactory` and similar factories for complex component creation instead of Designer.

---

### 10. Using Statements for Modal Forms

**Rule:** Always use `using` for Forms to ensure cleanup.

**Example (✅ CORRECT):**

```csharp
private void OpenOptionsDialog()
{
    using var dlg = new OptionsDialog();
    if (dlg.ShowDialog() == DialogResult.OK)
    {
        // Apply settings
    }
    // Form automatically disposed
}
```

---

## Integration with Wiley Widget Project Standards

### Theme Management (SfSkinManager Authority)

- See `.vscode/copilot-instructions.md` → "🎨 SYNCFUSION SFSKINMANAGER THEME ENFORCEMENT"
- See `src/WileyWidget.WinForms/Themes/ThemeColors.cs` for implementation

### Docking Architecture

- See `src/WileyWidget.WinForms/Factories/DockingHostFactory.cs`
- See `src/WileyWidget.WinForms/Forms/MainForm/MainForm.Docking.cs`

### Syncfusion Control Factory

- See `src/WileyWidget.WinForms/Factories/SyncfusionControlFactory.cs`
- MANDATORY: Use factory for all Syncfusion control creation

### Async Initialization Pattern

- See `.vscode/rules/async-initialization-pattern.md`
- Synchronous `Initialize()` only; heavy init via `IAsyncInitializable.InitializeAsync()`

---

## Copilot Chat Invocation

**To use this agent in Copilot Chat:**

1. Open Copilot Chat (`Ctrl+Shift+I` in VS Code)
2. Type: `@WinForms-Expert` followed by your question
3. Examples:
   - `@WinForms-Expert How should I structure a new UserControl?`
   - `@WinForms-Expert Review my InitializeComponent for designer compliance`
   - `@WinForms-Expert Help me implement async data loading in this form`

---

## Validation Checklist

Before committing WinForms changes:

- [ ] No control flow (`if`, `for`, etc.) in `InitializeComponent`
- [ ] No lambdas in `InitializeComponent` event binding
- [ ] Modern C# applied to `.cs` files (not `.designer.cs`)
- [ ] Nullable types on event handlers: `object? sender`
- [ ] All `await` calls in async void handlers wrapped in `try/catch`
- [ ] No `BackColor`/`ForeColor` assignments (except semantic status colors)
- [ ] SfSkinManager used for all theme application
- [ ] Modal Forms use `using` statement
- [ ] No `.Result` or `.Wait()` on Task/ValueTask
- [ ] Syncfusion controls created via factory (`SyncfusionControlFactory`)
- [ ] Build passes without analyzer warnings

---

## References

- **WinForms Expert Agent:** `.vscode/Agents/WinFormsExpert.agent.md`
- **Syncfusion WinForms Docs:** https://help.syncfusion.com/windowsforms/overview
- **Project Copilot Instructions:** `.vscode/copilot-instructions.md`
- **Approved Workflow:** `.vscode/approved-workflow.md`
- **C# Best Practices:** `.vscode/c-best-practices.md`
