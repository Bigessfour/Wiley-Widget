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
            // Use a lightweight retry strategy for transient failures (e.g. timeouts/network blips)
            var entries = await SafeExecuteAsync(
                () => _budgetRepository.GetByFiscalYearAsync(fiscalYear),
                $"BudgetRepository.GetByFiscalYearAsync({fiscalYear})",
                cancellationToken: cancellationToken);

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
                .Include(b => b.MunicipalAccount)
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
            // Reuse the same retry helper for totals so transient repository failures are retried
            var entries = await SafeExecuteAsync(
                () => _budgetRepository.GetByFiscalYearAsync(fiscalYear),
                $"BudgetRepository.GetByFiscalYearAsync({fiscalYear})",
                cancellationToken: cancellationToken);

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

    /// <summary>
    /// Lightweight, local retry helper used to retry transient operations.
    /// Keeps behavior local and predictable for services that do not opt into a global resilience pipeline.
    /// </summary>
    private async Task<T> SafeExecuteAsync<T>(Func<Task<T>> operation, string operationName, int maxRetries = 2, CancellationToken cancellationToken = default)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        var attempt = 0;
        Exception? lastException = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt < maxRetries)
            {
                lastException = ex;
                attempt++;
                var delayMs = (int)(Math.Pow(2, attempt) * 100); // 200ms, 400ms, ...
                _logger.LogWarning(ex, "{Operation} failed (attempt {Attempt}/{MaxRetries}). Retrying in {DelayMs}ms.",
                    operationName, attempt, maxRetries, delayMs);

                try
                {
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (OperationCanceledException) // preserve cancellation semantics
                {
                    throw;
                }

                continue;
            }
            catch (Exception ex)
            {
                // Non-transient or retries exhausted — bubble up
                _logger.LogDebug(ex, "{Operation} failed permanently after {Attempt} attempts.", operationName, attempt);
                throw;
            }
        }
    }

    private static bool IsTransientError(Exception ex)
    {
        // Conservative set of transient errors — expand later if needed
        if (ex is TimeoutException) return true;
        if (ex is TaskCanceledException) return true;
        if (ex is OperationCanceledException) return false; // cancellation is not transient
        if (ex is System.Net.Http.HttpRequestException) return true;
        if (ex is DbUpdateException) return true; // EF transient write issues

        // Fallback: treat common timeout hints as transient
        if (ex.Message?.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        return false;
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
            FundName = entry.Fund?.Name ?? entry.MunicipalAccount?.Name
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
            // Ensure a default department exists and set DepartmentId for the new municipal account
            var defaultDeptId = await GetDefaultDepartmentIdAsync(cancellationToken);
            account = new MunicipalAccount { AccountNumber = new AccountNumber("000"), Name = "General Fund", DepartmentId = defaultDeptId };
            _context.MunicipalAccounts.Add(account);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return account.Id;
    }
}
