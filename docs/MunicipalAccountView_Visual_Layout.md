# Municipal Account View - Visual Layout Diagram

## Complete Window Structure

```
╔═══════════════════════════════════════════════════════════════════════════════════╗
║  🎀 RIBBON CONTROL (Top Navigation Bar)                                          ║
║  ┌───────────────┬──────────────┬──────────────┬────────────────┐                ║
║  │ Data Ops      │ Filters      │ Navigation   │ View Options   │                ║
║  ├───────────────┼──────────────┼──────────────┼────────────────┤                ║
║  │ 🔄 Load Accts │ 📊 Fund Type │ ⬅️ Back      │ ⬆️ Expand All  │                ║
║  │ 🔗 Sync QBO   │ 🏷️ Acct Type │ 📤 Export    │ ⬇️ Collapse    │                ║
║  │ 📈 Budget     │ ✅ Apply     │ 🖨️ Print     │ ❌ Clear Error │                ║
║  │ 🔄 Refresh    │              │              │                │                ║
║  └───────────────┴──────────────┴──────────────┴────────────────┘                ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║                                                                                   ║
║  ┌─────────────────────────────────────────┬─┬──────────────────────────────┐   ║
║  │  📊 HIERARCHICAL ACCOUNT GRID           │║│  📋 ACCOUNT DETAILS PANEL    │   ║
║  │  (SfDataGrid - Left Panel)              │║│  (Right Panel)                │   ║
║  ├─────────────────────────────────────────┤║├──────────────────────────────┤   ║
║  │ Drop columns here to group...           │║│  ▼ Account Details           │   ║
║  ├──────┬───────────────┬───────┬──────────┤║│  ┌──────────────────────────┐│   ║
║  │ Acct │ Name          │ Fund  │ Type     │║│  │ Account #:  405.1        ││   ║
║  │ #    │               │       │          │║│  │ Name:       General Fund ││   ║
║  ├──────┼───────────────┼───────┼──────────┤║│  │ Fund:       General      ││   ║
║  │ 101  │ Cash - Gen    │ GEN   │ Asset    │║│  │ Type:       Asset        ││   ║
║  │ 102  │ Petty Cash    │ GEN   │ Asset    │║│  │ Balance:    $125,000.00  ││   ║
║  │ 201  │ A/P General   │ GEN   │ Liability│║│  │ Department: Finance      ││   ║
║  │ 301  │ Fund Balance  │ GEN   │ Equity   │║│  │ Period:     FY2024       ││   ║
║  ├──────┴───────────────┴───────┴──────────┤║│  │ Notes:      Main account ││   ║
║  │ ⊕ Special Revenue Fund                  │║│  └──────────────────────────┘│   ║
║  │   401  │ Property Tax │ SR   │ Revenue  │║├──────────────────────────────┤   ║
║  │   402  │ Sales Tax    │ SR   │ Revenue  │║│  📜 Recent Transactions      │   ║
║  ├──────┴───────────────┴───────┴──────────┤║│  ┌──────────────────────────┐│   ║
║  │ Balance │ Department │ Notes            │║│  │ Date │ Desc │ Debit │ Cr││   ║
║  ├─────────┼────────────┼──────────────────┤║│  ├──────┼──────┼───────┼───┤│   ║
║  │$50,000  │ Finance    │ Primary account  │║│  │01/05 │ Pay  │$1,500 │-  ││   ║
║  │$5,000   │ Finance    │ Petty cash fund  │║│  │01/08 │ Dep  │$2,000 │-  ││   ║
║  │-$15,000 │ Finance    │ Monthly payables │║│  │01/10 │ Bill │-      │$500││  ║
║  │$100,000 │ Finance    │ Net position     │║│  │01/12 │ Pay  │$3,200 │-  ││   ║
║  ├─────────┴────────────┴──────────────────┤║│  ├──────┴──────┼───────┼───┤│   ║
║  │  Group Summary: General Fund: $140,000  │║│  │ Total:      │$6,700 │$500││  ║
║  ├──────────────────────────────────────────┤║│  └─────────────┴───────┴───┘│   ║
║  │  💰 Total Accounts: 125 | Balance:      │║│                              │   ║
║  │     $1,234,567.89                        │║│                              │   ║
║  └──────────────────────────────────────────┴┴┴──────────────────────────────┘   ║
║                                                                                   ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║  📊 STATUS BAR                                                                    ║
║  ● Status: Ready | Total Accounts: 125                                           ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
```

## Color-Coded Balance Indicators

```
┌─────────────────────────────────────────────┐
│  Balance Column Visual Examples:            │
├─────────────────────────────────────────────┤
│  🟢 Positive: $125,000.00  (Green)          │
│  🔴 Negative: -$15,000.00  (Red)            │
│  🔵 Zero:     $0.00        (Neutral Blue)   │
└─────────────────────────────────────────────┘
```

## Grid Features Visualization

```
┌──────────────────────────────────────────────────────────┐
│  📊 SfDataGrid Advanced Features                         │
├──────────────────────────────────────────────────────────┤
│                                                           │
│  1️⃣ GROUPING (Drag & Drop)                              │
│     ┌─────────────────────────────────────────┐         │
│     │ Drop columns here to group by that      │         │
│     │ column                                   │         │
│     └─────────────────────────────────────────┘         │
│                                                           │
│  2️⃣ FILTERING (Column Headers)                          │
│     ┌──────┬───────┐                                    │
│     │ Fund │ Type  │ ← Click for filter menu            │
│     └──────┴───────┘                                    │
│                                                           │
│  3️⃣ SORTING (Click Column Header)                       │
│     Account # ▲ (Ascending)                              │
│     Balance ▼ (Descending)                               │
│                                                           │
│  4️⃣ INLINE EDITING (Click Cell)                         │
│     ┌─────────────────────┐                              │
│     │ General Operations  │ ← Editable cell             │
│     └─────────────────────┘                              │
│                                                           │
│  5️⃣ SUMMARIES                                            │
│     Group: $140,000 (Fund subtotal)                      │
│     Total: $1,234,567.89 (Grand total)                   │
│                                                           │
│  6️⃣ TOOLTIPS (Hover Over Cells)                         │
│     💭 "Balance: $125,000.00"                            │
│     💭 "Fund classification (General, Special..."        │
│                                                           │
└──────────────────────────────────────────────────────────┘
```

## Interaction Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│  USER INTERACTION FLOW                                      │
└─────────────────────────────────────────────────────────────┘

1️⃣ WINDOW OPENS
   ↓
   OnContentRendered()
   ↓
   InitializeAsync()
   ↓
   LoadAccountsCommand.Execute()
   ↓
   [Busy Indicator Shows]
   ↓
   Database Query
   ↓
   [Grid Populates with Data]
   ↓
   [Busy Indicator Hides]

2️⃣ USER SELECTS ACCOUNT (Click Row)
   ↓
   SelectedAccount Property Updates (TwoWay Binding)
   ↓
   Details Panel Updates
   ↓
   Transactions Grid Populates

3️⃣ USER APPLIES FILTER
   ↓
   Select Fund Type: "Special Revenue"
   ↓
   Click "Apply Filters"
   ↓
   FilterByFundCommand.Execute()
   ↓
   [Busy Indicator Shows]
   ↓
   Database Query with Filter
   ↓
   [Grid Refreshes]
   ↓
   [Status Bar Updates: "Filtered to 25 Special Revenue accounts"]

4️⃣ USER EDITS ACCOUNT
   ↓
   Click "Notes" Cell
   ↓
   [Cell Becomes Editable]
   ↓
   Type New Value
   ↓
   Press Enter or Click Away
   ↓
   [ViewModel Property Updates]
   ↓
   [Database Update Triggered]

5️⃣ USER NAVIGATES BACK
   ↓
   Click "Back to Dashboard"
   ↓
   NavigateBackCommand.Execute()
   ↓
   Window.Close()
   ↓
   [Returns to Previous View]
```

## State Management Diagram

```
┌────────────────────────────────────────────┐
│  VIEWMODEL STATE MACHINE                   │
└────────────────────────────────────────────┘

┌─────────┐
│ Initial │
│ State   │
└────┬────┘
     │
     ↓
┌────────────────┐     ┌──────────────┐
│ Loading Data   │────→│ IsBusy=True  │
│ (Async)        │     │ StatusMsg... │
└────┬───────────┘     └──────────────┘
     │
     ↓
┌────────────────┐
│ Data Loaded    │
│ (Success)      │
└────┬───────────┘
     │
     ├──→ ┌─────────────────┐
     │    │ Ready State     │
     │    │ - IsBusy=False  │
     │    │ - HasError=False│
     │    └─────────────────┘
     │
     ├──→ ┌─────────────────┐
     │    │ Error State     │
     │    │ - IsBusy=False  │
     │    │ - HasError=True │
     │    │ - ErrorMessage  │
     │    └─────────────────┘
     │
     └──→ ┌─────────────────┐
          │ Filtered State  │
          │ - Subset Shown  │
          │ - StatusMsg...  │
          └─────────────────┘
```

## Data Binding Map

```
┌───────────────────────────────────────────────────────────┐
│  XAML ←→ VIEWMODEL BINDING MAP                           │
└───────────────────────────────────────────────────────────┘

RIBBON BUTTONS:
  LoadAccountsCommand           → LoadAccountsCommand
  SyncFromQuickBooksCommand     → SyncFromQuickBooksCommand
  FilterByFundCommand           → FilterByFundCommand
  NavigateBackCommand           → NavigateBackCommand

ACCOUNT GRID:
  ItemsSource                   → MunicipalAccounts (ObservableCollection)
  SelectedItem (TwoWay)         → SelectedAccount

FILTER CONTROLS:
  SelectedItem (TwoWay)         → SelectedFundFilter
  SelectedItem (TwoWay)         → SelectedTypeFilter

DETAILS PANEL:
  DataContext                   → SelectedAccount
    AccountNumber.Value         → Account Number Display
    Name                        → Account Name Display
    Balance                     → Balance Display (Formatted)
    Department.Name             → Department Display

TRANSACTION GRID:
  ItemsSource                   → SelectedAccount.Transactions

STATUS BAR:
  Text                          → StatusMessage
  Text                          → ErrorMessage
  Visibility                    → HasError (via BoolToVis)

BUSY INDICATOR:
  IsBusy                        → IsBusy
  Visibility                    → IsBusy (via BoolToVis)
```

## Theme Color Palette

```
╔═══════════════════════════════════════════════════════════╗
║  FLUENT DARK THEME COLOR SCHEME                          ║
╠═══════════════════════════════════════════════════════════╣
║  Background (Main):       #FF0F1724  (Dark Blue-Gray)    ║
║  Background (Panel):      #FF1A2C41  (Medium Blue-Gray)  ║
║  Foreground (Primary):    #FFE0E6F0  (Light Gray-White)  ║
║  Foreground (Secondary):  #FFB9C8EC  (Light Blue-Gray)   ║
║  Accent (Positive):       #FF4ADE80  (Green)             ║
║  Accent (Negative):       #FFF87171  (Red)               ║
║  Accent (Primary):        #FF3B82F6  (Blue)              ║
║  Border:                  #FF2D4A6E  (Medium Blue)       ║
║  Overlay:                 #DD000000  (Semi-Transparent)  ║
╚═══════════════════════════════════════════════════════════╝
```

---

**Legend:**
- 🎀 Ribbon Control (Syncfusion)
- 📊 Data Grid (Syncfusion SfDataGrid)
- 📋 Accordion (Syncfusion SfAccordion)
- 🔄 Command Button
- ⬅️ Navigation
- 📤 Export/Print
- ⬆️⬇️ View Controls
- 🟢 Positive Balance (Green)
- 🔴 Negative Balance (Red)
- 🔵 Neutral/Zero (Blue-Gray)
- ● Status Indicator
- ⊕ Expandable Group

**Created**: 2025-01-12  
**Version**: 1.0
