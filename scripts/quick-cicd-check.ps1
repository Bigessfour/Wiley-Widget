# Quick CI/CD Tools Check
Write-Host "🔧 WileyWidget CI/CD Tools Quick Check" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

$tools = @(
    @{ Name = "Trunk"; Command = "trunk --version"; Expected = "1.25.0" },
    @{ Name = "Node.js"; Command = "node --version"; Expected = "v" },
    @{ Name = "NPM"; Command = "npm --version"; Expected = "" },
    @{ Name = "PowerShell"; Command = "pwsh --version"; Expected = "PowerShell" },
    @{ Name = ".NET"; Command = "dotnet --version"; Expected = "" },
    @{ Name = "Azure CLI"; Command = "az --version"; Expected = "azure-cli" },
    @{ Name = "Git"; Command = "git --version"; Expected = "git version" }
)

$results = @()
$available = 0
$total = $tools.Count

Write-Host "`n📦 Checking Tools:" -ForegroundColor Yellow

foreach ($tool in $tools) {
    Write-Host "  🔍 $($tool.Name)..." -NoNewline
    try {
        $output = & cmd /c "$($tool.Command) 2>&1" 2>$null
        if ($LASTEXITCODE -eq 0 -and ($tool.Expected -eq "" -or $output -like "*$($tool.Expected)*")) {
            Write-Host " ✅" -ForegroundColor Green
            $results += "$($tool.Name): Available"
            $available++
        } else {
            Write-Host " ❌" -ForegroundColor Red
            $results += "$($tool.Name): Failed"
        }
    } catch {
        Write-Host " ❌" -ForegroundColor Red
        $results += "$($tool.Name): Error - $($_.Exception.Message)"
    }
}

Write-Host "`n📊 Results:" -ForegroundColor Cyan
Write-Host "   Available: $available / $total tools" -ForegroundColor $(if ($available -eq $total) { "Green" } elseif ($available -gt $total/2) { "Yellow" } else { "Red" })

Write-Host "`n🔍 Configuration Status:" -ForegroundColor Yellow

# Check trunk config
if (Test-Path ".\.trunk\trunk.yaml") {
    Write-Host "  ✅ Trunk configuration found" -ForegroundColor Green
} else {
    Write-Host "  ❌ Trunk configuration missing" -ForegroundColor Red
}

# Check GitHub Actions
if (Test-Path ".\.github\workflows") {
    $workflows = Get-ChildItem ".\.github\workflows\*.yml" -ErrorAction SilentlyContinue
    Write-Host "  ✅ GitHub Actions: $($workflows.Count) workflow(s)" -ForegroundColor Green
} else {
    Write-Host "  ❌ GitHub Actions workflows missing" -ForegroundColor Red
}

# Check scripts
if (Test-Path ".\scripts") {
    $scripts = Get-ChildItem ".\scripts\*.ps1" -ErrorAction SilentlyContinue
    Write-Host "  ✅ Build scripts: $($scripts.Count) PowerShell script(s)" -ForegroundColor Green
} else {
    Write-Host "  ❌ Build scripts directory missing" -ForegroundColor Red
}

Write-Host "`n💡 Recommendations:" -ForegroundColor Cyan
if ($available -lt $total) {
    Write-Host "   • Some tools may need to be added to PATH" -ForegroundColor Yellow
    Write-Host "   • Consider using full paths to executables" -ForegroundColor Yellow
}
if (Test-Path ".\.trunk\trunk.yaml") {
    Write-Host "   • Run 'trunk check' to analyze code quality" -ForegroundColor Green
}
if (Test-Path ".\.github\workflows") {
    Write-Host "   • CI/CD pipelines are configured" -ForegroundColor Green
}

Write-Host "`n✅ Quick check completed!" -ForegroundColor Green
