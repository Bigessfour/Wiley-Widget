# Run WileyWidget WinForms App

## ✅ Quick Launch

### Method 1: Press F5 (Recommended)

In VS Code:

1. Press **F5**
2. Wait 10-15 seconds for build and launch
3. WileyWidget window will appear

**What happens:**

- Builds the project
- Launches with debugger attached
- Full breakpoint support
- Output window shows logs

### Method 2: Debug View

1. Open Debug panel: **Ctrl+Shift+D**
2. Select configuration: **"Wiley Widget WinForms – Full Symbols + Hot Reload"**
3. Click green play button (or press F5)

### Method 3: Command Palette

1. Press **Ctrl+Shift+P**
2. Type: `Debug: Start Debugging`
3. Select: **"Wiley Widget WinForms – Full Symbols + Hot Reload"**

## ⏱️ Expected Timeline

- **0-3s** - Compilation starts
- **3-10s** - Build in progress
- **10-12s** - Launch initialization
- **12-15s** - Window appears on screen

If nothing appears after 20 seconds:

- Check **Debug Console** (View → Debug Console)
- Look for error messages
- See **Troubleshooting** section below

## 🐛 Troubleshooting

### Window Not Appearing

1. **Check Task Manager**
   - Press **Ctrl+Shift+Esc**
   - Look for `WileyWidget.WinForms.exe`
   - If found: Click it → Click "Switch to"

2. **Check Behind VS Code**
   - Click outside VS Code window
   - Or press **Alt+Tab** to cycle windows

3. **Check Debug Console**
   - View → Debug Console (Ctrl+Shift+Y)
   - Look for error messages

### Build Fails

```
If you see "Build failed" in Debug Console:
1. Open Terminal: Ctrl+`
2. Run: dotnet clean WileyWidget.sln
3. Run: dotnet build WileyWidget.sln
4. Then press F5 again
```

### "Program not found" Error

1. Run build first:
   ```powershell
   dotnet build src/WileyWidget.WinForms/WileyWidget.WinForms.csproj
   ```
2. Then press F5

## 🎯 What You'll See

When the app launches:

- **Main Window** - WileyWidget dashboard
- **Syncfusion UI** - Themed controls (Office2019Colorful)
- **Menu Bar** - File, Edit, View, Tools, Help
- **Status Bar** - Connection status at bottom
- **Various Panels** - Charts, grids, controls

## 📊 Debug Features

Once running with F5, you can:

### Set Breakpoints

- Click on line numbers in code editor
- Red dot appears
- Execution pauses when line is hit

### Step Through Code

- **F10** - Step over (next line)
- **F11** - Step into (enter function)
- **Shift+F11** - Step out (exit function)

### Watch Variables

- Debug → Windows → Watch
- Add variable names
- See live values

### Immediate Window

- Debug → Windows → Immediate
- Execute expressions while paused
- Check variable values

## ⏹️ Stop Debugging

- **Shift+F5** - Stop debugging
- **Ctrl+C** - In terminal (if running via `dotnet run`)
- Close the WileyWidget window

## ✅ Expected Success

Window appears with:

- ✅ Form title: "WileyWidget"
- ✅ Menu bar with options
- ✅ Status bar at bottom
- ✅ Various UI controls
- ✅ Can interact with buttons and menus

---

**Status:** Ready to launch  
**Action:** Press **F5** to start debugging
