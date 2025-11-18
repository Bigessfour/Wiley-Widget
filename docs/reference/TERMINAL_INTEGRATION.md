# VS Code Terminal Integration - Intelligent Script Runner

## Overview

This document describes the intelligent script execution system integrated with VS Code's terminal shell integration features. The system automatically detects script types and routes them to the appropriate interpreter with proper environment setup.

## Components

### 1. **run-script.ps1** - Universal Script Runner

Location: `scripts/tools/run-script.ps1`

**Features:**

- Automatic file type detection (.py, .ps1, .csx, .cs)
- Intelligent interpreter discovery and validation
- Version checking (Python 3.11+, PowerShell 7.5+, .NET 9.0)
- Environment variable setup per language
- Support for background execution
- Comprehensive error handling and logging
- Fallback strategies (e.g., Docker for C# scripts when dotnet-script unavailable)

**Usage:**

```powershell
# Run any script type
.\scripts\tools\run-script.ps1 -ScriptPath "tests\test_startup_validator.py"

# With verbose output and interpreter info
.\scripts\tools\run-script.ps1 -ScriptPath "tests\test_startup_validator.py" -Verbose -ShowInterpreterInfo

# Background execution
.\scripts\tools\run-script.ps1 -ScriptPath "scripts\maintenance\cleanup.ps1" -Background

# With arguments
.\scripts\tools\run-script.ps1 -ScriptPath "script.py" -Arguments "--verbose", "--debug"
```

### 2. **VS Code Profile** - Enhanced Terminal Experience

Location: `.vscode/profile.ps1`

**Features:**

- Auto-enables VS Code shell integration using official API
- Sets up Python, .NET, and PowerShell environments
- Provides convenient aliases and helper functions
- Custom prompt with Git branch and project indicators
- PSReadLine enhancements for better command editing

**Aliases:**

- `rscript` / `rs` - Run any script type
- `pyrun` - Run Python scripts with interpreter info
- `validator` - Quick shortcut to run startup validator
- `wwbuild` - Build Wiley Widget solution
- `trunkcheck` - Run Trunk CLI checks

**Installation:**
Add to your PowerShell profile (`code $PROFILE`):

```powershell
if (Test-Path "$env:WILEY_WIDGET_ROOT\.vscode\profile.ps1") {
    . "$env:WILEY_WIDGET_ROOT\.vscode\profile.ps1"
}
```

### 3. **Terminal Profiles** - Language-Specific Terminals

Location: `.vscode/settings.json`

**Configured Profiles:**

- **PowerShell 7.5+** - Default, with shell integration enabled
- **Python 3.11** - Pre-configured with PYTHONPATH and PYTHONUNBUFFERED
- **Command Prompt** - Legacy Windows CMD support

Each profile has:

- Appropriate icon
- TERM_PROGRAM environment variable for VS Code integration
- Language-specific environment variables
- Proper PATH configuration

### 4. **VS Code Tasks** - One-Click Script Execution

Location: `.vscode/tasks.json`

**Available Tasks:**

- **Run Script (Intelligent)** - Auto-detects script type
- **Run Python Script** - Python-specific with pytest integration
- **Run PowerShell Script** - PowerShell with PSScriptAnalyzer
- **Run CSX Script** - C# script execution via Docker or dotnet-script
- **Run Startup Validator** - Quick access to Python validator

**Access:**

- Press `Ctrl+Shift+P` → "Tasks: Run Task"
- Or use keyboard shortcut for build tasks

## VS Code Shell Integration Features Enabled

Based on the [official documentation](https://code.visualstudio.com/docs/terminal/shell-integration), we've enabled:

### ✅ Command Detection & Navigation

- Ctrl/Cmd+Up/Down to navigate between commands
- Visual command boundaries in terminal
- Exit code tracking and display

### ✅ Command Decorations

- Blue circles for successful commands
- Red circles for failed commands
- Overview ruler indicators in scrollbar
- Context menu with "Re-run Command", "Copy Output"

### ✅ IntelliSense in Terminal

- Tab completion for files, folders, commands
- Argument suggestions for git, dotnet, npm, etc.
- Triggered by Ctrl+Space or automatically while typing

### ✅ Quick Fixes

- Auto-suggestions for common errors
- Port conflict resolution
- Git push upstream suggestions
- Command-not-found alternatives

### ✅ Enhanced Accessibility

- Audio cues for command completion/failure
- Better screen reader support
- Accessible buffer navigation (Alt+F2)

### ✅ Sticky Scroll

- Command stays visible at top of viewport
- Click to jump to command location

## Interpreter Discovery Priority

### Python 3.11+

1. Windows Store: `C:\Users\$USERNAME\AppData\Local\Microsoft\WindowsApps\python3.11.exe`
2. Windows Store generic: `python.exe`
3. Standard install: `C:\Python311\python.exe`
4. Program Files: `C:\Program Files\Python311\python.exe`
5. PATH resolution: `python3.11`, `python3`, `python`

### PowerShell 7.5+

1. Stable: `C:\Program Files\PowerShell\7\pwsh.exe`
2. Preview: `C:\Program Files\PowerShell\7-preview\pwsh.exe`
3. PATH resolution: `pwsh`
4. Fallback: Current session PowerShell

### C# Scripts (.csx)

1. dotnet-script global tool
2. Docker with wiley-widget/csx-mcp:local image
3. Error if neither available

## Environment Variables Set

### Python

- `PYTHONPATH` = Workspace root
- `PYTHONUNBUFFERED` = "1" (immediate output)
- `TERM_PROGRAM` = "vscode"

### PowerShell

- `WILEY_WIDGET_ROOT` = Workspace root
- `TERM_PROGRAM` = "vscode"
- Shell integration variables (auto-set by VS Code)

### C#

- `WW_REPO_ROOT` = "/app" (Docker) or workspace root
- `WW_LOGS_DIR` = Logs directory path

## Testing

### Test Script Runner

```powershell
# Python script
.\scripts\tools\run-script.ps1 -ScriptPath "tests\test_startup_validator.py"

# PowerShell script
.\scripts\tools\run-script.ps1 -ScriptPath "scripts\maintenance\git-update.ps1"

# C# script
.\scripts\tools\run-script.ps1 -ScriptPath "scripts\examples\csharp\44-xaml-binding-static-analyzer.csx"
```

### Test VS Code Tasks

1. Press `Ctrl+Shift+P`
2. Type "Tasks: Run Task"
3. Select "Run Startup Validator (Python)"
4. Verify output in terminal with decorations and exit code

### Test Profile Aliases

```powershell
# Load profile (in VS Code terminal)
. .\.vscode\profile.ps1

# Test aliases
rscript tests\test_startup_validator.py
pyrun tests\test_startup_validator.py
validator
```

## Benefits

### For Developers

✅ No manual interpreter selection
✅ Consistent environment across team
✅ One command for all script types
✅ Visual feedback in terminal (decorations, colors)
✅ Command history and navigation
✅ Quick re-run of failed commands

### For CI/CD

✅ Same runner script works locally and in pipelines
✅ Automatic fallback strategies (e.g., Docker for CSX)
✅ Exit code propagation
✅ Verbose logging for debugging

### For Debugging

✅ Interpreter version displayed
✅ Environment variables logged
✅ Clear error messages with resolution hints
✅ Stack traces preserved

## Troubleshooting

### Python Not Found

**Error:** `Python 3.11+ not found`

**Solution:**

1. Install Python 3.11 from Microsoft Store (recommended)
2. Or download from https://www.python.org/downloads/
3. Ensure added to PATH

### PowerShell Version Too Old

**Error:** `PowerShell 7.5+ not found`

**Solution:**

1. Install PowerShell 7.5+ from https://aka.ms/powershell
2. Update `.vscode/settings.json` terminal profile path if installed elsewhere

### Shell Integration Not Working

**Symptoms:** No command decorations, no IntelliSense

**Solution:**

1. Check `terminal.integrated.shellIntegration.enabled` is `true` in settings
2. Restart VS Code
3. Open new terminal (old terminals won't have integration)
4. Verify `$env:TERM_PROGRAM` equals "vscode"

### CSX Scripts Fail

**Error:** `No C# script executor found`

**Solution:**

1. Install dotnet-script: `dotnet tool install -g dotnet-script`
2. Or ensure Docker is running for fallback
3. Build Docker image: Run task "csx:build-image"

## Integration with Existing Tools

### Trunk CLI

Compatible with Trunk's file checks. Use:

```powershell
trunkcheck --fix  # Via profile alias
# Or
trunk check --fix
```

### Git Automation

Compatible with `scripts/maintenance/git-update.ps1`:

```powershell
rscript scripts\maintenance\git-update.ps1 -Message "Update"
```

### MCP Servers

Compatible with C# MCP evaluation:

```powershell
rscript scripts\examples\csharp\60P-dashboardviewmodel-unit-test.csx
```

## Future Enhancements

- [ ] Add support for Jupyter notebooks (.ipynb)
- [ ] Integrate with VS Code Test Explorer
- [ ] Add shell history synchronization across sessions
- [ ] Create custom quick fixes for project-specific errors
- [ ] Add F# script support (.fsx)
- [ ] Add TypeScript/Node.js script support (.ts)

## References

- [VS Code Terminal Shell Integration](https://code.visualstudio.com/docs/terminal/shell-integration)
- [PowerShell Profile Documentation](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_profiles)
- [VS Code Tasks Documentation](https://code.visualstudio.com/docs/editor/tasks)
- [Python Virtual Environments](https://docs.python.org/3/library/venv.html)

---

**Last Updated:** November 11, 2025
**Wiley Widget Development Team**
