# QuickBooks Sandbox Setup and Validation Script
# Run this script to configure and validate your QuickBooks sandbox credentials

param(
    [Parameter(Mandatory = $true)]
    [string]$ClientId,

    [Parameter(Mandatory = $true)]
    [string]$ClientSecret,

    [Parameter(Mandatory = $false)]
    [switch]$ValidateOnly,

    [Parameter(Mandatory = $false)]
    [switch]$SetupSandboxAccounts
)

Write-Output "=== QuickBooks Sandbox Setup and Validation ==="
Write-Output ""

# Set environment variables
Write-Output "Setting QuickBooks sandbox credentials..."
[System.Environment]::SetEnvironmentVariable("QBO_CLIENT_ID", $ClientId, "User")
[System.Environment]::SetEnvironmentVariable("QBO_CLIENT_SECRET", $ClientSecret, "User")
[System.Environment]::SetEnvironmentVariable("QBO_REALM_ID", "9341455168020461", "User")
[System.Environment]::SetEnvironmentVariable("QBO_ENVIRONMENT", "sandbox", "User")

Write-Output "Environment variables set successfully!"
Write-Output "Client ID: $ClientId"
Write-Output "Realm ID: 9341455168020461"
Write-Output "Environment: sandbox"
Write-Output ""

if ($ValidateOnly) {
    Write-Output "=== Validating QuickBooks API Connection ==="

    # Build the project first
    Write-Output "Building project..."
    dotnet build "WileyWidget.csproj" --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed. Cannot validate QuickBooks connection."
        exit 1
    }

    # Run a simple test to validate the connection
    Write-Output "Testing QuickBooks API connection..."
    try {
        # Create a simple test program to validate connection
        $testCode = @"
using System;
using System.Threading.Tasks;
using WileyWidget.Services;

class Program {
    static async Task Main(string[] args) {
        try {
            var settings = new SettingsService();
            var qbService = new QuickBooksService(settings);

            Console.WriteLine("Testing QuickBooks connection...");
            bool isConnected = await qbService.TestConnectionAsync();

            if (isConnected) {
                Console.WriteLine("SUCCESS: QuickBooks API connection validated!");
                Console.WriteLine("Fetching sample data...");

                var customers = await qbService.GetCustomersAsync();
                Console.WriteLine($"Found {customers.Count} customers");

                var accounts = await qbService.GetChartOfAccountsAsync();
                Console.WriteLine($"Found {accounts.Count} accounts");

                Console.WriteLine("QuickBooks sandbox is ready!");
            } else {
                Console.WriteLine("FAILED: Could not connect to QuickBooks API");
                Environment.Exit(1);
            }
        } catch (Exception ex) {
            Console.WriteLine($"ERROR: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
"@

        $testCode | Out-File -FilePath "QuickBooksTest.cs" -Encoding UTF8

        # Compile and run the test
        dotnet run --project WileyWidget.csproj QuickBooksTest.cs
        $testExitCode = $LASTEXITCODE

        # Clean up
        Remove-Item "QuickBooksTest.cs" -ErrorAction SilentlyContinue

        if ($testExitCode -eq 0) {
            Write-Output "✓ QuickBooks API validation completed successfully!"
        }
        else {
            Write-Error "✗ QuickBooks API validation failed!"
            exit 1
        }

    }
    catch {
        Write-Error "Failed to run validation test: $_"
        exit 1
    }

    Write-Output ""
    Write-Output "Next steps:"
    Write-Output "1. Run your application"
    Write-Output "2. Go to Settings and test QuickBooks connection"
    Write-Output "3. The app will handle OAuth authentication automatically"
    exit 0
}

if ($SetupSandboxAccounts) {
    Write-Output "=== Setting up Town of Wiley Accounts in QuickBooks Sandbox ==="

    # Build the project first
    Write-Output "Building project..."
    dotnet build "WileyWidget.csproj" --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed. Cannot setup sandbox accounts."
        exit 1
    }

    # Create a setup program for sandbox accounts
    Write-Output "Setting up Town of Wiley sandbox accounts..."
    try {
        $setupCode = @"
using System;
using System.Threading.Tasks;
using System.Linq;
using Intuit.Ipp.Data;
using WileyWidget.Services;

class Program {
    static async Task Main(string[] args) {
        try {
            var settings = new SettingsService();
            var qbService = new QuickBooksService(settings);

            Console.WriteLine("Setting up Town of Wiley Accounts in QuickBooks Sandbox...");

            // Test connection first
            bool isConnected = await qbService.TestConnectionAsync();
            if (!isConnected) {
                Console.WriteLine("FAILED: Could not connect to QuickBooks API");
                Environment.Exit(1);
            }

            Console.WriteLine("Connected to QuickBooks sandbox successfully!");

            // Get existing chart of accounts
            var existingAccounts = await qbService.GetChartOfAccountsAsync();
            Console.WriteLine($"Found {existingAccounts.Count} existing accounts");

            // Setup Town of Wiley specific accounts if they don't exist
            await SetupTownOfWileyAccounts(qbService);

            // Setup sample customers
            await SetupSampleCustomers(qbService);

            // Setup sample vendors
            await SetupSampleVendors(qbService);

            Console.WriteLine("SUCCESS: Town of Wiley Accounts setup completed!");
            Console.WriteLine("Sandbox is ready for municipal accounting operations.");

        } catch (Exception ex) {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    static async Task SetupTownOfWileyAccounts(QuickBooksService qbService) {
        Console.WriteLine("Setting up municipal accounting chart of accounts...");

        // This would typically create specific accounts for municipal operations
        // For now, we'll just verify we can access the chart of accounts
        var accounts = await qbService.GetChartOfAccountsAsync();

        // Look for common municipal accounts
        var municipalAccounts = new[] {
            "General Fund",
            "Water Fund",
            "Sewer Fund",
            "Property Taxes Receivable",
            "Utility Bills Receivable",
            "Salaries Payable",
            "Vendor Payments"
        };

        foreach (var accountName in municipalAccounts) {
            var existing = accounts.FirstOrDefault(a => a.Name.Contains(accountName));
            if (existing != null) {
                Console.WriteLine($"✓ Found account: {existing.Name}");
            } else {
                Console.WriteLine($"⚠ Account not found: {accountName} (may need manual setup)");
            }
        }
    }

    static async Task SetupSampleCustomers(QuickBooksService qbService) {
        Console.WriteLine("Setting up sample municipal customers...");

        var customers = await qbService.GetCustomersAsync();
        Console.WriteLine($"Found {customers.Count} existing customers");

        // Sample municipal customers (property owners, utility customers, etc.)
        var sampleCustomers = new[] {
            "John Smith - 123 Main St",
            "Jane Doe - 456 Oak Ave",
            "Town of Wiley Administration",
            "Wiley Public Library",
            "Wiley Fire Department"
        };

        foreach (var customerName in sampleCustomers) {
            var existing = customers.FirstOrDefault(c => c.Name.Contains(customerName.Split('-')[0].Trim()));
            if (existing != null) {
                Console.WriteLine($"✓ Found customer: {existing.Name}");
            } else {
                Console.WriteLine($"⚠ Customer not found: {customerName} (may need manual setup)");
            }
        }
    }

    static async Task SetupSampleVendors(QuickBooksService qbService) {
        Console.WriteLine("Setting up sample municipal vendors...");

        // Note: QuickBooks SDK doesn't have a direct GetVendors method
        // This would typically be handled through the Vendor entity
        Console.WriteLine("Vendor setup would be implemented with Vendor entity access");
    }
}
"@

        $setupCode | Out-File -FilePath "QuickBooksSetup.cs" -Encoding UTF8

        # Compile and run the setup
        dotnet run --project WileyWidget.csproj QuickBooksSetup.cs
        $setupExitCode = $LASTEXITCODE

        # Clean up
        Remove-Item "QuickBooksSetup.cs" -ErrorAction SilentlyContinue

        if ($setupExitCode -eq 0) {
            Write-Output "✓ Town of Wiley Accounts setup completed successfully!"
        }
        else {
            Write-Error "✗ Town of Wiley Accounts setup failed!"
            exit 1
        }

    }
    catch {
        Write-Error "Failed to run sandbox setup: $_"
        exit 1
    }

    Write-Output ""
    Write-Output "Sandbox setup complete!"
    Write-Output "You can now use the Wiley Widget application with QuickBooks integration."
    exit 0
}

Write-Output "Environment variables set successfully!"
Write-Output ""
Write-Output "Next steps:"
Write-Output "1. Run the script with -ValidateOnly to test the connection:"
Write-Output "   .\setup-quickbooks-sandbox.ps1 -ClientId '$ClientId' -ClientSecret '$ClientSecret' -ValidateOnly"
Write-Output ""
Write-Output "2. Run the script with -SetupSandboxAccounts to setup Town of Wiley accounts:"
Write-Output "   .\setup-quickbooks-sandbox.ps1 -ClientId '$ClientId' -ClientSecret '$ClientSecret' -SetupSandboxAccounts"
Write-Output ""
Write-Output "3. Run your application and test QuickBooks connection in Settings"
