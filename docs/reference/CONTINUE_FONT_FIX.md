# Continue.dev Font Size 0 Bug - Manual Fix

## ⚡ FASTEST FIX (Works Immediately)

### Use Browser DevTools to Force Font Size

1. **In VS Code, with Continue.dev chat open**:
   - Press **`Ctrl+Shift+P`**
   - Type: **"Developer: Toggle Developer Tools"**
   - Press Enter

2. **In the DevTools Console tab**, paste this JavaScript and press Enter:

```javascript
document.querySelectorAll(".continue-gui-view, .continue-chat-container, .continue-chat-message").forEach((el) => {
  el.style.fontSize = "14px";
  el.style.setProperty("font-size", "14px", "important");
});
```

3. **Close DevTools** - Continue chat should now be readable!

---

## Quick Fix Options:

### Option 1: VS Code Settings (Recommended)

1. **Press `Ctrl+,`** to open Settings
2. Search for: **"continue font"**
3. Look for **"Continue: Font Size"**
4. Set to **14** (or your preferred size)

### Option 2: Edit settings.json Directly

1. **Press `Ctrl+Shift+P`**
2. Type: **"Preferences: Open User Settings (JSON)"**
3. Add this line:

```json
{
  "continue.chatFontSize": 14,
  "continue.fontSize": 14
}
```

4. Save and reload VS Code

### Option 3: Use Zoom (Temporary)

- Press **`Ctrl+Plus`** (zoom in) while in Continue chat
- Press **`Ctrl+Minus`** (zoom out)
- Press **`Ctrl+0`** (reset zoom)

### Option 4: PowerShell Fix (Automated)

Run this command to update your VS Code settings:

```powershell
$settingsPath = "$env:APPDATA\Code\User\settings.json"
if (Test-Path $settingsPath) {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    $settings | Add-Member -NotePropertyName "continue.chatFontSize" -NotePropertyValue 14 -Force
    $settings | Add-Member -NotePropertyName "continue.fontSize" -NotePropertyValue 14 -Force
    $settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath
    Write-Host "✅ Font size updated to 14. Reload VS Code." -ForegroundColor Green
} else {
    Write-Warning "Settings file not found at: $settingsPath"
}
```

---

## Common Issue: Continue Extension Not Loaded

If font settings don't appear:

1. **Check extension is active**:
   - Press `Ctrl+Shift+X` (Extensions)
   - Search "Continue"
   - Ensure it's **Enabled** (not just installed)

2. **Reload VS Code**:
   - Press `Ctrl+Shift+P`
   - Type: **"Developer: Reload Window"**

3. **Reinstall Continue.dev** (if needed):
   ```powershell
   code --uninstall-extension Continue.continue
   code --install-extension Continue.continue
   ```

---

## If Still Not Working:

The font size issue is often caused by CSS conflicts. Try this:

1. **Open Continue Settings**:
   - Press `Ctrl+Shift+P`
   - Type: **"Continue: Open Config"**

2. **Add UI settings**:

   ```json
   {
     "ui": {
       "fontSize": 14,
       "fontFamily": "Consolas, 'Courier New', monospace"
     }
   }
   ```

3. **Restart VS Code completely** (close all windows)

---

Let me know which option works for you!
