using System.Collections.Generic;
using WileyWidget.Models;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for data anonymization service providing privacy-compliant data masking.
    /// Used to sanitize sensitive information before sending to AI services or external systems.
    /// </summary>
    public interface IDataAnonymizerService
    {
        /// <summary>
        /// Anonymizes an Enterprise object by masking sensitive identifiable information.
        /// </summary>
        /// <param name="enterprise">The enterprise to anonymize</param>
        /// <returns>Anonymized copy of the enterprise</returns>
        Enterprise AnonymizeEnterprise(Enterprise enterprise);

        /// <summary>
        /// Anonymizes a BudgetData object by masking sensitive financial identifiers.
        /// </summary>
        /// <param name="budgetData">The budget data to anonymize</param>
        /// <returns>Anonymized copy of the budget data</returns>
        BudgetData AnonymizeBudgetData(BudgetData budgetData);

        /// <summary>
        /// Anonymizes a collection of enterprises.
        /// </summary>
        /// <param name="enterprises">Collection of enterprises to anonymize</param>
        /// <returns>Collection of anonymized enterprises</returns>
        IEnumerable<Enterprise> AnonymizeEnterprises(IEnumerable<Enterprise> enterprises);

        /// <summary>
        /// Anonymizes a collection of budget data.
        /// </summary>
        /// <param name="budgetDataCollection">Collection of budget data to anonymize</param>
        /// <returns>Collection of anonymized budget data</returns>
        IEnumerable<BudgetData> AnonymizeBudgetDataCollection(IEnumerable<BudgetData> budgetDataCollection);

        /// <summary>
        /// Clears the anonymization cache. Use with caution as this will affect consistency.
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Gets statistics about the anonymization cache.
        /// </summary>
        /// <returns>Dictionary with cache statistics</returns>
        Dictionary<string, int> GetCacheStatistics();
    }
}
