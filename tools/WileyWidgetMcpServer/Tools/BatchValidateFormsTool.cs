using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using WileyWidget.McpServer.Helpers;

namespace WileyWidget.McpServer.Tools;

/// <summary>
/// MCP tool for batch validation of multiple forms.
/// Validates theme compliance across all or selected forms and generates structured reports.
/// </summary>
[McpServerToolType]
public static class BatchValidateFormsTool
{
    [McpServerTool]
    [Description("Validates theme compliance for multiple WinForms forms in batch. Returns structured JSON report with pass/fail status for each form.")]
    public static string BatchValidateForms(
        [Description("Optional: Array of fully qualified form type names to validate. If empty, validates all forms in WileyWidget.WinForms.Forms namespace.")]
        string[]? formTypeNames = null,
        [Description("Expected theme name (default: 'Office2019Colorful')")]
        string expectedTheme = "Office2019Colorful",
        [Description("Stop validation on first failure (default: false)")]
        bool failFast = false,
        [Description("Output format: 'text', 'json', or 'html' (default: 'text')")]
        string outputFormat = "text")
    {
        var startTime = DateTime.UtcNow;
        var results = new List<FormValidationResult>();

        try
        {
            // If no specific forms provided, discover all forms in the assembly
            IEnumerable<string> formsToValidate;

            if (formTypeNames == null || formTypeNames.Length == 0)
            {
                // Use cached form discovery
                var formTypes = FormTypeCache.GetAllFormTypes();
                formsToValidate = formTypes.Select(t => t.FullName!).OrderBy(n => n);
            }
            else
            {
                formsToValidate = formTypeNames;
            }

            var formsList = formsToValidate.ToList();
            var totalForms = formsList.Count;
            var currentIndex = 0;

            foreach (var formTypeName in formsList)
            {
                currentIndex++;
                var formResult = new FormValidationResult
                {
                    FormTypeName = formTypeName,
                    FormName = formTypeName.Split('.').Last(),
                    ExpectedTheme = expectedTheme,
                    ValidationTime = DateTime.UtcNow
                };

                try
                {
                    // Create mock MainForm with docking enabled for realism
                    var mockMainForm = MockFactory.CreateMockMainForm();

                    // Get form type from cache
                    var formType = FormTypeCache.GetFormType(formTypeName);
                    if (formType == null)
                    {
                        formResult.Passed = false;
                        formResult.Error = $"Form type not found: {formTypeName}";
                        results.Add(formResult);
                        continue;
                    }

                    Form? form = null;

                    // Instantiate form with proper constructor handling
                    try
                    {
                        form = FormInstantiationHelper.InstantiateForm(formType, mockMainForm);
                    }
                    catch (Exception ex)
                    {
                        formResult.Passed = false;
                        formResult.Error = $"Failed to instantiate: {ex.Message}";
                        results.Add(formResult);
                        mockMainForm.Dispose();
                        continue;
                    }

                    // Load form with theme
                    var loaded = FormInstantiationHelper.LoadFormWithTheme(form, expectedTheme);
                    if (!loaded)
                    {
                        formResult.Passed = false;
                        formResult.Error = "Failed to load form components";
                        FormInstantiationHelper.SafeDispose(form, mockMainForm);
                        results.Add(formResult);
                        continue;
                    }

                    // Validate theme
                    formResult.ThemeValid = SyncfusionTestHelper.ValidateTheme(form, expectedTheme);

                    // Check for manual colors
                    var violations = SyncfusionTestHelper.ValidateNoManualColors(form);
                    formResult.ManualColorViolations = violations.ToArray();
                    formResult.ViolationCount = violations.Count;

                    formResult.Passed = formResult.ThemeValid && violations.Count == 0;

                    // Safe cleanup
                    FormInstantiationHelper.SafeDispose(form, mockMainForm);
                }
                catch (Exception ex)
                {
                    formResult.Passed = false;
                    formResult.Error = $"Validation error: {ex.Message}";
                }

                results.Add(formResult);

                // Check fail-fast
                if (failFast && !formResult.Passed)
                {
                    break;
                }
            }

            var duration = DateTime.UtcNow - startTime;

            // Generate output based on format
            return outputFormat.ToLowerInvariant() switch
            {
                "json" => GenerateJsonReport(results, duration, totalForms),
                "html" => GenerateHtmlReport(results, duration, totalForms),
                _ => GenerateTextReport(results, duration, totalForms)
            };
        }
        catch (Exception ex)
        {
            return $"❌ Batch validation error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
        }
    }

    private static string GenerateTextReport(List<FormValidationResult> results, TimeSpan duration, int totalForms)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("           BATCH FORM VALIDATION REPORT");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Total Forms:    {totalForms}");
        sb.AppendLine($"Validated:      {results.Count}");
        sb.AppendLine($"Passed:         {results.Count(r => r.Passed)} ✅");
        sb.AppendLine($"Failed:         {results.Count(r => !r.Passed)} ❌");
        sb.AppendLine($"Duration:       {duration.TotalSeconds:F2}s");
        sb.AppendLine($"Timestamp:      {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();

        var passedForms = results.Where(r => r.Passed).ToList();
        var failedForms = results.Where(r => !r.Passed).ToList();

        if (failedForms.Any())
        {
            sb.AppendLine("❌ FAILED FORMS:");
            sb.AppendLine();

            foreach (var result in failedForms)
            {
                sb.AppendLine($"  • {result.FormName}");

                if (!string.IsNullOrEmpty(result.Error))
                {
                    sb.AppendLine($"    Error: {result.Error}");
                }
                else
                {
                    if (!result.ThemeValid)
                    {
                        sb.AppendLine($"    ⚠️  Theme check: FAIL");
                    }

                    if (result.ViolationCount > 0)
                    {
                        sb.AppendLine($"    ⚠️  Manual color violations: {result.ViolationCount}");
                        foreach (var violation in result.ManualColorViolations)
                        {
                            sb.AppendLine($"       - {violation}");
                        }
                    }
                }

                sb.AppendLine();
            }
        }

        if (passedForms.Any())
        {
            sb.AppendLine("✅ PASSED FORMS:");
            sb.AppendLine();

            foreach (var result in passedForms)
            {
                sb.AppendLine($"  • {result.FormName}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════");

        if (failedForms.Any())
        {
            sb.AppendLine();
            sb.AppendLine("⚠️  ACTION REQUIRED: Fix violations in failed forms before committing.");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("🎉 All forms passed validation! Ready for production.");
        }

        return sb.ToString();
    }

    private static string GenerateJsonReport(List<FormValidationResult> results, TimeSpan duration, int totalForms)
    {
        var report = new
        {
            reportType = "FormValidationReport",
            timestamp = DateTime.UtcNow,
            summary = new
            {
                totalForms,
                validated = results.Count,
                passed = results.Count(r => r.Passed),
                failed = results.Count(r => !r.Passed),
                durationSeconds = duration.TotalSeconds
            },
            results = results.Select(r => new
            {
                formName = r.FormName,
                formTypeName = r.FormTypeName,
                passed = r.Passed,
                themeValid = r.ThemeValid,
                expectedTheme = r.ExpectedTheme,
                violationCount = r.ViolationCount,
                violations = r.ManualColorViolations,
                error = r.Error,
                validationTime = r.ValidationTime
            })
        };

        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static string GenerateHtmlReport(List<FormValidationResult> results, TimeSpan duration, int totalForms)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>WileyWidget Form Validation Report</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; background: #f5f5f5; }");
        sb.AppendLine("        .container { max-width: 1200px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        sb.AppendLine("        h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px; }");
        sb.AppendLine("        .summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; margin: 20px 0; }");
        sb.AppendLine("        .summary-card { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px; }");
        sb.AppendLine("        .summary-card.passed { background: linear-gradient(135deg, #56ab2f 0%, #a8e063 100%); }");
        sb.AppendLine("        .summary-card.failed { background: linear-gradient(135deg, #eb3349 0%, #f45c43 100%); }");
        sb.AppendLine("        .summary-card h3 { margin: 0 0 10px 0; font-size: 14px; opacity: 0.9; }");
        sb.AppendLine("        .summary-card .value { font-size: 32px; font-weight: bold; }");
        sb.AppendLine("        table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
        sb.AppendLine("        th { background: #34495e; color: white; padding: 12px; text-align: left; }");
        sb.AppendLine("        td { padding: 12px; border-bottom: 1px solid #ddd; }");
        sb.AppendLine("        tr:hover { background: #f8f9fa; }");
        sb.AppendLine("        .status-pass { color: #27ae60; font-weight: bold; }");
        sb.AppendLine("        .status-fail { color: #e74c3c; font-weight: bold; }");
        sb.AppendLine("        .violations { background: #fff3cd; border-left: 4px solid #ffc107; padding: 10px; margin: 5px 0; border-radius: 4px; }");
        sb.AppendLine("        .violations ul { margin: 5px 0; padding-left: 20px; }");
        sb.AppendLine("        .error { background: #f8d7da; border-left: 4px solid #dc3545; padding: 10px; margin: 5px 0; border-radius: 4px; color: #721c24; }");
        sb.AppendLine("        .timestamp { color: #7f8c8d; font-size: 12px; margin-top: 20px; text-align: right; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container\">");
        sb.AppendLine("        <h1>🎨 WileyWidget Form Validation Report</h1>");

        // Summary cards
        sb.AppendLine("        <div class=\"summary\">");
        sb.AppendLine($"            <div class=\"summary-card\">");
        sb.AppendLine($"                <h3>TOTAL FORMS</h3>");
        sb.AppendLine($"                <div class=\"value\">{totalForms}</div>");
        sb.AppendLine($"            </div>");
        sb.AppendLine($"            <div class=\"summary-card passed\">");
        sb.AppendLine($"                <h3>✅ PASSED</h3>");
        sb.AppendLine($"                <div class=\"value\">{results.Count(r => r.Passed)}</div>");
        sb.AppendLine($"            </div>");
        sb.AppendLine($"            <div class=\"summary-card failed\">");
        sb.AppendLine($"                <h3>❌ FAILED</h3>");
        sb.AppendLine($"                <div class=\"value\">{results.Count(r => !r.Passed)}</div>");
        sb.AppendLine($"            </div>");
        sb.AppendLine($"            <div class=\"summary-card\">");
        sb.AppendLine($"                <h3>⏱️ DURATION</h3>");
        sb.AppendLine($"                <div class=\"value\">{duration.TotalSeconds:F1}s</div>");
        sb.AppendLine($"            </div>");
        sb.AppendLine("        </div>");

        // Results table
        sb.AppendLine("        <table>");
        sb.AppendLine("            <thead>");
        sb.AppendLine("                <tr>");
        sb.AppendLine("                    <th>Form</th>");
        sb.AppendLine("                    <th>Status</th>");
        sb.AppendLine("                    <th>Theme</th>");
        sb.AppendLine("                    <th>Violations</th>");
        sb.AppendLine("                    <th>Details</th>");
        sb.AppendLine("                </tr>");
        sb.AppendLine("            </thead>");
        sb.AppendLine("            <tbody>");

        foreach (var result in results.OrderBy(r => r.Passed).ThenBy(r => r.FormName))
        {
            var statusClass = result.Passed ? "status-pass" : "status-fail";
            var statusText = result.Passed ? "✅ PASS" : "❌ FAIL";
            var themeText = result.ThemeValid ? "✅" : "❌";

            sb.AppendLine("                <tr>");
            sb.AppendLine($"                    <td><strong>{result.FormName}</strong></td>");
            sb.AppendLine($"                    <td class=\"{statusClass}\">{statusText}</td>");
            sb.AppendLine($"                    <td>{themeText}</td>");
            sb.AppendLine($"                    <td>{result.ViolationCount}</td>");
            sb.AppendLine("                    <td>");

            if (!string.IsNullOrEmpty(result.Error))
            {
                sb.AppendLine($"                        <div class=\"error\">{result.Error}</div>");
            }
            else if (result.ViolationCount > 0)
            {
                sb.AppendLine("                        <div class=\"violations\">");
                sb.AppendLine("                            <strong>Manual Color Violations:</strong>");
                sb.AppendLine("                            <ul>");
                foreach (var violation in result.ManualColorViolations)
                {
                    sb.AppendLine($"                                <li>{violation}</li>");
                }
                sb.AppendLine("                            </ul>");
                sb.AppendLine("                        </div>");
            }
            else if (result.Passed)
            {
                sb.AppendLine("                        <span style=\"color: #27ae60;\">All checks passed</span>");
            }

            sb.AppendLine("                    </td>");
            sb.AppendLine("                </tr>");
        }

        sb.AppendLine("            </tbody>");
        sb.AppendLine("        </table>");

        sb.AppendLine($"        <div class=\"timestamp\">Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}

/// <summary>
/// Result of a single form validation.
/// </summary>
public class FormValidationResult
{
    public required string FormTypeName { get; set; }
    public required string FormName { get; set; }
    public required string ExpectedTheme { get; set; }
    public bool Passed { get; set; }
    public bool ThemeValid { get; set; }
    public int ViolationCount { get; set; }
    public string[] ManualColorViolations { get; set; } = Array.Empty<string>();
    public string? Error { get; set; }
    public DateTime ValidationTime { get; set; }
}
