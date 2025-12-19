using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Windows.Forms;
using Syncfusion.WinForms.DataGrid;
using WileyWidget.McpServer.Helpers;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.McpServer.Tools;

/// <summary>
/// MCP tool for inspecting SfDataGrid controls on WinForms forms.
/// Returns column configuration, data binding info, and styling details.
/// </summary>
[McpServerToolType]
public static class InspectSfDataGridTool
{
    [McpServerTool]
    [Description("Inspects a Syncfusion SfDataGrid control on a WinForms form. Returns column count, column names, data binding details, theme info, and sample row data.")]
    public static string InspectSfDataGrid(
        [Description("Fully qualified type name of the form containing the grid (e.g., 'WileyWidget.WinForms.Forms.AccountsForm')")]
        string formTypeName,
        [Description("Optional: specific control name to inspect. If omitted, finds first SfDataGrid.")]
        string? gridName = null,
        [Description("Whether to include sample row data (default: true)")]
        bool includeSampleData = true)
    {
        Form? form = null;
        MainForm? mockMainForm = null;

        try
        {
            // Get form type from cache
            var formType = FormTypeCache.GetFormType(formTypeName);
            if (formType == null)
            {
                return $"❌ Form type not found: {formTypeName}";
            }

            // Create mock MainForm with MDI enabled
            mockMainForm = MockFactory.CreateMockMainForm(enableMdi: true);

            // Instantiate form
            try
            {
                form = FormInstantiationHelper.InstantiateForm(formType, mockMainForm);
            }
            catch (Exception ex)
            {
                return $"❌ Failed to instantiate form: {ex.Message}";
            }

            // Load form with theme
            var loaded = FormInstantiationHelper.LoadFormWithTheme(form);
            if (!loaded)
            {
                return $"❌ Failed to load form: {formTypeName}";
            }

            // Find grid
            var grid = SyncfusionTestHelper.FindSfDataGrid(form, gridName);
            if (grid == null)
            {
                return $"❌ SfDataGrid not found on form: {formTypeName}" + (gridName != null ? $" (name: {gridName})" : "");
            }

            // Validate grid
            var validationResult = SyncfusionTestHelper.ValidateSfDataGrid(grid);

            // Build inspection report
            var report = new StringBuilder();
            report.AppendLine($"✅ SfDataGrid Inspection: {formTypeName}");
            report.AppendLine();
            report.AppendLine($"Grid Name: {grid.Name ?? "(unnamed)"}");
            report.AppendLine($"Column Count: {grid.Columns.Count}");
            report.AppendLine($"Theme Name: {grid.ThemeName ?? "(default/inherited)"}");
            report.AppendLine($"AutoGenerateColumns: {grid.AutoGenerateColumns}");
            report.AppendLine($"AllowEditing: {grid.AllowEditing}");
            report.AppendLine($"AllowSorting: {grid.AllowSorting}");
            report.AppendLine($"AllowFiltering: {grid.AllowFiltering}");
            report.AppendLine();

            if (grid.Columns.Count > 0)
            {
                report.AppendLine("Columns:");
                for (int i = 0; i < grid.Columns.Count; i++)
                {
                    var col = grid.Columns[i];
                    report.AppendLine($"  {i + 1}. {col.MappingName} ({col.GetType().Name})");
                    report.AppendLine($"     HeaderText: {col.HeaderText}");
                    report.AppendLine($"     Width: {col.Width}");
                    report.AppendLine($"     Visible: {col.Visible}");
                }
                report.AppendLine();
            }

            // Data source info
            if (grid.DataSource != null)
            {
                var dataSourceType = grid.DataSource.GetType();
                report.AppendLine($"Data Source: {dataSourceType.Name}");

                // Try to get row count
                if (grid.View != null)
                {
                    report.AppendLine($"Row Count: {grid.View.Records.Count}");

                    if (includeSampleData && grid.View.Records.Count > 0)
                    {
                        report.AppendLine();
                        report.AppendLine("Sample Data (first 3 rows):");
                        var rowsToShow = Math.Min(3, grid.View.Records.Count);
                        for (int i = 0; i < rowsToShow; i++)
                        {
                            report.AppendLine($"  Row {i + 1}:");
                            var record = grid.View.Records[i];
                            // Display first 5 properties
                            var properties = record.Data.GetType().GetProperties().Take(5);
                            foreach (var prop in properties)
                            {
                                try
                                {
                                    var value = prop.GetValue(record.Data);
                                    report.AppendLine($"    {prop.Name}: {value}");
                                }
                                catch
                                {
                                    report.AppendLine($"    {prop.Name}: (error reading value)");
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                report.AppendLine("Data Source: (not bound)");
            }

            report.AppendLine();
            report.AppendLine($"Validation: {(validationResult ? "✅ PASS" : "❌ FAIL")}");

            return report.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
        }
        finally
        {
            // Safe cleanup
            FormInstantiationHelper.SafeDispose(form, mockMainForm);
        }
    }
}
