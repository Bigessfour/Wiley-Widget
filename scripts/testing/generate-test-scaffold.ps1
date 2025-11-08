<#
.SYNOPSIS
Generate a new test scaffold from template with MCP integration

.DESCRIPTION
Creates a new .csx test file from the scaffold template with:
- Proper naming conventions
- Test category setup
- MCP-ready structure
- Copilot integration hooks

.PARAMETER TestName
Name of the test (e.g., "BudgetCalculation", "UserAuthentication")

.PARAMETER Category
Test category: Repository, ViewModel, Service, Integration, E2E

.PARAMETER OutputDir
Output directory (defaults to scripts/examples/csharp/)

.PARAMETER NuGetPackages
Comma-separated list of NuGet packages to reference

.EXAMPLE
.\scripts\generate-test-scaffold.ps1 -TestName "BudgetCalculation" -Category "ViewModel"

.EXAMPLE
.\scripts\generate-test-scaffold.ps1 -TestName "UserRepository" -Category "Repository" -NuGetPackages "Microsoft.EntityFrameworkCore.InMemory,Moq"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TestName,

    [Parameter(Mandatory = $true)]
    [ValidateSet("Repository", "ViewModel", "Service", "Integration", "E2E")]
    [string]$Category,

    [Parameter(Mandatory = $false)]
    [string]$OutputDir,

    [Parameter(Mandatory = $false)]
    [string]$NuGetPackages,

    [Parameter(Mandatory = $false)]
    [switch]$RunAfterCreate
)

$ErrorActionPreference = "Stop"
$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path $scriptRoot -Parent

if (-not $OutputDir) {
    $OutputDir = Join-Path $scriptRoot "examples\csharp"
}

# Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Generate filename
$sanitizedName = $TestName -replace '[^a-zA-Z0-9]', ''
$nextNumber = (Get-ChildItem -Path $OutputDir -Filter "*.csx" |
    ForEach-Object {
        if ($_.Name -match '^(\d+)-') { [int]$matches[1] }
    } |
    Measure-Object -Maximum).Maximum + 1

$filename = "{0:D2}-{1}-{2}-test.csx" -f $nextNumber, $sanitizedName.ToLower(), $Category.ToLower()
$filepath = Join-Path $OutputDir $filename

Write-Host "=== Generating Test Scaffold ===" -ForegroundColor Cyan
Write-Host "Test Name: $TestName"
Write-Host "Category: $Category"
Write-Host "Output: $filepath`n"

# Read template
$templatePath = Join-Path $scriptRoot "templates\test-scaffold-template.csx"
if (-not (Test-Path $templatePath)) {
    Write-Host "❌ Template not found: $templatePath" -ForegroundColor Red
    exit 1
}

$template = Get-Content $templatePath -Raw

# Replace placeholders
$content = $template `
    -replace '\[YOUR TEST NAME\]', $TestName `
    -replace '\[Repository\|ViewModel\|Service\|Integration\]', $Category `
    -replace '\[Describe what this test validates\]', "Validates $TestName behavior in $Category layer" `
    -replace '\[List NuGet packages if needed\]', $(if ($NuGetPackages) { $NuGetPackages } else { "None" }) `
    -replace '\[Category\]', $Category `
    -replace '\[Description\]', "Test case description" `
    -replace '\[Assertion description\]', "Expected behavior verified" `
    -replace '\[Test name\]', $TestName `
    -replace '\[Percentage or N/A\]', "N/A" `
    -replace '\[What to implement or fix next\]', "Implement actual test logic"

# Add NuGet references if specified
if ($NuGetPackages) {
    $references = ($NuGetPackages -split ',') | ForEach-Object {
        "#r `"nuget: $($_.Trim())`""
    }
    $referenceBlock = $references -join "`n"
    $content = "$referenceBlock`n`n$content"
}

# Write file
$content | Out-File -FilePath $filepath -Encoding UTF8

Write-Host "✓ Test scaffold created: $filename" -ForegroundColor Green

# Create quick reference card
$refCard = @"

=== Quick Reference for $filename ===

1. Edit the test:
   code "$filepath"

2. Run via MCP:
   .\scripts\design-phase-workflow.ps1 -ScriptName "$filename"

3. Run with context generation:
   .\scripts\design-phase-workflow.ps1 -ScriptName "$filename" -GenerateContext

4. Run in Docker directly:
   docker run -i --rm \`
     -v "$OutputDir:/app:ro" \`
     -v "$repoRoot/logs:/logs:rw" \`
     -e WW_REPO_ROOT="/app" \`
     -e WW_LOGS_DIR="/logs" \`
     ghcr.io/infinityflowapp/csharp-mcp:latest \`
     "/app/$filename"

5. Add to CI pipeline:
   Add task to csx:run-all-prism-tests in .vscode/tasks.json

=== Copilot Integration ===

Use this prompt after running:
@workspace Review the test results for $filename and suggest:
1. Improvements to test coverage
2. Additional test cases
3. Mock setup optimizations
4. Assertion enhancements

"@

Write-Host $refCard

# Open in editor if requested
if ($RunAfterCreate) {
    Write-Host "`n▶ Running test..." -ForegroundColor Yellow
    & "$scriptRoot\design-phase-workflow.ps1" -ScriptName $filename -GenerateContext
} else {
    Write-Host "`n💡 Tip: Use -RunAfterCreate to automatically run the test after creation" -ForegroundColor Cyan
}
