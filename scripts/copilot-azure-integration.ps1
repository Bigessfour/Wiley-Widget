# Copilot Chat Integration for Azure Operations
# Safe PowerShell functions to work with GitHub Copilot Chat

function Invoke-CopilotAzureHelp {
    <#
    .SYNOPSIS
        Get safe Azure help from GitHub Copilot Chat

    .DESCRIPTION
        This function provides safe, novice-friendly Azure assistance by generating
        Copilot Chat prompts and commands that prioritize safety and education.

    .PARAMETER Topic
        The Azure topic you need help with

    .PARAMETER Operation
        Specific Azure operation (status, backup, connect, etc.)

    .PARAMETER SafeMode
        Always true for novices - enables safety features

    .EXAMPLE
        Invoke-CopilotAzureHelp -Topic "database connection"

    .EXAMPLE
        Invoke-CopilotAzureHelp -Operation "backup"
    #>

    param(
        [Parameter(Mandatory = $false)]
        [string]$Topic,

        [Parameter(Mandatory = $false)]
        [ValidateSet("status", "backup", "connect", "list", "create", "delete")]
        [string]$Operation,

        [Parameter(Mandatory = $false)]
        [bool]$SafeMode = $true
    )

    Write-Host "🤖 GitHub Copilot Azure Assistant" -ForegroundColor Cyan
    Write-Host "=================================" -ForegroundColor Cyan

    if ($SafeMode) {
        Write-Host "🛡️  SAFE MODE ENABLED - Prioritizing safety and education" -ForegroundColor Green
    }

    # Generate safe Copilot prompts
    $prompts = @()

    if ($Topic) {
        $prompts += "Explain '$Topic' in Azure in simple terms for a beginner"
        $prompts += "Show me safe ways to work with '$Topic' in Azure"
        $prompts += "What are common mistakes beginners make with '$Topic' in Azure?"
    }

    if ($Operation) {
        switch ($Operation) {
            "status" {
                $prompts += "How do I safely check my Azure account and resource status?"
                $prompts += "Show me the safe script command to check Azure status"
            }
            "backup" {
                $prompts += "How do I create a safe backup of my Azure SQL database?"
                $prompts += "Explain why backups are important for beginners"
            }
            "connect" {
                $prompts += "How do I safely test my Azure SQL database connection?"
                $prompts += "What should I check if my connection fails?"
            }
            "list" {
                $prompts += "How do I safely list all my Azure resources?"
                $prompts += "Show me what information I can get about my resources"
            }
            "create" {
                $prompts += "How do I safely create Azure resources as a beginner?"
                $prompts += "What resources should I create first?"
            }
            "delete" {
                $prompts += "How do I safely delete Azure resources without breaking things?"
                $prompts += "What should I backup before deleting anything?"
            }
        }
    }

    # Display suggested prompts
    Write-Host "`n💬 Suggested questions to ask GitHub Copilot Chat:" -ForegroundColor Yellow
    Write-Host "================================================" -ForegroundColor Yellow

    for ($i = 0; $i -lt $prompts.Count; $i++) {
        Write-Host "$($i + 1). $($prompts[$i])" -ForegroundColor White
    }

    # Show safe commands
    Write-Host "`n🔧 Safe Azure Commands:" -ForegroundColor Green
    Write-Host "=======================" -ForegroundColor Green

    Write-Host "# Check Azure status (safe, read-only)" -ForegroundColor Gray
    Write-Host ".\scripts\azure-safe-operations.ps1 -Operation status`n" -ForegroundColor White

    Write-Host "# Test database connection (safe, read-only)" -ForegroundColor Gray
    Write-Host ".\scripts\azure-safe-operations.ps1 -Operation connect`n" -ForegroundColor White

    Write-Host "# Create database backup (safe, creates copy)" -ForegroundColor Gray
    Write-Host ".\scripts\azure-safe-operations.ps1 -Operation backup`n" -ForegroundColor White

    Write-Host "# List all resources (safe, read-only)" -ForegroundColor Gray
    Write-Host ".\scripts\azure-safe-operations.ps1 -Operation list`n" -ForegroundColor White

    # Show dry run examples
    Write-Host "🧪 Test ANY operation safely with -DryRun:" -ForegroundColor Cyan
    Write-Host "==========================================" -ForegroundColor Cyan
    Write-Host "# See what would happen (recommended for beginners)" -ForegroundColor Gray
    Write-Host ".\scripts\azure-safe-operations.ps1 -Operation status -DryRun`n" -ForegroundColor White

    # Emergency help
    Write-Host "🚨 If something goes wrong:" -ForegroundColor Red
    Write-Host "===========================" -ForegroundColor Red
    Write-Host "# Check what you have" -ForegroundColor Gray
    Write-Host ".\scripts\azure-safe-operations.ps1 -Operation status`n" -ForegroundColor White

    Write-Host "# Ask Copilot for help" -ForegroundColor Gray
    Write-Host "'Help! I think I broke something with Azure'`n" -ForegroundColor White

    # Learning resources
    Write-Host "📚 Learning Resources:" -ForegroundColor Magenta
    Write-Host "=====================" -ForegroundColor Magenta
    Write-Host "• Microsoft Learn: Azure Fundamentals" -ForegroundColor White
    Write-Host "• Azure Documentation (filter for 'beginner')" -ForegroundColor White
    Write-Host "• docs/azure-novice-guide.md (in your project)" -ForegroundColor White
    Write-Host "• docs/copilot-azure-examples.md (examples)" -ForegroundColor White

    Write-Host "`n💡 Pro Tip: Always ask Copilot to explain Azure concepts in simple terms!" -ForegroundColor Cyan
    Write-Host "💡 Pro Tip: Use -DryRun for any operation you're unsure about!" -ForegroundColor Cyan
}

function Get-AzureLearningPath {
    <#
    .SYNOPSIS
        Show a learning path for Azure beginners

    .DESCRIPTION
        Displays a structured learning path for novice Azure developers
    #>

    Write-Host "🚀 Azure Learning Path for Beginners" -ForegroundColor Cyan
    Write-Host "===================================" -ForegroundColor Cyan

    $weeks = @(
        @{
            Week = 1
            Topic = "Azure Basics & Safety"
            Goals = @(
                "Understand Azure concepts (Resource Groups, Subscriptions)",
                "Learn safe Azure operations",
                "Master the safe scripts",
                "Set up development environment"
            )
            Commands = @(
                ".\scripts\azure-safe-operations.ps1 -Operation status",
                ".\scripts\azure-safe-operations.ps1 -Operation list"
            )
        },
        @{
            Week = 2
            Topic = "Database Operations"
            Goals = @(
                "Connect to Azure SQL safely",
                "Understand connection strings",
                "Learn backup and recovery",
                "Practice with dry runs"
            )
            Commands = @(
                ".\scripts\azure-safe-operations.ps1 -Operation connect",
                ".\scripts\azure-safe-operations.ps1 -Operation backup -DryRun"
            )
        },
        @{
            Week = 3
            Topic = "Resource Management"
            Goals = @(
                "Create and manage resources safely",
                "Understand Azure pricing",
                "Learn resource organization",
                "Practice resource cleanup"
            )
            Commands = @(
                "az group list",
                "az resource list --resource-group WileyWidget-RG"
            )
        },
        @{
            Week = 4
            Topic = "Advanced Operations"
            Goals = @(
                "Deploy applications to Azure",
                "Use Azure Functions",
                "Monitor and troubleshoot",
                "Plan for production"
            )
            Commands = @(
                "az webapp list",
                "az functionapp list"
            )
        }
    )

    foreach ($week in $weeks) {
        Write-Host "`n📅 Week $($week.Week): $($week.Topic)" -ForegroundColor Yellow
        Write-Host "Goals:" -ForegroundColor Green
        foreach ($goal in $week.Goals) {
            Write-Host "  • $goal" -ForegroundColor White
        }
        Write-Host "Practice Commands:" -ForegroundColor Cyan
        foreach ($command in $week.Commands) {
            Write-Host "  • $command" -ForegroundColor Gray
        }
    }

    Write-Host "`n🎯 Remember:" -ForegroundColor Magenta
    Write-Host "• Take your time - Azure isn't a race" -ForegroundColor White
    Write-Host "• Always use safe scripts for important operations" -ForegroundColor White
    Write-Host "• Ask Copilot to explain anything you don't understand" -ForegroundColor White
    Write-Host "• Celebrate small victories!" -ForegroundColor White
}

# Export functions for use (commented out since this isn't a module)
# Export-ModuleMember -Function Invoke-CopilotAzureHelp, Get-AzureLearningPath

Write-Host "`n🤖 Copilot Azure Integration Loaded!" -ForegroundColor Green
Write-Host "Available functions:" -ForegroundColor White
Write-Host "• Invoke-CopilotAzureHelp - Get safe Azure assistance prompts" -ForegroundColor White
Write-Host "• Get-AzureLearningPath - Show structured learning path" -ForegroundColor White
Write-Host "`nUsage examples:" -ForegroundColor Cyan
Write-Host "• Invoke-CopilotAzureHelp -Topic 'database'" -ForegroundColor Gray
Write-Host "• Invoke-CopilotAzureHelp -Operation 'backup'" -ForegroundColor Gray
Write-Host "• Get-AzureLearningPath" -ForegroundColor Gray
