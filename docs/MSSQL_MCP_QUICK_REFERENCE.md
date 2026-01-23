# MSSQL MCP Quick Reference Card

**Workspace:** Wiley-Widget  
**Status:** Ready to configure  
**Date:** January 22, 2026

## 🚀 Quick Start (3 Steps)

### Step 1: Set Connection String (Choose One)

**Windows Authentication (Recommended):**
```powershell
# Run this in PowerShell (any terminal)
[System.Environment]::SetEnvironmentVariable('MSSQL_CONNECTION_STRING', 'Server=localhost;Database=WileyWidget;Integrated Security=true;', [System.EnvironmentVariableTarget]::User)
```

**Or use the VS Code Task:**
- Press `Ctrl+Shift+B` → Select "🔌 Setup MSSQL MCP (Windows Auth)"

### Step 2: Restart VS Code
- Close and reopen VS Code for the environment variable to take effect

### Step 3: Start MSSQL MCP Server
1. Press `Ctrl+Shift+P`
2. Type: `MCP: Start Server`
3. Select: `mssql`
4. Watch for: ✓ `mssql: running (stdio)`

## 💬 Using MSSQL MCP in Copilot Chat

**Open Chat:** `Ctrl+Alt+I`

### Example Queries

```
@mssql Show all tables in the WileyWidget database
```

```
@mssql What columns are in the TownOfWileyBudget2026 table?
```

```
@mssql Count the total budget amount by FundOrDepartment
```

```
@mssql Generate a C# LINQ query to fetch all recreation budget items
```

## 📋 Available Tasks

Run with `Ctrl+Shift+B` (or `Ctrl+Shift+P` → "Run Task"):

| Task | Purpose |
|------|---------|
| 🔌 Setup MSSQL MCP (Windows Auth) | Configure with Windows authentication |
| 🔌 Setup MSSQL MCP (SQL Auth) | Configure with SQL Server login |
| 🧪 Test MSSQL Connection | Verify MSSQL_CONNECTION_STRING is set |
| 📊 View MSSQL MCP Setup Guide | Open the full setup guide |

## 🔧 VS Code Commands

| Command | Shortcut | Purpose |
|---------|----------|---------|
| MCP: Start Server | `Ctrl+Shift+P` | Start the MSSQL MCP server |
| MCP: List Servers | `Ctrl+Shift+P` | Show all MCP servers and status |
| MCP: Restart Server | `Ctrl+Shift+P` | Restart MSSQL MCP server |
| MCP: Kill All Servers | `Ctrl+Shift+P` | Stop all MCP servers |
| SQL: Connect | `Ctrl+Shift+P` | Connect to SQL Server via Explorer |
| SQL: New Query | `Ctrl+Shift+P` | Create a new SQL query file |

## 🐛 Troubleshooting

### Issue: "MSSQL_CONNECTION_STRING not found"
```powershell
# Verify the variable is set
$env:MSSQL_CONNECTION_STRING

# If empty, set it:
[System.Environment]::SetEnvironmentVariable('MSSQL_CONNECTION_STRING', 'Server=localhost;Database=WileyWidget;Integrated Security=true;', [System.EnvironmentVariableTarget]::User)

# Restart VS Code
```

### Issue: "Connection refused" or "SQL Server not reachable"
```powershell
# Check if SQL Server is running
Get-Service MSSQLSERVER | Select-Object Status

# Try alternative server values in connection string:
# - localhost
# - 127.0.0.1
# - 127.0.0.1,1433 (with port)
# - (local)
```

### Issue: "Authentication failed"
```powershell
# For Windows Auth: Verify your Windows account has SQL Server login
# For SQL Auth: Verify username/password and that 'sa' is enabled

# Test with sqlcmd:
sqlcmd -S localhost -E -Q "SELECT DB_NAME() AS DatabaseName"
```

### Issue: MCP server won't start
1. Run: `Ctrl+Shift+P` → "MCP: Kill All Servers"
2. Wait 2 seconds
3. Run: `Ctrl+Shift+P` → "MCP: Start Server" → `mssql`
4. Check VS Code Output panel for errors

## 📚 Example SQL Queries

### List all tables
```sql
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'
```

### Get TownOfWileyBudget2026 schema
```sql
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'TownOfWileyBudget2026'
ORDER BY ORDINAL_POSITION
```

### Budget summary by fund
```sql
SELECT 
    FundOrDepartment,
    COUNT(*) AS ItemCount,
    SUM(BudgetYear) AS TotalBudget,
    AVG(BudgetYear) AS AvgBudget
FROM dbo.TownOfWileyBudget2026
WHERE BudgetYear IS NOT NULL
GROUP BY FundOrDepartment
ORDER BY TotalBudget DESC
```

### Top 10 budget items
```sql
SELECT TOP 10
    FundOrDepartment,
    Description,
    AccountCode,
    BudgetYear
FROM dbo.TownOfWileyBudget2026
WHERE BudgetYear IS NOT NULL
ORDER BY BudgetYear DESC
```

## 🔐 Security Notes

✅ **Do:**
- Use Windows Authentication when possible (no password in connection string)
- Set MSSQL_CONNECTION_STRING as an environment variable, not in code
- Add `.env.mssql` to `.gitignore`
- Rotate SQL Server credentials regularly

❌ **Don't:**
- Hardcode connection strings in code
- Commit credentials to git
- Use weak passwords for SQL Server logins
- Share MSSQL_CONNECTION_STRING with teammates (use environment variables)

## 📖 More Information

- **Full Setup Guide:** [docs/MSSQL_MCP_SETUP_GUIDE.md](../docs/MSSQL_MCP_SETUP_GUIDE.md)
- **MCP Specification:** https://modelcontextprotocol.io
- **VS Code MCP Docs:** https://code.visualstudio.com/docs/copilot/customization/mcp-servers
- **MSSQL Extension:** https://marketplace.visualstudio.com/items?itemName=ms-mssql.mssql
- **SQL Server Connection Strings:** https://www.connectionstrings.com/sql-server/

## 🎯 Integration Checklist

- [ ] MSSQL_CONNECTION_STRING environment variable set
- [ ] VS Code restarted after setting environment variable
- [ ] MSSQL MCP server starts successfully (`MCP: Start Server`)
- [ ] Server shows as running (`MCP: List Servers`)
- [ ] Copilot Chat works with `@mssql` prefix
- [ ] Test query executes successfully
- [ ] Budget data is accessible in DashboardViewModel

## 💾 Environment Variable Setup Commands

### Windows (PowerShell - Recommended)
```powershell
# Current user (recommended for development)
[System.Environment]::SetEnvironmentVariable('MSSQL_CONNECTION_STRING', 'Server=localhost;Database=WileyWidget;Integrated Security=true;', [System.EnvironmentVariableTarget]::User)

# All users (requires admin, for CI/CD)
[System.Environment]::SetEnvironmentVariable('MSSQL_CONNECTION_STRING', 'Server=localhost;Database=WileyWidget;Integrated Security=true;', [System.EnvironmentVariableTarget]::Machine)
```

### View Current Setting
```powershell
# Current session
$env:MSSQL_CONNECTION_STRING

# User-level (persistent)
[System.Environment]::GetEnvironmentVariable('MSSQL_CONNECTION_STRING', [System.EnvironmentVariableTarget]::User)

# System-level (persistent)
[System.Environment]::GetEnvironmentVariable('MSSQL_CONNECTION_STRING', [System.EnvironmentVariableTarget]::Machine)
```

---

**Ready to use?** Run task: `🔌 Setup MSSQL MCP (Windows Auth)` then restart VS Code.

For detailed instructions, see [docs/MSSQL_MCP_SETUP_GUIDE.md](../docs/MSSQL_MCP_SETUP_GUIDE.md)
