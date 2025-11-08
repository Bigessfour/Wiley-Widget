# AI-Powered E2E Testing Setup for Wiley Widget

## Overview

This guide sets up **Continue.dev** (LLM-powered code generation) + **FlaUI** (WPF UI automation) for AI-driven end-to-end testing of the Wiley Widget WPF application with Syncfusion controls.

### Architecture

- **Continue.dev**: VS Code extension for AI-assisted test generation using local LLMs
- **Ollama**: Local LLM runtime (free, open-source, runs models like CodeLlama/DeepSeek-Coder)
- **FlaUI**: .NET library for Windows UI automation (successor to TestStack.White)
- **xUnit**: Test framework (already in project)

### Why This Stack?

✅ **100% Open-Source**: No paid API keys required
✅ **Local Execution**: All LLM inference runs on your machine (privacy-first)
✅ **WPF Native**: FlaUI has excellent Syncfusion SfDataGrid support
✅ **AI-Augmented**: Continue.dev generates tests from natural language prompts

---

## Step 1: Install Continue.dev Extension

### Manual Installation

1. Open VS Code
2. Go to Extensions (Ctrl+Shift+X)
3. Search for **"Continue - Codestral, Claude, and more"**
4. Click **Install**
5. Reload VS Code

### Verify Installation

```powershell
# Check if Continue.dev is installed
code --list-extensions | Select-String "Continue"
```

Expected output: `Continue.continue`

---

## Step 2: Install Ollama for Local LLM

### Download and Install Ollama

1. **Download**: Visit [https://ollama.com/download](https://ollama.com/download)
2. **Install**: Run the Windows installer (`OllamaSetup.exe`)
3. **Verify**: Open PowerShell and run:

```powershell
ollama --version
```

Expected output: `ollama version is x.x.x`

### Download Recommended Models

For C# code generation, we'll use **DeepSeek-Coder** (best for enterprise C#):

```powershell
# Primary model: DeepSeek-Coder 6.7B (4.4GB download)
ollama pull deepseek-coder:6.7b-instruct

# Alternative: CodeLlama 7B (good for test generation)
ollama pull codellama:7b-instruct

# Verify models are downloaded
ollama list
```

**Note**: First download takes 5-15 minutes depending on connection.

### Test Ollama

```powershell
# Test code generation
ollama run deepseek-coder:6.7b-instruct "Write a C# xUnit test that asserts a list has 31 items"
```

---

## Step 3: Configure Continue.dev with Ollama

### Create Continue Configuration

1. Open Command Palette (Ctrl+Shift+P)
2. Search: **"Continue: Open Config"**
3. Replace `config.json` with:

```json
{
  "models": [
    {
      "title": "DeepSeek Coder (Local)",
      "provider": "ollama",
      "model": "deepseek-coder:6.7b-instruct",
      "apiBase": "http://localhost:11434"
    },
    {
      "title": "CodeLlama (Fallback)",
      "provider": "ollama",
      "model": "codellama:7b-instruct",
      "apiBase": "http://localhost:11434"
    }
  ],
  "tabAutocompleteModel": {
    "title": "DeepSeek Coder",
    "provider": "ollama",
    "model": "deepseek-coder:6.7b-instruct"
  },
  "slashCommands": [
    {
      "name": "edit",
      "description": "Edit selected code"
    },
    {
      "name": "comment",
      "description": "Write comments for code"
    },
    {
      "name": "test",
      "description": "Generate unit tests"
    }
  ],
  "contextProviders": [
    {
      "name": "diff",
      "params": {}
    },
    {
      "name": "open",
      "params": {}
    },
    {
      "name": "terminal",
      "params": {}
    }
  ],
  "allowAnonymousTelemetry": false,
  "embeddingsProvider": {
    "provider": "ollama",
    "model": "nomic-embed-text",
    "apiBase": "http://localhost:11434"
  }
}
```

### Download Embeddings Model (for context understanding)

```powershell
ollama pull nomic-embed-text
```

### Test Continue.dev

1. Open any C# file in VS Code
2. Press **Ctrl+I** (Continue inline chat)
3. Type: `"Generate a simple xUnit test that validates a list count"`
4. Press Enter

If successful, you'll see AI-generated code suggestions.

---

## Step 4: Install FlaUI NuGet Packages

### Update WileyWidget.Tests.csproj

Add FlaUI packages for WPF automation:

```powershell
# Navigate to test project
cd c:\Users\biges\Desktop\Wiley_Widget\WileyWidget.Tests

# Install FlaUI packages
dotnet add package FlaUI.Core --version 4.0.0
dotnet add package FlaUI.UIA3 --version 4.0.0
dotnet add package FlaUI.TestUtilities --version 4.0.0

# Restore and build
dotnet restore
dotnet build
```

### Verify Installation

Check `WileyWidget.Tests.csproj` contains:

```xml
<PackageReference Include="FlaUI.Core" Version="4.0.0" />
<PackageReference Include="FlaUI.UIA3" Version="4.0.0" />
<PackageReference Include="FlaUI.TestUtilities" Version="4.0.0" />
```

---

## Step 5: Create FlaUI Test Infrastructure

### Base Test Helper Class

Create `WileyWidget.Tests/E2E/WpfTestBase.cs`:

```csharp
using System;
using System.Diagnostics;
using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;

namespace WileyWidget.Tests.E2E;

/// <summary>
/// Base class for WPF E2E tests using FlaUI.
/// Handles application lifecycle and provides common helpers.
/// </summary>
public abstract class WpfTestBase : IDisposable
{
    protected Application? App { get; private set; }
    protected UIA3Automation Automation { get; }
    protected Window? MainWindow { get; private set; }

    protected WpfTestBase()
    {
        Automation = new UIA3Automation();
    }

    protected void LaunchApplication(string exePath, int timeoutSeconds = 30)
    {
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Application not found: {exePath}");
        }

        App = Application.Launch(exePath);
        App.WaitWhileBusy(TimeSpan.FromSeconds(timeoutSeconds));

        MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(timeoutSeconds));
        Assert.NotNull(MainWindow);
    }

    protected AutomationElement? FindElementByAutomationId(string automationId)
    {
        return MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
    }

    protected AutomationElement? FindElementByName(string name)
    {
        return MainWindow?.FindFirstDescendant(cf => cf.ByName(name));
    }

    protected AutomationElement? FindElementByClassName(string className)
    {
        return MainWindow?.FindFirstDescendant(cf => cf.ByClassName(className));
    }

    public void Dispose()
    {
        MainWindow?.Close();
        App?.Close();
        App?.Dispose();
        Automation.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

### Syncfusion SfDataGrid Helper

Create `WileyWidget.Tests/E2E/SyncfusionHelpers.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace WileyWidget.Tests.E2E;

/// <summary>
/// Helper methods for interacting with Syncfusion SfDataGrid controls via FlaUI.
/// </summary>
public static class SyncfusionHelpers
{
    /// <summary>
    /// Gets the row count from a Syncfusion SfDataGrid.
    /// </summary>
    public static int GetDataGridRowCount(AutomationElement dataGrid)
    {
        if (dataGrid == null) throw new ArgumentNullException(nameof(dataGrid));

        // SfDataGrid exposes rows as a Table pattern
        var rows = dataGrid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
        return rows.Length;
    }

    /// <summary>
    /// Gets all visible row elements from the data grid.
    /// </summary>
    public static AutomationElement[] GetAllRows(AutomationElement dataGrid)
    {
        if (dataGrid == null) throw new ArgumentNullException(nameof(dataGrid));

        return dataGrid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
    }

    /// <summary>
    /// Gets cell text from a specific row and column index.
    /// </summary>
    public static string? GetCellText(AutomationElement row, int columnIndex)
    {
        if (row == null) throw new ArgumentNullException(nameof(row));

        var cells = row.FindAllChildren(cf => cf.ByControlType(ControlType.Text));
        if (columnIndex < 0 || columnIndex >= cells.Length)
        {
            return null;
        }

        return cells[columnIndex].Name;
    }

    /// <summary>
    /// Gets all cell values from a specific column.
    /// </summary>
    public static List<string> GetColumnValues(AutomationElement dataGrid, int columnIndex)
    {
        var values = new List<string>();
        var rows = GetAllRows(dataGrid);

        foreach (var row in rows)
        {
            var cellText = GetCellText(row, columnIndex);
            if (!string.IsNullOrEmpty(cellText))
            {
                values.Add(cellText);
            }
        }

        return values;
    }

    /// <summary>
    /// Counts rows matching a specific filter condition.
    /// </summary>
    public static int CountRowsWhere(AutomationElement dataGrid, Func<AutomationElement, bool> predicate)
    {
        var rows = GetAllRows(dataGrid);
        return rows.Count(predicate);
    }

    /// <summary>
    /// Applies a filter to the data grid by typing in a filter textbox.
    /// </summary>
    public static void ApplyFilter(AutomationElement dataGrid, string filterText)
    {
        // Syncfusion filter boxes are typically TextBox controls above the grid
        var filterBox = dataGrid.Parent.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Edit).And(cf.ByName("Filter")));

        if (filterBox != null)
        {
            filterBox.AsTextBox().Text = filterText;
            System.Threading.Thread.Sleep(500); // Wait for filter to apply
        }
    }
}
```

---

## Step 6: Generate Sample E2E Test with Continue.dev

### Using Continue.dev to Generate Tests

1. **Create Test File**: `WileyWidget.Tests/E2E/MunicipalAccountViewE2ETests.cs`
2. **Open Continue Chat**: Press **Ctrl+L** (sidebar chat)
3. **Paste This Prompt**:

```
Generate a C# xUnit E2E test class for WPF application testing Municipal Account View with Syncfusion SfDataGrid.

Requirements:
- Inherit from WpfTestBase
- Test: Load Conservation Trust Fund (31 accounts) and verify row count = 31
- Test: Filter by Type="Bank" and verify 5 rows remain
- Test: Validate Type column distribution (Bank, Investment, Cash)
- Use FlaUI to locate SfDataGrid by AutomationId "MunicipalAccountsGrid"
- Use SyncfusionHelpers.GetDataGridRowCount() and GetColumnValues()
- Add [StaFact] attribute (WPF requires STA thread)
- Include disposal and timeout handling

Reference project structure:
- App path: bin/Debug/net9.0-windows/WileyWidget.exe
- Grid AutomationId: MunicipalAccountsGrid
- Type column index: 2 (0=Account, 1=Description, 2=Type)
```

4. **Review Generated Code** and apply it to your test file.

### Manual Implementation (If Continue Generates Incomplete Code)

Create `WileyWidget.Tests/E2E/MunicipalAccountViewE2ETests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using WileyWidget.Tests.Fixtures;
using Xunit;

namespace WileyWidget.Tests.E2E;

/// <summary>
/// E2E tests for Municipal Account View with Conservation Trust Fund data.
/// Validates Syncfusion SfDataGrid interactions via FlaUI.
/// </summary>
[Collection("Sequential")]
public class MunicipalAccountViewE2ETests : WpfTestBase
{
    private const string AppExePath = @"bin\Debug\net9.0-windows\WileyWidget.exe";
    private const string GridAutomationId = "MunicipalAccountsGrid";

    [StaFact]
    public void LoadConservationTrustFund_ShouldDisplay31Accounts()
    {
        // Arrange
        var exePath = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", AppExePath);
        LaunchApplication(exePath, timeoutSeconds: 30);

        // Act - Navigate to Municipal Accounts view
        var municipalButton = FindElementByAutomationId("MunicipalAccountsButton");
        municipalButton?.AsButton().Click();
        System.Threading.Thread.Sleep(2000); // Wait for grid to load

        var dataGrid = FindElementByAutomationId(GridAutomationId);
        dataGrid.Should().NotBeNull("Municipal Accounts grid should be visible");

        // Assert
        var rowCount = SyncfusionHelpers.GetDataGridRowCount(dataGrid!);
        rowCount.Should().Be(31, "Conservation Trust Fund should have 31 accounts");
    }

    [StaFact]
    public void FilterByTypeBank_ShouldDisplay5Rows()
    {
        // Arrange
        var exePath = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", AppExePath);
        LaunchApplication(exePath, timeoutSeconds: 30);

        var municipalButton = FindElementByAutomationId("MunicipalAccountsButton");
        municipalButton?.AsButton().Click();
        System.Threading.Thread.Sleep(2000);

        var dataGrid = FindElementByAutomationId(GridAutomationId);
        dataGrid.Should().NotBeNull();

        // Act - Apply filter to Type column
        SyncfusionHelpers.ApplyFilter(dataGrid!, "Bank");
        System.Threading.Thread.Sleep(1000);

        // Assert
        var rowCount = SyncfusionHelpers.GetDataGridRowCount(dataGrid!);
        rowCount.Should().Be(5, "Filtering Type='Bank' should show 5 accounts");
    }

    [StaFact]
    public void ValidateTypeColumnDistribution_ShouldMatchExpectedCounts()
    {
        // Arrange
        var exePath = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", AppExePath);
        LaunchApplication(exePath, timeoutSeconds: 30);

        var municipalButton = FindElementByAutomationId("MunicipalAccountsButton");
        municipalButton?.AsButton().Click();
        System.Threading.Thread.Sleep(2000);

        var dataGrid = FindElementByAutomationId(GridAutomationId);
        dataGrid.Should().NotBeNull();

        // Act - Get Type column values (column index 2)
        var typeValues = SyncfusionHelpers.GetColumnValues(dataGrid!, columnIndex: 2);

        // Assert
        var bankCount = typeValues.Count(t => t.Equals("Bank", StringComparison.OrdinalIgnoreCase));
        var investmentCount = typeValues.Count(t => t.Equals("Investment", StringComparison.OrdinalIgnoreCase));
        var cashCount = typeValues.Count(t => t.Equals("Cash", StringComparison.OrdinalIgnoreCase));

        bankCount.Should().Be(5, "Should have 5 Bank accounts");
        investmentCount.Should().BeGreaterThan(0, "Should have Investment accounts");
        cashCount.Should().BeGreaterThan(0, "Should have Cash accounts");

        (bankCount + investmentCount + cashCount).Should().Be(31,
            "Total type counts should equal 31 accounts");
    }

    [StaFact]
    public void GridLoad_ShouldCompleteWithin10Seconds()
    {
        // Arrange
        var exePath = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", AppExePath);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        LaunchApplication(exePath, timeoutSeconds: 30);
        var municipalButton = FindElementByAutomationId("MunicipalAccountsButton");
        municipalButton?.AsButton().Click();

        var dataGrid = FindElementByAutomationId(GridAutomationId);
        dataGrid.Should().NotBeNull();

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
            "Grid should load within 10 seconds for enterprise performance");
    }
}
```

---

## Step 7: Run E2E Tests

### Build and Run

```powershell
# Build application
dotnet build c:\Users\biges\Desktop\Wiley_Widget\WileyWidget.csproj -c Debug

# Run E2E tests
dotnet test c:\Users\biges\Desktop\Wiley_Widget\WileyWidget.Tests\WileyWidget.Tests.csproj `
    --filter "FullyQualifiedName~E2E" `
    --logger "console;verbosity=detailed"
```

### Expected Output

```
Starting test execution, please wait...
A total of 4 test files matched the specified pattern.
  Passed LoadConservationTrustFund_ShouldDisplay31Accounts [2.3s]
  Passed FilterByTypeBank_ShouldDisplay5Rows [2.1s]
  Passed ValidateTypeColumnDistribution_ShouldMatchExpectedCounts [2.5s]
  Passed GridLoad_ShouldCompleteWithin10Seconds [1.8s]

Test Run Successful.
Total tests: 4
     Passed: 4
```

---

## Continue.dev Usage Patterns

### 1. Generate Test from Natural Language

**Prompt** (Ctrl+L):

```
Create xUnit test that launches Wiley Widget, navigates to Budget Entry view,
verifies 12 revenue categories are displayed in the dropdown, and validates
that selecting "Property Tax" updates the description field.
```

### 2. Generate Assertion Logic

**Prompt** (highlight code, Ctrl+I):

```
Add FluentAssertions to validate this list contains 31 items,
all have non-null AccountNumber property, and at least 5 have Type="Bank"
```

### 3. Generate Mock Data

**Prompt** (Ctrl+L):

```
Generate 31 ConservationTrustFundAccount objects with realistic municipal
data (AccountNumber, Description, Type=Bank/Investment/Cash, Balance)
```

### 4. Refactor for Performance

**Prompt** (select method, Ctrl+I):

```
Optimize this E2E test to reduce wait times - use explicit waits
instead of Thread.Sleep, add retry logic for element location
```

---

## Troubleshooting

### Ollama Not Responding

```powershell
# Check Ollama service status
Get-Process ollama

# Restart Ollama
taskkill /f /im ollama.exe
& "C:\Users\$env:USERNAME\AppData\Local\Programs\Ollama\ollama.exe" serve
```

### Continue.dev Not Generating Code

1. Check `Continue: Open Logs` in Command Palette
2. Verify `http://localhost:11434` is accessible:
   ```powershell
   Invoke-RestMethod -Uri "http://localhost:11434/api/tags"
   ```

### FlaUI Can't Find Elements

```csharp
// Enable FlaUI logging
FlaUI.Core.Logging.LoggerBase.Instance = new FlaUI.Core.Logging.ConsoleLogger();

// Use Inspect.exe to verify AutomationIds
// Download: https://learn.microsoft.com/en-us/windows/win32/winauto/inspect-objects
```

### WPF App Doesn't Launch

```powershell
# Ensure app builds successfully
dotnet build c:\Users\biges\Desktop\Wiley_Widget\WileyWidget.csproj -c Debug

# Test manual launch
& "c:\Users\biges\Desktop\Wiley_Widget\bin\Debug\net9.0-windows\WileyWidget.exe"
```

---

## Performance Tips

### Optimize LLM Response Time

```powershell
# Use smaller models for faster generation
ollama pull deepseek-coder:1.3b-instruct

# Configure Continue.dev with lower context window
# In config.json: "contextLength": 2048
```

### Speed Up E2E Tests

1. **Use Application Recycling**: Keep app instance alive between tests
2. **Parallel Execution**: Run independent tests concurrently
3. **Smart Waits**: Replace `Thread.Sleep` with `WaitUntil` conditions

---

## Next Steps

1. ✅ **Extend Test Coverage**: Use Continue.dev to generate tests for Budget Entry, Tax Rate views
2. ✅ **CI/CD Integration**: Add E2E tests to GitHub Actions workflow
3. ✅ **Visual Regression**: Integrate with Playwright for screenshot comparison
4. ✅ **Test Data Management**: Use database seeding for consistent test data

---

## References

- **Continue.dev**: https://github.com/continuedev/continue
- **Ollama**: https://ollama.com/
- **FlaUI**: https://github.com/FlaUI/FlaUI
- **Syncfusion Automation**: https://help.syncfusion.com/wpf/datagrid/ui-automation

---

**Last Updated**: November 3, 2025
**Author**: AI-Assisted Setup for Wiley Widget Team
