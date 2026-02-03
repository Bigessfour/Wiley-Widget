param(
    [string]$Project = "tests/WileyWidget.WinForms.Tests/WileyWidget.WinForms.Tests.csproj"
)

$projectPath = $Project

Write-Host "Listing tests in $projectPath..."
$raw = & dotnet test --no-build --list-tests $projectPath -v minimal 2>&1
$start = $false
$tests = @()
foreach ($line in $raw) {
    if ($line -match 'The following Tests are available') { $start = $true; continue }
    if (-not $start) { continue }
    $trim = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trim)) { continue }
    if ($trim -match '^WileyWidget') {
        $tests += $trim
    }
}

if ($tests.Count -eq 0) {
    Write-Host "No tests found."
    exit 0
}

Write-Host "Found $($tests.Count) tests."
$index = 0
foreach ($test in $tests) {
    $index++
    Write-Host "[$index/$($tests.Count)] Running: $test"
    & dotnet test --no-build --logger trx --results-directory TestResults $projectPath --filter "FullyQualifiedName~$test" -v minimal
    $exit = $LASTEXITCODE
    if ($exit -ne 0) {
        Write-Host "TEST FAILED: $test (exit code $exit)"
        exit $exit
    } else {
        Write-Host "OK: $test"
    }
}

Write-Host "All tests passed."
exit 0
