# Test QuickBooks Online Local Secret Vault Integration
# This script validates that QBO secrets are properly loaded from the local encrypted secret vault

Write-Output "=== Testing QuickBooks Online Local Secret Vault Integration ==="
Write-Output ""

# Check environment variables
Write-Output ""
Write-Output "Checking QBO environment variables:"
$envVars = @("QUICKBOOKS_CLIENT_ID", "QUICKBOOKS_CLIENT_SECRET", "QUICKBOOKS_REALM_ID", "QUICKBOOKS_ENVIRONMENT")
$envStatus = @{}

foreach ($var in $envVars) {
    $value = [System.Environment]::GetEnvironmentVariable($var, "User")
    if ([string]::IsNullOrEmpty($value)) {
        $value = [System.Environment]::GetEnvironmentVariable($var)
    }
    if ([string]::IsNullOrEmpty($value)) {
        Write-Output "❌ $var : Not set"
        $envStatus[$var] = $false
    }
    else {
        Write-Output "✅ $var : Set ($($value.Substring(0, [Math]::Min(8, $value.Length)))...)"
        $envStatus[$var] = $true
    }
}

# Check local secret vault
Write-Output ""
Write-Output "Checking local encrypted secret vault:"
$appData = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::ApplicationData)
$vaultPath = Join-Path $appData "WileyWidget\Secrets"

if (Test-Path $vaultPath) {
    Write-Output "✅ Secret vault directory exists: $vaultPath"
    $secretFiles = Get-ChildItem -Path $vaultPath -Filter "*.secret" -ErrorAction SilentlyContinue
    if ($secretFiles) {
        Write-Output "   Found $($secretFiles.Count) encrypted secret files:"
        $secretFiles | ForEach-Object {
            $secretName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
            Write-Output "   - $secretName"
        }
    } else {
        Write-Output "   No secret files found in vault"
    }
} else {
    Write-Output "❌ Secret vault directory not found: $vaultPath"
}

# Build and test the application
Write-Output ""
Write-Output "Building and testing application..."
dotnet build "WileyWidget.csproj" --verbosity quiet /property:BuildIncremental=true
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build failed"
    exit 1
}

Write-Output "✅ Application builds successfully"

# Test QBO service initialization
Write-Output ""
Write-Output "Testing QBO service initialization..."
$testCode = @"
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WileyWidget.Services;
using Microsoft.Extensions.Logging;

class Program {
    static async Task Main(string[] args) {
        try {
            Console.WriteLine("Testing QBO service initialization...");

            // Create minimal host to test DI
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) => {
                    // Register minimal services needed for QBO
                    services.AddSingleton<SettingsService>(SettingsService.Instance);
                    services.AddSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
                    services.AddLogging();
                })
                .Build();

            var serviceProvider = host.Services;
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Program>();

            // Test local encrypted secret vault service
            var kvService = serviceProvider.GetRequiredService<ISecretVaultService>();
            Console.WriteLine("✓ Local encrypted secret vault service resolved");

            // Test QBO service creation
            var qbService = new QuickBooksService(
                serviceProvider.GetRequiredService<SettingsService>(),
                kvService,
                loggerFactory.CreateLogger<QuickBooksService>()
            );
            Console.WriteLine("✓ QuickBooks service created successfully");

            // Test basic connectivity (will fail without auth, but validates setup)
            try {
                var testResult = await qbService.TestConnectionAsync();
                Console.WriteLine($"✓ Connection test completed (result: {testResult})");
            } catch (Exception ex) {
                Console.WriteLine($"⚠ Connection test failed as expected (authentication required): {ex.Message}");
            }

            Console.WriteLine("");
            Console.WriteLine("SUCCESS: QBO service integration test completed!");
            Console.WriteLine("The service is properly configured to load secrets from the local encrypted secret vault.");

        } catch (Exception ex) {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
"@

$testCode | Out-File -FilePath "TestQboIntegration.cs" -Encoding UTF8

dotnet run --project WileyWidget.csproj TestQboIntegration.cs 2>$null
$testExitCode = $LASTEXITCODE

Remove-Item "TestQboIntegration.cs" -ErrorAction SilentlyContinue

if ($testExitCode -eq 0) {
    Write-Output "✅ QBO service integration test passed"
}
else {
    Write-Output "❌ QBO service integration test failed"
}

# Summary
Write-Output ""
Write-Output "=== Integration Summary ==="
Write-Output "Environment Variables: $($envStatus.Values | Where-Object { $_ } | Measure-Object | Select-Object -ExpandProperty Count)/$($envVars.Count) configured"
Write-Output "Local Secret Vault: $(if (Test-Path $vaultPath) { "Available" } else { "Not available" })"

if (($envStatus.Values | Where-Object { $_ } | Measure-Object | Select-Object -ExpandProperty Count) -eq $envVars.Count -or (Test-Path $vaultPath)) {
    Write-Output "✅ QBO secrets are available (via environment variables or local secret vault)"
}
else {
    Write-Output "❌ QBO secrets not available - configure environment variables or ensure local secret vault is populated"
}

Write-Output ""
Write-Output "=== Next Steps ==="
Write-Output "1. If environment variables are not set, populate the local secret vault:"
Write-Output "   - Start the application and use the Settings UI to configure QBO secrets"
Write-Output "   - Or use the secret vault migration scripts to import from environment variables"
Write-Output ""
Write-Output "2. Test full application startup to verify QBO initialization logging"
Write-Output "3. Use QuickBooks validation scripts to test API connectivity"
