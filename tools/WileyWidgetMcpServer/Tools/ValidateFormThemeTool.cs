using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Forms;
using WileyWidget.McpServer.Helpers;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.McpServer.Tools;

/// <summary>
/// MCP tool for validating form theme compliance with SkinManager.
/// Loads a form headlessly and checks for manual color assignments.
/// </summary>
[McpServerToolType]
public static class ValidateFormThemeTool
{
    [McpServerTool]
    [Description("Validates that a WinForms form uses SkinManager theming exclusively (no manual BackColor/ForeColor assignments).")]
    public static string ValidateFormTheme(
        [Description("Fully qualified type name of the form to validate (e.g., 'WileyWidget.WinForms.Forms.AccountsForm')")]
        string formTypeName,
        [Description("Expected theme name (default: 'Office2019Colorful')")]
        string expectedTheme = "Office2019Colorful",
        [Description("Output format: 'text' or 'json' (default: 'text')")]
        string outputFormat = "text")
    {
        Form? form = null;
        MainForm? mockMainForm = null;

        try
        {
            // Get form type from cache
            var formType = FormTypeCache.GetFormType(formTypeName);
            if (formType == null)
            {
                return FormatError($"Form type not found: {formTypeName}", outputFormat);
            }

            // Create mock MainForm with MDI enabled for realism
            mockMainForm = MockFactory.CreateMockMainForm(enableMdi: true);

            // Instantiate form with proper constructor handling
            try
            {
                form = FormInstantiationHelper.InstantiateForm(formType, mockMainForm);
            }
            catch (Exception ex)
            {
                return FormatError($"Failed to instantiate form: {ex.Message}", outputFormat);
            }

            // Load form with theme applied
            var loaded = FormInstantiationHelper.LoadFormWithTheme(form, expectedTheme);
            if (!loaded)
            {
                return FormatError($"Failed to load form: {formTypeName}", outputFormat);
            }

            // Validate theme
            var themeValid = SyncfusionTestHelper.ValidateTheme(form, expectedTheme);

            // Check for manual colors
            var violations = SyncfusionTestHelper.ValidateNoManualColors(form);

            // Build result
            var result = new ValidationResult
            {
                FormTypeName = formTypeName,
                FormName = formType.Name,
                ExpectedTheme = expectedTheme,
                Passed = themeValid && violations.Count == 0,
                ThemeValid = themeValid,
                ViolationCount = violations.Count,
                Violations = violations.ToArray()
            };

            return outputFormat.ToLowerInvariant() == "json"
                ? FormatJson(result)
                : FormatText(result);
        }
        catch (Exception ex)
        {
            return FormatError($"Validation error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", outputFormat);
        }
        finally
        {
            // Safe cleanup with disposal error suppression
            FormInstantiationHelper.SafeDispose(form, mockMainForm);
        }
    }

    private static string FormatText(ValidationResult result)
    {
        var lines = new List<string>
        {
            $"✅ Form Validation: {result.FormTypeName}",
            "",
            $"Theme Check: {(result.ThemeValid ? "✅ PASS" : "❌ FAIL")}",
            $"Manual Color Check: {(result.ViolationCount == 0 ? "✅ PASS" : $"❌ FAIL ({result.ViolationCount} violations)")}",
            ""
        };

        if (result.ViolationCount > 0)
        {
            lines.Add("Violations:");
            foreach (var violation in result.Violations)
            {
                lines.Add($"  - {violation}");
            }
        }
        else
        {
            lines.Add("No violations found. Form uses SkinManager theming exclusively.");
        }

        return string.Join("\n", lines);
    }

    private static string FormatJson(ValidationResult result)
    {
        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static string FormatError(string errorMessage, string outputFormat)
    {
        if (outputFormat.ToLowerInvariant() == "json")
        {
            var error = new { error = errorMessage, success = false };
            return JsonSerializer.Serialize(error, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        return $"❌ Error: {errorMessage}";
    }

    private class ValidationResult
    {
        public required string FormTypeName { get; set; }
        public required string FormName { get; set; }
        public required string ExpectedTheme { get; set; }
        public bool Passed { get; set; }
        public bool ThemeValid { get; set; }
        public int ViolationCount { get; set; }
        public string[] Violations { get; set; } = Array.Empty<string>();
    }
}
