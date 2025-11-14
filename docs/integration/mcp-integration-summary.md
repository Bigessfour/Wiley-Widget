<![CDATA[# MCP Integration Summary for Wiley Widget

**Last Updated**: November 14, 2025  
**Status**: Active Development

## Overview

This document provides a comprehensive summary of all Model Context Protocol (MCP) integrations within the Wiley Widget project, including setup instructions, usage patterns, and maintenance procedures.

## Active MCP Servers

### 1. C# MCP (`csharp-mcp`)

**Purpose**: Execute C# scripts and snippets for testing and validation

**Configuration**:
```json
{
  "command": "docker",
  "args": [
    "run", "-i", "--rm",
    "-v", "${workspaceFolder}:/scripts:ro",
    "ghcr.io/infinityflowapp/csharp-mcp:latest"
  ]
}
```

**Usage**:
- Code validation during development
- Running CSX test scripts in Docker
- Quick prototyping and experimentation

**VS Code Tasks**:
- `mcp:test-csharp-eval` - Test MCP server connection
- `csx:run-*` - Run specific CSX test scripts

### 2. Filesystem MCP (`@modelcontextprotocol/server-filesystem`)

**Purpose**: File system operations with audit trails and git-style diffs

**Configuration**:
```json
{
  "command": "npx",
  "args": ["-y", "@modelcontextprotocol/server-filesystem", "C:\\Users\\biges\\Desktop\\Wiley_Widget"]
}
```

**Usage**:
- Reading and writing files with diffs
- Directory traversal and analysis
- Batch file operations
- Search and replace with validation

**Tools**:
- `mcp_filesystem_read_text_file` - Read file contents
- `mcp_filesystem_edit_file` - Edit with git-style diffs
- `mcp_filesystem_write_file` - Create/overwrite files
- `mcp_filesystem_search_files` - Search by pattern

### 3. Sequential Thinking MCP (`sequential-thinking`)

**Purpose**: Complex problem decomposition with step-by-step reasoning

**Configuration**:
```json
{
  "command": "npx",
  "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
}
```

**Usage**:
- Debugging complex issues
- Test strategy design
- Architecture decisions
- Root cause analysis

**Integration**: Combined with `csharp-mcp` for validated problem-solving

### 4. GitHub MCP (`github`)

**Purpose**: GitHub repository operations and CI/CD integration

**Configuration**:
```json
{
  "command": "npx",
  "args": ["-y", "@modelcontextprotocol/server-github"],
  "env": {
    "GITHUB_PERSONAL_ACCESS_TOKEN": "${env:GITHUB_TOKEN}"
  }
}
```

**Usage**:
- Monitoring CI/CD workflow runs
- Analyzing build failures
- Managing pull requests
- Repository operations

**Tools**:
- `mcp_github_list_workflow_runs` - List CI runs
- `mcp_github_get_job_logs` - Fetch failure logs
- `mcp_github_create_issue` - Create issues
- `mcp_github_fork_repository` - Fork repos

### 5. SQL Server MCP (`mssql`) **[NEW]**

**Purpose**: Database testing and validation integration

**Setup**: Run `scripts/tools/setup-sql-mcp.ps1 -TestConnection -CreateProfile`

**Configuration**:
```json
{
  "command": "npx",
  "args": ["-y", "@modelcontextprotocol/server-mssql"],
  "env": {
    "MSSQL_CONNECTION_TIMEOUT": "30"
  }
}
```

**Usage**:
- Connect to SQL Server instances
- Query database schemas
- Validate database initialization
- Test EF Core migrations

**Tools**:
- `mssql_connect` - Establish connection
- `mssql_list_databases` - List databases
- `mssql_list_tables` - List tables
- `mssql_query` - Execute queries

**VS Code Task**: `mcp:setup-sql-server`

### 6. Syncfusion MCP (Custom) **[PLANNED]**

**Purpose**: Automated Syncfusion WinUI component validation

**Status**: Design phase (see `docs/integration/syncfusion-mcp-server-design.md`)

**Planned Tools**:
- `syncfusion_validate_theme` - SfSkinManager validation
- `syncfusion_analyze_datagrid` - SfDataGrid configuration analysis
- `syncfusion_check_license` - License validation
- `syncfusion_parse_xaml` - Component detection
- `syncfusion_generate_report` - CI/CD reporting

**Implementation**: Phase 1 starts November 18, 2025 (tentative)

## Integration Patterns

### Pattern 1: Sequential Thinking + C# MCP (Debugging)

**Workflow**:
1. Use `sequential-thinking` to decompose problem
2. Test each hypothesis with `csharp-mcp`
3. Iterate until solution found

**Example**:
```
Copilot: "Use sequential-thinking to debug why Prism navigation fails"
→ Generates step-by-step analysis plan
→ Test each step with csharp-mcp evaluation
→ Identify root cause
```

**Helper**: `Invoke-SequentialCSharpDebug -Problem "..." -CodePath "..."`

### Pattern 2: GitHub MCP + CI/CD Feedback Loop

**Workflow**:
1. Push changes to GitHub
2. Monitor CI with `mcp_github_list_workflow_runs`
3. Analyze failures with `mcp_github_get_job_logs`
4. Fix issues and re-push

**Automation**: `scripts/tools/analyze-ci-failures.ps1`

### Pattern 3: Filesystem MCP + Documentation

**Workflow**:
1. Read multiple files with `mcp_filesystem_read_multiple_files`
2. Analyze content structure
3. Edit with `mcp_filesystem_edit_file` (git-style diffs)
4. Validate changes with Copilot review

**Benefit**: Complete audit trail of AI-generated changes

### Pattern 4: SQL Server MCP + Database Testing

**Workflow**:
1. Connect to SQL Server with `mssql_connect`
2. List databases and tables
3. Execute validation queries
4. Generate test data

**Integration**: Supports EF Core migration testing

## Maintenance Procedures

### Monthly Tasks

#### 1. Update Docker Images

**Script**: `scripts/maintenance/update-mcp-images.ps1`

**VS Code Task**: `mcp:update-docker-images`

**What it does**:
- Pulls latest MCP Docker images
- Compares versions with current
- Updates configuration if needed
- Runs health checks
- Generates update report

**Schedule**: 1st of each month (automated reminder)

#### 2. Audit GitHub Token Scopes

**Script**: `scripts/maintenance/audit-github-token.ps1`

**VS Code Task**: `mcp:audit-github-token`

**What it does**:
- Validates GitHub PAT is still valid
- Checks token scopes against required permissions
- Identifies missing or excessive scopes
- Generates security audit report
- Logs to `logs/mcp-audit-{date}.log`

**Required Scopes**:
- `repo` (full control)
- `workflow` (GitHub Actions access)
- `read:org` (organization access)

**Schedule**: Monthly security audit

### Quarterly Tasks

#### 1. MCP Server Health Check

**Script**: `scripts/tools/validate-mcp-setup.ps1 -Verbose`

**What it does**:
- Tests all MCP server connections
- Validates configuration files
- Checks Docker image availability
- Runs integration tests
- Generates comprehensive health report

#### 2. Sequential Thinking Integration Review

**Script**: `scripts/tools/integrate-sequential-thinking.ps1 -Validate`

**What it does**:
- Verifies sequential-thinking + csharp-mcp integration
- Tests example workflows
- Updates helper functions
- Regenerates documentation

### Ad-Hoc Tasks

#### Fix MCP Issues

```powershell
.\scripts\tools\validate-mcp-setup.ps1 -FixIssues
```

Automatically detects and fixes common MCP configuration problems.

#### Regenerate AI-Fetchable Manifest

```powershell
python scripts/tools/generate_repo_urls.py -o ai-fetchable-manifest.json
```

Updates the manifest used by Copilot for code discovery.

## VS Code Tasks Reference

### Setup and Configuration

| Task | Purpose | Frequency |
|------|---------|-----------|
| `mcp:validate-setup` | Validate all MCP servers | Weekly |
| `mcp:update-images` | Update Docker images | Monthly |
| `mcp:fix-issues` | Auto-fix configuration | As needed |
| `mcp:setup-sql-server` | Configure SQL Server MCP | Once |
| `mcp:integrate-sequential-thinking` | Setup integration helpers | Once |

### Testing and Validation

| Task | Purpose | Frequency |
|------|---------|-----------|
| `mcp:test-csharp-eval` | Test C# MCP connection | Daily |
| `mcp:audit-github-token` | Audit GitHub token scopes | Monthly |

### Utilities

| Task | Purpose |
|------|---------|
| `generate-ai-fetchable-manifest` | Generate code discovery manifest |
| `Analyze CI Failures` | Analyze GitHub Actions failures |

## Environment Variables

### Required

```bash
# GitHub MCP
GITHUB_TOKEN=ghp_xxxxxxxxxxxxx

# Syncfusion (when implemented)
SYNCFUSION_LICENSE_KEY=xxxxxxxxxxxxx

# SQL Server MCP
MSSQL_CONNECTION_TIMEOUT=30
```

### Optional

```bash
# C# MCP
CSX_ALLOWED_PATH=/scripts
WW_REPO_ROOT=/path/to/workspace
WW_LOGS_DIR=/path/to/logs

# Filesystem MCP
FILESYSTEM_ALLOWED_DIRS=/path1:/path2
```

## Security Considerations

### 1. GitHub Token Management

- **Storage**: Store in `secrets/github_token` (excluded from git)
- **Scope Auditing**: Monthly review with `audit-github-token.ps1`
- **Rotation**: Rotate every 90 days minimum
- **Access**: Read-only where possible

### 2. SQL Server Credentials

- **Authentication**: Use Windows Integrated Authentication
- **Connection Strings**: Never store in version control
- **Profiles**: Store in `config/sql-profiles/` (gitignored)

### 3. Syncfusion License Key

- **Storage**: Environment variable only
- **Logging**: Never log license keys
- **Validation**: Verify format before use

### 4. Docker Image Security

- **Source**: Use official MCP images from npm/ghcr.io
- **Updates**: Monthly security updates
- **Scanning**: Automated vulnerability scanning via Trunk

## Troubleshooting

### MCP Server Not Connecting

**Symptoms**: "MCP server failed to start" error in VS Code

**Solutions**:
1. Run `mcp:validate-setup` task
2. Check Docker is running (for C# MCP)
3. Verify npm packages installed: `npx -y @modelcontextprotocol/server-filesystem --help`
4. Restart VS Code
5. Run `mcp:fix-issues` for auto-repair

### GitHub MCP Authentication Failed

**Symptoms**: "401 Unauthorized" when using GitHub tools

**Solutions**:
1. Run `mcp:audit-github-token` to validate token
2. Check `GITHUB_TOKEN` environment variable is set
3. Verify token has required scopes
4. Regenerate token if expired

### C# MCP Docker Issues

**Symptoms**: "Container not found" or "Image pull failed"

**Solutions**:
1. Run `docker pull ghcr.io/infinityflowapp/csharp-mcp:latest`
2. Check Docker daemon is running
3. Verify disk space available
4. Run `mcp:update-docker-images` task

### Sequential Thinking Integration Not Working

**Symptoms**: Helper functions not found or examples missing

**Solutions**:
1. Run `mcp:integrate-sequential-thinking` task
2. Import helper module: `Import-Module scripts/tools/Invoke-SequentialCSharp.ps1`
3. Verify `sequential-thinking` MCP is configured
4. Check examples directory: `scripts/examples/sequential-thinking/`

## Performance Optimization

### Filesystem MCP

- **Batch Operations**: Use `mcp_filesystem_read_multiple_files` instead of multiple single reads
- **Caching**: Filesystem MCP caches file reads within session
- **Path Optimization**: Use absolute paths to avoid resolution overhead

### C# MCP

- **Image Caching**: Docker caches images locally after first pull
- **Volume Mounting**: Use read-only mounts (`:ro`) when possible
- **CSX Compilation**: C# scripts compile once per container run

### GitHub MCP

- **Rate Limiting**: GitHub API has rate limits (5000/hour authenticated)
- **Batch Queries**: Combine related queries when possible
- **Caching**: Use `--limit` parameter to reduce API calls

## Future Enhancements

### Phase 1 (Q4 2025)
- ✅ SQL Server MCP integration
- ✅ Sequential thinking + C# MCP workflows
- ⏳ Syncfusion MCP Phase 1 implementation

### Phase 2 (Q1 2026)
- Custom PowerShell MCP for script analysis
- Azure DevOps MCP for CI/CD integration
- Enhanced Syncfusion validation (Phases 2-3)

### Phase 3 (Q2 2026)
- Visual validation MCP (screenshot comparison)
- Performance profiling MCP
- Auto-fix capabilities for common issues

## Documentation Links

- [MCP Specification](https://modelcontextprotocol.io/specification)
- [GitHub MCP Server](https://github.com/modelcontextprotocol/servers/tree/main/src/github)
- [Filesystem MCP Server](https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem)
- [Sequential Thinking MCP](https://github.com/modelcontextprotocol/servers/tree/main/src/sequential-thinking)
- [Syncfusion MCP Design](./syncfusion-mcp-server-design.md)
- [SQL Server MCP Setup](./sql-server-mcp.md)
- [CI/CD Feedback Loop](../../scripts/tools/README.md)

## Changelog

### 2025-11-14
- Added SQL Server MCP integration
- Created sequential thinking + C# MCP integration
- Designed custom Syncfusion MCP server
- Added monthly Docker image update automation
- Implemented GitHub token scope auditing
- Created comprehensive VS Code tasks
- Documented all integration patterns

### 2025-11-12
- Initial MCP infrastructure setup
- C# MCP Docker integration
- Filesystem MCP configuration
- GitHub MCP CI/CD integration
- Basic documentation

---

**Maintained By**: Development Team  
**Review Schedule**: Quarterly  
**Last Review**: November 14, 2025
]]>