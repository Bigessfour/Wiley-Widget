using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of IQuickBooksSandboxSeederService.
/// Programmatically creates municipal finance accounts in QBO sandbox via REST API.
/// </summary>
public sealed class QuickBooksSandboxSeederService : IQuickBooksSandboxSeederService
{
    private readonly ILogger<QuickBooksSandboxSeederService> _logger;
    private readonly IQuickBooksAuthService _authService;
    private readonly QuickBooksTokenStore? _tokenStore;
    private readonly HttpClient _httpClient;
    private SandboxSeedingResult? _lastSeedingResult;

    // Intuit QBO API endpoint for creating accounts
    private const string CreateAccountEndpoint = "https://quickbooks.api.intuit.com/v2/company/{0}/account";

    // Standard municipal finance account structure
    private static readonly List<QuickBooksAccountTemplate> MunicipalAccountTemplates = new()
    {
        // General Fund - Assets
        new() { Name = "Cash - General Fund", Type = "Asset", SubType = "Cash", AccountNumber = "1010", Description = "Operating cash for general fund" },
        new() { Name = "Investments - General Fund", Type = "Asset", SubType = "OtherCurrentAsset", AccountNumber = "1020", Description = "Short-term investments" },
        new() { Name = "Accounts Receivable - Taxes", Type = "Asset", SubType = "AccountsReceivable", AccountNumber = "1030", Description = "Tax receivables" },
        new() { Name = "Accounts Receivable - Other", Type = "Asset", SubType = "AccountsReceivable", AccountNumber = "1040", Description = "Other receivables" },

        // General Fund - Liabilities
        new() { Name = "Accounts Payable", Type = "Liability", SubType = "AccountsPayable", AccountNumber = "2010", Description = "Outstanding invoices payable" },
        new() { Name = "Accrued Payroll", Type = "Liability", SubType = "OtherCurrentLiability", AccountNumber = "2020", Description = "Accrued salary and wages" },
        new() { Name = "Deferred Revenue", Type = "Liability", SubType = "OtherCurrentLiability", AccountNumber = "2030", Description = "Advance collections" },

        // Fund Balance/Equity
        new() { Name = "Fund Balance - Assigned", Type = "Equity", SubType = "RetainedEarnings", AccountNumber = "3010", Description = "Committed fund balance" },
        new() { Name = "Fund Balance - Unassigned", Type = "Equity", SubType = "RetainedEarnings", AccountNumber = "3020", Description = "Unassigned fund balance" },
        new() { Name = "Net Position - Restricted", Type = "Equity", SubType = "RetainedEarnings", AccountNumber = "3030", Description = "Restricted net position for enterprise funds" },

        // Revenue Accounts
        new() { Name = "Property Tax Revenue", Type = "Income", SubType = "OtherIncomeSource", AccountNumber = "4010", Description = "Property tax collections" },
        new() { Name = "Sales Tax Revenue", Type = "Income", SubType = "OtherIncomeSource", AccountNumber = "4020", Description = "Sales tax collections" },
        new() { Name = "License & Permit Fees", Type = "Income", SubType = "OtherIncomeSource", AccountNumber = "4030", Description = "License and permit revenue" },
        new() { Name = "Intergovernmental Revenue", Type = "Income", SubType = "OtherIncomeSource", AccountNumber = "4040", Description = "Grants and shared revenue" },
        new() { Name = "Charges for Services", Type = "Income", SubType = "OtherIncomeSource", AccountNumber = "4050", Description = "User fees and service charges" },

        // Expense Accounts
        new() { Name = "Personal Services", Type = "Expense", SubType = "Salaries", AccountNumber = "5010", Description = "Salaries and wages" },
        new() { Name = "Employee Benefits", Type = "Expense", SubType = "Payroll", AccountNumber = "5020", Description = "Health insurance, retirement" },
        new() { Name = "Supplies", Type = "Expense", SubType = "OfficeSupplies", AccountNumber = "5030", Description = "Office and operational supplies" },
        new() { Name = "Utilities", Type = "Expense", SubType = "Utilities", AccountNumber = "5040", Description = "Electric, water, gas" },
        new() { Name = "Repairs & Maintenance", Type = "Expense", SubType = "Repairs", AccountNumber = "5050", Description = "Building and equipment maintenance" },
        new() { Name = "Capital Outlay", Type = "Expense", SubType = "DepreciableAssets", AccountNumber = "5060", Description = "Equipment and infrastructure purchases" },
    };

    public QuickBooksSandboxSeederService(
        ILogger<QuickBooksSandboxSeederService> logger,
        IQuickBooksAuthService authService,
        QuickBooksTokenStore? tokenStore,
        HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _tokenStore = tokenStore;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Seeds the sandbox with municipal finance accounts.
    /// </summary>
    public async Task<SandboxSeedingResult> SeedSandboxAsync(CancellationToken cancellationToken = default)
    {
        var result = new SandboxSeedingResult
        {
            AccountsAttempted = MunicipalAccountTemplates.Count
        };

        try
        {
            // Get access token
            var token = await _authService.GetAccessTokenAsync(cancellationToken);
            if (token == null)
            {
                result.IsSuccess = false;
                result.Message = "No valid OAuth token available";
                result.Errors.Add("Cannot seed sandbox: missing OAuth token");
                _logger.LogWarning("Sandbox seeding failed: no OAuth token");
                return result;
            }

            // Get realm ID
            var realmId = _tokenStore?.GetRealmId();
            if (string.IsNullOrEmpty(realmId))
            {
                result.IsSuccess = false;
                result.Message = "No QuickBooks realm ID available";
                result.Errors.Add("Cannot seed sandbox: missing realm ID");
                _logger.LogWarning("Sandbox seeding failed: no realm ID");
                return result;
            }

            _logger.LogInformation("Starting sandbox seeding for realm {RealmId}", realmId);

            // Create each account template
            foreach (var template in MunicipalAccountTemplates)
            {
                try
                {
                    var accountCreated = await CreateAccountAsync(token.AccessToken, realmId, template, cancellationToken);
                    if (accountCreated)
                    {
                        result.AccountsCreated++;
                        result.CreatedAccounts.Add(template.Name);
                        _logger.LogDebug("Created account: {AccountName}", template.Name);
                    }
                    else
                    {
                        result.Errors.Add($"Failed to create account: {template.Name}");
                        _logger.LogWarning("Failed to create account: {AccountName}", template.Name);
                    }

                    // Rate limiting: wait 100ms between requests (10 req/sec)
                    await Task.Delay(100, cancellationToken);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error creating {template.Name}: {ex.Message}");
                    _logger.LogError(ex, "Error creating account {AccountName}", template.Name);
                }
            }

            result.IsSuccess = result.AccountsCreated >= MunicipalAccountTemplates.Count / 2; // Success if at least 50% created
            result.Message = result.IsSuccess
                ? $"Successfully created {result.AccountsCreated} of {result.AccountsAttempted} accounts"
                : $"Partial success: created {result.AccountsCreated} of {result.AccountsAttempted} accounts";

            _logger.LogInformation("Sandbox seeding completed: {Message}", result.Message);
            _lastSeedingResult = result;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during sandbox seeding");
            result.IsSuccess = false;
            result.Message = $"Sandbox seeding failed: {ex.Message}";
            result.Errors.Add(ex.Message);
            _lastSeedingResult = result;
            return result;
        }
    }

    /// <summary>
    /// Gets the last seeding result.
    /// </summary>
    public SandboxSeedingResult? GetLastSeedingStatus()
    {
        return _lastSeedingResult;
    }

    /// <summary>
    /// Clears the seeding status.
    /// </summary>
    public void ClearSeedingStatus()
    {
        _lastSeedingResult = null;
    }

    /// <summary>
    /// Creates a single account via QuickBooks API.
    /// </summary>
    private async Task<bool> CreateAccountAsync(
        string accessToken,
        string realmId,
        QuickBooksAccountTemplate template,
        CancellationToken cancellationToken)
    {
        try
        {
            // Build account creation payload
            var payload = new
            {
                Name = template.Name,
                Type = template.Type,
                SubType = template.SubType,
                AcctNum = template.AccountNumber,
                Description = template.Description,
                Active = template.Active,
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            var endpoint = string.Format(CreateAccountEndpoint, realmId);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Account created successfully: {AccountName}", template.Name);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to create account {AccountName}: {StatusCode} - {Error}",
                    template.Name, response.StatusCode, errorContent);
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error creating account {AccountName}", template.Name);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating account {AccountName}", template.Name);
            return false;
        }
    }
}
