# Fetchability Resources Manifest

This document describes the fetchability resources system for the Wiley Widget project, which provides machine-readable file manifests with SHA256 hashes for CI/CD pipeline integration.

## Overview

The fetchability resources system generates a JSON manifest containing:
- SHA256 hashes for all files (tracked and untracked)
- File metadata (size, timestamps, extensions)
- Git repository information (commit, branch, status)
- Statistics and generation metadata

## Files

- `fetchability-resources.json` - The generated manifest file (in repository root)
- `scripts/Generate-FetchabilityManifest.ps1` - Main PowerShell script
- `scripts/ci-generate-manifest.ps1` - CI/CD integration wrapper

## Usage

### Manual Generation

```powershell
# Generate manifest in repository root
.\scripts\Generate-FetchabilityManifest.ps1

# Generate with custom output path
.\scripts\Generate-FetchabilityManifest.ps1 -OutputPath "custom-manifest.json"
```

### CI/CD Integration

#### GitHub Actions Example

```yaml
name: Build and Deploy
on: [push, pull_request]

jobs:
  build:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v4

    - name: Setup PowerShell
      uses: actions/setup-powershell@v1
      with:
        pwsh: true

    - name: Generate Fetchability Manifest
      run: .\scripts\ci-generate-manifest.ps1

    - name: Upload Manifest
      uses: actions/upload-artifact@v4
      with:
        name: fetchability-manifest
        path: fetchability-resources.json
```

#### Azure DevOps Example

```yaml
trigger:
  branches:
    include:
    - main

pool:
  vmImage: 'windows-latest'

steps:
- checkout: self

- task: PowerShell@2
  displayName: 'Generate Fetchability Manifest'
  inputs:
    targetType: 'inline'
    script: '.\scripts\ci-generate-manifest.ps1'

- task: PublishBuildArtifacts@1
  displayName: 'Publish Manifest'
  inputs:
    pathToPublish: 'fetchability-resources.json'
    artifactName: 'fetchability-manifest'
```

## JSON Schema

```json
{
  "metadata": {
    "generatedAt": "ISO8601 timestamp",
    "generator": "script name",
    "repository": {
      "commitHash": "git commit hash",
      "branch": "current branch",
      "isDirty": false,
      "remoteUrl": "git remote URL"
    },
    "statistics": {
      "totalFiles": 35,
      "trackedFiles": 33,
      "untrackedFiles": 2,
      "totalSize": 114688
    }
  },
  "files": [
    {
      "path": "relative/path/to/file",
      "sha256": "sha256 hash",
      "size": 1234,
      "lastModified": "ISO8601 timestamp",
      "tracked": true,
      "extension": ".ext"
    }
  ]
}
```

## File Exclusions

The following file types and directories are automatically excluded:
- Build artifacts: `bin/`, `obj/`, `TestResults/`
- IDE files: `.vs/`, `.idea/`, `*.user`, `*.suo`
- Temporary files: `*.tmp`, `*.log`, `*.cache`
- Git directory: `.git/`
- Tool directories: `.trunk/`, `node_modules/`
- OS files: `Thumbs.db`, `Desktop.ini`

## Use Cases

1. **File Integrity Verification**: Compare SHA256 hashes to detect file tampering
2. **CI/CD Auditing**: Track what files were present during builds
3. **Deployment Validation**: Ensure all expected files are deployed
4. **Change Detection**: Identify modified files between commits
5. **Security Compliance**: Maintain file inventory for compliance requirements

## Parameters

### Generate-FetchabilityManifest.ps1

- `OutputPath`: Custom output file path (default: "fetchability-resources.json")
- `ExcludePatterns`: Additional patterns to exclude

### ci-generate-manifest.ps1

- `OutputPath`: Custom output file path
- `FailOnUntracked`: Fail the build if untracked files are found

## Output Variables (GitHub Actions)

When run in GitHub Actions, the CI script sets these output variables:
- `manifest-path`: Path to the generated manifest
- `file-count`: Total number of files processed
- `commit-hash`: Current git commit hash

## Requirements

- PowerShell 7.0+
- Git repository
- Write access to output directory

## Error Handling

The scripts include comprehensive error handling:
- Git repository validation
- File access permission checks
- SHA256 calculation error handling
- JSON serialization validation

## Security Notes

- SHA256 hashes provide cryptographic integrity verification
- File paths are normalized to forward slashes for consistency
- Timestamps use UTC for global compatibility
- Git information includes repository state for audit trails
