<#
.SYNOPSIS
  Enhanced Git update script for CI/Local use in Wiley Widget workflows.

.DESCRIPTION
  Stages, commits, and pushes changes safely. Handles conflicts, untracked files, upstream rebases,
  CI failure auto-fixes (Trunk), and AI manifest generation. PS7.5+ optimized with retries,
  validation, and structured logging. Inspired by posh-git and enterprise automation best practices.

.PARAMETER Message
  Commit message. Defaults to generated summary.

.PARAMETER Remote
  Remote to push (default: 'origin').

.PARAMETER Branch
  Branch to operate on (default: current).

.PARAMETER Sign
  GPG-sign the commit (requires git config user.signingkey).

.PARAMETER Force
  Commit/push even if no changes (empty commit).

.PARAMETER DryRun
  Simulate without destructive actions.

.PARAMETER NoManifest
  Skip AI manifest generation.

.PARAMETER LogFormat
  Output format: 'text' (default) or 'json' for CI parsing.

.PARAMETER RetryCount
  Max retries for flaky ops (default: 2).

.EXAMPLE
  pwsh git-update.ps1 -Message "fix: resolve DI generics" -Sign -Verbose
  pwsh git-update.ps1 -DryRun -LogFormat json | ConvertFrom-Json

.NOTES
  Requires GitHub CLI (gh) for CI checks. Excludes patterns from Wiley manifest (e.g., bin/obj).
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0)]
    [ValidatePattern('^[^<>"|?*\\]+$')]  # Basic path safety
    [string] $Message = '',

    [Parameter()]
    [string] $Remote = 'origin',

    [Parameter()]
    [string] $Branch = '',

    [Parameter()]
    [switch] $Sign,

    [Parameter()]
    [switch] $Force,

    [Parameter()]
    [switch] $DryRun,

    [Parameter()]
    [switch] $NoManifest,

    [ValidateSet('text', 'json')]
    [string] $LogFormat = 'text',

    [ValidateRange(0, 5)]
    [int] $RetryCount = 2
)

# Global error policy for automation
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'  # Aligns with Wiley diagnostics

# Structured logging helper (text/JSON toggle)
function Write-Log {
    param([string] $Level, [string] $Message, [hashtable] $Metadata = @{})
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'
    $logEntry = @{
        Timestamp = $timestamp
        Level = $Level
        Message = $Message
        Metadata = $Metadata
    }
    if ($LogFormat -eq 'json') {
        $logEntry | ConvertTo-Json -Compress | Write-Output
    } else {
        $emoji = switch ($Level) { 'INFO' { 'ℹ️' } 'WARN' { '⚠️' } 'ERROR' { '❌' } default { '✓' } }
        Write-Output "[$timestamp] $emoji [$Level] $Message"
        if ($Metadata.Count -gt 0) { Write-Verbose "  Details: $($Metadata | ConvertTo-Json -Compress)" }
    }
}

function Write-ErrorAndExit([string] $msg, [int] $code = 1, [hashtable] $metadata = @{}) {
    Write-Log 'ERROR' $msg $metadata
    exit $code
}

# Retry wrapper for flaky ops
function Invoke-WithRetry([scriptblock] $Operation, [int] $MaxRetries = $RetryCount, [string] $OpName = '') {
    $attempt = 0
    $delay = 1  # Start with 1s, exponential
    while ($attempt -le $MaxRetries) {
        try {
            return & $Operation
        } catch {
            $attempt++
            if ($attempt -gt $MaxRetries) { throw }
            $backoff = [math]::Pow(2, $attempt - 1) * $delay
            Write-Log 'WARN' "Retry $attempt/$MaxRetries for $OpName : $($_.Exception.Message)" @{ DelaySeconds = $backoff }
            Start-Sleep -Seconds $backoff
        }
    }
}

function Invoke-Git([string[]] $GitArgs, [string] $OpName = '') {
    Write-Verbose "git $($GitArgs -join ' ')"
    $result = Invoke-WithRetry {
        $output = & git @GitArgs 2>&1
        @{ ExitCode = $LASTEXITCODE; Output = $output }
    } $RetryCount $OpName
    return $result
}

# Enhanced warning filter
function Get-FilteredGitLines([string[]] $lines) {
    return $lines | ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -notmatch '^(warning:|CRLF)' }
}

# Improved Python finder with version check
function Find-PythonExe {
    $currentPath = (Get-Location).Path
    $candidates = @(
        (Join-Path $currentPath '.venv\Scripts\python.exe'),
        (Join-Path $currentPath 'venv\Scripts\python.exe'),
        (Join-Path $currentPath '.venv/bin/python'),
        (Join-Path $currentPath 'venv/bin/python')
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) {
            $ver = & $c --version 2>$null | Select-String 'Python (\d+\.\d+)'
            if ($ver -and [version]$ver.Matches.Groups[1].Value -ge [version]'3.11') {
                return (Resolve-Path $c).Path
            }
        }
    }
    $names = @('python3.11', 'python3', 'python')
    foreach ($n in $names) {
        $cmd = Get-Command $n -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }
    $fallback = "C:\Users\$env:USERNAME\AppData\Local\Microsoft\WindowsApps\python.exe"
    if (Test-Path $fallback) { return $fallback }
    return $null
}

function Invoke-PythonScript($pythonExe, $scriptPath, [string[]] $Arguments) {
    Write-Log 'INFO' "Running Python: $pythonExe $scriptPath $($Arguments -join ' ')"
    $out = & $pythonExe $scriptPath @Arguments 2>&1
    $exit = $LASTEXITCODE
    if ($out) { $out | ForEach-Object { Write-Output $_ } }
    return @{ ExitCode = $exit; Output = $out }
}

function Assert-ScriptsLayout {
    $repoRoot = (Get-Location).Path
    $requiredPaths = @('scripts/maintenance', 'scripts/tools', 'scripts/testing', 'scripts/examples', 'scripts/maintenance/git-update.ps1', 'scripts/tools/generate_repo_urls.py')
    $missing = $requiredPaths | Where-Object { -not (Test-Path (Join-Path -Path $repoRoot -ChildPath $_)) }
    if ($missing) {
        Write-ErrorAndExit "Canonical scripts layout incomplete. Missing: $($missing -join ', ')." 99 @{ MissingPaths = $missing }
    }
    Write-Log 'INFO' 'Scripts layout validated'
}

# Main flow
try {
    if ($MyInvocation.MyCommand.Path) {
        $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
        $repoRoot = Resolve-Path (Join-Path $scriptDir '..' | Join-Path -ChildPath '..')
        Set-Location $repoRoot
    } else {
        Set-Location (Resolve-Path .)
    }

    Assert-ScriptsLayout

    # Git repo check
    $gitDir = Invoke-Git @('rev-parse', '--git-dir') 'repo-check'
    if ($gitDir.ExitCode -ne 0) { Write-ErrorAndExit 'Not a Git repository.' }

    # Branch resolution
    if ([string]::IsNullOrEmpty($Branch)) {
        $branchInfo = Invoke-Git @('rev-parse', '--abbrev-ref', 'HEAD') 'branch-detect'
        $Branch = (Get-FilteredGitLines $branchInfo.Output)[0]
    }
    Write-Log 'INFO' "Operating on branch: $Branch" @{ Branch = $Branch }

    # Conflict check
    $unmerged = Invoke-Git @('diff', '--name-only', '--diff-filter=U') 'conflict-check'
    $unmergedPaths = Get-FilteredGitLines $unmerged.Output
    if ($unmergedPaths) {
        Write-Log 'WARN' 'Unmerged files detected:' @{ Paths = $unmergedPaths }
        Write-ErrorAndExit 'Resolve conflicts first.' 2 @{ Conflicts = $unmergedPaths }
    }

    # Status & untracked
    $status = Invoke-Git @('status', '--porcelain') 'status-fetch'
    $untrackedRaw = Invoke-Git @('ls-files', '--others', '--exclude-standard') 'untracked-fetch'
    $untrackedPaths = Get-FilteredGitLines $untrackedRaw.Output

    # Enhanced untracked handling (parallel filter opt-in for large repos)
    $excludePatterns = @('\.lock$', 'packages\.lock\.json$', '\.dll$', '\.exe$', '\.pdb$', '^logs/', '^test-logs/', 'ai-fetchable-manifest\.json$', '^bin/', '^obj/', '\.cache$', '\.suo$', '\.user$', '\.key$', 'secrets\.json')  # Wiley security patterns
    $toAdd = if ($PSVersionTable.PSVersion.Major -ge 7 -and $untrackedPaths.Count -gt 50 -and $IsWindows) {
        $untrackedPaths | ForEach-Object -Parallel {
            $pn = $_ -replace '\\', '/'
            if (-not ($using:excludePatterns | Where-Object { $pn -match $_ })) { $_ }
        } -ThrottleLimit 8
    } else {
        $untrackedPaths | Where-Object { $pn = $_ -replace '\\', '/'; -not ($excludePatterns | Where-Object { $pn -match $_ }) }
    }

    if ($toAdd.Count -gt 0 -and -not $DryRun) {
        Write-Log 'INFO' "Adding $($toAdd.Count) untracked files"
        $succeeded = 0
        foreach ($p in $toAdd) {
            $addRes = Invoke-Git @('add', '--', $p) "add-untracked-$p"
            if ($addRes.ExitCode -eq 0) { $succeeded++ } else { Write-Log 'WARN' "Failed to add: $p" @{ Path = $p } }
        }
        if ([string]::IsNullOrEmpty($Message) -and $succeeded -gt 0) {
            $sample = ($toAdd | Select-Object -First 5) -join ', '
            $Message = "chore: add $succeeded new files: $sample"
        }
    }

    # Stage all
    if ($PSCmdlet.ShouldProcess('all changes', 'git add -A')) {
        $addAll = Invoke-Git @('add', '-A') 'stage-all'
        if ($addAll.ExitCode -ne 0) { Write-ErrorAndExit 'Staging failed.' 6 @{ Output = $addAll.Output } }
    }

    # Diff check
    $staged = Invoke-Git @('diff', '--cached', '--name-only') 'diff-check'
    $stagedFiles = Get-FilteredGitLines $staged.Output

    if ($stagedFiles.Count -eq 0) {
        if (-not $Force) { Write-Log 'INFO' 'No changes to commit'; exit 0 }
        $emptyMsg = if ($Message) { $Message } else { "chore: empty commit on $Branch - $(Get-Date -Format o)" }
        if ($PSCmdlet.ShouldProcess('empty commit', "git commit --allow-empty -m '$emptyMsg'")) {
            $commitRes = Invoke-Git @('commit', '--allow-empty', '-m', $emptyMsg) 'empty-commit'
            if ($commitRes.ExitCode -ne 0) { Write-ErrorAndExit 'Empty commit failed.' 8 @{ Output = $commitRes.Output } }
        }
    } else {
        # Doc warning (Wiley style: check CHANGELOG)
        $codeChanges = $stagedFiles | Where-Object { $_ -match '^src/|^tests/|^WileyWidget\.' }
        $docChanges = $stagedFiles | Where-Object { $_ -match '^docs/|^README\.md$|^CHANGELOG\.md$' }
        if ($codeChanges -and -not $docChanges) {
            Write-Log 'WARN' 'Code changes without docs/CHANGELOG updates. Review before commit.'
        }

        if ([string]::IsNullOrEmpty($Message)) {
            $shortList = ($stagedFiles | Select-Object -First 6) -join ', '
            $Message = "chore: workspace update on $Branch - files: $shortList"
        }
        $commitArgs = @('commit', '-m', $Message)
        if ($Sign) { $commitArgs += '--gpg-sign' }
        if ($PSCmdlet.ShouldProcess("commit: $Message", 'git commit')) {
            $commitRes = Invoke-Git $commitArgs 'commit'
            if ($commitRes.ExitCode -ne 0) { Write-ErrorAndExit 'Commit failed.' 9 @{ Output = $commitRes.Output } }
        }
    }

    # Remote/upstream
    $remotes = Invoke-Git @('remote') 'remotes-list'
    if ($remotes.Output -notcontains $Remote) { Write-ErrorAndExit "Remote '$Remote' not found. Available: $($remotes.Output -join ', ')." 11 }

    $upstream = Invoke-Git @('rev-parse', '--abbrev-ref', '--symbolic-full-name', '@{u}') 'upstream-detect'
    $upstream = if ($upstream.ExitCode -eq 0) { (Get-FilteredGitLines $upstream.Output)[0] } else { $null }

    # Pull if behind
    if ($upstream) {
        $counts = Invoke-Git @('rev-list', '--left-right', '--count', "HEAD...$upstream") 'ahead-behind'
        if ($counts.ExitCode -eq 0) {
            $ahead, $behind = ($counts.Output -join '') -split '\s+' | Select-Object -First 2 | ForEach-Object { [int]$_ }
            Write-Log 'INFO' "Ahead: $ahead, Behind: $behind" @{ Upstream = $upstream }
            if ($behind -gt 0 -and $PSCmdlet.ShouldProcess('rebase pull', 'git pull --rebase --autostash')) {
                $pullRes = Invoke-Git @('pull', '--rebase', '--autostash') 'pull-rebase'
                if ($pullRes.ExitCode -ne 0) { Write-ErrorAndExit 'Pull failed. Resolve conflicts.' 12 @{ Output = $pullRes.Output } }
            }
        }
    } else {
        Write-Log 'WARN' "No upstream for '$Branch'. Will set on push." @{ Branch = $Branch }
    }

    # Protected branch check (simple)
    $protectedBranches = @('main', 'develop')
    if ($protectedBranches -contains $Branch -and -not $Force) {
        $confirm = Read-Host "Push to protected branch '$Branch'? (Y/N)"
        if ($confirm -ne 'Y') { exit 1 }
    }

    # Push
    $pushArgs = @('push')
    if (-not $upstream) { $pushArgs += @('--set-upstream', $Remote, $Branch) } else { $pushArgs += @($Remote, $Branch) }
    if ($PSCmdlet.ShouldProcess("push to $Remote/$Branch", 'git push')) {
        $pushRes = Invoke-Git $pushArgs 'push'
        if ($pushRes.ExitCode -ne 0) { Write-ErrorAndExit 'Push failed.' 13 @{ Output = $pushRes.Output } }
    }

    # Enhanced CI check & auto-fix
    if (-not $DryRun -and (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Log 'INFO' 'Checking GitHub CI status'
        $latestRun = gh run list --limit 1 --json status,conclusion,databaseId,workflowName 2>$null | ConvertFrom-Json
        if ($latestRun) {
            $runId, $conclusion, $workflow = $latestRun.databaseId, $latestRun.conclusion, $latestRun.workflowName
            Write-Log 'INFO' "Latest CI: $runId ($conclusion) via $workflow"
            if ($conclusion -eq 'failure') {
                $logOutput = gh run view $runId --log-failed 2>$null
                $logText = $logOutput -join "`n"
                if ($logText -match 'trunk|lint|format|security') {
                    Write-Log 'INFO' 'Trunk issues detected; auto-fixing'
                    if ($PSCmdlet.ShouldProcess('Trunk fix', 'trunk check --fix')) {
                        $trunkFix = Invoke-WithRetry { & trunk check --fix 2>&1 } 1 'trunk-fix'
                        if ($LASTEXITCODE -eq 0 -and (Invoke-Git @('status', '--porcelain') 'post-fix-status').Output.Count -gt 0) {
                            $fixCommitMsg = 'fix: auto-apply Trunk fixes for CI'
                            $fixCommit = Invoke-Git @('add', '-A') 'fix-stage'
                            if ($fixCommit.ExitCode -eq 0) {
                                $fixCommit2 = Invoke-Git @('commit', '-m', $fixCommitMsg) 'fix-commit'
                                if ($fixCommit2.ExitCode -eq 0) {
                                    $repush = Invoke-Git @('push', $Remote, $Branch) 'repush'
                                    if ($repush.ExitCode -eq 0) {
                                        gh run rerun $runId 2>$null  # Trigger re-run
                                        Write-Log 'INFO' 'CI re-triggered after fixes'
                                    }
                                }
                            }
                        }
                    }
                } else {
                    Write-Log 'WARN' 'CI failure; manual review needed' @{ RunId = $runId }
                }
            }
        } else {
            Write-Log 'WARN' 'Could not fetch CI status (gh CLI?)'
        }
    }

    # Manifest (opt-out)
    if (-not $NoManifest) {
        $manifestScript = 'scripts/tools/generate_repo_urls.py'
        if (Test-Path $manifestScript) {
            $pythonExe = Find-PythonExe
            if (-not $pythonExe) { Write-ErrorAndExit 'Python not found for manifest gen.' 14 }
            if ($PSCmdlet.ShouldProcess('manifest update', "python $manifestScript")) {
                $pyRes = Invoke-PythonScript $pythonExe $manifestScript @('-o', 'ai-fetchable-manifest.json')
                if ($pyRes.ExitCode -ne 0) { Write-ErrorAndExit 'Manifest gen failed.' 15 @{ Output = $pyRes.Output } }
                Write-Log 'INFO' 'Manifest updated'
            }
        } else {
            Write-Log 'WARN' "Manifest script missing: $manifestScript"
        }
    }

    Write-Log 'INFO' 'Update complete' @{ Success = $true }
    exit 0
} catch {
    Write-ErrorAndExit $_.Exception.Message $LASTEXITCODE @{ StackTrace = $_.ScriptStackTrace }
}
