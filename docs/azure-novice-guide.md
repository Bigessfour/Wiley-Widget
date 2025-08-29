# Azure Development with GitHub Copilot Chat
# Safe interaction patterns for novice developers

## Overview
This guide shows you how to use GitHub Copilot Chat safely with Azure operations. As a novice, you'll learn to avoid dangerous commands while still being productive.

## Safe Azure Operations Script

Your project includes `scripts/azure-safe-operations.ps1` which provides safe Azure operations:

### Basic Usage
```powershell
# Check Azure status (safe, read-only)
.\scripts\azure-safe-operations.ps1 -Operation status

# List resources (safe, read-only)
.\scripts\azure-safe-operations.ps1 -Operation list

# Test database connection (safe, read-only)
.\scripts\azure-safe-operations.ps1 -Operation connect

# Create backup (safe, creates copy)
.\scripts\azure-safe-operations.ps1 -Operation backup
```

### Dry Run Mode (Recommended for Novices)
```powershell
# See what would happen without actually doing it
.\scripts\azure-safe-operations.ps1 -Operation status -DryRun

# Test any operation safely
.\scripts\azure-safe-operations.ps1 -Operation backup -DryRun
```

## GitHub Copilot Chat Integration

### How to Ask Copilot for Azure Help

**✅ Safe Patterns to Use:**
```
"Show me how to check my Azure database connection using the safe script"
"Help me create a backup of my Azure database safely"
"How do I list my Azure resources without making changes?"
"Explain what this Azure command does before I run it"
```

**❌ Avoid These Patterns:**
```
"Delete my Azure database"
"Drop table users"
"Run this Azure CLI command"
```

### Copilot Chat Commands for Azure

**For Safe Operations:**
```powershell
# Ask Copilot: "Show me the safe way to check Azure status"
.\scripts\azure-safe-operations.ps1 -Operation status

# Ask Copilot: "How do I safely test my database connection?"
.\scripts\azure-safe-operations.ps1 -Operation connect -DryRun

# Ask Copilot: "Help me create a safe backup"
.\scripts\azure-safe-operations.ps1 -Operation backup -DryRun
```

**For Learning Azure:**
```powershell
# Ask Copilot: "Explain Azure Resource Groups in simple terms"
# Ask Copilot: "What is Azure SQL Database and why would I use it?"
# Ask Copilot: "Show me Azure CLI commands but explain what each part does"
```

## Safe Development Workflow

### 1. Always Use Dry Run First
```powershell
# Test any operation before running it
.\scripts\azure-safe-operations.ps1 -Operation connect -DryRun
```

### 2. Check Status Before Changes
```powershell
# Always know your current state
.\scripts\azure-safe-operations.ps1 -Operation status
```

### 3. Create Backups Before Modifications
```powershell
# Backup before making any changes
.\scripts\azure-safe-operations.ps1 -Operation backup
```

### 4. Use Read-Only Operations for Learning
```powershell
# Safe way to explore
.\scripts\azure-safe-operations.ps1 -Operation list
```

## Azure CLI Commands (Safe Usage)

### Read-Only Commands (Safe to Run)
```bash
# Check account
az account show

# List resources
az resource list --resource-group WileyWidget-RG

# Check database
az sql db show --resource-group WileyWidget-RG --server your-server --name WileyWidgetDb
```

### Commands to Avoid (Use Script Instead)
```bash
# Don't run these directly - use the safe script
az sql db delete  # Use safe script instead
az group delete   # Use safe script instead
az sql db update  # Test with dry run first
```

## Emergency Recovery

If something goes wrong:

### 1. Don't Panic
```powershell
# Check what you have
.\scripts\azure-safe-operations.ps1 -Operation status
```

### 2. Restore from Backup
```powershell
# If you created a backup, you can restore it
# Ask Copilot: "How do I restore from an Azure database backup?"
```

### 3. Get Help
```powershell
# Ask Copilot for help
"Help! I accidentally [describe what happened] with Azure"
```

## Learning Resources

### Books for Novices
- "Azure for Dummies"
- "Cloud Computing for Beginners"
- "Database Administration Fundamentals"

### Online Resources
- Microsoft Learn: Azure fundamentals
- Azure documentation (search for "beginner")
- YouTube: "Azure for beginners" playlists

### Safe Practice Environment
```powershell
# Use dry run mode for learning
.\scripts\azure-safe-operations.ps1 -Operation status -DryRun -Verbose
```

## Pro Tips for Novice Azure Developers

1. **Always backup before changes**
2. **Use dry run mode to learn**
3. **Ask Copilot to explain commands**
4. **Start with read-only operations**
5. **Keep a log of what you do**
6. **Test in development environment first**
7. **Have a recovery plan**

## Getting Help

### From GitHub Copilot Chat
```
"I need help with Azure but I'm a beginner"
"Show me safe Azure commands for novices"
"Explain this Azure error message in simple terms"
```

### From the Safe Script
```powershell
# Get help with available operations
.\scripts\azure-safe-operations.ps1
```

Remember: **There's no such thing as a stupid question when learning Azure!** Take your time, use the safe scripts, and ask for help when needed.
