// DiagnosticsService.cs - Implementation of application diagnostics service
//
// Extracted from App.xaml.cs as part of Phase 2: Architectural Refactoring (TODO 2.4)
// Date: November 9, 2025
//
// This service collects and displays comprehensive application diagnostics including:
// - Error and warning collection
// - System information (OS, .NET runtime, memory)
// - Module health status
// - Diagnostic report generation and UI display

using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Serilog;
using WileyWidget.Services;

namespace WileyWidget.Services.Startup
{
    /// <summary>
    /// Service responsible for collecting and displaying comprehensive application diagnostics.
    /// Provides error/warning reporting, system information, and module health status.
    /// </summary>
    public class DiagnosticsService : IDiagnosticsService
    {
        private readonly ILogger<DiagnosticsService> _logger;
        private readonly IModuleHealthService? _moduleHealthService;
        private readonly IStartupEnvironmentValidator _environmentValidator;

        public DiagnosticsService(
            ILogger<DiagnosticsService> logger,
            IStartupEnvironmentValidator environmentValidator,
            IModuleHealthService? moduleHealthService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _environmentValidator = environmentValidator ?? throw new ArgumentNullException(nameof(environmentValidator));
            _moduleHealthService = moduleHealthService; // Optional - may not be available early in startup
        }

        /// <summary>
        /// Collects and displays a comprehensive diagnostic report in a dialog window.
        /// </summary>
        public void RevealErrorsAndWarnings()
        {
            try
            {
                _logger.LogInformation("Generating diagnostic report...");

                var diagnosticReport = GenerateDiagnosticReport();

                // Display on UI thread with BeginInvoke to avoid deadlocks
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // Create scrollable text display
                        var textBox = new TextBox
                        {
                            Text = diagnosticReport,
                            IsReadOnly = true,
                            TextWrapping = TextWrapping.Wrap,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                            FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New"),
                            FontSize = 12,
                            Padding = new Thickness(10)
                        };

                        var scrollViewer = new ScrollViewer
                        {
                            Content = textBox,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                        };

                        // Create diagnostic window
                        var window = new Window
                        {
                            Title = "Application Diagnostics Report",
                            Content = scrollViewer,
                            Width = 800,
                            Height = 600,
                            WindowStartupLocation = WindowStartupLocation.CenterScreen,
                            ShowInTaskbar = false,
                            Icon = Application.Current?.MainWindow?.Icon
                        };

                        window.ShowDialog();

                        Log.Information("Diagnostic report displayed to user");
                        _logger.LogInformation("Diagnostic report displayed successfully");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to display diagnostic window");
                        _logger.LogError(ex, "Failed to display diagnostic window");

                        // Fallback to MessageBox
                        MessageBox.Show(
                            $"Failed to display diagnostic window: {ex.Message}",
                            "Diagnostic Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to generate or display diagnostic report");
                _logger.LogError(ex, "Failed to generate or display diagnostic report");

                // Fallback MessageBox on UI thread
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Failed to generate diagnostic report: {ex.Message}",
                        "Diagnostic Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
            }
        }

        /// <summary>
        /// Generates a comprehensive diagnostic report as text.
        /// </summary>
        public string GenerateDiagnosticReport()
        {
            try
            {
                var report = new StringBuilder();

                // Header
                report.AppendLine("╔═══════════════════════════════════════════════════════════════════╗");
                report.AppendLine("║         WileyWidget Application Diagnostic Report                ║");
                report.AppendLine("╚═══════════════════════════════════════════════════════════════════╝");
                report.AppendLine();
                report.AppendLine($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
                report.AppendLine();

                // System Information
                report.AppendLine("═══════════════════════════════════════════════════════════════════");
                report.AppendLine("SYSTEM INFORMATION");
                report.AppendLine("═══════════════════════════════════════════════════════════════════");
                report.AppendLine(CollectSystemInformation());
                report.AppendLine();

                // Module Status
                report.AppendLine("═══════════════════════════════════════════════════════════════════");
                report.AppendLine("MODULE STATUS");
                report.AppendLine("═══════════════════════════════════════════════════════════════════");
                report.AppendLine(CollectModuleStatus());
                report.AppendLine();

                // Recent Errors and Warnings
                report.AppendLine("═══════════════════════════════════════════════════════════════════");
                report.AppendLine("RECENT ERRORS AND WARNINGS");
                report.AppendLine("═══════════════════════════════════════════════════════════════════");
                report.AppendLine("Recent error logs can be found in: logs/wiley-widget-*.log");
                report.AppendLine("Check Serilog output for detailed error information.");
                report.AppendLine();

                // Footer
                report.AppendLine("═══════════════════════════════════════════════════════════════════");
                report.AppendLine("End of Diagnostic Report");
                report.AppendLine("═══════════════════════════════════════════════════════════════════");

                return report.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate diagnostic report");
                return $"ERROR: Failed to generate diagnostic report: {ex.Message}";
            }
        }

        /// <summary>
        /// Collects current module health status for diagnostic reporting.
        /// </summary>
        public string CollectModuleStatus()
        {
            try
            {
                var status = new StringBuilder();

                if (_moduleHealthService == null)
                {
                    status.AppendLine("⚠ Module health service not available");
                    return status.ToString();
                }

                var moduleStatuses = _moduleHealthService.GetAllModuleStatuses();

                if (!moduleStatuses.Any())
                {
                    status.AppendLine("No modules registered");
                    return status.ToString();
                }

                status.AppendLine($"Total Modules: {moduleStatuses.Count}");
                status.AppendLine();

                foreach (var module in moduleStatuses.OrderBy(m => m.ModuleName))
                {
                    var statusIcon = module.Status switch
                    {
                        Models.ModuleHealthStatus.Healthy => "✓",
                        Models.ModuleHealthStatus.Degraded => "⚠",
                        Models.ModuleHealthStatus.Unhealthy => "✗",
                        Models.ModuleHealthStatus.Initializing => "◐",
                        _ => "?"
                    };

                    status.AppendLine($"{statusIcon} {module.ModuleName,-30} Status: {module.Status}");

                    if (!string.IsNullOrWhiteSpace(module.ErrorMessage))
                    {
                        status.AppendLine($"    Error: {module.ErrorMessage}");
                    }

                    if (module.LastHealthCheck != default)
                    {
                        status.AppendLine($"    Last Check: {module.LastHealthCheck:yyyy-MM-dd HH:mm:ss UTC}");
                    }
                }

                return status.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect module status");
                return $"ERROR: Failed to collect module status: {ex.Message}";
            }
        }

        /// <summary>
        /// Collects system information for diagnostic reporting.
        /// </summary>
        public string CollectSystemInformation()
        {
            try
            {
                var info = new StringBuilder();

                // Operating System
                info.AppendLine($"OS: {Environment.OSVersion}");
                info.AppendLine($"OS Version: {Environment.OSVersion.VersionString}");
                info.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
                info.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
                info.AppendLine();

                // .NET Runtime
                info.AppendLine($".NET Runtime: {Environment.Version}");
                info.AppendLine($"Runtime Directory: {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}");
                info.AppendLine();

                // Memory
                var availableMemoryMB = _environmentValidator.GetAvailableMemoryMB();
                info.AppendLine($"Available Memory: {availableMemoryMB} MB");

                var gcInfo = GC.GetGCMemoryInfo();
                info.AppendLine($"Total Memory: {gcInfo.TotalAvailableMemoryBytes / (1024 * 1024)} MB");
                info.AppendLine($"Heap Size: {gcInfo.HeapSizeBytes / (1024 * 1024)} MB");
                info.AppendLine($"GC Mode: {(GCSettings.IsServerGC ? "Server" : "Workstation")}");
                info.AppendLine($"GC Latency Mode: {GCSettings.LatencyMode}");
                info.AppendLine();

                // Process Information
                info.AppendLine($"Process ID: {Environment.ProcessId}");
                info.AppendLine($"Machine Name: {Environment.MachineName}");
                info.AppendLine($"User: {Environment.UserDomainName}\\{Environment.UserName}");
                info.AppendLine($"Processor Count: {Environment.ProcessorCount}");
                info.AppendLine();

                // Application Information
                info.AppendLine($"Application Domain: {AppDomain.CurrentDomain.FriendlyName}");
                info.AppendLine($"Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
                info.AppendLine($"Loaded Assemblies: {AppDomain.CurrentDomain.GetAssemblies().Length}");

                return info.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect system information");
                return $"ERROR: Failed to collect system information: {ex.Message}";
            }
        }
    }
}
