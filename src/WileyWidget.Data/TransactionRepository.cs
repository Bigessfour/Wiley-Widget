#nullable enable

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WileyWidget.Models;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for Transaction data operations
/// </summary>
public class TransactionRepository : ITransactionRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private readonly ITelemetryService? _telemetryService;

    // Activity source for repository-level telemetry
    private static readonly ActivitySource ActivitySource = new("WileyWidget.Data.TransactionRepository");

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public TransactionRepository(
        IDbContextFactory<AppDbContext> contextFactory,
        IMemoryCache cache,
        ITelemetryService? telemetryService = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _telemetryService = telemetryService;
    }

    /// <summary>
    /// Safely attempts to get a value from cache, handling disposed cache gracefully
    /// </summary>
    private bool TryGetFromCache<T>(string key, out T? value)
    {
        try
        {
            return _cache.TryGetValue(key, out value);
        }
        catch (ObjectDisposedException)
        {
            value = default;
            return false;
        }
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsForBudgetEntryAsync(int budgetEntryId)
    {
        using var activity = ActivitySource.StartActivity("GetTransactionsForBudgetEntry");

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Transactions
            .Where(t => t.BudgetEntryId == budgetEntryId)
            .ToListAsync();
    }

    public async Task AddAsync(Transaction transaction)
    {
        using var activity = ActivitySource.StartActivity("AddTransaction");

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();
    }

    public async Task<Transaction?> GetByIdAsync(int id)
    {
        using var activity = ActivitySource.StartActivity("GetTransactionById");

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Transactions.FindAsync(id);
    }

    public async Task UpdateAsync(Transaction transaction)
    {
        using var activity = ActivitySource.StartActivity("UpdateTransaction");

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Transactions.Update(transaction);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var activity = ActivitySource.StartActivity("DeleteTransaction");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var transaction = await context.Transactions.FindAsync(id);
        if (transaction != null)
        {
            context.Transactions.Remove(transaction);
            await context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Transaction>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        using var activity = ActivitySource.StartActivity("GetTransactionsByDateRange");

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Transactions
            .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate)
            .ToListAsync();
    }

    public async Task<Transaction?> GetByIdWithIncludesAsync(int id)
    {
        using var activity = ActivitySource.StartActivity("GetTransactionByIdWithIncludes");

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Transactions
            .Include(t => t.BudgetEntry)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Transaction>> GetAllAsync()
    {
        using var activity = ActivitySource.StartActivity("GetAllTransactions");

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Transactions
            .Include(t => t.BudgetEntry)
            .ToListAsync();
    }

    public async Task BulkInsertAsync(IEnumerable<Transaction> transactions)
    {
        using var activity = ActivitySource.StartActivity("BulkInsertTransactions");

        await using var context = await _contextFactory.CreateDbContextAsync();
        await context.Transactions.AddRangeAsync(transactions);
        await context.SaveChangesAsync();
    }
}
