# Example: Using GitHub Copilot Chat with Azure Safely
# This shows how to ask Copilot for help with Azure operations

## How to Ask Copilot for Azure Help

### ✅ Good Questions (Safe, Educational)
```
"Show me how to check my Azure database status safely"
"How do I create a backup of my Azure database using the safe script?"
"Explain what an Azure Resource Group is in simple terms"
"What would happen if I run this Azure command? Explain it first"
"Help me understand my Azure SQL connection string"
```

### ❌ Bad Questions (Dangerous, Direct Commands)
```
"Delete my Azure database"
"Run this az sql db delete command"
"Drop all tables in my database"
"Execute this Azure CLI command for me"
```

## Example Conversation with Copilot

### Scenario: You want to check your Azure setup

**You ask:**
```
"I'm new to Azure and want to check my database connection. How do I do this safely?"
```

**Copilot should respond:**
```
Great question! Since you're new to Azure, let's do this safely step by step:

1. **Check your Azure status** (completely safe, read-only):
   ```powershell
   .\scripts\azure-safe-operations.ps1 -Operation status
   ```
   This will show you if you're logged in and what resources exist.

2. **Test your database connection** (safe, read-only):
   ```powershell
   .\scripts\azure-safe-operations.ps1 -Operation connect
   ```
   This tests if your app can connect to Azure SQL without making any changes.

3. **See all your resources** (safe, read-only):
   ```powershell
   .\scripts\azure-safe-operations.ps1 -Operation list
   ```
   This shows you everything you have in Azure.

Try the first command and let me know what it shows! Then we can go from there.
```

### Scenario: You want to make a change

**You ask:**
```
"I want to modify my database. How do I do this safely?"
```

**Copilot should respond:**
```
Smart thinking to ask about safety first! Here's the safe approach:

1. **Create a backup first** (always do this before changes):
   ```powershell
   .\scripts\azure-safe-operations.ps1 -Operation backup
   ```
   This creates a safe copy of your database.

2. **Test your planned changes** (see what would happen):
   ```powershell
   # Ask me to show you the specific command with -DryRun
   # For example: "Show me how to add a table with dry run"
   ```

3. **Make the actual change** (only after testing):
   ```powershell
   # Only run the real command after you understand and have tested it
   ```

What's the specific change you want to make? I can show you the safe way to do it!
```

## Quick Reference for Common Tasks

### Check Azure Status
```powershell
# Ask Copilot: "How do I check my Azure setup safely?"
.\scripts\azure-safe-operations.ps1 -Operation status
```

### Test Database Connection
```powershell
# Ask Copilot: "How do I test my database connection?"
.\scripts\azure-safe-operations.ps1 -Operation connect
```

### Create Database Backup
```powershell
# Ask Copilot: "How do I backup my database safely?"
.\scripts\azure-safe-operations.ps1 -Operation backup
```

### List All Resources
```powershell
# Ask Copilot: "What Azure resources do I have?"
.\scripts\azure-safe-operations.ps1 -Operation list
```

### Test Any Operation
```powershell
# Ask Copilot: "Can I [operation] safely?"
.\scripts\azure-safe-operations.ps1 -Operation [operation] -DryRun
```

## Emergency Help

### If Something Goes Wrong
```
# Ask Copilot: "I think I broke something with Azure. Help!"
# Copilot should suggest:
.\scripts\azure-safe-operations.ps1 -Operation status
# Then guide you through recovery steps
```

### If You're Not Sure
```
# Ask Copilot: "I'm not sure what this Azure command does. Can you explain it?"
# Copilot should explain in simple terms before suggesting anything
```

## Pro Tips for Using Copilot with Azure

1. **Always ask "safely"** - Include the word "safe" in your questions
2. **Request explanations** - Ask "explain this" before running commands
3. **Use dry run** - Ask for `-DryRun` examples first
4. **Check status often** - Know your current state
5. **Backup first** - Always backup before changes
6. **Take it slow** - One step at a time

## Remember
- Copilot is your learning partner, not just a command generator
- It's okay to ask "dumb" questions - that's how you learn!
- Safety first, speed second
- You can always ask for simpler explanations
- Take breaks and celebrate progress

Happy (safe) Azure learning! 🚀
