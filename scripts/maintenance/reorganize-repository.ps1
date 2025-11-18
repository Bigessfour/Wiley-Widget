<#
.SYNOPSIS
    Reorganizes the Wiley-Widget repository structure following .NET best practices.

.DESCRIPTION
    This script reorganizes the repository by:
    - Removing user-specific and generated files from git tracking
    - Moving source projects to src/ directory
    - Moving test projects to tests/ directory
    - Organizing scripts into categorized subdirectories
    - Centralizing configuration files
    - Updating all project references and paths
    - Updating .gitignore with comprehensive patterns

.PARAMETER DryRun
    Preview changes without executing them.

.PARAMETER SkipBackup
    Skip creating a backup branch (not recommended).

.PARAMETER Force
    Force execution even if there are uncommitted changes.

.EXAMPLE
    .\reorganize-repository.ps1 -DryRun
    Preview what changes will be made.

.EXAMPLE
    .\reorganize-repository.ps1
    Execute the reorganization with safety checks.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$DryRun,
    [switch]$SkipBackup,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# Change to repository root
Push-Location $repoRoot

try {
    Write-Host "🔍 Wiley-Widget Repository Reorganization Script" -ForegroundColor Cyan
    Write-Host "=" * 60 -ForegroundColor Cyan

    # ========================================
    # Phase 1: Pre-flight Checks
    # ========================================
    Write-Host "`n📋 Phase 1: Pre-flight Checks" -ForegroundColor Yellow

    # Check if we're in a git repository
    if (-not (Test-Path ".git")) {
        throw "Not a git repository. Please run this script from the repository root."
    }

    # Check for uncommitted changes
    $gitStatus = git status --porcelain
    if ($gitStatus -and -not $Force) {
        Write-Warning "You have uncommitted changes:"
        git status --short
        throw "Please commit or stash your changes before reorganizing. Use -Force to override."
    }

    # Get current branch
    $currentBranch = git branch --show-current
    Write-Host "✓ Current branch: $currentBranch" -ForegroundColor Green

    # ========================================
    # Phase 2: Create Backup Branch
    # ========================================
    if (-not $SkipBackup -and -not $DryRun) {
        Write-Host "`n💾 Phase 2: Creating Backup Branch" -ForegroundColor Yellow
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $backupBranch = "backup/pre-reorganization-$timestamp"

        git branch $backupBranch
        Write-Host "✓ Created backup branch: $backupBranch" -ForegroundColor Green
        Write-Host "  You can restore with: git checkout $backupBranch" -ForegroundColor Gray
    }

    # ========================================
    # Phase 3: Remove User-Specific Files
    # ========================================
    Write-Host "`n🗑️  Phase 3: Removing User-Specific and Generated Files" -ForegroundColor Yellow

    $filesToRemove = @(
        "%APPDATA%",
        ".continue",
        ".mcp",
        "node_modules",
        ".mypy_cache",
        ".pytest_cache",
        ".ruff_cache",
        ".tmp.drivedownload",
        "test.csx",
        ".coverage"
    )

    foreach ($file in $filesToRemove) {
        if (Test-Path $file) {
            Write-Host "  Removing: $file" -ForegroundColor Gray
            if (-not $DryRun) {
                git rm -rf $file 2>$null
                if (Test-Path $file) {
                    Remove-Item -Path $file -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }

    # ========================================
    # Phase 4: Update .gitignore
    # ========================================
    Write-Host "`n📝 Phase 4: Updating .gitignore" -ForegroundColor Yellow

    $gitignoreAdditions = @"

# ==============================================================================
# REORGANIZATION ADDITIONS - Added $(Get-Date -Format "yyyy-MM-dd")
# ==============================================================================

# IDE-specific configurations (user-specific)
.continue/
.mcp/

# VS Code (keep shareable configs, ignore user-specific)
.vscode/*
!.vscode/extensions.json
!.vscode/tasks.json
!.vscode/launch.json
!.vscode/*.code-snippets
!.vscode/settings.json
.vscode/settings.json.user

# Python development artifacts
.venv/
venv/
__pycache__/

# Node.js artifacts
node_modules/
.npm/
npm-debug.log*
yarn-debug.log*
yarn-error.log*

# User-specific Windows paths
%APPDATA%/
%LOCALAPPDATA%/
%TEMP%/

# Temporary and download folders
.tmp/
.tmp.*/
*.tmp
tmp/
temp/

# Generated manifests (optional - remove comment to ignore)
# ai-fetchable-manifest.json
"@

    if (-not $DryRun) {
        Add-Content -Path ".gitignore" -Value $gitignoreAdditions
        Write-Host "✓ Updated .gitignore" -ForegroundColor Green
    } else {
        Write-Host "  [DRY RUN] Would add comprehensive patterns to .gitignore" -ForegroundColor Gray
    }

    # ========================================
    # Phase 5: Move Source Projects
    # ========================================
    Write-Host "`n📦 Phase 5: Moving Source Projects to src/" -ForegroundColor Yellow

    # Note: src/ already contains the main WileyWidget application
    # We need to move the library projects at root into src/

    $sourceProjects = @(
        "WileyWidget.Abstractions",
        "WileyWidget.Business",
        "WileyWidget.Data",
        "WileyWidget.Facade",
        "WileyWidget.Models",
        "WileyWidget.Services",
        "WileyWidget.Services.Abstractions",
        "WileyWidget.UI",
        "WileyWidget.Webhooks"
    )

    foreach ($project in $sourceProjects) {
        if (Test-Path $project) {
            $destination = "src/$project"
            Write-Host "  Moving: $project -> $destination" -ForegroundColor Gray

            if (-not $DryRun) {
                git mv $project $destination
            }
        } else {
            Write-Host "  Skipping: $project (not found at root)" -ForegroundColor DarkGray
        }
    }

    # Handle "Wiley Widget" folder with space (if exists)
    if (Test-Path "Wiley Widget") {
        Write-Host "  Moving: 'Wiley Widget' -> src/WileyWidget.Legacy" -ForegroundColor Gray
        if (-not $DryRun) {
            git mv "Wiley Widget" "src/WileyWidget.Legacy"
        }
    }

    # ========================================
    # Phase 6: Move Test Projects
    # ========================================
    Write-Host "`n🧪 Phase 6: Moving Test Projects to tests/" -ForegroundColor Yellow

    if (Test-Path "WileyWidget.Tests") {
        Write-Host "  Moving: WileyWidget.Tests -> tests/WileyWidget.Tests" -ForegroundColor Gray

        if (-not $DryRun) {
            New-Item -ItemType Directory -Path "tests" -Force | Out-Null
            git mv "WileyWidget.Tests" "tests/WileyWidget.Tests"
        }
    }

    # ========================================
    # Phase 7: Organize Scripts
    # ========================================
    Write-Host "`n📜 Phase 7: Organizing Scripts" -ForegroundColor Yellow

    $scriptMoves = @{
        "run-e2e.ps1"              = "scripts/testing/run-e2e.ps1"
        "verify-license-setup.ps1" = "scripts/maintenance/verify-license-setup.ps1"
    }

    foreach ($script in $scriptMoves.Keys) {
        if (Test-Path $script) {
            $destination = $scriptMoves[$script]
            Write-Host "  Moving: $script -> $destination" -ForegroundColor Gray

            if (-not $DryRun) {
                $destDir = Split-Path -Parent $destination
                if (-not (Test-Path $destDir)) {
                    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                }
                git mv $script $destination
            }
        }
    }

    # ========================================
    # Phase 8: Centralize Configuration Files
    # ========================================
    Write-Host "`n⚙️  Phase 8: Centralizing Configuration Files" -ForegroundColor Yellow

    # Create config subdirectories
    if (-not $DryRun) {
        New-Item -ItemType Directory -Path "config/development" -Force | Out-Null
        New-Item -ItemType Directory -Path "config/production" -Force | Out-Null
        New-Item -ItemType Directory -Path "config/shared" -Force | Out-Null
    }

    $configMoves = @{
        "app.config"                  = "config/shared/app.config"
        "appsettings.json"            = "config/development/appsettings.json"
        "appsettings.Production.json" = "config/production/appsettings.Production.json"
        "assistant-preferences.yaml"  = "config/assistant-preferences.yaml"
        "event.push.json"             = "config/event.push.json"
    }

    foreach ($configFile in $configMoves.Keys) {
        if (Test-Path $configFile) {
            $destination = $configMoves[$configFile]
            Write-Host "  Moving: $configFile -> $destination" -ForegroundColor Gray

            if (-not $DryRun) {
                git mv $configFile $destination
            }
        }
    }

    # Move example data files to docs
    if (Test-Path "budgeted_amounts.txt") {
        Write-Host "  Moving: budgeted_amounts.txt -> docs/examples/" -ForegroundColor Gray
        if (-not $DryRun) {
            New-Item -ItemType Directory -Path "docs/examples" -Force | Out-Null
            git mv "budgeted_amounts.txt" "docs/examples/budgeted_amounts.txt"
        }
    }

    if (Test-Path "budget_entries_schema.txt") {
        Write-Host "  Moving: budget_entries_schema.txt -> docs/examples/" -ForegroundColor Gray
        if (-not $DryRun) {
            git mv "budget_entries_schema.txt" "docs/examples/budget_entries_schema.txt"
        }
    }

    # ========================================
    # Phase 9: Rename SQL to sql
    # ========================================
    Write-Host "`n📊 Phase 9: Renaming SQL to sql" -ForegroundColor Yellow

    if (Test-Path "SQL") {
        Write-Host "  Renaming: SQL -> sql" -ForegroundColor Gray

        if (-not $DryRun) {
            git mv SQL sql
        }
    }

    # ========================================
    # Phase 10: Update Solution File
    # ========================================
    Write-Host "`n🔧 Phase 10: Updating Solution File References" -ForegroundColor Yellow

    if (-not $DryRun) {
        $slnContent = Get-Content "WileyWidget.sln" -Raw
        $originalContent = $slnContent

        # Update project paths to src/
        foreach ($project in $sourceProjects) {
            $oldPath = "$project\\$project.csproj"
            $newPath = "src\\$project\\$project.csproj"
            $slnContent = $slnContent -replace [regex]::Escape($oldPath), $newPath
        }

        # Update test project path
        $slnContent = $slnContent -replace "WileyWidget\.Tests\\WileyWidget\.Tests\.csproj", "tests\\WileyWidget.Tests\\WileyWidget.Tests.csproj"

        if ($slnContent -ne $originalContent) {
            Set-Content "WileyWidget.sln" -Value $slnContent -NoNewline
            Write-Host "✓ Updated WileyWidget.sln" -ForegroundColor Green
        }
    } else {
        Write-Host "  [DRY RUN] Would update project paths in WileyWidget.sln" -ForegroundColor Gray
    }

    # ========================================
    # Phase 11: Update Project References
    # ========================================
    Write-Host "`n🔗 Phase 11: Updating Project References" -ForegroundColor Yellow

    if (-not $DryRun) {
        # Find all csproj files
        $csprojFiles = Get-ChildItem -Path "src", "tests" -Recurse -Filter "*.csproj" -ErrorAction SilentlyContinue

        foreach ($csproj in $csprojFiles) {
            $content = Get-Content $csproj.FullName -Raw
            $originalContent = $content

            # Update ProjectReference paths
            # From root level to src/
            $content = $content -replace '<ProjectReference Include="\.\.\\([^\\]+)\\', '<ProjectReference Include="..\$1\'

            # From tests to src
            $content = $content -replace '<ProjectReference Include="\.\.\\([^\\]+)\\', '<ProjectReference Include="..\..\src\$1\'

            if ($content -ne $originalContent) {
                Set-Content $csproj.FullName -Value $content -NoNewline
                Write-Host "  Updated: $($csproj.Name)" -ForegroundColor Gray
            }
        }
    } else {
        Write-Host "  [DRY RUN] Would update ProjectReference paths in .csproj files" -ForegroundColor Gray
    }

    # ========================================
    # Phase 12: Validation
    # ========================================
    Write-Host "`n✅ Phase 12: Validation" -ForegroundColor Yellow

    if (-not $DryRun) {
        Write-Host "  Running dotnet restore..." -ForegroundColor Gray
        $restoreResult = dotnet restore WileyWidget.sln 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ dotnet restore succeeded" -ForegroundColor Green
        } else {
            Write-Warning "dotnet restore encountered issues. Review manually."
            Write-Host $restoreResult -ForegroundColor Yellow
        }

        Write-Host "`n  Checking Trunk..." -ForegroundColor Gray
        if (Get-Command trunk -ErrorAction SilentlyContinue) {
            trunk check --ci 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ Trunk check passed" -ForegroundColor Green
            } else {
                Write-Warning "Trunk check found issues. Run 'trunk check' manually."
            }
        }
    } else {
        Write-Host "  [DRY RUN] Would run validation checks" -ForegroundColor Gray
    }

    # ========================================
    # Phase 13: Commit Changes
    # ========================================
    Write-Host "`n💾 Phase 13: Committing Changes" -ForegroundColor Yellow

    if (-not $DryRun) {
        git add -A

        $commitMessage = @"
chore: reorganize repository structure following .NET best practices

BREAKING CHANGE: Project paths have been reorganized

- Moved source projects to src/ directory
- Moved test projects to tests/ directory
- Organized scripts into categorized subdirectories
- Centralized configuration files in config/
- Renamed SQL to sql for consistency
- Updated .gitignore with comprehensive patterns
- Removed user-specific files and caches from repository
- Updated all project references and solution paths

This reorganization improves maintainability and follows industry standards
for .NET solution structure.

Backup branch: $backupBranch
"@

        git commit -m $commitMessage
        Write-Host "✓ Changes committed" -ForegroundColor Green
    } else {
        Write-Host "  [DRY RUN] Would commit all changes" -ForegroundColor Gray
    }

    # ========================================
    # Summary
    # ========================================
    Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
    Write-Host "✨ Repository Reorganization Complete!" -ForegroundColor Green
    Write-Host ("=" * 60) -ForegroundColor Cyan

    if ($DryRun) {
        Write-Host "`n⚠️  DRY RUN MODE - No changes were made" -ForegroundColor Yellow
        Write-Host "Run without -DryRun to execute the reorganization" -ForegroundColor Yellow
    } else {
        Write-Host "`nNext Steps:" -ForegroundColor Cyan
        Write-Host "1. Review the changes: git diff HEAD~1" -ForegroundColor Gray
        Write-Host "2. Test the build: dotnet build" -ForegroundColor Gray
        Write-Host "3. Run tests: dotnet test" -ForegroundColor Gray
        Write-Host "4. Push changes: git push" -ForegroundColor Gray
        Write-Host "`nTo rollback: git reset --hard $backupBranch" -ForegroundColor Yellow
    }
} catch {
    Write-Error "❌ Reorganization failed: $_"
    Write-Host "`nTo rollback changes, run:" -ForegroundColor Yellow
    Write-Host "  git reset --hard HEAD" -ForegroundColor Yellow
    if (-not $SkipBackup) {
        Write-Host "  git checkout $backupBranch" -ForegroundColor Yellow
    }
    exit 1
} finally {
    Pop-Location
}
