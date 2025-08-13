param(
  [switch]$Publish,
  [string]$Config = 'Release',
  [switch]$SelfContained,
  [string]$Runtime = 'win-x64',
  [switch]$SkipLicenseCheck,
  [switch]$SkipCoverageCheck
)

$ErrorActionPreference = 'Stop'

Write-Host '== Pre-build setup ==' -ForegroundColor Cyan
$env:MSBUILDDEBUGPATH = Join-Path $env:TEMP 'MSBuildDebug'
if (-not (Test-Path $env:MSBUILDDEBUGPATH)) { New-Item -Path $env:MSBUILDDEBUGPATH -ItemType Directory -Force | Out-Null }
try {
  # Marker file ensures directory is never empty so CI artifact upload always has content
  "MSBuild debug logs marker (created $(Get-Date -Format o))" | Out-File -FilePath (Join-Path $env:MSBUILDDEBUGPATH 'marker.txt') -Encoding utf8 -Force
} catch { Write-Warning "Failed to create MSBuildDebug marker: $_" }
Write-Host "MSBuild logs will be written to $env:MSBUILDDEBUGPATH"

Write-Host '== Pre-clean (terminate lingering UI/test processes & remove stale dirs) =='
foreach ($name in 'WileyWidget','testhost','vstest.console') {
  try { Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "Stopping $($_.ProcessName) (pid=$($_.Id))"; $_ | Stop-Process -Force -ErrorAction SilentlyContinue } } catch { }
}
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue TestResults
New-Item -ItemType Directory -Path TestResults | Out-Null

if (-not $SkipLicenseCheck) {
  Write-Host '== Check Syncfusion License ==' -ForegroundColor Cyan
  try {
    $hasEnv = -not [string]::IsNullOrWhiteSpace($Env:SYNCFUSION_LICENSE_KEY)
    $licenseFile = Get-ChildItem -Path . -Filter 'license.key' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $hasEnv -and -not $licenseFile) {
      Write-Warning 'No SYNCFUSION_LICENSE_KEY env var or license.key file detected (proceeding, but Syncfusion controls may show trial dialog).'
    } else {
      if ($hasEnv) { Write-Host 'Env var SYNCFUSION_LICENSE_KEY detected.' -ForegroundColor Green }
      if ($licenseFile) { Write-Host "license.key found at $($licenseFile.FullName)" -ForegroundColor Green }
    }
  } catch { Write-Warning "License check failed: $_" }
}

Write-Host '== Restore ==' -ForegroundColor Cyan
dotnet restore ./WileyWidget.sln --no-cache
if ($LASTEXITCODE -ne 0) { Write-Error 'NuGet restore failed (check network / Syncfusion feed)'; exit 1 }

Write-Host "== Build ($Config) ==" -ForegroundColor Cyan
$binlogPath = Join-Path (Resolve-Path .) 'msbuild.binlog'
if (Test-Path $binlogPath) { Remove-Item -Force -ErrorAction SilentlyContinue $binlogPath }
dotnet build ./WileyWidget.sln -c $Config --no-restore /bl:$binlogPath
if ($LASTEXITCODE -ne 0) {
  Write-Error 'Build failed.'
  if (Test-Path $binlogPath) {
    try {
      Copy-Item -Path $binlogPath -Destination (Join-Path 'TestResults' 'msbuild.binlog') -Force
      Write-Host "Copied msbuild.binlog to TestResults/msbuild.binlog for diagnostics" -ForegroundColor Yellow
    } catch { Write-Warning "Failed to copy msbuild.binlog: $_" }
  } else { Write-Warning 'msbuild.binlog not found (build may have failed before log creation).' }
  exit 1
}
if (Test-Path $binlogPath) {
  try {
    Copy-Item -Path $binlogPath -Destination (Join-Path 'TestResults' 'msbuild.binlog') -Force
    Write-Host 'msbuild.binlog captured at TestResults/msbuild.binlog' -ForegroundColor Green
  } catch { Write-Warning "Failed to copy msbuild.binlog after successful build: $_" }
}

Write-Host '== Test ==' -ForegroundColor Cyan
$filter = $env:TEST_FILTER
if (-not [string]::IsNullOrWhiteSpace($filter)) { Write-Host "Using test filter: $filter" }

$maxRetries = 3; $attempt = 0

function Invoke-TestRun {
  param([string[]]$testArgs)
  dotnet test @testArgs
}

if ($filter) {
  # Single pass with user-provided filter over solution
  $testArgs = @('./WileyWidget.sln','-c', $Config,'--no-build','--no-parallel','--collect:"XPlat Code Coverage"','--results-directory','TestResults','--filter', $filter)
  do {
    if ($attempt -gt 0) { Write-Host "Retrying (attempt $($attempt+1)/$maxRetries)..." }
    Invoke-TestRun $testArgs
    if ($LASTEXITCODE -eq 0) { break }
    Start-Sleep -Seconds 2
    foreach ($name in 'WileyWidget','testhost','vstest.console') { try { Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue } catch { } }
    $attempt++
  } while ($attempt -lt $maxRetries)
} else {
  # Run unit tests first (with coverage) then optional UI smoke tests
  $runUi = ($Env:RUN_UI_TESTS -eq '1')
  Write-Host "Unit tests (coverage)" -ForegroundColor DarkCyan
  $unitArgs = @('./WileyWidget.Tests/WileyWidget.Tests.csproj','-c', $Config,'--no-build','--no-parallel','--collect:"XPlat Code Coverage"','--results-directory','TestResults')
  do {
    if ($attempt -gt 0) { Write-Host "Retrying unit tests (attempt $($attempt+1)/$maxRetries)..." }
    Invoke-TestRun $unitArgs
    if ($LASTEXITCODE -eq 0) { break }
    Start-Sleep -Seconds 2
    foreach ($name in 'WileyWidget','testhost','vstest.console') { try { Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue } catch { } }
    $attempt++
  } while ($attempt -lt $maxRetries)
  if ($LASTEXITCODE -ne 0) { Write-Error 'Unit tests failed after retries.'; exit 1 }

  if ($runUi) {
    Write-Host "UI smoke tests (Category=UiSmokeTests)" -ForegroundColor DarkCyan
    $attempt = 0
    $uiArgs = @('./WileyWidget.UiTests/WileyWidget.UiTests.csproj','-c', $Config,'--no-build','--no-parallel','--results-directory','TestResults','--filter','Category=UiSmokeTests')
    do {
      if ($attempt -gt 0) { Write-Host "Retrying UI tests (attempt $($attempt+1)/$maxRetries)..." }
      Invoke-TestRun $uiArgs
      if ($LASTEXITCODE -eq 0) { break }
      Start-Sleep -Seconds 3
      foreach ($name in 'WileyWidget','WileyWidget.UiTests','testhost','vstest.console') { try { Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue } catch { } }
      $attempt++
    } while ($attempt -lt $maxRetries)
    if ($LASTEXITCODE -ne 0) { Write-Error 'UI tests failed after retries.'; exit 1 }
  } else {
    Write-Host 'Skipping UI tests (set RUN_UI_TESTS=1 to include).' -ForegroundColor DarkYellow
  }
}

if ($LASTEXITCODE -ne 0) {
  Write-Error 'Tests failed after retries.'
  Write-Warning 'If MSB4166 occurred, inspect %TEMP%/MSBuildTemp or set $env:MSBUILDDEBUGPATH before running.'
  exit 1
}

Write-Host '== Coverage Processing ==' -ForegroundColor Cyan
if (-not $SkipCoverageCheck) {
  $coverageFiles = Get-ChildItem -Path TestResults -Filter coverage.cobertura.xml -Recurse -ErrorAction SilentlyContinue
  if ($coverageFiles) {
    $file = $coverageFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    try {
      [xml]$xml = Get-Content -Raw -Path $file.FullName
      $rate = [double]$xml.coverage.'line-rate'
      $percent = [math]::Round($rate * 100,2)
      $threshold = if ($Env:COVERAGE_MIN) { [double]$Env:COVERAGE_MIN } else { 0 }
      Write-Host "Line coverage: $percent% (file: $($file.FullName))" -ForegroundColor Green
      if ($threshold -gt 0 -and $percent -lt $threshold) {
        Write-Error "Coverage $percent% below threshold $threshold%"; exit 1
      }
    } catch { Write-Warning "Failed to parse coverage file: $_" }
  } else {
    Write-Warning 'No coverage.cobertura.xml files found.'
  }
}

if (Get-Command reportgenerator -ErrorAction SilentlyContinue) {
  try {
    reportgenerator -reports:TestResults/**/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html > $null 2>&1
    if (Test-Path CoverageReport/index.html) { Write-Host 'HTML coverage report: CoverageReport/index.html' -ForegroundColor Cyan }
  } catch { Write-Warning "ReportGenerator failed: $_" }
} else {
  Write-Host 'Install ReportGenerator for HTML coverage: dotnet tool install -g dotnet-reportgenerator-globaltool' -ForegroundColor DarkYellow
}

if ($Publish) {
  Write-Host '== Publish ==' -ForegroundColor Cyan
  $out = Join-Path -Path (Resolve-Path .) -ChildPath 'publish'
  $sc = $SelfContained ? '/p:SelfContained=true' : '/p:SelfContained=false'
  $rid = $SelfContained ? "-r $Runtime" : ''
  dotnet publish ./WileyWidget/WileyWidget.csproj -c $Config -o $out $rid /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false $sc
  if ($LASTEXITCODE -ne 0) { Write-Error 'Publish failed.'; exit 1 }
  Write-Host "Published to $out" -ForegroundColor Green
  if ($SelfContained) { Write-Host "Self-contained runtime: $Runtime" }
}

Write-Host '== Archive MSBuild debug logs ==' -ForegroundColor Cyan
try {
  $dbg = $env:MSBUILDDEBUGPATH
  if (-not [string]::IsNullOrWhiteSpace($dbg) -and (Test-Path $dbg)) {
    $dest = Join-Path 'TestResults' 'MSBuildDebug'
    if (Test-Path $dest) { Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $dest }
    Copy-Item -Recurse -Force -Path $dbg -Destination $dest
    $zipPath = Join-Path 'TestResults' 'MSBuildDebug.zip'
    if (Test-Path $zipPath) { Remove-Item -Force -ErrorAction SilentlyContinue $zipPath }
    Compress-Archive -Path (Join-Path $dbg '*') -DestinationPath $zipPath -Force -ErrorAction SilentlyContinue
    Write-Host "Archived MSBuild debug logs to $dest and $zipPath" -ForegroundColor Green
  } else {
    Write-Host 'No MSBuild debug directory present (set MSBUILDDEBUGPATH before build to capture).' -ForegroundColor DarkYellow
  }
} catch { Write-Warning "Failed to archive MSBuild debug logs: $_" }

Write-Host 'Done.' -ForegroundColor Green
