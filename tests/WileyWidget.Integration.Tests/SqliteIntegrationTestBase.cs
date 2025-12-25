using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.WinForms.Configuration;
using WileyWidget.Models;

namespace WileyWidget.Integration.Tests
{
    /// <summary>
    /// Base class for integration tests that require a SQLite in-memory database
    /// (supports concurrency semantics not available in the InMemory provider).
    /// </summary>
    public abstract class SqliteIntegrationTestBase : IAsyncLifetime
    {
        protected IServiceProvider Services { get; private set; } = null!;
        protected AppDbContext DbContext { get; private set; } = null!;
        private IServiceScope? _scope;
        private readonly Action<IServiceCollection>? _configureServices;
        private SqliteConnection? _connection;

        protected SqliteIntegrationTestBase(Action<IServiceCollection>? configureServices = null)
        {
            _configureServices = configureServices;
        }

        public async Task InitializeAsync()
        {
            // Keep the connection open for the lifetime of the test to allow shared in-memory DB
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var services = DependencyInjection.CreateServiceCollection(includeDefaults: false);

            if (!services.Any(sd => sd.ServiceType == typeof(IConfiguration)))
            {
                var defaultConfig = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                        ["Logging:LogLevel:Default"] = "Information"
                    })
                    .Build();
                services.AddSingleton<IConfiguration>(defaultConfig);
            }

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.AddSingleton(sp =>
            {
                var builder = new DbContextOptionsBuilder<AppDbContext>();
                builder.UseSqlite(_connection);
                return builder.Options;
            });

            services.AddDbContextFactory<AppDbContext>((sp, options) => options.UseSqlite(_connection));

            // Ensure logging and health checks are available for services under test
            services.AddLogging();
            services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("Database");

            _configureServices?.Invoke(services);

            Services = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });

            _scope = Services.CreateScope();
            DbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Create schema without migrations (use current model for in-memory SQLite)
            // Disable FK constraints for SQLite during schema creation
            await DbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            await DbContext.Database.EnsureCreatedAsync();
            await DbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

            // Seed required parent entities for FK constraints
            await SeedRequiredDataAsync();
        }

        private async Task SeedRequiredDataAsync()
        {
            // Seed BudgetPeriods if not exist
            if (!await DbContext.BudgetPeriods.AnyAsync())
            {
                DbContext.BudgetPeriods.AddRange(
                    new BudgetPeriod { Id = 1, Year = 2025, StartDate = new DateTime(2025, 1, 1), EndDate = new DateTime(2025, 12, 31), IsActive = true }
                );
            }

            // Seed Departments if not exist
            if (!await DbContext.Departments.AnyAsync())
            {
                DbContext.Departments.AddRange(
                    new Department { Id = 1, Name = "Administration", DepartmentCode = "ADM" },
                    new Department { Id = 2, Name = "Public Works", DepartmentCode = "PW" },
                    new Department { Id = 3, Name = "Police", DepartmentCode = "POL" },
                    new Department { Id = 4, Name = "Fire", DepartmentCode = "FIRE" },
                    new Department { Id = 5, Name = "Parks & Recreation", DepartmentCode = "PR" },
                    new Department { Id = 6, Name = "Finance", DepartmentCode = "FIN" },
                    new Department { Id = 7, Name = "Planning", DepartmentCode = "PLAN" },
                    new Department { Id = 8, Name = "Utilities", DepartmentCode = "UTIL" },
                    new Department { Id = 9, Name = "Economic Development", DepartmentCode = "ED" },
                    new Department { Id = 10, Name = "Human Resources", DepartmentCode = "HR" }
                );
            }

            // Seed MunicipalAccounts if not exist
            if (!await DbContext.MunicipalAccounts.AnyAsync())
            {
                DbContext.MunicipalAccounts.AddRange(
                    new MunicipalAccount { Id = 1, Name = "General Fund", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "100", Type = AccountType.Asset, TypeDescription = "Asset", Fund = MunicipalFundType.General, FundDescription = "General Fund", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 2, Name = "Special Revenue", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "101", Type = AccountType.Revenue, TypeDescription = "Revenue", Fund = MunicipalFundType.SpecialRevenue, FundDescription = "Special Revenue", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 3, Name = "Capital Projects", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "102", Type = AccountType.Asset, TypeDescription = "Asset", Fund = MunicipalFundType.CapitalProjects, FundDescription = "Capital Projects", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 4, Name = "Debt Service", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "103", Type = AccountType.Payables, TypeDescription = "Liability", Fund = MunicipalFundType.DebtService, FundDescription = "Debt Service", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 5, Name = "Enterprise", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "104", Type = AccountType.Asset, TypeDescription = "Asset", Fund = MunicipalFundType.Enterprise, FundDescription = "Enterprise", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 6, Name = "Internal Service", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "105", Type = AccountType.Asset, TypeDescription = "Asset", Fund = MunicipalFundType.InternalService, FundDescription = "Internal Service", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 7, Name = "Trust", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "106", Type = AccountType.Asset, TypeDescription = "Asset", Fund = MunicipalFundType.Trust, FundDescription = "Trust", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 8, Name = "Agency", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "107", Type = AccountType.Asset, TypeDescription = "Asset", Fund = MunicipalFundType.Agency, FundDescription = "Agency", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 9, Name = "Conservation Trust", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "108", Type = AccountType.Asset, TypeDescription = "Asset", Fund = MunicipalFundType.ConservationTrust, FundDescription = "Conservation Trust", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 10, Name = "Recreation", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "109", Type = AccountType.Revenue, TypeDescription = "Revenue", Fund = MunicipalFundType.Recreation, FundDescription = "Recreation", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 11, Name = "Utility", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "110", Type = AccountType.Asset, TypeDescription = "Asset", Fund = MunicipalFundType.Utility, FundDescription = "Utility", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 12, Name = "Water", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "111", Type = AccountType.Asset, TypeDescription = "Asset", Fund = MunicipalFundType.Water, FundDescription = "Water", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 13, Name = "Sewer", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "112", Type = AccountType.Asset, TypeDescription = "Asset", Fund = MunicipalFundType.Sewer, FundDescription = "Sewer", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 14, Name = "Trash", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "113", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.Trash, FundDescription = "Trash", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 15, Name = "Permits and Assessments", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "114", Type = AccountType.Revenue, TypeDescription = "Revenue", Fund = MunicipalFundType.General, FundDescription = "General Fund", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 16, Name = "Professional Services", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "115", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.General, FundDescription = "General Fund", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 17, Name = "Contract Labor", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "320", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.General, FundDescription = "General Fund", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 18, Name = "Dues and Subscriptions", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "323", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.General, FundDescription = "General Fund", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 19, Name = "Capital Outlay", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "325", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.CapitalProjects, FundDescription = "Capital Projects", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 20, Name = "Transfers", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "360", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.General, FundDescription = "General Fund", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 21, Name = "Salaries", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "370", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.General, FundDescription = "General Fund", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 22, Name = "Supplies", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "2111", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.General, FundDescription = "General Fund", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 23, Name = "Services", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "2112", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.General, FundDescription = "General Fund", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 24, Name = "Utilities", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "410", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.Enterprise, FundDescription = "Enterprise", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 25, Name = "Maintenance", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "420", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.Enterprise, FundDescription = "Enterprise", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 26, Name = "Insurance", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "425", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.Enterprise, FundDescription = "Enterprise", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 27, Name = "Depreciation", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "430", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.Enterprise, FundDescription = "Enterprise", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 28, Name = "Permits and Assessments", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "435", Type = AccountType.Revenue, TypeDescription = "Revenue", Fund = MunicipalFundType.Enterprise, FundDescription = "Enterprise", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 29, Name = "Professional Services", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "440", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.Enterprise, FundDescription = "Enterprise", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 30, Name = "Contract Labor", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "445", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.Enterprise, FundDescription = "Enterprise", Balance = 0, BudgetAmount = 0, IsActive = true },
                    new MunicipalAccount { Id = 31, Name = "Capital Outlay", DepartmentId = 1, BudgetPeriodId = 1, AccountNumber_Value = "450", Type = AccountType.Expense, TypeDescription = "Expense", Fund = MunicipalFundType.CapitalProjects, FundDescription = "Capital Projects", Balance = 0, BudgetAmount = 0, IsActive = true }
                );
            }

            await DbContext.SaveChangesAsync();
        }

        public async Task DisposeAsync()
        {
            if (_scope != null)
            {
                var db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureDeletedAsync();
                _scope.Dispose();
                _scope = null;
                DbContext = null!;
                Services = null!;
            }

            if (_connection != null)
            {
                _connection.Close();
                _connection.Dispose();
                _connection = null;
            }
        }

        protected IServiceScope CreateScope() => Services.CreateScope();

        [return: System.Diagnostics.CodeAnalysis.NotNull]
        protected T GetRequiredService<T>() where T : notnull
        {
            if (_scope != null)
            {
                return _scope.ServiceProvider.GetRequiredService<T>();
            }

            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<T>();
        }

        protected virtual Task SeedTestDataAsync() => Task.CompletedTask;

        public async Task ResetDatabaseAsync()
        {
            using var scope = CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }
    }
}
