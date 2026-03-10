using System;

namespace WileyWidget.WinForms.Configuration
{
    public static class StartupProfiles
    {
        public const string Balanced = "Balanced";
        public const string Diagnostic = "Diagnostic";
        public const string Production = "Production";
    }

    /// <summary>
    /// Strongly-typed representation of the 'Startup' section in appsettings.json.
    /// </summary>
    public class StartupOptions
    {
        /// <summary>
        /// Maximum time the orchestrator is allowed to block the UI thread before we log a warning or cancel.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 120;

        /// <summary>
        /// Startup operating profile. Balanced is default for normal developer runs,
        /// Diagnostic enables expensive diagnostics, Production keeps startup lean.
        /// </summary>
        public string Profile { get; set; } = StartupProfiles.Balanced;

        public bool EnableDiValidation { get; set; } = true;
        public bool EnableThemeValidation { get; set; } = true;
        public bool EnableLicenseValidation { get; set; } = true;
        public bool EnablePostShownServiceValidation { get; set; } = false;
        public bool EnablePostShownAsyncWarmup { get; set; } = false;
        public bool EnableStartupTimelineReport { get; set; } = false;
        public int PostShownValidationDelayMs { get; set; } = 750;

        public bool IsDiagnosticProfile =>
            string.Equals(Profile, StartupProfiles.Diagnostic, StringComparison.OrdinalIgnoreCase);

        public bool IsProductionProfile =>
            string.Equals(Profile, StartupProfiles.Production, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Per-phase budgets that help diagnostic logging understand how the timeout was allocated.
        /// </summary>
        public PhaseTimeoutsOptions PhaseTimeouts { get; set; } = new();

        /// <summary>
        /// Total milliseconds budgeted for the three tracked startup phases.
        /// </summary>
        public int TotalPhaseBudgetMs => PhaseTimeouts.TotalPhaseBudgetMs;
    }

    public class PhaseTimeoutsOptions
    {
        public int DockingInitMs { get; set; } = 15000;
        public int ViewModelInitMs { get; set; } = 10000;
        public int DataLoadMs { get; set; } = 8000;

        public int TotalPhaseBudgetMs => DockingInitMs + ViewModelInitMs + DataLoadMs;
    }
}
