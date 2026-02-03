# Answer: Can You Delete Old GitHub Actions Workflows?

## Short Answer

**Yes**, but it depends on what you mean by "workflows":

1. **Workflow Definition Files (.yml)** ✅ **YES** - Can be deleted from the repository
2. **Workflow Run History** ⚠️ **YES, but requires proper permissions** - Can be deleted via GitHub UI or API

## What Was Done

### ✅ Immediate Actions Taken

1. **Deleted obsolete workflow file**:
   - Removed `.github/workflows/grok-review.yml.disabled`

2. **Created comprehensive documentation**:
   - `.github/WORKFLOW_CLEANUP.md` - Complete workflow cleanup guide
   - `.github/scripts/README.md` - Script usage instructions

3. **Created automation script**:
   - `.github/scripts/cleanup-workflow-runs.ps1` - PowerShell script for bulk deletion of workflow runs

### 📊 Current Repository Status

**Workflow Files:**
- 11 active workflow files in `.github/workflows/`
- 1 disabled file removed (grok-review.yml.disabled)
- All other disabled workflow files were already cleaned up previously

**Workflow History:**
- ~718 workflow runs in repository history
- Mix of successful, failed, and cancelled runs
- Oldest runs date back several months

**Disabled Workflows (registered but files deleted):**
- ci.yml
- sql-server-integration-tests.yml
- pdf-service-tests.yml
- ci-consolidated.yml
- backup-to-blob.yml
- mega-audit.yml

## How to Complete the Cleanup

### Option 1: Use the Provided Script (Recommended)

The automated PowerShell script makes bulk deletion easy and safe:

```powershell
# Navigate to scripts directory
cd .github/scripts

# Step 1: List all runs to see what you have
.\cleanup-workflow-runs.ps1 -List

# Step 2: Preview what will be deleted (dry run)
.\cleanup-workflow-runs.ps1 -OlderThanDays 90 -DryRun

# Step 3: Delete old runs (requires confirmation)
.\cleanup-workflow-runs.ps1 -OlderThanDays 90
```

**Prerequisites:**
- Install GitHub CLI: https://cli.github.com/
- Authenticate: `gh auth login`
- Have appropriate repository permissions

**Common Use Cases:**

```powershell
# Delete failed runs older than 30 days
.\cleanup-workflow-runs.ps1 -OlderThanDays 30 -Status failed

# Delete cancelled runs older than 14 days
.\cleanup-workflow-runs.ps1 -OlderThanDays 14 -Status cancelled

# Delete all runs for a specific workflow
.\cleanup-workflow-runs.ps1 -WorkflowName "Old CI" -DryRun

# Delete with no confirmation prompt (use carefully!)
.\cleanup-workflow-runs.ps1 -OlderThanDays 90 -Force
```

### Option 2: Manual Deletion via GitHub UI

1. Go to repository → **Actions** tab
2. Click on a workflow from the left sidebar
3. For each run:
   - Click the run
   - Click **⋯** (three dots) menu
   - Select **Delete workflow run**

**Note**: This is slow for bulk deletion (one at a time).

### Option 3: GitHub API/CLI Manual Commands

```bash
# Delete a specific run
gh api --method DELETE /repos/Bigessfour/Wiley-Widget/actions/runs/{run_id}

# List all runs for a workflow
gh api repos/Bigessfour/Wiley-Widget/actions/workflows/{workflow_id}/runs

# Delete all runs for a specific workflow (bash one-liner)
gh api repos/Bigessfour/Wiley-Widget/actions/workflows/{workflow_id}/runs | \
  jq -r '.workflow_runs[] | .id' | \
  xargs -I {} gh api --method DELETE /repos/Bigessfour/Wiley-Widget/actions/runs/{}
```

## Understanding Workflow States

### Active Workflows
- ✅ Workflow file exists in repository
- ✅ Can be triggered automatically or manually
- ✅ Shows in Actions UI
- ✅ Counts toward workflow quota

### Disabled Workflows
- ⚠️ Workflow file may or may not exist
- ❌ Cannot be triggered
- ✅ Still shows in Actions UI
- ✅ Run history preserved
- ⚠️ May still count toward quota

### Deleted Workflows
- ❌ Workflow file removed from repository
- ❌ Cannot be triggered
- ⚠️ Registration may remain in GitHub
- ✅ Run history preserved until manually deleted
- ❌ Does not count toward active workflow quota

## Best Practices

### Retention Recommendations

- **Failed runs**: Keep 30 days
- **Cancelled runs**: Keep 14 days
- **Successful runs**: Keep 90 days
- **Release runs**: Keep 1 year
- **Manual runs**: Review case-by-case

### When to Clean Up

1. **Storage concerns**: Workflow runs consume GitHub Actions storage
2. **Performance**: Large run histories slow down the Actions UI
3. **Clarity**: Remove noise from old failed/cancelled runs
4. **Compliance**: Meet organizational data retention policies

### What NOT to Delete

- Recent runs (last 30 days) - useful for debugging
- Successful release runs - documentation of deployments
- Runs with important artifacts - may be needed for recovery
- Runs under investigation - active debugging/triage

## Why Can't the Bot Delete Workflow Runs?

Workflow run deletion requires:
1. **Write permissions** to repository actions
2. **Authentication** with GitHub (the bot runs in a sandboxed environment)
3. **API rate limit management** (bulk deletions can hit rate limits)

The bot can:
- ✅ Delete workflow files
- ✅ Create documentation and scripts
- ✅ Analyze and report on workflows
- ❌ Delete workflow run history (requires your credentials)

## Summary

**What the bot did:**
1. ✅ Removed 1 obsolete disabled workflow file
2. ✅ Created comprehensive cleanup documentation
3. ✅ Built an automated cleanup script with safety features
4. ✅ Provided clear instructions for completion

**What you need to do:**
1. Install GitHub CLI and authenticate
2. Review the documentation in `.github/WORKFLOW_CLEANUP.md`
3. Run the cleanup script in `.github/scripts/cleanup-workflow-runs.ps1`
4. Monitor and maintain workflow hygiene going forward

## Questions?

- Review: `.github/WORKFLOW_CLEANUP.md` for detailed documentation
- Run: `.github/scripts/cleanup-workflow-runs.ps1 -List` to see your workflow runs
- Check: GitHub Actions documentation for official guidance
- Ask: Repository maintainers for specific organizational policies

---

**Created**: 2026-02-03  
**Status**: Implemented and ready for user execution
