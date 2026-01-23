# Town of Wiley Budget Data Flow - Debugging Checklist

**Date**: January 22, 2026  
**Purpose**: Verify where the imported budget data stops flowing through the system  
**Status**: Implementation Mode (Tests 1-4 ready to execute)

---

## Quick Context

From the latest run log (19:55–19:56 MST):
- Dashboard loads but shows **0 rows** of real data
- Repository fallback activated showing sample data instead
- Questions: Did import complete? Is data in DB? Is query returning results?

---

## ✅ Test 1: Verify Data in Database (CRITICAL FIRST)

**When**: Before any other test  
**Duration**: ~2 minutes  
**Tool**: SSMS or command-line SQL

### Execute These Queries

Open SSMS → Connect to your database → Run:

```sql
-- Query 1: Total row count
SELECT COUNT(*) AS TotalRowsInDB FROM dbo.TownOfWileyBudget2026;
```

**Expected**: Should see a number > 50–100 (not 0)

```sql
-- Query 2: Apartments rows
SELECT 
    COUNT(*) AS ApartmentsRowCount, 
    SUM(CAST(BudgetYear AS DECIMAL(18,2))) AS TotalApartmentsBudget,
    SUM(CAST(ActualYTD AS DECIMAL(18,2))) AS TotalApartmentsActual
FROM dbo.TownOfWileyBudget2026 
WHERE MappedDepartment = 'Apartments'
   OR Description LIKE '%Apartment%'
   OR FundOrDepartment LIKE '%Apartment%';
```

**Expected**: Rows > 5–10, TotalApartmentsBudget > 500,000

```sql
-- Query 3: Department breakdown
SELECT 
    MappedDepartment, 
    COUNT(*) AS Lines, 
    SUM(CAST(BudgetYear AS DECIMAL(18,2))) AS ProposedBudget, 
    SUM(CAST(ActualYTD AS DECIMAL(18,2))) AS ActualYTD
FROM dbo.TownOfWileyBudget2026
WHERE MappedDepartment IN ('Sanitation District','Sewer','Water','Trash','Apartments','Administration','Capital Projects')
   OR FundOrDepartment IN ('Sanitation District','Sewer','Water','Trash','Apartments','Administration','Capital Projects')
GROUP BY MappedDepartment
ORDER BY ProposedBudget DESC;
```

**Expected**: Each department has some rows with non-zero budgets

### What It Tells You

| Result | Interpretation | Next Step |
|--------|-----------------|-----------|
| **0 rows** in Query 1 | Import never ran or failed silently | Re-run import scripts |
| **Rows exist** but Apartments empty | Mapping or import partially succeeded | Proceed to Test 2 |
| **All departments have data** | Database is fine → issue is retrieval | Proceed to Test 2 |

---

## ✅ Test 2: Run App with Debug Output (TEST MODE)

**When**: After Test 1 shows data exists  
**Duration**: ~5 minutes  
**What Changed**: Repository now prints `[TEST]` console messages when fetching data

### Steps

1. **Run the app** (Debug or Release mode)
2. **Open Dashboard panel** (or any panel that loads budget data)
3. **Check Output Window** in VS Code for `[TEST]` lines:
   - `[TEST] Repository: Fetched X rows from DB`
   - `[TEST] Repository: INJECTED 1 fake TEST-APARTMENTS row...` (if DB is empty)
   - `[TEST] Repository: ERROR...` (if connection fails)

### Expected Output Sequences

**Scenario A: Database has real data**
```
[TEST] Repository: Fetched 87 rows from DB
[Dashboard visible with data]
```

**Scenario B: Database is empty (import failed)**
```
[TEST] Repository: Fetched 0 rows from DB
[TEST] Repository: INJECTED 1 fake TEST-APARTMENTS row for debugging
[Dashboard shows 1 test row: "TEST-APARTMENTS"]
```

**Scenario C: Connection fails**
```
[TEST] Repository: ERROR fetching TownOfWileyBudget2026: The server was not found...
[Dashboard shows error message]
```

### What It Tells You

| Output | Interpretation | Root Cause |
|--------|-----------------|-----------|
| `Fetched 87 rows` + data shows | ✅ **Everything works** | None—data pipeline OK |
| `Fetched 0 rows` + 1 fake row appears | ❌ **Import failed** | Run import scripts again |
| `ERROR: Connection` | ❌ **Wrong connection string** | Check `appsettings.json` |
| `Fetched 0 rows` + no fake row | ❌ **View binding broken** | Check viewmodel/panel code |

---

## ✅ Test 3: Visual Verification (Dashboard Panel)

**When**: After Test 2 shows [TEST] output  
**Duration**: ~2 minutes  
**What to Look For**:

### If TEST-APARTMENTS appears:

1. Go to **Charts** or **Dashboard** panel
2. Look for grid/table with department rows
3. Check if **"TEST-APARTMENTS"** row is visible with:
   - Description: "Test Rental Income (DEBUG)"
   - Budgeted: $1,500,000
   - Actual YTD: $720,000

### If real data appears:

1. Go to **Dashboard** panel
2. Check metrics for non-zero values:
   - Total Budget > $0
   - Active Departments > 0
   - Department breakdown shows Water, Sewer, Trash, etc.

### Common Issues & Fixes

| Issue | Fix |
|-------|-----|
| Panel is blank | Check `IsLoading` state; refresh with Refresh button if present |
| "0 rows" message | Check browser console / output window for errors |
| Only sample data | Test 2 showed `Fetched 0 rows` → import failed |
| Grid won't load | Check theme/styling—doesn't affect data but hides rows visually |

---

## ✅ Test 4: Force Data Reload (Manual Panel Refresh)

**When**: After Test 2 & 3 complete  
**Duration**: ~2 minutes

### Steps

1. **If there's a Refresh button**: Click it
2. **If none exists**: 
   - Close the panel (undock or hide)
   - Reopen it (should retrigger `LoadCommand`)
3. **Watch Output** for `[TEST]` lines again
4. **Check Data**: Does it update with fresh counts?

### What It Tests

- Does the viewmodel properly call `LoadDashboardDataAsync`?
- Is the repository method actually invoked?
- Do property bindings auto-update when data changes?

---

## 📋 Complete Execution Order

Execute these **in sequence** (don't skip):

```
1. Run Test 1 (SSMS queries)
   ↓
   [0 rows?]  → Re-run import, then loop back to Test 1
   [Data exists?] → Proceed
   ↓
2. Run Test 2 (Start app, check [TEST] output)
   ↓
   [TEST output shows 0 rows] → DB is empty, import failed
   [TEST output shows 87 rows] → Proceed to Test 3
   ↓
3. Run Test 3 (Visual check in panel)
   ↓
   [No data visible?] → Check binding code, run Test 4
   [Data visible?] → ✅ DONE—data pipeline works!
   ↓
4. Run Test 4 (Refresh and re-verify)
   ↓
   [Data persists on refresh?] → ✅ STABLE
   [Data disappears?] → Check cache/state management
```

---

## 🔍 Specific Files to Monitor

### Console Output (Watch For [TEST] Messages)

**File**: Output Window in VS Code (View → Output)  
**Filter**: Search for `[TEST]` in output

### Code Changes Made (For Reference)

**File**: [BudgetRepository.cs](src/WileyWidget.Data/BudgetRepository.cs)  
**Method**: `GetTownOfWileyBudgetDataAsync()`  
**What Changed**:
- Added `Console.WriteLine("[TEST] Repository: ...")` at 3 key points
- Added fake data injection when DB returns 0 rows
- All changes guarded with `[TEST]` prefix for easy identification

### Optional: Add Breakpoints

If console output is unclear, add breakpoints here:

**File**: [DashboardViewModel.cs](src/WileyWidget.WinForms/ViewModels/DashboardViewModel.cs)  
**Line**: Line that calls `_budgetRepository.GetBudgetSummaryAsync(...)`  
**Watch Variables**:
- `analysis` → Should have `TotalBudgeted > 0`
- `analysis.DepartmentSummaries.Count` → Should be > 0

---

## 📊 Expected Success Criteria

### Test 1 Success
- [ ] Query 1 shows > 50 rows in `TownOfWileyBudget2026`
- [ ] Query 2 shows Apartments with non-zero budget
- [ ] Query 3 shows 4+ departments with data

### Test 2 Success
- [ ] See `[TEST] Repository: Fetched X rows` message
- [ ] X > 0 (either real data or injected fake row)

### Test 3 Success
- [ ] Dashboard panel displays data
- [ ] Can see at least one department row
- [ ] Numbers match Test 1 query results

### Test 4 Success
- [ ] Refresh button updates data
- [ ] [TEST] output shows fresh row count
- [ ] Visual display updates accordingly

---

## 🚀 Quick Reference Commands

### Run Import (if Test 1 shows 0 rows)

```powershell
# Navigate to scripts folder
cd c:\Users\biges\Desktop\Wiley-Widget\sql

# Run the import
sqlcmd -S (local)\SQLEXPRESS -d WileyWidget -i import_town_of_wiley_2026.sql
```

### Run App in Debug Mode

```powershell
cd c:\Users\biges\Desktop\Wiley-Widget
dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
```

### View Output Window

In VS Code: **View** → **Output** → Dropdown: Select **"WileyWidget"** or **"Debug Console"**

---

## 📝 Notes & Observations

- The fake row injection is **temporary** (for testing only)—remove before commit
- `[TEST]` output is **only visible** in console/output window, not in UI
- If tests pass, you'll know:
  - ✅ Import works
  - ✅ Database connectivity is OK
  - ✅ Repository queries work
  - ✅ ViewModel loads data correctly
  - ✅ UI bindings display data

---

**Status**: Ready to execute  
**Last Updated**: January 22, 2026  
**Next Step**: Start with Test 1 (SSMS queries)
