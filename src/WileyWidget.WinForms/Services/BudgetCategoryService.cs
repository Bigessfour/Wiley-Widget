using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.WinForms.Models;

namespace WileyWidget.WinForms.Services;

/// <summary>
/// Service for budget category CRUD operations
/// </summary>
public interface IBudgetCategoryService
{
    Task<List<BudgetCategoryDto>> GetAllCategoriesAsync(int fiscalYear, CancellationToken cancellationToken = default);
    Task<BudgetCategoryDto?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<BudgetCategoryDto> CreateCategoryAsync(BudgetCategoryDto category, CancellationToken cancellationToken = default);
    Task<BudgetCategoryDto> UpdateCategoryAsync(BudgetCategoryDto category, CancellationToken cancellationToken = default);
    Task<bool> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);
    Task<(decimal TotalBudget, decimal TotalActual, decimal TotalEncumbrance)> GetTotalsAsync(int fiscalYear, CancellationToken cancellationToken = default);
}

public class BudgetCategoryService : IBudgetCategoryService
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly AppDbContext _context;
    private readonly ILogger<BudgetCategoryService> _logger;

    public BudgetCategoryService(
        IBudgetRepository budgetRepository,
        AppDbContext context,
        ILogger<BudgetCategoryService> logger)
    {
        _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<BudgetCategoryDto>> GetAllCategoriesAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _budgetRepository.GetByFiscalYearAsync(fiscalYear);

            return entries.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get budget categories for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
    }

    public async Task<BudgetCategoryDto?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = await _context.BudgetEntries
                .Include(b => b.Department)
                .Include(b => b.Fund)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

            return entry == null ? null : MapToDto(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get budget category {Id}", id);
            throw;
        }
    }

    public async Task<BudgetCategoryDto> CreateCategoryAsync(BudgetCategoryDto category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        try
        {
            var entry = new BudgetEntry
            {
                AccountNumber = category.AccountNumber,
                Description = category.Category,
                BudgetedAmount = category.BudgetedAmount,
                ActualAmount = category.ActualAmount,
                EncumbranceAmount = category.EncumbranceAmount,
                FiscalYear = category.FiscalYear,
                Variance = category.Variance,
                CreatedAt = DateTime.UtcNow,
                StartPeriod = new DateTime(category.FiscalYear, 1, 1),
                EndPeriod = new DateTime(category.FiscalYear, 12, 31),
                IsGASBCompliant = true,
                // Assign to first available department (should be from UI)
                DepartmentId = await GetDefaultDepartmentIdAsync(cancellationToken),
                MunicipalAccountId = await GetDefaultMunicipalAccountIdAsync(cancellationToken)
            };

            _context.BudgetEntries.Add(entry);
            await _context.SaveChangesAsync(cancellationToken);

            category.Id = entry.Id;
            _logger.LogInformation("Created budget category {Id}: {Category}", entry.Id, category.Category);

            return category;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create budget category {Category}", category.Category);
            throw;
        }
    }

    public async Task<BudgetCategoryDto> UpdateCategoryAsync(BudgetCategoryDto category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        try
        {
            var entry = await _context.BudgetEntries.FindAsync(new object[] { category.Id }, cancellationToken);
            if (entry == null)
                throw new InvalidOperationException($"Budget category {category.Id} not found");

            entry.AccountNumber = category.AccountNumber;
            entry.Description = category.Category;
            entry.BudgetedAmount = category.BudgetedAmount;
            entry.ActualAmount = category.ActualAmount;
            entry.EncumbranceAmount = category.EncumbranceAmount;
            entry.Variance = category.Variance;
            entry.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated budget category {Id}: {Category}", entry.Id, category.Category);

            return category;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update budget category {Id}", category.Id);
            throw;
        }
    }

    public async Task<bool> DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = await _context.BudgetEntries.FindAsync(new object[] { id }, cancellationToken);
            if (entry == null)
                return false;

            _context.BudgetEntries.Remove(entry);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted budget category {Id}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete budget category {Id}", id);
            throw;
        }
    }

    public async Task<(decimal TotalBudget, decimal TotalActual, decimal TotalEncumbrance)> GetTotalsAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _budgetRepository.GetByFiscalYearAsync(fiscalYear);

            var totalBudget = entries.Sum(e => e.BudgetedAmount);
            var totalActual = entries.Sum(e => e.ActualAmount);
            var totalEncumbrance = entries.Sum(e => e.EncumbranceAmount);

            return (totalBudget, totalActual, totalEncumbrance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get totals for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
    }

    private static BudgetCategoryDto MapToDto(BudgetEntry entry)
    {
        return new BudgetCategoryDto
        {
            Id = entry.Id,
            AccountNumber = entry.AccountNumber,
            Category = entry.Description,
            BudgetedAmount = entry.BudgetedAmount,
            ActualAmount = entry.ActualAmount,
            EncumbranceAmount = entry.EncumbranceAmount,
            FiscalYear = entry.FiscalYear,
            DepartmentName = entry.Department?.Name ?? "Unknown",
            FundName = entry.Fund?.Name
        };
    }

    private async Task<int> GetDefaultDepartmentIdAsync(CancellationToken cancellationToken)
    {
        var department = await _context.Departments.FirstOrDefaultAsync(cancellationToken);
        if (department == null)
        {
            // Create default department if none exist
            department = new Department { Name = "General", DepartmentCode = "GEN" };
            _context.Departments.Add(department);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return department.Id;
    }

    private async Task<int> GetDefaultMunicipalAccountIdAsync(CancellationToken cancellationToken)
    {
        var account = await _context.MunicipalAccounts.FirstOrDefaultAsync(cancellationToken);
        if (account == null)
        {
            // Create default municipal account if none exist
            account = new MunicipalAccount { AccountNumber = new AccountNumber("000"), Name = "General Fund" };
            _context.MunicipalAccounts.Add(account);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return account.Id;
    }
}
