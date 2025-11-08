#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Automated setup script for Continue.dev + Ollama + FlaUI E2E testing.

.DESCRIPTION
    Sets up the complete AI-powered E2E testing stack for Wiley Widget:
    - Installs Continue.dev VS Code extension
    - Downloads and configures Ollama with DeepSeek-Coder
    - Installs FlaUI NuGet packages
    - Creates test infrastructure files
    - Validates the complete setup

.PARAMETER SkipOllama
    Skip Ollama installation (use if already installed).

.PARAMETER SkipContinue
    Skip Continue.dev extension installation.

.PARAMETER SkipPackages
    Skip NuGet package installation.

.PARAMETER OllamaModel
    Ollama model to download (default: deepseek-coder:6.7b-instruct).

.EXAMPLE
    .\setup-ai-e2e-testing.ps1

.EXAMPLE
    .\setup-ai-e2e-testing.ps1 -SkipOllama -OllamaModel "codellama:7b-instruct"

.NOTES
    Author: AI-Assisted Setup
    Date: November 3, 2025
    Requires: PowerShell 7.5+, Windows 10/11, VS Code
#>

[CmdletBinding()]
param(
    [switch]$SkipOllama,
    [switch]$SkipContinue,
    [switch]$SkipPackages,
    [string]$OllamaModel = "deepseek-coder:6.7b-instruct"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "Continue"

# Configuration
$RepoRoot = Split-Path $PSScriptRoot -Parent
$TestProjectPath = Join-Path $RepoRoot "WileyWidget.Tests"
$TestProjectFile = Join-Path $TestProjectPath "WileyWidget.Tests.csproj"
$ContinueConfigPath = Join-Path $env:USERPROFILE ".continue\config.json"
$OllamaInstallPath = "C:\Users\$env:USERNAME\AppData\Local\Programs\Ollama\ollama.exe"

Write-Host "🚀 Wiley Widget AI E2E Testing Setup" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

#region Step 1: Install Continue.dev Extension
if (-not $SkipContinue) {
    Write-Host "📦 Step 1: Installing Continue.dev VS Code Extension..." -ForegroundColor Yellow

    # Check if VS Code is installed
    $vscodePath = (Get-Command code -ErrorAction SilentlyContinue).Source
    if (-not $vscodePath) {
        Write-Warning "VS Code 'code' command not found in PATH. Install VS Code first."
        Write-Host "   Download: https://code.visualstudio.com/" -ForegroundColor Gray
        exit 1
    }

    # Install Continue.dev extension
    Write-Host "   Installing Continue extension..." -ForegroundColor Gray
    $installResult = & code --install-extension Continue.continue --force 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Continue.dev extension installed successfully" -ForegroundColor Green
    } else {
        Write-Warning "Continue.dev installation may have failed. Check manually with: code --list-extensions"
    }

    # Verify installation
    $extensions = & code --list-extensions 2>&1
    if ($extensions -match "Continue\.continue") {
        Write-Host "   ✅ Verified: Continue.continue is installed" -ForegroundColor Green
    } else {
        Write-Warning "Continue.dev not detected. Try manual installation from VS Code Extensions marketplace."
    }
} else {
    Write-Host "⏭️  Step 1: Skipping Continue.dev installation (--SkipContinue specified)" -ForegroundColor Gray
}
Write-Host ""

#region Step 2: Install Ollama
if (-not $SkipOllama) {
    Write-Host "🤖 Step 2: Installing Ollama Local LLM Runtime..." -ForegroundColor Yellow

    # Check if Ollama is already installed
    $ollamaExists = Test-Path $OllamaInstallPath
    if ($ollamaExists) {
        Write-Host "   ✅ Ollama already installed at: $OllamaInstallPath" -ForegroundColor Green
        $ollamaVersion = & $OllamaInstallPath --version 2>&1
        Write-Host "   Version: $ollamaVersion" -ForegroundColor Gray
    } else {
        Write-Host "   📥 Downloading Ollama installer..." -ForegroundColor Gray
        Write-Host "   NOTE: Manual download required from https://ollama.com/download" -ForegroundColor Cyan
        Write-Host "   Run this script again after installing Ollama." -ForegroundColor Cyan

        # Open download page
        Start-Process "https://ollama.com/download/windows"

        Write-Host ""
        Write-Host "   ⏸️  Pausing for Ollama installation..." -ForegroundColor Yellow
        Write-Host "   Press Enter after installing Ollama to continue..." -ForegroundColor Yellow
        Read-Host

        # Re-check installation
        if (Test-Path $OllamaInstallPath) {
            Write-Host "   ✅ Ollama installation detected" -ForegroundColor Green
        } else {
            Write-Error "Ollama not found at expected path. Install manually and re-run."
            exit 1
        }
    }

    # Start Ollama service if not running
    $ollamaProcess = Get-Process -Name "ollama" -ErrorAction SilentlyContinue
    if (-not $ollamaProcess) {
        Write-Host "   🔄 Starting Ollama service..." -ForegroundColor Gray
        Start-Process -FilePath $OllamaInstallPath -ArgumentList "serve" -WindowStyle Hidden
        Start-Sleep -Seconds 3
    }

    # Verify Ollama is responding
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -TimeoutSec 5 -ErrorAction Stop
        Write-Host "   ✅ Ollama service is running on port 11434" -ForegroundColor Green
    } catch {
        Write-Warning "Ollama service not responding. Check if it started correctly."
    }

    # Download model
    Write-Host "   📥 Downloading model: $OllamaModel (this may take 5-15 minutes)..." -ForegroundColor Gray
    Write-Host "   Progress will be shown below:" -ForegroundColor Gray

    $modelPullProcess = Start-Process -FilePath $OllamaInstallPath `
        -ArgumentList "pull", $OllamaModel `
        -NoNewWindow `
        -PassThru `
        -Wait

    if ($modelPullProcess.ExitCode -eq 0) {
        Write-Host "   ✅ Model $OllamaModel downloaded successfully" -ForegroundColor Green
    } else {
        Write-Warning "Model download may have failed. Verify with: ollama list"
    }

    # Download embeddings model
    Write-Host "   📥 Downloading embeddings model (nomic-embed-text)..." -ForegroundColor Gray
    $embedProcess = Start-Process -FilePath $OllamaInstallPath `
        -ArgumentList "pull", "nomic-embed-text" `
        -NoNewWindow `
        -PassThru `
        -Wait

    if ($embedProcess.ExitCode -eq 0) {
        Write-Host "   ✅ Embeddings model downloaded" -ForegroundColor Green
    }

    # List installed models
    Write-Host ""
    Write-Host "   📋 Installed Ollama models:" -ForegroundColor Gray
    & $OllamaInstallPath list

} else {
    Write-Host "⏭️  Step 2: Skipping Ollama installation (--SkipOllama specified)" -ForegroundColor Gray
}
Write-Host ""

#region Step 3: Configure Continue.dev
Write-Host "⚙️  Step 3: Configuring Continue.dev with Ollama..." -ForegroundColor Yellow

# Create .continue directory if it doesn't exist
$continueDir = Split-Path $ContinueConfigPath -Parent
if (-not (Test-Path $continueDir)) {
    Write-Host "   Creating .continue directory..." -ForegroundColor Gray
    New-Item -ItemType Directory -Path $continueDir -Force | Out-Null
}

# Generate config.json
$continueConfig = @{
    models                  = @(
        @{
            title    = "DeepSeek Coder (Local)"
            provider = "ollama"
            model    = $OllamaModel
            apiBase  = "http://localhost:11434"
        },
        @{
            title    = "CodeLlama (Fallback)"
            provider = "ollama"
            model    = "codellama:7b-instruct"
            apiBase  = "http://localhost:11434"
        }
    )
    tabAutocompleteModel    = @{
        title    = "DeepSeek Coder"
        provider = "ollama"
        model    = $OllamaModel
    }
    slashCommands           = @(
        @{ name = "edit"; description = "Edit selected code" },
        @{ name = "comment"; description = "Write comments for code" },
        @{ name = "test"; description = "Generate unit tests" }
    )
    contextProviders        = @(
        @{ name = "diff"; params = @{} },
        @{ name = "open"; params = @{} },
        @{ name = "terminal"; params = @{} }
    )
    allowAnonymousTelemetry = $false
    embeddingsProvider      = @{
        provider = "ollama"
        model    = "nomic-embed-text"
        apiBase  = "http://localhost:11434"
    }
} | ConvertTo-Json -Depth 10

Write-Host "   Writing config to: $ContinueConfigPath" -ForegroundColor Gray
Set-Content -Path $ContinueConfigPath -Value $continueConfig -Force
Write-Host "   ✅ Continue.dev configured successfully" -ForegroundColor Green
Write-Host ""

#region Step 4: Install FlaUI NuGet Packages
if (-not $SkipPackages) {
    Write-Host "📦 Step 4: Installing FlaUI NuGet Packages..." -ForegroundColor Yellow

    if (-not (Test-Path $TestProjectFile)) {
        Write-Error "Test project not found: $TestProjectFile"
        exit 1
    }

    # Install packages
    $packages = @(
        @{ Name = "FlaUI.Core"; Version = "4.0.0" },
        @{ Name = "FlaUI.UIA3"; Version = "4.0.0" },
        @{ Name = "FlaUI.TestUtilities"; Version = "4.0.0" }
    )

    foreach ($package in $packages) {
        Write-Host "   Installing $($package.Name) v$($package.Version)..." -ForegroundColor Gray

        $addResult = dotnet add $TestProjectFile package $package.Name --version $package.Version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✅ $($package.Name) installed" -ForegroundColor Green
        } else {
            Write-Warning "Failed to install $($package.Name). Error: $addResult"
        }
    }

    # Restore packages
    Write-Host "   Restoring NuGet packages..." -ForegroundColor Gray
    dotnet restore $TestProjectFile --force-evaluate
    Write-Host "   ✅ NuGet packages restored" -ForegroundColor Green
} else {
    Write-Host "⏭️  Step 4: Skipping NuGet package installation (--SkipPackages specified)" -ForegroundColor Gray
}
Write-Host ""

#region Step 5: Create Test Infrastructure
Write-Host "🏗️  Step 5: Creating FlaUI Test Infrastructure..." -ForegroundColor Yellow

$e2eDir = Join-Path $TestProjectPath "E2E"
if (-not (Test-Path $e2eDir)) {
    Write-Host "   Creating E2E test directory..." -ForegroundColor Gray
    New-Item -ItemType Directory -Path $e2eDir -Force | Out-Null
}

# Create WpfTestBase.cs
$wpfTestBasePath = Join-Path $e2eDir "WpfTestBase.cs"
if (-not (Test-Path $wpfTestBasePath)) {
    Write-Host "   Creating WpfTestBase.cs..." -ForegroundColor Gray

    $wpfTestBaseContent = @'
using System;
using System.Diagnostics;
using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;

namespace WileyWidget.Tests.E2E;

/// <summary>
/// Base class for WPF E2E tests using FlaUI.
/// Handles application lifecycle and provides common helpers.
/// </summary>
public abstract class WpfTestBase : IDisposable
{
    protected Application? App { get; private set; }
    protected UIA3Automation Automation { get; }
    protected Window? MainWindow { get; private set; }

    protected WpfTestBase()
    {
        Automation = new UIA3Automation();
    }

    protected void LaunchApplication(string exePath, int timeoutSeconds = 30)
    {
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Application not found: {exePath}");
        }

        App = Application.Launch(exePath);
        App.WaitWhileBusy(TimeSpan.FromSeconds(timeoutSeconds));

        MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(timeoutSeconds));
        Assert.NotNull(MainWindow);
    }

    protected AutomationElement? FindElementByAutomationId(string automationId)
    {
        return MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
    }

    protected AutomationElement? FindElementByName(string name)
    {
        return MainWindow?.FindFirstDescendant(cf => cf.ByName(name));
    }

    protected AutomationElement? FindElementByClassName(string className)
    {
        return MainWindow?.FindFirstDescendant(cf => cf.ByClassName(className));
    }

    public void Dispose()
    {
        MainWindow?.Close();
        App?.Close();
        App?.Dispose();
        Automation.Dispose();
        GC.SuppressFinalize(this);
    }
}
'@

    Set-Content -Path $wpfTestBasePath -Value $wpfTestBaseContent -Force
    Write-Host "   ✅ WpfTestBase.cs created" -ForegroundColor Green
} else {
    Write-Host "   ⏭️  WpfTestBase.cs already exists" -ForegroundColor Gray
}

# Create SyncfusionHelpers.cs
$syncfusionHelpersPath = Join-Path $e2eDir "SyncfusionHelpers.cs"
if (-not (Test-Path $syncfusionHelpersPath)) {
    Write-Host "   Creating SyncfusionHelpers.cs..." -ForegroundColor Gray

    $syncfusionHelpersContent = @'
using System;
using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace WileyWidget.Tests.E2E;

/// <summary>
/// Helper methods for interacting with Syncfusion SfDataGrid controls via FlaUI.
/// </summary>
public static class SyncfusionHelpers
{
    /// <summary>
    /// Gets the row count from a Syncfusion SfDataGrid.
    /// </summary>
    public static int GetDataGridRowCount(AutomationElement dataGrid)
    {
        if (dataGrid == null) throw new ArgumentNullException(nameof(dataGrid));

        var rows = dataGrid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
        return rows.Length;
    }

    /// <summary>
    /// Gets all visible row elements from the data grid.
    /// </summary>
    public static AutomationElement[] GetAllRows(AutomationElement dataGrid)
    {
        if (dataGrid == null) throw new ArgumentNullException(nameof(dataGrid));

        return dataGrid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
    }

    /// <summary>
    /// Gets cell text from a specific row and column index.
    /// </summary>
    public static string? GetCellText(AutomationElement row, int columnIndex)
    {
        if (row == null) throw new ArgumentNullException(nameof(row));

        var cells = row.FindAllChildren(cf => cf.ByControlType(ControlType.Text));
        if (columnIndex < 0 || columnIndex >= cells.Length)
        {
            return null;
        }

        return cells[columnIndex].Name;
    }

    /// <summary>
    /// Gets all cell values from a specific column.
    /// </summary>
    public static List<string> GetColumnValues(AutomationElement dataGrid, int columnIndex)
    {
        var values = new List<string>();
        var rows = GetAllRows(dataGrid);

        foreach (var row in rows)
        {
            var cellText = GetCellText(row, columnIndex);
            if (!string.IsNullOrEmpty(cellText))
            {
                values.Add(cellText);
            }
        }

        return values;
    }

    /// <summary>
    /// Counts rows matching a specific filter condition.
    /// </summary>
    public static int CountRowsWhere(AutomationElement dataGrid, Func<AutomationElement, bool> predicate)
    {
        var rows = GetAllRows(dataGrid);
        return rows.Count(predicate);
    }

    /// <summary>
    /// Applies a filter to the data grid by typing in a filter textbox.
    /// </summary>
    public static void ApplyFilter(AutomationElement dataGrid, string filterText)
    {
        var filterBox = dataGrid.Parent.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Edit).And(cf.ByName("Filter")));

        if (filterBox != null)
        {
            filterBox.AsTextBox().Text = filterText;
            System.Threading.Thread.Sleep(500);
        }
    }
}
'@

    Set-Content -Path $syncfusionHelpersPath -Value $syncfusionHelpersContent -Force
    Write-Host "   ✅ SyncfusionHelpers.cs created" -ForegroundColor Green
} else {
    Write-Host "   ⏭️  SyncfusionHelpers.cs already exists" -ForegroundColor Gray
}

Write-Host ""

#region Step 6: Validation
Write-Host "✅ Step 6: Validating Setup..." -ForegroundColor Yellow

$validationPassed = $true

# Check Continue.dev extension
$extensions = & code --list-extensions 2>&1
if ($extensions -match "Continue\.continue") {
    Write-Host "   ✅ Continue.dev extension: Installed" -ForegroundColor Green
} else {
    Write-Host "   ❌ Continue.dev extension: Not found" -ForegroundColor Red
    $validationPassed = $false
}

# Check Ollama service
try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -TimeoutSec 5 -ErrorAction Stop
    Write-Host "   ✅ Ollama service: Running" -ForegroundColor Green

    $modelCount = $response.models.Count
    Write-Host "   ✅ Ollama models: $modelCount installed" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Ollama service: Not responding" -ForegroundColor Red
    $validationPassed = $false
}

# Check Continue.dev config
if (Test-Path $ContinueConfigPath) {
    Write-Host "   ✅ Continue.dev config: Created at $ContinueConfigPath" -ForegroundColor Green
} else {
    Write-Host "   ❌ Continue.dev config: Not found" -ForegroundColor Red
    $validationPassed = $false
}

# Check FlaUI packages
$csprojContent = Get-Content $TestProjectFile -Raw
if ($csprojContent -match "FlaUI\.Core" -and $csprojContent -match "FlaUI\.UIA3") {
    Write-Host "   ✅ FlaUI packages: Added to project" -ForegroundColor Green
} else {
    Write-Host "   ❌ FlaUI packages: Not found in project" -ForegroundColor Red
    $validationPassed = $false
}

# Check test infrastructure
if ((Test-Path $wpfTestBasePath) -and (Test-Path $syncfusionHelpersPath)) {
    Write-Host "   ✅ Test infrastructure: Created in $e2eDir" -ForegroundColor Green
} else {
    Write-Host "   ❌ Test infrastructure: Files missing" -ForegroundColor Red
    $validationPassed = $false
}

Write-Host ""

#region Summary
if ($validationPassed) {
    Write-Host "🎉 Setup Complete! All components installed successfully." -ForegroundColor Green
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Restart VS Code to activate Continue.dev extension" -ForegroundColor White
    Write-Host "2. Press Ctrl+L in VS Code to open Continue.dev chat" -ForegroundColor White
    Write-Host "3. Try generating a test with: 'Generate xUnit test for Municipal Account View'" -ForegroundColor White
    Write-Host "4. Read documentation: docs/AI_E2E_TESTING_SETUP.md" -ForegroundColor White
    Write-Host ""
    Write-Host "Test Continue.dev now:" -ForegroundColor Cyan
    Write-Host "   Open any C# file, press Ctrl+I, and type:" -ForegroundColor Gray
    Write-Host "   'Generate a simple xUnit test that validates a list count'" -ForegroundColor Yellow
} else {
    Write-Host "⚠️  Setup completed with warnings. Review messages above." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Cyan
    Write-Host "- Check Ollama service: ollama list" -ForegroundColor White
    Write-Host "- Verify Continue.dev: code --list-extensions | Select-String Continue" -ForegroundColor White
    Write-Host "- Read docs: docs/AI_E2E_TESTING_SETUP.md" -ForegroundColor White
}

Write-Host ""
Write-Host "📚 Documentation: docs/AI_E2E_TESTING_SETUP.md" -ForegroundColor Cyan
Write-Host "🔧 Run tests: dotnet test --filter 'FullyQualifiedName~E2E'" -ForegroundColor Cyan
Write-Host ""
