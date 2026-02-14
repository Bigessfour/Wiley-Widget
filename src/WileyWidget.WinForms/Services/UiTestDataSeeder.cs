using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Services
{
    internal static class UiTestDataSeeder
    {
        private const string UiTestEnvVar = "WILEYWIDGET_UI_TESTS";

        public static async Task SeedIfEnabledAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled())
            {
                return;
            }

            // Create a scope for scoped services
            using var scope = services.CreateScope();
            var scopedServices = scope.ServiceProvider;

            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILoggerFactory>(scopedServices)?.CreateLogger("UiTestDataSeeder");
            var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDbContext>(scopedServices);

            try
            {
                await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

                var department = await db.Departments
                    .OrderByDescending(d => d.Id)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (department == null)
                {
                    department = new Department
                    {
                        Name = "General Government",
                        DepartmentCode = "GOV"
                    };
                    db.Departments.Add(department);
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                var period = await db.BudgetPeriods
                    .OrderByDescending(bp => bp.Year)
                    .ThenByDescending(bp => bp.CreatedDate)
                    .ThenByDescending(bp => bp.Id)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (period == null)
                {
                    var year = DateTime.UtcNow.Year;
                    period = new BudgetPeriod
                    {
                        Year = year,
                        Name = $"FY{year}",
                        CreatedDate = DateTime.UtcNow,
                        Status = BudgetStatus.Adopted,
                        StartDate = new DateTime(year, 1, 1),
                        EndDate = new DateTime(year, 12, 31),
                        IsActive = true
                    };
                    db.BudgetPeriods.Add(period);
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                if (!await db.MunicipalAccounts.AnyAsync(cancellationToken).ConfigureAwait(false))
                {
                    db.MunicipalAccounts.AddRange(
                        new MunicipalAccount
                        {
                            DepartmentId = department.Id,
                            BudgetPeriodId = period.Id,
                            AccountNumber = new AccountNumber("1000"),
                            Name = "General Fund Cash",
                            Type = AccountType.Asset,
                            TypeDescription = "Asset",
                            FundType = MunicipalFundType.General,
                            FundDescription = "General Fund",
                            Balance = 125000m,
                            BudgetAmount = 150000m,
                            IsActive = true
                        },
                        new MunicipalAccount
                        {
                            DepartmentId = department.Id,
                            BudgetPeriodId = period.Id,
                            AccountNumber = new AccountNumber("2000"),
                            Name = "Utility Revenue",
                            Type = AccountType.Revenue,
                            TypeDescription = "Revenue",
                            FundType = MunicipalFundType.Enterprise,
                            FundDescription = "Enterprise Fund",
                            Balance = 89000m,
                            BudgetAmount = 120000m,
                            IsActive = true
                        });
                }

                if (!await db.ActivityLogs.AnyAsync(cancellationToken).ConfigureAwait(false))
                {
                    db.ActivityLogs.AddRange(
                        new ActivityLog { Timestamp = DateTime.UtcNow.AddMinutes(-10), Activity = "Account Updated", Details = "GL-1000", User = "System" },
                        new ActivityLog { Timestamp = DateTime.UtcNow.AddMinutes(-30), Activity = "Report Generated", Details = "Budget Overview", User = "Scheduler" },
                        new ActivityLog { Timestamp = DateTime.UtcNow.AddHours(-1), Activity = "Customer Sync", Details = "5 new customers", User = "Integrator" }
                    );
                }

                if (!await db.UtilityCustomers.AnyAsync(cancellationToken).ConfigureAwait(false))
                {
                    db.UtilityCustomers.Add(
                        new UtilityCustomer
                        {
                            AccountNumber = "U-1001",
                            FirstName = "Alex",
                            LastName = "Rivera",
                            CompanyName = "Rivera Farms",
                            ServiceAddress = "123 Main St",
                            ServiceCity = "Wiley",
                            ServiceState = "CT",
                            ServiceZipCode = "06001",
                            MailingAddress = "123 Main St",
                            MailingCity = "Wiley",
                            MailingState = "CT",
                            MailingZipCode = "06001",
                            PhoneNumber = "860-555-1001",
                            EmailAddress = "alex.rivera@example.com",
                            CurrentBalance = 125.50m,
                            Status = CustomerStatus.Active,
                            AccountOpenDate = DateTime.UtcNow.AddYears(-2)
                        });
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger?.LogInformation("UI test data seeded for WinForms UI (in-memory: {InMemory})", true);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "UI test data seeding failed");
            }
        }

        private static bool IsEnabled() => string.Equals(Environment.GetEnvironmentVariable(UiTestEnvVar), "true", StringComparison.OrdinalIgnoreCase);
    }
}
