# WileyWidget.Paneltest

**Panel Test Harness** — Isolated panel rendering and testing without launching the full application.

## Quick Start

```bash
# List available panels
WileyWidget.Paneltest.exe list

# Run WarRoomPanel with visual display
WileyWidget.Paneltest.exe warroom --show

# Run with dark theme
WileyWidget.Paneltest.exe warroom --theme Office2019Black --show

# Run multiple iterations
WileyWidget.Paneltest.exe warroom --iterations 5
```

## Features

✅ **Isolated Panel Rendering** — No full app launch, fast iteration  
✅ **Mock Data & Services** — Built-in Grok mock, theme fixtures, sample data  
✅ **Multi-Theme Testing** — Test across Office2019Colorful, Black, DarkGray, etc.  
✅ **STA Thread Support** — Proper WinForms threading via Xunit.StaFact  
✅ **Extensible Framework** — Add new panels by inheriting `BasePanelTestCase`  
✅ **JSON Report Generation** — Results saved to `Results/test-results.json`

## Project Structure

```
WileyWidget.Paneltest/
├── Program.cs                       Entry point, CLI orchestrator
├── TestCases/
│   ├── BasePanelTestCase.cs         Base class (inherit for new panels)
│   ├── WarRoomPanelTestCase.cs      WarRoomPanel test implementation
│   └── [NewPanelTestCase].cs        Add new panels here
├── Fixtures/
│   ├── PanelTestFixture.cs          DI setup and mock configuration
│   ├── MockDataGenerator.cs         Sample data for testing
│   └── ThemeTestFixture.cs          Theme variant testing
├── Helpers/
│   └── TestHelpers.cs               Reflection utils, STA runner
└── Results/
    └── test-results.json            Test report (generated)
```

## Usage

### Basic Rendering

```csharp
// In a test case (inherit BasePanelTestCase)
[StaFact]
public void MyPanel_Initializes_Successfully()
{
    RenderPanel(showForm: false);  // showForm: true opens window
    AssertPanelInitialized();
}
```

### With Mock Data

```csharp
protected override void InitializePanelData(UserControl panel)
{
    var scenarios = MockDataGenerator.GenerateSampleScenarios();
    // Load into ViewModel
}
```

### Accessing Panel State

```csharp
var viewModel = GetViewModel();
var chart = GetPanelField("_revenueChart");
var control = GetPanelProperty("SomeProperty");
```

### Testing Multiple Themes

```csharp
[Theory]
[InlineData("Office2019Colorful")]
[InlineData("Office2019Black")]
public void Panel_RendersCorrectly_AcrossThemes(string themeName)
{
    var themeMock = ThemeTestFixture.CreateThemeMock(themeName);
    Fixture.AddMockService(themeMock);
    RenderPanel();
}
```

## Adding a New Panel Test

### 1. Create Test Case Class

```csharp
// TestCases/MyPanelTestCase.cs
public class MyPanelTestCase : BasePanelTestCase
{
    protected override string GetPanelName() => "MyPanel";
    
    protected override UserControl CreatePanel(IServiceProvider provider)
    {
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var logger = provider.GetRequiredService<ILoggerFactory>()
            .CreateLogger<ScopedPanelBase<MyViewModel>>();
        return new MyPanel(scopeFactory, (ILogger<ScopedPanelBase<object>>)(object)logger);
    }

    [StaFact]
    public void MyPanel_Initializes_Successfully()
    {
        RenderPanel();
        AssertPanelInitialized();
    }
}
```

### 2. Register in Program.cs

```csharp
private static readonly Dictionary<string, Type> RegisteredPanelTests = new()
{
    ["warroom"] = typeof(WarRoomPanelTestCase),
    ["mypanel"] = typeof(MyPanelTestCase),  // Add this
};
```

### 3. Run

```bash
WileyWidget.Paneltest.exe mypanel --show
```

## API Reference

### BasePanelTestCase

| Method | Purpose |
|--------|---------|
| `RenderPanel(bool showForm)` | Create and initialize panel |
| `GetViewModel()` | Access panel's ViewModel |
| `GetPanelField(string name)` | Access private field via reflection |
| `GetPanelProperty(string name)` | Access private property via reflection |
| `AssertPanelInitialized()` | Verify panel loaded successfully |
| `AssertControlExists(string name)` | Verify control exists |
| `ProcessUIEvents(int ms)` | Allow async operations to complete |

### Fixtures

| Class | Purpose |
|-------|---------|
| `PanelTestFixture` | Basic DI setup, mock service registration |
| `WarRoomPanelTestFixture` | Pre-configured mocks for WarRoom (Grok, Theme) |
| `ThemeTestFixture` | Multi-theme testing utilities |

### Helpers

| Class | Purpose |
|-------|---------|
| `TestScopeFactory` | Wraps IServiceScopeFactory for panels |
| `PanelReflectionHelper` | Reflection access to private fields/properties |
| `StaThreadRunner` | Run code on STA thread (for WinForms) |

### Mock Data

```csharp
// Generate sample data
var scenarios = MockDataGenerator.GenerateSampleScenarios();
var activities = MockDataGenerator.GenerateActivityLogs(50);
var revenue = MockDataGenerator.GenerateMonthlyRevenue();
var expenses = MockDataGenerator.GenerateExpensesByCategory();
var accounts = MockDataGenerator.GenerateSampleAccounts(15);
var audits = MockDataGenerator.GenerateAuditLogs(100);
```

## Styling & Theming

All panels use **SfSkinManager** for theming. Theme is automatically applied via `Fixture.ThemeServiceMock`:

```csharp
var fixture = new PanelTestFixture();
var darkMock = ThemeTestFixture.CreateThemeMock("Office2019Black");
fixture.AddMockService(darkMock);
```

**Important:** No manual `BackColor` assignments allowed. Theme cascade handles all colors.

## Test Results

Test results are saved to `Results/test-results.json`:

```json
{
  "generatedUtc": "2026-01-27T12:34:56Z",
  "results": [
    {
      "testType": "WarRoomPanelTestCase",
      "status": "PASSED",
      "message": "Panel rendered and initialized successfully.",
      "startTime": "2026-01-27T12:34:50Z",
      "duration": "00:00:03.2100000"
    }
  ],
  "summary": {
    "total": 1,
    "passed": 1,
    "failed": 0,
    "totalDuration": "00:00:03.2100000"
  }
}
```

## Troubleshooting

### "Services not available"
Ensure all required services are registered in your fixture. Check that IServiceScopeFactory is available.

### "Panel not rendered"
Call `RenderPanel()` before accessing `GetViewModel()` or controls. The method sets up the form and triggers DI resolution.

### "StaThreadRequired"
Tests must use `[StaFact]` attribute from `Xunit.StaFact` for WinForms UI tests.

### Chart/Grid not visible
Check that:
1. ViewModel is initialized (call `GetViewModel()`)
2. Mock data was loaded (call `InitializePanelData()`)
3. `ProcessUIEvents()` is called to allow rendering

## Best Practices

1. **Inherit BasePanelTestCase** — Provides common setup/teardown
2. **Use Fixtures** — Pre-configured mocks save setup time
3. **Test Themes** — Use `[Theory]` with theme variants
4. **Mock External Services** — Grok, QuickBooks, etc. via Moq
5. **Generate Sample Data** — Use MockDataGenerator for realistic testing
6. **Process UI Events** — Call `ProcessUIEvents()` after data loads

## Performance

- **Panel Load Time:** ~100-300ms (depends on ViewModel complexity)
- **Theme Switch:** ~50ms
- **Full Iteration (5 panels):** ~2-3 seconds

For baseline performance: run `WileyWidget.Paneltest.exe warroom --iterations 10` and check `test-results.json` durations.

## Integration with CI/CD

Run tests headless (no UI display):

```powershell
# PowerShell
& ".\tools\WileyWidget.Paneltest\bin\Debug\net10.0-windows\WileyWidget.Paneltest.exe" warroom --iterations 5

# Check exit code
if ($LASTEXITCODE -ne 0) { exit 1 }

# Parse results
$results = Get-Content Results/test-results.json | ConvertFrom-Json
if ($results.summary.failed -gt 0) { exit 1 }
```

## Contributing

To add a new panel test:

1. Create `TestCases/[PanelName]TestCase.cs` inheriting `BasePanelTestCase`
2. Implement `CreatePanel()` and `GetPanelName()`
3. Add `[StaFact]` test methods
4. Register in `Program.RegisteredPanelTests`
5. Run: `WileyWidget.Paneltest.exe list` to verify registration

## License

Same as WileyWidget main project.
