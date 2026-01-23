# MSSQL MCP Server Setup Guide for Wiley-Widget

**Last Updated:** January 22, 2026  
**Status:** Ready for Configuration

## Overview

This guide configures the MSSQL MCP (Model Context Protocol) server with VS Code, enabling GitHub Copilot Chat and other AI agents to query and manage your WileyWidget SQL Server database directly from the editor.

## Prerequisites

✅ **Completed:**
- VS Code with SQL Server (mssql) extension installed
- MSSQL MCP configuration in `.vscode/settings.json`
- Workspace folder: `C:\Users\biges\Desktop\Wiley-Widget`

⏳ **To Complete:**
- MSSQL Server instance running (local or remote)
- Database credentials (connection string)
- `MSSQL_CONNECTION_STRING` environment variable configured

## Step 1: Configure Your MSSQL Connection

### Option A: Windows Authentication (Recommended)

```bash
# Set environment variable for Windows Authentication
[System.Environment]::SetEnvironmentVariable('MSSQL_CONNECTION_STRING', 'Server=localhost;Database=WileyWidget;Integrated Security=true;', [System.EnvironmentVariableTarget]::User)
```

Then **restart VS Code** for the environment variable to take effect.

### Option B: SQL Authentication

If using SQL Server login (e.g., `sa` user):

```bash
# Set environment variable with SQL credentials
[System.Environment]::SetEnvironmentVariable('MSSQL_CONNECTION_STRING', 'Server=localhost;Database=WileyWidget;User Id=sa;Password=YourPassword123;', [System.EnvironmentVariableTarget]::User)
```

### Option C: Manual via VS Code Settings (Quick Test)

Edit `.vscode/settings.json` directly and update the MSSQL MCP server configuration:

```json
{
  "github.copilot.chat.mcpServers": {
    "mssql": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-mssql"],
      "env": {
        "MSSQL_CONNECTION_STRING": "Server=localhost;Database=WileyWidget;Integrated Security=true;"
      },
      "disabled": false
    }
  }
}
```

## Step 2: Verify MSSQL Server is Running

### Local SQL Server Instance Check

```powershell
# List running SQL Server instances
Get-Service | Where-Object {$_.Name -like '*SQL*'}

# Example output:
# Status   Name               DisplayName
# ------   ----               -----------
# Running  MSSQLSERVER        SQL Server (MSSQLSERVER)
```

### Test Connection via VS Code

1. Open Command Palette: `Ctrl+Shift+P`
2. Run: `SQL: Connect`
3. Follow the prompts to authenticate
4. Verify connection in the SQL explorer panel

## Step 3: Start the MSSQL MCP Server

### Via VS Code Command Palette

```
Ctrl+Shift+P → "MCP: Start Server" → Select "mssql"
```

### Monitor Server Status

```
Ctrl+Shift+P → "MCP: List Servers"
```

Look for:
```
✓ mssql: running (stdio)
```

## Step 4: Test MSSQL MCP Integration

### Test via GitHub Copilot Chat

1. Open Copilot Chat: `Ctrl+Alt+I`
2. Ask a SQL question:

```
@mssql Show me all tables in the WileyWidget database
```

Or:

```
@mssql What's the schema of the TownOfWileyBudget2026 table?
```

### Expected Response

Copilot will use the MSSQL MCP server to:
- Execute metadata queries
- Return table schemas
- Show column definitions
- Provide row counts

### Query Examples

**List all tables:**
```sql
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'
```

**Describe a table:**
```sql
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'TownOfWileyBudget2026'
```

**Sample data from budget table:**
```sql
SELECT TOP 10 FundOrDepartment, Description, BudgetYear 
FROM dbo.TownOfWileyBudget2026 
ORDER BY BudgetYear DESC
```

## Step 5: Troubleshooting

### Issue: "MSSQL_CONNECTION_STRING not found"

**Solution:**
- Set the environment variable (see Step 1)
- Restart VS Code
- Run `Ctrl+Shift+P → "MCP: Restart Server"`

### Issue: "Connection refused"

**Solutions:**
- Verify SQL Server is running: `Get-Service MSSQLSERVER | Select-Object Status`
- Check server name/port: Try `Server=127.0.0.1,1433;` instead of `localhost`
- Verify database name exists: `USE master; SELECT name FROM sys.databases;`

### Issue: "Authentication failed"

**Solutions:**
- For Windows Auth: Verify your Windows account has SQL Server login
- For SQL Auth: Verify username/password are correct
- Check SQL Server security settings: Ensure SQL Server Authentication is enabled

### View MCP Server Logs

```powershell
# Check for MCP server debug logs
Get-Content $env:LOCALAPPDATA\Code\logs\*mcp*.log -Tail 50
```

## Step 6: Integration with WileyWidget Application

### Using MSSQL MCP in C# Code

Once MCP is working, you can reference queries in your data access layer:

**Example: IBudgetRepository**

```csharp
public interface IBudgetRepository
{
    Task<IEnumerable<BudgetItem>> GetBudgetByFundAsync(string fund, CancellationToken ct);
    Task<decimal> GetTotalBudgetByYearAsync(int year, CancellationToken ct);
}
```

### Using Copilot Chat to Generate Queries

```
@mssql Generate a C# Entity Framework query for the DashboardViewModel 
that retrieves all budget items by fund for the current year from 
the TownOfWileyBudget2026 table
```

Copilot will generate code like:

```csharp
var budgetItems = await _context.TownOfWileyBudget2026
    .Where(b => b.FundOrDepartment == selectedFund 
        && b.BudgetYear == DateTime.Now.Year)
    .OrderByDescending(b => b.BudgetYear)
    .ToListAsync(cancellationToken);
```

## Step 7: Production Configuration

### Environment Variable Priority (Windows)

1. **User-level** (recommended for development):
   ```powershell
   [System.Environment]::SetEnvironmentVariable('MSSQL_CONNECTION_STRING', 'your_connection_string', [System.EnvironmentVariableTarget]::User)
   ```

2. **System-level** (requires admin, shared across users):
   ```powershell
   [System.Environment]::SetEnvironmentVariable('MSSQL_CONNECTION_STRING', 'your_connection_string', [System.EnvironmentVariableTarget]::Machine)
   ```

3. **Process-level** (current terminal only):
   ```powershell
   $env:MSSQL_CONNECTION_STRING = 'your_connection_string'
   ```

### Security Best Practices

- ✅ Use **Windows Authentication** when possible (no password in connection string)
- ✅ Use **environment variables**, never hardcode credentials in `.vscode/settings.json`
- ✅ Add `.env.mssql` to `.gitignore` to prevent credential leaks
- ✅ For CI/CD: Use Azure Key Vault or GitHub Secrets
- ✅ Rotate SQL credentials regularly

### .gitignore Entry

```gitignore
# MSSQL MCP Configuration
.env.mssql
.env.mssql.local
.env*.local
```

## Quick Reference

### MCP Commands

| Command | Shortcut |
|---------|----------|
| Start MCP Server | `Ctrl+Shift+P` → "MCP: Start Server" |
| List Active Servers | `Ctrl+Shift+P` → "MCP: List Servers" |
| Restart Server | `Ctrl+Shift+P` → "MCP: Restart Server" |
| Kill All Servers | `Ctrl+Shift+P` → "MCP: Kill All Servers" |

### MSSQL Extension Commands

| Command | Shortcut |
|---------|----------|
| Connect to Database | `Ctrl+Shift+P` → "SQL: Connect" |
| Create Query | `Ctrl+Shift+P` → "SQL: New Query" |
| Execute Query | `Ctrl+Shift+E` (in query editor) |
| Disconnect | `Ctrl+Shift+P` → "SQL: Disconnect" |

### Copilot Chat Integration

```
@mssql <your_sql_question>
```

**Examples:**
- `@mssql Show the schema of the TownOfWileyBudget2026 table`
- `@mssql Count rows by FundOrDepartment`
- `@mssql Generate a C# LINQ query for budget totals by year`

## Additional Resources

- **MSSQL MCP GitHub:** https://github.com/modelcontextprotocol/servers/tree/main/src/sql-server
- **VS Code MCP Docs:** https://code.visualstudio.com/docs/copilot/customization/mcp-servers
- **SQL Server Connection Strings:** https://www.connectionstrings.com/sql-server/
- **Microsoft Docs - MSSQL Extension:** https://learn.microsoft.com/en-us/sql/tools/visual-studio-code-extensions/mssql/mssql-extension-visual-studio-code

## Next Steps

1. ✅ **Set MSSQL_CONNECTION_STRING** environment variable
2. ✅ **Test MSSQL connection** via VS Code SQL Explorer
3. ✅ **Start MCP server** and verify status
4. ✅ **Query via Copilot Chat** using `@mssql` prefix
5. ✅ **Integrate into DashboardViewModel** for budget data queries

---

**Status:** Ready to use once environment variable is configured.  
**Support:** See `.vscode/copilot-instructions.md` for workspace guidelines.
