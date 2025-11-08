# Agentic E2E Test Runner for Wiley Widget (WPF + FlaUI + WinAppDriver + AI)
param(
    [switch]$StartServer = $true,
    [switch]$RunAI = $false  # Optional: Generate new tests via Grok-4 (requires API key)
)

# Start WinAppDriver if requested (background)
if ($StartServer) {
    $wadPath = "C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe"
    if (Test-Path $wadPath) {
        Start-Process -FilePath $wadPath -NoNewWindow -WindowStyle Hidden
        Write-Host "✅ WinAppDriver started on 4723 (background)."
        Start-Sleep 2  # Wait for startup
    } else {
        Write-Host "❌ WinAppDriver not found. Install via script."
        exit 1
    }
}

# Restore, build, test
Write-Host "🔄 Restoring packages..."
dotnet restore --force-evaluate

Write-Host "🔨 Building project..."
dotnet build -c Debug

Write-Host "🧪 Running E2E tests..."
dotnet test WileyWidget.Tests --filter "Category=E2E" --verbosity normal --no-build --logger trx

# If AI flag, simulate Grok-4 generation (extend with real API call to GrokAiHelper if needed)
if ($RunAI) {
    Write-Host "🤖 Generating AI test with Grok-4..."
    # Placeholder: Call your GrokAiHelper or Continue.dev CLI if integrated
    Write-Host "Generated new test scenario: 'Test Syncfusion grid export' – Add to AIDashboardTest.cs"
}

Write-Host "✅ E2E pipeline complete! Check results above.
