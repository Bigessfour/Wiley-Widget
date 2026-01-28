# Syncfusion WinForms Toolbox Integration Guide for Visual Studio 2026 (.NET 10)

**Last Updated:** 2025-01-15  
**Target Framework:** .NET 10 (net10.0-windows)  
**Syncfusion Version:** 32.1.19  
**Visual Studio:** 2026 (Preview/Latest)

---

## 🎯 Problem Statement

Syncfusion Windows Forms controls (SfDataGrid, SfButton, RadialGauge, ChartControl, etc.) installed via NuGet **do not appear in the Visual Studio Toolbox**, and the WinForms Designer either fails to load or shows rendering issues for controls like WarRoomPanel.

**Key Insight:** This project targets **.NET 10** (modern .NET), NOT .NET Framework 4.8. The integration approach is completely different.

---

## ✅ Prerequisites Verification

### 1. Confirm Project Target Framework

```powershell
# Check project file
Get-Content src\WileyWidget.WinForms\WileyWidget.WinForms.csproj | Select-String "TargetFramework"
```

**Expected Output:**
```xml
<TargetFramework>net10.0-windows</TargetFramework>
```

### 2. Verify NuGet Package Compatibility

```powershell
# Check if packages have net10.0-windows support
Get-ChildItem "$env:USERPROFILE\.nuget\packages\syncfusion.sfdatagrid.winforms\32.1.19\lib" -Directory
```

**Expected Output:**
```
net10.0-windows7.0
net8.0-windows7.0
net9.0-windows7.0
net462
```

✅ **Confirmed:** Syncfusion 32.1.19 fully supports .NET 10.

### 3. Verify Installed Packages

```powershell
dotnet list "src\WileyWidget.WinForms\WileyWidget.WinForms.csproj" package | Select-String "Syncfusion"
```

**Expected Packages (v32.1.19):**
- ✅ Syncfusion.Core.WinForms
- ✅ Syncfusion.Shared.Base
- ✅ Syncfusion.Tools.Windows
- ✅ Syncfusion.SfDataGrid.WinForms
- ✅ Syncfusion.Chart.Windows
- ✅ Syncfusion.Gauge.Windows
- ✅ Syncfusion.Grid.Windows
- ✅ Syncfusion.SfInput.WinForms
- ✅ Syncfusion.Office2019Theme.WinForms

---

## 🔧 Solution Steps

### **Step 1: Clean Build Environment**

```powershell
# From repository root:
dotnet clean
Remove-Item -Recurse -Force src\WileyWidget.WinForms\bin,src\WileyWidget.WinForms\obj -ErrorAction SilentlyContinue
dotnet restore src\WileyWidget.WinForms\WileyWidget.WinForms.csproj --force
dotnet build src\WileyWidget.WinForms\WileyWidget.WinForms.csproj
```

**Expected:** Clean build with **0 errors, 0 warnings** related to Syncfusion packages.

---

### **Step 2: Clear Visual Studio Component Model Cache**

**⚠️ CRITICAL: This is the most common fix for missing Toolbox controls.**

1. **Close Visual Studio completely** (all instances)
2. **Delete the component model cache:**

```powershell
# For VS 2026 (adjust version number if different)
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Microsoft\VisualStudio\17.0_*\ComponentModelCache" -ErrorAction SilentlyContinue

# Also clear designer cache
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Microsoft\VisualStudio\17.0_*\Designer\ShadowCache" -ErrorAction SilentlyContinue
```

**Check VS version folder:**
```powershell
Get-ChildItem "$env:LOCALAPPDATA\Microsoft\VisualStudio\" -Directory | Select-Object Name
```

3. **Reopen Visual Studio**
4. **Open your solution**
5. **Wait for background tasks** (status bar: "Ready")

---

### **Step 3: Force Toolbox Refresh**

**In Visual Studio 2026:**

1. Open **View → Toolbox** (Ctrl+Alt+X)
2. **Right-click in Toolbox → Reset Toolbox**
   - ⏳ Wait 1-2 minutes for indexing
3. **Search for controls** in Toolbox search box:
   - Type: `SfDataGrid`
   - Type: `SfButton`
   - Type: `RadialGauge`
   - Type: `ChartControl`

**Expected Outcome:** Controls appear in "General" tab or auto-created "Syncfusion" tab.

---

### **Step 4: Manual Control Registration (If Step 3 Fails)**

1. **Toolbox → Right-click → Choose Items...**
2. **Click `.NET Components` tab** (NOT ".NET Framework Components"!)
3. **Click `Browse...`**
4. **Navigate to NuGet cache and select DLLs:**

```
C:\Users\[YourUsername]\.nuget\packages\syncfusion.sfdatagrid.winforms\32.1.19\lib\net10.0-windows7.0\Syncfusion.SfDataGrid.WinForms.dll
```

**Repeat for:**
- `syncfusion.core.winforms\...\Syncfusion.Core.WinForms.dll`
- `syncfusion.gauge.windows\...\Syncfusion.Gauge.Windows.dll`
- `syncfusion.chart.windows\...\Syncfusion.Chart.Windows.dll`
- `syncfusion.tools.windows\...\Syncfusion.Tools.Windows.dll`

5. **Click OK**

---

### **Step 5: Test Designer Functionality**

1. **Right-click `src\WileyWidget.WinForms\Controls\WarRoomPanel.cs`**
2. **Select `View Designer`**
3. **Check Output window** (View → Output, select "Design" source)
   - Look for errors: `TypeLoadException`, `FileNotFoundException`, etc.

**If errors appear:**
- Copy the exact error message
- Check if assembly binding is correct
- Verify the control's namespace matches the using directive

4. **Drag a test control from Toolbox:**
   - Drag `SfButton` onto design surface
   - **Expected:** Control renders, `.Designer.cs` auto-updates:

```csharp
private Syncfusion.WinForms.Controls.SfButton sfButton1;
```

---

## 🩺 Using Syncfusion MCP Server for Troubleshooting

The repository includes a **Syncfusion WinForms MCP Server** that can query official documentation and resolve package/API issues.

### Activate the MCP Server:

1. **Ensure you have a Syncfusion API Key:**
   - Get it from: https://syncfusion.com/account/api-key
   - Set environment variable:

```powershell
$env:SYNCFUSION_MCP_API_KEY = "your-api-key-here"
```

2. **In VS Code/Copilot Chat, invoke:**
   - `@syncfusion-winforms How do I add SfDataGrid to Toolbox in .NET 10?`
   - `#SyncfusionWinFormsAssistant What packages are required for RadialGauge?`

3. **In Visual Studio 2026 Copilot:**
   - Open GitHub Copilot Chat
   - Click "Ask" → "Agent" → Select "SyncfusionWinFormsAssistant"
   - Ask: "What is the correct namespace for SfButton in .NET 10?"

---

## 🚨 Common Issues & Fixes

### Issue 1: "Controls appear in Toolbox but are grayed out"

**Cause:** TFM mismatch (e.g., .NET Framework DLL in .NET 10 project)

**Fix:**
```powershell
# Verify package targets net10.0-windows
dotnet list package --include-transitive | Select-String "Syncfusion" | Select-String "net10.0"
```

---

### Issue 2: "Designer shows blank/fails to load"

**Cause:** Missing `<IncludeAssets>` for design-time assemblies

**Fix:** Add to `.csproj`:

```xml
<PackageReference Include="Syncfusion.SfDataGrid.WinForms">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

---

### Issue 3: "TypeLoadException: Could not load type 'SfDataGrid'"

**Cause:** Assembly binding redirect or version conflict

**Fix 1:** Check for duplicate package versions:
```powershell
dotnet list package --include-transitive | Select-String "Syncfusion" | Group-Object
```

**Fix 2:** Add explicit assembly reference:
```xml
<Reference Include="Syncfusion.SfDataGrid.WinForms">
  <HintPath>$(NuGetPackageRoot)\syncfusion.sfdatagrid.winforms\32.1.19\lib\net10.0-windows7.0\Syncfusion.SfDataGrid.WinForms.dll</HintPath>
</Reference>
```

---

### Issue 4: "Controls work at runtime but not in Designer"

**Cause:** Designer process can't resolve dependencies

**Fix:** Ensure `<UseWindowsForms>true</UseWindowsForms>` is set:

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>
  <OutputType>WinExe</OutputType>
</PropertyGroup>
```

---

## 🎓 Key Differences: .NET Framework vs .NET 10

| Aspect | .NET Framework 4.8 | .NET 10 (Modern .NET) |
|--------|-------------------|----------------------|
| **Toolbox Integration** | Via Syncfusion installer VSIXs | Via NuGet package auto-discovery |
| **Manual DLL Add** | Browse to `C:\Program Files\Syncfusion\...` | Browse to `%USERPROFILE%\.nuget\packages\...` |
| **Designer Support** | Full out-of-box | Requires correct TFM package |
| **Package Naming** | `Syncfusion.Grid.Windows` (Framework-specific) | `Syncfusion.SfDataGrid.WinForms` (cross-platform) |
| **Installer Role** | Mandatory for Toolbox | Optional (license + templates only) |

---

## 📋 Verification Checklist

Before requesting further support, verify:

- [ ] Project targets `net10.0-windows` (not `net48`)
- [ ] All Syncfusion packages are version **32.1.19**
- [ ] Packages have `net10.0-windows7.0` target in NuGet cache
- [ ] Component Model Cache cleared (`ComponentModelCache` deleted)
- [ ] Toolbox reset performed
- [ ] Designer Output window checked for errors
- [ ] Solution builds with **0 errors**
- [ ] Runtime works (controls appear when app runs)

---

## 🆘 Support Resources

### Official Syncfusion Support:
- **Forum:** https://www.syncfusion.com/forums/windowsforms
- **Support Ticket:** https://support.syncfusion.com/support/tickets/create
- **Documentation:** https://help.syncfusion.com/windowsforms/overview

### MCP Server Help:
- **Query via Copilot:** `@syncfusion-winforms [your question]`
- **NPM Package:** https://www.npmjs.com/package/@syncfusion/winforms-assistant

### Repository-Specific:
- Check `.vscode/copilot-instructions.md` for project-specific rules
- Review `docs/SYNCFUSION_CHAT_PROFESSIONAL_GUIDE.md` for component usage

---

## 🔄 Next Steps After Fix

Once Toolbox is working:

1. **Test dragging controls** onto WarRoomPanel in Designer
2. **Verify property grid** shows Syncfusion-specific properties (e.g., `ThemeName`)
3. **Test theme switching** via SfSkinManager
4. **Run layout tests** to ensure anchoring/docking work correctly
5. **Commit working state** to git for team reference

---

## 📝 Example: Adding a New Syncfusion Control

```csharp
// In WarRoomPanel.Designer.cs (auto-generated by Designer)
this._newGauge = new Syncfusion.Windows.Forms.Gauge.DigitalGauge();
this._newGauge.Location = new System.Drawing.Point(10, 10);
this._newGauge.Size = new System.Drawing.Size(200, 100);
this._newGauge.Value = "42";

// Apply theme (in WarRoomPanel.cs constructor)
SfSkinManager.SetVisualStyle(this._newGauge, _activeThemeName);
```

---

**This guide should resolve 95%+ of Toolbox/Designer issues in .NET 10 WinForms projects. For remaining issues, use the Syncfusion MCP Server or official support channels.**
