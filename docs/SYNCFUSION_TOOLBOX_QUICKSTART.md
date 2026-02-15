# Syncfusion Toolbox Quick Start for VS 2026

**URGENT:** If your toolbox is empty, follow these steps IN ORDER.

---

## ⚡ Quick Fix (5 minutes)

### Step 1: Run Diagnostic Script

```powershell
# From repository root in PowerShell 7+:
.\scripts\diagnose-syncfusion-toolbox-vs2026.ps1
```

This will tell you exactly what's wrong.

---

### Step 2: Close Visual Studio

**⚠️ CRITICAL:** Close ALL Visual Studio instances before continuing.

```powershell
# Verify no VS is running:
Get-Process -Name "devenv" -ErrorAction SilentlyContinue
```

If any processes show, close them manually.

---

### Step 3: Clear Component Cache

```powershell
# Clear VS cache:
.\scripts\diagnose-syncfusion-toolbox-vs2026.ps1 -ClearCache
```

**OR manually:**

```powershell
# Find your VS version folder:
Get-ChildItem "$env:LOCALAPPDATA\Microsoft\VisualStudio\" -Directory | Where-Object { $_.Name -like "17.*" }

# Clear caches (replace 17.X with your version):
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Microsoft\VisualStudio\17.X_*\ComponentModelCache"
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Microsoft\VisualStudio\17.X_*\Designer\ShadowCache"
```

---

### Step 4: Restore Packages

```powershell
# Clean and restore:
dotnet clean
dotnet restore src\WileyWidget.WinForms\WileyWidget.WinForms.csproj --force
dotnet build src\WileyWidget.WinForms\WileyWidget.WinForms.csproj
```

**Expected:** Build succeeds with 0 errors.

---

### Step 5: Open Visual Studio & Reset Toolbox

1. **Open Visual Studio 2026**
2. **Open the solution** (`WileyWidget.sln`)
3. **Wait for "Ready"** status (bottom-left, can take 1-2 minutes)
4. **View → Toolbox** (Ctrl+Alt+X)
5. **Right-click in Toolbox → Reset Toolbox**
6. **Wait 30 seconds** for indexing
7. **Search for "SfDataGrid"** in Toolbox search box

**Expected:** SfDataGrid appears in "General" or "Syncfusion" tab.

---

## 🔍 Troubleshooting

### Issue: "No controls appear after Reset Toolbox"

**Solution: Manual Control Registration**

1. **Toolbox → Right-click → Choose Items...**
2. **Click `.NET Components` tab** (NOT ".NET Framework"!)
3. **Click `Browse...`**
4. **Navigate to:**

```
C:\Users\[YourUsername]\.nuget\packages\syncfusion.sfdatagrid.winforms\32.1.19\lib\net10.0-windows7.0\Syncfusion.SfDataGrid.WinForms.dll
```

5. **Also add:**
   - `syncfusion.core.winforms\32.1.19\lib\net10.0-windows7.0\Syncfusion.Core.WinForms.dll`
   - `syncfusion.tools.windows\32.1.19\lib\net10.0-windows7.0\Syncfusion.Tools.Windows.dll`
   - `syncfusion.gauge.windows\32.1.19\lib\net10.0-windows7.0\Syncfusion.Gauge.Windows.dll`

6. **Click OK**

---

### Issue: "Essential Studio Extensions not visible"

**Check:**

1. **Extensions → Manage Extensions**
2. **Search for "Syncfusion"**
3. **Look for "Essential Studio for WinForms"**

**If NOT installed:**

- Download from: https://www.syncfusion.com/downloads/essential-studio/winforms
- Install version **32.1.19** (matches your project)
- Restart VS 2026

---

### Issue: "Designer shows errors when opening forms"

**Check Designer Output:**

1. **View → Output** (Ctrl+Alt+O)
2. **Select "Show output from: Design"**
3. **Copy any errors and search in documentation**

**Common errors:**

- **TypeLoadException:** Missing assembly reference or wrong version
- **FileNotFoundException:** NuGet package not restored
- **InvalidOperationException:** Theme assembly not loaded

---

## 📦 Verify Installation

Run this to confirm everything is working:

```powershell
# Check NuGet packages:
dotnet list src\WileyWidget.WinForms\WileyWidget.WinForms.csproj package | Select-String "Syncfusion"

# Check local Syncfusion installation:
Test-Path "C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\32.1.19"
```

---

## 🆘 Still Not Working?

### Option 1: Use MCP Syncfusion Assistant

If you have GitHub Copilot:

```
@syncfusion-winforms How do I manually add SfDataGrid to my form in .NET 10?
```

### Option 2: Add Controls Programmatically

Instead of using the designer, add controls in code:

```csharp
// In your form constructor or InitializeComponent:
var sfDataGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid
{
    Name = "dataGrid1",
    Dock = DockStyle.Fill
};
this.Controls.Add(sfDataGrid);
```

### Option 3: Check Full Documentation

See: `docs\SYNCFUSION_TOOLBOX_VS2026_GUIDE.md` for comprehensive troubleshooting.

---

## ✅ Success Checklist

- [ ] Diagnostic script runs without errors
- [ ] Visual Studio 2026 installed
- [ ] Essential Studio 32.1.19 installed
- [ ] Component cache cleared
- [ ] Packages restored successfully
- [ ] Toolbox reset completed
- [ ] SfDataGrid appears in Toolbox
- [ ] Designer loads forms without errors

---

**Last Resort:** If nothing works, Syncfusion controls work perfectly via **programmatic instantiation** (no designer needed). See existing panels in `src\WileyWidget.WinForms\Controls\Panels\` for examples.
