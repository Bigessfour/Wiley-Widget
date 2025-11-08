using WileyWidget.Models;
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Unit of Work pattern for coordinating multiple repository operations in a single transaction
/// BEST PRACTICE: DbContext itself IS the Unit of Work in EF Core - this is just a facade for convenience
/// </summary>
public interface IUnitOfWorkBestPractice : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Enterprise repository access
    /// BEST PRACTICE: Inject repositories rather than create them
    /// </summary>
    IEnterpriseRepository Enterprises { get; }

    /// <summary>
    /// Municipal account repository access
    /// </summary>
    IMunicipalAccountRepository MunicipalAccounts { get; }

    /// <summary>
    /// Utility customer repository access
    /// </summary>
    IUtilityCustomerRepository UtilityCustomers { get; }

    /// <summary>
    /// Saves all pending changes to the database
    /// BEST PRACTICE: This is the ONLY place SaveChanges should be called
    /// Repositories should NOT call SaveChanges - they just modify the tracked entities
    /// </summary>
    /// <returns>Number of affected rows</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new transaction (or returns existing if nested)
    /// BEST PRACTICE: Support idempotent transaction nesting
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction
    /// BEST PRACTICE: Only commit if we started the transaction (support nesting)
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation within a transaction, automatically committing or rolling back
    /// BEST PRACTICE: Support nested transactions by checking for existing transaction
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a void operation within a transaction
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// BEST PRACTICE: Expose change tracker for advanced scenarios
    /// Allows checking if there are unsaved changes
    /// </summary>
    bool HasChanges();

    /// <summary>
    /// BEST PRACTICE: Clear change tracker (useful for long-lived contexts)
    /// </summary>
    void DetachAll();
}
