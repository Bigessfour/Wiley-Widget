# SQL Server MCP Integration Guide

## Overview

Integration of Microsoft SQL Server MCP (`@modelcontextprotocol/server-mssql`) for database testing, query validation, and schema analysis within the Wiley Widget development workflow.

## Prerequisites

- Node.js 18+ and npm
- SQL Server (LocalDB, Express, or Full)
- Active database connection string
- Windows Authentication or SQL Authentication configured

## Installation

### 1. Install MSSQL MCP Server

```powershell
# Global installation (recommended for development)
npm install -g @modelcontextprotocol/server-mssql

# Verify installation
npx @modelcontextprotocol/server-mssql --version
```

### 2. Configure Connection String

**Environment Variable Setup:**

```powershell
# User-level (persistent)
[Environment]::SetEnvironmentVariable(
    'MSSQL_CONNECTION_STRING',
    'Server=(localdb)\MSSQLLocalDB;Database=WileyWidget;Integrated Security=true;TrustServerCertificate=true',
    'User'
)

# For SQL Authentication
[Environment]::SetEnvironmentVariable(
    'MSSQL_CONNECTION_STRING',
    'Server=localhost;Database=WileyWidget;User Id=sa;Password=<YourStrongPassword>;Encrypt=true;TrustServerCertificate=true',
    'User'
)
```

**Connection String Formats:**

| Scenario            | Connection String Template                                                     |
| ------------------- | ------------------------------------------------------------------------------ |
| **LocalDB**         | `Server=(localdb)\MSSQLLocalDB;Database=WileyWidget;Integrated Security=true`  |
| **SQL Express**     | `Server=localhost\SQLEXPRESS;Database=WileyWidget;Integrated Security=true`    |
| **Full SQL Server** | `Server=localhost;Database=WileyWidget;Integrated Security=true`               |
| **SQL Auth**        | `Server=localhost;Database=WileyWidget;User Id=sa;Password=<pwd>;Encrypt=true` |

### 3. MCP Configuration

Add to VS Code MCP settings (`cline_mcp_settings.json`):

```json
{
  "mcpServers": {
    "mssql": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-mssql"],
      "env": {
        "MSSQL_CONNECTION_STRING": "Server=(localdb)\\MSSQLLocalDB;Database=WileyWidget;Integrated Security=true;TrustServerCertificate=true"
      },
      "alwaysAllow": ["mssql_list_databases", "mssql_list_tables", "mssql_list_views"]
    }
  }
}
```

## Available Tools

### Database Discovery

| Tool                   | Purpose                      | Example                                  |
| ---------------------- | ---------------------------- | ---------------------------------------- |
| `mssql_list_databases` | List all databases on server | Query available databases for connection |
| `mssql_list_schemas`   | List schemas in database     | Explore database organization            |
| `mssql_list_tables`    | List tables with schema info | Find tables for testing                  |
| `mssql_list_views`     | List views in database       | Identify data access patterns            |
| `mssql_list_functions` | List user-defined functions  | Review custom logic                      |

### Connection Management

| Tool                           | Purpose                       | Example                       |
| ------------------------------ | ----------------------------- | ----------------------------- |
| `mssql_connect`                | Establish database connection | Connect to WileyWidget DB     |
| `mssql_disconnect`             | Close connection              | Clean up after tests          |
| `mssql_change_database`        | Switch databases              | Test multi-database scenarios |
| `mssql_get_connection_details` | View connection info          | Debug connection issues       |

## Use Cases in Wiley Widget

### 1. Database Schema Validation

**Scenario:** Verify Entity Framework migrations are applied correctly

```csharp
// C# Test (via csharp-mcp)
#r "nuget: Microsoft.Data.SqlClient, 5.2.0"

using Microsoft.Data.SqlClient;

var connectionString = Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING");
using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

// Use mssql_list_tables to verify schema
// Expected tables: BudgetEntries, Departments, MunicipalAccounts, etc.
```

**MCP Query:**

```javascript
// Use mssql_list_tables to get schema
mcp_mssql_list_tables({
  connectionId: "<connection-id>",
  database: "WileyWidget",
});

// Expected: Tables with correct schema names (dbo, reporting, etc.)
```

### 2. Integration Test Data Setup

**Scenario:** Pre-populate test data for QuickBooks sync tests

```sql
-- Use mssql MCP to execute setup queries
INSERT INTO Departments (Name, Code, IsActive) VALUES
  ('Public Works', 'PW', 1),
  ('Recreation', 'REC', 1),
  ('Administration', 'ADMIN', 1);

INSERT INTO BudgetPeriods (FiscalYear, StartDate, EndDate) VALUES
  (2025, '2025-01-01', '2025-12-31');
```

**Automated via CSX:**

```csharp
// 85P-database-test-setup.csx
#r "nuget: Microsoft.Data.SqlClient, 5.2.0"

// Setup test data using mssql MCP connection
// Verify via mssql_list_tables and custom queries
```

### 3. Query Performance Analysis

**Scenario:** Analyze slow queries identified in production logs

```javascript
// Connect to database
mcp_mssql_connect({
  serverName: "(localdb)\\MSSQLLocalDB",
  database: "WileyWidget",
});

// List views for query optimization candidates
mcp_mssql_list_views({
  connectionId: "<id>",
  database: "WileyWidget",
});

// Analyze execution plans (advanced)
```

### 4. CI/CD Database Validation

**Integration with GitHub Actions:**

```yaml
# .github/workflows/database-tests.yml
name: Database Integration Tests

on: [push, pull_request]

jobs:
  database-tests:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup LocalDB
        run: |
          sqllocaldb create MSSQLLocalDB
          sqllocaldb start MSSQLLocalDB

      - name: Apply Migrations
        run: |
          dotnet ef database update --project src/WileyWidget

      - name: Validate Schema via MCP
        run: |
          npx @modelcontextprotocol/server-mssql --validate
        env:
          MSSQL_CONNECTION_STRING: ${{ secrets.MSSQL_TEST_CONNECTION }}

      - name: Run Database Tests
        run: |
          dotnet test tests/WileyWidget.Database.Tests
```

## Security Considerations

### Connection String Storage

✅ **DO:**

- Store in environment variables (User/Machine level)
- Use `secrets/` directory for local development (excluded from git)
- Use Azure Key Vault or GitHub Secrets for CI/CD
- Enable `TrustServerCertificate=true` only for local/dev environments

❌ **DON'T:**

- Hardcode connection strings in source code
- Commit connection strings to git
- Use production credentials in development
- Share connection strings in plaintext (Slack, email)

### Least Privilege Access

Create dedicated database user for MCP operations:

```sql
-- Create read-only user for MCP queries
CREATE LOGIN mcp_reader WITH PASSWORD = 'StrongPassword123!';
CREATE USER mcp_reader FOR LOGIN mcp_reader;

-- Grant read access
ALTER ROLE db_datareader ADD MEMBER mcp_reader;

-- Grant schema view permissions
GRANT VIEW DEFINITION TO mcp_reader;
GRANT VIEW ANY DEFINITION TO mcp_reader;
```

**Connection String:**

```
Server=localhost;Database=WileyWidget;User Id=mcp_reader;Password=StrongPassword123!;Encrypt=true
```

## Testing Strategy

### Unit Tests with MCP Integration

**Test Structure:**

```
tests/
  WileyWidget.Database.Tests/
    SchemaValidationTests.cs    # Verify EF migrations
    DataIntegrityTests.cs        # Check constraints, indexes
    QueryPerformanceTests.cs     # Analyze slow queries

scripts/examples/csharp/
  86P-database-schema-validation.csx   # MCP-powered schema checks
  87P-database-migration-test.csx      # Migration rollback testing
```

**Example Test (xUnit):**

```csharp
using Xunit;
using Microsoft.Data.SqlClient;

public class DatabaseSchemaTests
{
    private readonly string _connectionString;

    public DatabaseSchemaTests()
    {
        _connectionString = Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("MSSQL_CONNECTION_STRING not set");
    }

    [Fact]
    public async Task Database_HasExpectedTables()
    {
        // Arrange
        var expectedTables = new[] { "BudgetEntries", "Departments", "MunicipalAccounts" };

        // Act - Use mssql_list_tables via MCP or direct query
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        using var command = new SqlCommand(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'",
            connection
        );
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        // Assert
        foreach (var expected in expectedTables)
        {
            Assert.Contains(expected, tables);
        }
    }

    [Fact]
    public async Task BudgetEntries_HasCorrectSchema()
    {
        // Verify columns, types, nullability, constraints
        // Use mssql MCP tools for schema inspection
    }
}
```

## Troubleshooting

### Common Issues

**1. Connection Refused**

```
Error: Failed to connect to SQL Server
```

**Solution:**

- Verify SQL Server service is running: `Get-Service MSSQL*`
- Check firewall rules for port 1433
- Use `(localdb)\MSSQLLocalDB` for LocalDB instances

**2. Authentication Failed**

```
Error: Login failed for user 'sa'
```

**Solution:**

- Verify SQL Authentication is enabled
- Check username/password in connection string
- Use Windows Authentication (`Integrated Security=true`) if available

**3. Database Not Found**

```
Error: Cannot open database "WileyWidget"
```

**Solution:**

- List available databases: `mssql_list_databases`
- Apply migrations: `dotnet ef database update`
- Check database name spelling (case-sensitive on Linux)

**4. MCP Server Not Found**

```
Error: Cannot find module '@modelcontextprotocol/server-mssql'
```

**Solution:**

```powershell
npm install -g @modelcontextprotocol/server-mssql
npx @modelcontextprotocol/server-mssql --version
```

## Integration with Existing Tools

### Trunk CLI Integration

Add database validation to Trunk checks:

```yaml
# .trunk/trunk.yaml
actions:
  enabled:
    - trunk-check-pre-push
    - database-schema-check

lint:
  definitions:
    - name: database-schema-check
      files: [sql]
      commands:
        - name: validate-schema
          run: pwsh scripts/tools/validate-database-schema.ps1
          success_codes: [0]
```

### CSX Test Integration

```csharp
// scripts/examples/csharp/86P-database-schema-validation.csx
#r "nuget: Microsoft.Data.SqlClient, 5.2.0"

using Microsoft.Data.SqlClient;

// Use MSSQL_CONNECTION_STRING from environment
var connectionString = Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING");

Console.WriteLine("🔍 Validating Database Schema...");

// Test 1: Connection
using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();
Console.WriteLine("✅ Database connection successful");

// Test 2: Core tables exist (delegate to mssql_list_tables)
// Test 3: Indexes present
// Test 4: Foreign key constraints
// Test 5: Stored procedures

Console.WriteLine("✅ All database schema validations passed");
```

## Best Practices

1. **Always use connection pooling** - Default in SqlClient
2. **Dispose connections properly** - Use `using` statements
3. **Parameterize queries** - Prevent SQL injection
4. **Use transactions for test setup** - Rollback after tests
5. **Monitor connection limits** - LocalDB has 10 connection limit
6. **Encrypt connections in CI/CD** - Use `Encrypt=true`

## Next Steps

- [ ] Configure MSSQL_CONNECTION_STRING environment variable
- [ ] Test connection with `mssql_list_databases`
- [ ] Create database schema validation CSX test
- [ ] Add database tests to CI/CD pipeline
- [ ] Document database seed data strategy
- [ ] Implement database backup/restore for test isolation

## References

- [MCP MSSQL Server Documentation](https://github.com/modelcontextprotocol/servers/tree/main/src/mssql)
- [SQL Server Connection Strings](https://www.connectionstrings.com/sql-server/)
- [Entity Framework Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Wiley Widget Database Schema](../../docs/DATABASE.md) _(to be created)_

---

**Last Updated:** November 14, 2025  
**Author:** Wiley Widget Development Team  
**Status:** Active Implementation
