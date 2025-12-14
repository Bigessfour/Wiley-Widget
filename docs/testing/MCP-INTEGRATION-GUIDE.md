# MCP Integration Guide for Wiley Widget Testing

## Overview

This guide documents how Model Context Protocol (MCP) servers enhance the Wiley Widget testing workflow, specifically for Phase 4 (Testing & Validation).

## Current MCP Stack

### 1. **GitHub MCP** (`mcp/github`)

**Purpose:** Repository management, issue/PR automation, code context
**Integration Points:**

- Automated test result reporting via GitHub Issues
- PR validation with test coverage requirements
- Code review automation for test quality

**Usage in Testing:**

```bash
# Via Docker
docker run --rm -e GITHUB_TOKEN=$env:GITHUB_TOKEN \
  mcp/github \
  create-issue --repo Bigessfour/Wiley-Widget \
  --title "Test Coverage Below 80%" \
  --body "$(cat coverage/report.txt)"
```

**Tasks Integration:**

- `git:update` - Commit test updates
- `Analyze CI Failures` - Query GitHub Actions logs

---

### 2. **Filesystem MCP** (`@modelcontextprotocol/server-filesystem`)

**Purpose:** Secure file operations with git-style diffs
**Integration Points:**

- All test file creation/editing
- Test result parsing
- Coverage report generation

**Usage in Testing:**

```javascript
// Mandatory for all file operations
mcp_filesystem_write_file({
  path: "c:/path/to/test/file.cs",
  content: "// Test content",
});

mcp_filesystem_edit_file({
  path: "c:/path/to/file.cs",
  edits: [
    {
      oldText: "old test code",
      newText: "improved test code",
    },
  ],
});
```

**Enforcement:** See `.vscode/copilot-mcp-rules.md` for mandatory usage rules.

---

### 3. **C# MCP** (`ghcr.io/infinityflowapp/csharp-mcp:latest`)

**Purpose:** Execute C# scripts for testing and validation
**Integration Points:**

- Pre-test validation (syntax, compile checks)
- Dynamic test generation
- Test data creation

**Usage in Testing:**

```bash
# Run validation script
docker run --rm \
  -v "${PWD}:/scripts:ro" \
  -v "${PWD}/logs:/logs:rw" \
  -e WW_REPO_ROOT=/scripts \
  -e WW_LOGS_DIR=/logs \
  ghcr.io/infinityflowapp/csharp-mcp:latest \
  scripts/examples/csharp/validate-test-structure.csx
```

**VS Code Tasks:**

- `csx:run-60P-dashboardviewmodel-test`
- `csx:run-61P-quickbooksservice-test`
- All `csx:run-*` tasks

**Benefits:**

- ✅ Validate C# test code before xUnit execution
- ✅ Generate test scaffolding dynamically
- ✅ Mock data generation for integration tests

---

### 4. **Everything MCP** (`@modelcontextprotocol/server-everything`)

**Purpose:** Full MCP protocol testing and validation
**Integration Points:**

- End-to-end test pipeline validation
- Multi-server integration tests
- Protocol conformance checks

**Usage in Testing:**

```bash
# Test full context pipeline
npx @modelcontextprotocol/server-everything \
  --test-resources \
  --test-tools \
  --test-prompts
```

**Use Cases:**

- Validate MCP server health before test runs
- Test complex workflows (GitHub → Filesystem → C# MCP)
- Debug MCP integration issues

---

### 5. **Sequential Thinking MCP** (`@modelcontextprotocol/server-sequential-thinking`)

**Purpose:** Structured problem-solving for complex test scenarios
**Integration Points:**

- Test design and planning
- Debugging test failures
- Root cause analysis

**Usage in Testing:**

```javascript
mcp_sequential_th_sequentialthinking({
  thought: "Analyzing why QuickBooksService tests are failing...",
  thoughtNumber: 1,
  totalThoughts: 5,
  nextThoughtNeeded: true,
});
```

**Workflow Integration:**

- Design non-whitewash test cases (3+ scenarios)
- Break down complex test failures
- Plan integration test sequences

**Example:**

```
Problem: Dashboard navigation test fails intermittently
→ Use Sequential Thinking MCP to analyze:
  1. Identify timing issues
  2. Check thread synchronization
  3. Review UI threading and dispatcher usage
  4. Propose fix with new test cases
```

---

## Proposed Enhancement: SQL Server MCP

### **`@modelcontextprotocol/server-mssql`** (NOT YET INSTALLED)

**Purpose:** Direct SQL Server access for integration testing
**Why Needed:**

- Test database initialization (DatabaseInitializer)
- Validate EF Core migrations
- Query test data state
- Performance testing

**Installation:**

```bash
npm install -g @modelcontextprotocol/server-mssql
```

**Configuration:**

```json
{
  "mssql": {
    "command": "npx",
    "args": [
      "@modelcontextprotocol/server-mssql",
      "--connection-string",
      "Server=localhost;Database=WileyWidget;User=sa;Password=WileyWidget!2025;TrustServerCertificate=true"
    ],
    "env": {
      "MSSQL_CONNECTION_STRING": "Server=localhost,1433;..."
    }
  }
}
```

**Usage in Testing:**

```javascript
// Query test database state
mcp_mssql_query({
  query: "SELECT COUNT(*) FROM Invoices WHERE SyncStatus = 'Pending'",
});

// Validate migration
mcp_mssql_query({
  query: "SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId DESC",
});
```

**Integration with Docker Compose:**

```yaml
# docker-compose.yml already has WILEY_DB
# SQL Server MCP can connect directly:
services:
  test:
    environment:
      - MSSQL_CONNECTION_STRING=Server=db;Database=WileyWidget;User=sa;Password=WileyWidget!2025;TrustServerCertificate=true
```

**Tasks to Add:**

```json
{
  "label": "mcp:validate-database",
  "type": "shell",
  "command": "npx",
  "args": ["@modelcontextprotocol/server-mssql", "--query", "SELECT @@VERSION"]
}
```

---

## MCP in Phase 4 Testing Workflow

### **Day 1-2: Unit Tests**

**MCP Stack:**

- **Filesystem MCP** → Create test files with git diffs
- **C# MCP** → Validate syntax before xUnit run
- **Sequential Thinking MCP** → Design 3+ test cases per method

**Workflow:**

1. Use Sequential Thinking to plan test structure
2. Generate test file with Filesystem MCP (tracked diffs)
3. Validate with C# MCP before committing
4. Commit with GitHub MCP automation

### **Day 3-4: Integration Tests**

**MCP Stack:**

- **SQL Server MCP** (proposed) → Query database state
- **C# MCP** → Run EF Core validation scripts
- **Docker Compose** → Spin up WILEY_DB container
- **Filesystem MCP** → Read/write test data files

**Workflow:**

1. Start Docker Compose database
2. Use SQL Server MCP to validate connection
3. Run integration tests with xUnit
4. Query results with SQL Server MCP
5. Generate coverage report (Filesystem MCP)

### **Day 5: UI Smoke Tests**

**MCP Stack:**

- **Everything MCP** → Full pipeline validation
- **Filesystem MCP** → Parse Playwright test results
- **GitHub MCP** → Report UI test failures

**Workflow:**

1. Run Playwright tests via Docker Compose
2. Parse results with Filesystem MCP
3. Auto-create GitHub issue if failures (GitHub MCP)

### **Day 6: CI/CD Integration**

**MCP Stack:**

- **GitHub MCP** → Monitor workflow runs
- **Sequential Thinking MCP** → Analyze CI failures
- **Filesystem MCP** → Update CI config

**Workflow:**

```bash
# Monitor CI via GitHub MCP
gh run list --workflow=ci-optimized.yml --limit=5

# If failures, analyze with Sequential Thinking
# Update .github/workflows/ci-optimized.yml with Filesystem MCP
# Commit with git:update task
```

---

## Security Best Practices

### **1. Environment Variables**

```powershell
# Validate all required env vars
.\scripts\tools\validate-mcp-setup.ps1

# Required for testing:
$env:GITHUB_TOKEN = "ghp_..."           # GitHub MCP
$env:CSX_ALLOWED_PATH = "C:\...\Wiley_Widget"  # C# MCP
$env:WW_REPO_ROOT = "C:\...\Wiley_Widget"      # C# MCP
$env:WW_LOGS_DIR = "C:\...\Wiley_Widget\logs"  # C# MCP
$env:MSSQL_CONNECTION_STRING = "Server=..."    # SQL Server MCP (proposed)
```

### **2. Filesystem Access Control**

- **Allowed:** `C:\Users\biges\Desktop\Wiley_Widget\**`
- **Excluded:**
  - `secrets/*`
  - `.env`
  - `*.pfx`, `*.p12`
  - `TestResults/` (read-only)

### **3. GitHub Token Scopes**

**Required:**

- `repo` (full repository access)
- `workflow` (trigger CI/CD)
- `read:org` (optional, for team features)

**Validate:**

```bash
gh auth status
```

---

## Troubleshooting MCP Issues

### **Error: "MCP server not responding"**

```powershell
# Validate setup
.\scripts\tools\validate-mcp-setup.ps1 -Verbose

# Check Docker containers
docker ps
docker logs WILEY_DB
```

### **Error: "Filesystem access denied"**

```
Cause: Path outside allowed directories
Fix: Ensure path starts with C:\Users\biges\Desktop\Wiley_Widget
```

### **Error: "C# MCP compilation failed"**

```bash
# Check script syntax locally
dotnet-script scripts/examples/csharp/test-file.csx

# Review logs
cat logs/csx-execution.log
```

### **Error: "GitHub MCP auth failed"**

```powershell
# Refresh token
gh auth refresh -h github.com -s repo,workflow

# Update env var
$env:GITHUB_TOKEN = (gh auth token)
```

---

## Performance Optimization

### **1. Docker Image Caching**

```bash
# Pre-pull images before testing
docker pull ghcr.io/infinityflowapp/csharp-mcp:latest
docker pull mcp/github
```

### **2. NPM Package Caching**

```bash
# Pre-cache MCP packages
npx --yes @modelcontextprotocol/server-filesystem
npx --yes @modelcontextprotocol/server-everything
npx --yes @modelcontextprotocol/server-sequential-thinking
```

### **3. Volume Mount Optimization**

```yaml
# Use cached volumes for better performance
volumes:
  - .:/src:cached # Instead of :ro
  - ~/.nuget/packages:/root/.nuget/packages:cached
```

---

## VS Code Tasks for MCP

```json
{
  "label": "mcp:validate-setup",
  "type": "shell",
  "command": "pwsh",
  "args": [
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", "${workspaceFolder}/scripts/tools/validate-mcp-setup.ps1",
    "-Verbose"
  ],
  "group": "build"
},
{
  "label": "mcp:update-images",
  "type": "shell",
  "command": "pwsh",
  "args": [
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", "${workspaceFolder}/scripts/tools/validate-mcp-setup.ps1",
    "-UpdateImages"
  ],
  "group": "build"
},
{
  "label": "mcp:test-csharp-eval",
  "type": "shell",
  "command": "docker",
  "args": [
    "run", "--rm",
    "-v", "${workspaceFolder}:/scripts:ro",
    "-e", "WW_REPO_ROOT=/scripts",
    "ghcr.io/infinityflowapp/csharp-mcp:latest",
    "-c", "Console.WriteLine(\"Hello from C# MCP\");"
  ],
  "group": "test"
}
```

---

## Integration with Copilot Instructions

From `.vscode/copilot-mcp-rules.md`:

### **Mandatory MCP Filesystem Usage**

```
✅ ALWAYS use mcp_filesystem_* for file operations
❌ NEVER use read_file, grep_search, create_file, replace_string_in_file
```

### **Pre-Flight Checklist (before EVERY file operation)**

```
[ ] activate_file_reading_tools()
[ ] activate_directory_and_file_creation_tools()
[ ] Am I using mcp_filesystem_* function?
[ ] Path is absolute
[ ] No terminal commands for file I/O
```

---

## Metrics and KPIs

Track MCP effectiveness in testing:

| Metric                         | Target             | Measurement                                    |
| ------------------------------ | ------------------ | ---------------------------------------------- |
| **File Operation Consistency** | 100% MCP usage     | Audit git diffs for tool provenance            |
| **C# Pre-validation Success**  | >95% pass rate     | CSX script execution before xUnit              |
| **Test Generation Speed**      | <5 min per service | Time from Sequential Thinking → committed test |
| **CI/CD Feedback Loop**        | <10 min            | GitHub MCP query → analysis → fix              |

---

## Next Steps

1. **Install SQL Server MCP:**

   ```bash
   npm install -g @modelcontextprotocol/server-mssql
   ```

2. **Update MCP config** to include MSSQL server

3. **Add validation task** to `.vscode/tasks.json`:

   ```json
   { "label": "mcp:validate-setup", ... }
   ```

4. **Run validation:**

   ```powershell
   .\scripts\tools\validate-mcp-setup.ps1 -FixIssues -UpdateImages
   ```

5. **Test SQL Server MCP** with Docker Compose:
   ```bash
   docker-compose up -d db
   npx @modelcontextprotocol/server-mssql --connection-string "Server=localhost;..."
   ```

---

**Last Updated:** November 14, 2025
**Status:** Active - Phase 4 Day 1 Complete
**Validation:** Run `validate-mcp-setup.ps1` weekly
