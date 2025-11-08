# Wiley Widget PowerShell Execution Policy

This policy defines the minimum requirements for running PowerShell in the Wiley Widget workspace. All ad-hoc commands, scripts, and automation **must** comply with these rules before execution. The goal is to keep strict mode enforcement (`Set-StrictMode -Version Latest`) productive instead of disruptive.

## 1. Mandatory Pre-Run Checklist

Before running any script:

1. **Declare every variable** – assign a value _before_ it is read. Never rely on implicit `$null` defaults.
2. **Validate parameters** – use `[Parameter(Mandatory)]` attributes or explicit `if (-not $Param) { throw "..." }` checks.
3. **Separate output streams** – when using `Start-Process`, redirect standard output and error to different files or use `-NoNewWindow -PassThru` to inherit the console.
4. **Verify paths** – create directories or files referenced by redirection prior to execution.
5. **Enable strict mode** – include `Set-StrictMode -Version Latest` at the top unless testing legacy code.
6. **Set `$ErrorActionPreference = 'Stop'`** to fail fast.

A script that fails any step must be corrected before it can run.

## 2. Required Script Structure

Every new or updated PowerShell script must:

- Include `param (...)` with strong types for inputs.
- Call `Set-StrictMode -Version Latest` and `$ErrorActionPreference = 'Stop'` at the top.
- Wrap external calls in `try { } catch { } finally { }` as needed, logging failures via `Write-Error`.
- Return data via `Write-Output` (never `Write-Host`).
- Support `-WhatIf` / `-Confirm` when destructive actions are possible (`[CmdletBinding(SupportsShouldProcess)]`).

## 3. Safe Command Helper

Use `scripts/Invoke-WileyWidgetProcess.ps1` to execute external commands safely. The helper enforces:

- Unique log file names (stdout vs. stderr).
- Automatic directory creation for log paths.
- Configurable timeout with clean cancellation.
- Consistent structured logging.

### Example

```powershell
pwsh -NoProfile -File scripts/Invoke-WileyWidgetProcess.ps1 \
    -FilePath 'dotnet' \
    -ArgumentList @('run','--project','WileyWidget.csproj','--no-build') \
    -WorkingDirectory 'C:/Users/biges/Desktop/Wiley_Widget' \
    -StdOutLog 'logs/dotnet-run.out.log' \
    -StdErrLog 'logs/dotnet-run.err.log' \
    -TimeoutSeconds 60
```

## 4. Enforcement

- Pull requests adding or modifying PowerShell must link to this policy.
- CI validations execute PowerShell under strict mode; scripts that violate the checklist will fail fast.
- Developers encountering an issue must update the script or helper—disabling strict mode or reusing the same log file path is not permitted.

Following this policy eliminates the recurring "variable has not been set" and `Start-Process` redirection errors while keeping scripts robust and auditable.
