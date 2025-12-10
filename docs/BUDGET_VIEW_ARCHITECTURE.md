# Budget View Architecture Guide

## How ViewModels and Forms are Structured

This document explains the **layered architecture** and how each file determines what goes into the ViewModel and Form.

---

## 🏗️ Architecture Layers (Bottom to Top)

```
┌─────────────────────────────────────────────────────────────┐
│                     PRESENTATION LAYER                       │
│  ┌────────────────────┐         ┌─────────────────────┐    │
│  │ BudgetViewForm.cs  │ ◄──────►│ BudgetViewModel.cs  │    │
│  │  (UI Controls)     │  Binds  │  (State & Logic)    │    │
│  └────────────────────┘         └─────────────────────┘    │
│          ▲                                ▲                  │
└──────────┼────────────────────────────────┼─────────────────┘
           │                                │
           │ Displays                       │ Uses
           │                                │
┌──────────┼────────────────────────────────┼─────────────────┐
│          │         BUSINESS LAYER         │                  │
│          │                                │                  │
│          │       ┌────────────────────────┴────────┐        │
│          │       │  IBudgetRepository.cs           │        │
│          │       │  (Defines data operations)      │        │
│          │       └─────────────────────────────────┘        │
│          │                      ▲                            │
└──────────┼──────────────────────┼─────────────────────────┬─┘
           │                      │                          │
           │                      │ Implements               │
           │                      │                          │
┌──────────┼──────────────────────┼──────────────────────────┼─┐
│          │         DATA LAYER   │                          │ │
│          │                      │                          │ │
│          │       ┌──────────────┴───────────┐              │ │
│          │       │ BudgetRepository.cs      │              │ │
│          │       │ (Data access logic)      │              │ │
│          │       └──────────────────────────┘              │ │
│          │                      ▲                          │ │
└──────────┼──────────────────────┼──────────────────────────┼─┘
           │                      │                          │
           │                      │ Queries                  │
           │                      │                          │
┌──────────┼──────────────────────┼──────────────────────────┼─┐
│          │        MODEL LAYER   │                          │ │
│          │                      │                          │ │
│          └──────────┬───────────┴──────────────────────────┘ │
│                     │  BudgetEntry.cs                         │
│                     │  (Data structure & properties)          │
│                     └─────────────────────────────────────────┘
└─────────────────────────────────────────────────────────────┘
```

---

## 📁 File Responsibilities

### 1️⃣ **BudgetEntry.cs** (Model Layer)

**Location:** `src\WileyWidget.Models\Models\BudgetEntry.cs`

**Determines:**

- ✅ What **properties** exist (AccountNumber, BudgetedAmount, ActualAmount, etc.)
- ✅ What **relationships** exist (Department, Fund, Parent/Children)
- ✅ **Data validation rules** (Required, MaxLength, RegularExpression)
- ✅ **Computed properties** (Variance = BudgetedAmount - ActualAmount)

**Example:**

```csharp
public class BudgetEntry
{
    public int Id { get; set; }
    public string AccountNumber { get; set; }
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public int FiscalYear { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; }
    // ... more properties
}
```

**Impact on ViewModel:** The ViewModel needs collections and properties to work with these fields.

---

### 2️⃣ **IBudgetRepository.cs** (Business Interface)

**Location:** `src\WileyWidget.Business\Interfaces\IBudgetRepository.cs`

**Determines:**

- ✅ What **data operations** are available
- ✅ What **query methods** exist (GetByFiscalYearAsync, GetByDepartmentAsync)
- ✅ What **CRUD operations** are supported (AddAsync, UpdateAsync, DeleteAsync)
- ✅ What **analysis methods** exist (GetVarianceAnalysisAsync, GetYearEndSummaryAsync)

**Example:**

```csharp
public interface IBudgetRepository
{
    Task<IEnumerable<BudgetEntry>> GetByFiscalYearAsync(int fiscalYear);
    Task<IEnumerable<BudgetEntry>> GetByDepartmentAsync(int departmentId);
    Task<BudgetVarianceAnalysis> GetVarianceAnalysisAsync(DateTime start, DateTime end);
    Task AddAsync(BudgetEntry entry);
    Task UpdateAsync(BudgetEntry entry);
    Task DeleteAsync(int id);
}
```

**Impact on ViewModel:** The ViewModel creates **commands** that call these repository methods.

---

### 3️⃣ **BudgetRepository.cs** (Data Implementation)

**Location:** `src\WileyWidget.Data\BudgetRepository.cs`

**Determines:**

- ✅ **How** data is fetched (EF Core queries, caching, filtering)
- ✅ **Performance optimizations** (AsNoTracking, Include statements)
- ✅ **Telemetry and logging**

**Example:**

```csharp
public class BudgetRepository : IBudgetRepository
{
    public async Task<IEnumerable<BudgetEntry>> GetByFiscalYearAsync(int year)
    {
        return await _context.BudgetEntries
            .Where(b => b.FiscalYear == year)
            .Include(b => b.Department)
            .Include(b => b.Fund)
            .AsNoTracking()
            .ToListAsync();
    }
}
```

**Impact on ViewModel:** The ViewModel doesn't need to know implementation details.

---

### 4️⃣ **BudgetViewModel.cs** (Presentation Logic)

**Location:** `src\WileyWidget.WinForms\ViewModels\BudgetViewModel.cs`

**Determines:**

- ✅ What **state** the UI needs (collections, selected items, filters)
- ✅ What **commands** are available (Load, Add, Edit, Delete, Export)
- ✅ What **business logic** happens before/after data operations
- ✅ What **derived properties** are computed (TotalBudgeted, TotalVariance, PercentUsed)

**Built from:**

1. **BudgetEntry properties** → `ObservableCollection<BudgetEntry> BudgetEntries`
2. **Repository methods** → `IAsyncRelayCommand LoadBudgetsCommand`, `AddEntryAsync()`
3. **UI needs** → Filter properties, analysis properties, error messages
4. **Export services** → Export commands using `IPdfExportService`, `IExcelExportService`

**Example:**

```csharp
public partial class BudgetViewModel : ObservableObject
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly IPdfExportService _pdfExportService;

    // State from BudgetEntry model
    [ObservableProperty]
    private ObservableCollection<BudgetEntry> budgetEntries = new();

    [ObservableProperty]
    private int selectedFiscalYear = DateTime.Now.Year;

    // Derived analysis (computed from entries)
    [ObservableProperty]
    private decimal totalBudgeted;

    [ObservableProperty]
    private decimal totalActual;

    // Commands mapped to repository methods
    public IAsyncRelayCommand LoadBudgetsCommand { get; }
    public IAsyncRelayCommand<string> ExportToPdfCommand { get; }

    private async Task LoadBudgetsAsync()
    {
        var entries = await _budgetRepository.GetByFiscalYearAsync(SelectedFiscalYear);
        BudgetEntries = new ObservableCollection<BudgetEntry>(entries);

        // Calculate derived properties
        TotalBudgeted = BudgetEntries.Sum(e => e.BudgetedAmount);
        TotalActual = BudgetEntries.Sum(e => e.ActualAmount);
    }
}
```

---

### 5️⃣ **BudgetViewForm.cs** (UI Implementation)

**Location:** `src\WileyWidget.WinForms\Forms\BudgetViewForm.cs`

**Determines:**

- ✅ What **UI controls** are shown (DataGrid, Charts, Buttons, TextBoxes)
- ✅ How **data binding** works (BindingSource, PropertyChanged events)
- ✅ What **user interactions** are possible (Click events, Context menus)
- ✅ How data is **visualized** (Chart types, Grid columns, Formatting)

**Built from:**

1. **ViewModel properties** → Bound to UI controls
2. **ViewModel commands** → Wired to button clicks
3. **BudgetEntry properties** → Grid columns, chart data points
4. **UI/UX requirements** → Layout, styling, user feedback

**Example:**

```csharp
public partial class BudgetViewForm : Form
{
    private readonly BudgetViewModel _viewModel;
    private SfDataGrid _dataGrid;
    private ChartControl _chart;

    private void InitializeComponent()
    {
        // Grid columns come from BudgetEntry properties
        _dataGrid.Columns.Add(new GridTextColumn {
            MappingName = nameof(BudgetEntry.AccountNumber),
            HeaderText = "Account"
        });
        _dataGrid.Columns.Add(new GridNumericColumn {
            MappingName = nameof(BudgetEntry.BudgetedAmount),
            HeaderText = "Budget",
            Format = "C2"
        });

        // Button wired to ViewModel command
        var refreshBtn = new Button { Text = "Refresh" };
        refreshBtn.Click += async (s, e) =>
            await _viewModel.LoadBudgetsCommand.ExecuteAsync(null);
    }

    private void SetupBindings()
    {
        // Bind ViewModel collection to Grid
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BudgetViewModel.BudgetEntries))
            {
                _bindingSource.DataSource = _viewModel.BudgetEntries;
                _dataGrid.DataSource = _bindingSource;
            }
        };
    }
}
```

---

## 🔄 Decision Flow: "What goes where?"

### When adding a new feature, ask:

#### 1. **Is it a data field?** → Add to `BudgetEntry.cs`

```csharp
// Example: Add "Notes" field
public string Notes { get; set; }
```

#### 2. **Is it a data operation?** → Add to `IBudgetRepository.cs` and implement in `BudgetRepository.cs`

```csharp
// Example: Get overbudget entries
Task<IEnumerable<BudgetEntry>> GetOverBudgetEntriesAsync(int fiscalYear);
```

#### 3. **Is it UI state or business logic?** → Add to `BudgetViewModel.cs`

```csharp
// Example: Filter state
[ObservableProperty]
private bool showOnlyOverBudget;

// Example: Derived calculation
[ObservableProperty]
private int entriesOverBudget;
```

#### 4. **Is it a user action?** → Add command to `BudgetViewModel.cs` and wire in Form

```csharp
// ViewModel:
public IAsyncRelayCommand CalculateVariancesCommand { get; }

// Form:
var calcBtn = new Button { Text = "Calculate Variances" };
calcBtn.Click += async (s, e) =>
    await _viewModel.CalculateVariancesCommand.ExecuteAsync(null);
```

#### 5. **Is it visual/layout?** → Add to `BudgetViewForm.cs`

```csharp
// Example: Add a chart
var chart = new ChartControl { Dock = DockStyle.Fill };
panel.Controls.Add(chart);
```

---

## 📊 Concrete Example: Adding "Budget Notes" Feature

### Step 1: Add to Model

```csharp
// BudgetEntry.cs
[MaxLength(500)]
public string? Notes { get; set; }
```

### Step 2: Add Repository Method (if needed)

```csharp
// IBudgetRepository.cs
Task<IEnumerable<BudgetEntry>> GetEntriesWithNotesAsync(int fiscalYear);

// BudgetRepository.cs
public async Task<IEnumerable<BudgetEntry>> GetEntriesWithNotesAsync(int fiscalYear)
{
    return await _context.BudgetEntries
        .Where(b => b.FiscalYear == fiscalYear && !string.IsNullOrEmpty(b.Notes))
        .ToListAsync();
}
```

### Step 3: Add to ViewModel

```csharp
// BudgetViewModel.cs
[ObservableProperty]
private bool showOnlyWithNotes;

public IAsyncRelayCommand FilterNotesCommand { get; }

private async Task FilterNotesAsync()
{
    if (ShowOnlyWithNotes)
    {
        var entries = await _budgetRepository.GetEntriesWithNotesAsync(SelectedFiscalYear);
        BudgetEntries = new ObservableCollection<BudgetEntry>(entries);
    }
}
```

### Step 4: Add to Form

```csharp
// BudgetViewForm.cs
// Add column to grid
_dataGrid.Columns.Add(new GridTextColumn {
    MappingName = nameof(BudgetEntry.Notes),
    HeaderText = "Notes",
    Width = 200
});

// Add filter checkbox
var notesFilterCheck = new CheckBox { Text = "Show only with notes" };
notesFilterCheck.CheckedChanged += (s, e) =>
{
    _viewModel.ShowOnlyWithNotes = notesFilterCheck.Checked;
    await _viewModel.FilterNotesCommand.ExecuteAsync(null);
};
```

---

## 🎯 Key Principles

1. **Model (BudgetEntry)** = Data structure → "What data exists?"
2. **Repository (IBudgetRepository)** = Data access → "How do I get/save data?"
3. **ViewModel (BudgetViewModel)** = Presentation logic → "What does the UI need?"
4. **Form (BudgetViewForm)** = UI implementation → "How does the user interact?"

---

## 🔍 Quick Reference: Finding What You Need

| Want to...                      | Look at...                                       |
| ------------------------------- | ------------------------------------------------ |
| Know what properties exist      | `BudgetEntry.cs`                                 |
| Know what queries are possible  | `IBudgetRepository.cs`                           |
| Understand data fetching logic  | `BudgetRepository.cs`                            |
| See what commands are available | `BudgetViewModel.cs` (Commands section)          |
| See what filters/state exist    | `BudgetViewModel.cs` (ObservableProperty fields) |
| See what buttons/controls exist | `BudgetViewForm.cs` (InitializeComponent)        |
| Understand data binding         | `BudgetViewForm.cs` (SetupBindings)              |

---

## 🚀 Export Services Pattern

Export functionality uses separate service interfaces:

```csharp
// Defined in WileyWidget.Services.Abstractions or inline in Services
public interface IPdfExportService
{
    Task ExportBudgetEntriesToPdfAsync(List<BudgetEntry> entries, string filePath);
}

public interface IExcelExportService
{
    Task ExportBudgetEntriesAsync(List<BudgetEntry> entries, string filePath);
}

// ViewModel injects and uses them
private readonly IPdfExportService _pdfExportService;
public IAsyncRelayCommand<string> ExportToPdfCommand { get; }

private async Task ExportToPdfAsync(string? filePath)
{
    await _pdfExportService.ExportBudgetEntriesToPdfAsync(
        BudgetEntries.ToList(),
        filePath
    );
}
```

---

## Summary

**The ViewModel knows what to contain by looking at:**

1. ✅ The **Model** (BudgetEntry) → What properties/data exist
2. ✅ The **Repository Interface** → What operations are possible
3. ✅ The **UI requirements** → What state/commands the Form needs
4. ✅ The **Business logic** → What derived calculations are needed

**The Form knows what to display by looking at:**

1. ✅ The **ViewModel properties** → What to bind to controls
2. ✅ The **ViewModel commands** → What actions to trigger
3. ✅ The **Model properties** → What columns/fields to show
4. ✅ The **UX requirements** → How to layout and style controls
