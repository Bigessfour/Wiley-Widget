# Wiley Widget Project Guidelines

## ⚠️ FILESYSTEM MCP MANDATORY ENFORCEMENT

**CRITICAL: ALL FILE OPERATIONS MUST USE MCP FILESYSTEM TOOLS**

**BEFORE ANY FILE OPERATION:**

1. **Activate filesystem tools:**
   ```javascript
   activate_file_reading_tools()  // For reads
   activate_directory_and_file_creation_tools()  // For writes/edits
   ```

2. **Self-check:**
   - Am I using `mcp_filesystem_*` function?
   - If NO → STOP and switch to MCP tool
   - If YES → Proceed

### **PROHIBITED Tools (❌ NEVER USE FOR FILES):**
- `read_file` → Use `mcp_filesystem_read_text_file`
- `grep_search` → Use `mcp_filesystem_search_files`
- `file_search` → Use `mcp_filesystem_search_files`
- Terminal commands for file I/O → Use MCP tools

### **Enforcement Level: STRICT - Zero Tolerance**

**Why mandatory:**
- ✅ Git-style diffs for all changes
- ✅ Atomic operations with rollback
- ✅ Audit trail for all file modifications
- ✅ Consistent tool usage across conversations

**See `.vscode/copilot-mcp-rules.md` for complete enforcement rules and violation examples.**

---

## MCP Tools Integration

### 🔧 **Automatic MCP Tool Usage**

The following MCP (Model Context Protocol) tools are automatically available and should be used proactively in the development workflow:

#### **C# MCP Server (`mcp_csharp-mcp_eval_c_sharp`)**
- **When to use**: Automatically evaluate C# code snippets, test scripts, or small programs
- **Integration**: Use for validating C# syntax, running quick tests, or prototyping code before implementation
- **Workflow placement**: During code review, testing phases, or when analyzing C# files

#### **Filesystem MCP (`mcp_filesystem_*`)**
- **When to use**: For file system operations like reading, writing, searching, or analyzing files
- **Integration**: Use for bulk file operations, directory analysis, or when working with large codebases
- **Workflow placement**: During project analysis, file management, or when exploring workspace structure

#### **Sequential Thinking MCP (`mcp_sequential-th_sequentialthinking`)**
- **When to use**: For complex problem-solving requiring step-by-step analysis and iterative refinement
- **Integration**: Use for architectural decisions, debugging complex issues, or multi-step implementation planning
- **Workflow placement**: During design phases, troubleshooting, or when breaking down complex tasks

### 📋 **MCP Tool Usage Guidelines**

- **Proactive Usage**: Use these tools automatically when their capabilities would benefit the task
- **No Explicit Request Required**: Integrate tool usage naturally into responses without waiting for user prompts
- **Fallback Strategy**: If MCP tools fail or are unavailable, gracefully fall back to standard tools
- **Documentation**: Log MCP tool usage in commit messages when tools contribute to solutions

---

## ⚠️ FILESYSTEM MCP MANDATORY ENFORCEMENT

### **CRITICAL: ALL FILE OPERATIONS MUST USE MCP FILESYSTEM TOOLS**

**BEFORE ANY FILE OPERATION:**

1. **Activate filesystem tools:**
   ```javascript
   activate_file_reading_tools()  // For reads
   activate_directory_and_file_creation_tools()  // For writes/edits
   ```

2. **Self-check:**
   - Am I using `mcp_filesystem_*` function?
   - If NO → STOP and switch to MCP tool
   - If YES → Proceed

### **PROHIBITED Tools (❌ NEVER USE FOR FILES):**
- `read_file` → Use `mcp_filesystem_read_text_file`
- `grep_search` → Use `mcp_filesystem_search_files`
- `file_search` → Use `mcp_filesystem_search_files`
- Terminal commands for file I/O → Use MCP tools

### **Enforcement Level: STRICT - Zero Tolerance**

**Why mandatory:**
- ✅ Git-style diffs for all changes
- ✅ Atomic operations with rollback
- ✅ Audit trail for all file modifications
- ✅ Consistent tool usage across conversations

**See `.vscode/copilot-mcp-rules.md` for complete enforcement rules and violation examples.**

---

## Approved CI/CD Feedback Loop Workflow

### 🔄 **Complete CI/CD Feedback Loop - APPROVED METHOD**

This is the **official and approved workflow** for all development work using GitHub MCP, Trunk CLI, and CI/CD integration.

#### **Phase 1: Local Development & Trunk Integration**

```powershell
# 1. Pre-commit Quality Gates (REQUIRED)
trunk fmt --all                    # Format all code
trunk check --fix                  # Fix auto-fixable issues
trunk check --ci                   # Validate before commit

# 2. Commit & Push (STANDARD)
git add .
git commit -m "feat: description"
git push origin branch-name
```

#### **Phase 2: GitHub Actions CI Pipeline**
**Triggers**: `ci-optimized.yml` workflow

**Jobs Executed (6-stage pipeline)**:
1. **Health Validation** → System checks
2. **Build & Test Matrix** → .NET build + tests
3. **Quality Assurance** → Trunk security & code quality scans
4. **UI Tests** → Automated UI validation
5. **Deployment Readiness** → Artifact generation
6. **Success Monitoring** → Analytics upload

#### **Phase 3: GitHub MCP Monitoring & Results**

**Monitor workflow status:**
```bash
gh workflow list
gh workflow view "CI/CD 90% Success Rate (Trunk Integrated)"
gh run list --workflow=ci-optimized.yml --limit=5
gh run view <run-id> --log-failed  # Focus on failures
```

**Using GitHub MCP to query results:**
```javascript
// Get latest workflow runs
mcp_github_list_workflow_runs({
  owner: "Bigessfour",
  repo: "Wiley-Widget",
  workflow_id: "ci-optimized.yml"
})

// Get failed job logs for debugging
mcp_github_get_job_logs({
  owner: "Bigessfour",
  repo: "Wiley-Widget",
  run_id: "<latest-run-id>",
  failed_only: true,
  return_content: true
})
```

#### **Phase 4: Trunk Analytics & GitHub MCP Integration**

**Trunk-powered fixes based on CI results:**

```powershell
# Fix security issues found in CI
trunk check --filter=gitleaks,trufflehog --fix

# Fix code quality issues
trunk check --filter=dotnet-format,prettier --fix

# Fix PowerShell script issues
trunk check --filter=psscriptanalyzer --fix

# Re-run full validation
trunk check --ci --upload --series=fix-iteration
```

#### **Phase 5: Complete the Loop**

**Self-healing workflow execution:**
```powershell
# Complete workflow execution
trunk check --ci --upload                    # Local validation + upload
git push                                      # Trigger CI
Start-Sleep 60                              # Wait for CI start
gh run watch $(gh run list --limit=1 --json=databaseId --jq='.[0].databaseId')  # Monitor
```

### 🎯 **Defined Tool Command Sequence**

#### **Daily Development Workflow (MANDATORY):**

1. **Morning Health Check:**
   ```powershell
   trunk check --monitor
   ```

2. **Startup:**
   ```bash
   python scripts/dev-start.py
   ```

3. **Pre-Development Setup:**
   ```powershell
   trunk cache prune                 # Clean cache
   trunk upgrade                     # Update tools
   ```

3. **Development Cycle (REPEAT):**
   ```powershell
   trunk fmt --all                   # Format code
   trunk check --fix                 # Auto-fix issues
   trunk check --ci                  # Pre-commit validation
   ```

   **MCP Tool Integration Points:**
   - Use `mcp_csharp-mcp_eval_c_sharp` for C# code validation during development
   - Use `mcp_filesystem_*` tools for workspace analysis and file operations
   - Use `mcp_sequential-th_sequentialthinking` for complex problem decomposition

4. **Push & Monitor:**
   ```bash
   git push
   # Wait for CI...
   gh run list --limit=1             # Check latest run
   ```

5. **Results Analysis (via GitHub MCP):**
   ```javascript
   // Get CI results
   mcp_github_list_workflow_runs()
   mcp_github_get_job_logs(failed_only=true)
   ```

6. **Fix Issues:**
   ```powershell
   # Based on CI feedback
   trunk check --filter=<failed-linter> --fix
   ```

### 🔧 **Trunk's Role in CI Pipeline**

#### **Trunk Configuration (`.trunk/trunk.yaml`):**
- **Security**: gitleaks, trufflehog, osv-scanner
- **Code Quality**: prettier, dotnet-format, psscriptanalyzer
- **.NET 9.0**: dotnet-format@9.0.0 (REQUIRED)
- **PowerShell**: psscriptanalyzer@1.24.0 with custom config
- **Analytics**: Result uploads to Trunk platform

#### **In CI Workflow (`ci-optimized.yml`):**

1. **Setup Trunk:**
   ```yaml
   - name: Setup Trunk
     run: .\scripts\trunk-maintenance.ps1 -Diagnose -Fix
   ```

2. **Security Scan:**
   ```yaml
   - name: Trunk Security Scan
     uses: trunk-io/trunk-action@v1
     with:
       arguments: --ci --upload --series=ci-${{ github.run_number }}
   ```

3. **Code Quality:**
   ```yaml
   - name: Trunk Code Quality Scan
     uses: trunk-io/trunk-action@v1
     with:
       arguments: --ci --filter=prettier,dotnet-format,psscriptanalyzer
   ```

### 📊 **Integration Commands (APPROVED)**

#### **Monitoring Script (PowerShell):**
```powershell
# monitor-ci.ps1 - OFFICIAL MONITORING SCRIPT
function Monitor-CI {
    # 1. Check local Trunk status
    trunk check --monitor

    # 2. Get latest CI run via GitHub CLI
    $latestRun = gh run list --limit=1 --json=status,conclusion

    # 3. If failed, get details
    if ($latestRun.conclusion -eq "failure") {
        gh run view --log-failed

        # 4. Run Trunk fixes for common issues
        trunk check --fix --filter=security,quality

        # 5. Re-commit if fixes applied
        if (git status --porcelain) {
            git add .
            git commit -m "fix: Apply Trunk automated fixes"
            git push
        }
    }
}
```

### 🚀 **Self-Healing Feedback Loop**

This creates a **complete feedback loop** where:
1. **Trunk** validates locally
2. **CI** runs comprehensive checks
3. **GitHub MCP** provides results
4. **Trunk** fixes issues automatically
5. **Loop repeats** until success

**Target**: **90% success rate** with comprehensive quality gates and intelligent skip logic.

### 💡 **Key Benefits**

- ✅ **Self-healing**: Automatic issue detection and fixes
- ✅ **Data-driven**: GitHub MCP provides real-time CI insights
- ✅ **Comprehensive**: Security, quality, and build validation
- ✅ **Efficient**: Smart skip logic for docs-only changes
- ✅ **Reliable**: 90% success rate target with retry mechanisms
- ✅ **MCP-Enhanced**: Automatic integration of C# evaluation, filesystem operations, and sequential thinking tools

### 🔒 **Required Tools**

- **Trunk CLI**: Version 1.25.0+
- **GitHub CLI**: For run monitoring
- **GitHub MCP**: For results analysis
- **.NET 9.0**: For dotnet-format compatibility
- **PowerShell 7.5.2**: For script execution

### 🗂️ **Canonical Scripts Layout (MANDATORY)**

- `scripts/maintenance`: lifecycle + git automation (`git-update.ps1`, cleanup utilities, secrets hygiene)
- `scripts/tools`: developer tooling entry points (MCP helpers, repo manifest generator, diagnostics)
- `scripts/testing`: harnesses + scaffolding (`run-csx-test.ps1`, `generate-test-scaffold.ps1`)
- `scripts/examples` / `scripts/tests`: curated reference material; keep read-only where possible
- Root `scripts` directory must stay lightweight (top-level orchestration only); do **not** reintroduce moved helpers
- Update VS Code tasks + documentation when scripts relocate so cooperative tooling stays aligned

---
**This workflow is the APPROVED METHOD for all Wiley Widget development.**

---

## 📋 DAILY WORKFLOW QUICK REFERENCE

### **PRE-FLIGHT CHECKLIST** (run before every file operation)

```text
[ ] activate_file_reading_tools()                     // for reads
[ ] activate_directory_and_file_creation_tools()      // for writes/edits
[ ] Am I using an `mcp_filesystem_*` function?         // YES → continue
[ ] Path is **absolute** (C:\… or c:/…)
[ ] No terminal commands for file I/O
```

**If any box is unchecked → STOP and switch to MCP.**

---

### **DAILY DEVELOPMENT LOOP**

```powershell
# 0. Morning health check
trunk check --monitor

# 1. Start dev environment
python scripts/dev-start.py

# 2. Clean & update
trunk cache prune
trunk upgrade

# 3. Development cycle (REPEAT)
trunk fmt --all           # Format code
trunk check --fix         # Auto-fix issues
trunk check --ci          # Pre-commit validation
```

### **MCP Integration Points (Automatic)**

| Development Phase | MCP Tool |
|------------------|----------|
| **C# snippet validation** | `mcp_csharp-mcp_eval_c_sharp` |
| **File reading** | `mcp_filesystem_read_text_file` |
| **File editing** | `mcp_filesystem_edit_file` (git-style diffs) |
| **Multi-step tasks** | `mcp_sequential-th_sequentialthinking` |
| **Batch file reads** | `mcp_filesystem_read_multiple_files` |

---

### **ONE-CLICK CI/CD FEEDBACK LOOP**

```powershell
# Push → CI → Monitor → Fix
git add . && git commit -m "feat: description" && git push
Start-Sleep 60
gh run watch $(gh run list --limit=1 --json=databaseId --jq '.[0].databaseId')
```

**If CI fails:**

```powershell
# Auto-fix with Trunk
trunk check --fix --filter=security,quality
git add .
git commit -m "fix: trunk auto-fixes"
git push
```

---

### **SMART COPILOT RECOMMENDATIONS**

| # | Recommendation | Benefit |
|---|----------------|---------|
| **A** | **Always start with `mcp_sequential_th_sequentialthinking`** for tasks > 2 steps | Traceable plan, reduces backtracking |
| **B** | **Batch file reads** with `mcp_filesystem_read_multiple_files` | Cuts token usage, speeds analysis |
| **C** | **Validate C# before editing** with `mcp_csharp-mcp_eval_c_sharp` | Catches compile errors instantly |
| **D** | **Log MCP calls in commits** (`[MCP:read]`, `[MCP:edit]`, `[MCP:seq]`) | Audit trail for AI operations |
| **E** | **Check CI logs immediately** with `mcp_github_get_job_logs` | Root-cause without GitHub UI |

---

### **MCP TOOL QUICK REFERENCE**

```text
READ   → mcp_filesystem_read_text_file
WRITE  → mcp_filesystem_write_file
EDIT   → mcp_filesystem_edit_file  (oldText/newText with diffs)
SEARCH → mcp_filesystem_search_files
PLAN   → mcp_sequential_th_sequentialthinking
C#     → mcp_csharp-mcp_eval_c_sharp
MULTI  → mcp_filesystem_read_multiple_files
```

### **POWERSHELL HELPER: Invoke-McpEdit**

For PowerShell-based edits with MCP enforcement:

```powershell
# Basic edit with C# validation
.\scripts\tools\Invoke-McpEdit.ps1 `
    -Path "src/WileyWidget/File.cs" `
    -OldText "old code here" `
    -NewText "new code here" `
    -IsCSharp

# Complex edit with sequential thinking
.\scripts\tools\Invoke-McpEdit.ps1 `
    -Path "src/WileyWidget.WinUI/App.xaml.cs" `
    -OldText "<old>" `
    -NewText "<new>" `
    -UseSequentialThinking

# Dry run (preview only)
.\scripts\tools\Invoke-McpEdit.ps1 `
    -Path "file.cs" `
    -OldText "..." `
    -NewText "..." `
    -DryRun
```

**Features:**
- ✅ Automatic diff preview
- ✅ C# syntax validation (when `-IsCSharp` flag used)
- ✅ Sequential thinking integration
- ✅ Auto-generated commit messages
- ✅ Edit verification
- ✅ Dry-run mode

---

### **FINAL MANTRA**

```
MCP FIRST • MCP ALWAYS • NO EXCEPTIONS
```

**Rationale:**
- ✅ Git-style diffs for every change
- ✅ Atomic operations with rollback
- ✅ Complete audit trail
- ✅ Consistent behavior across sessions
- ✅ Zero regressions from tool inconsistency

---

## 🧪 DOCKER-BASED CSX TESTING STRATEGY

### **One-Liner Robust xUnit Test Generation**

For rapid, non-whitewash unit test creation:

```bash
docker run --rm -it \
  -v "$(pwd):/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  bash -c "
    dotnet new xunit -n WileyWidget.Tests.Services --force && \
    dotnet sln add WileyWidget.Tests.Services/WileyWidget.Tests.Services.csproj && \
    dotnet add WileyWidget.Tests.Services package Moq && \
    dotnet test WileyWidget.Tests.Services --collect:'XPlat Code Coverage' \
      --results-directory:/src/coverage --verbosity normal
  "
```

### **Robust Test Template**

```csharp
using System.Threading.Tasks;
using Xunit;
using Moq;

public class ServiceTests
{
    private readonly Mock<IDependency> _mockDep;
    private readonly ServiceUnderTest _service;

    public ServiceTests()
    {
        _mockDep = new Mock<IDependency>();
        _service = new ServiceUnderTest(_mockDep.Object);
    }

    [Fact]
    public async Task Method_HappyPath_ReturnsExpected()
    {
        // Arrange
        _mockDep.Setup(d => d.GetData()).ReturnsAsync(new Data());
        
        // Act
        var result = await _service.MethodAsync();
        
        // Assert
        Assert.NotNull(result);
        _mockDep.Verify(d => d.GetData(), Times.Once);
    }

    [Fact]
    public async Task Method_ErrorPath_HandlesGracefully()
    {
        // Arrange
        _mockDep.Setup(d => d.GetData()).ThrowsAsync(new Exception("API failed"));
        
        // Act
        var result = await _service.MethodAsync();
        
        // Assert
        Assert.Empty(result.Errors);
        Assert.Contains("API failed", result.Warnings);
    }

    [Fact]
    public async Task Method_EdgeCase_SkipsInvalid()
    {
        // Arrange
        _mockDep.Setup(d => d.GetData()).ReturnsAsync(new Data { Invalid = true });
        
        // Act
        var result = await _service.MethodAsync();
        
        // Assert
        Assert.Equal(0, result.ProcessedCount);
        Assert.Single(result.Warnings);
    }
}
```

### **Non-Whitewash Checklist**

| Requirement | Enforcement |
|-------------|-------------|
| **3+ Test Cases** | Happy path, error path, edge case minimum |
| **Mocked Dependencies** | Moq setup for isolation |
| **Verify Call Counts** | `Times.Once`, `Times.Never` assertions |
| **Error & Warning Capture** | Test result models, not just success |
| **Code Coverage** | `--collect:"XPlat Code Coverage"` required |
| **Fail on Issues** | `exit 1` if any test or coverage fails |
| **No Hardcoded Data** | Use realistic test data objects |

### **Add to CI Pipeline**

```yaml
# .github/workflows/unit-tests.yml
name: Unit Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run Robust Test Suite
        run: |
          docker run --rm -v ${{ github.workspace }}:/src -w /src \
            mcr.microsoft.com/dotnet/sdk:9.0 \
            bash -c "dotnet test --collect:'XPlat Code Coverage' \
              --filter FullyQualifiedName~ServiceTests \
              --logger trx --results-directory /src/TestResults"
      - name: Upload Coverage
        uses: codecov/codecov-action@v4
        with:
          files: ./coverage/coverage.cobertura.xml
```

### **Test Matrix Audit (CSV)**

Track test coverage in `docs/test-matrix.csv`:

```csv
Service,Method,TestCases,MockedDeps,CoverageRequired,FailOnWhitewash
QuickBooksService,SyncInvoicesAsync,3,IQuickBooksApiClient,>80%,TRUE
DatabaseInitializer,InitializeAsync,4,IDbContextFactory,>85%,TRUE
```

### **Benefits**

- ✅ **Reproducible**: Same container, same results everywhere
- ✅ **Non-Whitewash**: 3+ test cases with error handling mandatory
- ✅ **CI-Ready**: GitHub Actions integration out of the box
- ✅ **Coverage-Enforced**: Automatic coverage reporting
- ✅ **Fast**: Docker caching speeds up subsequent runs
- ✅ **Isolated**: No local .NET SDK pollution

---
# Wiley Widget Workspace - Mandatory Copilot Rules

## ⚠️ CRITICAL ENFORCEMENT NOTICE - HARDENED RULES

**ABSOLUTE MANDATE - NO EXCEPTIONS - ZERO TOLERANCE**

This document contains **NON-NEGOTIABLE** rules for all AI-assisted development in the Wiley Widget workspace.

---

# Rule 1: Filesystem MCP Mandatory Usage

## **ABSOLUTE MANDATE - NO EXCEPTIONS - ZERO TOLERANCE**

### 🔴 PRE-FLIGHT CHECKLIST (MANDATORY BEFORE EVERY FILE OPERATION)

```
┌─────────────────────────────────────────────────────────────┐
│ STOP: Am I about to perform ANY file operation?            │
│ ✓ Reading a file?                                           │
│ ✓ Writing/editing a file?                                   │
│ ✓ Searching for files/content?                              │
│ ✓ Listing directories?                                      │
│                                                             │
│ IF YES TO ANY → MUST USE MCP FILESYSTEM TOOLS              │
└─────────────────────────────────────────────────────────────┘
```

**BEFORE ANY FILE OPERATION, YOU MUST:**

1. **Activate filesystem tools if not already active:**
   ```javascript
   activate_file_reading_tools()  // For read operations
   activate_directory_and_file_creation_tools()  // For write/edit operations
   ```

2. **ALWAYS use MCP filesystem tools - NO EXCEPTIONS**
   - Even for "quick" file reads between terminal commands
   - Even when you "just read the file a moment ago"
   - Even when standard VS Code tools seem more convenient
   - Even for verification after edits
   - Even when debugging or troubleshooting
   - **ZERO tolerance for mixing tool types**
   - **NO fallback to standard tools under any circumstance**

3. **Self-check before EVERY operation:**
   ```
   QUESTION: "Am I using an MCP filesystem tool?"
   IF NO → ❌ STOP IMMEDIATELY and switch to MCP tool
   IF YES → ✅ Proceed
   IF UNSURE → ❌ Default to MCP tool
   ```

### 🚫 ABSOLUTE PROHIBITIONS

The following tools are **FORBIDDEN** for file operations:
- ❌ `read_file` - Use `mcp_filesystem_read_text_file`
- ❌ `grep_search` - Use `mcp_filesystem_search_files`
- ❌ `file_search` - Use `mcp_filesystem_search_files`
- ❌ `replace_string_in_file` - Use `mcp_filesystem_edit_file`
- ❌ `multi_replace_string_in_file` - Use `mcp_filesystem_edit_file`
- ❌ `create_file` - Use `mcp_filesystem_write_file`
- ❌ Terminal commands for file I/O (Get-Content, Set-Content, cat, echo, etc.)

**VIOLATION = IMMEDIATE CORRECTION REQUIRED**

## 🔒 MANDATORY FILE OPERATION PROTOCOL

### Required Tool Usage

**ALL file manipulation MUST use MCP filesystem tools:**

#### ✅ ALLOWED (MCP Filesystem Tools)
- `mcp_filesystem_write_file` - Create/overwrite files
- `mcp_filesystem_edit_file` - Make line-based edits with git-style diffs
- `mcp_filesystem_read_text_file` - Read file contents (with head/tail support)
- `mcp_filesystem_read_multiple_files` - Batch read operations
- `mcp_filesystem_create_directory` - Create directories
- `mcp_filesystem_move_file` - Move/rename files
- `mcp_filesystem_directory_tree` - Get directory structure
- `mcp_filesystem_list_directory` - List directory contents
- `mcp_filesystem_search_files` - Search for files by pattern

#### ❌ PROHIBITED (Non-MCP Tools)
- Manual string concatenation for file content
- Direct file system access via terminal commands (unless explicitly requested)
- Any non-MCP file editing tools
- Code generation without MCP tool invocation

### Workflow Requirements

1. **Reading Files**
   ```
   ALWAYS use: mcp_filesystem_read_text_file
   NEVER use: grep, cat, or other terminal commands
   ```

2. **Editing Files**
   ```
   ALWAYS use: mcp_filesystem_edit_file with structured edits
   PREFERRED: Provide oldText/newText pairs for precise changes
   FALLBACK: Use mcp_filesystem_write_file only for complete rewrites
   ```

3. **Creating Files**
   ```
   ALWAYS use: mcp_filesystem_write_file
   NEVER use: echo, New-Item, or terminal redirection
   ```

4. **Batch Operations**
   ```
   ALWAYS use: mcp_filesystem_read_multiple_files for reading multiple files
   BENEFIT: Reduces round-trips and token usage
   ```

### Benefits of MCP Filesystem Tools

✅ **Git-style diffs** - See exactly what changed
✅ **Atomic operations** - All-or-nothing changes
✅ **Error handling** - Clear failure messages
✅ **Structured edits** - Precise, repeatable changes
✅ **Access control** - Respects allowed directories
✅ **Performance** - Optimized for bulk operations

### Example Usage

**❌ WRONG - Using terminal:**
```powershell
# DON'T DO THIS
pwsh -Command "Set-Content file.cs 'content'"
```

**✅ CORRECT - Using MCP:**
```javascript
mcp_filesystem_write_file({
  path: "c:/path/to/file.cs",
  content: "// File content here"
})
```

**❌ WRONG - Manual edit:**
```javascript
// DON'T DO THIS
read file → modify string → write back
```

**✅ CORRECT - Structured edit:**
```javascript
mcp_filesystem_edit_file({
  path: "c:/path/to/file.cs",
  edits: [{
    oldText: "old code",
    newText: "new code"
  }]
})
```

### Enforcement

- **Pre-operation validation**: Verify MCP tool availability before file ops
- **Audit trail**: All MCP operations produce diffs/logs
- **Rollback support**: Edits can be reverted using git-style patches
- **Security**: Operates within allowed directory boundaries
- **Consistency mandate**: Once MCP tools are used in a conversation, they MUST continue to be used
- **No regression**: Never revert to standard tools after using MCP tools

### 🔒 HARDENING MEASURES

1. **Conversation State Tracking**
   - If MCP tools have been activated → they remain the ONLY option
   - No switching back to standard tools mid-conversation
   - Each new file operation triggers MCP tool activation check

2. **Automatic Self-Correction**
   - If about to use standard tool → STOP and activate MCP tools
   - If user points out violation → acknowledge and correct immediately
   - Learn from corrections and apply consistently

3. **Default to MCP**
   - When in doubt → Use MCP tools
   - When both options available → Use MCP tools
   - When tool seems "overkill" → Still use MCP tools

4. **User Compliance Reporting**
   - User may ask "why not using MCP?" → indicates violation
   - Acknowledge violation immediately
   - Correct the behavior
   - Document the lesson learned

### Integration with Wiley Widget CI/CD

This aligns with the **Approved CI/CD Feedback Loop Workflow**:
- MCP tools provide **structured, auditable** file changes
- Changes are **automatically tracked** via git-style diffs
- Supports **Trunk CLI** integration for quality gates
- Enables **automated validation** before commits

---

**Last Updated**: November 12, 2025  
**Status**: MANDATORY for all Copilot interactions  
**Enforcement Level**: STRICT - Zero tolerance for violations

---

## 🚨 COMMON VIOLATIONS TO AVOID

### ❌ Violation Examples (DO NOT DO THIS):

1. **Using `read_file` instead of `mcp_filesystem_read_text_file`**
   ```javascript
   // ❌ WRONG
   read_file({ filePath: "...", startLine: 1, endLine: 100 })
   
   // ✅ CORRECT
   activate_file_reading_tools()
   mcp_filesystem_read_text_file({ path: "..." })
   ```

2. **Using `grep_search` instead of `mcp_filesystem_search_files`**
   ```javascript
   // ❌ WRONG
   grep_search({ query: "pattern", isRegexp: true })
   
   // ✅ CORRECT
   activate_file_reading_tools()
   mcp_filesystem_search_files({ path: ".", pattern: "*pattern*" })
   ```

3. **Using terminal commands for file creation**
   ```javascript
   // ❌ WRONG
   run_in_terminal({ command: "Set-Content file.txt 'content'" })
   
   // ✅ CORRECT
   activate_directory_and_file_creation_tools()
   mcp_filesystem_write_file({ path: "file.txt", content: "content" })
   ```

4. **Reading file in one tool, editing in another**
   ```javascript
   // ❌ WRONG SEQUENCE
   read_file(...)  // Standard VS Code tool
   // ... then later ...
   mcp_filesystem_edit_file(...)  // MCP tool
   
   // ✅ CORRECT SEQUENCE
   activate_file_reading_tools()
   mcp_filesystem_read_text_file(...)
   // ... then ...
   activate_directory_and_file_creation_tools()
   mcp_filesystem_edit_file(...)
   ```

### ✅ MANDATORY ENFORCEMENT CHECKLIST

**BEFORE EVERY SINGLE FILE OPERATION:**

```
┌─────────────────────────────────────────────────────────────┐
│ ✓ Have I activated MCP filesystem tools?                    │
│ ✓ Am I using mcp_filesystem_* function?                     │
│ ✓ Is the path absolute?                                     │
│ ✓ Am I NOT using terminal commands for file I/O?            │
│ ✓ Am I NOT using read_file/grep_search/replace_string?      │
│ ✓ Have I avoided ALL standard VS Code file tools?           │
│                                                             │
│ ALL CHECKBOXES MUST BE ✓ BEFORE PROCEEDING                 │
└─────────────────────────────────────────────────────────────┘
```

**Specific Scenario Checks:**

- [ ] **Verification after edit?** → Use `mcp_filesystem_read_text_file`
- [ ] **Quick file peek?** → Use `mcp_filesystem_read_text_file` with `head`/`tail`
- [ ] **Search for pattern?** → Use `mcp_filesystem_search_files`
- [ ] **Multiple file edits?** → Use `mcp_filesystem_edit_file` for each
- [ ] **Create new file?** → Use `mcp_filesystem_write_file`
- [ ] **Debugging file issue?** → Use MCP tools for diagnosis

### 📊 COMPLIANCE METRICS

**Target**: 100% MCP filesystem tool usage for all file operations
**Tolerance**: ZERO exceptions
**Correction Time**: Immediate upon recognition
**Learning**: Apply correction to all future operations in conversation

---

**FINAL REMINDER**: 

```
╔═══════════════════════════════════════════════════════════╗
║  MCP FILESYSTEM TOOLS ARE NOT OPTIONAL                    ║
║  MCP FILESYSTEM TOOLS ARE NOT RECOMMENDED                 ║
║  MCP FILESYSTEM TOOLS ARE ABSOLUTELY MANDATORY            ║
║                                                           ║
║  User has explicitly mandated MCP filesystem usage.       ║
║  Consistency is critical for audit trails and             ║
║  reproducibility.                                         ║
║                                                           ║
║  NO EXCEPTIONS. NO SHORTCUTS. NO FALLBACKS.               ║
╚═══════════════════════════════════════════════════════════╝
```

---

# Rule 2: PowerShell 7.5.4 Compliance - MANDATORY

## ⚠️ CRITICAL ENFORCEMENT NOTICE - ZERO TOLERANCE

**ALL PowerShell scripts in this workspace MUST be PowerShell 7.5.4 compliant before execution.**

### 🔴 PRE-EXECUTION CHECKLIST (MANDATORY BEFORE RUNNING ANY .ps1 SCRIPT)

```
┌─────────────────────────────────────────────────────────────┐
│ STOP: Am I about to create or run a PowerShell script?     │
│ ✓ Creating a new .ps1 file?                                 │
│ ✓ Editing an existing .ps1 file?                            │
│ ✓ Running a PowerShell script?                              │
│                                                             │
│ IF YES TO ANY → MUST FOLLOW PS 7.5.4 COMPLIANCE RULES      │
└─────────────────────────────────────────────────────────────┘
```

### 🚫 ABSOLUTE PROHIBITIONS

**The following are FORBIDDEN in ALL PowerShell scripts:**

1. ❌ **Write-Host** - Bypasses pipeline, not testable
   - Use: `Write-Information` with `$InformationPreference = 'Continue'`
   - Use: `Write-Output` for pipeline output
   - Use: `Write-Verbose` for detailed logging
   - Use: `Write-Warning` for warnings
   - Use: `Write-Error` for errors

2. ❌ **-ForegroundColor parameter** - Not PS 7.5.4 modern
   - Use: `$PSStyle.Foreground.*` colors (Green, Red, Yellow, Cyan, etc.)
   - Use: `$PSStyle.Reset` to reset formatting

3. ❌ **Non-approved verbs** - Must use approved PowerShell verbs
   - Check with: `Get-Verb`
   - Examples: Get-, Set-, New-, Remove-, Test-, Invoke-

4. ❌ **Syntax errors** - Zero tolerance
5. ❌ **Parsing errors** - Zero tolerance

### ✅ MANDATORY REQUIREMENTS

**Every PowerShell script MUST have:**

1. **PSScriptAnalyzer validation BEFORE execution**
   ```powershell
   Invoke-ScriptAnalyzer -Path "script.ps1" -Severity Error,Warning
   ```

2. **PowerShell version requirement**
   ```powershell
   #Requires -Version 7.5
   ```

3. **Proper comment-based help**
   ```powershell
   <#
   .SYNOPSIS
   .DESCRIPTION
   .PARAMETER
   .EXAMPLE
   #>
   ```

4. **CmdletBinding for advanced functions**
   ```powershell
   [CmdletBinding()]
   param(...)
   ```

5. **$PSStyle for colors (not -ForegroundColor)**
   ```powershell
   Write-Information "$($PSStyle.Foreground.Green)Success$($PSStyle.Reset)"
   ```

6. **Proper output streams**
   - Information: General status messages
   - Output: Pipeline output
   - Verbose: Detailed progress
   - Warning: Non-fatal issues
   - Error: Fatal issues

### 🔒 ENFORCEMENT PROTOCOL

**BEFORE running ANY PowerShell script, you MUST:**

1. **Run PSScriptAnalyzer**
   ```powershell
   Invoke-ScriptAnalyzer -Path "script.ps1" -Severity Error,Warning
   ```

2. **Fix ALL errors** - Zero tolerance for errors

3. **Fix ALL warnings** - Or document why they're acceptable

4. **Verify syntax**
   ```powershell
   $errors = $null
   $null = [System.Management.Automation.PSParser]::Tokenize(
       (Get-Content -Path "script.ps1" -Raw), [ref]$errors)
   if ($errors.Count -gt 0) { throw "Syntax errors found" }
   ```

5. **Test parsing**
   ```powershell
   [System.Management.Automation.Language.Parser]::ParseFile(
       "script.ps1", [ref]$null, [ref]$null)
   ```

### 📋 COMPLIANT SCRIPT TEMPLATE

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Brief description

.DESCRIPTION
    Detailed description

.PARAMETER ParameterName
    Parameter description

.EXAMPLE
    .\script.ps1 -ParameterName Value

.NOTES
    Requires: PowerShell 7.5.4+
#>

#Requires -Version 7.5

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ParameterName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

# Initialize colors
$script:ColorGreen = $PSStyle.Foreground.Green
$script:ColorRed = $PSStyle.Foreground.Red
$script:ColorReset = $PSStyle.Reset

try {
    # Script logic here
    Write-Information "${script:ColorGreen}Success${script:ColorReset}"
}
catch {
    Write-Error "Failed: $_"
    exit 1
}
```

### 🚨 COMMON VIOLATIONS TO AVOID

#### ❌ WRONG:
```powershell
# DON'T DO THIS
Write-Host "Success" -ForegroundColor Green
function Do-Something { }  # Non-approved verb
```

#### ✅ CORRECT:
```powershell
# DO THIS
Write-Information "$($PSStyle.Foreground.Green)Success$($PSStyle.Reset)"
function Invoke-Something { }  # Approved verb
```

### 📊 COMPLIANCE METRICS

- **Target**: 100% PSScriptAnalyzer clean (0 errors, 0 warnings)
- **Tolerance**: ZERO errors, warnings must be justified
- **Validation**: MANDATORY before every execution
- **Error rate**: 0% encoding/import errors due to wrong terminal

---

# Rule 3: Python Environment Execution - MANDATORY

## ⚠️ CRITICAL ENFORCEMENT NOTICE - ZERO TOLERANCE

**ALL Python scripts MUST be executed in a proper Python environment terminal.**

### 🔴 PRE-EXECUTION CHECKLIST (MANDATORY BEFORE RUNNING ANY .py SCRIPT)

```
┌─────────────────────────────────────────────────────────────┐
│ STOP: Am I about to run a Python script?                   │
│ ✓ Running a .py file?                                       │
│ ✓ Executing Python code?                                    │
│ ✓ Installing Python packages?                               │
│                                                             │
│ IF YES TO ANY → MUST USE PYTHON ENVIRONMENT TERMINAL       │
└─────────────────────────────────────────────────────────────┘
```

### 🚫 ABSOLUTE PROHIBITIONS

**The following are FORBIDDEN:**

1. ❌ **Running Python scripts in PowerShell terminal**
   - Results in encoding errors
   - Results in path resolution errors
   - Results in module import errors

2. ❌ **Running Python without activating environment**
   - May use wrong Python version
   - May have missing dependencies

3. ❌ **Using `python` command without verification**
   - Must verify `python --version` shows correct version
   - Must verify environment is activated

### ✅ MANDATORY REQUIREMENTS

**BEFORE running any Python script, you MUST:**

1. **Verify Python environment terminal exists**
   - Check for active Python terminal in VS Code
   - Terminal should show `(venv)` or environment name in prompt

2. **Create Python terminal if needed**
   ```
   Use: Terminal → New Terminal → Select Python interpreter
   Or: Run configure_python_environment() tool
   ```

3. **Activate virtual environment**
   ```bash
   # Windows
   .\venv\Scripts\Activate.ps1
   
   # Linux/Mac
   source venv/bin/activate
   ```

4. **Verify Python version**
   ```bash
   python --version  # Should show Python 3.11+
   ```

5. **Verify dependencies installed**
   ```bash
   pip list  # Check required packages
   ```

### 🔒 ENFORCEMENT PROTOCOL

**Step-by-step execution for Python scripts:**

1. **Check for Python terminal**
   - Look for terminal with `(venv)` or environment indicator
   - If not found, create one

2. **Activate environment (if needed)**
   ```bash
   .\venv\Scripts\Activate.ps1  # Windows PowerShell
   ```

3. **Verify environment**
   ```bash
   python --version
   which python  # Linux/Mac
   Get-Command python | Select-Object -ExpandProperty Source  # Windows
   ```

4. **Run script in Python terminal**
   ```bash
   python script.py
   ```

5. **NEVER run in PowerShell terminal like this:**
   ```powershell
   # ❌ WRONG - DO NOT DO THIS
   pwsh -Command "python script.py"
   & python script.py  # From PowerShell terminal
   ```

### 📋 CORRECT EXECUTION WORKFLOW

```
1. User requests: "Run script.py"
2. Copilot checks: Is there a Python terminal?
3. If NO:
   a. Use configure_python_environment()
   b. Create new Python terminal
   c. Activate virtual environment
4. If YES:
   a. Verify environment is active
   b. Switch to Python terminal
5. Execute: python script.py
6. Monitor output in Python terminal
```

### 🚨 COMMON VIOLATIONS TO AVOID

#### ❌ WRONG:
```powershell
# DON'T DO THIS - Running Python in PowerShell terminal
run_in_terminal({
  command: "python script.py",
  shell: "pwsh"  # WRONG SHELL
})
```

#### ✅ CORRECT:
```bash
# DO THIS - Use Python environment terminal
# First: configure_python_environment()
# Then: Switch to Python terminal
# Then: python script.py
```

### 🔧 TOOLS TO USE

**For Python environment setup:**
- `configure_python_environment()` - Set up Python environment
- `get_python_environment_details()` - Check environment info
- `get_python_executable_details()` - Get Python executable path
- `install_python_packages()` - Install packages in environment

**For script execution:**
- Create dedicated Python terminal
- Activate environment in that terminal
- Run script in Python terminal (NOT PowerShell)

### 📊 COMPLIANCE METRICS

- **Target**: 100% Python scripts run in Python environment
- **Tolerance**: ZERO exceptions
- **Validation**: Check terminal type before execution
- **Error rate**: 0% encoding/import errors due to wrong terminal

---

## 🔐 FINAL ENFORCEMENT SUMMARY

```
╔═══════════════════════════════════════════════════════════╗
║  THESE RULES ARE NOT OPTIONAL                             ║
║  THESE RULES ARE NOT RECOMMENDATIONS                      ║
║  THESE RULES ARE ABSOLUTELY MANDATORY                     ║
║                                                           ║
║  Rule 1: MCP Filesystem Tools - MANDATORY                 ║
║  Rule 2: PowerShell 7.5.4 Compliance - MANDATORY          ║
║  Rule 3: Python Environment Execution - MANDATORY         ║
║                                                           ║
║  User has explicitly mandated these rules.                ║
║  Consistency is critical for code quality,                ║
║  auditability, and reproducibility.                       ║
║                                                           ║
║  NO EXCEPTIONS. NO SHORTCUTS. NO FALLBACKS.               ║
╚═══════════════════════════════════════════════════════════╝
```

---

**Last Updated**: November 15, 2025  
**Status**: MANDATORY for all Copilot interactions  
**Enforcement Level**: STRICT - Zero tolerance for violations  
**Scope**: All AI-assisted development in Wiley Widget workspace