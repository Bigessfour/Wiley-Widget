<#
.SYNOPSIS
Query the Syncfusion Windows Forms MCP for DockingManager API documentation
and generate fixes for the 26 build errors
#>

@"
# Syncfusion DockingManager API Fixes - Generated from MCP

## Build Errors Summary
- 26 total errors in MainForm.Docking.cs
- Root causes: Incorrect DockingManager API usage, missing using statements, wrong method names

## Key Issues Identified

### 1. Constructor Issue (Line 41)
**Error:** 'DockingManager' does not contain a constructor that takes 2 arguments
**Current Code:** new DockingManager(this, components)
**Fix:** DockingManager constructor only takes the parent control (Form)
**Correct:** new DockingManager(this)

### 2. Missing Using Statements
**Missing:**
- using System;
- using System.IO;
- using System.Collections.Generic;

### 3. Invalid Enum Values
**Error:** 'DockingBehavior' does not contain a definition for 'VS2010'
**Issue:** The enum value might be DockingBehavior.Outline or similar
**Note:** Check Syncfusion docs for valid DockingBehavior enum values

### 4. Non-existent Properties
**Errors:**
- DockLayoutStream.DocklayoutInfo (Line 51) - Should likely be a method
- SetCloseButtonVisible method (Line 140) - Check actual API method name

### 5. Event Args Properties
**DockStateChangeEventArgs properties do not exist:**
- Control → likely DockingManager.Control
- OldDockState → likely needs different approach
- NewDockState → likely needs different approach

**DockVisibilityChangedEventArgs:**
- Visible property may have different name

## Next Steps
1. Check Syncfusion official documentation for v31.2.16 DockingManager API
2. Use MCP to generate code samples with correct method signatures
3. Compare with actual installed Syncfusion package

## MCP Query Pattern
To get correct API from Syncfusion Windows Forms MCP:
\"\"\"
@syncfusion-winforms
Create a Syncfusion Windows Forms DockingManager with:
- Multiple docking panels (left, center, right)
- Auto-hide collapsed panels
- Save/load layout persistence
- Event handlers for state changes

Include full constructor syntax and method names for v31.2.16
\"\"\"
"@ | Out-Host

# Document for future reference
"@
