using System;

namespace WileyWidget.WinForms.Configuration
{
    /// <summary>
    /// Strongly-typed representation of the 'Startup' section in appsettings.json.
    /// </summary>
    public class StartupOptions
    {
        /// <summary>
        /// Maximum time the orchestrator is allowed to block the UI thread before we log a warning or cancel.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 120;

        public bool EnableDiValidation { get; set; } = true;
        public bool EnableThemeValidation { get; set; } = true;
        public bool EnableLicenseValidation { get; set; } = true;

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
