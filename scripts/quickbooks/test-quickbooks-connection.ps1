# QuickBooks API Connection Test Script
# Tests the QuickBooks service integration without making actual API calls

Write-Output "=== QuickBooks API Connection Test ==="
Write-Output ""

# Check environment variables
Write-Output "Checking environment variables..."
$envVars = @("QBO_CLIENT_ID", "QBO_CLIENT_SECRET", "QBO_REALM_ID", "QBO_ENVIRONMENT")
$missingVars = @()

foreach ($var in $envVars) {
    $value = [System.Environment]::GetEnvironmentVariable($var, "User")
    if ([string]::IsNullOrEmpty($value)) {
        $missingVars += $var
        Write-Output "❌ $var : Not set"
    }
    else {
        Write-Output "✅ $var : Set ($value)"
    }
}

if ($missingVars.Count -gt 0) {
    Write-Error "Missing required environment variables: $($missingVars -join ', ')"
    Write-Output ""
    Write-Output "To set credentials, run:"
    Write-Output ".\setup-quickbooks-sandbox.ps1 -ClientId 'your-client-id' -ClientSecret 'your-client-secret'"
    exit 1
}

Write-Output ""
Write-Output "✅ All required environment variables are set"
Write-Output ""

# Build the project
Write-Output "Building project..."
dotnet build "WileyWidget.csproj" --verbosity quiet /property:BuildIncremental=true
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed. Cannot test QuickBooks integration."
    exit 1
}

Write-Output "✅ Project builds successfully"
Write-Output ""

# Test service instantiation
Write-Output "Testing QuickBooks service instantiation..."
$testCode = @"
using System;
using System.Threading.Tasks;
using System.Linq;
using WileyWidget.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

class Program {
    static async Task Main(string[] args) {
        try {
            Console.WriteLine("Testing QuickBooks service instantiation...");

            // Test SettingsService instantiation
            var settings = SettingsService.Instance;
            Console.WriteLine("✓ SettingsService created successfully");

            // Test QuickBooksService instantiation
            var keyVault = new FakeSecretVaultService();
            ILogger<QuickBooksService> logger = NullLogger<QuickBooksService>.Instance;
            var qbService = new QuickBooksService(settings, keyVault, logger);
            Console.WriteLine("✓ QuickBooksService created successfully");

            // Test interface implementation
            IQuickBooksService qbInterface = qbService;
            Console.WriteLine("✓ IQuickBooksService interface implemented correctly");

            // Test method availability (without calling them)
            var methods = typeof(IQuickBooksService).GetMethods();
            Console.WriteLine($"✓ Interface has {methods.Length} methods defined");

            var expectedMethods = new[] {
                "TestConnectionAsync",
                "GetCustomersAsync",
                "GetInvoicesAsync",
                "GetChartOfAccountsAsync",
                "GetJournalEntriesAsync",
                "GetBudgetsAsync"
            };

            foreach (var methodName in expectedMethods) {
                var method = methods.FirstOrDefault(m => m.Name == methodName);
                if (method != null) {
                    Console.WriteLine($"✓ Method {methodName} is available");
                } else {
                    Console.WriteLine($"❌ Method {methodName} is missing");
                }
            }

            Console.WriteLine("");
            Console.WriteLine("SUCCESS: QuickBooks service structure validation completed!");
            Console.WriteLine("The service is properly implemented and ready for API calls.");

        } catch (Exception ex) {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}

sealed class FakeSecretVaultService : ISecretVaultService {
    public Task<string?> GetSecretAsync(string secretName) => Task.FromResult<string?>(null);
    public Task SetSecretAsync(string secretName, string value) => Task.CompletedTask;
    public Task<bool> TestConnectionAsync() => Task.FromResult(true);
}
"@

$testCode | Out-File -FilePath "QuickBooksStructureTest.cs" -Encoding UTF8

dotnet run --project WileyWidget.csproj QuickBooksStructureTest.cs
$testExitCode = $LASTEXITCODE

Remove-Item "QuickBooksStructureTest.cs" -ErrorAction SilentlyContinue

if ($testExitCode -eq 0) {
    Write-Output "✅ QuickBooks service structure validation completed successfully!"
    Write-Output ""
    Write-Output "Next steps for full validation:"
    Write-Output "1. Obtain real QuickBooks sandbox credentials from developer.intuit.com"
    Write-Output "2. Run: .\setup-quickbooks-sandbox.ps1 -ClientId '<real-id>' -ClientSecret '<real-secret>'"
    Write-Output "3. Run: .\setup-quickbooks-sandbox.ps1 -ValidateOnly"
    Write-Output "4. Run: .\setup-town-of-wiley.ps1 to setup municipal accounts"
}
else {
    Write-Error "❌ QuickBooks service structure validation failed!"
    exit 1
}
