param(
    [string]$ConnectionString = $env:CI_CONNECTION_STRING
)

if (![string]::IsNullOrWhiteSpace($env:CI_CONNECTION_STRING) -and -not $ConnectionString) {
    $ConnectionString = $env:CI_CONNECTION_STRING
}

if (-not $ConnectionString) {
    Write-Error "Connection string is required via parameter or CI_CONNECTION_STRING env var."
    exit 1
}

Write-Host "Running MigrationChecker against database..."
dotnet run --project tools/MigrationChecker --no-build -- --connection "$ConnectionString" --wait --wait-seconds 120

if ($LASTEXITCODE -ne 0) {
    Write-Error "MigrationChecker failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "MigrationChecker passed."
