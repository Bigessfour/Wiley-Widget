# GitHub Actions Workflow Cleanup Guide

## Overview

This guide helps you manage and clean up GitHub Actions workflows and their run history.

## Current Workflow Status

### Active Workflows (11 files)
These workflows are currently active and should be kept:
- `build-winforms.yml` - CI/CD Dashboard Enhanced
- `dotnet.yml` - .NET Build & Test
- `e2e-headless-mcp.yml` - E2E Headless Testing
- `fast-pr-feedback.yml` - Fast PR Feedback
- `grok-pr-review.yml` - Grok Code Review
- `logging-evaluation.yml` - Logging Evaluation
- `main.yml` - Main workflow
- `polish-validation.yml` - Polish Validation
- `restore.yml` - Restore CI
- `startup-ci.yml` - Startup CI
- `syncfusion-theming.yml` - Syncfusion Theming Compliance

### Disabled Workflows (Already Cleaned)
These workflows are disabled in GitHub and their files have been removed:
- ✅ `ci.yml` - Old CI workflow (replaced by newer workflows)
- ✅ `sql-server-integration-tests.yml` - Superseded by polish-validation.yml
- ✅ `pdf-service-tests.yml` - Integrated into other tests
- ✅ `ci-consolidated.yml` - Consolidated into build-winforms.yml
- ✅ `backup-to-blob.yml` - Deprecated workflow
- ✅ `mega-audit.yml` - Experimental audit workflow

## Workflow Run History

As of the last check, there are **718 workflow runs** in the repository history.

### Why Clean Up Workflow Runs?

1. **Storage**: Workflow runs consume GitHub Actions storage (artifacts, logs)
2. **Performance**: Large run histories can slow down the Actions UI
3. **Clarity**: Removing old failed/cancelled runs improves visibility
4. **Compliance**: Some organizations require periodic cleanup of old execution data

## How to Delete Workflow Runs

### Option 1: GitHub Web UI (Easiest)

1. Go to your repository → **Actions** tab
2. Click on a specific workflow from the left sidebar
3. For each run you want to delete:
   - Click on the run
   - Click the **⋯** (three dots) menu in the top right
   - Select **Delete workflow run**

**Note**: This must be done one run at a time through the UI.

### Option 2: GitHub CLI (Bulk Deletion)

Use the provided cleanup script (see below) with GitHub CLI:

```bash
# Prerequisites: Install gh CLI and authenticate
# https://cli.github.com/

# Run the cleanup script
pwsh .github/scripts/cleanup-workflow-runs.ps1
```

### Option 3: REST API (Advanced)

Delete individual runs using the API:

```bash
# Delete a specific run
gh api --method DELETE /repos/Bigessfour/Wiley-Widget/actions/runs/{run_id}

# Delete all runs for a specific workflow
gh api repos/Bigessfour/Wiley-Widget/actions/workflows/{workflow_id}/runs | \
  jq -r '.workflow_runs[] | .id' | \
  xargs -I {} gh api --method DELETE /repos/Bigessfour/Wiley-Widget/actions/runs/{}
```

## Cleanup Script

A PowerShell script is available at `.github/scripts/cleanup-workflow-runs.ps1` to help automate workflow run deletion.

### Features:
- List all workflow runs with status and dates
- Filter runs by status (completed, failed, cancelled)
- Filter runs by age (older than X days)
- Dry-run mode to preview deletions
- Bulk deletion with confirmation

### Usage Examples:

```powershell
# List all workflow runs
.\.github\scripts\cleanup-workflow-runs.ps1 -List

# Delete runs older than 90 days (dry run)
.\.github\scripts\cleanup-workflow-runs.ps1 -OlderThanDays 90 -DryRun

# Delete completed runs older than 90 days
.\.github\scripts\cleanup-workflow-runs.ps1 -OlderThanDays 90 -Status completed

# Delete failed runs older than 30 days
.\.github\scripts\cleanup-workflow-runs.ps1 -OlderThanDays 30 -Status failed

# Delete all cancelled runs
.\.github\scripts\cleanup-workflow-runs.ps1 -Status cancelled
```

## Best Practices

1. **Keep recent runs**: Don't delete runs from the last 30 days for debugging purposes
2. **Preserve successful runs**: Consider keeping successful runs longer than failed ones
3. **Archive important runs**: If needed, download logs before deletion
4. **Regular cleanup**: Schedule periodic cleanup (e.g., quarterly) to prevent accumulation
5. **Document reasons**: Keep notes on why workflows were disabled/removed

## Retention Policies

Consider establishing workflow run retention policies:

- **Failed runs**: Keep 30 days
- **Cancelled runs**: Keep 14 days  
- **Successful runs**: Keep 90 days
- **Release runs**: Keep 1 year
- **Manual runs**: Review case-by-case

## Disabled vs Deleted Workflows

### Disabled Workflows
- Workflow file still exists in repository
- Cannot be triggered automatically
- Can be re-enabled by editing the workflow file
- Run history is preserved
- Still counts toward workflow quota

### Deleted Workflows  
- Workflow file removed from repository
- Workflow registration remains in GitHub
- Cannot be triggered at all
- Run history is preserved until manually deleted
- Does not count toward active workflow quota

## Unregistering Workflows

GitHub automatically unregisters workflows when:
1. The workflow file is deleted from the default branch
2. The repository is archived
3. After a significant period of inactivity (GitHub's discretion)

**Note**: Unregistering removes the workflow from the UI but preserves run history.

## Related Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Managing workflow runs](https://docs.github.com/en/actions/managing-workflow-runs)
- [GitHub CLI Documentation](https://cli.github.com/manual/)
- [Actions REST API](https://docs.github.com/en/rest/actions)

## Support

For questions or issues with workflow cleanup:
1. Check this documentation
2. Review GitHub Actions documentation
3. Contact repository maintainers
4. Open an issue in the repository

---

*Last updated: 2026-02-03*
