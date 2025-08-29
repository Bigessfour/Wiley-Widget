# Trunk Flaky Tests Setup Script
# This script helps set up and validate Trunk Flaky Tests integration

param(
    [switch]$Validate,
    [switch]$TestUpload,
    [string]$OrgSlug,
    [string]$ApiToken
)

Write-Host "🔧 Trunk Flaky Tests Setup" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

if ($Validate) {
    Write-Host "`n📋 Validating test reports..." -ForegroundColor Yellow

    # Run tests to generate reports
    Write-Host "Running tests to generate JUnit XML..." -ForegroundColor Gray
    dotnet test --logger "junit;LogFileName=test-results.xml" --verbosity quiet

    if (Test-Path "TestResults/*/test-results.xml") {
        Write-Host "✅ Test results generated successfully" -ForegroundColor Green

        # Download and validate with Trunk CLI
        Write-Host "`n🔍 Validating with Trunk CLI..." -ForegroundColor Yellow
        curl -fsSLO --retry 3 https://trunk.io/releases/trunk
        chmod +x trunk

        ./trunk flakytests validate --junit-paths "TestResults/*/test-results.xml"
    } else {
        Write-Host "❌ No test results found. Check your test configuration." -ForegroundColor Red
        exit 1
    }
}

if ($TestUpload) {
    if (-not $OrgSlug -or -not $ApiToken) {
        Write-Host "❌ OrgSlug and ApiToken are required for test upload" -ForegroundColor Red
        Write-Host "Usage: .\setup-flaky-tests.ps1 -TestUpload -OrgSlug 'your-org' -ApiToken 'your-token'" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "`n📤 Testing upload to Trunk..." -ForegroundColor Yellow

    # Ensure we have test results
    if (-not (Test-Path "TestResults/*/test-results.xml")) {
        Write-Host "Generating test results first..." -ForegroundColor Gray
        dotnet test --logger "junit;LogFileName=test-results.xml" --verbosity quiet
    }

    # Download Trunk CLI and upload
    curl -fsSLO --retry 3 https://trunk.io/releases/trunk
    chmod +x trunk

    Write-Host "Uploading test results..." -ForegroundColor Gray
    $uploadResult = ./trunk flakytests upload --junit-paths "TestResults/*/test-results.xml" --org-url-slug $OrgSlug --token $ApiToken

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Test results uploaded successfully!" -ForegroundColor Green
        Write-Host "📊 Results will be processed by Trunk. Check your dashboard in ~1 hour." -ForegroundColor Cyan
    } else {
        Write-Host "❌ Upload failed. Check your organization slug and token." -ForegroundColor Red
        Write-Host "Error details: $uploadResult" -ForegroundColor Red
    }
}

if (-not $Validate -and -not $TestUpload) {
    Write-Host "`n📖 Usage:" -ForegroundColor Yellow
    Write-Host "  .\setup-flaky-tests.ps1 -Validate              # Validate test reports locally" -ForegroundColor White
    Write-Host "  .\setup-flaky-tests.ps1 -TestUpload -OrgSlug 'your-org' -ApiToken 'your-token'  # Test upload" -ForegroundColor White
    Write-Host "`n🔑 Setup Required:" -ForegroundColor Yellow
    Write-Host "  1. Get your Organization Slug and Token from: https://app.trunk.io/settings/manage/organization" -ForegroundColor White
    Write-Host "  2. Add to GitHub Secrets:" -ForegroundColor White
    Write-Host "     - TRUNK_ORG_SLUG: your-organization-slug" -ForegroundColor Gray
    Write-Host "     - TRUNK_API_TOKEN: your-api-token" -ForegroundColor Gray
    Write-Host "`n📝 Note: Multiple uploads are needed before Trunk can detect flaky tests accurately." -ForegroundColor Cyan
}
