# AI-Fetchable Manifest Enhancements

## Overview

This document describes the comprehensive enhancements made to the `generate_repo_urls.py` script and the `ai-fetchable-manifest.json` it generates. These improvements transform the manifest from a basic file index into a full repository intelligence artifact, optimized for AI analysis and enhanced vision of remote repositories.

## Key Improvements Implemented

### 1. ✅ Enhanced Categorization with Sub-Categories

**Problem**: Many files were categorized as "unknown" (284 files), and categorization lacked granularity.

**Solution**:

- Improved file categorization logic to properly classify PowerShell scripts (`.ps1`, `.psm1`, `.psd1`) as "automation"
- Added sub-category support for better organization:
  - **Source Code**: `ui`, `viewmodel`, `view`, `model`, `service`, `data`, `converter`, `behavior`
  - **Tests**: `unit_test`, `integration_test`
  - **Automation**: `scripts`, `ci_cd`, `docker`
  - **Configuration**: `build`
  - **Assets**: `image`, `web`

**Example Output**:

```json
{
  "context": {
    "category": "source_code",
    "sub_category": "viewmodel",
    "importance": "normal"
  }
}
```

### 2. ✅ Project-Level Dependency Graph

**Problem**: No comprehensive view of project dependencies, NuGet packages, or project-to-project references.

**Solution**: Added `dependency_graph` section that includes:

- **Projects**: All `.csproj` files with their NuGet packages and project references
- **NuGet Packages**: Complete list of packages and which projects use them
- **Top Dependencies**: Most frequently used dependencies across the codebase

**Example Output**:

```json
{
  "dependency_graph": {
    "projects": {
      "WileyWidget": {
        "path": "src/WileyWidget/WileyWidget.csproj",
        "nuget_packages": ["Prism.DryIoc", "Syncfusion.SfChart.WPF", "Microsoft.EntityFrameworkCore"],
        "project_references": ["WileyWidget.Abstractions", "WileyWidget.Services"]
      }
    },
    "nuget_packages": {
      "Prism.DryIoc": {
        "used_by_projects": ["WileyWidget", "WileyWidget.Tests"]
      }
    },
    "top_dependencies": [
      { "name": "Prism", "usage_count": 42 },
      { "name": "Syncfusion.WPF", "usage_count": 38 }
    ]
  }
}
```

### 3. ✅ Git Commit History Per File

**Problem**: Git metadata was empty (`last_commit: null`), providing no insight into file evolution.

**Solution**:

- Added `_get_file_git_history()` method to extract last 5 commits per file
- Populates git metadata with:
  - Last commit hash, author, date, message
  - `recent_commits` array with full history

**Configuration**: Can be disabled via config with `"include_git_history": false`

**Example Output**:

```json
{
  "metadata": {
    "git": {
      "last_commit": "d169e29",
      "last_commit_hash": "d169e2920cd7d7414ec61d14a0204c1bbbda709c",
      "last_commit_author": "John Doe",
      "last_commit_date": "2025-11-08T08:15:00+00:00",
      "last_commit_message": "feat: Add budget analysis module",
      "recent_commits": [
        {
          "hash": "d169e2920cd7d7414ec61d14a0204c1bbbda709c",
          "author": "John Doe",
          "date": "2025-11-08T08:15:00+00:00",
          "message": "feat: Add budget analysis module"
        },
        {
          "hash": "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0",
          "author": "Jane Smith",
          "date": "2025-11-05T14:22:00+00:00",
          "message": "fix: Resolve binding errors"
        }
      ]
    }
  }
}
```

### 4. ✅ License Detection and Compliance

**Problem**: No license information in manifest, making compliance tracking difficult.

**Solution**:

- Added `_extract_license_info()` method that detects common licenses (MIT, Apache-2.0, GPL-3.0, BSD)
- Automatically identifies LICENSE file and extracts type

**Example Output**:

```json
{
  "license": {
    "type": "MIT",
    "file": "LICENSE",
    "detected": true
  }
}
```

### 5. ✅ Manifest Freshness Tracking

**Problem**: No way to know if manifest is stale or out-of-date.

**Solution**:

- Added `valid_until` timestamp (default: 7 days from generation)
- Configurable via `"manifest_validity_hours": 168` in config

**Example Output**:

```json
{
  "repository": {
    "generated_at": "2025-11-08T08:15:15",
    "valid_until": "2025-11-15T08:15:15"
  }
}
```

### 6. ✅ JSON Schema Validation

**Problem**: No structured schema for manifest validation.

**Solution**:

- Created `schemas/ai-manifest-schema.json` with full JSON Schema definition
- Added `$schema` reference to manifest for validation support
- Enables IDE autocomplete and validation when viewing/editing manifests

**Usage**:

```json
{
  "$schema": "https://raw.githubusercontent.com/Bigessfour/Wiley-Widget/main/schemas/ai-manifest-schema.json"
}
```

### 7. ✅ Enhanced Dependency Extraction

**Improvements**:

- **C# Projects (`.csproj`)**: Extracts `PackageReference` and `ProjectReference` items
- **C# Files (`.cs`)**: Detects EntityFrameworkCore, Moq, xUnit, Prism, Syncfusion, WPF
- **PowerShell (`.ps1`, `.psm1`)**: Extracts `Import-Module` statements
- **XAML**: Detects Prism, Syncfusion, and custom behavior dependencies

## Configuration Options

Create a `.ai-manifest-config.json` file in your repository root:

```json
{
  "exclude_patterns": ["custom/pattern/.*"],
  "focus_mode": false,
  "include_only_extensions": [".cs", ".xaml", ".md"],
  "parallel_workers": 4,
  "max_file_size_for_summary": 200000,
  "max_summary_length": 1500,
  "test_file_summary_length": 50000,
  "include_git_history": true,
  "manifest_validity_hours": 168,
  "custom_categories": [
    {
      "name": "custom_category",
      "sub_category": "custom_sub",
      "patterns": ["custom/path/**"]
    }
  ]
}
```

## Usage Examples

### Generate Manifest with Default Settings

```powershell
python scripts/tools/generate_repo_urls.py -o ai-fetchable-manifest.json
```

### Generate Manifest for Specific Categories

```powershell
python scripts/tools/generate_repo_urls.py -o manifest.json -c "source_code,documentation"
```

### Use Custom Configuration

```powershell
python scripts/tools/generate_repo_urls.py --config .my-custom-config.json
```

## Manifest Statistics

After generation, the script now displays comprehensive statistics:

```
[SUCCESS] Manifest generated successfully!
   Files: 834
   Size: 15,442,741 bytes
   Categories: source_code, test, documentation, automation, configuration, assets
   Search keywords: 500
   Projects: 11
   NuGet packages: 42
   License: MIT
   Valid until: 2025-11-15 08:15
```

## AI Analysis Use Cases

The enhanced manifest enables powerful AI-driven analysis:

### 1. Dependency Impact Analysis

Query: "Which projects depend on Prism.DryIoc?"

```json
// AI can search dependency_graph.nuget_packages
{
  "Prism.DryIoc": {
    "used_by_projects": ["WileyWidget", "WileyWidget.Tests"]
  }
}
```

### 2. Change History Analysis

Query: "Who last modified the BudgetViewModel?"

```json
// AI can check git.recent_commits for the file
{
  "git": {
    "last_commit_author": "John Doe",
    "last_commit_date": "2025-11-08T08:15:00+00:00",
    "recent_commits": [...]
  }
}
```

### 3. Architecture Understanding

Query: "Show all ViewModels and their dependencies"

```json
// AI can filter files by sub_category: "viewmodel"
// Then check dependencies array for each
```

### 4. Test Coverage Mapping

Query: "Which ViewModels have test files?"

```json
// AI can match "BudgetViewModel.cs" with "BudgetViewModelTests.cs"
// using related_files or category filtering
```

## Future Enhancements (Not Implemented)

### Code Quality Metrics

- Cyclomatic complexity scores
- Maintainability index
- Code duplication detection

### Advanced Relationship Mapping

- Full inheritance chains
- Interface implementations
- Event pub/sub mappings

### Build Artifact Integration

- Link to CI/CD build status
- Test coverage percentages
- Code analysis warnings

## Performance Characteristics

- **Parallel Processing**: Uses `ThreadPoolExecutor` (default 4 workers)
- **Git History**: ~50-100ms per file (configurable)
- **Typical Runtime**: 834 files in ~45-60 seconds
- **Output Size**: ~15MB JSON for 834 files with full metadata

## Schema Validation

Validate your manifest using any JSON Schema validator:

```bash
# Using Python jsonschema
python -c "
import json, jsonschema
schema = json.load(open('schemas/ai-manifest-schema.json'))
manifest = json.load(open('ai-fetchable-manifest.json'))
jsonschema.validate(manifest, schema)
print('✓ Manifest is valid!')
"
```

## Contributing

To add new features to the manifest generator:

1. Update `generate_repo_urls.py` with new extraction methods
2. Update `schemas/ai-manifest-schema.json` to reflect schema changes
3. Update this documentation with usage examples
4. Run tests to ensure backward compatibility

## Version History

### v2.0.0 (2025-11-08) - Major Enhancement Release

- ✅ Added sub-category support
- ✅ Implemented dependency graph generation
- ✅ Added git commit history per file
- ✅ Implemented license detection
- ✅ Added manifest freshness tracking
- ✅ Created JSON Schema definition
- ✅ Enhanced dependency extraction for all file types
- ✅ Improved categorization (reduced "unknown" files by ~200)

### v1.0.0 (2025-10-28) - Initial Release

- Basic file metadata collection
- GitHub URL generation
- Simple categorization
- Search index generation

## References

- **JSON Schema**: http://json-schema.org/
- **Git Log Format**: https://git-scm.com/docs/git-log#_pretty_formats
- **NuGet PackageReference**: https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files
- **Syncfusion WPF Components**: https://help.syncfusion.com/wpf/welcome-to-syncfusion-essential-wpf
