# Wiley Widget Scripts

This directory contains local development, diagnostics, validation, and setup scripts for the current repository.

## What This Folder Is For

- local setup helpers
- validation and diagnostics
- environment and MCP configuration
- targeted data seeding and integration setup
- one-off repair and migration utilities

This folder is not a promise that every development workflow is wrapped in a single canonical script. In practice, the VS Code task list and focused test strategy are the most reliable entry points for day-to-day work.

## Current High-Value Scripts

### Setup

- `setup-license.ps1`: configure Syncfusion licensing for local UI execution
- `setup-env.ps1`: bootstrap local environment values
- `generate-vs-mcp-config.ps1`: regenerate local MCP-related configuration
- `setup-mssql-mcp.ps1`: local MSSQL MCP setup

### Verification And Diagnostics

- `verify-startup.ps1`: startup-focused verification
- `verify-syncfusion-setup.ps1`: Syncfusion setup checks
- `verify-xai-api-key.ps1`: xAI key validation
- `analyze-startup-timeline.ps1`: startup timing analysis
- `profile-di-validation.ps1`: DI validation profiling
- `monitor-timeouts.ps1`: timeout and cancellation monitoring

### Maintenance

- `maintenance/kill-test-processes.ps1`: stop stuck test processes before reruns
- `clear-ribbon-cache.ps1`: clear ribbon-related cache state

### QuickBooks

- `quickbooks/setup-oauth.ps1`: local QuickBooks OAuth setup
- `seed-sandbox-qbo.ps1`: sandbox QuickBooks data seeding helper

## Important Notes

- Some older script names that appeared in previous docs no longer exist. Do not rely on historical references such as `build.ps1`, `test.ps1`, or `setup-database.ps1` unless they are restored to the repository.
- Prefer the current VS Code tasks in `.vscode/tasks.json` for build, run, and many validation flows.
- Use `docs/TESTING_STRATEGY.md` to decide which test lane actually provides meaningful proof.

## Updating This Folder

When a script is added, removed, or becomes the preferred entry point for an important workflow, update this file in the same change.
