using WileyWidget.Models;
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Unit of Work pattern for coordinating multiple repository operations in a single transaction
/// Ensures ACID compliance for complex operations (e.g., update budget + create audit log)
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Budget repository access
    /// </summary>
    IBudgetRepository Budgets { get; }

    /// <summary>
    /// Department repository access
    /// </summary>
    IDepartmentRepository Departments { get; }

    /// <summary>
    /// Municipal account repository access
    /// </summary>
    IMunicipalAccountRepository MunicipalAccounts { get; }

    /// <summary>
    /// Utility customer repository access
    /// </summary>
    IUtilityCustomerRepository UtilityCustomers { get; }

    /// <summary>
    /// Utility bill repository access
    /// </summary>
    IUtilityBillRepository UtilityBills { get; }

    /// <summary>
    /// Enterprise repository access
    /// </summary>
    IEnterpriseRepository Enterprises { get; }

    /// <summary>
    /// Audit repository access
    /// </summary>
    IAuditRepository Audits { get; }

    /// <summary>
    /// Saves all pending changes to the database
    /// </summary>
    /// <returns>Number of affected rows</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new transaction
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation within a transaction, automatically committing or rolling back
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
}
