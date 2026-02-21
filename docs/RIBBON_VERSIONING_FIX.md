# Ribbon Structure Versioning & Persistence Fix

## Problem
Ribbon updates weren't "sticking" because saved layout files cached old ribbon configurations. When you made changes to the ribbon structure in code (adding/removing buttons, changing groups), the old layout was being restored from cache files, hiding your changes.

## Solution Implemented

### 1. Ribbon Structure Version Tracking
Added `RibbonStructureVersion` constant in [MainForm.cs](../src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs):

```csharp
public const int RibbonStructureVersion = 2; // Increment when ribbon layout changes
```

### 2. Version-Based Invalidation
Modified [MainForm.LayoutPersistence.cs](../src/WileyWidget.WinForms/Forms/MainForm/MainForm.LayoutPersistence.cs) to:
- **Save** the ribbon structure version when saving layouts
- **Check** the version when loading layouts
- **Skip** ribbon state restoration if version mismatch detected

This ensures that whenever you change the ribbon structure in code:
1. The app detects the version mismatch on startup
2. Skips loading the old ribbon state
3. Uses the new ribbon structure from code

## Files Modified

| File | Changes |
|------|---------|
| `src/WileyWidget.WinForms/Forms/MainForm/MainForm.cs` | Added `RibbonStructureVersion = 2` constant |
| `src/WileyWidget.WinForms/Forms/MainForm/MainForm.LayoutPersistence.cs` | Added version checking in `SaveRibbonState()` and `RestoreRibbonState()` |
| `scripts/clear-ribbon-cache.ps1` | New helper script to manually clear layout cache |

## Usage

### For Future Ribbon Changes
When you modify the ribbon structure (add/remove buttons, change groups, reorder tabs):

1. **Increment the version** in `MainForm.cs`:
   ```csharp
   public const int RibbonStructureVersion = 3; // Bump version
   ```

2. **Build and run** - the app will automatically:
   - Detect the version mismatch
   - Skip loading old ribbon state
   - Use your new ribbon structure

### Manual Cache Clearing (Optional)
If you want to force-clear all layout caches:

```powershell
.\scripts\clear-ribbon-cache.ps1
```

This removes:
- `Layouts/` directory (saved workspace layouts)
- `CDock.xml` (docking manager state)
- `CDock.bin` (binary docking cache)

## Technical Details

### Version Flow
1. **On Save** (`SaveRibbonState`):
   ```csharp
   serializer.SerializeObject("Ribbon.StructureVersion", MainFormResources.RibbonStructureVersion);
   ```

2. **On Load** (`RestoreRibbonState`):
   ```csharp
   var savedVersion = (int)(savedVersionObj ?? 0);
   if (savedVersion != MainFormResources.RibbonStructureVersion)
   {
       _logger?.LogInformation("Skipping ribbon state restoration - version mismatch");
       return; // Skip restoration, use code-defined structure
   }
   ```

### What Gets Versioned
- Ribbon structure version controls:
  - Tab count and names
  - Group organization
  - Button names and icons
  - Navigation command bindings

- **NOT versioned** (always from code):
  - Ribbon creation logic
  - Button click handlers
  - Theme styling
  - Panel registry mappings

## Verification

After this fix, ribbon updates will **always apply** because:
1. Ribbon structure is **always created from code** in `InitializeRibbon()`
2. Only compatible saved states are restored (version match required)
3. Version mismatch forces fresh ribbon from code

## Migration Notes

### Existing Users
On first launch after this change (version bumped to 2):
- Saved ribbon state (selected tab, QAT visibility) will be **ignored**
- Ribbon will use **default HomeTab selection**
- Next save will use version 2

### Log Messages
Look for this in logs when version mismatch occurs:
```
Skipping ribbon state restoration - structure version mismatch (saved: 1, current: 2). Ribbon will use defaults.
```

## Related Files
- Ribbon initialization: `MainForm.Chrome.cs` (`InitializeRibbon()`)
- Ribbon helpers: `MainForm.RibbonHelpers.cs` (group/button creation)
- Panel registry: `Services/PanelRegistry.cs` (panel-to-ribbon mapping)
- Layout persistence: `MainForm.LayoutPersistence.cs` (save/load logic)

## Best Practices

### When to Bump Version
✅ **DO bump version** when:
- Adding or removing ribbon buttons
- Changing button names/icons
- Reorganizing groups
- Adding or removing tabs
- Changing navigation targets

❌ **DON'T bump version** when:
- Changing button click handlers
- Updating theme colors
- Modifying panel implementations
- Changing non-ribbon UI

### Development Workflow
1. Make ribbon code changes
2. Bump `RibbonStructureVersion`
3. Build
4. Test (old layout ignored, new structure applies)
5. Commit version bump with ribbon changes

---

**Version:** 1.0  
**Date:** 2026-02-17  
**Author:** GitHub Copilot  
**Status:** ✅ Active
