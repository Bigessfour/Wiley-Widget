# Wiley Widget Quick Start

This quick start is for the repository as it exists today. It is aimed at contributors who need to build, run, validate, and safely change the application during release stabilization.

## Prerequisites

- Windows 10 or Windows 11
- .NET 10 SDK
- PowerShell 7
- Syncfusion license key for local UI execution
- Optional: QuickBooks credentials if you are working on QuickBooks flows

## First-Time Setup

1. Restore the solution.

   ```powershell
   dotnet restore WileyWidget.sln
   ```

2. Configure the Syncfusion license.

   ```powershell
   pwsh ./scripts/setup-license.ps1
   ```

3. If you rely on local MCP-assisted tooling in Visual Studio or VS Code, regenerate the local MCP config.

   ```powershell
   pwsh ./scripts/generate-vs-mcp-config.ps1
   ```

4. Build the solution.

   ```powershell
   dotnet build WileyWidget.sln -m:2
   ```

5. Run the application.

   ```powershell
   dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
   ```

## Everyday Workflow

1. Start with the smallest build that gives useful signal.
   - VS Code: `build: fast`
   - Full verification: `build`

2. Make the smallest safe change.

3. Prove the behavior you changed.
   - Use `docs/TESTING_STRATEGY.md` to choose the right lane.
   - Prefer targeted shell, panel, smoke, or integration tests over broad low-signal runs.
   - If you touch a shared method, MainForm, navigation, layout, theme handling, or panel infrastructure, add or update regression proof for the existing behavior at risk.

4. Only use a manual smoke path when automated proof is impractical, and document what you checked.

## What Not To Count As Proof

- `dotnet test WileyWidget.sln` by itself
- stale reports under `Reports/`
- filtered runs that discover zero tests
- tests that only mirror implementation details without protecting real behavior

## Useful Local Commands

Build fast:

```powershell
dotnet build WileyWidget.sln -m:2 -p:RunAnalyzers=false -p:RunAnalyzersDuringBuild=false
```

Run the app:

```powershell
dotnet run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
```

Kill lingering test processes before rerunning focused WinForms tests:

```powershell
pwsh ./scripts/maintenance/kill-test-processes.ps1
```

Optional QuickBooks OAuth setup:

```powershell
pwsh ./scripts/quickbooks/setup-oauth.ps1
```

## If Something Looks Off

- License or Syncfusion setup issues: `scripts/verify-syncfusion-setup.ps1`
- MCP config drift: `scripts/generate-vs-mcp-config.ps1`
- Startup issues: `scripts/verify-startup.ps1`
- Layout or panel drift: start with `docs/TESTING_STRATEGY.md` and the focused WinForms test tasks in `.vscode/tasks.json`

## Read Next

- `CONTRIBUTING.md`
- `docs/TESTING_STRATEGY.md`
- `docs/PRE_RELEASE_CHECKLIST.md`
- `.vscode/approved-workflow.md`
