#nullable enable
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
    Task<IEnumerable<UtilityBill>> GetAllAsync();

    /// <summary>
    /// Gets a utility bill by ID
    /// </summary>
    Task<UtilityBill?> GetByIdAsync(int id);

    /// <summary>
    /// Gets a utility bill by bill number
    /// </summary>
    Task<UtilityBill?> GetByBillNumberAsync(string billNumber);

    /// <summary>
    /// Gets all bills for a specific customer
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetByCustomerIdAsync(int customerId);

    /// <summary>
    /// Gets bills by status
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetByStatusAsync(BillStatus status);

    /// <summary>
    /// Gets overdue bills
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetOverdueBillsAsync();

    /// <summary>
    /// Gets bills due within a date range
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetBillsDueInRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets unpaid bills for a customer
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetUnpaidBillsByCustomerIdAsync(int customerId);

    /// <summary>
    /// Calculates the total balance for a customer (sum of all unpaid bills)
    /// </summary>
    Task<decimal> GetCustomerBalanceAsync(int customerId);

    /// <summary>
    /// Gets charges for a specific bill
    /// </summary>
    Task<IEnumerable<Charge>> GetChargesByBillIdAsync(int billId);

    /// <summary>
    /// Gets all charges for a customer across all bills
    /// </summary>
    Task<IEnumerable<Charge>> GetChargesByCustomerIdAsync(int customerId);

    /// <summary>
    /// Adds a new utility bill
    /// </summary>
    Task<UtilityBill> AddAsync(UtilityBill bill);

    /// <summary>
    /// Adds a charge to a bill
    /// </summary>
    Task<Charge> AddChargeAsync(Charge charge);

    /// <summary>
    /// Updates an existing utility bill
    /// </summary>
    Task<UtilityBill> UpdateAsync(UtilityBill bill);

    /// <summary>
    /// Deletes a utility bill by ID
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Records a payment for a bill
    /// </summary>
    Task<bool> RecordPaymentAsync(int billId, decimal amount, DateTime paymentDate);

    /// <summary>
    /// Checks if a bill number already exists
    /// </summary>
    Task<bool> BillNumberExistsAsync(string billNumber, int? excludeId = null);

    /// <summary>
    /// Gets the total number of bills
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// Gets bills created within a date range
    /// </summary>
    Task<IEnumerable<UtilityBill>> GetBillsByDateRangeAsync(DateTime startDate, DateTime endDate);
}
