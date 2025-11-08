<#
.SYNOPSIS
  Lightweight, safe Git update script for CI/Local use.

.DESCRIPTION
  This script stages new and modified files, commits with a message, and
  pushes the current branch to the specified remote. It follows conservative
  best-practices: it detects unmerged conflicts, ensures untracked files are
  added, checks upstream tracking, pulls (rebase) if the remote is ahead, and
  then pushes. The script is intended to run under PowerShell 7.5.x.

.PARAMETER Message
  Commit message. If omitted, a reasonable generated message will be used.

.PARAMETER Remote
  The remote to push to (default: 'origin').

.PARAMETER Branch
  The branch to operate on. If omitted the current checked-out branch is used.

.PARAMETER Force
  Force the commit/push even if there are no changes (will no-op by default).

.PARAMETER DryRun
  Show what would be done without executing destructive actions.

Examples
  .\git-update.ps1 -Message "chore: apply local fixes" -Verbose
  .\git-update.ps1 -DryRun

#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $Message = '',

    [Parameter()]
    [string] $Remote = 'origin',

    [Parameter()]
    [string] $Branch = '',

    [Parameter()]
    [switch] $Force,

    [Parameter()]
    [switch] $DryRun,

    [Parameter()]
    [bool] $RunManifest = $true
)

function Throw-Termination($msg, [int]$code = 1) {
    Write-Error $msg
    exit $code
}

function Run-Git([string[]]$GitArgs) {
    Write-Verbose "git $($GitArgs -join ' ')"
    $proc = & git @GitArgs 2>&1
    $exit = $LASTEXITCODE
    return @{ ExitCode = $exit; Output = $proc }
}

# Filter out git warning lines (e.g., CRLF -> LF messages) and empty lines
function Clean-GitLines([string[]]$lines) {
    if (-not $lines) { return @() }
    return $lines | ForEach-Object { $_.Trim() } |
    Where-Object { $_ -ne '' -and ($_ -notmatch '^warning:') -and ($_ -notmatch 'CRLF will be replaced by LF') }
}

# Find a suitable Python executable: prefer local venv, then system python versions
function Find-PythonExe {
    # Check common virtualenv folders
    $candidates = @(
        (Join-Path (Get-Location) '.venv\Scripts\python.exe'),
        (Join-Path (Get-Location) 'venv\Scripts\python.exe'),
        (Join-Path (Get-Location) '.venv/bin/python'),
        (Join-Path (Get-Location) 'venv/bin/python')
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return (Resolve-Path $c).Path }
    }

    # Try well-known names on PATH
    $names = @('python3.11', 'python3', 'python')
    foreach ($n in $names) {
        $cmd = Get-Command $n -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }

    # Windows App store path fallback
    $possible = "C:\\Users\\$env:USERNAME\\AppData\\Local\\Microsoft\\WindowsApps\\python3.11.exe"
    if (Test-Path $possible) { return $possible }

    return $null
}

function Run-PythonScript($pythonExe, $scriptPath, [string[]]$args) {
    Write-Output "Running Python: $pythonExe $scriptPath $($args -join ' ')"
    $out = & $pythonExe $scriptPath @args 2>&1
    $exit = $LASTEXITCODE

    # Output the results to console
    if ($out) {
        $out | ForEach-Object { Write-Output $_ }
    }

    return @{ ExitCode = $exit; Output = $out }
}

function Assert-ScriptsLayout {
    $repoRoot = (Get-Location).Path
    $requiredPaths = @(
        'scripts/maintenance',
        'scripts/tools',
        'scripts/testing',
        'scripts/examples',
        'scripts/maintenance/git-update.ps1',
        'scripts/tools/generate_repo_urls.py'
    )

    $missing = @()
    foreach ($relative in $requiredPaths) {
        $checkPath = Join-Path $repoRoot $relative
        if (-not (Test-Path $checkPath)) {
            $missing += $relative
        }
    }

    if ($missing.Count -gt 0) {
        Throw-Termination "Canonical scripts layout incomplete. Missing: $($missing -join ', '). Restore the expected structure before continuing." 99
    }
}

if ($MyInvocation.MyCommand.Path) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $repoRoot = Resolve-Path ([System.IO.Path]::Combine($scriptDir, '..', '..'))
    Set-Location -Path $repoRoot
} else {
    Set-Location -Path (Resolve-Path -Path .).Path
}

Assert-ScriptsLayout

# Ensure this is a git repository
$check = Run-Git -GitArgs @('rev-parse', '--git-dir')
if ($check.ExitCode -ne 0) {
    Throw-Termination "This directory is not a Git repository (git rev-parse failed)."
}

# Determine current branch if not provided
if ([string]::IsNullOrEmpty($Branch)) {
    $branchInfo = Run-Git -GitArgs @('rev-parse', '--abbrev-ref', 'HEAD')
    if ($branchInfo.ExitCode -ne 0) {
        Throw-Termination "Failed to determine current branch."
    }
    $Branch = $branchInfo.Output -join "`n" | ForEach-Object { $_.Trim() } | Select-Object -First 1
}

Write-Output "Repository branch: $Branch"

# Check for unmerged paths (merge conflicts)
$unmerged = Run-Git -GitArgs @('diff', '--name-only', '--diff-filter=U')
if ($unmerged.ExitCode -ne 0) {
    Throw-Termination "Failed to check for unmerged files."
}
# Clean out warning lines that may appear on stderr (CRLF -> LF messages)
$unmergedPaths = Clean-GitLines $unmerged.Output
if ($unmergedPaths.Count -gt 0) {
    Write-Warning "Unmerged/conflicted files detected:"
    $unmergedPaths | ForEach-Object { Write-Warning "  $_" }
    Throw-Termination "Resolve conflicts before running this script." 2
}

# Gather porcelain status
$status = Run-Git -GitArgs @('status', '--porcelain')
if ($status.ExitCode -ne 0) {
    Throw-Termination "git status failed." 3
}

# Collect untracked files explicitly
$untracked = Run-Git -GitArgs @('ls-files', '--others', '--exclude-standard')
if ($untracked.ExitCode -ne 0) {
    Throw-Termination "git ls-files failed." 4
}

if ($untracked.Output.Count -gt 0) {
    Write-Warning "Untracked files detected: $($untracked.Output.Count)"
    if ($DryRun) {
        # Show a short sample of untracked files in dry-run
        $sample = ($untracked.Output | Select-Object -First 10) -join ', '
        Write-Output "Would add untracked files (dry-run). Sample: $sample"
        # Show planned commit message for adding these files
        $plannedMsg = "chore: add $($untracked.Output.Count) new files: $sample"
        Write-Output "Planned commit message: $plannedMsg"
    } else {
        # Parse and filter untracked files before adding. Exclude common lock/build/generated files.
        $UntrackedExcludePatterns = @(
            '\\.lock$', 'packages.lock.json$', '\\.dll$', '\\.exe$', '\\.pdb$', '^logs/', '^test-logs/', 'ai-fetchable-manifest\.json$', '^bin/', '^obj/', '\\.cache$', '\\.suo$', '\\.user$'
        )

        function MatchesExclude([string]$path) {
            foreach ($pat in $UntrackedExcludePatterns) {
                if ($path -match $pat) { return $true }
            }
            return $false
        }

        $untrackedPaths = Clean-GitLines $untracked.Output
        $toAdd = @()
        foreach ($p in $untrackedPaths) {
            # Normalize to forward slashes for matching
            $pn = $p -replace '\\', '/'
            if (-not (MatchesExclude $pn)) { $toAdd += $p }
        }

        if ($toAdd.Count -eq 0) {
            Write-Output "No untracked files to add after filtering exclusions."
        } else {
            Write-Output "Adding untracked files: $($toAdd.Count)"
            $addArgs = @('add', '--') + $toAdd
            $addRes = Run-Git -GitArgs $addArgs
            if ($addRes.ExitCode -ne 0) {
                Throw-Termination "Failed to add untracked files. Output:`n$($addRes.Output -join "`n")" 5
            }
            # Build a commit message that lists added files (shortened)
            $addedSample = ($toAdd | Select-Object -First 10) -join ', '
            $addSummaryMsg = "chore: add $($toAdd.Count) new files: $addedSample"
            # If there is no other commit message provided, use this when committing
            if ([string]::IsNullOrEmpty($Message)) { $Message = $addSummaryMsg }
        }
    }
}

# Stage all changes
if ($DryRun) {
    Write-Output "Dry-run: would stage all changes (git add -A)."
} else {
    Write-Output "Staging all changes (git add -A)..."
    $addAll = Run-Git -GitArgs @('add', '-A')
    if ($addAll.ExitCode -ne 0) {
        Throw-Termination "git add -A failed. Output:`n$($addAll.Output -join "`n")" 6
    }
}

# Check if there is anything to commit
$diffCached = Run-Git -GitArgs @('diff', '--cached', '--name-only')
if ($diffCached.ExitCode -ne 0) {
    Throw-Termination "git diff --cached failed." 7
}

$stagedFiles = Clean-GitLines $diffCached.Output

if ($stagedFiles.Count -eq 0) {
    if ($Force) {
        Write-Warning "No staged changes, but -Force specified: creating an empty commit."
        if (-not $DryRun) {
            $commitRes = Run-Git -GitArgs @('commit', '--allow-empty', '-m', (if ($Message -ne '') { $Message } else { "chore: empty commit on $Branch - $(Get-Date -Format o)" }))
            if ($commitRes.ExitCode -ne 0) {
                Throw-Termination "Failed to create empty commit. Output:`n$($commitRes.Output -join "`n")" 8
            }
        }
    } else {
        Write-Output "No changes to commit."
        exit 0
    }
} else {
    $codeSignals = @('^src/', '^tests/', '^WileyWidget\.', '^scripts/(?!maintenance/git-update\.ps1$)')
    $docSignals = @('^docs/', '^README\.md$', '^CHANGELOG\.md$', '^CONTRIBUTING\.md$', '^SECURITY\.md$')
    $hasCodeChange = $false
    foreach ($pattern in $codeSignals) {
        if ($stagedFiles | Where-Object { $_ -match $pattern }) {
            $hasCodeChange = $true
            break
        }
    }
    if ($hasCodeChange) {
        $hasDocChange = $false
        foreach ($pattern in $docSignals) {
            if ($stagedFiles | Where-Object { $_ -match $pattern }) {
                $hasDocChange = $true
                break
            }
        }
        if (-not $hasDocChange) {
            Write-Warning "No documentation or changelog updates staged alongside code changes. Review CHANGELOG.md, README.md, or docs/ before completing the commit if behavior changed."
        }
    }

    # Perform commit
    if ([string]::IsNullOrEmpty($Message)) {
        $shortList = ($stagedFiles | Select-Object -First 6) -join ', '
        $Message = "chore: workspace update on $Branch - files: $shortList"
    }

    Write-Output "Committing changes: $Message"
    if (-not $DryRun) {
        $commitRes = Run-Git -GitArgs @('commit', '-m', $Message)
        if ($commitRes.ExitCode -ne 0) {
            Throw-Termination "git commit failed. Output:`n$($commitRes.Output -join "`n")" 9
        }
    }
}

# Ensure remote exists
$remotes = Run-Git -GitArgs @('remote')
if ($remotes.ExitCode -ne 0) {
    Throw-Termination "git remote failed." 10
}
if (-not ($remotes.Output -contains $Remote)) {
    Write-Error "Remote '$Remote' not found. Available remotes: $($remotes.Output -join ', ')"
    Throw-Termination "Remote '$Remote' does not exist." 11
}

# Check for upstream and perform a safe pull (rebase) if remote is ahead
try {
    $upstream = & git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>$null
    $upstream = $upstream -join "`n" | ForEach-Object { $_.Trim() }
} catch {
    $upstream = $null
}

if (-not [string]::IsNullOrEmpty($upstream)) {
    Write-Verbose "Upstream exists: $upstream"
    # Compare local and upstream
    $counts = Run-Git -GitArgs @('rev-list', '--left-right', '--count', 'HEAD...@{u}')
    if ($counts.ExitCode -eq 0) {
        $parts = ($counts.Output -join "") -split '\s+'
        if ($parts.Length -ge 2) {
            $ahead = [int]$parts[0]
            $behind = [int]$parts[1]
            Write-Output "Ahead: $ahead, Behind: $behind"
            if ($behind -gt 0) {
                Write-Warning "Remote has new commits; pulling (rebase + autostash) before push..."
                if (-not $DryRun) {
                    $pullRes = Run-Git -GitArgs @('pull', '--rebase', '--autostash')
                    if ($pullRes.ExitCode -ne 0) {
                        Throw-Termination "git pull --rebase failed. Resolve conflicts and try again. Output:`n$($pullRes.Output -join "`n")" 12
                    }
                }
            }
        }
    } else {
        Write-Verbose "Could not determine ahead/behind counts: $($counts.Output -join ' ')"
    }
} else {
    Write-Warning "No upstream configured for branch '$Branch'. Will set upstream on push."
}

# Push
if ($DryRun) {
    if ([string]::IsNullOrEmpty($upstream)) {
        Write-Output "Dry-run: would run: git push --set-upstream $Remote $Branch"
    } else {
        Write-Output "Dry-run: would run: git push $Remote $Branch"
    }
    exit 0
}

if ([string]::IsNullOrEmpty($upstream)) {
    Write-Output "Pushing and setting upstream: git push --set-upstream $Remote $Branch"
    $pushRes = Run-Git -GitArgs @('push', '--set-upstream', $Remote, $Branch)
} else {
    Write-Output "Pushing: git push $Remote $Branch"
    $pushRes = Run-Git -GitArgs @('push', $Remote, $Branch)
}

if ($pushRes.ExitCode -ne 0) {
    Throw-Termination "git push failed. Output:`n$($pushRes.Output -join "`n")" 13
}

Write-Output "Push succeeded."

# After successful push, optionally update the AI fetchable manifest using the repository's Python environment
if ($RunManifest) {
    $manifestScript = Join-Path (Get-Location) 'scripts\tools\generate_repo_urls.py'
    if (-not (Test-Path $manifestScript)) {
        Write-Warning "Manifest generator script not found at $manifestScript. Skipping manifest update."
        exit 0
    }

    $pythonExe = Find-PythonExe
    if (-not $pythonExe) {
        Throw-Termination "No Python executable found to run manifest generator. Please install Python or create a virtualenv." 14
    }

    Write-Output "Invoking manifest generator with: $pythonExe $manifestScript"
    if ($DryRun) {
        Write-Output "Dry-run: would run manifest generator: $pythonExe $manifestScript -o ai-fetchable-manifest.json"
        exit 0
    }

    $pyRes = Run-PythonScript $pythonExe $manifestScript @('-o', 'ai-fetchable-manifest.json')
    if ($pyRes.ExitCode -ne 0) {
        Write-Warning "Manifest generation failed with exit code $($pyRes.ExitCode)"
        Throw-Termination "Manifest generation failed. Output:`n$($pyRes.Output -join "`n")" 15
    }

    Write-Output "Manifest generated successfully."
}

exit 0
