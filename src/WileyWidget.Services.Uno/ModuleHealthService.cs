using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using Prism.Modularity;
using Prism.Events;
using Serilog;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for monitoring and tracking the health status of Prism modules.
    /// Provides diagnostics and status reporting for module initialization.
    /// </summary>
    public interface IModuleHealthService
    {
        void RegisterModule(string moduleName);
        void MarkModuleInitialized(string moduleName, bool success, string? errorMessage = null);
        ModuleHealthStatus GetModuleStatus(string moduleName);
        IEnumerable<ModuleHealthInfo> GetAllModuleStatuses();
        bool AreAllModulesHealthy();
        void LogHealthReport();

        // Events for real-time monitoring
        event Action<ModuleHealthInfo>? ModuleHealthChanged;
        event Action<string>? ModuleFailed;
        event Action<string>? ModuleRecovered;
    }

    /// <summary>
    /// Represents the health status of a module
    /// </summary>
    public class ModuleHealthInfo
    {
        public string ModuleName { get; set; } = string.Empty;
        public ModuleHealthStatus Status { get; set; }
        public DateTime RegistrationTime { get; set; }
        public DateTime? InitializationTime { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan? InitializationDuration => InitializationTime.HasValue ?
            InitializationTime.Value - RegistrationTime : null;
    }

    public enum ModuleHealthStatus
    {
        Registered,
        Initializing,
        Healthy,
        Failed,
        NotFound
    }

    /// <summary>
    /// Implementation of module health monitoring service
    /// </summary>
    public class ModuleHealthService : IModuleHealthService
    {
        private readonly Dictionary<string, ModuleHealthInfo> _moduleHealth = new();
        private readonly ILogger<ModuleHealthService> _logger;

        // Event implementations
        public event Action<ModuleHealthInfo>? ModuleHealthChanged;
        public event Action<string>? ModuleFailed;
        public event Action<string>? ModuleRecovered;

        public ModuleHealthService(ILogger<ModuleHealthService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Log.Information("ModuleHealthService initialized for tracking module health status");
        }

        public void RegisterModule(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                _logger.LogWarning("Attempted to register module with null or empty name");
                return;
            }

            var healthInfo = new ModuleHealthInfo
            {
                ModuleName = moduleName,
                Status = ModuleHealthStatus.Registered,
                RegistrationTime = DateTime.UtcNow
            };

            _moduleHealth[moduleName] = healthInfo;
            _logger.LogInformation("Registered module '{ModuleName}' for health monitoring", moduleName);
        }

        public void MarkModuleInitialized(string moduleName, bool success, string? errorMessage = null)
        {
            if (!_moduleHealth.TryGetValue(moduleName, out var healthInfo))
            {
                _logger.LogWarning("Attempted to mark unknown module '{ModuleName}' as initialized", moduleName);
                return;
            }

            var previousStatus = healthInfo.Status;
            healthInfo.Status = success ? ModuleHealthStatus.Healthy : ModuleHealthStatus.Failed;
            healthInfo.InitializationTime = DateTime.UtcNow;
            healthInfo.ErrorMessage = errorMessage;

            // Raise events for health status changes
            ModuleHealthChanged?.Invoke(healthInfo);

            if (success)
            {
                _logger.LogInformation("Module '{ModuleName}' initialized successfully in {Duration}",
                    moduleName, healthInfo.InitializationDuration);

                // Check if this is a recovery from failure
                if (previousStatus == ModuleHealthStatus.Failed)
                {
                    ModuleRecovered?.Invoke(moduleName);
                    _logger.LogInformation("Module '{ModuleName}' has recovered from previous failure", moduleName);
                }
            }
            else
            {
                _logger.LogError("Module '{ModuleName}' failed to initialize: {ErrorMessage}",
                    moduleName, errorMessage);

                // Raise failure event
                ModuleFailed?.Invoke(moduleName);
            }
        }

        public ModuleHealthStatus GetModuleStatus(string moduleName)
        {
            return _moduleHealth.TryGetValue(moduleName, out var healthInfo)
                ? healthInfo.Status
                : ModuleHealthStatus.NotFound;
        }

        public IEnumerable<ModuleHealthInfo> GetAllModuleStatuses()
        {
            return _moduleHealth.Values.OrderBy(m => m.RegistrationTime);
        }

        public bool AreAllModulesHealthy()
        {
            return _moduleHealth.Values.All(m => m.Status == ModuleHealthStatus.Healthy);
        }

        public void LogHealthReport()
        {
            var totalModules = _moduleHealth.Count;
            var healthyModules = _moduleHealth.Values.Count(m => m.Status == ModuleHealthStatus.Healthy);
            var failedModules = _moduleHealth.Values.Count(m => m.Status == ModuleHealthStatus.Failed);

            _logger.LogInformation("=== Module Health Report ===");
            _logger.LogInformation("Total Modules: {Total}", totalModules);
            _logger.LogInformation("Healthy: {Healthy}", healthyModules);
            _logger.LogInformation("Failed: {Failed}", failedModules);
            _logger.LogInformation("Overall Status: {Status}",
                AreAllModulesHealthy() ? "ALL HEALTHY" : "ISSUES DETECTED");

            foreach (var healthInfo in GetAllModuleStatuses())
            {
                var status = healthInfo.Status.ToString().ToUpper(System.Globalization.CultureInfo.InvariantCulture);
                var duration = healthInfo.InitializationDuration?.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A";

                if (healthInfo.Status == ModuleHealthStatus.Failed)
                {
                    _logger.LogError("  {Module}: {Status} ({Duration}ms) - {Error}",
                        healthInfo.ModuleName, status, duration, healthInfo.ErrorMessage);
                }
                else
                {
                    _logger.LogInformation("  {Module}: {Status} ({Duration}ms)",
                        healthInfo.ModuleName, status, duration);
                }
            }
            _logger.LogInformation("=== End Module Health Report ===");
        }
    }
}
