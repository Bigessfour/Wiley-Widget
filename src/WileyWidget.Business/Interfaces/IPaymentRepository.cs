#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for payment/check management
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Gets all payments
    /// </summary>
    Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a payment by ID
    /// </summary>
    Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets payments by check number
    /// </summary>
    Task<IReadOnlyList<Payment>> GetByCheckNumberAsync(string checkNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets payments by payee name (partial match)
    /// </summary>
    Task<IReadOnlyList<Payment>> GetByPayeeAsync(string payee, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets payments within a date range
    /// </summary>
    Task<IReadOnlyList<Payment>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets payments for a specific municipal account
    /// </summary>
    Task<IReadOnlyList<Payment>> GetByAccountAsync(int accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets payments for a specific vendor
    /// </summary>
    Task<IReadOnlyList<Payment>> GetByVendorAsync(int vendorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets payments by status
    /// </summary>
    Task<IReadOnlyList<Payment>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new payment
    /// </summary>
    Task<Payment> AddAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing payment
    /// </summary>
    Task<Payment> UpdateAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a payment
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a check number already exists
    /// </summary>
    Task<bool> CheckNumberExistsAsync(string checkNumber, int? excludePaymentId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets total payment amount within date range
    /// </summary>
    Task<decimal> GetTotalAmountAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
}
