# 🚀 Quick Start: Syncfusion Toolbox & MCP Servers

**Copy this to your desktop for quick reference!**

---

## ⚡ 30-Second Setup

```powershell
# 1. Set API key (get from https://syncfusion.com/account/api-key)
[System.Environment]::SetEnvironmentVariable("SYNCFUSION_API_KEY", "YOUR_KEY", "User")

# 2. Clear VS cache
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Microsoft\VisualStudio\17.0_*\ComponentModelCache"

# 3. Restart Visual Studio

# 4. Reset Toolbox: View → Toolbox → Right-click → Reset Toolbox
```

---

## 🎯 Quick Tests

### Test 1: MCP Server Working?

```
Open Copilot Chat → Ask → Agent → SyncfusionWinFormsAssistant
Ask: "What packages do I need for SfDataGrid in .NET 10?"
✅ Should get official Syncfusion docs
```

### Test 2: Toolbox Working?

```
View → Toolbox → Search: "SfDataGrid"
✅ Should see Syncfusion controls
```

### Test 3: Designer Working?

```
Right-click WarRoomPanel.cs → View Designer
✅ Should load without errors
```

---

## 🩺 Quick Fixes

### Controls Missing from Toolbox?

```powershell
# Manual add: Toolbox → Choose Items → .NET Components → Browse to:
C:\Users\[You]\.nuget\packages\syncfusion.sfdatagrid.winforms\32.1.19\lib\net10.0-windows7.0\Syncfusion.SfDataGrid.WinForms.dll
```

### MCP Server Not Found?

```powershell
# Regenerate config
.\scripts\generate-vs-mcp-config.ps1
```

### Build Errors?

```powershell
dotnet clean
dotnet restore --force
dotnet build
```

---

## 📖 Full Docs

- **Toolbox Guide:** `docs/SYNCFUSION_TOOLBOX_VS2026_GUIDE.md`
- **MCP Setup:** `docs/MCP_SERVER_SETUP_GUIDE.md`
- **Setup Summary:** `SYNCFUSION_SETUP_COMPLETE.md`

---

## 🆘 Emergency Help

**Copilot not using MCP tools?**
Check: `logs/mcp-debugger.log`

**Designer crashes?**
Check: View → Output → "Design" pane

**Still stuck?**
Ask: `@SyncfusionWinFormsAssistant [your issue]`

---

**🎉 Once working, you can drag/drop Syncfusion controls in Designer!**
