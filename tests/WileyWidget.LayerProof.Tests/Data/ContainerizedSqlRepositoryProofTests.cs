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
using WileyWidget.LayerProof.Tests.Data.E2E;
using WileyWidget.Models;
using WileyWidget.Models.Entities;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.LayerProof.Tests.Data;

[Trait("Category", "E2E")]
[Trait("Category", "LayerProof")]
public sealed class ContainerizedSqlRepositoryProofTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public ContainerizedSqlRepositoryProofTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CriticalRepositoryQuerySurface_ExecutesAgainstContainerizedSqlServer()
    {
        await _fixture.ResetDatabaseAsync();

        await using var provider = BuildProvider(_fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = GetRequired<AppDbContext>(services);

        (await db.Database.CanConnectAsync()).Should().BeTrue();

        var sample = await CreateProofDataAsync(db);

        var accounts = GetRequired<IAccountsRepository>(services);
        var budgets = GetRequired<IBudgetRepository>(services);
        var departments = GetRequired<IDepartmentRepository>(services);
        var enterprises = GetRequired<IEnterpriseRepository>(services);
        var municipalAccounts = GetRequired<IMunicipalAccountRepository>(services);
        var payments = GetRequired<IPaymentRepository>(services);
        var utilityBills = GetRequired<IUtilityBillRepository>(services);

        (await accounts.GetAllAccountsAsync()).Should().NotBeEmpty();
        (await accounts.GetAccountsByFundAsync(sample.FundType)).Should().NotBeEmpty();
        (await accounts.GetAccountsByTypeAsync(sample.AccountType)).Should().NotBeEmpty();
        (await accounts.GetAccountsByFundAndTypeAsync(sample.FundType, sample.AccountType)).Should().NotBeEmpty();
        (await accounts.GetAccountByIdAsync(sample.AccountId)).Should().NotBeNull();
        (await accounts.SearchAccountsAsync(sample.AccountSearchTerm)).Should().NotBeEmpty();
        (await accounts.GetMonthlyRevenueAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();

        (await budgets.GetBudgetHierarchyAsync(sample.FiscalYear)).Should().NotBeNull();
        (await budgets.GetByFiscalYearAsync(sample.FiscalYear)).Should().NotBeEmpty();
        (await budgets.GetByFundAsync(sample.FundId)).Should().NotBeNull();
        (await budgets.GetByDepartmentAsync(sample.DepartmentId)).Should().NotBeNull();
        (await budgets.GetByFundAndFiscalYearAsync(sample.FundId, sample.FiscalYear)).Should().NotBeNull();
        (await budgets.GetByDepartmentAndFiscalYearAsync(sample.DepartmentId, sample.FiscalYear)).Should().NotBeNull();
        (await budgets.GetByDateRangeAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        (await budgets.GetByIdAsync(sample.BudgetEntryId)).Should().NotBeNull();

        (await departments.GetAllAsync()).Should().NotBeEmpty();
        (await departments.GetByIdAsync(sample.DepartmentId)).Should().NotBeNull();
        (await departments.GetByCodeAsync(sample.DepartmentCode)).Should().NotBeNull();

        (await enterprises.GetAllAsync()).Should().NotBeEmpty();
        (await enterprises.GetByIdAsync(sample.EnterpriseId)).Should().NotBeNull();
        (await enterprises.GetByTypeAsync(sample.EnterpriseType)).Should().NotBeNull();

        (await municipalAccounts.GetAllAsync()).Should().NotBeEmpty();
        (await municipalAccounts.GetByIdAsync(sample.AccountId)).Should().NotBeNull();
        (await municipalAccounts.GetByAccountNumberAsync(sample.AccountNumber)).Should().NotBeNull();
        (await municipalAccounts.GetByFundAsync(sample.FundType)).Should().NotBeNull();
        (await municipalAccounts.GetByTypeAsync(sample.AccountType)).Should().NotBeNull();
        (await municipalAccounts.GetAllWithRelatedAsync()).Should().NotBeNull();

        (await payments.GetAllAsync()).Should().NotBeEmpty();
        (await payments.GetRecentAsync(10)).Should().NotBeEmpty();
        (await payments.GetByIdAsync(sample.PaymentId)).Should().NotBeNull();
        (await payments.GetByCheckNumberAsync(sample.PaymentCheckNumber)).Should().NotBeEmpty();
        (await payments.GetByPayeeAsync(sample.PaymentPayeeSearch)).Should().NotBeEmpty();
        (await payments.GetByDateRangeAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
        (await payments.GetByStatusAsync(sample.PaymentStatus)).Should().NotBeNull();

        (await utilityBills.GetAllAsync()).Should().NotBeEmpty();
        (await utilityBills.GetByIdAsync(sample.UtilityBillId)).Should().NotBeNull();
        (await utilityBills.GetByBillNumberAsync(sample.UtilityBillNumber)).Should().NotBeNull();
        (await utilityBills.GetByCustomerIdAsync(sample.UtilityCustomerId)).Should().NotBeNull();
        (await utilityBills.GetByStatusAsync(sample.BillStatus)).Should().NotBeNull();
        (await utilityBills.GetBillsByDateRangeAsync(sample.RangeStart, sample.RangeEnd)).Should().NotBeNull();
    }

    [Fact]
    public async Task ScenarioSnapshotRepository_SaveAndRead_ExecutesAgainstContainerizedSqlServer()
    {
        await _fixture.ResetDatabaseAsync();

        await using var provider = BuildProvider(_fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = GetRequired<AppDbContext>(services);
        var scenarios = GetRequired<IScenarioSnapshotRepository>(services);

        var uniqueToken = $"layer-proof-{Guid.NewGuid():N}";
        var saved = await scenarios.SaveAsync(new SavedScenarioSnapshot
        {
            Name = $"Data Layer Proof {uniqueToken}",
            Description = "Containerized SQL Server layer proof",
            RateIncreasePercent = 1.25m,
            ExpenseIncreasePercent = 0.75m,
            RevenueTarget = 10000m,
            ProjectedValue = 10500m,
            Variance = 500m,
        });

        try
        {
            saved.Id.Should().BeGreaterThan(0);

            var recent = await scenarios.GetRecentAsync(20);
            recent.Should().Contain(snapshot => snapshot.Id == saved.Id && snapshot.Name == saved.Name);

            var persisted = await db.SavedScenarioSnapshots
                .AsNoTracking()
                .SingleOrDefaultAsync(snapshot => snapshot.Id == saved.Id);

            persisted.Should().NotBeNull();
            persisted!.Description.Should().Be("Containerized SQL Server layer proof");
        }
        finally
        {
            await CleanupScenarioSnapshotsAsync(db, saved.Id);
        }
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Warning);
        });
        services.AddMemoryCache();
        services.AddSingleton(Mock.Of<ITelemetryService>());

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddDbContextFactory<AppDbContext>(options => options.UseSqlServer(connectionString), ServiceLifetime.Scoped);

        services.AddScoped<IAccountsRepository, AccountsRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IEnterpriseRepository, EnterpriseRepository>();
        services.AddScoped<IMunicipalAccountRepository, MunicipalAccountRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IUtilityBillRepository, UtilityBillRepository>();
        services.AddScoped<IScenarioSnapshotRepository, ScenarioSnapshotRepository>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    private static async Task<SampleData> CreateProofDataAsync(AppDbContext db)
    {
        var uniqueToken = $"LP{Guid.NewGuid():N}";
        var rangeEnd = DateTime.UtcNow;
        var rangeStart = rangeEnd.AddYears(-5);

        var fiscalYear = rangeEnd.Year;
        var periodStart = new DateTime(fiscalYear, 1, 1);
        var periodEnd = new DateTime(fiscalYear, 12, 31);

        var department = new Department
        {
            Name = $"Layer Proof Department {uniqueToken}",
            DepartmentCode = $"DP-{uniqueToken[..8]}",
        };

        var fund = new Fund
        {
            FundCode = $"100-{uniqueToken[..4]}",
            Name = $"Layer Proof General Fund {uniqueToken}",
            Type = FundType.GeneralFund,
        };

        var budgetPeriod = new BudgetPeriod
        {
            Year = fiscalYear,
            Name = $"{fiscalYear} Layer Proof",
            CreatedDate = DateTime.UtcNow,
            StartDate = periodStart,
            EndDate = periodEnd,
            Status = BudgetStatus.Adopted,
            IsActive = true,
        };

        db.Departments.Add(department);
        db.Funds.Add(fund);
        db.BudgetPeriods.Add(budgetPeriod);
        await db.SaveChangesAsync();

        var account = new MunicipalAccount
        {
            DepartmentId = department.Id,
            BudgetPeriodId = budgetPeriod.Id,
            FundId = fund.Id,
            AccountNumber = new AccountNumber("410.1"),
            Name = $"Layer Proof Account {uniqueToken}",
            Type = AccountType.Asset,
            TypeDescription = "Asset",
            FundType = MunicipalFundType.General,
            FundDescription = "General Fund",
            Balance = 2500m,
            BudgetAmount = 5000m,
        };

        var enterprise = new Enterprise
        {
            Name = $"Layer Proof Enterprise {uniqueToken}",
            Type = "Water",
            CurrentRate = 15.50m,
            MonthlyExpenses = 8.25m,
            CitizenCount = 75,
            Description = "Containerized layer proof enterprise",
        };

        var utilityCustomer = new UtilityCustomer
        {
            AccountNumber = $"UC-{uniqueToken[..8]}",
            FirstName = "Layer",
            LastName = "Proof",
            CustomerType = CustomerType.Residential,
            ServiceAddress = "123 Proof Lane",
            ServiceCity = "Wiley",
            ServiceState = "CO",
            ServiceZipCode = "81092",
            ServiceLocation = ServiceLocation.InsideCityLimits,
            Status = CustomerStatus.Active,
            AccountOpenDate = DateTime.UtcNow.AddMonths(-2),
            CurrentBalance = 0m,
        };

        db.MunicipalAccounts.Add(account);
        db.Enterprises.Add(enterprise);
        db.UtilityCustomers.Add(utilityCustomer);
        await db.SaveChangesAsync();

        var budgetEntry = new BudgetEntry
        {
            AccountNumber = account.AccountNumber!.Value,
            Description = $"Layer Proof Budget {uniqueToken}",
            BudgetedAmount = 12000m,
            ActualAmount = 6000m,
            Variance = 6000m,
            FiscalYear = fiscalYear,
            StartPeriod = periodStart,
            EndPeriod = periodEnd,
            FundType = FundType.GeneralFund,
            EncumbranceAmount = 100m,
            DepartmentId = department.Id,
            FundId = fund.Id,
            MunicipalAccountId = account.Id,
        };

        var payment = new Payment
        {
            CheckNumber = $"CHK-{uniqueToken[..8]}",
            PaymentDate = DateTime.UtcNow.AddDays(-2),
            Payee = $"Layer Proof Vendor {uniqueToken[..6]}",
            Amount = 321.45m,
            Description = "Containerized layer proof payment",
            MunicipalAccountId = account.Id,
            Status = "Pending",
        };

        var utilityBill = new UtilityBill
        {
            CustomerId = utilityCustomer.Id,
            BillNumber = $"BILL-{uniqueToken[..8]}",
            BillDate = DateTime.UtcNow.AddDays(-10),
            DueDate = DateTime.UtcNow.AddDays(20),
            PeriodStartDate = DateTime.UtcNow.AddMonths(-1),
            PeriodEndDate = DateTime.UtcNow,
            WaterCharges = 42.50m,
            SewerCharges = 18.25m,
            Status = BillStatus.Pending,
        };

        db.BudgetEntries.Add(budgetEntry);
        db.Payments.Add(payment);
        db.UtilityBills.Add(utilityBill);
        await db.SaveChangesAsync();

        return new SampleData(
            RangeStart: rangeStart,
            RangeEnd: rangeEnd,
            FiscalYear: fiscalYear,
            DepartmentId: department.Id,
            DepartmentCode: department.DepartmentCode ?? string.Empty,
            FundId: fund.Id,
            EnterpriseId: enterprise.Id,
            EnterpriseType: enterprise.Type,
            AccountId: account.Id,
            AccountNumber: account.AccountNumber!.Value,
            AccountSearchTerm: "Proof",
            FundType: account.FundType,
            AccountType: account.Type,
            BudgetEntryId: budgetEntry.Id,
            PaymentId: payment.Id,
            PaymentCheckNumber: payment.CheckNumber,
            PaymentPayeeSearch: "Proof",
            PaymentStatus: payment.Status,
            UtilityCustomerId: utilityCustomer.Id,
            UtilityBillId: utilityBill.Id,
            UtilityBillNumber: utilityBill.BillNumber,
            BillStatus: utilityBill.Status);
    }

    private static async Task CleanupScenarioSnapshotsAsync(AppDbContext db, params int[] ids)
    {
        var snapshotIds = ids.Where(id => id > 0).Distinct().ToArray();
        if (snapshotIds.Length == 0)
        {
            return;
        }

        var snapshots = await db.SavedScenarioSnapshots
            .Where(snapshot => snapshotIds.Contains(snapshot.Id))
            .ToListAsync();

        if (snapshots.Count == 0)
        {
            return;
        }

        db.SavedScenarioSnapshots.RemoveRange(snapshots);
        await db.SaveChangesAsync();
    }

    private static T GetRequired<T>(IServiceProvider services)
        where T : notnull
    {
        return ServiceProviderServiceExtensions.GetRequiredService<T>(services);
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
        int AccountId,
        string AccountNumber,
        string AccountSearchTerm,
        MunicipalFundType FundType,
        AccountType AccountType,
        int BudgetEntryId,
        int PaymentId,
        string PaymentCheckNumber,
        string PaymentPayeeSearch,
        string PaymentStatus,
        int UtilityCustomerId,
        int UtilityBillId,
        string UtilityBillNumber,
        BillStatus BillStatus);
}
