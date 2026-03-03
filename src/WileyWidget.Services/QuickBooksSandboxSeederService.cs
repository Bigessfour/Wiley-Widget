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

    // Intuit QBO API endpoint for creating accounts. v3 is required; host differs by environment.
    private string GetCreateAccountEndpoint(string realmId)
    {
        var host = _authService.GetEnvironment() == "sandbox"
            ? "sandbox-quickbooks.api.intuit.com"
            : "quickbooks.api.intuit.com";
        // minorversion=65 targets a stable, well-defined API surface
        return $"https://{host}/v3/company/{realmId}/account?minorversion=65";
    }

    // Standard municipal finance account structure.
    // AccountType and AccountSubType must match Intuit AccountTypeEnum exactly.
    // Reference: https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/account
    private static readonly List<QuickBooksAccountTemplate> MunicipalAccountTemplates = new()
    {
        // ── Assets: Cash & Bank (1000–1099) ───────────────────────────────────
        new() { Name = "Cash - General Fund",                    AccountType = "Bank",                    AccountSubType = "Checking",                       AccountNumber = "1010", Description = "Operating cash for general fund" },
        new() { Name = "Cash - Water/Sewer Fund",                AccountType = "Bank",                    AccountSubType = "Checking",                       AccountNumber = "1015", Description = "Operating cash for water/sewer utility" },
        new() { Name = "Cash - Capital Projects Fund",           AccountType = "Bank",                    AccountSubType = "Checking",                       AccountNumber = "1020", Description = "Capital project cash reserves" },
        new() { Name = "Petty Cash",                             AccountType = "Bank",                    AccountSubType = "CashAndCashEquivalents",          AccountNumber = "1030", Description = "Petty cash on hand" },
        new() { Name = "Cash - Debt Service Fund",               AccountType = "Bank",                    AccountSubType = "Checking",                       AccountNumber = "1040", Description = "Restricted cash for debt service payments" },

        // ── Assets: Investments & Receivables (1050–1199) ────────────────────
        new() { Name = "Investments - Short Term",               AccountType = "Other Current Asset",     AccountSubType = "OtherCurrentAssets",             AccountNumber = "1055", Description = "Short-term investments and T-bills" },
        new() { Name = "Accounts Receivable - Property Taxes",   AccountType = "Accounts Receivable",     AccountSubType = "AccountsReceivable",             AccountNumber = "1100", Description = "Property tax receivables" },
        new() { Name = "Accounts Receivable - Sales Taxes",      AccountType = "Accounts Receivable",     AccountSubType = "AccountsReceivable",             AccountNumber = "1110", Description = "Sales tax receivables" },
        new() { Name = "Accounts Receivable - Utility Fees",     AccountType = "Accounts Receivable",     AccountSubType = "AccountsReceivable",             AccountNumber = "1120", Description = "Water/sewer fee receivables" },
        new() { Name = "Accounts Receivable - Other",            AccountType = "Accounts Receivable",     AccountSubType = "AccountsReceivable",             AccountNumber = "1130", Description = "Miscellaneous accounts receivable" },
        new() { Name = "Allowance for Doubtful Accounts",        AccountType = "Other Current Asset",     AccountSubType = "AllowanceForBadDebts",           AccountNumber = "1140", Description = "Contra-receivable for estimated uncollectibles" },

        // ── Assets: Prepaid & Other Current (1200–1299) ──────────────────────
        new() { Name = "Prepaid Insurance",                      AccountType = "Other Current Asset",     AccountSubType = "PrepaidExpenses",                AccountNumber = "1200", Description = "Insurance premiums paid in advance" },
        new() { Name = "Prepaid Expenses - Other",               AccountType = "Other Current Asset",     AccountSubType = "PrepaidExpenses",                AccountNumber = "1210", Description = "Other prepaid expenses" },
        new() { Name = "Inventory - Supplies",                   AccountType = "Other Current Asset",     AccountSubType = "Inventory",                      AccountNumber = "1220", Description = "Consumable supplies inventory" },

        // ── Assets: Capital / Fixed (1300–1499) ──────────────────────────────
        new() { Name = "Land",                                   AccountType = "Fixed Asset",             AccountSubType = "Land",                           AccountNumber = "1300", Description = "Municipal land holdings (not depreciated)" },
        new() { Name = "Buildings",                              AccountType = "Fixed Asset",             AccountSubType = "Buildings",                      AccountNumber = "1310", Description = "Municipal buildings at cost" },
        new() { Name = "Accum Depreciation - Buildings",         AccountType = "Fixed Asset",             AccountSubType = "AccumulatedDepreciation",        AccountNumber = "1315", Description = "Accumulated depreciation on buildings" },
        new() { Name = "Equipment & Machinery",                  AccountType = "Fixed Asset",             AccountSubType = "MachineryAndEquipment",           AccountNumber = "1320", Description = "Heavy equipment and machinery at cost" },
        new() { Name = "Accum Depreciation - Equipment",         AccountType = "Fixed Asset",             AccountSubType = "AccumulatedDepreciation",        AccountNumber = "1325", Description = "Accumulated depreciation on equipment" },
        new() { Name = "Fleet Vehicles",                         AccountType = "Fixed Asset",             AccountSubType = "Vehicles",                       AccountNumber = "1330", Description = "Municipal vehicle fleet at cost" },
        new() { Name = "Accum Depreciation - Vehicles",          AccountType = "Fixed Asset",             AccountSubType = "AccumulatedDepreciation",        AccountNumber = "1335", Description = "Accumulated depreciation on vehicles" },
        new() { Name = "Infrastructure - Roads & Bridges",       AccountType = "Fixed Asset",             AccountSubType = "LeaseholdImprovements",          AccountNumber = "1340", Description = "Roads, bridges, and public infrastructure at cost" },
        new() { Name = "Accum Depreciation - Infrastructure",    AccountType = "Fixed Asset",             AccountSubType = "AccumulatedDepreciation",        AccountNumber = "1345", Description = "Accumulated depreciation on infrastructure" },
        new() { Name = "Furniture & Fixtures",                   AccountType = "Fixed Asset",             AccountSubType = "FurnitureAndFixtures",           AccountNumber = "1350", Description = "Office furniture and fixtures" },

        // ── Liabilities: Accounts Payable (2000–2099) ────────────────────────
        new() { Name = "Accounts Payable",                       AccountType = "Accounts Payable",        AccountSubType = "AccountsPayable",                AccountNumber = "2010", Description = "Vendor invoices outstanding" },

        // ── Liabilities: Accrued & Current (2020–2199) ───────────────────────
        new() { Name = "Accrued Payroll",                        AccountType = "Other Current Liability", AccountSubType = "PayrollTaxPayable",              AccountNumber = "2020", Description = "Salaries and wages accrued but unpaid" },
        new() { Name = "Accrued Employee Benefits",              AccountType = "Other Current Liability", AccountSubType = "PayrollTaxPayable",              AccountNumber = "2030", Description = "Accrued health and retirement benefits" },
        new() { Name = "Payroll Taxes Payable",                  AccountType = "Other Current Liability", AccountSubType = "PayrollTaxPayable",              AccountNumber = "2040", Description = "Federal/state payroll taxes withheld" },
        new() { Name = "Sales Tax Payable",                      AccountType = "Other Current Liability", AccountSubType = "SalesTaxPayable",                AccountNumber = "2050", Description = "Sales tax collected pending remittance" },
        new() { Name = "Deferred Revenue - Taxes",               AccountType = "Other Current Liability", AccountSubType = "OtherCurrentLiabilities",        AccountNumber = "2060", Description = "Tax revenue collected for future period" },
        new() { Name = "Deferred Revenue - Grants",              AccountType = "Other Current Liability", AccountSubType = "OtherCurrentLiabilities",        AccountNumber = "2070", Description = "Grant proceeds not yet earned" },
        new() { Name = "Due to Other Funds",                     AccountType = "Other Current Liability", AccountSubType = "OtherCurrentLiabilities",       AccountNumber = "2080", Description = "Interfund payables" },
        new() { Name = "Current Portion - Long Term Debt",       AccountType = "Other Current Liability", AccountSubType = "LineOfCredit",                   AccountNumber = "2090", Description = "Current year portion of long-term obligations" },

        // ── Liabilities: Long-term (2200–2299) ───────────────────────────────
        new() { Name = "General Obligation Bonds Payable",       AccountType = "Long Term Liability",     AccountSubType = "NotesPayable",                   AccountNumber = "2200", Description = "General obligation bond debt outstanding" },
        new() { Name = "Revenue Bonds Payable",                  AccountType = "Long Term Liability",     AccountSubType = "NotesPayable",                   AccountNumber = "2210", Description = "Revenue-backed bond debt outstanding" },
        new() { Name = "Notes Payable - Long Term",              AccountType = "Long Term Liability",     AccountSubType = "NotesPayable",                   AccountNumber = "2220", Description = "Long-term notes and loans payable" },

        // ── Fund Balance / Equity (3000–3099) ────────────────────────────────
        new() { Name = "Fund Balance - Nonspendable",            AccountType = "Equity",                  AccountSubType = "RetainedEarnings",               AccountNumber = "3010", Description = "Non-liquid or legally restricted fund balance" },
        new() { Name = "Fund Balance - Restricted",              AccountType = "Equity",                  AccountSubType = "RetainedEarnings",               AccountNumber = "3020", Description = "Externally restricted fund balance" },
        new() { Name = "Fund Balance - Committed",               AccountType = "Equity",                  AccountSubType = "RetainedEarnings",               AccountNumber = "3030", Description = "Formally committed fund balance" },
        new() { Name = "Fund Balance - Assigned",                AccountType = "Equity",                  AccountSubType = "RetainedEarnings",               AccountNumber = "3040", Description = "Appropriated fund balance" },
        new() { Name = "Fund Balance - Unassigned",              AccountType = "Equity",                  AccountSubType = "RetainedEarnings",               AccountNumber = "3050", Description = "Residual unassigned fund balance" },
        new() { Name = "Net Investment in Capital Assets",       AccountType = "Equity",                  AccountSubType = "OwnersEquity",                   AccountNumber = "3060", Description = "Net position invested in capital assets" },
        new() { Name = "Net Position - Restricted",              AccountType = "Equity",                  AccountSubType = "OwnersEquity",                   AccountNumber = "3070", Description = "Restricted net position for enterprise funds" },
        new() { Name = "Net Position - Unrestricted",            AccountType = "Equity",                  AccountSubType = "OwnersEquity",                   AccountNumber = "3080", Description = "Unrestricted net position" },

        // ── Revenue (4000–4199) ───────────────────────────────────────────────
        new() { Name = "Property Tax Revenue",                   AccountType = "Income",                  AccountSubType = "OtherPrimaryIncome",             AccountNumber = "4010", Description = "Ad valorem property tax levy" },
        new() { Name = "Sales Tax Revenue",                      AccountType = "Income",                  AccountSubType = "OtherPrimaryIncome",             AccountNumber = "4020", Description = "Municipal sales tax collections" },
        new() { Name = "License & Permit Fees",                  AccountType = "Income",                  AccountSubType = "ServiceFeeIncome",               AccountNumber = "4030", Description = "Business licenses and building permits" },
        new() { Name = "Intergovernmental - Federal Grants",     AccountType = "Income",                  AccountSubType = "NonProfitIncome",                AccountNumber = "4040", Description = "Federal grant revenue" },
        new() { Name = "Intergovernmental - State Grants",       AccountType = "Income",                  AccountSubType = "NonProfitIncome",                AccountNumber = "4050", Description = "State grant and shared revenue" },
        new() { Name = "Charges for Services - Public Works",    AccountType = "Income",                  AccountSubType = "ServiceFeeIncome",               AccountNumber = "4060", Description = "Public works service fees" },
        new() { Name = "Charges for Services - Parks & Rec",     AccountType = "Income",                  AccountSubType = "ServiceFeeIncome",               AccountNumber = "4070", Description = "Recreation program and facility fees" },
        new() { Name = "Building Permit Revenue",                AccountType = "Income",                  AccountSubType = "ServiceFeeIncome",               AccountNumber = "4080", Description = "Building and zoning permit revenue" },
        new() { Name = "Fines & Forfeitures",                    AccountType = "Income",                  AccountSubType = "OtherPrimaryIncome",             AccountNumber = "4090", Description = "Court fines and code enforcement penalties" },
        new() { Name = "Interest Revenue",                       AccountType = "Income",                  AccountSubType = "OtherPrimaryIncome",             AccountNumber = "4100", Description = "Interest earned on investments and deposits" },
        new() { Name = "Rental Revenue",                         AccountType = "Income",                  AccountSubType = "OtherPrimaryIncome",             AccountNumber = "4110", Description = "Revenue from municipal property leases" },
        new() { Name = "Water Revenue",                          AccountType = "Income",                  AccountSubType = "ServiceFeeIncome",               AccountNumber = "4130", Description = "Water utility billings" },
        new() { Name = "Sewer Revenue",                          AccountType = "Income",                  AccountSubType = "ServiceFeeIncome",               AccountNumber = "4140", Description = "Sewer/wastewater utility billings" },
        new() { Name = "Stormwater Revenue",                     AccountType = "Income",                  AccountSubType = "ServiceFeeIncome",               AccountNumber = "4150", Description = "Stormwater utility fees" },
        new() { Name = "Miscellaneous Revenue",                  AccountType = "Income",                  AccountSubType = "OtherPrimaryIncome",             AccountNumber = "4190", Description = "Miscellaneous general revenue" },

        // ── Expenses: General Government (5000–5099) ─────────────────────────
        new() { Name = "Salaries - Administration",              AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "5010", Description = "Executive and administrative staff salaries" },
        new() { Name = "Benefits - Administration",              AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "5020", Description = "Health, retirement, and payroll taxes - admin" },
        new() { Name = "Office Supplies - Administration",       AccountType = "Expense",                 AccountSubType = "SuppliesMaterials",              AccountNumber = "5030", Description = "Administrative office supplies" },
        new() { Name = "Professional Services - Legal",          AccountType = "Expense",                 AccountSubType = "LegalProfessionalFees",          AccountNumber = "5040", Description = "City attorney and outside legal counsel" },
        new() { Name = "Professional Services - Audit",          AccountType = "Expense",                 AccountSubType = "LegalProfessionalFees",          AccountNumber = "5050", Description = "Annual audit and financial consulting fees" },
        new() { Name = "Insurance - General Liability",          AccountType = "Expense",                 AccountSubType = "Insurance",                      AccountNumber = "5060", Description = "General liability and property insurance" },
        new() { Name = "Technology & Software",                  AccountType = "Expense",                 AccountSubType = "OfficeGeneralAdministrativeExpenses", AccountNumber = "5070", Description = "IT infrastructure, software licenses, support" },
        new() { Name = "Postage & Printing",                     AccountType = "Expense",                 AccountSubType = "OfficeGeneralAdministrativeExpenses", AccountNumber = "5080", Description = "Postage, printing, and public notices" },
        new() { Name = "Dues & Subscriptions",                   AccountType = "Expense",                 AccountSubType = "OfficeGeneralAdministrativeExpenses", AccountNumber = "5090", Description = "Professional memberships and publications" },

        // ── Expenses: Public Safety – Police (5100–5199) ─────────────────────
        new() { Name = "Salaries - Police",                      AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "5110", Description = "Police department sworn and civilian salaries" },
        new() { Name = "Benefits - Police",                      AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "5120", Description = "Police department benefits and payroll taxes" },
        new() { Name = "Equipment - Police",                     AccountType = "Expense",                 AccountSubType = "SuppliesMaterials",              AccountNumber = "5130", Description = "Police equipment, uniforms, and gear" },
        new() { Name = "Training - Police",                      AccountType = "Expense",                 AccountSubType = "OfficeGeneralAdministrativeExpenses", AccountNumber = "5140", Description = "Police training and certification costs" },

        // ── Expenses: Public Safety – Fire (5200–5299) ───────────────────────
        new() { Name = "Salaries - Fire",                        AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "5210", Description = "Fire department salaries and overtime" },
        new() { Name = "Benefits - Fire",                        AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "5220", Description = "Fire department benefits and payroll taxes" },
        new() { Name = "Apparatus & Equipment - Fire",           AccountType = "Expense",                 AccountSubType = "EquipmentRental",                AccountNumber = "5230", Description = "Fire apparatus, gear, and equipment" },
        new() { Name = "Training - Fire",                        AccountType = "Expense",                 AccountSubType = "OfficeGeneralAdministrativeExpenses", AccountNumber = "5240", Description = "Firefighter training and certification" },
        new() { Name = "Supplies - Fire",                        AccountType = "Expense",                 AccountSubType = "SuppliesMaterials",              AccountNumber = "5250", Description = "Fire station supplies and materials" },

        // ── Expenses: Public Works (5300–5399) ───────────────────────────────
        new() { Name = "Salaries - Public Works",                AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "5310", Description = "Public works department salaries" },
        new() { Name = "Benefits - Public Works",                AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "5320", Description = "Public works benefits and payroll taxes" },
        new() { Name = "Street Maintenance Materials",           AccountType = "Expense",                 AccountSubType = "SuppliesMaterials",              AccountNumber = "5330", Description = "Asphalt, aggregate, and patching materials" },
        new() { Name = "Equipment Fuel & Maintenance",           AccountType = "Expense",                 AccountSubType = "RepairMaintenance",              AccountNumber = "5340", Description = "Fleet fuel, oil, and scheduled maintenance" },
        new() { Name = "Contract Services - Streets",            AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",  AccountNumber = "5350", Description = "Contracted street repair and paving" },
        new() { Name = "Snow Removal & De-icing",                AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",  AccountNumber = "5360", Description = "Winter road maintenance expenses" },

        // ── Expenses: Parks & Recreation (5400–5499) ─────────────────────────
        new() { Name = "Salaries - Parks & Rec",                 AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "5410", Description = "Parks and recreation staff salaries" },
        new() { Name = "Benefits - Parks & Rec",                 AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "5420", Description = "Parks department benefits and payroll taxes" },
        new() { Name = "Supplies - Parks",                       AccountType = "Expense",                 AccountSubType = "SuppliesMaterials",              AccountNumber = "5430", Description = "Grounds supplies, seed, fertilizer" },
        new() { Name = "Facility Maintenance - Parks",           AccountType = "Expense",                 AccountSubType = "RepairMaintenance",              AccountNumber = "5440", Description = "Park building and facility repairs" },
        new() { Name = "Recreation Program Expenses",            AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",  AccountNumber = "5450", Description = "Recreation program and event direct costs" },

        // ── Expenses: Utilities – Water (6000–6049) ──────────────────────────
        new() { Name = "Salaries - Water Utility",               AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "6010", Description = "Water utility operations staff salaries" },
        new() { Name = "Benefits - Water Utility",               AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "6020", Description = "Water utility benefits and payroll taxes" },
        new() { Name = "Water Treatment Chemicals",              AccountType = "Expense",                 AccountSubType = "SuppliesMaterials",              AccountNumber = "6030", Description = "Chlorine, fluoride, and treatment chemicals" },
        new() { Name = "Water System Maintenance",               AccountType = "Expense",                 AccountSubType = "RepairMaintenance",              AccountNumber = "6040", Description = "Water main, pump, and meter maintenance" },

        // ── Expenses: Utilities – Sewer (6050–6099) ──────────────────────────
        new() { Name = "Salaries - Sewer Utility",               AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "6050", Description = "Sewer/WWTP operations staff salaries" },
        new() { Name = "Benefits - Sewer Utility",               AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",   AccountNumber = "6060", Description = "Sewer utility benefits and payroll taxes" },
        new() { Name = "Sewer Treatment Operations",             AccountType = "Expense",                 AccountSubType = "OtherMiscellaneousServiceCost",  AccountNumber = "6070", Description = "WWTP operating costs and compliance" },
        new() { Name = "Sewer System Maintenance",               AccountType = "Expense",                 AccountSubType = "RepairMaintenance",              AccountNumber = "6080", Description = "Collection system repair and maintenance" },

        // ── Expenses: Utilities – Electric (6090–6099) ───────────────────────
        new() { Name = "Utilities - Electric",                   AccountType = "Expense",                 AccountSubType = "Utilities",                      AccountNumber = "6090", Description = "Municipal electric utility costs" },
        new() { Name = "Utilities - Natural Gas",                AccountType = "Expense",                 AccountSubType = "Utilities",                      AccountNumber = "6095", Description = "Natural gas for municipal facilities" },

        // ── Expenses: Capital Outlay (6100–6199) ─────────────────────────────
        new() { Name = "Capital Outlay - Buildings",             AccountType = "Fixed Asset",             AccountSubType = "Buildings",                      AccountNumber = "6110", Description = "Capital expenditures on building construction" },
        new() { Name = "Capital Outlay - Equipment",             AccountType = "Fixed Asset",             AccountSubType = "MachineryAndEquipment",           AccountNumber = "6120", Description = "Major equipment purchases and replacements" },
        new() { Name = "Capital Outlay - Vehicles",              AccountType = "Fixed Asset",             AccountSubType = "Vehicles",                       AccountNumber = "6130", Description = "Fleet vehicle additions and replacements" },
        new() { Name = "Capital Outlay - Infrastructure",        AccountType = "Fixed Asset",             AccountSubType = "LeaseholdImprovements",          AccountNumber = "6140", Description = "Road, bridge, and infrastructure capital investments" },

        // ── Expenses: Debt Service (6200–6299) ───────────────────────────────
        new() { Name = "Bond Principal Payments",                AccountType = "Expense",                 AccountSubType = "InterestPaid",                   AccountNumber = "6210", Description = "Annual principal retirement on bond debt" },
        new() { Name = "Bond Interest Payments",                 AccountType = "Expense",                 AccountSubType = "InterestPaid",                   AccountNumber = "6220", Description = "Interest expense on outstanding bonds" },
        new() { Name = "Debt Issuance Costs",                    AccountType = "Expense",                 AccountSubType = "InterestPaid",                   AccountNumber = "6230", Description = "Underwriting and issuance fees on new debt" },
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
            // Build account creation payload using exact Intuit QBO field names.
            // AccountType and AccountSubType must match Intuit enum values exactly.
            // CurrencyRef requires BOTH value (ISO code) AND name when multicurrency is enabled.
            // Omitting name causes error -90012 even if value is present (QBO multicurrency validation).
            var payload = new
            {
                Name = template.Name,
                AccountType = template.AccountType,
                AccountSubType = template.AccountSubType,
                AcctNum = template.AccountNumber,
                Description = template.Description,
                Active = template.Active,
                CurrencyRef = new { value = "USD", name = "United States Dollar" },
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            var endpoint = GetCreateAccountEndpoint(realmId);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.ParseAdd("application/json");

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
