using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.McpServer.Helpers;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.McpServer.Tools;

/// <summary>
/// MCP tool to inspect and validate Syncfusion DockingManager configuration.
/// Validates configuration properties, docked controls, state persistence, and best practices.
/// </summary>
[McpServerToolType]
public static class InspectDockingManagerTool
{
    [McpServerTool]
    [Description("Inspects and validates Syncfusion DockingManager configuration on a form. " +
                 "Returns comprehensive details about docking properties, docked controls, " +
                 "labels, visibility states, and configuration compliance with best practices.")]
    public static string InspectDockingManager(
        [Description("Fully qualified type name of the form containing DockingManager (e.g., 'WileyWidget.WinForms.Forms.MainForm')")]
        string formTypeName,
        [Description("Output format: 'text' or 'json' (default: 'text')")]
        string outputFormat = "text")
    {
        ArgumentNullException.ThrowIfNull(outputFormat);
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

            // Create mock MainForm with docking enabled
            mockMainForm = MockFactory.CreateMockMainForm();

            // Instantiate form
            form = FormInstantiationHelper.InstantiateForm(formType, mockMainForm);
            if (form == null)
            {
                return FormatError($"Failed to instantiate form: {formTypeName}", outputFormat);
            }

            // Load form with theme to trigger full initialization
            var loaded = FormInstantiationHelper.LoadFormWithTheme(form);
            if (!loaded)
            {
                return FormatError($"Failed to load form with theme: {formTypeName}", outputFormat);
            }

            // Find DockingManager
            var dockingManager = FindDockingManager(form);
            if (dockingManager == null)
            {
                return FormatError("No DockingManager found on form", outputFormat);
            }

            // Collect inspection results
            var result = InspectDockingManagerConfiguration(dockingManager, form);

            // Format output
            return outputFormat.Equals("json", StringComparison.OrdinalIgnoreCase)
                ? FormatJson(result)
                : FormatText(result);
        }
        catch (Exception ex)
        {
            return FormatError($"Inspection failed: {ex.Message}\n{ex.StackTrace}", outputFormat);
        }
        finally
        {
            FormInstantiationHelper.SafeDispose(form, mockMainForm);
        }
    }

    private static DockingManager? FindDockingManager(Control control)
    {
        // DockingManager is a component, not a control - search form's components
        if (control is Form form && form.Site?.Container != null)
        {
            foreach (System.ComponentModel.IComponent component in form.Site.Container.Components)
            {
                if (component is DockingManager dm)
                {
                    return dm;
                }
            }
        }

        // Fallback: search via reflection for private _dockingManager field
        var dockingManagerField = control.GetType()
            .GetField("_dockingManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (dockingManagerField != null)
        {
            var value = dockingManagerField.GetValue(control);
            if (value is DockingManager dm)
            {
                return dm;
            }
        }

        return null;
    }

    private static DockingManagerInspectionResult InspectDockingManagerConfiguration(DockingManager dm, Form form)
    {
        var result = new DockingManagerInspectionResult
        {
            FormTypeName = form.GetType().FullName ?? "Unknown",
            HostControlName = dm.HostControl?.Name ?? form.Name ?? "(null)"
        };

        // Core configuration properties
        result.EnableDocumentMode = dm.EnableDocumentMode;
        result.PersistState = dm.PersistState;
        result.AnimateAutoHiddenWindow = dm.AnimateAutoHiddenWindow;
        result.ShowCaption = dm.ShowCaption;
        result.DockBehavior = dm.DockBehavior.ToString();

        // Visual properties
        result.AutoHideTabFont = dm.AutoHideTabFont?.ToString() ?? "(null)";
        result.DockTabFont = dm.DockTabFont?.ToString() ?? "(null)";

        // Auto-hide properties
        result.AutoHideActiveControl = dm.AutoHideActiveControl;

        // Enumerate docked controls
        var dockedControls = GetDockedControls(dm, form);
        result.DockedControls = dockedControls.Select(ctrl => InspectDockedControl(dm, ctrl)).ToList();
        result.DockedControlCount = result.DockedControls.Count;

        // Validate configuration
        result.ValidationIssues = ValidateConfiguration(dm, form, result);
        result.IsValid = result.ValidationIssues.Count == 0;

        // Best practices compliance
        result.BestPracticesScore = CalculateBestPracticesScore(result);

        return result;
    }

    private static List<Control> GetDockedControls(DockingManager dm, Form form)
    {
        var dockedControls = new List<Control>();

        // Recursively find all controls that are docked
        FindDockedControlsRecursive(form, dm, dockedControls);

        return dockedControls;
    }

    private static void FindDockedControlsRecursive(Control parent, DockingManager dm, List<Control> dockedControls)
    {
        foreach (Control child in parent.Controls)
        {
            // Check if this control is enabled for docking
            try
            {
                if (dm.GetEnableDocking(child))
                {
                    dockedControls.Add(child);
                }
            }
            catch
            {
                // Control not docked, continue
            }

            // Recursively check children
            if (child.HasChildren)
            {
                FindDockedControlsRecursive(child, dm, dockedControls);
            }
        }
    }

    private static DockedControlInfo InspectDockedControl(DockingManager dm, Control control)
    {
        var info = new DockedControlInfo
        {
            Name = control.Name ?? "(unnamed)",
            Type = control.GetType().Name
        };

        try
        {
            info.DockLabel = dm.GetDockLabel(control) ?? "(no label)";
            info.DockStyle = dm.GetDockStyle(control).ToString();
            info.DockVisibility = dm.GetDockVisibility(control).ToString();
            info.AutoHideMode = dm.GetAutoHideOnLoad(control); // GetAutoHideOnLoad instead of IsAutoHidden
            info.FloatOnly = dm.GetFloatOnly(control);
            info.EnableDocking = dm.GetEnableDocking(control);
            info.IsVisible = control.Visible;
            info.Size = $"{control.Width}x{control.Height}";

                // Per-control caption button visibility (may not be available in older Syncfusion versions)
                try { info.CloseButtonVisible = dm.GetCloseButtonVisibility(control); } catch { }
                try { info.AutoHideButtonVisible = dm.GetAutoHideButtonVisibility(control); } catch { }
                try { info.MenuButtonVisible = dm.GetMenuButtonVisibility(control); } catch { }
        var issues = new List<string>();

        // Validate HostControl is set
        if (dm.HostControl == null)
        {
            issues.Add("❌ CRITICAL: HostControl is null - DockingManager requires a host form");
        }
        else if (dm.HostControl != form)
        {
            issues.Add($"⚠️  WARNING: HostControl mismatch - expected {form.Name}, got {dm.HostControl.Name}");
        }

        // Validate docked controls exist
        if (result.DockedControlCount == 0)
        {
            issues.Add("⚠️  WARNING: No docked controls found - DockingManager is initialized but unused");
        }
        else
        {
            // Check if at least one control is visible
            var visibleCount = result.DockedControls.Count(c => c.IsVisible);
            if (visibleCount == 0)
            {
                issues.Add("⚠️  WARNING: All docked controls are hidden - users won't see any panels");
            }
        }

        // Validate docked controls have labels and check for issues
        foreach (var ctrl in result.DockedControls)
        {
            // Empty or missing dock labels
            if (string.IsNullOrWhiteSpace(ctrl.DockLabel) || ctrl.DockLabel == "(no label)")
            {
                issues.Add($"⚠️  WARNING: Control '{ctrl.Name}' has empty/missing dock label - users won't see panel title (use SetDockLabel)");
            }

            // EnableDocking mismatch
            if (!ctrl.EnableDocking)
            {
                issues.Add($"❌ ERROR: Control '{ctrl.Name}' found in docked list but EnableDocking=false - inconsistent state");
            }

            // FloatOnly warning if multiple controls have it (unusual pattern)
            if (ctrl.FloatOnly)
            {
                issues.Add($"ℹ️  INFO: Control '{ctrl.Name}' is FloatOnly=true - cannot be re-docked if floated");
            }

            // Hidden controls (potential issue)
            if (!ctrl.IsVisible)
            {
                issues.Add($"ℹ️  INFO: Control '{ctrl.Name}' is docked but hidden (Visible=false) - intentional?");
            }
        }

        // Validate EnableDocumentMode (WileyWidget standard: should be false)
        if (dm.EnableDocumentMode)
        {
            issues.Add("⚠️  POLICY VIOLATION: EnableDocumentMode=true violates WileyWidget standard (should be false - use TabbedMDI instead)");
        }

        // Validate PersistState is enabled (best practice - strongly recommended)
        if (!dm.PersistState)
        {
            issues.Add("⚠️  BEST PRACTICE (RECOMMENDED): PersistState=false - strongly recommend enabling to preserve user's layout preferences across sessions");
        }

        // Validate ShowCaption (best practice)
        if (!dm.ShowCaption)
        {
            issues.Add("⚠️  BEST PRACTICE: ShowCaption=false - users cannot see panel titles");
        }

        // Validate fonts are set (accessibility)
        if (string.IsNullOrEmpty(result.AutoHideTabFont) || result.AutoHideTabFont == "(null)")
        {
            issues.Add("⚠️  ACCESSIBILITY: AutoHideTabFont not set - may use default system font");
        }

        if (string.IsNullOrEmpty(result.DockTabFont) || result.DockTabFont == "(null)")
        {
            issues.Add("⚠️  ACCESSIBILITY: DockTabFont not set - may use default system font");
        }

        return issues;
    }

    private static int CalculateBestPracticesScore(DockingManagerInspectionResult result)
    {
        int score = 100;

        // Deduct points for issues
        foreach (var issue in result.ValidationIssues)
        {
            if (issue.Contains("CRITICAL", StringComparison.Ordinal))
                score -= 30;
            else if (issue.Contains("ERROR", StringComparison.Ordinal))
                score -= 20;
            else if (issue.Contains("POLICY VIOLATION", StringComparison.Ordinal))
                score -= 15;
            else if (issue.Contains("BEST PRACTICE", StringComparison.Ordinal))
                score -= 10;
            else if (issue.Contains("WARNING", StringComparison.Ordinal))
                score -= 5;
        }

        // Bonus points for good practices
        if (result.DockedControlCount > 0)
            score += 10; // Using DockingManager effectively

        if (result.PersistState)
            score += 5; // State persistence enabled

        if (!result.EnableDocumentMode)
            score += 5; // Following WileyWidget standard

        return Math.Max(0, Math.Min(100, score));
    }

    private static string FormatText(DockingManagerInspectionResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("        DOCKINGMANAGER CONFIGURATION INSPECTION");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Form:                    {result.FormTypeName}");
        sb.AppendLine($"Host Control:            {result.HostControlName}");
        sb.AppendLine($"Validation Status:       {(result.IsValid ? "✅ PASS" : "❌ FAIL")}");
        sb.AppendLine($"Best Practices Score:    {result.BestPracticesScore}/100");
        sb.AppendLine();

        sb.AppendLine("CORE CONFIGURATION:");
        sb.AppendLine($"  EnableDocumentMode:           {result.EnableDocumentMode} {(result.EnableDocumentMode ? "⚠️  (WileyWidget standard: false)" : "✅")}");
        sb.AppendLine($"  PersistState:                 {result.PersistState} {(result.PersistState ? "✅" : "⚠️ ")}");
        sb.AppendLine($"  AnimateAutoHiddenWindow:      {result.AnimateAutoHiddenWindow}");
        sb.AppendLine($"  ShowCaption:                  {result.ShowCaption}");
        sb.AppendLine($"  DockBehavior:                 {result.DockBehavior}");
        sb.AppendLine();

        sb.AppendLine("AUTO-HIDE SETTINGS:");
        sb.AppendLine($"  AutoHideActiveControl:        {result.AutoHideActiveControl}");
        sb.AppendLine();

        sb.AppendLine("VISUAL PROPERTIES:");
        sb.AppendLine($"  AutoHideTabFont:              {result.AutoHideTabFont}");
        sb.AppendLine($"  DockTabFont:                  {result.DockTabFont}");
        sb.AppendLine();

        sb.AppendLine($"DOCKED CONTROLS ({result.DockedControlCount}):");
        if (result.DockedControls.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            foreach (var ctrl in result.DockedControls)
            {
                sb.AppendLine($"  • {ctrl.Name} ({ctrl.Type})");
                sb.AppendLine($"      Label:          {ctrl.DockLabel}");
                sb.AppendLine($"      Dock Style:     {ctrl.DockStyle}");
                sb.AppendLine($"      Visibility:     {ctrl.DockVisibility}");
                sb.AppendLine($"      Auto-Hide:      {ctrl.AutoHideMode}");
                sb.AppendLine($"      Float Only:     {ctrl.FloatOnly}");
                sb.AppendLine($"      Size:           {ctrl.Size}");
                sb.AppendLine($"      Visible:        {ctrl.IsVisible}");
                if (!string.IsNullOrEmpty(ctrl.Error))
                {
                    sb.AppendLine($"      ⚠️  Error:      {ctrl.Error}");
                }
                sb.AppendLine();
            }
        }

        if (result.ValidationIssues.Count > 0)
        {
            sb.AppendLine("VALIDATION ISSUES:");
            foreach (var issue in result.ValidationIssues)
            {
                sb.AppendLine($"  {issue}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════");
        return sb.ToString();
    }

    private static string FormatJson(DockingManagerInspectionResult result)
    {
        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static string FormatError(string error, string outputFormat)
    {
        if (outputFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { error }, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        return $"❌ ERROR: {error}";
    }
}

internal class DockingManagerInspectionResult
{
    public string FormTypeName { get; set; } = string.Empty;
    public string HostControlName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public int BestPracticesScore { get; set; }

    // Core configuration
    public bool EnableDocumentMode { get; set; }
    public bool PersistState { get; set; }
    public bool AnimateAutoHiddenWindow { get; set; }
    public bool ShowCaption { get; set; }
    public string DockBehavior { get; set; } = string.Empty;

    // Visual properties
    public string AutoHideTabFont { get; set; } = string.Empty;
    public string DockTabFont { get; set; } = string.Empty;

    // Auto-hide
    public bool AutoHideActiveControl { get; set; }

    // Docked controls
    public int DockedControlCount { get; set; }
    public List<DockedControlInfo> DockedControls { get; set; } = new();

    // Validation
    public List<string> ValidationIssues { get; set; } = new();
}

internal class DockedControlInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string DockLabel { get; set; } = string.Empty;
    public string DockStyle { get; set; } = string.Empty;
    public string DockVisibility { get; set; } = string.Empty;
    public bool AutoHideMode { get; set; }
    public bool FloatOnly { get; set; }
    public bool EnableDocking { get; set; }
    public bool IsVisible { get; set; }
    public string Size { get; set; } = string.Empty;

    // Caption button visibility (may not be supported in older Syncfusion versions)
    public bool CloseButtonVisible { get; set; }
    public bool AutoHideButtonVisible { get; set; }
    public bool MenuButtonVisible { get; set; }

    public string? Error { get; set; }
}
