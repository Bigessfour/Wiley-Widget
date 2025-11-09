// IDiagnosticsService.cs - Interface for application diagnostics service
//
// Extracted from App.xaml.cs as part of Phase 2: Architectural Refactoring (TODO 2.4)
// Date: November 9, 2025
//
// This service is responsible for collecting and displaying application diagnostics including:
// - Error and warning collection
// - System information gathering
// - Diagnostic report generation and display
// - Module status reporting

using System;

namespace WileyWidget.Services.Startup
{
    /// <summary>
    /// Service responsible for collecting and displaying application diagnostic information.
    /// Provides comprehensive error, warning, and system status reporting.
    /// </summary>
    public interface IDiagnosticsService
    {
        /// <summary>
        /// Collects and displays a comprehensive diagnostic report including errors, warnings,
        /// module status, and system information.
        /// Opens a dialog window with the diagnostic information.
        /// </summary>
        void RevealErrorsAndWarnings();

        /// <summary>
        /// Generates a diagnostic report as text without displaying it.
        /// Useful for logging or programmatic access.
        /// </summary>
        /// <returns>Formatted diagnostic report text</returns>
        string GenerateDiagnosticReport();

        /// <summary>
        /// Collects current module health status for diagnostic reporting.
        /// </summary>
        /// <returns>Formatted module status text</returns>
        string CollectModuleStatus();

        /// <summary>
        /// Collects system information for diagnostic reporting.
        /// Includes OS version, .NET version, memory, etc.
        /// </summary>
        /// <returns>Formatted system information text</returns>
        string CollectSystemInformation();
    }
}
