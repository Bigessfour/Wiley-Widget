# WileyWidget Azure Safety Quick Reference Card
# Standard Operating Procedures - Keep This Handy!

## 🚨 EMERGENCY NUMBERS
- **Status Check**: `.\scripts\azure-safe-operations.ps1 -Operation status`
- **Emergency Backup**: `.\scripts\azure-safe-operations.ps1 -Operation backup`
- **Connection Test**: `.\scripts\azure-safe-operations.ps1 -Operation connect`

## ✅ APPROVED OPERATIONS (Use These Only)

### Daily Operations
```powershell
# Start your day
.\scripts\azure-safe-operations.ps1 -Operation status

# Before development work
.\scripts\azure-safe-operations.ps1 -Operation connect

# End your day
.\scripts\azure-safe-operations.ps1 -Operation status
```

### Safe Database Operations
```powershell
# Test connection (safe, read-only)
.\scripts\azure-safe-operations.ps1 -Operation connect

# Create backup (safe copy)
.\scripts\azure-safe-operations.ps1 -Operation backup

# List resources (safe, read-only)
.\scripts\azure-safe-operations.ps1 -Operation list
```

### Testing Operations (MANDATORY!)
```powershell
# Test ANY operation before running it
.\scripts\azure-safe-operations.ps1 -Operation [operation] -DryRun
```

## ❌ FORBIDDEN OPERATIONS (Never Use These)

### Dangerous Direct Commands
```bash
❌ az sql db delete
❌ az group delete
❌ az resource delete
❌ az sql db update
❌ Any command with --force or --yes
```

### Unsafe Practices
- ❌ Running commands without dry-run testing
- ❌ Making changes without backups
- ❌ Using direct Azure CLI for important operations
- ❌ Bypassing safety protocols

## 🤖 SAFE COPILOT CHAT QUESTIONS

### ✅ Good Questions
```
"How do I safely check my Azure database connection?"
"Show me how to create a backup using the safe script"
"Explain Azure Resource Groups in simple terms"
"What would happen if I run this command? Explain first"
"I'm new to Azure - help me understand [concept]"
```

### ❌ Bad Questions
```
"Delete my Azure database"
"Run this az sql db delete command"
"Execute this Azure CLI command for me"
```

## 📋 PRE-OPERATION CHECKLIST

**COMPLETE ALL ITEMS BEFORE any Azure operation:**

- [ ] Read the Standard Operating Procedures
- [ ] Check current Azure status
- [ ] Create backup (if operation is destructive)
- [ ] Test with `-DryRun` first
- [ ] Understand what the operation does
- [ ] Have a recovery plan ready
- [ ] Get approval if unsure

## 🚨 IF SOMETHING GOES WRONG

### Don't Panic - Follow These Steps:
1. **Check Status**: `.\scripts\azure-safe-operations.ps1 -Operation status`
2. **Verify Backups**: Ensure you have recent backups
3. **Ask for Help**: Use Copilot or team support
4. **Document Incident**: Note what happened for learning
5. **Recovery Plan**: Follow documented recovery procedures

### Emergency Contacts:
- **Copilot Chat**: "Help! I think I broke something with Azure"
- **Team Support**: Ask in project communication channels
- **Azure Support**: https://azure.microsoft.com/support

## 🎯 DAILY AZURE ROUTINE

### Morning (Start of Day)
```powershell
# Check everything is working
.\scripts\azure-safe-operations.ps1 -Operation status
.\scripts\azure-safe-operations.ps1 -Operation connect
```

### During Development
```powershell
# Before making changes
.\scripts\azure-safe-operations.ps1 -Operation backup

# Test any operation
.\scripts\azure-safe-operations.ps1 -Operation [operation] -DryRun

# Execute only after testing
.\scripts\azure-safe-operations.ps1 -Operation [operation]
```

### Evening (End of Day)
```powershell
# Verify everything is still working
.\scripts\azure-safe-operations.ps1 -Operation status
```

## 💡 PRO TIPS

1. **Always dry-run first** - See what would happen
2. **Backup before changes** - Better safe than sorry
3. **Ask Copilot to explain** - Understand before acting
4. **Use safe scripts only** - They're designed for safety
5. **Take your time** - Azure isn't a race
6. **Celebrate learning** - Every safe operation is progress

## 📚 QUICK LEARNING RESOURCES

- **Azure Safety Guide**: `docs/azure-novice-guide.md`
- **Standard Procedures**: `docs/sop-azure-operations.md`
- **Copilot Examples**: `docs/copilot-azure-examples.md`
- **Microsoft Learn**: Azure Fundamentals free course

---

## REMEMBER
**Safety is not optional. These procedures protect your work and learning journey.**

**Keep this card handy and follow it every time!** 🛡️✨
