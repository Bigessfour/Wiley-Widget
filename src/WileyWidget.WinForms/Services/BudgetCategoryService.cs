using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.WinForms.Models;

namespace WileyWidget.WinForms.Services
{
    public class BudgetCategoryService : IBudgetCategoryService
    {
        private const int DefaultRetryCount = 3;
        private readonly IBudgetRepository _budgetRepository;
        private readonly AppDbContext _context;
        private readonly ILogger _logger;

        public BudgetCategoryService(IBudgetRepository budgetRepository, AppDbContext context, ILogger logger)
        {
            _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger?.ForContext<BudgetCategoryService>() ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<BudgetCategoryDto>> GetAllCategoriesAsync(int fiscalYear, CancellationToken cancellationToken = default)
        {
            var entries = await ExecuteWithRetryAsync(() => _budgetRepository.GetByFiscalYearAsync(fiscalYear), cancellationToken);
            return entries.Select(MapToDto).ToList();
        }

        public async Task<(decimal TotalBudget, decimal TotalActual, decimal TotalEncumbrance)> GetTotalsAsync(int fiscalYear, CancellationToken cancellationToken = default)
        {
            var entries = await ExecuteWithRetryAsync(() => _budgetRepository.GetByFiscalYearAsync(fiscalYear), cancellationToken);
            var list = entries.ToList();
            var totalBudget = list.Sum(e => e.BudgetedAmount);
            var totalActual = list.Sum(e => e.ActualAmount);
            var totalEncumbrance = list.Sum(e => e.EncumbranceAmount);
            return (totalBudget, totalActual, totalEncumbrance);
        }

        public async Task<BudgetCategoryDto?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = await _context.BudgetEntries
                .Include(e => e.Department)
                .Include(e => e.Fund)
                .Include(e => e.MunicipalAccount)
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

            return entry == null ? null : MapToDto(entry);
        }

        public async Task<BudgetCategoryDto> CreateCategoryAsync(BudgetCategoryDto category, CancellationToken cancellationToken = default)
        {
            if (category is null)
            {
                throw new ArgumentNullException(nameof(category));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var department = await EnsureDefaultDepartmentAsync(cancellationToken);
            var municipalAccount = await EnsureDefaultMunicipalAccountAsync(department, cancellationToken);

            var entry = new BudgetEntry
            {
                Description = category.Category ?? string.Empty,
                AccountNumber = category.AccountNumber ?? string.Empty,
                BudgetedAmount = category.BudgetedAmount,
                ActualAmount = category.ActualAmount,
                EncumbranceAmount = category.EncumbranceAmount,
                FiscalYear = category.FiscalYear,
                DepartmentId = department.Id,
                MunicipalAccountId = municipalAccount.Id,
                FundType = FundType.GeneralFund
            };

            _context.BudgetEntries.Add(entry);
            await _context.SaveChangesAsync(cancellationToken);

            // Reload with relationships to populate names
            var saved = await _context.BudgetEntries
                .Include(e => e.Department)
                .Include(e => e.Fund)
                .Include(e => e.MunicipalAccount)
                .FirstAsync(e => e.Id == entry.Id, cancellationToken);

            return MapToDto(saved);
        }

        public async Task<BudgetCategoryDto> UpdateCategoryAsync(BudgetCategoryDto category, CancellationToken cancellationToken = default)
        {
            if (category is null)
            {
                throw new ArgumentNullException(nameof(category));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var entry = await _context.BudgetEntries.FindAsync(new object[] { category.Id }, cancellationToken);
            if (entry is null)
            {
                throw new InvalidOperationException($"Budget category with id {category.Id} not found");
            }

            entry.Description = category.Category ?? string.Empty;
            entry.AccountNumber = category.AccountNumber ?? string.Empty;
            entry.BudgetedAmount = category.BudgetedAmount;
            entry.ActualAmount = category.ActualAmount;
            entry.EncumbranceAmount = category.EncumbranceAmount;
            entry.FiscalYear = category.FiscalYear;
            entry.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            var updated = await _context.BudgetEntries
                .Include(e => e.Department)
                .Include(e => e.Fund)
                .Include(e => e.MunicipalAccount)
                .FirstAsync(e => e.Id == entry.Id, cancellationToken);

            return MapToDto(updated);
        }

        public async Task<bool> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = await _context.BudgetEntries.FindAsync(new object[] { id }, cancellationToken);
            if (entry is null)
            {
                return false;
            }

            _context.BudgetEntries.Remove(entry);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task<IEnumerable<BudgetEntry>> ExecuteWithRetryAsync(Func<Task<IEnumerable<BudgetEntry>>> action, CancellationToken cancellationToken)
        {
            var attempt = 0;
            TimeoutException? lastTimeout = null;

            while (attempt < DefaultRetryCount)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await action();
                }
                catch (TimeoutException ex)
                {
                    lastTimeout = ex;
                    attempt++;
                    _logger.Warning(ex, "Transient failure while retrieving budget categories (attempt {Attempt}/{MaxAttempts})", attempt, DefaultRetryCount);
                    if (attempt >= DefaultRetryCount)
                    {
                        throw;
                    }
                }
            }

            if (lastTimeout != null)
            {
                throw lastTimeout;
            }

            return Enumerable.Empty<BudgetEntry>();
        }

        private async Task<Department> EnsureDefaultDepartmentAsync(CancellationToken cancellationToken)
        {
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.Name == "General", cancellationToken)
                              ?? await _context.Departments.FirstOrDefaultAsync(cancellationToken);

            if (department != null)
            {
                return department;
            }

            var defaultDepartment = new Department
            {
                Name = "General",
                DepartmentCode = "GEN"
            };

            _context.Departments.Add(defaultDepartment);
            await _context.SaveChangesAsync(cancellationToken);
            return defaultDepartment;
        }

        private async Task<BudgetPeriod> EnsureBudgetPeriodAsync(CancellationToken cancellationToken)
        {
            var period = await _context.BudgetPeriods.OrderBy(bp => bp.Id).FirstOrDefaultAsync(cancellationToken);
            if (period != null)
            {
                return period;
            }

            var currentYear = DateTime.UtcNow.Year;
            var defaultPeriod = new BudgetPeriod
            {
                Year = currentYear,
                Name = $"{currentYear} Default",
                CreatedDate = DateTime.UtcNow,
                Status = BudgetStatus.Draft,
                StartDate = new DateTime(currentYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(currentYear, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                IsActive = true
            };

            _context.BudgetPeriods.Add(defaultPeriod);
            await _context.SaveChangesAsync(cancellationToken);
            return defaultPeriod;
        }

        private async Task<MunicipalAccount> EnsureDefaultMunicipalAccountAsync(Department department, CancellationToken cancellationToken)
        {
            var account = await _context.MunicipalAccounts
                .Include(ma => ma.AccountNumber)
                .FirstOrDefaultAsync(ma => ma.Name == "General Fund", cancellationToken)
                ?? await _context.MunicipalAccounts
                    .Include(ma => ma.AccountNumber)
                    .FirstOrDefaultAsync(cancellationToken);

            if (account != null)
            {
                return account;
            }

            var period = await EnsureBudgetPeriodAsync(cancellationToken);

            var defaultAccount = new MunicipalAccount
            {
                Name = "General Fund",
                DepartmentId = department.Id,
                BudgetPeriodId = period.Id,
                AccountNumber = new AccountNumber("0000"),
                Fund = MunicipalFundType.General,
                FundDescription = "General Fund",
                Type = AccountType.Cash,
                TypeDescription = "Cash",
                IsActive = true
            };

            _context.MunicipalAccounts.Add(defaultAccount);
            await _context.SaveChangesAsync(cancellationToken);
            return defaultAccount;
        }

        private static BudgetCategoryDto MapToDto(BudgetEntry entry)
        {
            return new BudgetCategoryDto
            {
                Id = entry.Id,
                Category = entry.Description ?? string.Empty,
                AccountNumber = entry.AccountNumber,
                BudgetedAmount = entry.BudgetedAmount,
                ActualAmount = entry.ActualAmount,
                EncumbranceAmount = entry.EncumbranceAmount,
                FiscalYear = entry.FiscalYear,
                DepartmentName = entry.Department?.Name ?? string.Empty,
                FundName = entry.Fund?.Name
            };
        }
    }
}
