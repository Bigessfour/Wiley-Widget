using System.Collections.Generic;
using System.Linq;
using WileyWidget.WinForms.Models;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Lightweight calculator to count active accounts in a table of accounts.
    /// Keeps calculation logic separated for easy unit testing.
    /// </summary>
    public class ActiveAccountCountTableSummaryCalculator
    {
        public int Calculate(IEnumerable<MunicipalAccountDisplay>? rows)
        {
            return rows?.Count(r => r.IsActive) ?? 0;
        }
    }
}
