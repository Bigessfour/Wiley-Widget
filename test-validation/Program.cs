using System;
using System.Windows.Forms;
using System.Reflection;
using System.Text.RegularExpressions;
using WileyWidget.McpServer.Helpers;
using WileyWidget.McpServer.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Licensing;

namespace PanelValidationTest;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
        Console.WriteLine("PANEL BATCH VALIDATION - LIVE TEST RUN");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
        Console.WriteLine();

        try
        {
            // ────────────────────────────────────────────────────────────────
            // CRITICAL: Initialize Syncfusion theme system before panel creation
            // Mimic production app startup to ensure theme validation works
            // ────────────────────────────────────────────────────────────────
            Console.WriteLine("🔑 Registering Syncfusion license...");
            try
            {
                var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                if (!string.IsNullOrEmpty(licenseKey))
                {
                    SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    Console.WriteLine("   ✓ License registered successfully");
                }
                else
                {
                    Console.WriteLine("   ⚠ SYNCFUSION_LICENSE_KEY not found - running in trial mode");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠ License registration failed: {ex.Message}");
                Console.WriteLine("   → Continuing in trial mode (license warnings may appear)");
            }

            Console.WriteLine();
            Console.WriteLine("📦 Loading Syncfusion theme assemblies...");
            try
            {
                // Load Office2019Theme assembly (supports Office2019Colorful, Office2019Black, Office2019White, Office2019DarkGray)
                // This is the primary theme package used in production - must be loaded before any panel instantiation
                SkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
                Console.WriteLine("   ✓ Office2019Theme assembly loaded");
                Console.WriteLine("   → Supports: Office2019Colorful, Office2019Black, Office2019White, Office2019DarkGray");

                // Set global application theme (helps controls inherit theme automatically)
                SfSkinManager.ApplicationVisualTheme = "Office2019Colorful";
                Console.WriteLine($"   ✓ Global theme set to: {SfSkinManager.ApplicationVisualTheme}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ FATAL: Failed to load Office2019Theme assembly");
                Console.WriteLine($"   Error: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Theme assembly is required for panel validation. Ensure:");
                Console.WriteLine("  1. Syncfusion.Office2019Theme.WinForms NuGet package (v32.1.19) is installed");
                Console.WriteLine("  2. Package references are restored (dotnet restore)");
                Console.WriteLine("  3. Build succeeded before running validation");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine();
            Console.WriteLine("────────────────────────────────────────────────────────────────");

            // Discover panels first
            Console.WriteLine("📍 Discovering panels...");
            var panels = PanelTypeCache.GetAllPanelTypes();
            Console.WriteLine($"✓ Found {panels.Count} panels in WileyWidget.WinForms.Controls");

            if (panels.Count > 0)
            {
                Console.WriteLine("\n📋 Panels discovered:");
                foreach (var panel in panels.Take(5))
                {
                    Console.WriteLine($"  - {panel.Name}");
                }
                if (panels.Count > 5)
                {
                    Console.WriteLine($"  ... and {panels.Count - 5} more");
                }
            }

            Console.WriteLine();
            Console.WriteLine("────────────────────────────────────────────────────────────────");
            Console.WriteLine("🚀 Running batch validation...");
            Console.WriteLine();

            var startTime = DateTime.Now;

            // Run validation with all formats for comprehensive analysis
            var htmlResult = BatchValidatePanelsTool.BatchValidatePanels(
                panelTypeNames: null,  // All panels
                expectedTheme: "Office2019Colorful",
                failFast: false,
                outputFormat: "html"
            );

            var textResult = BatchValidatePanelsTool.BatchValidatePanels(
                panelTypeNames: null,
                expectedTheme: "Office2019Colorful",
                failFast: false,
                outputFormat: "text"
            );

            var duration = DateTime.Now - startTime;

            // Save reports
            File.WriteAllText("report.html", htmlResult);
            File.WriteAllText("report.txt", textResult);

            Console.WriteLine();
            Console.WriteLine("────────────────────────────────────────────────────────────────");
            Console.WriteLine("📊 VALIDATION RESULTS");
            Console.WriteLine("────────────────────────────────────────────────────────────────");
            Console.WriteLine();

            // Parse and analyze results
            var analysis = AnalyzeReport(textResult, panels.Count);

            Console.WriteLine($"Total Panels:    {analysis.TotalPanels}");
            Console.WriteLine($"Passed:          {analysis.PassedPanels} ({analysis.PassPercentage:F1}%)");
            Console.WriteLine($"Failed:          {analysis.FailedPanels} ({analysis.FailPercentage:F1}%)");
            Console.WriteLine($"Duration:        {duration.TotalSeconds:F2}s");
            Console.WriteLine();

            Console.WriteLine("📋 FAILURE BREAKDOWN:");
            Console.WriteLine($"   Theme Issues:            {analysis.ThemeFailures} panels");
            Console.WriteLine($"   Control Compliance:      {analysis.ControlComplianceFailures} panels");
            Console.WriteLine($"   MVVM Binding Issues:     {analysis.MvvmFailures} panels");
            Console.WriteLine($"   Validation Setup:        {analysis.ValidationSetupFailures} panels");
            Console.WriteLine();

            Console.WriteLine("📁 REPORTS SAVED:");
            Console.WriteLine($"   HTML: {Path.GetFullPath("report.html")}");
            Console.WriteLine($"   TEXT: {Path.GetFullPath("report.txt")}");
            Console.WriteLine();

            // Show detailed failure analysis
            if (analysis.FailedPanels > 0)
            {
                ShowDetailedFailures(analysis);
            }

            // Actionable next steps
            Console.WriteLine("────────────────────────────────────────────────────────────────");
            Console.WriteLine("🎯 RECOMMENDED ACTIONS (Per Panel_Prompt.md)");
            Console.WriteLine("────────────────────────────────────────────────────────────────");
            Console.WriteLine();

            if (analysis.ThemeFailures > 0)
            {
                Console.WriteLine($"1️⃣  FIX THEME ISSUES ({analysis.ThemeFailures} panels):");
                Console.WriteLine("   Add to each panel constructor (after InitializeComponent):");
                Console.WriteLine("   ");
                Console.WriteLine("   var theme = SfSkinManager.ApplicationVisualTheme ?? \"Office2019Colorful\";");
                Console.WriteLine("   SfSkinManager.SetVisualStyle(this, theme);");
                Console.WriteLine();
            }

            if (analysis.MvvmFailures > 0)
            {
                Console.WriteLine($"2️⃣  FIX MVVM BINDINGS ({analysis.MvvmFailures} panels) - Panel_Prompt.md Category #4:");
                Console.WriteLine("   Required per ICompletablePanel interface:");
                Console.WriteLine("   ");
                Console.WriteLine("   • Public ViewModel property (strongly-typed, not object)");
                Console.WriteLine("   • ViewModel implements INotifyPropertyChanged");
                Console.WriteLine("   • Controls use DataBindings.Add() with BindingSource:");
                Console.WriteLine("     _bindingSource = new BindingSource { DataSource = ViewModel };");
                Console.WriteLine("     control.DataBindings.Add(\"Text\", _bindingSource, \"PropertyName\",");
                Console.WriteLine("         formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged);");
                Console.WriteLine("   ");
                Console.WriteLine("   • Commands wired via async handlers (no .Result/.Wait())");
                Console.WriteLine("   • CancellationToken support for async operations");
                Console.WriteLine();
            }

            if (analysis.ValidationSetupFailures > 0)
            {
                Console.WriteLine($"3️⃣  FIX VALIDATION SETUP ({analysis.ValidationSetupFailures} panels) - Panel_Prompt.md Category #5:");
                Console.WriteLine("   Required for input panels per ICompletablePanel:");
                Console.WriteLine("   ");
                Console.WriteLine("   • ErrorProvider field and initialization:");
                Console.WriteLine("     private ErrorProvider _errorProvider;");
                Console.WriteLine("     private ErrorProviderBinding _errorBinding;");
                Console.WriteLine("   ");
                Console.WriteLine("     _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };");
                Console.WriteLine("     _errorBinding = new ErrorProviderBinding(_errorProvider, ViewModel);");
                Console.WriteLine("     _errorBinding.MapControl(nameof(ViewModel.Property), controlInstance);");
                Console.WriteLine("   ");
                Console.WriteLine("   • Override ValidateAsync(CancellationToken):");
                Console.WriteLine("     - Check required fields");
                Console.WriteLine("     - Validate ranges/formats");
                Console.WriteLine("     - Call ViewModel.ValidateAsync() for async checks");
                Console.WriteLine("     - Return ValidationResult.Success or ValidationResult.Failed(items)");
                Console.WriteLine("   ");
                Console.WriteLine("   • Dispose pattern: unsubscribe events, dispose ErrorProvider");
                Console.WriteLine();
            }

            if (analysis.ControlComplianceFailures > 0)
            {
                Console.WriteLine($"4️⃣  FIX CONTROL COMPLIANCE ({analysis.ControlComplianceFailures} panels) - Panel_Prompt.md Category #2:");
                Console.WriteLine("   Use Syncfusion v32.1.19 controls exclusively:");
                Console.WriteLine("   ");
                Console.WriteLine("   Replace legacy WinForms → Syncfusion equivalents:");
                Console.WriteLine("     • ComboBox         → SfComboBox");
                Console.WriteLine("     • TextBox (numeric) → SfNumericTextBox / SfNumericUpDown");
                Console.WriteLine("     • DataGridView     → SfDataGrid");
                Console.WriteLine("     • DateTimePicker   → SfDateTimeEdit");
                Console.WriteLine("     • Button (styled)  → SfButton");
                Console.WriteLine("   ");
                Console.WriteLine("   Validate against Syncfusion v32.1.19 API:");
                Console.WriteLine("     https://help.syncfusion.com/windowsforms/overview");
                Console.WriteLine("   ");
                Console.WriteLine("   Set ThemeName on all Syncfusion controls:");
                Console.WriteLine("     control.ThemeName = \"Office2019Colorful\";");
                Console.WriteLine();
            }

            Console.WriteLine("────────────────────────────────────────────────────────────────");
            Console.WriteLine();

            if (analysis.PassedPanels == analysis.TotalPanels)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ ALL PANELS PASSED VALIDATION!");
                Console.WriteLine();
                Console.WriteLine("All panels comply with Panel_Prompt.md requirements:");
                Console.WriteLine("  ✓ Theme management via SfSkinManager");
                Console.WriteLine("  ✓ Syncfusion v32.1.19 controls only");
                Console.WriteLine("  ✓ Proper MVVM bindings with INotifyPropertyChanged");
                Console.WriteLine("  ✓ ErrorProvider validation setup");
                Console.WriteLine("  ✓ ICompletablePanel interface implementation");
                Console.ResetColor();
                Environment.Exit(0);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠️  {analysis.FailedPanels} panel(s) require fixes before merging");
                Console.WriteLine();
                Console.WriteLine("Next steps:");
                Console.WriteLine($"  1. Review HTML report: {Path.GetFullPath("report.html")}");
                Console.WriteLine("  2. Fix issues per Panel_Prompt.md categories above");
                Console.WriteLine("  3. Re-run validation: dotnet run --project test-validation");
                Console.WriteLine("  4. Achieve 100% pass rate before PR");
                Console.ResetColor();
                Console.WriteLine();
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("❌ VALIDATION FAILED WITH ERROR");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Stack Trace:");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            Environment.Exit(1);
        }
    }

    private static ValidationAnalysis AnalyzeReport(string textReport, int totalPanels)
    {
        var analysis = new ValidationAnalysis { TotalPanels = totalPanels };

        // Parse the text report to extract detailed statistics
        var lines = textReport.Split('\n');
        string? currentPanel = null;

        foreach (var line in lines)
        {
            // Track current panel being analyzed
            if (line.Contains("Panel:") && !line.Contains("Total Panels"))
            {
                currentPanel = line.Replace("Panel:", "").Trim();
            }

            // Count failures by category
            if (line.Contains("❌ Theme:"))
            {
                analysis.ThemeFailures++;
                if (currentPanel != null && !analysis.FailedPanelsByCategory.ContainsKey("Theme"))
                    analysis.FailedPanelsByCategory["Theme"] = new List<string>();
                if (currentPanel != null)
                    analysis.FailedPanelsByCategory["Theme"].Add(currentPanel);
            }
            if (line.Contains("❌ Control Compliance:"))
            {
                analysis.ControlComplianceFailures++;
                if (currentPanel != null && !analysis.FailedPanelsByCategory.ContainsKey("ControlCompliance"))
                    analysis.FailedPanelsByCategory["ControlCompliance"] = new List<string>();
                if (currentPanel != null)
                    analysis.FailedPanelsByCategory["ControlCompliance"].Add(currentPanel);
            }
            if (line.Contains("❌ MVVM Bindings:"))
            {
                analysis.MvvmFailures++;
                if (currentPanel != null && !analysis.FailedPanelsByCategory.ContainsKey("MVVM"))
                    analysis.FailedPanelsByCategory["MVVM"] = new List<string>();
                if (currentPanel != null)
                    analysis.FailedPanelsByCategory["MVVM"].Add(currentPanel);
            }
            if (line.Contains("❌ Validation Setup:"))
            {
                analysis.ValidationSetupFailures++;
                if (currentPanel != null && !analysis.FailedPanelsByCategory.ContainsKey("Validation"))
                    analysis.FailedPanelsByCategory["Validation"] = new List<string>();
                if (currentPanel != null)
                    analysis.FailedPanelsByCategory["Validation"].Add(currentPanel);
            }
            if (line.Contains("✅ PASS"))
            {
                analysis.PassedPanels++;
                if (currentPanel != null)
                    analysis.PassedPanelsList.Add(currentPanel);
            }
            if (line.Contains("❌ FAIL"))
            {
                if (currentPanel != null)
                    analysis.FailedPanelsList.Add(currentPanel);
            }
        }

        analysis.FailedPanels = totalPanels - analysis.PassedPanels;
        analysis.PassPercentage = totalPanels > 0 ? (analysis.PassedPanels * 100.0 / totalPanels) : 0;
        analysis.FailPercentage = totalPanels > 0 ? (analysis.FailedPanels * 100.0 / totalPanels) : 0;

        return analysis;
    }

    private static void ShowDetailedFailures(ValidationAnalysis analysis)
    {
        if (analysis.FailedPanels == 0) return;

        Console.WriteLine("────────────────────────────────────────────────────────────────");
        Console.WriteLine("📝 DETAILED FAILURE ANALYSIS");
        Console.WriteLine("────────────────────────────────────────────────────────────────");
        Console.WriteLine();

        // Group failures by severity
        var criticalPanels = analysis.FailedPanelsList
            .Where(p => GetFailureCount(p, analysis) >= 3)
            .ToList();

        var moderatePanels = analysis.FailedPanelsList
            .Where(p => GetFailureCount(p, analysis) == 2)
            .ToList();

        var minorPanels = analysis.FailedPanelsList
            .Where(p => GetFailureCount(p, analysis) == 1)
            .ToList();

        if (criticalPanels.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"🔴 CRITICAL ({criticalPanels.Count} panels) - 3+ failures:");
            Console.ResetColor();
            foreach (var panel in criticalPanels.Take(5))
                Console.WriteLine($"   • {panel} ({GetFailureCount(panel, analysis)} issues)");
            if (criticalPanels.Count > 5)
                Console.WriteLine($"   ... and {criticalPanels.Count - 5} more");
            Console.WriteLine();
        }

        if (moderatePanels.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"🟡 MODERATE ({moderatePanels.Count} panels) - 2 failures:");
            Console.ResetColor();
            foreach (var panel in moderatePanels.Take(5))
                Console.WriteLine($"   • {panel}");
            if (moderatePanels.Count > 5)
                Console.WriteLine($"   ... and {moderatePanels.Count - 5} more");
            Console.WriteLine();
        }

        if (minorPanels.Any())
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🔵 MINOR ({minorPanels.Count} panels) - 1 failure:");
            Console.ResetColor();
            foreach (var panel in minorPanels.Take(5))
                Console.WriteLine($"   • {panel}");
            if (minorPanels.Count > 5)
                Console.WriteLine($"   ... and {minorPanels.Count - 5} more");
            Console.WriteLine();
        }

        // Show category-specific lists
        Console.WriteLine("BY CATEGORY:");
        Console.WriteLine();

        foreach (var category in analysis.FailedPanelsByCategory.OrderByDescending(x => x.Value.Count))
        {
            Console.WriteLine($"   {category.Key}: {category.Value.Count} panels");
            if (category.Value.Count <= 3)
            {
                foreach (var panel in category.Value)
                    Console.WriteLine($"      - {panel}");
            }
        }
        Console.WriteLine();
    }

    private static int GetFailureCount(string panelName, ValidationAnalysis analysis)
    {
        int count = 0;
        foreach (var category in analysis.FailedPanelsByCategory.Values)
        {
            if (category.Contains(panelName))
                count++;
        }
        return count;
    }
}

class ValidationAnalysis
{
    public int TotalPanels { get; set; }
    public int PassedPanels { get; set; }
    public int FailedPanels { get; set; }
    public double PassPercentage { get; set; }
    public double FailPercentage { get; set; }
    public int ThemeFailures { get; set; }
    public int ControlComplianceFailures { get; set; }
    public int MvvmFailures { get; set; }
    public int ValidationSetupFailures { get; set; }
    public List<string> PassedPanelsList { get; set; } = new();
    public List<string> FailedPanelsList { get; set; } = new();
    public Dictionary<string, List<string>> FailedPanelsByCategory { get; set; } = new();
}
