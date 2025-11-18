# repair-via-clone.ps1
# Safely clone remote, copy working-tree changes from the current (possibly corrupted) repo into the clone, commit there (no push).
# Run from repository root. PowerShell 7+ recommended.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workspace = (Get-Location).Path
$parent = Split-Path $workspace -Parent
$leaf = Split-Path $workspace -Leaf
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$cloneDir = Join-Path $parent ("${leaf}_repair_clone_${timestamp}")

Write-Output "Workspace: $workspace"
Write-Output "Clone target: $cloneDir"

# Get remote URL
try {
    $rawRemote = & git -C $workspace remote get-url origin 2>$null
    $remoteUrl = if ($rawRemote) { $rawRemote.Trim() } else { $null }
} catch {
    Write-Error "Failed to get 'origin' remote URL from repo at $workspace. Aborting."
    exit 1
}
if (-not $remoteUrl) {
    Write-Error "No 'origin' remote found. Provide a remote or run this manually. Aborting."
    exit 1
}
Write-Output "Remote URL: $remoteUrl"

# Clone from remote (fetch fresh copy)
Write-Output "Cloning fresh copy from remote: $remoteUrl"
try {
    & git clone $remoteUrl $cloneDir 2>&1 | ForEach-Object { Write-Output $_ }
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "git clone failed, falling back to local init..."
        New-Item -ItemType Directory -Path $cloneDir -Force | Out-Null
        Push-Location $cloneDir
        & git init | Out-Null
        & git remote add origin $remoteUrl 2>$null
        & git fetch origin 2>&1 | ForEach-Object { Write-Output $_ }
        Pop-Location
    }
    Write-Output "Fresh repository created at $cloneDir"
} catch {
    Write-Error "Failed to clone or initialize repository: $($_.Exception.Message)"
    exit 1
}

# Detect current branch
$branch = git -C $workspace rev-parse --abbrev-ref HEAD 2>$null
if (-not $branch) { $branch = "main" }
Write-Output "Current branch: $branch"

# Checkout branch in clone (create if missing)
Write-Output "Checking out branch '$branch' in clone..."
Push-Location $cloneDir
try {
    & git rev-parse --verify $branch 2>$null | Out-Null
    $exists = $LASTEXITCODE -eq 0
} catch {
    $exists = $false
}
if ($exists) {
    & git checkout $branch | Out-Null
} else {
    # create branch
    & git checkout -b $branch | Out-Null
}
Pop-Location


# Gather modified and untracked files from original workspace
Write-Output "Attempting to list modified files from corrupted repo..."
try {
    $modified = (& git -C $workspace ls-files -m 2>$null) -split "`n" | Where-Object { $_ -and $_.Trim() -ne '' }
} catch {
    Write-Warning "Could not list modified files (corruption). Will use --cached instead."
    $modified = @()
}

Write-Output "Attempting to list staged files from corrupted repo..."
try {
    $staged = (& git -C $workspace diff --cached --name-only 2>$null) -split "`n" | Where-Object { $_ -and $_.Trim() -ne '' }
    if ($staged) { $modified += $staged }
} catch {
    Write-Warning "Could not list staged files."
}

Write-Output "Listing untracked files..."
try {
    $untracked = (& git -C $workspace ls-files --others --exclude-standard 2>$null) -split "`n" | Where-Object { $_ -and $_.Trim() -ne '' }
} catch {
    Write-Warning "Could not list untracked files."
    $untracked = @()
}

$all = @()
if ($modified) { $all += $modified }
if ($untracked) { $all += $untracked }

# Fallback: if git is too corrupted, manually list key directories
if (-not $all -or $all.Count -eq 0) {
    Write-Warning "Git commands failed to list changes. Using fallback: scanning working directory for non-ignored files..."
    $criticalDirs = @('src', 'WileyWidget.Services', 'WileyWidget.UI', 'WileyWidget.Tests', 'scripts', 'docs', 'docker')
    $criticalFiles = @('.gitignore', 'WileyWidget.csproj', 'WileyWidget.sln', 'Directory.Build.props', 'Directory.Packages.props')

    foreach ($dir in $criticalDirs) {
        $dirPath = Join-Path $workspace $dir
        if (Test-Path $dirPath) {
            Get-ChildItem $dirPath -Recurse -File | Where-Object {
                $_.FullName -notmatch '[\\/](bin|obj|node_modules|\.vs|\.git)[\\/]'
            } | ForEach-Object {
                $rel = $_.FullName.Substring($workspace.Length + 1)
                $all += $rel
            }
        }
    }

    foreach ($file in $criticalFiles) {
        $filePath = Join-Path $workspace $file
        if (Test-Path $filePath) {
            $all += $file
        }
    }

    $all = $all | Select-Object -Unique
    Write-Output "Fallback scan found $($all.Count) files to copy."
}

if (-not $all -or $all.Count -eq 0) {
    Write-Output "No modified or untracked files detected in workspace. Nothing to copy."
    Write-Output "You can still inspect $cloneDir if you want a fresh clone."
    exit 0
}

Write-Output "Preparing to copy $($all.Count) files into clone..."

foreach ($f in $all) {
    $src = Join-Path $workspace $f
    if (-not (Test-Path -LiteralPath $src)) {
        Write-Warning "Source file missing (skipping): $f"
        continue
    }
    $dst = Join-Path $cloneDir $f
    $dstdir = Split-Path $dst -Parent
    if (-not (Test-Path $dstdir)) { New-Item -ItemType Directory -Path $dstdir -Force | Out-Null }
    Copy-Item -LiteralPath $src -Destination $dst -Force
}

# Commit in clone
Write-Output "Staging copied files in clone..."
Push-Location $cloneDir
# Try git add -A, then retry a few times if nothing staged. Fall back to per-file adds if needed.
function Get-StatusArray {
    $raw = & git status --porcelain 2>$null
    if (-not $raw) { return @() }
    return ($raw -split "`n" | Where-Object { $_ -and $_.Trim() -ne '' })
}

& git add -A 2>$null
$status = Get-StatusArray
if ((@($status)).Count -eq 0) {
    Write-Warning "git add didn't stage files; retrying up to 3 times..."
    for ($i = 0; $i -lt 3; $i++) {
        Start-Sleep -Milliseconds 200
        & git add -A 2>$null
        $status = Get-StatusArray
        if ((@($status)).Count -gt 0) { break }
    }
}

if ((@($status)).Count -eq 0) {
    Write-Warning "Staging still empty; attempting per-file add for copied files..."
    foreach ($f in $all) {
        $rel = $f.Trim()
        if (-not $rel) { continue }
        try {
            & git add -- $rel 2>$null
        } catch {
            $errMsg = $_.Exception.Message
            Write-Warning "Per-file add failed for ${rel}: $errMsg"
        }
    }
    $status = Get-StatusArray
}

if ((@($status)).Count -eq 0) {
    Write-Output "No changes detected in clone after copy. Nothing to commit."
    Write-Output "Clone is at: $cloneDir"
    Pop-Location
    exit 0
}

$summaryCount = (@($status)).Count
$commitMessage = "repair: import $summaryCount working-tree changes from corrupted local repo on branch '$branch' at $timestamp"
Write-Output "Committing $summaryCount changes in clone with message:`n$commitMessage"

& git commit -m $commitMessage
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Initial commit failed; attempting 'git add -A' and commit again..."
    & git add -A 2>$null
    & git commit -m $commitMessage 2>&1 | ForEach-Object { Write-Output $_ }
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Commit in clone failed. Inspect: $cloneDir"
        Pop-Location
        exit 1
    }
}

Pop-Location
Write-Output "Commit created in clone at: $cloneDir"
Write-Output "Review the clone, run tests, and when satisfied push changes from there with:"
Write-Output "  cd '$cloneDir'"
Write-Output "  git push origin $branch"

# Print a short report of copied files
Write-Output "Copied files (sample up to 50):"
$all | Select-Object -First 50 | ForEach-Object { Write-Output " - $_" }

Write-Output "Repair clone ready. Original repository left untouched."

exit 0
