using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for UtilityCustomer data operations
/// </summary>
public interface IUtilityCustomerRepository
{
    /// <summary>
    /// Gets all utility customers
    /// </summary>
    Task<IEnumerable<UtilityCustomer>> GetAllAsync();

    /// <summary>
    /// Gets a utility customer by ID
    /// </summary>
    Task<UtilityCustomer?> GetByIdAsync(int id);

    /// <summary>
    /// Gets a utility customer by account number
    /// </summary>
    Task<UtilityCustomer?> GetByAccountNumberAsync(string accountNumber);

    /// <summary>
    /// Gets customers by customer type
    /// </summary>
    Task<IEnumerable<UtilityCustomer>> GetByCustomerTypeAsync(CustomerType customerType);

    /// <summary>
    /// Gets customers by service location
    /// </summary>
    Task<IEnumerable<UtilityCustomer>> GetByServiceLocationAsync(ServiceLocation serviceLocation);

    /// <summary>
    /// Gets active customers only
    /// </summary>
    Task<IEnumerable<UtilityCustomer>> GetActiveCustomersAsync();

    /// <summary>
    /// Gets customers with outstanding balances
    /// </summary>
    Task<IEnumerable<UtilityCustomer>> GetCustomersWithBalanceAsync();

    /// <summary>
    /// Searches customers by name or account number
    /// </summary>
    Task<IEnumerable<UtilityCustomer>> SearchAsync(string searchTerm);

    /// <summary>
    /// Adds a new utility customer
    /// </summary>
    Task<UtilityCustomer> AddAsync(UtilityCustomer customer);

    /// <summary>
    /// Updates an existing utility customer
    /// </summary>
    Task<UtilityCustomer> UpdateAsync(UtilityCustomer customer);

    /// <summary>
    /// Deletes a utility customer by ID
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks if a customer exists by account number
    /// </summary>
    Task<bool> ExistsByAccountNumberAsync(string accountNumber, int? excludeId = null);

    /// <summary>
    /// Gets the total number of customers
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// Gets customers outside city limits
    /// </summary>
    Task<IEnumerable<UtilityCustomer>> GetCustomersOutsideCityLimitsAsync();
}
