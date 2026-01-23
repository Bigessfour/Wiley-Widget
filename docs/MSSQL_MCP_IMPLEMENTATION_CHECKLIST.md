# MSSQL MCP Implementation Checklist

**Project:** Wiley-Widget  
**Date Created:** January 22, 2026  
**Objective:** Set up MSSQL MCP server for GitHub Copilot Chat integration with budget database

---

## Phase 1: Environment Setup ✅ Complete

- [x] SQL Server (mssql) VS Code extension installed
- [x] `.vscode/settings.json` has MSSQL MCP configuration
- [x] MSSQL connection string template created (`.env.mssql.example`)
- [x] PowerShell setup script created (`scripts/setup-mssql-mcp.ps1`)
- [x] VS Code tasks added for MSSQL MCP management
- [x] Setup guide documentation created

---

## Phase 2: Configuration (In Progress)

### Step 1: Set MSSQL Connection String ⏳

Choose one option:

**Option A: Windows Authentication (Recommended)**
- [ ] Run task: `🔌 Setup MSSQL MCP (Windows Auth)`
  - OR manually run PowerShell:
  ```powershell
  [System.Environment]::SetEnvironmentVariable('MSSQL_CONNECTION_STRING', 'Server=localhost;Database=WileyWidget;Integrated Security=true;', [System.EnvironmentVariableTarget]::User)
  ```

**Option B: SQL Server Authentication**
- [ ] Run task: `🔌 Setup MSSQL MCP (SQL Auth)`
  - OR manually set with sa password:
  ```powershell
  [System.Environment]::SetEnvironmentVariable('MSSQL_CONNECTION_STRING', 'Server=localhost;Database=WileyWidget;User Id=sa;Password=YourPassword123;', [System.EnvironmentVariableTarget]::User)
  ```

### Step 2: Verify SQL Server is Running ⏳

- [ ] Check SQL Server service:
  ```powershell
  Get-Service MSSQLSERVER | Select-Object Status
  ```
  Expected: `Running`

- [ ] Database exists:
  ```powershell
  sqlcmd -S localhost -E -Q "SELECT name FROM sys.databases WHERE name='WileyWidget'"
  ```
  Expected: `WileyWidget` in results

### Step 3: Restart VS Code ⏳

- [ ] Close VS Code completely
- [ ] Reopen VS Code
- [ ] Wait for extensions to load (check bottom-right corner for "Ready")

---

## Phase 3: Server Startup ⏳

### Step 1: Start MSSQL MCP Server

- [ ] Press `Ctrl+Shift+P`
- [ ] Type: `MCP: Start Server`
- [ ] Select: `mssql`
- [ ] Wait for startup message

### Step 2: Verify Server is Running

- [ ] Press `Ctrl+Shift+P`
- [ ] Type: `MCP: List Servers`
- [ ] Confirm you see:
  ```
  ✓ mssql: running (stdio)
  ```

### Step 3: Test MSSQL Connection

- [ ] Run task: `🧪 Test MSSQL Connection`
- [ ] Verify output shows:
  ```
  ✅ MSSQL_CONNECTION_STRING is set
  ```

---

## Phase 4: Copilot Chat Integration ⏳

### Step 1: Open Copilot Chat

- [ ] Press `Ctrl+Alt+I` (or `Ctrl+Shift+P` → "Copilot Chat: Open")
- [ ] Chat panel opens on the right

### Step 2: Test Basic Query

- [ ] Type in chat:
  ```
  @mssql Show all tables in the WileyWidget database
  ```
- [ ] Press Enter and wait for response
- [ ] Verify Copilot returns list of tables

### Step 3: Test Budget Table Query

- [ ] Type in chat:
  ```
  @mssql What is the schema of the TownOfWileyBudget2026 table?
  ```
- [ ] Verify column names and types are returned

### Step 4: Test Data Query

- [ ] Type in chat:
  ```
  @mssql Count rows in TownOfWileyBudget2026 by FundOrDepartment
  ```
- [ ] Verify budget totals by fund are shown

---

## Phase 5: Application Integration ⏳

### Step 1: Update DashboardViewModel

- [ ] Open: `src/WileyWidget.WinForms/ViewModels/DashboardViewModel.cs`
- [ ] Identify budget data binding requirements
- [ ] Use Copilot Chat: `@mssql Generate a LINQ query for budget data`

### Step 2: Create IBudgetRepository Interface

- [ ] Create: `src/WileyWidget.Data/Repositories/IBudgetRepository.cs`
- [ ] Define methods:
  - [ ] `GetBudgetByFundAsync(string fund, CancellationToken ct)`
  - [ ] `GetBudgetSummaryAsync(CancellationToken ct)`
  - [ ] `GetBudgetByYearAsync(int year, CancellationToken ct)`

### Step 3: Implement Budget Repository

- [ ] Create: `src/WileyWidget.Data/Repositories/BudgetRepository.cs`
- [ ] Implement EF Core queries
- [ ] Use Copilot Chat: `@mssql Help me write LINQ queries for budget reports`

### Step 4: Register in DI Container

- [ ] Update: `src/WileyWidget.WinForms/Program.cs`
- [ ] Add registration:
  ```csharp
  services.AddScoped<IBudgetRepository, BudgetRepository>();
  ```

### Step 5: Bind to UI Controls

- [ ] Update DashboardViewModel to use `IBudgetRepository`
- [ ] Bind budget data to WinForms controls
- [ ] Test with sample budget data

---

## Phase 6: Testing & Validation ⏳

### Unit Tests

- [ ] Create: `tests/WileyWidget.Tests/Data/BudgetRepositoryTests.cs`
- [ ] Test cases:
  - [ ] `GetBudgetByFundAsync_WithValidFund_ReturnsBudgetItems`
  - [ ] `GetBudgetSummaryAsync_ReturnsCorrectTotals`
  - [ ] `GetBudgetByYearAsync_FiltersByYear`

### Integration Tests

- [ ] Test with actual database (if available)
- [ ] Verify EF Core queries execute correctly
- [ ] Check data accuracy

### UI Tests

- [ ] DashboardPanel displays budget data
- [ ] Fund filter works correctly
- [ ] Year selection updates data
- [ ] SfDataGrid renders properly

### Run Tests

- [ ] Run: `Ctrl+Shift+B` → `test` (or specific test task)
- [ ] All tests pass ✓

---

## Phase 7: Documentation & Handoff ⏳

### Documentation

- [ ] Update: `docs/BACKEND_QUICK_REFERENCE.md` with budget data structure
- [ ] Create: `docs/BUDGET_API_GUIDE.md` with usage examples
- [ ] Update: `CONTRIBUTING.md` with MSSQL MCP instructions

### Code Comments

- [ ] Add XML documentation to `IBudgetRepository`
- [ ] Add inline comments for complex LINQ queries
- [ ] Update method signatures with parameter descriptions

### Team Handoff

- [ ] Share setup guide: [docs/MSSQL_MCP_SETUP_GUIDE.md](MSSQL_MCP_SETUP_GUIDE.md)
- [ ] Share quick reference: [docs/MSSQL_MCP_QUICK_REFERENCE.md](MSSQL_MCP_QUICK_REFERENCE.md)
- [ ] Demonstrate Copilot Chat queries in team standup

---

## Troubleshooting

### Connection Issues

| Problem | Solution |
|---------|----------|
| "MSSQL_CONNECTION_STRING not found" | Set env var, restart VS Code |
| "Connection refused" | Verify SQL Server service is running |
| "Authentication failed" | Check credentials, verify SQL login exists |
| "Database WileyWidget not found" | Create database or update connection string |

### MCP Server Issues

| Problem | Solution |
|---------|----------|
| "mssql: error (stdio)" | Check MSSQL_CONNECTION_STRING env var |
| Server won't start | Kill all servers, restart VS Code |
| Copilot Chat can't reach server | Verify server status with `MCP: List Servers` |

### Quick Diagnostic Commands

```powershell
# Check connection string is set
$env:MSSQL_CONNECTION_STRING

# Check SQL Server service
Get-Service MSSQLSERVER | Select-Object Status

# Test connection with sqlcmd
sqlcmd -S localhost -E -Q "SELECT DB_NAME() AS CurrentDB"

# Check MCP server logs
Get-Content $env:LOCALAPPDATA\Code\logs\*mcp*.log -Tail 50
```

---

## Progress Tracking

| Phase | Status | Date | Notes |
|-------|--------|------|-------|
| 1. Environment Setup | ✅ Complete | 2026-01-22 | All files created and configured |
| 2. Configuration | ⏳ In Progress | TBD | Awaiting MSSQL_CONNECTION_STRING setup |
| 3. Server Startup | ⏳ Pending | TBD | Will start after configuration |
| 4. Copilot Integration | ⏳ Pending | TBD | Will test after server startup |
| 5. Application Integration | ⏳ Pending | TBD | DashboardViewModel integration |
| 6. Testing & Validation | ⏳ Pending | TBD | Unit/integration/UI tests |
| 7. Documentation & Handoff | ⏳ Pending | TBD | Team documentation |

---

## Resources

- **Setup Guide:** [docs/MSSQL_MCP_SETUP_GUIDE.md](MSSQL_MCP_SETUP_GUIDE.md)
- **Quick Reference:** [docs/MSSQL_MCP_QUICK_REFERENCE.md](MSSQL_MCP_QUICK_REFERENCE.md)
- **SQL Script:** [sql/TownOfWileyBudget2026_Import.sql](../sql/TownOfWileyBudget2026_Import.sql)
- **VS Code MCP Docs:** https://code.visualstudio.com/docs/copilot/customization/mcp-servers
- **MSSQL Extension:** https://marketplace.visualstudio.com/items?itemName=ms-mssql.mssql

---

## Notes

- MSSQL MCP is configured in `.vscode/settings.json` under `github.copilot.chat.mcpServers`
- Connection string is read from `MSSQL_CONNECTION_STRING` environment variable (secure, not in code)
- TownOfWileyBudget2026 table contains 482+ rows of budget data from CSV and image imports
- All tasks are available in VS Code Task Runner (`Ctrl+Shift+B`)
- PowerShell script `setup-mssql-mcp.ps1` automates environment variable configuration

---

**Next Action:** Complete Phase 2 Step 1 by setting MSSQL_CONNECTION_STRING environment variable.

For help, see: [docs/MSSQL_MCP_SETUP_GUIDE.md](MSSQL_MCP_SETUP_GUIDE.md)
