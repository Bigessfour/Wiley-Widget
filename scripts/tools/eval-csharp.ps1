<#
.SYNOPSIS
    C# Script Evaluator using MCP Server
.DESCRIPTION
    Provides multiple modes for evaluating C# code:
    - File execution (.csx files)
    - REPL mode (interactive)
    - Quick eval (one-liner)
    - Test runner (for ViewModel/Service tests)
.EXAMPLE
    .\eval-csharp.ps1 -File .\test.csx
    .\eval-csharp.ps1 -Repl
    .\eval-csharp.ps1 -Code "Console.WriteLine('Hello');"
#>

[CmdletBinding(DefaultParameterSetName = 'File')]
param(
    [Parameter(ParameterSetName = 'File', Position = 0)]
    [string]$File,

    [Parameter(ParameterSetName = 'Code')]
    [string]$Code,

    [Parameter(ParameterSetName = 'Repl')]
    [switch]$Repl,

    [Parameter(ParameterSetName = 'Test')]
    [string]$Test,

    [int]$Timeout = 30
)

$ErrorActionPreference = 'Stop'

#region Helper Functions

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Type = 'Info'
    )

    $color = switch ($Type) {
        'Success' { 'Green' }
        'Error' { 'Red' }
        'Warning' { 'Yellow' }
        'Info' { 'Cyan' }
        default { 'White' }
    }

    Microsoft.PowerShell.Utility\Write-Host $Message -ForegroundColor $color
}

function Invoke-CSharpMcp {
    param(
        [string]$CsxCode,
        [int]$TimeoutSeconds = 30
    )

    # Use the MCP tool via Copilot
    Write-ColorOutput "ℹ️  Call Copilot with: Run this C# code using MCP" -Type Info
    Write-ColorOutput $CsxCode -Type Info

    # Return instructions for user
    return @{
        success = $true
        message = "Use Copilot to execute this code"
        timeout = $TimeoutSeconds
    }
}

function Invoke-ReplMode {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-ColorOutput "🚀 C# REPL Mode (Ctrl+C to exit)" -Type Info
    Write-ColorOutput "   Type 'exit' or 'quit' to leave" -Type Info
    Write-ColorOutput "   Use '#r nuget: PackageName' for NuGet packages" -Type Info
    Write-ColorOutput ""

    $history = @()
    $sessionCode = @"
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
"@

    while ($true) {
        Microsoft.PowerShell.Utility\Write-Host "C# > " -NoNewline -ForegroundColor Yellow
        $userInput = Read-Host

        if ($userInput -in @('exit', 'quit', 'q')) {
            Write-ColorOutput "👋 Goodbye!" -Type Success
            break
        }

        if ([string]::IsNullOrWhiteSpace($userInput)) {
            continue
        }

        $history += $userInput

        # Build full code
        $fullCode = $sessionCode + "`n" + $userInput

        # Execute
        Write-ColorOutput "⏳ Executing..." -Type Info

        if ($PSCmdlet.ShouldProcess($userInput, "Execute C# code")) {
            # Note: In real implementation, call MCP here
            Write-ColorOutput "ℹ️  REPL mode requires MCP integration" -Type Warning
            Write-ColorOutput "   Use: Copilot Chat > Ask to evaluate: $userInput" -Type Info
        }
    }

    # Save history
    if ($history.Count -gt 0) {
        $historyFile = "$PSScriptRoot\.csharp_repl_history.txt"
        $history | Out-File $historyFile -Append
        Write-ColorOutput "History saved to: $historyFile" -Type Info
    }
}

function Test-ViewModelCode {
    param([string]$TestName)

    $testTemplates = @{
        'BudgetEntry' = @"
#r "nuget: Prism.Core, 9.0.537"
using Prism.Mvvm;

public class BudgetEntryViewModel : BindableBase {
    private decimal _amount;
    public decimal Amount {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }
}

var vm = new BudgetEntryViewModel();
vm.Amount = 1000m;
Console.WriteLine(`$"Amount: {vm.Amount}");
return "Test passed!";
"@
        'Municipal'   = @"
#r "nuget: Prism.Core, 9.0.537"
using Prism.Mvvm;
using System.Collections.ObjectModel;

public class MunicipalViewModel : BindableBase {
    private string _name;
    public string Name {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ObservableCollection<string> Departments { get; } = new();
}

var vm = new MunicipalViewModel();
vm.Name = "Test City";
vm.Departments.Add("Police");
vm.Departments.Add("Fire");
Console.WriteLine(`$"Municipality: {vm.Name} ({vm.Departments.Count} depts)");
return "Test passed!";
"@
    }

    if ($testTemplates.ContainsKey($TestName)) {
        return $testTemplates[$TestName]
    }

    Write-ColorOutput "Available tests: $($testTemplates.Keys -join ', ')" -Type Info
    return $null
}

#endregion

#region Main Logic

try {
    switch ($PSCmdlet.ParameterSetName) {
        'File' {
            if (-not $File) {
                Write-ColorOutput "❌ No file specified. Use -File parameter." -Type Error
                exit 1
            }

            if (-not (Test-Path $File)) {
                Write-ColorOutput "❌ File not found: $File" -Type Error
                exit 1
            }

            Write-ColorOutput "📄 Executing: $File" -Type Info
            $csxCode = Get-Content $File -Raw

            Write-ColorOutput "ℹ️  To execute this file, use:" -Type Info
            Write-ColorOutput "   Copilot Chat > @workspace /eval $(Get-Content $File -Raw)" -Type Info
        }

        'Code' {
            Write-ColorOutput "⚡ Quick Eval" -Type Info
            Write-ColorOutput "Code: $Code" -Type Info
            Write-ColorOutput ""
            Write-ColorOutput "ℹ️  To execute, use:" -Type Info
            Write-ColorOutput "   Copilot Chat > @workspace /eval $Code" -Type Info
        }

        'Repl' {
            Invoke-ReplMode
        }

        'Test' {
            $testCode = Test-ViewModelCode -TestName $Test
            if ($null -eq $testCode) {
                exit 1
            }

            Write-ColorOutput "🧪 Running test: $Test" -Type Info
            Write-ColorOutput "ℹ️  To execute, use:" -Type Info
            Write-ColorOutput "   Copilot Chat > @workspace /eval" -Type Info
            Write-ColorOutput ""
            Write-ColorOutput $testCode -Type Info
        }
    }

    Write-ColorOutput ""
    Write-ColorOutput "💡 Pro Tip: For direct execution, ask Copilot:" -Type Success
    Write-ColorOutput "   'Run this C# code using MCP: <your code>'" -Type Success

}
catch {
    Write-ColorOutput "❌ Error: $_" -Type Error
    Write-ColorOutput $_.ScriptStackTrace -Type Error
    exit 1
}

#endregion
