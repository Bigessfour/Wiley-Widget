# Dead Code Evaluation & Cleanup Guide

## 🎯 Overview

When the dead code scanner finds "unused" methods, **don't delete them immediately**. You need to evaluate each one to determine if it's:

1. **False Positive** - Actually used but not detected (keep it)
2. **Unimplemented Feature** - Should be wired up (implement it)
3. **True Dead Code** - Never used and not needed (delete it)

## 📋 Evaluation Process

### Phase 1: Run the Scanner

```powershell
# Scan for dead code
.\tools\Find-DeadCode.ps1

# Output:
# - tmp/dead-code-report.csv
# - tmp/dead-code-report.json
```

Or use VS Code task: `Ctrl+Shift+P` → "Tasks: Run Task" → "🔍 Find Dead Code"

### Phase 2: Auto-Categorize

```powershell
# Run the evaluator
.\tools\Evaluate-DeadCode.ps1

# This will:
# 1. Check for event handler wiring in .Designer.cs files
# 2. Check for [RelayCommand] attributes
# 3. Check for reflection-based calls
# 4. Categorize methods automatically
# 5. Optionally prompt for interactive review
```

Or use VS Code task: `Ctrl+Shift+P` → "Tasks: Run Task" → "🔍 Evaluate Dead Code"

### Phase 3: Manual Review

For each method flagged for review, check:

#### **Event Handlers (WinForms)**

**Check**: Does `.Designer.cs` file have event subscription?

```csharp
// In MyPanel.Designer.cs - LOOK FOR THIS:
this.myButton.Click += new System.EventHandler(this.MyButton_Click);
```

- **FOUND** → **KEEP IT** (false positive - it's wired in designer)
- **NOT FOUND** → Is this intentional?
  - **YES** → **IMPLEMENT IT** (wire up the event)
  - **NO** → **DELETE IT** (dead code)

**How to wire up:**

```csharp
// In MyPanel.Designer.cs InitializeComponent():
this.myButton.Click += new System.EventHandler(this.MyButton_Click);

// Or in code:
myButton.Click += MyButton_Click;
```

#### **Command Methods (ViewModels)**

**Check**: Is there a `[RelayCommand]` attribute or Command property?

```csharp
// LOOK FOR THIS:
[RelayCommand]
private async Task SaveDataAsync() { }

// OR THIS:
public ICommand SaveDataCommand { get; }
```

- **FOUND** → **KEEP IT** (false positive - used via command binding)
- **NOT FOUND** → Should this be a command?
  - **YES** → **IMPLEMENT IT** (add RelayCommand)
  - **NO** → **DELETE IT** (dead code)

**How to implement:**

```csharp
// Add attribute (CommunityToolkit.Mvvm):
[RelayCommand]
private async Task SaveDataAsync()
{
    // Implementation
}

// Command property auto-generated as: SaveDataCommand
```

#### **Helper Methods**

**Check**: Is it called from anywhere? Search codebase.

```powershell
# Search for method calls
Get-ChildItem -Recurse -Filter "*.cs" | Select-String "MethodName\("
```

- **FOUND** → **KEEP IT** (false positive - search missed it)
- **NOT FOUND** → Was it intended for future use?
  - **YES** → **MARK IT** (add TODO comment, keep for now)
  - **NO** → **DELETE IT** (dead code)

**How to mark for future:**

```csharp
// TODO: This method will be used for [feature X]
[Obsolete("Planned for future use - not yet implemented", error: false)]
private void FutureFeature() { }
```

#### **Reflection-Based Calls**

**Check**: Search for reflection patterns:

```powershell
# Search for reflection usage
Get-ChildItem -Recurse -Filter "*.cs" |
    Select-String "GetMethod|MethodInfo|Invoke"
```

Look for:

```csharp
typeof(MyClass).GetMethod("MyMethod");
methodInfo.Invoke(instance, parameters);
```

- **FOUND** → **KEEP IT** (false positive - called via reflection)
- **NOT FOUND** → **DELETE IT** (dead code)

---

## 🗂️ Common Patterns & Decisions

### ✅ KEEP (False Positives)

**Event handlers in WinForms panels:**

```csharp
// AccountsPanel.cs
private void Grid_CellClick(object sender, EventArgs e)
// ✅ KEEP - Wired in AccountsPanel.Designer.cs
```

**ViewModel command methods:**

```csharp
// AccountsViewModel.cs
[RelayCommand]
private async Task CreateAccountAsync()
// ✅ KEEP - Used via CreateAccountCommand property
```

**Interface implementations:**

```csharp
// Even if private, may be called via interface
private void ISomeInterface.SomeMethod()
// ✅ KEEP - Interface contract
```

**Reflection-based calls:**

```csharp
// Called via MethodInfo.Invoke() or DI
private void ConfigureServices(IServiceCollection services)
// ✅ KEEP - Called via reflection
```

### 🔧 IMPLEMENT (Unimplemented Features)

**Event handler declared but never wired:**

```csharp
// BudgetPanel.cs
private void ExportPdfButton_Click(object sender, EventArgs e)
{
    // Implementation exists but never connected
}

// ACTION: Add to Designer.cs:
this.exportPdfButton.Click += new System.EventHandler(this.ExportPdfButton_Click);
```

**Command method missing [RelayCommand]:**

```csharp
// QuickBooksViewModel.cs
private async Task SyncAccountsAsync()
{
    // Implementation exists but not exposed as command
}

// ACTION: Add attribute:
[RelayCommand]
private async Task SyncAccountsAsync() { }
```

**Helper method that should be called:**

```csharp
// MainForm.Helpers.cs
private void RefreshActiveGrid()
{
    // Useful method that was never called
}

// ACTION: Call it from relevant event handlers or methods
```

### 🗑️ DELETE (True Dead Code)

**Old implementations replaced by refactoring:**

```csharp
// Old version kept around "just in case"
private void LoadDataOld()
{
    // Old SQL-based implementation
    // Replaced by Entity Framework version
}
// ❌ DELETE - No longer needed
```

**Copy-paste code never integrated:**

```csharp
// Copied from Stack Overflow but never used
private void HelperMethod()
{
    // Was going to use this but found better solution
}
// ❌ DELETE - Never integrated
```

**Experimental code abandoned:**

```csharp
// TestMethod2, TestImplementation, etc.
private void TestNewApproach()
{
    // Tried this but went different direction
}
// ❌ DELETE - Experiment abandoned
```

**Legacy methods from old architecture:**

```csharp
private void LegacyDataLoad()
{
    // From before the ViewModel refactoring
}
// ❌ DELETE - Architecture evolved
```

---

## 🔄 Workflow Summary

```
1. Run Scanner
   ↓
2. Run Evaluator (auto-categorize)
   ↓
3. Review "Needs Review" category
   ↓
4. For each method, decide:
   ├─ Keep (false positive)
   ├─ Implement (wire it up)
   └─ Delete (true dead code)
   ↓
5. Take action on each category
   ↓
6. Re-run scanner to verify
```

---

## 🛠️ Taking Action

### For KEEP (False Positives)

```powershell
# No action needed, just document why in evaluation report
# The scanner limitations are acceptable
```

### For IMPLEMENT (Wire Up)

**Event Handlers:**

```csharp
// 1. Open .Designer.cs file
// 2. Find InitializeComponent()
// 3. Add event subscription:
this.myButton.Click += new System.EventHandler(this.MyButton_Click);
```

**Commands:**

```csharp
// Add [RelayCommand] attribute:
[RelayCommand]
private async Task MyActionAsync() { }

// Or create command property manually:
public ICommand MyActionCommand { get; }
MyActionCommand = new RelayCommand(MyAction);
```

### For DELETE (Dead Code)

**Manual Deletion (Recommended):**

```powershell
# 1. Open file in VS Code
# 2. Navigate to line number (Ctrl+G)
# 3. Delete entire method
# 4. Save file
# 5. Build to verify no breaks
```

**Bulk Deletion (Use with EXTREME caution):**

```powershell
# The evaluator can generate a deletion guide
# But manual review is strongly recommended
```

---

## 📊 Example Session

```powershell
# Step 1: Find dead code
PS> .\tools\Find-DeadCode.ps1
📊 Found 139 potentially unused methods (16.26% of 855 total)
💾 Report saved: tmp/dead-code-report.csv

# Step 2: Evaluate
PS> .\tools\Evaluate-DeadCode.ps1
✅ Keep (False Positives):   85
🔧 Implement (Wire Up):      12
🗑️  Delete (True Dead Code): 7
👀 Still Under Review:       35

# Step 3: Review the "Needs Review" category
# (Interactive prompt walks through each one)

# Step 4: Take action
# - Wire up the 12 methods marked for implementation
# - Delete the 7 confirmed dead code methods
# - Re-categorize the 35 under review

# Step 5: Verify
PS> .\tools\Find-DeadCode.ps1
📊 Found 47 potentially unused methods (5.5% of 855 total)
✅ Good progress! Down from 139 to 47
```

---

## ⚠️ Important Warnings

1. **Always build after deletions** - Ensure you didn't break anything
2. **Use source control** - Commit before bulk deletions so you can revert
3. **Be conservative** - When in doubt, keep the method (mark with [Obsolete] if needed)
4. **Test thoroughly** - Especially for event handlers and UI interactions
5. **Check test projects** - Methods may be tested even if not used in main code

---

## 🎯 Success Metrics

**Good Progress:**

- Unused percentage goes down (e.g., 16% → 5%)
- Most remaining "unused" are false positives
- Code is cleaner and more maintainable

**Red Flags:**

- Build breaks after deletion
- Runtime errors in previously working features
- Tests fail unexpectedly

---

## 📚 Additional Resources

- **False Positive Patterns**: See `docs/DEAD_CODE_FALSE_POSITIVES.md` (if created)
- **Refactoring Guide**: See `docs/CODE_CLEANUP_GUIDE.md` (if created)
- **CI/CD Integration**: Add dead code checks to build pipeline

---

## 🤝 Need Help?

If unsure about a method:

1. Search for method name in entire solution (Ctrl+Shift+F)
2. Check git history: `git log -p --all -S "MethodName"`
3. Ask team members who worked on that file
4. When in doubt, add `[Obsolete]` and keep it for one release cycle
