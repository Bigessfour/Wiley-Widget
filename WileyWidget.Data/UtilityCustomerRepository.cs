using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for UtilityCustomer data operations
/// </summary>
public class UtilityCustomerRepository : IUtilityCustomerRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly Microsoft.Extensions.Logging.ILogger<UtilityCustomerRepository> _logger;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public UtilityCustomerRepository(IDbContextFactory<AppDbContext> contextFactory, Microsoft.Extensions.Logging.ILogger<UtilityCustomerRepository> logger, IMemoryCache cache)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger.LogInformation("UtilityCustomerRepository constructed and DB factory injected");
    }

    /// <summary>
    /// Gets all utility customers
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetAllAsync()
    {
        const string cacheKey = "UtilityCustomers_All";

        if (!_cache.TryGetValue(cacheKey, out IEnumerable<UtilityCustomer>? customers))
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            customers = await context.UtilityCustomers
                .AsNoTracking()
                .OrderBy(c => c.AccountNumber)
                .ToListAsync();

            _cache.Set(cacheKey, customers, TimeSpan.FromMinutes(10));
        }

        return customers!;
    }

    /// <summary>
    /// Gets paged utility customers with sorting support
    /// </summary>
    public async Task<(IEnumerable<UtilityCustomer> Items, int TotalCount)> GetPagedAsync(
        int pageNumber = 1,
        int pageSize = 50,
        string? sortBy = null,
        bool sortDescending = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.UtilityCustomers.AsQueryable();

        // Apply sorting
        query = ApplySorting(query, sortBy, sortDescending);

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply paging
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>
    /// Gets an IQueryable for flexible querying and paging
    /// </summary>
    public async Task<IQueryable<UtilityCustomer>> GetQueryableAsync()
    {
        var context = await _contextFactory.CreateDbContextAsync();
        return context.UtilityCustomers.AsQueryable();
    }

    /// <summary>
    /// Gets a utility customer by ID
    /// </summary>
    public async Task<UtilityCustomer?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>
    /// Gets a utility customer by account number
    /// </summary>
    public async Task<UtilityCustomer?> GetByAccountNumberAsync(string accountNumber)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.AccountNumber == accountNumber);
    }

    /// <summary>
    /// Gets customers by customer type
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetByCustomerTypeAsync(CustomerType customerType)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .Where(c => c.CustomerType == customerType)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets customers by service location
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetByServiceLocationAsync(ServiceLocation serviceLocation)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .Where(c => c.ServiceLocation == serviceLocation)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets all active customers
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetActiveCustomersAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .Where(c => c.Status == CustomerStatus.Active)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets customers with outstanding balances
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetCustomersWithBalanceAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .Where(c => c.CurrentBalance > 0)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Searches customers by name or account number
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> SearchAsync(string searchTerm)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // If search term is empty or null, return all customers
        if (string.IsNullOrEmpty(searchTerm))
        {
            return await context.UtilityCustomers
                .AsNoTracking()
                .ToListAsync();
        }

        return await context.UtilityCustomers
            .Where(c => (c.CompanyName != null && c.CompanyName.Contains(searchTerm)) ||
                       ((c.FirstName + " " + c.LastName).Contains(searchTerm)) ||
                       c.AccountNumber.Contains(searchTerm))
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Adds a new utility customer
    /// </summary>
    public async Task<UtilityCustomer> AddAsync(UtilityCustomer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var now = DateTime.Now;
        customer.CreatedDate = now;
        customer.LastModifiedDate = now;
        context.UtilityCustomers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    /// <summary>
    /// Updates an existing utility customer
    /// </summary>
    public async Task<UtilityCustomer> UpdateAsync(UtilityCustomer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        await using var context = await _contextFactory.CreateDbContextAsync();
        customer.LastModifiedDate = DateTime.Now;
        context.UtilityCustomers.Update(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    /// <summary>
    /// Deletes a utility customer by ID
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var customer = await context.UtilityCustomers.FindAsync(id);
        if (customer == null)
            return false;

        context.UtilityCustomers.Remove(customer);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Checks if a customer exists by account number
    /// </summary>
    public async Task<bool> ExistsByAccountNumberAsync(string accountNumber, int? excludeId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.UtilityCustomers.Where(c => c.AccountNumber == accountNumber);
        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    /// <summary>
    /// Gets the total number of customers
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers.CountAsync();
    }

    /// <summary>
    /// Gets customers outside city limits
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetCustomersOutsideCityLimitsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .Where(c => c.ServiceLocation == ServiceLocation.OutsideCityLimits)
            .AsNoTracking()
            .ToListAsync();
    }

    private IQueryable<UtilityCustomer> ApplySorting(IQueryable<UtilityCustomer> query, string? sortBy, bool sortDescending)
    {
        if (string.IsNullOrEmpty(sortBy))
        {
            return sortDescending
                ? query.OrderByDescending(c => c.AccountNumber)
                : query.OrderBy(c => c.AccountNumber);
        }

        return sortBy.ToLowerInvariant() switch
        {
            "accountnumber" => sortDescending
                ? query.OrderByDescending(c => c.AccountNumber)
                : query.OrderBy(c => c.AccountNumber),
            "name" => sortDescending
                ? query.OrderByDescending(c => c.FirstName + " " + c.LastName)
                : query.OrderBy(c => c.FirstName + " " + c.LastName),
            "currentbalance" => sortDescending
                ? query.OrderByDescending(c => c.CurrentBalance)
                : query.OrderBy(c => c.CurrentBalance),
            "customertype" => sortDescending
                ? query.OrderByDescending(c => c.CustomerType)
                : query.OrderBy(c => c.CustomerType),
            "servicelocation" => sortDescending
                ? query.OrderByDescending(c => c.ServiceLocation)
                : query.OrderBy(c => c.ServiceLocation),
            _ => sortDescending
                ? query.OrderByDescending(c => c.AccountNumber)
                : query.OrderBy(c => c.AccountNumber)
        };
    }
}
