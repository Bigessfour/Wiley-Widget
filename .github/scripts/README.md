# GitHub Actions Scripts

This directory contains utility scripts for managing GitHub Actions workflows.

## Available Scripts

### cleanup-workflow-runs.ps1

PowerShell script for cleaning up old GitHub Actions workflow runs.

**Prerequisites:**
- PowerShell 7.x (cross-platform)
- GitHub CLI (`gh`) installed and authenticated
- Appropriate repository permissions

**Quick Start:**

```powershell
# List all workflow runs
.\cleanup-workflow-runs.ps1 -List

# Preview deletion (dry run)
.\cleanup-workflow-runs.ps1 -OlderThanDays 90 -DryRun

# Delete failed runs older than 30 days
.\cleanup-workflow-runs.ps1 -OlderThanDays 30 -Status failed

# Delete cancelled runs older than 14 days
.\cleanup-workflow-runs.ps1 -OlderThanDays 14 -Status cancelled
```

**Parameters:**
- `-List` - List all workflow runs without deleting
- `-OlderThanDays <int>` - Delete runs older than X days (default: 90)
- `-Status <string>` - Filter by status (all, completed, failed, cancelled, success)
- `-WorkflowName <string>` - Filter by workflow name
- `-DryRun` - Preview deletions without actually deleting
- `-Force` - Skip confirmation prompts

See the script header for full documentation.

## Related Documentation

- [WORKFLOW_CLEANUP.md](../WORKFLOW_CLEANUP.md) - Complete guide to workflow cleanup
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [GitHub CLI Documentation](https://cli.github.com/manual/)
