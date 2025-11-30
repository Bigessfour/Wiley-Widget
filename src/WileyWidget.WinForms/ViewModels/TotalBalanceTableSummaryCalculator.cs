using System.Collections.Generic;
using System.Linq;
using WileyWidget.WinForms.Models;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Lightweight calculator to compute the total balance for a table of accounts.
    /// This is intentionally simple and testable — UI layers may call into this for summaries.
    /// </summary>
    public class TotalBalanceTableSummaryCalculator
    {
        public decimal Calculate(IEnumerable<MunicipalAccountDisplay>? rows)
        {
            return rows?.Sum(r => r.CurrentBalance) ?? 0m;
        }
    }
}
