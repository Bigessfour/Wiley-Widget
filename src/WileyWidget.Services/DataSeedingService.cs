#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Services
{
    /// <summary>
    /// Provides safe, idempotent data seeding helpers for development and test scenarios.
    /// Designed to be conservative (non-destructive by default) and suitable for CI/local use.
    /// </summary>
    public sealed class DataSeedingService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<DataSeedingService> _logger;

        public DataSeedingService(AppDbContext db, ILogger<DataSeedingService>? logger = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSeedingService>.Instance;
        }

        /// <summary>
        /// Seed budget-related sample data. Non-destructive by default; use <paramref name="force"/> to clear seeded tables first.
        /// </summary>
        /// <param name="force">When true, removes previously seeded budgets/accounts/periods/departments before seeding.</param>
        public async Task<SeedResult> SeedBudgetDataAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            // Ensure DB exists
            await _db.Database.EnsureCreatedAsync(cancellationToken);

            var beforeCount = await _db.BudgetEntries.CountAsync(cancellationToken).ConfigureAwait(false);
            if (beforeCount > 0 && !force)
            {
                _logger.LogInformation("Data seeding skipped - existing budget entries found ({Count})", beforeCount);
                return new SeedResult { ExistingRecords = beforeCount };
            }

            using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (force)
                {
                    // Conservative removal of known seedable collections only
                    _db.BudgetEntries.RemoveRange(_db.BudgetEntries);
                    _db.MunicipalAccounts.RemoveRange(_db.MunicipalAccounts);
                    _db.BudgetPeriods.RemoveRange(_db.BudgetPeriods);
                    // Keep departments removal optional and conservative (remove only empty ones)
                    var emptyDepartments = await _db.Departments
                        .Where(d => !_db.MunicipalAccounts.Any(ma => ma.DepartmentId == d.Id))
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);
                    _db.Departments.RemoveRange(emptyDepartments);

                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                // Ensure minimal domain objects exist
                var department = await _db.Departments.OrderBy(d => d.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                if (department == null)
                {
                    department = new Department
                    {
                        Name = "General Government",
                        DepartmentCode = "GOV"
                    };
                    _db.Departments.Add(department);
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                var period = await _db.BudgetPeriods.OrderBy(p => p.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
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
                    _db.BudgetPeriods.Add(period);
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                if (!await _db.MunicipalAccounts.AnyAsync(cancellationToken).ConfigureAwait(false))
                {
                    _db.MunicipalAccounts.AddRange(
                        new MunicipalAccount
                        {
                            DepartmentId = department.Id,
                            BudgetPeriodId = period.Id,
                            AccountNumber = new AccountNumber("1000"),
                            Name = "General Fund Cash",
                            Type = AccountType.Asset,
                            TypeDescription = "Asset",
                            Fund = MunicipalFundType.General,
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
                            Fund = MunicipalFundType.Enterprise,
                            FundDescription = "Enterprise Fund",
                            Balance = 89000m,
                            BudgetAmount = 120000m,
                            IsActive = true
                        });

                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                var accounts = await _db.MunicipalAccounts.Take(10).ToListAsync(cancellationToken).ConfigureAwait(false);

                if (!await _db.BudgetEntries.AnyAsync(cancellationToken).ConfigureAwait(false))
                {
                    var rnd = new Random(123);
                    var entries = new List<BudgetEntry>();
                    for (int i = 0; i < 50; i++)
                    {
                        var acc = accounts[i % accounts.Count];
                        entries.Add(new BudgetEntry
                        {
                            AccountNumber = $"{100 + (i % 900)}",
                            Description = $"Seeded budget entry #{i + 1}",
                            BudgetedAmount = decimal.Round((decimal)(rnd.NextDouble() * 100000.0), 2),
                            ActualAmount = 0m,
                            Variance = 0m,
                            FiscalYear = period.Year,
                            StartPeriod = period.StartDate,
                            EndPeriod = period.EndDate,
                            FundType = FundType.GeneralFund,
                            DepartmentId = department.Id,
                            MunicipalAccountId = acc.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    _db.BudgetEntries.AddRange(entries);
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

                var afterCount = await _db.BudgetEntries.CountAsync(cancellationToken).ConfigureAwait(false);
                var inserted = Math.Max(0, afterCount - beforeCount);
                _logger.LogInformation("Data seeding finished. Budget entries: {Total} (inserted {Inserted})", afterCount, inserted);

                return new SeedResult { ExistingRecords = beforeCount, InsertedRecords = inserted };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while seeding data");
                try { await tx.RollbackAsync(cancellationToken).ConfigureAwait(false); } catch { }
                throw;
            }
        }
    }

    public sealed class SeedResult
    {
        public int ExistingRecords { get; set; }
        public int InsertedRecords { get; set; }
        public int TotalRecords => ExistingRecords + InsertedRecords;
        public bool Skipped => InsertedRecords == 0 && ExistingRecords > 0;
    }
}
