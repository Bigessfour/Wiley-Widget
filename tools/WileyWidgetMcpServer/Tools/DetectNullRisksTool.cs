using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using WileyWidget.McpServer.Helpers;
using Syncfusion.WinForms.DataGrid;

namespace WileyWidget.McpServer.Tools;

/// <summary>
/// MCP tool for detecting potential NullReferenceException risks in forms.
/// Scans for uninitialized Syncfusion controls, null DataSources, and missing dependencies.
/// </summary>
[McpServerToolType]
public static class DetectNullRisksTool
{
    [McpServerTool]
    [Description("Scans WinForms forms for potential NullReferenceException risks (e.g., null SfDataGrid.DataSource). Returns structured report of risks.")]
    public static string DetectNullRisks(
        [Description("Optional: Array of fully qualified form type names to scan. If empty, scans all forms.")]
        string[]? formTypeNames = null,
        [Description("Output format: 'text' or 'json' (default: 'text')")]
        string outputFormat = "text")
    {
        var startTime = DateTime.UtcNow;
        var results = new List<NullRiskResult>();

        try
        {
            IEnumerable<string> formsToScan;

            if (formTypeNames == null || formTypeNames.Length == 0)
            {
                var formTypes = FormTypeCache.GetAllFormTypes();
                formsToScan = formTypes.Select(t => t.FullName!).OrderBy(n => n);
            }
            else
            {
                formsToScan = formTypeNames;
            }

            var formsList = formsToScan.ToList();
            var totalForms = formsList.Count;

            foreach (var formTypeName in formsList)
            {
                var formResult = new NullRiskResult
                {
                    FormTypeName = formTypeName,
                    FormName = formTypeName.Split('.').Last(),
                    ScanTime = DateTime.UtcNow
                };

                try
                {
                    var mockMainForm = MockFactory.CreateMockMainForm(enableMdi: true);
                    var formType = FormTypeCache.GetFormType(formTypeName);

                    if (formType == null)
                    {
                        formResult.Error = $"Form type not found: {formTypeName}";
                        results.Add(formResult);
                        continue;
                    }

                    Form? form = null;
                    try
                    {
                        form = FormInstantiationHelper.InstantiateForm(formType, mockMainForm);
                    }
                    catch (Exception ex)
                    {
                        // Include full exception details (including inner exceptions) to help diagnostics
                        var detail = ex.InnerException != null ? ex.InnerException.ToString() : ex.ToString();
                        formResult.Error = $"Failed to instantiate: {detail}";
                        results.Add(formResult);
                        mockMainForm.Dispose();
                        continue;
                    }

                    // Load form to trigger component initialization
                    FormInstantiationHelper.LoadFormWithTheme(form, "Office2019Colorful");

                    var risks = new List<string>();

                    // 1. Check SfDataGrid DataSource
                    var syncfusionControls = SyncfusionTestHelper.GetAllSyncfusionControls(form);
                    var grids = syncfusionControls.OfType<SfDataGrid>().ToList();

                    foreach (var grid in grids)
                    {
                        if (grid.DataSource == null)
                        {
                            risks.Add($"SfDataGrid '{grid.Name}' has null DataSource");
                        }
                    }

                    // 2. Check DockingManager
                    var dockingManagers = syncfusionControls.Where(c => c.GetType().Name == "DockingManager").ToList();
                    foreach (var dm in dockingManagers)
                    {
                        var hostControlProp = dm.GetType().GetProperty("HostControl");
                        if (hostControlProp != null && hostControlProp.GetValue(dm) == null)
                        {
                            risks.Add($"DockingManager '{dm.Name}' has null HostControl");
                        }
                    }

                    formResult.Risks = risks.ToArray();
                    formResult.RiskCount = risks.Count;
                    formResult.Passed = risks.Count == 0;

                    FormInstantiationHelper.SafeDispose(form, mockMainForm);
                }
                catch (Exception ex)
                {
                    formResult.Error = $"Scan error: {ex.Message}";
                }

                results.Add(formResult);
            }

            var duration = DateTime.UtcNow - startTime;

            return outputFormat.ToLowerInvariant() switch
            {
                "json" => GenerateJsonReport(results, duration, totalForms),
                _ => GenerateTextReport(results, duration, totalForms)
            };
        }
        catch (Exception ex)
        {
            return $"❌ Null risk scan error: {ex.Message}";
        }
    }

    private static string GenerateTextReport(List<NullRiskResult> results, TimeSpan duration, int totalForms)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("             NULL SAFETY SCAN REPORT");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Total Forms:    {totalForms}");
        sb.AppendLine($"Scanned:        {results.Count}");
        sb.AppendLine($"Clean:          {results.Count(r => r.Passed && string.IsNullOrEmpty(r.Error))} ✅");
        sb.AppendLine($"Risky:          {results.Count(r => !r.Passed || !string.IsNullOrEmpty(r.Error))} ⚠️");
        sb.AppendLine($"Duration:       {duration.TotalSeconds:F2}s");
        sb.AppendLine();

        var riskyForms = results.Where(r => !r.Passed || !string.IsNullOrEmpty(r.Error)).ToList();

        if (riskyForms.Any())
        {
            sb.AppendLine("⚠️  POTENTIAL RISKS DETECTED:");
            sb.AppendLine();

            foreach (var result in riskyForms)
            {
                sb.AppendLine($"  • {result.FormName}");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    sb.AppendLine($"    ❌ Error: {result.Error}");
                }
                else
                {
                    foreach (var risk in result.Risks)
                    {
                        sb.AppendLine($"    ⚠️  {risk}");
                    }
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("✅ No common null risks detected across all forms.");
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════");
        return sb.ToString();
    }

    private static string GenerateJsonReport(List<NullRiskResult> results, TimeSpan duration, int totalForms)
    {
        var report = new
        {
            reportType = "NullRiskScanReport",
            timestamp = DateTime.UtcNow,
            summary = new
            {
                totalForms,
                scanned = results.Count,
                passed = results.Count(r => r.Passed && string.IsNullOrEmpty(r.Error)),
                failed = results.Count(r => !r.Passed || !string.IsNullOrEmpty(r.Error)),
                durationSeconds = duration.TotalSeconds
            },
            results = results.Select(r => new
            {
                formName = r.FormName,
                formTypeName = r.FormTypeName,
                passed = r.Passed,
                riskCount = r.RiskCount,
                risks = r.Risks,
                error = r.Error
            })
        };

        return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    }
}

public class NullRiskResult
{
    public required string FormTypeName { get; set; }
    public required string FormName { get; set; }
    public bool Passed { get; set; }
    public int RiskCount { get; set; }
    public string[] Risks { get; set; } = Array.Empty<string>();
    public string? Error { get; set; }
    public DateTime ScanTime { get; set; }
}
