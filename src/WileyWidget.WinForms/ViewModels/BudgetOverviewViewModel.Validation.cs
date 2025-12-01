using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;

namespace WileyWidget.ViewModels
{
    /// <summary>
    /// Validation-related partial class for <see cref="BudgetOverviewViewModel"/>.
    /// </summary>
    public partial class BudgetOverviewViewModel
    {
        // Note: Export functionality is implemented in BudgetOverviewPanel.ExportToCsvAsync
        // which has access to SaveFileDialog and file I/O.
        // The ViewModel exposes the data via Metrics collection.

        /// <summary>
        /// Gets the CSV content for the current metrics data.
        /// Can be used by the panel or tests to export data.
        /// </summary>
        public string GetCsvContent()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Department,Category,Budgeted,Actual,Variance,Variance %,Over Budget,Fiscal Year");

            foreach (var metric in Metrics)
            {
                sb.AppendLine(string.Concat('"', metric.DepartmentName, '"', ",",
                    '"', metric.Category, '"', ",",
                    metric.BudgetedAmount.ToString(CultureInfo.InvariantCulture), ",",
                    metric.Amount.ToString(CultureInfo.InvariantCulture), ",",
                    metric.Variance.ToString(CultureInfo.InvariantCulture), ",",
                    metric.VariancePercent.ToString("F2", CultureInfo.InvariantCulture), ",",
                    metric.IsOverBudget.ToString(CultureInfo.InvariantCulture), ",",
                    metric.FiscalYear.ToString(CultureInfo.InvariantCulture)));
            }

            // Summary row
            sb.AppendLine();
            sb.AppendLine(string.Concat('"', "TOTAL", '"', ",",
                "", // empty column for department
                TotalBudgeted.ToString(CultureInfo.InvariantCulture), ",",
                TotalActual.ToString(CultureInfo.InvariantCulture), ",",
                TotalVariance.ToString(CultureInfo.InvariantCulture), ",",
                OverallVariancePercent.ToString("F2", CultureInfo.InvariantCulture), ",",
                "", ",",
                SelectedFiscalYear.ToString(CultureInfo.InvariantCulture)));

            return sb.ToString();
        }
    }
}
