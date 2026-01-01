# Run UI E2E tests with interactive env
$env:WILEYWIDGET_UI_TESTS = 'true'
$env:RUNNER_LABELS = 'self-hosted'

Write-Host "Building WinForms project..."
dotnet build 'c:\Users\biges\Desktop\Wiley-Widget\src\WileyWidget.WinForms\WileyWidget.WinForms.csproj' --configuration Debug

Write-Host "Running UI E2E tests (Category=UI)..."
# Run tests and produce TRX in TestResults
dotnet test 'c:\Users\biges\Desktop\Wiley-Widget\tests\WileyWidget.WinForms.E2ETests\WileyWidget.WinForms.E2ETests.csproj' --logger trx --logger 'console;verbosity=detailed' --results-directory 'c:\Users\biges\Desktop\Wiley-Widget\TestResults' --filter "Category=UI"
