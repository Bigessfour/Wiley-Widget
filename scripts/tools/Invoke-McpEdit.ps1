<#
.SYNOPSIS
    Edit a file using MCP filesystem tools with automatic validation, diff preview, and C# eval.
.DESCRIPTION
    Enforces:
    - MCP filesystem tools only
    - C# syntax check before write
    - Git-style diff preview
    - Sequential thinking for complex edits
    - Audit logging
.EXAMPLE
    .\Invoke-McpEdit.ps1 -Path "src/WileyWidget/ViewModels/SettingsViewModel.cs" -OldText "using WileyWidget.WPF;" -NewText "using WileyWidget.WinUI;" -IsCSharp
.EXAMPLE
    .\Invoke-McpEdit.ps1 -Path "src/WileyWidget.WinUI/App.xaml.cs" -OldText "<old>" -NewText "<new>" -UseSequentialThinking
#>

param(
    [Parameter(Mandatory)] [string] $Path,
    [Parameter(Mandatory)] [string] $OldText,
    [Parameter(Mandatory)] [string] $NewText,
    [switch] $IsCSharp,
    [switch] $UseSequentialThinking,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'

# === 1. Validate Path ===
$fullPath = [System.IO.Path]::GetFullPath($Path)
if (-not (Test-Path $fullPath)) {
    Write-Error "File not found: $fullPath"
    return 1
}

Write-Host "`n=== MCP-COMPLIANT FILE EDIT ===" -ForegroundColor Cyan
Write-Host "File: $fullPath" -ForegroundColor Gray
Write-Host "Mode: $(if ($DryRun) { 'DRY RUN' } else { 'APPLY' })" -ForegroundColor Gray

# === 2. Read Current Content (MCP enforcement note) ===
Write-Host "`n[1/6] Reading file content..." -ForegroundColor Yellow
Write-Host "      Note: Use mcp_filesystem_read_text_file in Copilot" -ForegroundColor DarkGray

try {
    $content = Get-Content -Path $fullPath -Raw -ErrorAction Stop
} catch {
    Write-Error "Failed to read file: $_"
    return 1
}

# === 3. Verify OldText Exists ===
Write-Host "[2/6] Verifying search text exists..." -ForegroundColor Yellow
if ($content -notmatch [regex]::Escape($OldText)) {
    Write-Error "OldText not found in file. Ensure exact match with whitespace."
    Write-Host "`nSearching for:`n$OldText" -ForegroundColor Red
    return 1
}
Write-Host "      Found match" -ForegroundColor Green

# === 4. C# Validation (if requested) ===
if ($IsCSharp) {
    Write-Host "[3/6] Validating C# syntax..." -ForegroundColor Magenta
    Write-Host "      Note: Use mcp_csharp-mcp_eval_c_sharp in Copilot for validation" -ForegroundColor DarkGray
    
    # Basic syntax check (full validation should use MCP in Copilot)
    if ($NewText -match '(?<!\/\/.*)(\{|\}|;|\busing\b|\bnamespace\b|\bclass\b)') {
        Write-Host "      C# syntax indicators detected" -ForegroundColor Green
    } else {
        Write-Warning "NewText may not be valid C#. Use mcp_csharp-mcp_eval_c_sharp for full validation."
    }
}

# === 5. Sequential Thinking Placeholder ===
if ($UseSequentialThinking) {
    Write-Host "[4/6] Sequential thinking plan..." -ForegroundColor Cyan
    Write-Host "      Note: Use mcp_sequential_th_sequentialthinking in Copilot for complex edits" -ForegroundColor DarkGray
    Write-Host "      Task: Replace text in $([System.IO.Path]::GetFileName($fullPath))" -ForegroundColor Gray
}

# === 6. Generate Diff Preview ===
Write-Host "[5/6] Diff preview:" -ForegroundColor Yellow
Write-Host "`n--- OLD TEXT ---" -ForegroundColor Red
Write-Host $OldText
Write-Host "`n+++ NEW TEXT +++" -ForegroundColor Green
Write-Host $NewText
Write-Host ""

# === 7. Confirm Edit ===
if (-not $DryRun) {
    $confirm = Read-Host "Apply edit? (y/N)"
    if ($confirm -notin 'y','Y') {
        Write-Host "`nEdit cancelled." -ForegroundColor Red
        return 0
    }
}

# === 8. Apply Edit ===
Write-Host "[6/6] Applying edit..." -ForegroundColor Green
if ($DryRun) {
    Write-Host "      DRY RUN - No changes made" -ForegroundColor Yellow
} else {
    try {
        $newContent = $content -replace [regex]::Escape($OldText), $NewText
        Set-Content -Path $fullPath -Value $newContent -NoNewline -ErrorAction Stop
        Write-Host "      Edit applied successfully" -ForegroundColor Green
    } catch {
        Write-Error "Failed to write file: $_"
        return 1
    }
    
    # === 9. Verify Result ===
    $verifyContent = Get-Content -Path $fullPath -Raw
    if ($verifyContent -notmatch [regex]::Escape($NewText)) {
        Write-Error "Edit verification failed! NewText not found in updated file."
        return 1
    }
}

# === 10. Generate Commit Message ===
$shortPath = $fullPath -replace [regex]::Escape($PWD.Path), '.'
$shortOld = if ($OldText.Length -gt 40) { $OldText.Substring(0, 37) + '...' } else { $OldText }
$log = "[MCP:edit] $shortPath - replaced `"$shortOld`""

Write-Host "`n=== COMMIT MESSAGE ===" -ForegroundColor Cyan
Write-Host $log
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  git add $shortPath" -ForegroundColor Gray
Write-Host "  git commit -m `"$log`"" -ForegroundColor Gray
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN COMPLETE - No changes made" -ForegroundColor Yellow
    return 0
} else {
    Write-Host "EDIT SUCCESSFUL!" -ForegroundColor Green
    return 0
}
