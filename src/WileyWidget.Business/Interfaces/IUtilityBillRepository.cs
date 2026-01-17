#nullable enable
using System.Threading;
using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for UtilityBill data operations
/// </summary>
public interface IUtilityBillRepository
{
    /// <summary>
    /// Gets all utility bills
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a utility bill by ID
    /// </summary>
    Task<UtilityBill?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a utility bill by bill number
    /// </summary>
    Task<UtilityBill?> GetByBillNumberAsync(string billNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all bills for a specific customer
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets bills by status
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetByStatusAsync(BillStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets overdue bills
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetOverdueBillsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets bills due within a date range
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetBillsDueInRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unpaid bills for a customer
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetUnpaidBillsByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the total balance for a customer (sum of all unpaid bills)
    /// </summary>
    Task<decimal> GetCustomerBalanceAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets charges for a specific bill
    /// </summary>
    Task<IEnumerable<Charge>> GetChargesByBillIdAsync(int billId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all charges for a customer across all bills
    /// </summary>
    Task<IEnumerable<Charge>> GetChargesByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new utility bill
    /// </summary>
    Task<UtilityBill> AddAsync(UtilityBill bill, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a charge to a bill
    /// </summary>
    Task<Charge> AddChargeAsync(Charge charge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing utility bill
    /// </summary>
    Task<UtilityBill> UpdateAsync(UtilityBill bill, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a utility bill by ID
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a payment for a bill
    /// </summary>
    Task<bool> RecordPaymentAsync(int billId, decimal amount, DateTime paymentDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a bill number already exists
    /// </summary>
    Task<bool> BillNumberExistsAsync(string billNumber, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total number of bills
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets bills created within a date range
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetBillsByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
