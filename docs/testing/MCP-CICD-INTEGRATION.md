# WileyWidget MCP Server - CI/CD Integration Guide

This guide provides patterns and scripts for integrating the **WileyWidget MCP Server** into CI/CD pipelines, PR reviews, and pre-commit hooks.

## Table of Contents

- [Overview](#overview)
- [Pre-Commit Hook Integration](#pre-commit-hook-integration)
- [GitHub Actions Workflow](#github-actions-workflow)
- [PR Review Automation](#pr-review-automation)
- [Local Development Scripts](#local-development-scripts)
- [PowerShell Helper Functions](#powershell-helper-functions)
- [Azure DevOps Pipeline](#azure-devops-pipeline)

---

## Overview

The MCP server tools can be invoked programmatically for automated validation:

| Tool                  | CI/CD Use Case         | Exit Code Pattern        |
| --------------------- | ---------------------- | ------------------------ |
| `ValidateFormTheme`   | Theme compliance gate  | 0 = pass, 1 = violations |
| `InspectSfDataGrid`   | Grid config validation | 0 = valid, 1 = errors    |
| `EvalCSharp`          | Custom assertions      | 0 = pass, 1 = fail       |
| `RunHeadlessFormTest` | Regression testing     | 0 = pass, 1 = fail       |

**Invocation Pattern:**

```powershell
dotnet run --project tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj --no-build -- <tool> <args>
```

---

## Pre-Commit Hook Integration

### Git Hook Setup (`scripts/git-hooks/pre-commit`)

```bash
#!/bin/bash
# Pre-commit hook to validate theme compliance on changed forms

echo "🔍 Running theme validation on changed forms..."

# Get list of changed form files
CHANGED_FORMS=$(git diff --cached --name-only --diff-filter=ACM | grep "src/WileyWidget.WinForms/Forms/.*\.cs$")

if [ -z "$CHANGED_FORMS" ]; then
    echo "✅ No form files changed"
    exit 0
fi

# Build MCP server
echo "Building MCP server..."
dotnet build tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj --nologo --verbosity quiet

if [ $? -ne 0 ]; then
    echo "❌ MCP server build failed"
    exit 1
fi

# Track failures
FAILED_FORMS=""

# Validate each changed form
for FILE in $CHANGED_FORMS; do
    # Extract form name from path (e.g., AccountsForm.cs -> AccountsForm)
    FORM_NAME=$(basename "$FILE" .cs)

    # Skip base classes and partial files
    if [[ "$FORM_NAME" == *"Base"* ]] || [[ "$FORM_NAME" == *"Extensions"* ]]; then
        continue
    fi

    echo "  Validating $FORM_NAME..."

    # Invoke ValidateFormTheme via MCP tool
    RESULT=$(dotnet run --project tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj --no-build 2>&1 | \
        jq -r --arg form "WileyWidget.WinForms.Forms.$FORM_NAME" \
        '.result.content[] | select(.text | contains("❌")) | .text')

    if [ ! -z "$RESULT" ]; then
        FAILED_FORMS="$FAILED_FORMS\n  - $FORM_NAME"
    fi
done

# Report results
if [ ! -z "$FAILED_FORMS" ]; then
    echo ""
    echo "❌ Theme validation failed for:$FAILED_FORMS"
    echo ""
    echo "Fix violations before committing. Run:"
    echo "  dotnet run --project tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj"
    exit 1
fi

echo "✅ All forms passed theme validation"
exit 0
```

**Installation:**

```bash
cp scripts/git-hooks/pre-commit .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
```

---

## GitHub Actions Workflow

### `.github/workflows/ui-validation.yml`

```yaml
name: UI Validation

on:
  pull_request:
    paths:
      - "src/WileyWidget.WinForms/Forms/**/*.cs"
      - "tools/WileyWidgetMcpServer/**"
  workflow_dispatch:

jobs:
  validate-forms:
    runs-on: windows-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET 9
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "9.0.x"

      - name: Restore dependencies
        run: dotnet restore WileyWidget.sln

      - name: Build solution
        run: dotnet build WileyWidget.sln --no-restore --configuration Release

      - name: Build MCP Server
        run: dotnet build tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj --no-restore

      - name: Validate Form Themes
        id: validate
        shell: pwsh
        run: |
          $forms = @(
            "AccountsForm",
            "BudgetOverviewForm",
            "ChartForm",
            "DashboardForm",
            "CustomersForm",
            "ReportsForm",
            "SettingsForm"
          )

          $failures = @()

          foreach ($form in $forms) {
            Write-Host "Validating $form..."

            # Note: Requires JSON-RPC wrapper script (see PowerShell Helpers below)
            $result = & scripts/testing/invoke-mcp-tool.ps1 `
              -Tool "ValidateFormTheme" `
              -Params @{
                formTypeName = "WileyWidget.WinForms.Forms.$form"
                expectedTheme = "Office2019Colorful"
              }

            if ($result.Contains("❌")) {
              $failures += $form
              Write-Host "::error::$form failed theme validation"
            }
          }

          if ($failures.Count -gt 0) {
            Write-Host "::error::Failed forms: $($failures -join ', ')"
            exit 1
          }

          Write-Host "✅ All forms passed validation"

      - name: Upload validation report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: ui-validation-report
          path: tmp/validation-report.json
```

---

## PR Review Automation

### GitHub Actions - Automated PR Comments

```yaml
name: PR UI Review

on:
  pull_request:
    types: [opened, synchronize]
    paths:
      - "src/WileyWidget.WinForms/Forms/**/*.cs"

permissions:
  pull-requests: write
  contents: read

jobs:
  ui-review:
    runs-on: windows-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4
        with:
          fetch-depth: 0 # Need full history for diff

      - name: Setup .NET 9
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "9.0.x"

      - name: Build and validate
        id: validation
        shell: pwsh
        run: |
          # Get changed form files
          $changedForms = git diff --name-only origin/${{ github.base_ref }}..HEAD |
            Where-Object { $_ -like "src/WileyWidget.WinForms/Forms/*.cs" } |
            ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) }

          if ($changedForms.Count -eq 0) {
            Write-Host "No forms changed"
            echo "has_changes=false" >> $env:GITHUB_OUTPUT
            exit 0
          }

          echo "has_changes=true" >> $env:GITHUB_OUTPUT

          # Build solution
          dotnet build WileyWidget.sln --configuration Release
          dotnet build tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj

          # Validate each form and generate report
          $report = @()

          foreach ($form in $changedForms) {
            $result = & scripts/testing/invoke-mcp-tool.ps1 `
              -Tool "ValidateFormTheme" `
              -Params @{ formTypeName = "WileyWidget.WinForms.Forms.$form" }

            $report += @{
              form = $form
              passed = -not $result.Contains("❌")
              details = $result
            }
          }

          # Save report for next step
          $report | ConvertTo-Json | Out-File -FilePath tmp/pr-validation.json

      - name: Comment on PR
        if: steps.validation.outputs.has_changes == 'true'
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            const report = JSON.parse(fs.readFileSync('tmp/pr-validation.json', 'utf8'));

            let comment = '## 🎨 UI Validation Report\n\n';
            comment += '| Form | Status | Details |\n';
            comment += '|------|--------|----------|\n';

            for (const item of report) {
              const status = item.passed ? '✅ PASS' : '❌ FAIL';
              const details = item.passed ? 'Theme compliant' : 'Violations found';
              comment += `| ${item.form} | ${status} | ${details} |\n`;
            }

            const failCount = report.filter(r => !r.passed).length;

            if (failCount > 0) {
              comment += '\n⚠️ **Action Required:** Fix theme violations before merging.\n';
              comment += '\nRun locally: `dotnet run --project tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj`\n';
            } else {
              comment += '\n✅ All forms passed theme validation!\n';
            }

            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: comment
            });
```

---

## Local Development Scripts

### PowerShell: Validate All Forms

**`scripts/testing/validate-all-forms.ps1`**

```powershell
<#
.SYNOPSIS
    Validates theme compliance for all WinForms in the project.

.DESCRIPTION
    Builds the MCP server and validates all form files for theme compliance.
    Generates a summary report and exits with non-zero code if any failures.

.EXAMPLE
    .\scripts\testing\validate-all-forms.ps1
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Office2019Colorful', 'MaterialDark', 'FluentLight')]
    [string]$Theme = 'Office2019Colorful',

    [Parameter()]
    [switch]$FailFast,

    [Parameter()]
    [switch]$GenerateReport
)

$ErrorActionPreference = 'Stop'

# Build MCP server
Write-Host "Building MCP server..." -ForegroundColor Cyan
dotnet build tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj --nologo --verbosity minimal

if ($LASTEXITCODE -ne 0) {
    Write-Error "MCP server build failed"
    exit 1
}

# Discover all form files
$formFiles = Get-ChildItem -Path "src/WileyWidget.WinForms/Forms" -Filter "*.cs" |
    Where-Object { $_.Name -notlike "*Base.cs" -and $_.Name -notlike "*.Extensions.cs" -and $_.Name -notlike "*.UI.cs" }

Write-Host "`nValidating $($formFiles.Count) forms..." -ForegroundColor Cyan
Write-Host ("=" * 60)

$results = @()
$failCount = 0

foreach ($file in $formFiles) {
    $formName = $file.BaseName
    $fullTypeName = "WileyWidget.WinForms.Forms.$formName"

    Write-Host "`n[$($formFiles.IndexOf($file) + 1)/$($formFiles.Count)] $formName" -ForegroundColor Yellow

    # Invoke MCP tool (requires helper function - see next section)
    $result = Invoke-McpTool -Tool "ValidateFormTheme" -Params @{
        formTypeName = $fullTypeName
        expectedTheme = $Theme
    }

    $passed = -not ($result -match "❌")

    $results += [PSCustomObject]@{
        Form = $formName
        Passed = $passed
        Theme = $Theme
        Details = $result
        Timestamp = Get-Date
    }

    if ($passed) {
        Write-Host "  ✅ PASS" -ForegroundColor Green
    } else {
        Write-Host "  ❌ FAIL" -ForegroundColor Red
        $failCount++

        if ($FailFast) {
            Write-Error "Validation failed for $formName. Stopping due to -FailFast."
            exit 1
        }
    }
}

# Summary
Write-Host "`n" + ("=" * 60)
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Total: $($results.Count)" -ForegroundColor White
Write-Host "  Passed: $($results.Count - $failCount)" -ForegroundColor Green
Write-Host "  Failed: $failCount" -ForegroundColor $(if ($failCount -gt 0) { 'Red' } else { 'White' })

if ($GenerateReport) {
    $reportPath = "tmp/validation-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    New-Item -Path (Split-Path $reportPath -Parent) -ItemType Directory -Force | Out-Null
    $results | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportPath
    Write-Host "`nReport saved: $reportPath" -ForegroundColor Cyan
}

exit $failCount
```

### PowerShell: Quick Form Validation

**`scripts/testing/validate-form.ps1`**

```powershell
<#
.SYNOPSIS
    Validates a single form's theme compliance.

.EXAMPLE
    .\scripts\testing\validate-form.ps1 -FormName AccountsForm
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$FormName,

    [Parameter()]
    [string]$Theme = 'Office2019Colorful'
)

$fullTypeName = "WileyWidget.WinForms.Forms.$FormName"

Write-Host "Validating $FormName..." -ForegroundColor Cyan

$result = Invoke-McpTool -Tool "ValidateFormTheme" -Params @{
    formTypeName = $fullTypeName
    expectedTheme = $Theme
}

Write-Host $result

$passed = -not ($result -match "❌")
exit $(if ($passed) { 0 } else { 1 })
```

---

## PowerShell Helper Functions

### MCP Tool Invocation Wrapper

**`scripts/testing/invoke-mcp-tool.ps1`**

```powershell
<#
.SYNOPSIS
    Invokes a WileyWidget MCP Server tool and returns the result.

.DESCRIPTION
    This is a helper wrapper that communicates with the MCP server via JSON-RPC over STDIO.
    Simplifies tool invocation for automation scripts.

.EXAMPLE
    Invoke-McpTool -Tool "ValidateFormTheme" -Params @{
        formTypeName = "WileyWidget.WinForms.Forms.AccountsForm"
        expectedTheme = "Office2019Colorful"
    }
#>

function Invoke-McpTool {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('ValidateFormTheme', 'InspectSfDataGrid', 'EvalCSharp', 'RunHeadlessFormTest')]
        [string]$Tool,

        [Parameter(Mandatory)]
        [hashtable]$Params,

        [Parameter()]
        [int]$TimeoutSeconds = 30
    )

    $mcpServerPath = "tools/WileyWidgetMcpServer/bin/Debug/net9.0-windows10.0.26100.0/WileyWidgetMcpServer.dll"

    # Build JSON-RPC request
    $request = @{
        jsonrpc = "2.0"
        id = [Guid]::NewGuid().ToString()
        method = "tools/call"
        params = @{
            name = $Tool
            arguments = $Params
        }
    } | ConvertTo-Json -Depth 10 -Compress

    # Launch MCP server process
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "dotnet"
    $psi.Arguments = $mcpServerPath
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    $process.Start() | Out-Null

    # Send request
    $process.StandardInput.WriteLine($request)
    $process.StandardInput.Close()

    # Read response with timeout
    $output = New-Object System.Text.StringBuilder
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    while (-not $process.HasExited -and $stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if (-not $process.StandardOutput.EndOfStream) {
            $line = $process.StandardOutput.ReadLine()
            [void]$output.AppendLine($line)
        }
        Start-Sleep -Milliseconds 100
    }

    if (-not $process.HasExited) {
        $process.Kill()
        throw "MCP server timeout after $TimeoutSeconds seconds"
    }

    # Parse JSON-RPC response
    $response = $output.ToString() | ConvertFrom-Json

    if ($response.error) {
        throw "MCP error: $($response.error.message)"
    }

    return $response.result.content[0].text
}
```

---

## Azure DevOps Pipeline

### `azure-pipelines-ui-validation.yml`

```yaml
trigger:
  branches:
    include:
      - main
      - develop
  paths:
    include:
      - src/WileyWidget.WinForms/Forms/**

pool:
  vmImage: "windows-latest"

variables:
  buildConfiguration: "Release"

steps:
  - task: UseDotNet@2
    displayName: "Install .NET 9 SDK"
    inputs:
      version: "9.x"

  - task: DotNetCoreCLI@2
    displayName: "Restore dependencies"
    inputs:
      command: "restore"
      projects: "WileyWidget.sln"

  - task: DotNetCoreCLI@2
    displayName: "Build solution"
    inputs:
      command: "build"
      projects: "WileyWidget.sln"
      arguments: "--no-restore --configuration $(buildConfiguration)"

  - task: DotNetCoreCLI@2
    displayName: "Build MCP Server"
    inputs:
      command: "build"
      projects: "tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj"
      arguments: "--no-restore"

  - task: PowerShell@2
    displayName: "Validate Form Themes"
    inputs:
      filePath: "scripts/testing/validate-all-forms.ps1"
      arguments: "-GenerateReport"
      failOnStderr: true
      pwsh: true

  - task: PublishTestResults@2
    displayName: "Publish validation results"
    condition: always()
    inputs:
      testResultsFormat: "JUnit"
      testResultsFiles: "**/validation-report-*.json"
      mergeTestResults: true
      testRunTitle: "UI Theme Validation"

  - task: PublishBuildArtifacts@1
    displayName: "Publish validation artifacts"
    condition: always()
    inputs:
      pathToPublish: "tmp"
      artifactName: "ui-validation-reports"
```

---

## Best Practices

### 1. **Incremental Validation**

Only validate changed forms in PR pipelines to save CI time:

```powershell
$changedForms = git diff --name-only origin/main..HEAD |
    Where-Object { $_ -like "src/WileyWidget.WinForms/Forms/*.cs" }
```

### 2. **Caching**

Cache build outputs and MCP server binaries:

```yaml
- task: Cache@2
  inputs:
    key: 'mcp-server | "$(Agent.OS)" | tools/WileyWidgetMcpServer/**'
    path: "tools/WileyWidgetMcpServer/bin"
```

### 3. **Parallel Execution**

Validate multiple forms in parallel for faster pipelines:

```powershell
$forms | ForEach-Object -Parallel {
    Invoke-McpTool -Tool "ValidateFormTheme" -Params @{ formTypeName = $_ }
} -ThrottleLimit 4
```

### 4. **Failure Annotations**

Add source file annotations for failures:

```yaml
- name: Annotate failures
  if: failure()
  run: |
    # Parse validation report and add annotations
    jq -r '.[] | select(.Passed == false) | "::error file=src/WileyWidget.WinForms/Forms/\(.Form).cs::Theme validation failed"' tmp/validation-report.json
```

---

## Troubleshooting

### Issue: "MCP server build failed"

**Solution:** Ensure dependencies are restored:

```powershell
dotnet restore WileyWidget.sln
dotnet build src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
```

### Issue: "Form type not found"

**Solution:** Check fully qualified type name:

```powershell
# Verify namespace in form file
Get-Content src/WileyWidget.WinForms/Forms/AccountsForm.cs | Select-String "namespace"
```

### Issue: Timeout in CI

**Solution:** Increase timeout and add verbose logging:

```powershell
$env:MCP_DEBUG = "1"
Invoke-McpTool -TimeoutSeconds 60
```

---

## See Also

- [MCP Copilot Prompts](MCP-COPILOT-PROMPTS.md)
- [MCP Integration Guide](MCP-INTEGRATION-GUIDE.md)
- [WileyWidget MCP Server README](../../tools/WileyWidgetMcpServer/README.md)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
