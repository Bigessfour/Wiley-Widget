---
applyTo: '**'
---
# Wiley Widget Project Guidelines

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

2. **Pre-Development Setup:**
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

### 🔒 **Required Tools**

- **Trunk CLI**: Version 1.25.0+
- **GitHub CLI**: For run monitoring
- **GitHub MCP**: For results analysis
- **.NET 9.0**: For dotnet-format compatibility
- **PowerShell 7.5.2**: For script execution

---
**This workflow is the APPROVED METHOD for all Wiley Widget development.**
