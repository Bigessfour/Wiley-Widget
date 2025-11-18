using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Background service that monitors Prism region health and reports issues proactively.
    /// This service helps prevent the DelayedRegionCreation KeyNotFoundException and other
    /// region-related timing issues by monitoring region state and behavior availability.
    /// </summary>
    public interface IRegionMonitoringService
    {
        /// <summary>
        /// Validates that all expected regions are available and properly configured.
        /// </summary>
        Task<RegionHealthReport> ValidateRegionHealthAsync();

        /// <summary>
        /// Checks if all required region behaviors are attached to regions.
        /// </summary>
        Task<BehaviorHealthReport> ValidateRegionBehaviorsAsync();

        /// <summary>
        /// Event fired when region health issues are detected.
        /// </summary>
        event Action<RegionHealthIssue>? RegionHealthIssueDetected;
    }

    public class RegionMonitoringService : BackgroundService, IRegionMonitoringService
    {
        private readonly IRegionManager _regionManager;
        private readonly ILogger<RegionMonitoringService> _logger;
        private readonly Timer? _healthCheckTimer;

        public event Action<RegionHealthIssue>? RegionHealthIssueDetected;

        // Expected regions from configuration (should match appsettings.json)
        private readonly HashSet<string> _expectedRegions = new()
        {
            "MainRegion", "SettingsRegion", "EnterpriseRegion", "BudgetRegion",
            "AccountsRegion", "CustomerRegion", "ReportsRegion", "AIRegion",
            "PanelRegion", "QuickBooksRegion", "LeftPanelRegion", "RightPanelRegion",
            "BottomPanelRegion"
        };

        // Expected behaviors that should be attached to regions
        private readonly HashSet<string> _expectedBehaviors = new()
        {
            "DelayedRegionCreation", "NavigationLogging", "AutoSave",
            "NavigationHistory", "AutoActivate"
        };

        public RegionMonitoringService(
            IRegionManager regionManager,
            ILogger<RegionMonitoringService> logger)
        {
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Set up periodic health checks every 30 seconds during startup phase
            _healthCheckTimer = new Timer(PeriodicHealthCheck, null,
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Region monitoring service started");

            try
            {
                // Initial health check after a brief delay for initialization
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await PerformHealthChecksAsync();

                        // Wait 2 minutes between comprehensive checks
                        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break; // Normal cancellation
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during region health monitoring");
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Backoff on error
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Region monitoring service cancelled");
            }
            finally
            {
                _logger.LogInformation("Region monitoring service stopped");
            }
        }

        private async void PeriodicHealthCheck(object? state)
        {
            try
            {
                await PerformHealthChecksAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Periodic health check failed");
            }
        }

        private async Task PerformHealthChecksAsync()
        {
            var regionHealth = await ValidateRegionHealthAsync();
            var behaviorHealth = await ValidateRegionBehaviorsAsync();

            // Report critical issues
            foreach (var issue in regionHealth.Issues.Where(i => i.Severity == RegionIssueSeverity.Critical))
            {
                RegionHealthIssueDetected?.Invoke(issue);
                _logger.LogError("CRITICAL region issue: {Issue}", issue.Description);
            }

            foreach (var issue in behaviorHealth.Issues.Where(i => i.Severity == RegionIssueSeverity.Critical))
            {
                RegionHealthIssueDetected?.Invoke(issue);
                _logger.LogError("CRITICAL behavior issue: {Issue}", issue.Description);
            }

            // Log summary
            if (regionHealth.Issues.Count > 0 || behaviorHealth.Issues.Count > 0)
            {
                _logger.LogWarning("Region health summary: {RegionIssues} region issues, {BehaviorIssues} behavior issues",
                    regionHealth.Issues.Count, behaviorHealth.Issues.Count);
            }
            else
            {
                _logger.LogDebug("Region health check passed - all regions and behaviors healthy");
            }
        }

        public async Task<RegionHealthReport> ValidateRegionHealthAsync()
        {
            return await Task.Run(() =>
            {
                var report = new RegionHealthReport();

                try
                {
                    var availableRegions = _regionManager.Regions.Select(r => r.Name).ToHashSet();

                    // Check for missing expected regions
                    var missingRegions = _expectedRegions.Except(availableRegions).ToList();
                    foreach (var missing in missingRegions)
                    {
                        report.Issues.Add(new RegionHealthIssue
                        {
                            Type = RegionIssueType.MissingRegion,
                            Severity = RegionIssueSeverity.Warning,
                            RegionName = missing,
                            Description = $"Expected region '{missing}' is not available"
                        });
                    }

                    // Check for empty regions that should have content
                    foreach (var region in _regionManager.Regions)
                    {
                        if (region.Views.Count() == 0 && region.ActiveViews.Count() == 0 &&
                            _expectedRegions.Contains(region.Name))
                        {
                            report.Issues.Add(new RegionHealthIssue
                            {
                                Type = RegionIssueType.EmptyRegion,
                                Severity = RegionIssueSeverity.Information,
                                RegionName = region.Name,
                                Description = $"Region '{region.Name}' has no views or active views"
                            });
                        }
                    }                    report.TotalExpectedRegions = _expectedRegions.Count;
                    report.TotalAvailableRegions = availableRegions.Count;
                    report.HealthyRegions = availableRegions.Intersect(_expectedRegions).Count();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validating region health");
                    report.Issues.Add(new RegionHealthIssue
                    {
                        Type = RegionIssueType.ValidationError,
                        Severity = RegionIssueSeverity.Critical,
                        Description = $"Region validation failed: {ex.Message}"
                    });
                }

                return report;
            });
        }

        public async Task<BehaviorHealthReport> ValidateRegionBehaviorsAsync()
        {
            return await Task.Run(() =>
            {
                var report = new BehaviorHealthReport();

                try
                {
                    foreach (var region in _regionManager.Regions)
                    {
                        // Check each expected behavior individually since IRegionBehaviorCollection
                        // doesn't expose Keys directly in Prism 9
                        var missingBehaviors = new List<string>();

                        foreach (var expectedBehavior in _expectedBehaviors)
                        {
                            if (!region.Behaviors.ContainsKey(expectedBehavior))
                            {
                                missingBehaviors.Add(expectedBehavior);
                            }
                        }

                        foreach (var missing in missingBehaviors)
                        {
                            var severity = missing == "DelayedRegionCreation"
                                ? RegionIssueSeverity.Critical  // This is the one causing KeyNotFoundException
                                : RegionIssueSeverity.Warning;

                            report.Issues.Add(new RegionHealthIssue
                            {
                                Type = RegionIssueType.MissingBehavior,
                                Severity = severity,
                                RegionName = region.Name,
                                BehaviorKey = missing,
                                Description = $"Region '{region.Name}' is missing expected behavior '{missing}'"
                            });
                        }

                        report.RegionsChecked++;
                        if (missingBehaviors.Count == 0)
                        {
                            report.HealthyRegions++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validating region behaviors");
                    report.Issues.Add(new RegionHealthIssue
                    {
                        Type = RegionIssueType.ValidationError,
                        Severity = RegionIssueSeverity.Critical,
                        Description = $"Behavior validation failed: {ex.Message}"
                    });
                }

                return report;
            });
        }

        public override void Dispose()
        {
            _healthCheckTimer?.Dispose();
            base.Dispose();
        }
    }

    // Supporting types for the monitoring service
    public class RegionHealthReport
    {
        public List<RegionHealthIssue> Issues { get; set; } = new();
        public int TotalExpectedRegions { get; set; }
        public int TotalAvailableRegions { get; set; }
        public int HealthyRegions { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class BehaviorHealthReport
    {
        public List<RegionHealthIssue> Issues { get; set; } = new();
        public int RegionsChecked { get; set; }
        public int HealthyRegions { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class RegionHealthIssue
    {
        public RegionIssueType Type { get; set; }
        public RegionIssueSeverity Severity { get; set; }
        public string RegionName { get; set; } = string.Empty;
        public string? BehaviorKey { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }

    public enum RegionIssueType
    {
        MissingRegion,
        EmptyRegion,
        MissingBehavior,
        InvalidAdapter,
        NavigationFailure,
        ValidationError
    }

    public enum RegionIssueSeverity
    {
        Information,
        Warning,
        Critical
    }
}
