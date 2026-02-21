# Run integration tests per test project, produce TRX files, and capture diag/blame.
Param(
    [int]$TimeoutSeconds = 900,
    [string]$ProjectPath = "",
    [string]$TestFilter = "Category=Integration"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$overallExitCode = 0

# Initialize the global overall exit code so later logic can safely update it
if (-not (Get-Variable -Name overallExitCode -Scope Global -ErrorAction SilentlyContinue)) {
    Set-Variable -Name overallExitCode -Scope Global -Value 0
}

# Ensure working directory is repository root (script location's parent)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir '..')
Set-Location $repoRoot

# Determine host platform once (used to skip Windows-only tests on non-Windows hosts)
$isHostWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)

Write-Host "Scanning ./tests for test projects..."
$testProjects = Get-ChildItem -Path .\tests -Filter *.csproj -Recurse -ErrorAction SilentlyContinue
if ($ProjectPath -and $ProjectPath -ne "") {
    Write-Host "ProjectPath provided: $ProjectPath. Resolving..."
    $resolved = $null
    # Try direct path
    if (Test-Path $ProjectPath) { $resolved = Resolve-Path $ProjectPath -ErrorAction SilentlyContinue }
    # Try workspace-relative
    if (-not $resolved) {
        $candidate = Join-Path (Get-Location) $ProjectPath
        if (Test-Path $candidate) { $resolved = Resolve-Path $candidate -ErrorAction SilentlyContinue }
    }

    if ($resolved) {
        $item = Get-Item $resolved.Path
        if ($item.PSIsContainer) {
            $csproj = Get-ChildItem -Path $item.FullName -Filter *.csproj -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($csproj) { $testProjects = @($csproj) } else { Write-Error "No csproj found in folder $($item.FullName)"; exit 1 }
        } else {
            $testProjects = @($item)
        }
    } else {
        # If user provided a folder under tests (relative), try to find csproj inside
        $candidateDir = Join-Path (Get-Location) $ProjectPath
        if (Test-Path $candidateDir -PathType Container) {
            $csproj = Get-ChildItem -Path $candidateDir -Filter *.csproj -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($csproj) { $testProjects = @($csproj) } else { Write-Error "No csproj found in folder $ProjectPath"; exit 1 }
        } else {
            Write-Error "ProjectPath '$ProjectPath' not found."; exit 1
        }
    }
}

if (-not $testProjects) {
    Write-Host "No test projects found under ./tests. Exiting."
    exit 0
}

# If host is not Windows and the requested project(s) appear to be WinForms/Windows-only,
# skip running them to avoid platform/runtime failures.
if (-not $isHostWindows) {
    $winUi = $false
    foreach ($p in $testProjects) {
        if ($p.FullName -match 'WinForms.Tests') { $winUi = $true; break }
        try {
            $content = Get-Content -Raw $p.FullName -ErrorAction SilentlyContinue
            if ($content -match '<UseWindowsForms>\s*true\s*</UseWindowsForms>') { $winUi = $true; break }
        } catch { }
    }
    if ($winUi) {
        Write-Host "Host is not Windows; detected Windows UI test projects. Skipping integration test run on this host."
        exit 0
    }
}

foreach ($proj in $testProjects) {
    $projPath = $proj.FullName
    $projName = [IO.Path]::GetFileNameWithoutExtension($proj.Name)
    # Place results inside the test project's own TestResults folder (not repo root)
    $projDir = Split-Path -Parent $projPath
    $resultsDir = Join-Path $projDir "TestResults"
    if (-not (Test-Path $resultsDir)) { New-Item -ItemType Directory -Path $resultsDir | Out-Null }
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $diagLog = Join-Path $resultsDir "TestDiag_${projName}_$timestamp.log"

    Write-Host "\n=== Running tests for $projName ==="
    Write-Host "Project: $projPath"
    Write-Host "Results: $resultsDir"
    Write-Host "Filter: $TestFilter"

    # If this project targets a Windows-specific framework (eg. net*-windows*) and we're
    # not running on Windows, skip running it to avoid platform/runtime failures.
    try {
        $projXml = [xml](Get-Content -Raw $projPath -ErrorAction Stop)
        $tf = $projXml.Project.PropertyGroup.TargetFramework -join ';'
        $tfs = $projXml.Project.PropertyGroup.TargetFrameworks -join ';'
        $combined = "$tf;$tfs"
        $isHostWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
        if ($combined -match '-windows' -and -not $isHostWindows) {
            Write-Host ("Skipping {0}: targets Windows desktop ({1}) and host is not Windows." -f $projName, $combined)
            continue
        }

        # Additional heuristic: if project uses Windows Forms, skip on non-Windows hosts
        $useWinForms = $projXml.Project.PropertyGroup.UseWindowsForms -join ';'

        # Debug: report detection values
        Write-Host ("Debug: HostIsWindows={0}; TargetFrameworks={1}; UseWindowsForms={2}" -f $isHostWindows, $combined, ($useWinForms -join ';'))
        if ($useWinForms -match 'true' -and -not $isHostWindows) {
            Write-Host ("Skipping {0}: uses Windows Forms and host is not Windows." -f $projName)
            continue
        }

        # Fallback quick-skip: if the project path indicates a WinForms test project, skip on non-Windows
        if ($projPath -match 'WinForms.Tests' -and -not $isHostWindows) {
            Write-Host ("Skipping {0}: project path indicates WinForms tests and host is not Windows." -f $projName)
            continue
        }
    } catch {
        # If we can't parse the csproj for any reason, fall through and attempt the test run.
    }

    # Build dotnet test argument list (no manual quoting)
    $logFileName = "$projName.trx"
    $argList = @(
        'test',
        $projPath,
        '--filter',
        $TestFilter,
        '--logger',
        "trx;LogFileName=$logFileName",
        '--results-directory',
        $resultsDir,
        '--blame',
        '--diag',
        $diagLog,
        '-v',
        'minimal'
    )

    $stdoutPath = Join-Path $resultsDir "job-output-stdout.txt"
    $stderrPath = Join-Path $resultsDir "job-output-stderr.txt"
    $combinedPath = Join-Path $resultsDir "job-output.txt"

    Write-Host "Running: dotnet $($argList -join ' ')"

    $proc = Start-Process -FilePath 'dotnet' -ArgumentList $argList -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -NoNewWindow -PassThru

    $finished = $proc.WaitForExit($TimeoutSeconds * 1000)

    if (-not $finished) {
        Write-Warning "Timed out after $TimeoutSeconds seconds for $projName. Attempting to terminate process tree."
        try {
            Start-Process -FilePath 'taskkill' -ArgumentList "/PID $($proc.Id) /T /F" -NoNewWindow -Wait -ErrorAction SilentlyContinue | Out-Null
        } catch {
            try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch { }
        }
        Start-Sleep -Seconds 2
        if (-not $proc.HasExited) { try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch { } }
        $exitCode = 124
        $timedOut = $true
    } else {
        $proc.WaitForExit()
        $exitCode = $proc.ExitCode
        $timedOut = $false
    }

    $stdout = Get-Content -Raw -ErrorAction SilentlyContinue $stdoutPath
    $stderr = Get-Content -Raw -ErrorAction SilentlyContinue $stderrPath

    if (-not $stdout) { $stdout = "" }
    if (-not $stderr) { $stderr = "" }

    @('===== STDOUT =====', $stdout, '===== STDERR =====', $stderr) -join "`n" | Out-File -FilePath $combinedPath -Encoding utf8

    # Find most recent TRX file in results dir
    $trxFile = Get-ChildItem -Path $resultsDir -Filter *.trx -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($trxFile) { Write-Host "TRX: $($trxFile.FullName)" } else { Write-Warning "No TRX file found in $resultsDir" }

    Write-Host "---- Last 50 lines of test output ----"
    $stdout -split "`n" | Select-Object -Last 50 | ForEach-Object { Write-Host $_ }

    if ($exitCode -ne 0) { Write-Host "dotnet test exit code: $exitCode" }

    if ($exitCode -ne 0) { $overallExitCode = $exitCode }

    Write-Host "Finished project $projName; check $resultsDir for TRX/diag/job-output.txt"
}

Write-Host "\nAll done. Summary: check ./TestResults for per-project outputs."
exit $overallExitCode
