using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;
using BusinessActivityLogRepository = WileyWidget.Business.Interfaces.IActivityLogRepository;
using ModelActivityLog = WileyWidget.Models.ActivityLog;

namespace WileyWidget.WinForms.Tests.Integration.Data;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Category", "SqlServer")]
public sealed class SqlRepositoryProofTests
{
    private const string SqlConnectionString = "Data Source=localhost\\SQLEXPRESS;Initial Catalog=WileyWidget;Integrated Security=True;Pooling=False;Encrypt=False;Trust Server Certificate=True";

    [Fact]
    public async Task AccountsAndBudgetRepositories_QuerySurface_ExecutesAgainstSqlServer()
    {
        await using var provider = BuildSqlServerProvider();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = GetRequired<AppDbContext>(services);

        (await db.Database.CanConnectAsync()).Should().BeTrue("the SQL proof suite requires a reachable SQL Server database");

        var sample = await LoadSampleDataAsync(db);
        var accountsRepository = GetRequired<IAccountsRepository>(services);
        var budgetRepository = GetRequired<IBudgetRepository>(services);
        var concreteBudgetRepository = GetRequired<BudgetRepository>(services);

        (await accountsRepository.GetAllAccountsAsync()).Should().NotBeNull();
        (await accountsRepository.GetAccountsByFundAsync(sample.FundType)).Should().NotBeNull();
        (await accountsRepository.GetAccountsByTypeAsync(sample.AccountType)).Should().NotBeNull();
        (await accountsRepository.GetAccountsByFundAndTypeAsync(sample.FundType, sample.AccountType)).Should().NotBeNull();
        await accountsRepository.GetAccountByIdAsync(sample.AccountId);
        (await accountsRepository.SearchAccountsAsync(sample.AccountSearchTerm)).Should().NotBeNull();
        (await accountsRepository.GetMonthlyRevenueAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();

        (await budgetRepository.GetBudgetHierarchyAsync(sample.FiscalYear)).Should().NotBeNull();
        (await budgetRepository.GetByFiscalYearAsync(sample.FiscalYear)).Should().NotBeNull();
        (await budgetRepository.GetByFundAsync(sample.FundId)).Should().NotBeNull();
        (await budgetRepository.GetByDepartmentAsync(sample.DepartmentId)).Should().NotBeNull();
        (await budgetRepository.GetByFundAndFiscalYearAsync(sample.FundId, sample.FiscalYear)).Should().NotBeNull();
        (await budgetRepository.GetByDepartmentAndFiscalYearAsync(sample.DepartmentId, sample.FiscalYear)).Should().NotBeNull();
        (await budgetRepository.GetSewerBudgetEntriesAsync(sample.FiscalYear)).Should().NotBeNull();
        (await budgetRepository.GetByDateRangeAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        await budgetRepository.GetByIdAsync(sample.BudgetEntryId);
        _ = await budgetRepository.ExistsAsync(sample.AccountNumber, sample.FiscalYear);
        (await budgetRepository.GetBudgetSummaryAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        (await budgetRepository.GetVarianceAnalysisAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        (await budgetRepository.GetDepartmentBreakdownAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        (await budgetRepository.GetFundAllocationsAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        (await budgetRepository.GetYearEndSummaryAsync(sample.FiscalYear)).Should().NotBeNull();
        (await budgetRepository.GetBudgetSummaryByEnterpriseAsync(sample.EnterpriseId, sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        (await budgetRepository.GetVarianceAnalysisByEnterpriseAsync(sample.EnterpriseId, sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        (await budgetRepository.GetDepartmentBreakdownByEnterpriseAsync(sample.EnterpriseId, sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        (await budgetRepository.GetFundAllocationsByEnterpriseAsync(sample.EnterpriseId, sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        (await budgetRepository.GetDataStatisticsAsync(sample.FiscalYear)).Should().NotBeNull();
        (await budgetRepository.GetRevenueAccountCountAsync(sample.FiscalYear)).Should().BeGreaterThanOrEqualTo(0);
        (await budgetRepository.GetExpenseAccountCountAsync(sample.FiscalYear)).Should().BeGreaterThanOrEqualTo(0);
        (await budgetRepository.GetTownOfWileyBudgetDataAsync()).Should().NotBeNull();
        (await budgetRepository.BulkUpdateActualsAsync(new Dictionary<string, decimal>(), sample.FiscalYear)).Should().BeGreaterThanOrEqualTo(0);
        (await budgetRepository.GetHistoricalBudgetSummaryAsync(3, sample.FiscalYear)).Should().NotBeNull();

        var pagedBudgetEntries = await concreteBudgetRepository.GetPagedAsync(1, 10, "AccountNumber", false, sample.FiscalYear);
        pagedBudgetEntries.Items.Should().NotBeNull();
        pagedBudgetEntries.TotalCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task MunicipalAccountsAndPayments_QuerySurface_ExecutesAgainstSqlServer()
    {
        await using var provider = BuildSqlServerProvider();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = GetRequired<AppDbContext>(services);
        var sample = await LoadSampleDataAsync(db);

        var municipalAccounts = GetRequired<IMunicipalAccountRepository>(services);
        var concreteMunicipalAccounts = GetRequired<MunicipalAccountRepository>(services);
        var payments = GetRequired<IPaymentRepository>(services);

        (await municipalAccounts.GetAllAsync()).Should().NotBeNull();
        await municipalAccounts.GetByIdAsync(sample.AccountId);
        await municipalAccounts.GetByAccountNumberAsync(sample.AccountNumber);
        (await municipalAccounts.GetByDepartmentAsync(sample.DepartmentId)).Should().NotBeNull();
        (await municipalAccounts.GetByFundAsync(sample.FundType)).Should().NotBeNull();
        (await municipalAccounts.GetByTypeAsync(sample.AccountType)).Should().NotBeNull();
        (await municipalAccounts.GetAllWithRelatedAsync()).Should().NotBeNull();
        await municipalAccounts.GetCurrentActiveBudgetPeriodAsync();
        (await municipalAccounts.GetCountAsync()).Should().BeGreaterThanOrEqualTo(0);
        await municipalAccounts.GetBudgetAnalysisAsync(sample.BudgetPeriodId);

        var pagedAccounts = await concreteMunicipalAccounts.GetPagedAsync(1, 10, "AccountNumber", false);
        pagedAccounts.Items.Should().NotBeNull();
        pagedAccounts.TotalCount.Should().BeGreaterThanOrEqualTo(0);
        (await concreteMunicipalAccounts.GetByFundClassAsync(FundClass.Governmental)).Should().NotBeNull();
        (await concreteMunicipalAccounts.GetByAccountTypeAsync(sample.AccountType)).Should().NotBeNull();
        (await concreteMunicipalAccounts.GetChildAccountsAsync(sample.AccountId)).Should().NotBeNull();
        (await concreteMunicipalAccounts.GetAccountHierarchyAsync(sample.AccountId)).Should().NotBeNull();
        (await concreteMunicipalAccounts.SearchByNameAsync(sample.AccountSearchTerm)).Should().NotBeNull();
        _ = await concreteMunicipalAccounts.AccountNumberExistsAsync(sample.AccountNumber);
        (await concreteMunicipalAccounts.GetBudgetAnalysisAsync()).Should().NotBeNull();
        await concreteMunicipalAccounts.SyncFromQuickBooksAsync(new List<Intuit.Ipp.Data.Account>());
        await concreteMunicipalAccounts.ImportChartOfAccountsAsync(new List<Intuit.Ipp.Data.Account>());

        (await payments.GetAllAsync()).Should().NotBeNull();
        (await payments.GetRecentAsync(10)).Should().NotBeNull();
        await payments.GetByIdAsync(sample.PaymentId);
        (await payments.GetByCheckNumberAsync(sample.PaymentCheckNumber)).Should().NotBeNull();
        (await payments.GetByPayeeAsync(sample.PaymentPayeeSearch)).Should().NotBeNull();
        (await payments.GetByDateRangeAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        (await payments.GetByAccountAsync(sample.AccountId)).Should().NotBeNull();
        (await payments.GetByVendorAsync(sample.VendorId)).Should().NotBeNull();
        (await payments.GetByStatusAsync(sample.PaymentStatus)).Should().NotBeNull();
        _ = await payments.CheckNumberExistsAsync(sample.PaymentCheckNumber);
        (await payments.GetTotalAmountAsync(sample.RangeStart, sample.RangeEnd)).Should().BeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public async Task UtilityRepositories_QuerySurface_ExecutesAgainstSqlServer()
    {
        await using var provider = BuildSqlServerProvider();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = GetRequired<AppDbContext>(services);
        var sample = await LoadSampleDataAsync(db);

        var utilityCustomers = GetRequired<IUtilityCustomerRepository>(services);
        var concreteUtilityCustomers = GetRequired<UtilityCustomerRepository>(services);
        var utilityBills = GetRequired<IUtilityBillRepository>(services);

        (await utilityCustomers.GetAllAsync()).Should().NotBeNull();
        await utilityCustomers.GetByIdAsync(sample.UtilityCustomerId);
        await utilityCustomers.GetByAccountNumberAsync(sample.UtilityCustomerAccountNumber);
        (await utilityCustomers.GetByCustomerTypeAsync(sample.CustomerType)).Should().NotBeNull();
        (await utilityCustomers.GetByServiceLocationAsync(sample.ServiceLocation)).Should().NotBeNull();
        (await utilityCustomers.GetActiveCustomersAsync()).Should().NotBeNull();
        (await utilityCustomers.GetCustomersWithBalanceAsync()).Should().NotBeNull();
        (await utilityCustomers.SearchAsync(sample.UtilityCustomerSearchTerm)).Should().NotBeNull();
        _ = await utilityCustomers.ExistsByAccountNumberAsync(sample.UtilityCustomerAccountNumber);
        (await utilityCustomers.GetCountAsync()).Should().BeGreaterThanOrEqualTo(0);
        (await utilityCustomers.GetCustomersOutsideCityLimitsAsync()).Should().NotBeNull();

        var pagedCustomers = await concreteUtilityCustomers.GetPagedAsync(1, 10, "AccountNumber", false);
        pagedCustomers.Items.Should().NotBeNull();
        pagedCustomers.TotalCount.Should().BeGreaterThanOrEqualTo(0);

        (await utilityBills.GetAllAsync()).Should().NotBeNull();
        await utilityBills.GetByIdAsync(sample.UtilityBillId);
        await utilityBills.GetByBillNumberAsync(sample.UtilityBillNumber);
        (await utilityBills.GetByCustomerIdAsync(sample.UtilityCustomerId)).Should().NotBeNull();
        (await utilityBills.GetByStatusAsync(sample.BillStatus)).Should().NotBeNull();
        (await utilityBills.GetOverdueBillsAsync()).Should().NotBeNull();
        (await utilityBills.GetBillsDueInRangeAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        (await utilityBills.GetUnpaidBillsByCustomerIdAsync(sample.UtilityCustomerId)).Should().NotBeNull();
        (await utilityBills.GetCustomerBalanceAsync(sample.UtilityCustomerId)).Should().BeGreaterThanOrEqualTo(0m);
        (await utilityBills.GetChargesByBillIdAsync(sample.UtilityBillId)).Should().NotBeNull();
        (await utilityBills.GetChargesByCustomerIdAsync(sample.UtilityCustomerId)).Should().NotBeNull();
        _ = await utilityBills.BillNumberExistsAsync(sample.UtilityBillNumber);
        (await utilityBills.GetCountAsync()).Should().BeGreaterThanOrEqualTo(0);
        (await utilityBills.GetBillsByDateRangeAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
    }

    [Fact]
    public async Task DepartmentEnterpriseAndVendorRepositories_QuerySurface_ExecutesAgainstSqlServer()
    {
        await using var provider = BuildSqlServerProvider();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = GetRequired<AppDbContext>(services);
        var sample = await LoadSampleDataAsync(db);

        var departments = GetRequired<IDepartmentRepository>(services);
        var concreteDepartments = GetRequired<DepartmentRepository>(services);
        var enterprises = GetRequired<IEnterpriseRepository>(services);
        var concreteEnterprises = GetRequired<EnterpriseRepository>(services);
        var vendors = GetRequired<IVendorRepository>(services);

        (await departments.GetAllAsync()).Should().NotBeNull();
        await departments.GetByIdAsync(sample.DepartmentId);
        await departments.GetByCodeAsync(sample.DepartmentCode);
        _ = await departments.ExistsByCodeAsync(sample.DepartmentCode);
        (await departments.GetRootDepartmentsAsync()).Should().NotBeNull();
        (await departments.GetChildDepartmentsAsync(sample.DepartmentId)).Should().NotBeNull();
        var pagedDepartments = await departments.GetPagedAsync(1, 10, "Name", false);
        pagedDepartments.Items.Should().NotBeNull();
        pagedDepartments.TotalCount.Should().BeGreaterThanOrEqualTo(0);
        (await concreteDepartments.GetQueryableAsync()).Should().NotBeNull();

        (await enterprises.GetAllAsync()).Should().NotBeNull();
        await enterprises.GetByIdAsync(sample.EnterpriseId);
        (await enterprises.GetByTypeAsync(sample.EnterpriseType)).Should().NotBeNull();
        (await enterprises.GetCountAsync()).Should().BeGreaterThanOrEqualTo(0);
        var pagedEnterprises = await concreteEnterprises.GetPagedAsync(1, 10, "Name", false);
        pagedEnterprises.Items.Should().NotBeNull();
        pagedEnterprises.TotalCount.Should().BeGreaterThanOrEqualTo(0);
        (await concreteEnterprises.GetAllIncludingDeletedAsync()).Should().NotBeNull();
        (await concreteEnterprises.GetQueryableAsync()).Should().NotBeNull();

        (await vendors.GetAllAsync()).Should().NotBeNull();
        (await vendors.GetActiveAsync()).Should().NotBeNull();
        await vendors.GetByIdAsync(sample.VendorId);
        await vendors.GetByNameAsync(sample.VendorName);
        (await vendors.SearchByNameAsync(sample.VendorSearchTerm)).Should().NotBeNull();
    }

    [Fact]
    public async Task AuditActivityAnalyticsAndScenarioRepositories_Surface_ExecutesAgainstSqlServer()
    {
        await using var provider = BuildSqlServerProvider();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = GetRequired<AppDbContext>(services);
        var sample = await LoadSampleDataAsync(db);

        var auditRepository = GetRequired<IAuditRepository>(services);
        var activityRepository = GetRequired<BusinessActivityLogRepository>(services);
        var budgetAnalyticsRepository = GetRequired<IBudgetAnalyticsRepository>(services);
        var scenarioRepository = GetRequired<IScenarioSnapshotRepository>(services);

        var uniqueToken = $"sql-proof-{Guid.NewGuid():N}";
        var cleanupSnapshotIds = new List<int>();

        try
        {
            (await auditRepository.GetAuditTrailAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
            (await auditRepository.GetAuditTrailForEntityAsync("MunicipalAccount", sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
            (await auditRepository.GetAuditTrailForEntityAsync("MunicipalAccount", sample.AccountId, sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();

            await auditRepository.AddAuditEntryAsync(new AuditEntry
            {
                EntityType = uniqueToken,
                EntityId = sample.AccountId,
                Action = "SQL_PROOF",
                Changes = "Smoke test audit write",
                Timestamp = DateTime.UtcNow,
                User = "sql-proof"
            });

            (await auditRepository.GetAuditTrailForEntityAsync(uniqueToken, sample.RangeStart, DateTime.UtcNow.AddMinutes(1)))
                .Should().Contain(entry => entry.EntityType == uniqueToken);

            (await activityRepository.GetRecentActivitiesAsync(0, 25)).Should().NotBeNull();

            await activityRepository.LogActivityAsync(new ModelActivityLog
            {
                Activity = $"SQL proof activity {uniqueToken}",
                Details = "Smoke test activity write",
                User = "sql-proof",
                Category = "Testing",
                Icon = "check",
                ActivityType = "SQL_PROOF",
                Timestamp = DateTime.UtcNow
            });

            (await activityRepository.GetRecentActivitiesAsync(0, 100))
                .Should().Contain(item => item.Activity.Contains(uniqueToken, StringComparison.Ordinal));

            (await budgetAnalyticsRepository.GetTopVariancesAsync(10, sample.FiscalYear)).Should().NotBeNull();
            (await budgetAnalyticsRepository.GetReserveHistoryAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
            (await budgetAnalyticsRepository.GetCategoryBreakdownAsync(sample.RangeStart, sample.RangeEnd, sample.EntityName)).Should().NotBeNull();
            (await budgetAnalyticsRepository.GetTrendAnalysisAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
            (await budgetAnalyticsRepository.GetBudgetOverviewDataAsync(sample.FiscalYear)).Should().NotBeNull();
            (await budgetAnalyticsRepository.GetBudgetMetricsAsync(sample.FiscalYear)).Should().NotBeNull();
            (await budgetAnalyticsRepository.GetSummaryKpisAsync(sample.FiscalYear)).Should().NotBeNull();
            (await budgetAnalyticsRepository.GetVarianceDetailsAsync(sample.FiscalYear)).Should().NotBeNull();

            var savedSnapshot = await scenarioRepository.SaveAsync(new SavedScenarioSnapshot
            {
                Name = $"SQL Proof {uniqueToken}",
                Description = "Repository smoke test snapshot",
                RateIncreasePercent = 1.25m,
                ExpenseIncreasePercent = 0.75m,
                RevenueTarget = 10000m,
                ProjectedValue = 10500m,
                Variance = 500m
            });

            cleanupSnapshotIds.Add(savedSnapshot.Id);

            (await scenarioRepository.GetRecentAsync(20))
                .Should().Contain(snapshot => snapshot.Id == savedSnapshot.Id);
        }
        finally
        {
            await CleanupScenarioSnapshotsAsync(db, cleanupSnapshotIds);
            await CleanupAuditEntriesAsync(db, uniqueToken);
            await CleanupActivityLogsAsync(db, uniqueToken);
        }
    }

    [Fact]
    public async Task AnalyticsRepository_QuerySurface_ExecutesAgainstSqlServer()
    {
        await using var provider = BuildSqlServerProvider();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = GetRequired<AppDbContext>(services);
        var sample = await LoadSampleDataAsync(db);

        var analyticsRepository = GetRequired<IAnalyticsRepository>(services);
        var concreteAnalyticsRepository = GetRequired<AnalyticsRepository>(services);
        var enterprises = GetRequired<IEnterpriseRepository>(services);
        var uniqueToken = $"analytics-proof-{Guid.NewGuid():N}";
        var createdEnterpriseId = 0;

        try
        {
            var enterprise = await enterprises.AddAsync(new Enterprise
            {
                Name = $"Analytics Proof Enterprise {uniqueToken}",
                Type = "Water",
                CurrentRate = 12.50m,
                MonthlyExpenses = 7.25m,
                CitizenCount = 50,
                Description = "Temporary analytics SQL proof enterprise"
            });

            createdEnterpriseId = enterprise.Id;

            (await analyticsRepository.GetHistoricalReserveDataAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
            (await analyticsRepository.GetCurrentReserveBalanceAsync()).Should().BeGreaterThanOrEqualTo(0m);
            (await analyticsRepository.GetMunicipalAccountsAsync()).Should().NotBeNull();
            (await analyticsRepository.GetAvailableEntitiesAsync()).Should().NotBeNull();
            (await analyticsRepository.GetPortfolioCurrentRateAsync()).Should().NotBeNull();
            (await analyticsRepository.GetTrendDataAsync(2)).Should().NotBeNull();
            (await analyticsRepository.RunScenarioAsync(5m, 3m, 10000m)).Should().NotBeNull();
            (await concreteAnalyticsRepository.GetBudgetEntriesForVarianceAnalysisAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        }
        finally
        {
            await CleanupEnterpriseAsync(enterprises, createdEnterpriseId);
        }
    }

    [Fact]
    public async Task AnalyticsService_QuerySurface_ExecutesAgainstSqlServer()
    {
        await using var provider = BuildSqlServerProvider();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = GetRequired<AppDbContext>(services);
        var sample = await LoadSampleDataAsync(db);

        var analyticsService = GetRequired<IAnalyticsService>(services);
        var enterprises = GetRequired<IEnterpriseRepository>(services);
        var uniqueToken = $"analytics-service-proof-{Guid.NewGuid():N}";
        var createdEnterpriseId = 0;

        try
        {
            var enterprise = await enterprises.AddAsync(new Enterprise
            {
                Name = $"Analytics Service Proof Enterprise {uniqueToken}",
                Type = "Water",
                CurrentRate = 14.75m,
                MonthlyExpenses = 8.10m,
                CitizenCount = 60,
                Description = "Temporary analytics service SQL proof enterprise"
            });

            createdEnterpriseId = enterprise.Id;

            (await analyticsService.PerformExploratoryAnalysisAsync(sample.RangeStart, sample.RangeEnd, sample.EntityName)).Should().NotBeNull();
            (await analyticsService.RunRateScenarioAsync(new RateScenarioParameters
            {
                RateIncreasePercentage = 0.05m,
                ExpenseIncreasePercentage = 0.03m,
                RevenueTargetPercentage = 0.02m,
                ProjectionYears = 2
            })).Should().NotBeNull();
            (await analyticsService.GenerateReserveForecastAsync(1)).Should().NotBeNull();
            (await analyticsService.GetBudgetOverviewAsync(sample.FiscalYear)).Should().NotBeNull();
            (await analyticsService.GetBudgetMetricsAsync(sample.FiscalYear)).Should().NotBeNull();
            (await analyticsService.GetSummaryKpisAsync(sample.FiscalYear)).Should().NotBeNull();
            (await analyticsService.GetTrendDataAsync(2)).Should().NotBeNull();
            (await analyticsService.RunScenarioAsync(5m, 3m, 10000m)).Should().NotBeNull();
            (await analyticsService.GetVarianceDetailsAsync(sample.FiscalYear)).Should().NotBeNull();
        }
        finally
        {
            await CleanupEnterpriseAsync(enterprises, createdEnterpriseId);
        }
    }

    [Fact]
    public async Task SimpleCrudRepositories_RoundTripAgainstSqlServer()
    {
        await using var provider = BuildSqlServerProvider();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = GetRequired<AppDbContext>(services);

        var departments = GetRequired<IDepartmentRepository>(services);
        var enterprises = GetRequired<IEnterpriseRepository>(services);
        var vendors = GetRequired<IVendorRepository>(services);
        var payments = GetRequired<IPaymentRepository>(services);

        var uniqueToken = $"sql-proof-{Guid.NewGuid():N}";
        var createdDepartmentId = 0;
        var createdEnterpriseId = 0;
        var createdVendorId = 0;
        var createdPaymentId = 0;

        try
        {
            var department = new Department
            {
                Name = $"SQL Proof Department {uniqueToken}",
                DepartmentCode = $"DP{uniqueToken[..8].ToUpperInvariant()}"
            };

            await departments.AddAsync(department);
            createdDepartmentId = department.Id;
            createdDepartmentId.Should().BeGreaterThan(0);
            (await departments.GetByCodeAsync(department.DepartmentCode!))!.Id.Should().Be(createdDepartmentId);

            department.Name += " Updated";
            await departments.UpdateAsync(department);
            (await departments.GetByIdAsync(createdDepartmentId))!.Name.Should().Contain("Updated");

            var vendor = new Vendor
            {
                Name = $"SQL Proof Vendor {uniqueToken}",
                Email = $"{uniqueToken[..8]}@example.com",
                IsActive = true
            };

            var addedVendor = await vendors.AddAsync(vendor);
            createdVendorId = addedVendor.Id;
            createdVendorId.Should().BeGreaterThan(0);
            (await vendors.GetByNameAsync(vendor.Name))!.Id.Should().Be(createdVendorId);

            addedVendor.ContactInfo = "Updated contact";
            await vendors.UpdateAsync(addedVendor);
            (await vendors.GetByIdAsync(createdVendorId))!.ContactInfo.Should().Be("Updated contact");

            var enterprise = new Enterprise
            {
                Name = $"SQL Proof Enterprise {uniqueToken}",
                Type = "Water",
                CurrentRate = 10m,
                MonthlyExpenses = 5m,
                CitizenCount = 25,
                Description = "Repository SQL proof"
            };

            var addedEnterprise = await enterprises.AddAsync(enterprise);
            createdEnterpriseId = addedEnterprise.Id;
            createdEnterpriseId.Should().BeGreaterThan(0);
            (await enterprises.GetByIdAsync(createdEnterpriseId))!.Name.Should().Contain(uniqueToken);

            addedEnterprise.Description = "Updated repository SQL proof";
            var updatedEnterprise = await enterprises.UpdateAsync(addedEnterprise);
            updatedEnterprise.Description.Should().Be("Updated repository SQL proof");

            var payment = new Payment
            {
                CheckNumber = $"SQ{DateTime.UtcNow:MMddHHmmss}",
                PaymentDate = DateTime.UtcNow,
                Payee = $"SQL Proof Payee {uniqueToken[..8]}",
                Amount = 123.45m,
                Description = "Repository SQL proof payment",
                Status = "Pending"
            };

            var addedPayment = await payments.AddAsync(payment);
            createdPaymentId = addedPayment.Id;
            createdPaymentId.Should().BeGreaterThan(0);
            (await payments.GetByIdAsync(createdPaymentId))!.CheckNumber.Should().Be(payment.CheckNumber);

            addedPayment.Memo = "Updated memo";
            addedPayment.Status = "Cleared";
            var updatedPayment = await payments.UpdateAsync(addedPayment);
            updatedPayment.Status.Should().Be("Cleared");
            updatedPayment.Memo.Should().Be("Updated memo");
        }
        finally
        {
            await CleanupPaymentAsync(payments, createdPaymentId);
            await CleanupEnterpriseAsync(enterprises, createdEnterpriseId);
            await CleanupVendorAsync(vendors, createdVendorId);
            await CleanupDepartmentAsync(departments, createdDepartmentId);
        }
    }

    private static ServiceProvider BuildSqlServerProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = SqlConnectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Warning);
        });
        services.AddMemoryCache();
        services.AddSingleton(Mock.Of<ITelemetryService>());

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(SqlConnectionString));
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(SqlConnectionString), ServiceLifetime.Scoped);

        services.AddScoped<IAccountsRepository, AccountsRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IEnterpriseRepository, EnterpriseRepository>();
        services.AddScoped<IMunicipalAccountRepository, MunicipalAccountRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IVendorRepository, VendorRepository>();
        services.AddScoped<IUtilityBillRepository, UtilityBillRepository>();
        services.AddScoped<IUtilityCustomerRepository, UtilityCustomerRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<BusinessActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IBudgetAnalyticsRepository, BudgetAnalyticsRepository>();
        services.AddScoped<IScenarioSnapshotRepository, ScenarioSnapshotRepository>();

        services.AddScoped<AnalyticsRepository>(sp => (AnalyticsRepository)GetRequired<IAnalyticsRepository>(sp));
        services.AddScoped<BudgetRepository>(sp => (BudgetRepository)GetRequired<IBudgetRepository>(sp));
        services.AddScoped<DepartmentRepository>(sp => (DepartmentRepository)GetRequired<IDepartmentRepository>(sp));
        services.AddScoped<EnterpriseRepository>(sp => (EnterpriseRepository)GetRequired<IEnterpriseRepository>(sp));
        services.AddScoped<MunicipalAccountRepository>(sp => (MunicipalAccountRepository)GetRequired<IMunicipalAccountRepository>(sp));
        services.AddScoped<UtilityCustomerRepository>(sp => (UtilityCustomerRepository)GetRequired<IUtilityCustomerRepository>(sp));

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private static async Task<SampleData> LoadSampleDataAsync(AppDbContext db)
    {
        var rangeEnd = DateTime.UtcNow;
        var rangeStart = rangeEnd.AddYears(-5);

        var fiscalYear = await db.BudgetEntries
            .AsNoTracking()
            .OrderByDescending(entry => entry.FiscalYear)
            .Select(entry => (int?)entry.FiscalYear)
            .FirstOrDefaultAsync() ?? rangeEnd.Year;

        var department = await db.Departments
            .AsNoTracking()
            .OrderBy(department => department.Id)
            .FirstOrDefaultAsync();

        var enterprise = await db.Enterprises
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync();

        var vendor = await db.Vendors
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync();

        var account = await db.MunicipalAccounts
            .AsNoTracking()
            .Where(item => item.AccountNumber != null)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync();

        var budgetEntry = await db.BudgetEntries
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync();

        var activeBudgetPeriod = await db.BudgetPeriods
            .AsNoTracking()
            .OrderByDescending(item => item.IsActive)
            .ThenByDescending(item => item.Year)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync();

        var utilityCustomer = await db.UtilityCustomers
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync();

        var utilityBill = await db.UtilityBills
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync();

        var payment = await db.Payments
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync();

        return new SampleData(
            RangeStart: rangeStart,
            RangeEnd: rangeEnd,
            FiscalYear: fiscalYear,
            DepartmentId: department?.Id ?? 0,
            DepartmentCode: department?.DepartmentCode ?? "SQL-PROOF-DEPT",
            FundId: budgetEntry?.FundId ?? 0,
            EnterpriseId: enterprise?.Id ?? 0,
            EnterpriseType: enterprise?.Type ?? "Water",
            EntityName: budgetEntry?.EntityName,
            AccountId: account?.Id ?? 0,
            AccountNumber: account?.AccountNumber?.Value ?? "000.000",
            AccountSearchTerm: account?.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Water",
            FundType: account?.FundType ?? MunicipalFundType.General,
            AccountType: account?.Type ?? AccountType.Asset,
            BudgetEntryId: budgetEntry?.Id ?? 0,
            BudgetPeriodId: activeBudgetPeriod?.Id ?? 0,
            VendorId: vendor?.Id ?? 0,
            VendorName: vendor?.Name ?? "Vendor",
            VendorSearchTerm: vendor?.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Vendor",
            PaymentId: payment?.Id ?? 0,
            PaymentCheckNumber: payment?.CheckNumber ?? "MISSING-CHECK",
            PaymentPayeeSearch: payment?.Payee?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Payee",
            PaymentStatus: payment?.Status ?? "Pending",
            UtilityCustomerId: utilityCustomer?.Id ?? 0,
            UtilityCustomerAccountNumber: utilityCustomer?.AccountNumber ?? "MISSING-ACCOUNT",
            UtilityCustomerSearchTerm: utilityCustomer?.LastName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Customer",
            CustomerType: utilityCustomer?.CustomerType ?? CustomerType.Residential,
            ServiceLocation: utilityCustomer?.ServiceLocation ?? ServiceLocation.InsideCityLimits,
            UtilityBillId: utilityBill?.Id ?? 0,
            UtilityBillNumber: utilityBill?.BillNumber ?? "MISSING-BILL",
            BillStatus: utilityBill?.Status ?? BillStatus.Pending);
    }

    private static async Task CleanupScenarioSnapshotsAsync(AppDbContext db, IEnumerable<int> ids)
    {
        var snapshotIds = ids.Where(id => id > 0).Distinct().ToArray();
        if (snapshotIds.Length == 0)
        {
            return;
        }

        var snapshots = await db.Set<SavedScenarioSnapshot>()
            .Where(snapshot => snapshotIds.Contains(snapshot.Id))
            .ToListAsync();

        if (snapshots.Count == 0)
        {
            return;
        }

        db.RemoveRange(snapshots);
        await db.SaveChangesAsync();
    }

    private static async Task CleanupAuditEntriesAsync(AppDbContext db, string uniqueToken)
    {
        var entries = await db.AuditEntries
            .Where(entry => entry.EntityType == uniqueToken)
            .ToListAsync();

        if (entries.Count == 0)
        {
            return;
        }

        db.AuditEntries.RemoveRange(entries);
        await db.SaveChangesAsync();
    }

    private static async Task CleanupActivityLogsAsync(AppDbContext db, string uniqueToken)
    {
        var entries = await db.ActivityLogs
            .Where(entry => entry.Activity.Contains(uniqueToken))
            .ToListAsync();

        if (entries.Count == 0)
        {
            return;
        }

        db.ActivityLogs.RemoveRange(entries);
        await db.SaveChangesAsync();
    }

    private static async Task CleanupPaymentAsync(IPaymentRepository repository, int id)
    {
        if (id <= 0)
        {
            return;
        }

        try
        {
            await repository.DeleteAsync(id);
        }
        catch
        {
            // Best-effort cleanup for temp proof data.
        }
    }

    private static async Task CleanupEnterpriseAsync(IEnterpriseRepository repository, int id)
    {
        if (id <= 0)
        {
            return;
        }

        try
        {
            await repository.DeleteAsync(id);
        }
        catch
        {
            // Best-effort cleanup for temp proof data.
        }
    }

    private static async Task CleanupVendorAsync(IVendorRepository repository, int id)
    {
        if (id <= 0)
        {
            return;
        }

        try
        {
            await repository.DeleteAsync(id);
        }
        catch
        {
            // Best-effort cleanup for temp proof data.
        }
    }

    private static async Task CleanupDepartmentAsync(IDepartmentRepository repository, int id)
    {
        if (id <= 0)
        {
            return;
        }

        try
        {
            await repository.DeleteAsync(id);
        }
        catch
        {
            // Best-effort cleanup for temp proof data.
        }
    }

    private static T GetRequired<T>(IServiceProvider services)
        where T : notnull
    {
        return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<T>(services);
    }

    private sealed record SampleData(
        DateTime RangeStart,
        DateTime RangeEnd,
        int FiscalYear,
        int DepartmentId,
        string DepartmentCode,
        int FundId,
        int EnterpriseId,
        string EnterpriseType,
        string? EntityName,
        int AccountId,
        string AccountNumber,
        string AccountSearchTerm,
        MunicipalFundType FundType,
        AccountType AccountType,
        int BudgetEntryId,
        int BudgetPeriodId,
        int VendorId,
        string VendorName,
        string VendorSearchTerm,
        int PaymentId,
        string PaymentCheckNumber,
        string PaymentPayeeSearch,
        string PaymentStatus,
        int UtilityCustomerId,
        string UtilityCustomerAccountNumber,
        string UtilityCustomerSearchTerm,
        CustomerType CustomerType,
        ServiceLocation ServiceLocation,
        int UtilityBillId,
        string UtilityBillNumber,
        BillStatus BillStatus);
}
