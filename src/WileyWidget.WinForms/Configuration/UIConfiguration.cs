using System;
using System.Collections.Generic;
using System.Drawing;
using Microsoft.Extensions.Configuration;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Configuration;

/// <summary>
/// Centralized UI configuration for the WinForms application.
/// Phase 1: Defaults to hard-coded values; can be overridden via IConfiguration (e.g., appsettings.json).
/// </summary>
public sealed record UIConfiguration
{


    /// <summary>
    /// Test harness mode - disables MessageBox, dialogs, and heavyweight UI for automated testing.
    /// </summary>
    public bool IsUiTestHarness { get; init; } = false;

    /// <summary>
    /// Default theme to apply on startup.
    /// </summary>
    public string DefaultTheme { get; init; } = ThemeColors.DefaultTheme;

    /// <summary>
    /// Whether to show the Ribbon control (disabled in test harness mode by default).
    /// </summary>
    public bool ShowRibbon { get; init; } = true;

    /// <summary>
    /// Whether to show the menu bar.
    /// </summary>
    public bool ShowMenuBar { get; init; } = true;

    /// <summary>
    /// Whether to show the status bar.
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
    /// Whether to auto-show the dashboard on startup.
    /// Changed to true by default to provide a meaningful initial view when docking is enabled.
    /// </summary>
    public bool AutoShowDashboard { get; init; } = true;

    /// <summary>
    /// Minimal mode - only JARVIS Chat and central document panel are created.
    /// Prevents auto-show of any other panels on first run or when enabled.
    /// </summary>
    public bool MinimalMode { get; init; } = false;  // Set to false for full UI with visible side panels

    /// <summary>
    /// Whether to auto-show panels based on layout or defaults.
    /// Enabled by default to provide immediate visual feedback when panels are loaded.
    /// </summary>
    public bool AutoShowPanels { get; init; } = true;

    /// <summary>
    /// Default fiscal year for financial views.
    /// </summary>
    public int DefaultFiscalYear { get; init; } = DateTime.UtcNow.Year;

    /// <summary>
    /// When true, wraps panel initialization in SuspendLayout/ResumeLayout to minimize flicker.
    /// </summary>
    public bool EnableDockingLockDuringLoad { get; init; } = true;

    /// <summary>
    /// Maximum depth for recursive theme application (higher = deeper traversal).
    /// </summary>
    public int ThemeApplyMaxDepth { get; init; } = 32;

    /// <summary>
    /// Control type names to skip during recursive theme application.
    /// Supports full type names or simple type names.
    /// </summary>
    public string[] ThemeApplySkipControlTypes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Enable verbose first-chance exception logging (debug builds only).
    /// </summary>
    public bool VerboseFirstChanceExceptions { get; init; } = false;

    /// <summary>
    /// Creates UIConfiguration from IConfiguration. Values in configuration override defaults where present.
    /// </summary>
    public static UIConfiguration FromConfiguration(IConfiguration? configuration)
    {
        if (configuration == null)
        {
            return new UIConfiguration();
        }

        static bool GetBoolean(IConfiguration config, string key, bool defaultValue)
        {
            var rawValue = config[key];
            return bool.TryParse(rawValue, out var parsedValue) ? parsedValue : defaultValue;
        }



        static int GetInteger(IConfiguration config, string key, int defaultValue)
        {
            var rawValue = config[key];
            return int.TryParse(rawValue, out var parsedValue) ? parsedValue : defaultValue;
        }

        var isTestHarness = GetBoolean(configuration, "UI:IsUiTestHarness", false);

        // Read form size pieces (allow Width/Height keys under UI:DefaultFormSize and UI:MinimumFormSize)
        int defaultWidth = GetInteger(configuration, "UI:DefaultFormSize:Width", 1400);
        int defaultHeight = GetInteger(configuration, "UI:DefaultFormSize:Height", 900);
        int minWidth = GetInteger(configuration, "UI:MinimumFormSize:Width", 1024);
        int minHeight = GetInteger(configuration, "UI:MinimumFormSize:Height", 768);

        return new UIConfiguration
        {
            IsUiTestHarness = isTestHarness,
            DefaultTheme = configuration.GetValue<string?>("UI:DefaultTheme") ?? ThemeColors.DefaultTheme,
            ShowRibbon = GetBoolean(configuration, "UI:ShowRibbon", !isTestHarness),
            ShowMenuBar = GetBoolean(configuration, "UI:ShowMenuBar", true),
            ShowStatusBar = GetBoolean(configuration, "UI:ShowStatusBar", true),
            DefaultFormSize = new Size(defaultWidth, defaultHeight),
            MinimumFormSize = new Size(minWidth, minHeight),
            AutoShowDashboard = GetBoolean(configuration, "UI:AutoShowDashboard", true),  // FIX: Match property default
            MinimalMode = GetBoolean(configuration, "UI:MinimalMode", false),  // FIX: Match property default (false = full UI)
            AutoShowPanels = GetBoolean(configuration, "UI:AutoShowPanels", true),  // FIX: Enable by default for better UX
            DefaultFiscalYear = GetInteger(configuration, "UI:DefaultFiscalYear", DateTime.UtcNow.Year),
            EnableDockingLockDuringLoad = GetBoolean(configuration, "UI:EnableDockingLockDuringLoad", true),
            ThemeApplyMaxDepth = GetInteger(configuration, "UI:ThemeApplyMaxDepth", 32),
            ThemeApplySkipControlTypes = configuration.GetSection("UI:ThemeApplySkipControlTypes").Get<string[]>() ?? Array.Empty<string>(),
            VerboseFirstChanceExceptions = GetBoolean(configuration, "Diagnostics:VerboseFirstChanceExceptions", false)
        };
    }

    /// <summary>
    /// Validates the configuration and returns a list of validation errors.
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (DefaultFormSize.Width < MinimumFormSize.Width || DefaultFormSize.Height < MinimumFormSize.Height)
        {
            errors.Add($"DefaultFormSize ({DefaultFormSize}) must be >= MinimumFormSize ({MinimumFormSize})");
        }

        if (MinimumFormSize.Width < 800 || MinimumFormSize.Height < 600)
        {
            errors.Add($"MinimumFormSize ({MinimumFormSize}) is too small (minimum 800x600)");
        }

        var currentYear = DateTime.UtcNow.Year;
        if (DefaultFiscalYear < 2000 || DefaultFiscalYear > currentYear + 10)
        {
            errors.Add($"DefaultFiscalYear ({DefaultFiscalYear}) is out of valid range (2000 to {currentYear + 10})");
        }

        if (string.IsNullOrWhiteSpace(DefaultTheme))
        {
            errors.Add("DefaultTheme cannot be empty");
        }

        if (ThemeApplyMaxDepth < 1)
        {
            errors.Add("ThemeApplyMaxDepth must be >= 1");
        }

        // Syncfusion WinForms supported themes for this workspace.
        var validThemes = new HashSet<string>(ThemeColors.GetSupportedThemes(), StringComparer.OrdinalIgnoreCase);
        if (!validThemes.Contains(DefaultTheme))
        {
            errors.Add($"DefaultTheme '{DefaultTheme}' is not a commonly supported theme. Examples: {string.Join(", ", validThemes)}");
        }

        return errors;
    }

    /// <summary>
    /// Gets a display-friendly string describing the UI architecture.
    /// </summary>
    public string GetArchitectureDescription()
    {
        return $"TestHarness={IsUiTestHarness}, Theme={DefaultTheme}, DockingLock={EnableDockingLockDuringLoad}";
    }
}
