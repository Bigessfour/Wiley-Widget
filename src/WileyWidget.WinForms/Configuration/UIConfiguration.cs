using Microsoft.Extensions.Configuration;
using System.Drawing;

namespace WileyWidget.WinForms.Configuration;

/// <summary>
/// Centralized UI configuration for the WinForms application.
/// Phase 1 Simplification: Hard-coded architecture with const-like values.
/// </summary>
public sealed record UIConfiguration
{
    /// <summary>
    /// Always use Syncfusion DockingManager (Phase 1: const true).
    /// </summary>
    public bool UseSyncfusionDocking { get; init; } = true;

    /// <summary>
    /// Always use MDI mode for child forms (Phase 1: const true).
    /// </summary>
    public bool UseMdiMode { get; init; } = true;

    /// <summary>
    /// TabbedMDI permanently disabled (Phase 1: const false).
    /// </summary>
    public bool UseTabbedMdi { get; init; } = false;

    /// <summary>
    /// Test harness mode - disables MessageBox, dialogs, and heavyweight UI for automated testing.
    /// </summary>
    public bool IsUiTestHarness { get; init; } = false;

    /// <summary>
    /// Default theme to apply on startup.
    /// Phase 1: Hard-coded to Office2019Colorful.
    /// </summary>
    public string DefaultTheme { get; init; } = "Office2019Colorful";

    /// <summary>
    /// Whether to show the Ribbon control (disabled in test harness mode).
    /// </summary>
    public bool ShowRibbon { get; init; } = true;

    /// <summary>
    /// Whether to show the menu bar (always enabled).
    /// </summary>
    public bool ShowMenuBar { get; init; } = true;

    /// <summary>
    /// Whether to show the status bar (always enabled).
    /// </summary>
    public bool ShowStatusBar { get; init; } = true;

    /// <summary>
    /// Default form size for MainForm.
    /// </summary>
    public Size DefaultFormSize { get; init; } = new(1400, 900);

    /// <summary>
    /// Minimum form size for MainForm.
    /// </summary>
    public Size MinimumFormSize { get; init; } = new(1024, 768);

    /// <summary>
    /// Whether to auto-show dashboard on startup.
    /// </summary>
    public bool AutoShowDashboard { get; init; } = false;

    /// <summary>
    /// Default fiscal year for charts and reports.
    /// </summary>
    public int DefaultFiscalYear { get; init; } = DateTime.UtcNow.Year;

    /// <summary>
    /// Creates UIConfiguration from IConfiguration.
    /// Phase 1: Most values are hard-coded, only test harness and theme are read from config.
    /// </summary>
    public static UIConfiguration FromConfiguration(IConfiguration configuration)
    {
        var isTestHarness = configuration.GetValue<bool>("UI:IsUiTestHarness", false);

        return new UIConfiguration
        {
            // Phase 1: Hard-coded architecture, but configurable for tests
            UseSyncfusionDocking = true,
            UseMdiMode = configuration.GetValue<bool>("UI:UseMdiMode", true),
            UseTabbedMdi = configuration.GetValue<bool>("UI:UseTabbedMdi", false),

            // Read from config
            IsUiTestHarness = isTestHarness,
            DefaultTheme = "Office2019Colorful", // Phase 1: Hard-coded

            // Chrome configuration
            ShowRibbon = !isTestHarness, // Ribbon disabled in test harness
            ShowMenuBar = true,
            ShowStatusBar = true,

            // Form defaults
            DefaultFormSize = new Size(1400, 900),
            MinimumFormSize = new Size(1024, 768),

            // Feature flags
            AutoShowDashboard = configuration.GetValue<bool>("UI:AutoShowDashboard", false),
            DefaultFiscalYear = configuration.GetValue<int>("UI:DefaultFiscalYear", DateTime.UtcNow.Year)
        };
    }

    /// <summary>
    /// Validates the UI configuration and returns validation errors.
    /// </summary>
    /// <returns>List of validation errors, empty if valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        // Validate form sizes
        if (DefaultFormSize.Width < MinimumFormSize.Width || DefaultFormSize.Height < MinimumFormSize.Height)
        {
            errors.Add($"DefaultFormSize ({DefaultFormSize}) must be >= MinimumFormSize ({MinimumFormSize})");
        }

        if (MinimumFormSize.Width < 800 || MinimumFormSize.Height < 600)
        {
            errors.Add($"MinimumFormSize ({MinimumFormSize}) is too small (minimum 800x600)");
        }

        // Validate fiscal year
        var currentYear = DateTime.UtcNow.Year;
        if (DefaultFiscalYear < 2000 || DefaultFiscalYear > currentYear + 10)
        {
            errors.Add($"DefaultFiscalYear ({DefaultFiscalYear}) is out of valid range (2000 to {currentYear + 10})");
        }

        // Validate theme
        if (string.IsNullOrWhiteSpace(DefaultTheme))
        {
            errors.Add("DefaultTheme cannot be empty");
        }

        return errors;
    }

    /// <summary>
    /// Gets a display-friendly string describing the UI architecture.
    /// </summary>
    public string GetArchitectureDescription()
    {
        return $"Docking={UseSyncfusionDocking}, MDI={UseMdiMode}, TabbedMDI={UseTabbedMdi}, TestHarness={IsUiTestHarness}";
    }
}
