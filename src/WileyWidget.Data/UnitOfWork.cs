using WileyWidget.Models;
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
// Clean Architecture: Data layer implements interfaces from Business layer
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Data;

/// <summary>
/// Implementation of Unit of Work pattern using EF Core DbContext
/// Coordinates multiple repositories and provides transaction management
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly ILogger<UnitOfWork> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMemoryCache _cache;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    // Lazy-initialized repositories
    private IBudgetRepository? _budgets;
    private IDepartmentRepository? _departments;
    private IMunicipalAccountRepository? _municipalAccounts;
    private IUtilityCustomerRepository? _utilityCustomers;
    private IUtilityBillRepository? _utilityBills;
    private IEnterpriseRepository? _enterprises;
    private IAuditRepository? _audits;

    /// <summary>
    /// Constructor with DbContext injection
    /// </summary>
    public UnitOfWork(AppDbContext context, ILogger<UnitOfWork> logger, ILoggerFactory loggerFactory, IMemoryCache cache)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Budget repository (lazy-loaded)
    /// </summary>
    public IBudgetRepository Budgets
    {
        get
        {
            if (_budgets == null)
            {
                var factory = new SingleContextFactory(_context);
                _budgets = new BudgetRepository(factory, _cache);
            }
            return _budgets;
        }
    }

    /// <summary>
    /// Department repository (lazy-loaded)
    /// </summary>
    public IDepartmentRepository Departments
    {
        get
        {
            if (_departments == null)
            {
                var factory = new SingleContextFactory(_context);
                _departments = new DepartmentRepository(factory, _cache);
            }
            return _departments;
        }
    }

    /// <summary>
    /// Municipal account repository (lazy-loaded)
    /// </summary>
    public IMunicipalAccountRepository MunicipalAccounts
    {
        get
        {
            if (_municipalAccounts == null)
            {
                var factory = new SingleContextFactory(_context);
                _municipalAccounts = new MunicipalAccountRepository(factory, _cache);
            }
            return _municipalAccounts;
        }
    }

    /// <summary>
    /// Utility customer repository (lazy-loaded)
    /// </summary>
    public IUtilityCustomerRepository UtilityCustomers
    {
        get
        {
            if (_utilityCustomers == null)
            {
                var factory = new SingleContextFactory(_context);
                var ucLogger = _loggerFactory.CreateLogger<UtilityCustomerRepository>();
                _utilityCustomers = new UtilityCustomerRepository(factory, ucLogger, _cache);
            }
            return _utilityCustomers;
        }
    }

    /// <summary>
    /// Utility bill repository (lazy-loaded)
    /// </summary>
    public IUtilityBillRepository UtilityBills
    {
        get
        {
            if (_utilityBills == null)
            {
                var factory = new SingleContextFactory(_context);
                var ubLogger = _loggerFactory.CreateLogger<UtilityBillRepository>();
                _utilityBills = new UtilityBillRepository(factory, ubLogger, _cache);
            }
            return _utilityBills;
        }
    }

    /// <summary>
    /// Enterprise repository (lazy-loaded)
    /// </summary>
    public IEnterpriseRepository Enterprises
    {
        get
        {
            if (_enterprises == null)
            {
                var factory = new SingleContextFactory(_context);
                var logger = _loggerFactory.CreateLogger<EnterpriseRepository>();
                _enterprises = new EnterpriseRepository(factory, logger, _cache);
            }
            return _enterprises;
        }
    }

    /// <summary>
    /// Audit repository (lazy-loaded)
    /// </summary>
    public IAuditRepository Audits
    {
        get
        {
            if (_audits == null)
            {
                var factory = new SingleContextFactory(_context);
                _audits = new AuditRepository(factory, _cache);
            }
            return _audits;
        }
    }

    /// <summary>
    /// Saves all pending changes to the database
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict in UnitOfWork.SaveChangesAsync");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving changes in UnitOfWork");
            throw;
        }
    }

    /// <summary>
    /// Begins a new database transaction
    /// </summary>
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress");
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _logger.LogDebug("Transaction started: {TransactionId}", _currentTransaction.TransactionId);
        return _currentTransaction;
    }

    /// <summary>
    /// Commits the current transaction
    /// </summary>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No transaction in progress");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Transaction committed: {TransactionId}", _currentTransaction.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error committing transaction");
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            try
            {
                await _currentTransaction.DisposeAsync();
            }
            catch
            {
                // Ignore dispose errors
            }
            finally
            {
                _currentTransaction = null;
            }
        }
    }

    /// <summary>
    /// Rolls back the current transaction
    /// </summary>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            return; // Nothing to rollback
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            _logger.LogDebug("Transaction rolled back: {TransactionId}", _currentTransaction.TransactionId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("completed", StringComparison.OrdinalIgnoreCase))
        {
            // Transaction already completed, ignore
            _logger.LogDebug("Transaction already completed, ignoring rollback");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back transaction");
            throw;
        }
        finally
        {
            try
            {
                await _currentTransaction.DisposeAsync();
            }
            catch
            {
                // Ignore dispose errors
            }
            finally
            {
                _currentTransaction = null;
            }
        }
    }

    /// <summary>
    /// Executes an operation within a transaction, automatically handling commit/rollback
    /// </summary>
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        await BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation();
            await CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Executes a void operation within a transaction
    /// </summary>
    public async Task ExecuteInTransactionAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        await BeginTransactionAsync(cancellationToken);
        try
        {
            await operation();
            await CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Dispose pattern implementation
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Async dispose pattern implementation
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _currentTransaction?.Dispose();
                // Context is managed by DI, don't dispose it here
            }
            _disposed = true;
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
        }
        // Context is managed by DI, don't dispose it here
    }

    /// <summary>
    /// Helper factory that returns a single context instance (for UnitOfWork repo coordination)
    /// </summary>
    private class SingleContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly AppDbContext _context;

        public SingleContextFactory(AppDbContext context)
        {
            _context = context;
        }

        public AppDbContext CreateDbContext()
        {
            return _context;
        }
    }
}
