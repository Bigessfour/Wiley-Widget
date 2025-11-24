# Legacy Scanner Documentation

## Overview

The Legacy Scanner is a comprehensive tool for detecting and exposing Syncfusion and Prism remnants in the Wiley Widget codebase. This is critical for post-refactor validation to ensure no hidden dependencies that could cause build/runtime issues in the pure WinUI 3 setup.

## Available Implementations

### 1. Python Scanner (Recommended)
**Location**: `tools/legacy_scanner.py`

**Features**:
- Cross-platform (runs anywhere Python 3.8+ is available)
- Threaded scanning for performance
- JSON report generation
- Verbose and summary modes
- CI integration support
- Custom pattern loading

**Usage**:
```bash
# Basic scan
python tools/legacy_scanner.py

# Scan specific directory
python tools/legacy_scanner.py --root src

# Verbose output (all hits)
python tools/legacy_scanner.py --verbose

# CI mode (exit 1 if hits found)
python tools/legacy_scanner.py --fail-on-hits

# Custom patterns
python tools/legacy_scanner.py --patterns custom_patterns.json

# Save report to specific file
python tools/legacy_scanner.py --output my_scan_report.json
```

### 2. PowerShell Scanner (Windows Native)
**Location**: `scripts/tools/legacy_scanner.ps1`

**Features**:
- Native Windows/PowerShell solution
- No external dependencies
- Progress bar for large scans
- Colored console output
- JSON export capability

**Usage**:
```powershell
# Basic scan
.\scripts\tools\legacy_scanner.ps1

# Scan specific directory
.\scripts\tools\legacy_scanner.ps1 -Root "C:\Wiley-Widget\src"

# Verbose output
.\scripts\tools\legacy_scanner.ps1 -Verbose

# CI mode with JSON export
.\scripts\tools\legacy_scanner.ps1 -FailOnHits -OutputJson "scan_report.json"

# Include commented code in scan
.\scripts\tools\legacy_scanner.ps1 -IncludeComments
```

## VS Code Tasks

Four tasks are available via **Terminal > Run Task**:

1. **`scan:legacy-code-python`** - Standard Python scan with JSON report
2. **`scan:legacy-code-python-verbose`** - Detailed Python scan with all hits
3. **`scan:legacy-code-powershell`** - PowerShell scan with JSON report
4. **`scan:legacy-code-ci`** - CI mode (fails if legacy code found)

## Detection Patterns

### Syncfusion Patterns
- **Namespaces**: `using Syncfusion.*`
- **Controls**: SfDataGrid, SfChart, SfTreeView, SfBusyIndicator, SfEditors, etc.
- **XAML Tags**: `<syncfusion:SfDataGrid>`, `<SfChart>`, etc.
- **XAML Namespaces**: `xmlns:syncfusion=`
- **Licensing**: `SyncfusionLicenseProvider.RegisterLicense`
- **Package References**: `<PackageReference Include="Syncfusion.*"`

### Prism Patterns
- **Namespaces**: `using Prism.*`
- **Classes/Methods**: BindableBase, DelegateCommand, IEventAggregator, IRegionManager, ContainerLocator, PrismApplication
- **XAML Regions**: `prism:RegionManager.RegionName`, `prism:ClearChildContent`
- **Package References**: `<PackageReference Include="Prism.*"`

## Baseline Scan Results (2025-11-23)

### Summary
- **Total Hits**: 22 (Prism: 0, Syncfusion: 22)
- **Affected Files**: 2
- **Status**: ⚠️ Legacy code detected

### Affected Files

#### 1. `src/WileyWidget.WinUI/Resources/DataTemplates.xaml` (21 hits)
**Issue**: Syncfusion DataGrid still in use
- Multiple `<syncfusion:SfDataGrid>` tags
- Syncfusion column definitions
- Legacy control attributes

**Action Required**: Replace with native WinUI DataGrid from `Microsoft.UI.Xaml.Controls`

#### 2. `Styles/Generic.xaml` (1 hit)
**Issue**: Syncfusion namespace declaration
- `xmlns:syncfusion="using:Syncfusion.UI.Xaml.Core"`

**Action Required**: Remove unused namespace declaration

## CI Integration

### GitHub Actions Example
```yaml
- name: Scan for Legacy Code
  run: python tools/legacy_scanner.py --fail-on-hits --output scan_report.json
  
- name: Upload Scan Report
  if: failure()
  uses: actions/upload-artifact@v3
  with:
    name: legacy-scan-report
    path: scan_report.json
```

### Pre-commit Hook
```bash
#!/bin/bash
# .git/hooks/pre-commit
python tools/legacy_scanner.py --fail-on-hits
if [ $? -ne 0 ]; then
    echo "Legacy code detected! Please remove before committing."
    exit 1
fi
```

## Custom Patterns

Create a JSON file with additional patterns:

```json
{
  "WPF": {
    "Namespace_Using": "using\\s+System\\.Windows",
    "Controls": "\\b(Window|UserControl|StackPanel)\\b",
    "Suggestion": "Migrate to Microsoft.UI.Xaml equivalents"
  }
}
```

Load with: `python tools/legacy_scanner.py --patterns custom.json`

## Output Format

### Console Output
- Summary statistics (total hits, by category, affected files)
- Top 3 hits per file with line numbers
- Refactoring suggestions
- Next steps guidance

### JSON Report Structure
```json
{
  "summary": {
    "total_hits": 22,
    "prism_hits": 0,
    "syncfusion_hits": 22,
    "affected_files": 2,
    "scan_date": "2025-11-23T17:51:54.421256",
    "root_dir": "C:\\Users\\biges\\Desktop\\Wiley-Widget"
  },
  "file_summary": {
    "path/to/file.xaml": [
      {
        "file_path": "path/to/file.xaml",
        "line_num": 30,
        "line_content": "<syncfusion:SfDataGrid",
        "pattern_type": "Syncfusion_XAML_Tags",
        "suggestion": "Replace with native WinUI controls..."
      }
    ]
  }
}
```

## Performance Characteristics

- **Scanning Speed**: ~335 files in <3 seconds
- **Memory Usage**: Minimal (streaming file reads)
- **Parallel Processing**: 4 threads by default (configurable)
- **File Size Limit**: Skips files >5MB (likely binary)

## Ignored Directories

The scanner automatically skips:
- Build artifacts: `bin`, `obj`, `packages`
- Node modules: `node_modules`
- Version control: `.git`
- IDE files: `.vs`, `.vscode`
- Test results: `TestResults`, `coverage`
- Temporary files: `temp`, `logs`, `secrets`, `ci-logs`

## Next Steps

1. **Immediate Priority**: Fix `DataTemplates.xaml`
   - Replace SfDataGrid with WinUI DataGrid
   - Update XAML bindings and column definitions
   - Test thoroughly in UI

2. **Clean Up**: Remove Syncfusion namespace from `Generic.xaml`

3. **Verify**: Re-run scanner to confirm 0 hits
   ```bash
   python tools/legacy_scanner.py
   ```

4. **CI Integration**: Add to GitHub Actions workflow
   ```yaml
   - name: Legacy Code Check
     run: python tools/legacy_scanner.py --fail-on-hits
   ```

5. **Documentation**: Update migration docs with findings

## Troubleshooting

### Python Scanner Issues
- **ImportError**: Ensure Python 3.8+ is installed
- **UnicodeDecodeError**: Files with invalid encoding are automatically skipped
- **PermissionError**: Run with appropriate file system permissions

### PowerShell Scanner Issues
- **Execution Policy**: Run with `-ExecutionPolicy Bypass`
- **Progress Bar**: Disable with `-ProgressPreference SilentlyContinue` if needed
- **Large Files**: Automatically skipped if >5MB

## Enhancement Ideas

1. **AST Parsing**: Use tree-sitter for accurate C# parsing (ignore comments)
2. **Auto-Fix**: Generate refactoring suggestions with line-by-line fixes
3. **HTML Reports**: Generate visual reports with syntax highlighting
4. **Thresholds**: Set warning/error thresholds for different categories
5. **Historical Tracking**: Track scan results over time
6. **IDE Integration**: VS Code extension for inline warnings

## Maintenance

- **Update Patterns**: Edit built-in patterns in scanner source or use custom JSON
- **Performance Tuning**: Adjust `max_workers` for larger codebases
- **False Positives**: Use comments to exclude specific lines: `// legacy-scan:ignore`

## References

- Python Script: `tools/legacy_scanner.py`
- PowerShell Script: `scripts/tools/legacy_scanner.ps1`
- Baseline Report: `legacy_scan_baseline.json`
- VS Code Tasks: `.vscode/tasks.json`
