using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services
{
    /// <summary>
    /// QuickBooks integration service for connecting to QuickBooks Online API.
    /// </summary>
    public class QuickBooksService
    {
        /// <summary>
        /// Get company information including fiscal year settings.
        /// </summary>
        public async Task<CompanyInfo> GetCompanyInfoAsync(CancellationToken cancellationToken = default)
        {
            // TODO: Replace with actual QuickBooks API call
            // Simulate API call
            await Task.Delay(500, cancellationToken);
            
            return new CompanyInfo
            {
                CompanyName = "Demo Company",
                FiscalYearStartMonth = 1 // January (standard calendar year)
            };
        }

        // Dummy implementation
        public void DoSomething()
        {
            System.Diagnostics.Debug.WriteLine("QuickBooks service called");
        }
    }

    /// <summary>
    /// Company information from QuickBooks.
    /// </summary>
    public class CompanyInfo
    {
        /// <summary>
        /// Company name as registered in QuickBooks.
        /// </summary>
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>
        /// Fiscal year start month (1 = January, 7 = July, etc.).
        /// </summary>
        public int FiscalYearStartMonth { get; set; }
    }
}
