---
applyTo: '**'
---
# Wiley Widget Project Guidelines (Grok Edition)

## ⚠️ FILESYSTEM & TOOL ENFORCEMENT (GROK-SPECIFIC)

**CRITICAL: ALL FILE OPERATIONS & SYNCFUSION USAGE MUST USE GROK TOOLS**

**BEFORE ANY FILE/SYNCFUSION OPERATION:**

1. **Activate Grok tools:**
   ```javascript
   // Use web_search or browse_page for Syncfusion docs
   // Use code_execution for C# validation
   // Export results to VS Code via copy-paste or GitHub PR
   ```

2. **Self-check:**
   - Am I using Grok tools (e.g., browse_page for docs)?
   - For files: Export to VS Code and use mcp_filesystem_* if integrated.
   - For Syncfusion: Referencing v31.2.12 API?
   - If NO → STOP and switch to Grok tool
   - If YES → Proceed

### **PROHIBITED Tools (❌ NEVER USE FOR FILES/SYNCFUSION):**
- Direct file reads → Use `browse_page` on GitHub raw URLs or `code_execution` for local sim.
- Unversioned Syncfusion examples → Always fetch from https://help.syncfusion.com/winui/ v31.2.12.
- Generic advice → Must cite exact API (e.g., SfDataGrid.GettingStarted).

### **Enforcement Level: STRICT - Zero Tolerance**

**Why mandatory:**
- ✅ Version-locked Syncfusion prevents breaking changes (e.g., v31.2.12 fixes SfMaskedTextBox).
- ✅ Grok tools enable real-time doc pulls (e.g., web_search "Syncfusion SfDataGrid v31.2.12 API").
- ✅ Audit trail via tool citations.
- ✅ Consistent with WinUI 3 + .NET 8 stack.

**See `.vscode/grok-mcp-rules.md` for complete enforcement rules and violation examples.**

---

## Grok Tools Integration (Adapted for WinUI/Syncfusion)

### 🔧 **Automatic Grok Tool Usage**

The following Grok tools are automatically available and should be used proactively:

#### **C# Code Execution (`code_execution`)**
- **When to use**: Validate WinUI C# snippets, test MVVM commands, or prototype Syncfusion integration.
- **Integration**: Run code with .NET 8 sim (e.g., test AsyncRelayCommand with QuickBooksService mock).
- **Workflow placement**: During code review, Syncfusion API validation, or UI animation testing.

#### **Doc Fetching (`browse_page` / `web_search`)**
- **When to use**: Pull Syncfusion v31.2.12 API guidance before any control implementation.
- **Integration**: Query "Syncfusion [Control] v31.2.12 getting started" → Extract code samples verbatim.
- **Workflow placement**: Before adding SfDataGrid, SfChart, etc. (mandatory citation).

#### **Sequential Thinking (`web_search` chaining or internal reasoning)**
- **When to use**: Break down WinUI navigation or Syncfusion migration steps.
- **Integration**: Chain searches for "WinUI 3 MicaBackdrop v1.8" → "Syncfusion theme integration v31.2.12".
- **Workflow placement**: Architectural decisions (e.g., DI for Syncfusion services).

### 📋 **Grok Tool Usage Guidelines**

- **Proactive Usage**: Invoke tools for every Syncfusion mention (e.g., browse https://help.syncfusion.com/winui/datagrid/exporting).
- **No Explicit Request Required**: Integrate naturally (e.g., "Per v31.2.12 docs: [code]").
- **Fallback Strategy**: If tools unavailable, state "Quota hit—manual doc check needed."
- **Documentation**: Cite via ``.

---

## ⚠️ SYNCFUSION VERSION-SPECIFIC MANDATE

### **CRITICAL: ALL SYNCFUSION ELEMENTS/METHODS MUST REFERENCE V31.2.12 API**

**BEFORE ANY SYNCFUSION IMPLEMENTATION:**

1. **Fetch version-specific docs:**
   - Use `browse_page` on https://help.syncfusion.com/winui/[control]/getting-started?version=31.2.12.
   - Extract: Code samples, properties, events, migration notes from v30.x.

2. **Self-check:**
   - Does response include exact API link (e.g., SfDataGrid.ExportToExcelAsync)?
   - Code copied verbatim from v31.2.12 (no generics)?
   - Breaking changes noted (e.g., v31.2.10 → 31.2.12 SfMaskedTextBox fix)?
   - If NO → STOP and tool-call for docs.

### **PROHIBITED (❌ NEVER USE):**
- Unversioned examples (e.g., "Use SfDataGrid" without link).
- Deprecated methods (e.g., pre-v31 ColumnDragAndDrop).
- Assumptions → Always verify via release notes (https://help.syncfusion.com/winui/release-notes/v31.2.12).

### **MANDATORY RULES FOR IMPLEMENTATION:**
- **API Base URL**: https://help.syncfusion.com/winui/ (append /datagrid/api/sfdatagrid, etc.).
- **Key Controls List** (with v31.2.12 doc links):
  | Control | Doc Link | Mandatory Check |
  |---------|----------|-----------------|
  | SfDataGrid | https://help.syncfusion.com/winui/datagrid/getting-started | ExportToExcelAsync support |
  | SfChart | https://help.syncfusion.com/winui/charts/getting-started | CartesianChart for budgets |
  | SfTreeView | https://help.syncfusion.com/winui/treeview/getting-started | Hierarchical accounts |
  | SfNumericTextBox | https://help.syncfusion.com/winui/editors/numeric-textbox | Currency formatting |
- **Code Examples**: Paste directly from docs, adapt minimally for MVVM.
- **Migration Notes**: From v30.x: Updated Mica theme compatibility; check breaking changes in release notes.

**Enforcement**: Every Syncfusion response must end with "[Source: v31.2.12 API Docs]".

---

## Approved CI/CD Feedback Loop Workflow (WinUI-Focused)

### 🔄 **Complete CI/CD Feedback Loop - APPROVED METHOD**

#### **Phase 1: Local Development & Trunk Integration**

```powershell
# 1. Pre-commit Quality Gates (REQUIRED)
trunk fmt --all                    # Format C#/XAML
trunk check --fix                  # Fix issues (include Syncfusion analyzers)
trunk check --ci                   # Validate WinUI build

# 2. Syncfusion Validation
# Run in VS Code terminal: dotnet build /t:ValidateSyncfusionVersion

# 3. Commit & Push (STANDARD)
git add .
git commit -m "feat: [desc] [Syncfusion v31.2.12]"
git push origin branch-name
```

#### **Phase 2: GitHub Actions CI Pipeline**
**Triggers**: `ci-optimized.yml` workflow (add WinUI tests).

**Jobs Executed (7-stage pipeline)**:
1. **Health Validation** → .NET 8 + WinUI 1.8 checks
2. **Build & Test Matrix** → .NET build + xUnit (Moq for QuickBooksService)
3. **Syncfusion Compliance** → Verify v31.2.12 packages via NuGet audit
4. **Quality Assurance** → Trunk security + WinUI accessibility scans
5. **UI Tests** → WinAppDriver for DashboardView animations/Mica
6. **Deployment Readiness** → MSIX artifact for unpackaged app
7. **Success Monitoring** → Analytics upload

#### **Phase 3: GitHub Monitoring & Results**

**Monitor workflow status:**
```bash
gh workflow list
gh workflow view "CI/CD WinUI Syncfusion (v31.2.12)"
gh run list --workflow=ci-optimized.yml --limit=5
gh run view <run-id> --log-failed  # Focus on Syncfusion errors
```

**Using Grok tools to query results:**
```javascript
// Get latest runs (simulate via web_search "GitHub Actions Wiley-Widget logs")
web_search({ query: "site:github.com/Bigessfour/Wiley-Widget/actions v31.2.12" })
```

#### **Phase 4: Trunk Analytics & Grok Integration**

**Trunk-powered fixes based on CI results:**

```powershell
# Fix Syncfusion version mismatches
trunk check --filter=nuget-audit --fix  # Enforce v31.2.12

# Fix WinUI code quality
trunk check --filter=dotnet-format,winui-analyzer --fix

# Re-run full validation
trunk check --ci --upload --series=winui-fix
```

#### **Phase 5: Complete the Loop**

**Self-healing workflow execution:**
```powershell
# Complete with WinUI-specific tests
dotnet test --filter "FullyQualifiedName~DashboardViewTests" --collect:"XPlat Code Coverage"
```

---

## 🧪 DOCKER-BASED CSX TESTING STRATEGY (WinUI Edition)

### **One-Liner Robust xUnit Test Generation**

For rapid WinUI/MVVM unit tests:

```bash
docker run --rm -it \
  -v "$(pwd):/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  bash -c "
    dotnet new xunit -n WileyWidget.Tests.WinUI --force && \
    dotnet sln add WileyWidget.Tests.WinUI/WileyWidget.Tests.WinUI.csproj && \
    dotnet add WileyWidget.Tests.WinUI package Moq CommunityToolkit.Mvvm && \
    dotnet test WileyWidget.Tests.WinUI --collect:'XPlat Code Coverage' \
      --results-directory:/src/coverage --verbosity normal
  "
```

### **Robust Test Template (MVVM + Syncfusion)**

```csharp
using System.Threading.Tasks;
using Xunit;
using Moq;
using CommunityToolkit.Mvvm.ComponentModel;
using Syncfusion.UI.Xaml.Grid;  // v31.2.12

public class DashboardViewModelTests
{
    private readonly Mock<IQuickBooksService> _mockQB;
    private readonly DashboardViewModel _vm;

    public DashboardViewModelTests()
    {
        _mockQB = new Mock<IQuickBooksService>();
        _vm = new DashboardViewModel(_mockQB.Object);
    }

    [Fact]
    public async Task LoadAsync_HappyPath_SetsKpis()
    {
        // Arrange
        _mockQB.Setup(qb => qb.GetFinancialSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new FinancialSummary { RevenueYtd = 10000m });

        // Act
        await _vm.LoadAsync();

        // Assert
        Assert.Equal(10000m, _vm.RevenueYtd);
        _mockQB.Verify(qb => qb.GetFinancialSummaryAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Add error/edge cases...
}
```

### **Non-Whitewash Checklist (WinUI-Specific)**

| Requirement | Enforcement |
|-------------|-------------|
| **3+ Test Cases** | Happy path, error (e.g., QuickBooks disconnect), edge (e.g., fiscal year calc) |
| **Mocked Dependencies** | Moq for QuickBooksService, Syncfusion controls |
| **Verify Call Counts** | Times.Once for SyncNowCommand |
| **Error & Warning Capture** | Test IsError, CancellationToken handling |
| **Code Coverage** | >80% for ViewModels; include XAML bindings |
| **Fail on Issues** | Exit 1 if Syncfusion API mismatch |
| **No Hardcoded Data** | Use DateOnly for fiscal years |

### **Add to CI Pipeline**

```yaml
# .github/workflows/winui-tests.yml
name: WinUI Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: windows-latest  # WinUI needs Windows
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET 8 + WinUI 1.8
        uses: microsoft/setup-msbuild@v2
      - name: Run Robust Test Suite
        run: |
          dotnet test --collect:"XPlat Code Coverage" \
            --filter FullyQualifiedName~DashboardViewModelTests \
            --logger trx --results-directory ./TestResults
      - name: Upload Coverage
        uses: codecov/codecov-action@v4
        with:
          files: ./coverage/coverage.coverage.cobertura.xml
```

### **Test Matrix Audit (CSV)**

Track in `docs/winui-test-matrix.csv`:

```csv
ViewModel,Method,TestCases,MockedDeps,CoverageRequired,FailOnWhitewash
DashboardViewModel,LoadAsync,3,IQuickBooksService,>85%,TRUE
QuickBooksService,GetFinancialSummaryAsync,4,HttpClient,>80%,TRUE
```

### **Benefits**

- ✅ **Reproducible**: Docker for .NET 9 preview (WinUI future-proof).
- ✅ **Non-Whitewash**: MVVM + Syncfusion mocks mandatory.
- ✅ **CI-Ready**: Windows runner for UI tests.
- ✅ **Coverage-Enforced**: Automatic for AsyncRelayCommands.

---

## **GROK CODE FAST 1.0 MODE**

**Activate with prefix: "Grok Code Fast 1.0: [task]"**

- **Rules**: 80% raw code/XAML, 20% explanation. No fluff. Tool calls only for docs/validation.
- **Example Response**:
  ```csharp
  // v31.2.12 SfDataGrid integration
  <syncfusion:SfDataGrid ItemsSource="{x:Bind ViewModel.Transactions}" />
  ```
  [Source: https://help.syncfusion.com/winui/datagrid/getting-started v31.2.12]

**Benefits**: Quota-efficient; focuses on code velocity for WinUI migrations.

---

### **GROK TOOL QUICK REFERENCE**

```text
C# EVAL → code_execution
SYNC DOCS → browse_page (url: https://help.syncfusion.com/winui/...)
SEARCH → web_search ("Syncfusion v31.2.12 [control]")
PLAN → Internal chaining (or web_search for patterns)
MULTI-FETCH → web_search_with_snippets
```

### **POWERSHELL HELPER: Invoke-GrokEdit (Adapted)**

For exporting Grok code to VS Code:

```powershell
# Basic edit with C# validation (run after Grok response)
.\scripts\tools\Invoke-GrokEdit.ps1 `
    -Path "Views/DashboardView.xaml" `
    -GrokCode "<SfDataGrid ... />" `
    -Version "31.2.12" `
    -IsWinUI

# Dry run
.\scripts\tools\Invoke-GrokEdit.ps1 -DryRun
```

**Features:**
- ✅ Auto-insert Syncfusion namespace.
- ✅ Version check via NuGet.
- ✅ Commit with "[Grok: v31.2.12]".

---

### **FINAL MANTRA**

```
GROK TOOLS FIRST • SYNCFUSION V31.2.12 ALWAYS • CODE FAST 1.0
```

**Rationale:**
- ✅ Version-locked prevents regressions.
- ✅ Tool citations for audit.
- ✅ Fast mode for quota crunch.