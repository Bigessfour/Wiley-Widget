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

    Write-Information "🤖 GitHub Copilot Azure Assistant" -InformationAction Continue
    Write-Information "=================================" -InformationAction Continue

    if ($SafeMode) {
        Write-Information "🛡️  SAFE MODE ENABLED - Prioritizing safety and education" -InformationAction Continue
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
    Write-Information "`n💬 Suggested questions to ask GitHub Copilot Chat:" -InformationAction Continue
    Write-Information "================================================" -InformationAction Continue

    for ($i = 0; $i -lt $prompts.Count; $i++) {
        Write-Information "$($i + 1). $($prompts[$i])" -InformationAction Continue
    }

    # Show safe commands
    Write-Information "`n🔧 Safe Azure Commands:" -InformationAction Continue
    Write-Information "=======================" -InformationAction Continue

    Write-Information "# Check Azure status (safe, read-only)" -InformationAction Continue
    Write-Information ".\scripts\azure-safe-operations.ps1 -Operation status`n" -InformationAction Continue

    Write-Information "# Test database connection (safe, read-only)" -InformationAction Continue
    Write-Information ".\scripts\azure-safe-operations.ps1 -Operation connect`n" -InformationAction Continue

    Write-Information "# Create database backup (safe, creates copy)" -InformationAction Continue
    Write-Information ".\scripts\azure-safe-operations.ps1 -Operation backup`n" -InformationAction Continue

    Write-Information "# List all resources (safe, read-only)" -InformationAction Continue
    Write-Information ".\scripts\azure-safe-operations.ps1 -Operation list`n" -InformationAction Continue

    # Show dry run examples
    Write-Information "🧪 Test ANY operation safely with -DryRun:" -InformationAction Continue
    Write-Information "==========================================" -InformationAction Continue
    Write-Information "# See what would happen (recommended for beginners)" -InformationAction Continue
    Write-Information ".\scripts\azure-safe-operations.ps1 -Operation status -DryRun`n" -InformationAction Continue

    # Emergency help
    Write-Information "🚨 If something goes wrong:" -InformationAction Continue
    Write-Information "===========================" -InformationAction Continue
    Write-Information "# Check what you have" -InformationAction Continue
    Write-Information ".\scripts\azure-safe-operations.ps1 -Operation status`n" -InformationAction Continue

    Write-Information "# Ask Copilot for help" -InformationAction Continue
    Write-Information "'Help! I think I broke something with Azure'`n" -InformationAction Continue

    # Learning resources
    Write-Information "📚 Learning Resources:" -InformationAction Continue
    Write-Information "=====================" -InformationAction Continue
    Write-Information "• Microsoft Learn: Azure Fundamentals" -InformationAction Continue
    Write-Information "• Azure Documentation (filter for 'beginner')" -InformationAction Continue
    Write-Information "• docs/azure-novice-guide.md (in your project)" -InformationAction Continue
    Write-Information "• docs/copilot-azure-examples.md (examples)" -InformationAction Continue

    Write-Information "`n💡 Pro Tip: Always ask Copilot to explain Azure concepts in simple terms!" -InformationAction Continue
    Write-Information "💡 Pro Tip: Use -DryRun for any operation you're unsure about!" -InformationAction Continue
}

function Get-AzureLearningPath {
    <#
    .SYNOPSIS
        Show a learning path for Azure beginners

    .DESCRIPTION
        Displays a structured learning path for novice Azure developers
    #>

    Write-Information "🚀 Azure Learning Path for Beginners" -InformationAction Continue
    Write-Information "===================================" -InformationAction Continue

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
        Write-Information "`n📅 Week $($week.Week): $($week.Topic)" -InformationAction Continue
        Write-Information "Goals:" -InformationAction Continue
        foreach ($goal in $week.Goals) {
            Write-Information "  • $goal" -InformationAction Continue
        }
        Write-Information "Practice Commands:" -InformationAction Continue
        foreach ($command in $week.Commands) {
            Write-Information "  • $command" -InformationAction Continue
        }
    }

    Write-Information "`n🎯 Remember:" -InformationAction Continue
    Write-Information "• Take your time - Azure isn't a race" -InformationAction Continue
    Write-Information "• Always use safe scripts for important operations" -InformationAction Continue
    Write-Information "• Ask Copilot to explain anything you don't understand" -InformationAction Continue
    Write-Information "• Celebrate small victories!" -InformationAction Continue
}

# Export functions for use (commented out since this isn't a module)
# Export-ModuleMember -Function Invoke-CopilotAzureHelp, Get-AzureLearningPath

Write-Information "`n🤖 Copilot Azure Integration Loaded!" -InformationAction Continue
Write-Information "Available functions:" -InformationAction Continue
Write-Information "• Invoke-CopilotAzureHelp - Get safe Azure assistance prompts" -InformationAction Continue
Write-Information "• Get-AzureLearningPath - Show structured learning path" -InformationAction Continue
Write-Information "`nUsage examples:" -InformationAction Continue
Write-Information "• Invoke-CopilotAzureHelp -Topic 'database'" -InformationAction Continue
Write-Information "• Invoke-CopilotAzureHelp -Operation 'backup'" -InformationAction Continue
Write-Information "• Get-AzureLearningPath" -InformationAction Continue
