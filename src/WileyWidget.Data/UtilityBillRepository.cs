using System.Threading;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for UtilityBill data operations
/// </summary>
public class UtilityBillRepository : IUtilityBillRepository
{
    private static readonly ActivitySource ActivitySource = new("WileyWidget.Data.UtilityBillRepository");

    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<UtilityBillRepository> _logger;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public UtilityBillRepository(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<UtilityBillRepository> logger,
        IMemoryCache cache)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger.LogInformation("UtilityBillRepository constructed and DB factory injected");
    }

    /// <summary>
    /// Gets all utility bills
    /// </summary>
    public async Task<IEnumerable<UtilityBill>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("UtilityBillRepository.GetAll");
        activity?.SetTag("operation.type", "query");
        activity?.SetTag("cache.enabled", true);

        const string cacheKey = "UtilityBills_All";

        try
        {
            if (!_cache.TryGetValue(cacheKey, out IEnumerable<UtilityBill>? bills))
            {
                activity?.SetTag("cache.hit", false);
                _logger.LogDebug("Cache miss for all utility bills, fetching from database");

                await using var context = await _contextFactory.CreateDbContextAsync();
                bills = await context.UtilityBills
                    .Include(b => b.Customer)
                    .Include(b => b.Charges)
                    .AsNoTracking()
                    .OrderByDescending(b => b.BillDate)
                    .ToListAsync();

                var options = new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
                _cache.Set(cacheKey, bills, options);
                _logger.LogInformation("Cached {Count} utility bills for 5 minutes", bills?.Count() ?? 0);
            }
            else
            {
                activity?.SetTag("cache.hit", true);
                _logger.LogDebug("Returning {Count} utility bills from cache", bills?.Count() ?? 0);
            }

            activity?.SetTag("result.count", bills?.Count() ?? 0);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return bills!;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error retrieving all utility bills");
            throw;
        }
    }

    /// <summary>
    /// Gets a utility bill by ID
    /// </summary>
    public async Task<UtilityBill?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityBills
            .Include(b => b.Customer)
            .Include(b => b.Charges)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    /// <summary>
    /// Gets a utility bill by bill number
    /// </summary>
    public async Task<UtilityBill?> GetByBillNumberAsync(string billNumber, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityBills
            .Include(b => b.Customer)
            .Include(b => b.Charges)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BillNumber == billNumber);
    }

    /// <summary>
    /// Gets all bills for a specific customer
    /// </summary>
    public async Task<IEnumerable<UtilityBill>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"UtilityBills_Customer_{customerId}";

        if (!_cache.TryGetValue(cacheKey, out IEnumerable<UtilityBill>? bills))
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            bills = await context.UtilityBills
                .Where(b => b.CustomerId == customerId)
                .Include(b => b.Customer)
                .Include(b => b.Charges)
                .AsNoTracking()
                .OrderByDescending(b => b.BillDate)
                .ToListAsync();

            var options = new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            _cache.Set(cacheKey, bills, options);
        }

        return bills!;
    }

    /// <summary>
    /// Gets bills by status
    /// </summary>
    public async Task<IEnumerable<UtilityBill>> GetByStatusAsync(BillStatus status, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityBills
            .Where(b => b.Status == status)
            .Include(b => b.Customer)
            .Include(b => b.Charges)
            .AsNoTracking()
            .OrderByDescending(b => b.BillDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all overdue bills
    /// </summary>
    public async Task<IEnumerable<UtilityBill>> GetOverdueBillsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var today = DateTime.Today;

        return await context.UtilityBills
            .Where(b => b.Status == BillStatus.Overdue ||
                       (b.Status == BillStatus.Pending && b.DueDate < today))
            .Include(b => b.Customer)
            .Include(b => b.Charges)
            .AsNoTracking()
            .OrderBy(b => b.DueDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets bills due within a date range
    /// </summary>
    public async Task<IEnumerable<UtilityBill>> GetBillsDueInRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.UtilityBills
            .Where(b => b.DueDate >= startDate && b.DueDate <= endDate)
            .Include(b => b.Customer)
            .Include(b => b.Charges)
            .AsNoTracking()
            .OrderBy(b => b.DueDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all unpaid bills for a specific customer
    /// </summary>
    public async Task<IEnumerable<UtilityBill>> GetUnpaidBillsByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.UtilityBills
            .Where(b => b.CustomerId == customerId &&
                       (b.Status == BillStatus.Pending || b.Status == BillStatus.Overdue))
            .Include(b => b.Customer)
            .Include(b => b.Charges)
            .AsNoTracking()
            .OrderBy(b => b.BillDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets the total outstanding balance for a customer
    /// </summary>
    public async Task<decimal> GetCustomerBalanceAsync(int customerId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var unpaidBills = await context.UtilityBills
            .Where(b => b.CustomerId == customerId &&
                       (b.Status == BillStatus.Pending || b.Status == BillStatus.Overdue))
            .AsNoTracking()
            .ToListAsync();

        return unpaidBills.Sum(b => b.TotalAmount - b.AmountPaid);
    }

    /// <summary>
    /// Gets all charges for a specific bill
    /// </summary>
    public async Task<IEnumerable<Charge>> GetChargesByBillIdAsync(int billId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Charges
            .Where(c => c.BillId == billId)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets all charges for a customer's bills
    /// </summary>
    public async Task<IEnumerable<Charge>> GetChargesByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Charges
            .Where(c => c.Bill != null && c.Bill.CustomerId == customerId)
            .Include(c => c.Bill)
            .AsNoTracking()
            .OrderByDescending(c => c.Bill!.BillDate)
            .ToListAsync();
    }

    /// <summary>
    /// Adds a new utility bill
    /// </summary>
    public async Task<UtilityBill> AddAsync(UtilityBill bill, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bill);

        await using var context = await _contextFactory.CreateDbContextAsync();

        // Validate customer exists
        var customerExists = await context.UtilityCustomers
            .AnyAsync(c => c.Id == bill.CustomerId);

        if (!customerExists)
        {
            _logger.LogError("Cannot create bill for non-existent customer ID: {CustomerId}", bill.CustomerId);
            throw new InvalidOperationException($"Customer with ID {bill.CustomerId} does not exist");
        }

        context.UtilityBills.Add(bill);
        await context.SaveChangesAsync();

        // Invalidate cache
        _cache.Remove("UtilityBills_All");
        _cache.Remove($"UtilityBills_Customer_{bill.CustomerId}");

        _logger.LogInformation("Created utility bill {BillNumber} for customer {CustomerId}",
            bill.BillNumber, bill.CustomerId);

        return bill;
    }

    /// <summary>
    /// Adds a charge to a bill
    /// </summary>
    public async Task<Charge> AddChargeAsync(Charge charge, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(charge);

        await using var context = await _contextFactory.CreateDbContextAsync();

        // Validate bill exists
        var billExists = await context.UtilityBills
            .AnyAsync(b => b.Id == charge.BillId);

        if (!billExists)
        {
            _logger.LogError("Cannot create charge for non-existent bill ID: {BillId}", charge.BillId);
            throw new InvalidOperationException($"Bill with ID {charge.BillId} does not exist");
        }

        context.Charges.Add(charge);
        await context.SaveChangesAsync();

        // Invalidate cache
        var bill = await context.UtilityBills.FindAsync(charge.BillId);
        if (bill != null)
        {
            _cache.Remove("UtilityBills_All");
            _cache.Remove($"UtilityBills_Customer_{bill.CustomerId}");
        }

        _logger.LogInformation("Added charge {ChargeType} of {Amount:C} to bill {BillId}",
            charge.ChargeType, charge.Amount, charge.BillId);

        return charge;
    }

    /// <summary>
    /// Updates an existing utility bill
    /// </summary>
    public async Task<UtilityBill> UpdateAsync(UtilityBill bill, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bill);

        await using var context = await _contextFactory.CreateDbContextAsync();

        var existingBill = await context.UtilityBills
            .FirstOrDefaultAsync(b => b.Id == bill.Id);

        if (existingBill == null)
        {
            _logger.LogError("Cannot update non-existent bill ID: {BillId}", bill.Id);
            throw new InvalidOperationException($"Bill with ID {bill.Id} does not exist");
        }

        context.Entry(existingBill).CurrentValues.SetValues(bill);
        await context.SaveChangesAsync();

        // Invalidate cache
        _cache.Remove("UtilityBills_All");
        _cache.Remove($"UtilityBills_Customer_{bill.CustomerId}");

        _logger.LogInformation("Updated utility bill {BillNumber}", bill.BillNumber);

        return existingBill;
    }

    /// <summary>
    /// Deletes a utility bill by ID
    /// </summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var bill = await context.UtilityBills
            .Include(b => b.Charges)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bill == null)
        {
            _logger.LogWarning("Cannot delete non-existent bill ID: {BillId}", id);
            return false;
        }

        // Remove associated charges (cascade should handle this, but being explicit)
        if (bill.Charges?.Any() == true)
        {
            context.Charges.RemoveRange(bill.Charges);
        }

        context.UtilityBills.Remove(bill);
        await context.SaveChangesAsync();

        // Invalidate cache
        _cache.Remove("UtilityBills_All");
        _cache.Remove($"UtilityBills_Customer_{bill.CustomerId}");

        _logger.LogInformation("Deleted utility bill {BillNumber}", bill.BillNumber);

        return true;
    }

    /// <summary>
    /// Records a payment against a bill
    /// </summary>
    public async Task<bool> RecordPaymentAsync(int billId, decimal paymentAmount, DateTime paymentDate, CancellationToken cancellationToken = default)
    {
        if (paymentAmount <= 0)
        {
            _logger.LogError("Payment amount must be positive: {PaymentAmount}", paymentAmount);
            throw new ArgumentException("Payment amount must be positive", nameof(paymentAmount));
        }

        await using var context = await _contextFactory.CreateDbContextAsync();

        var bill = await context.UtilityBills.FindAsync(billId);

        if (bill == null)
        {
            _logger.LogError("Cannot record payment for non-existent bill ID: {BillId}", billId);
            return false;
        }

        bill.AmountPaid += paymentAmount;

        // Update status based on payment
        if (bill.AmountPaid >= bill.TotalAmount)
        {
            bill.Status = BillStatus.Paid;
            _logger.LogInformation("Bill {BillNumber} fully paid", bill.BillNumber);
        }
        else if (bill.Status == BillStatus.Overdue && bill.DueDate >= DateTime.Today)
        {
            // Payment made on overdue bill that's no longer past due
            bill.Status = BillStatus.Pending;
        }

        await context.SaveChangesAsync();

        // Invalidate cache
        _cache.Remove("UtilityBills_All");
        _cache.Remove($"UtilityBills_Customer_{bill.CustomerId}");

        _logger.LogInformation("Recorded payment of {PaymentAmount:C} for bill {BillNumber}",
            paymentAmount, bill.BillNumber);

        return true;
    }

    /// <summary>
    /// Checks if a bill number exists
    /// </summary>
    public async Task<bool> BillNumberExistsAsync(string billNumber, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.UtilityBills.Where(b => b.BillNumber == billNumber);

        if (excludeId.HasValue)
        {
            query = query.Where(b => b.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    /// <summary>
    /// Gets the total number of bills
    /// </summary>
    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityBills.CountAsync();
    }

    /// <summary>
    /// Gets bills created within a date range
    /// </summary>
    public async Task<IEnumerable<UtilityBill>> GetBillsByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.UtilityBills
            .Where(b => b.BillDate >= startDate && b.BillDate <= endDate)
            .Include(b => b.Customer)
            .Include(b => b.Charges)
            .AsNoTracking()
            .OrderBy(b => b.BillDate)
            .ToListAsync();
    }
}
