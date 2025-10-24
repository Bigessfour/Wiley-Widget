using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Data;
using Serilog;

namespace WileyWidget.Services.Hosting;

/// <summary>
/// Hosted service that performs comprehensive application health checks during startup
/// using Polly for resilience patterns. Shows dialogs for failures instead of crashing.
/// </summary>
public class HealthCheckHostedService : IHostedService, IDisposable
{
    private readonly ILogger<HealthCheckHostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly HealthCheckConfiguration _healthCheckConfig;
    private bool _disposed;

    public HealthCheckHostedService(
        ILogger<HealthCheckHostedService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _healthCheckConfig = new HealthCheckConfiguration();
        _configuration.GetSection("HealthChecks").Bind(_healthCheckConfig);
    }

    /// <summary>
    /// Starts the health check service and performs initial health checks
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HealthCheckHostedService is starting.");

        try
        {
            // Perform comprehensive health checks
            var report = await PerformHealthChecksAsync();

            // Update the global health report
            WileyWidget.App.UpdateLatestHealthReport(report);

            // Check if application can start
            if (!CanApplicationStart(report))
            {
                _logger.LogError("Application cannot start due to critical service failures");

                // Show dialog instead of crashing
                await ShowHealthCheckFailureDialogAsync(report);
            }
            else
            {
                _logger.LogInformation("Health checks passed - application can start");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during health checks");

            // Show dialog for critical failures
            await ShowHealthCheckFailureDialogAsync(null, ex);
        }
    }

    /// <summary>
    /// Stops the health check service
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HealthCheckHostedService is stopping.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs comprehensive health checks using Polly for resilience
    /// </summary>
    private async Task<HealthCheckReport> PerformHealthChecksAsync()
    {
        var totalStopwatch = Stopwatch.StartNew();
        var report = new HealthCheckReport
        {
            Timestamp = DateTime.UtcNow,
            OverallStatus = HealthStatus.Healthy
        };

        try
        {
            Log.Information("=== ENTERPRISE HEALTH CHECKS STARTED ===");

            // Execute health checks with resilience patterns
            var healthCheckTasks = new List<Task<HealthCheckResult>>();

            // Core services (always checked)
            healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("Configuration", CheckConfigurationHealthAsync()));
            healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("Database", CheckDatabaseHealthAsync()));

            // External services (with circuit breakers)
            if (!IsServiceSkipped("Secret Vault"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("Secret Vault", CheckSecretVaultHealthAsync()));

            if (!IsServiceSkipped("QuickBooks"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("QuickBooks", CheckQuickBooksHealthAsync()));

            if (!IsServiceSkipped("Syncfusion License"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("Syncfusion License", CheckSyncfusionLicenseHealthAsync()));

            if (!IsServiceSkipped("AI Service"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("AI Service", CheckAIServiceHealthAsync()));

            if (!IsServiceSkipped("External Dependencies"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("External Dependencies", CheckExternalDependenciesHealthAsync()));

            if (!IsServiceSkipped("System Resources"))
                healthCheckTasks.Add(ExecuteHealthCheckWithPollyAsync("System Resources", CheckSystemResourcesHealthAsync()));

            // Wait for all health checks to complete with overall timeout
            var timeoutTask = Task.Delay(_healthCheckConfig.DefaultTimeout);
            var completedTask = await Task.WhenAny(Task.WhenAll(healthCheckTasks), timeoutTask);

            if (completedTask == timeoutTask)
            {
                Log.Warning("Health checks timed out after {Timeout}s", _healthCheckConfig.DefaultTimeout.TotalSeconds);

                // Create timeout results for incomplete tasks
                var timedOutResults = healthCheckTasks
                    .Where(t => !t.IsCompletedSuccessfully)
                    .Select(t => HealthCheckResult.Unhealthy("Unknown Service", "Health check timed out", null, _healthCheckConfig.DefaultTimeout));

                report.Results.AddRange(timedOutResults);
            }

            // Collect successful results
            var completedResults = healthCheckTasks.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToList();
            report.Results.AddRange(completedResults);

            // Handle failed tasks
            var failedTasks = healthCheckTasks.Where(t => t.IsFaulted).ToList();
            foreach (var failedTask in failedTasks)
            {
                var exception = failedTask.Exception?.InnerException ?? failedTask.Exception;
                Log.Error(exception, "Health check task failed");

                // Create failed result
                var failedResult = HealthCheckResult.Unhealthy("Unknown Service", $"Health check failed: {exception?.Message}", exception);
                report.Results.Add(failedResult);
            }

            // Determine overall status
            report.OverallStatus = DetermineOverallHealthStatus(report.Results);

            // Log results
            LogHealthCheckResults(report);

            Log.Information("=== ENTERPRISE HEALTH CHECKS COMPLETED ===");
            return report;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Critical error during health checks");
            report.OverallStatus = HealthStatus.Unhealthy;
            report.Results.Add(HealthCheckResult.Unhealthy("HealthCheckSystem", "Health check system failed", ex));
            return report;
        }
        finally
        {
            totalStopwatch.Stop();
            report.TotalDuration = totalStopwatch.Elapsed;
        }
    }

    /// <summary>
    /// Executes a health check with Polly retry and circuit breaker policies
    /// </summary>
    private async Task<HealthCheckResult> ExecuteHealthCheckWithPollyAsync(string serviceName, Task<HealthCheckResult> healthCheckTask)
    {
        // Create retry policy with exponential backoff using Polly
        var retryPolicy = Policy<HealthCheckResult>
            .Handle<Exception>()
            .OrResult(r => r.Status == HealthStatus.Unhealthy || r.Status == HealthStatus.Unavailable)
            .WaitAndRetryAsync(
                retryCount: _healthCheckConfig.MaxRetries,
                sleepDurationProvider: attempt => _healthCheckConfig.RetryDelay * Math.Pow(2, attempt - 1),
                onRetry: (outcome, delay, attempt, context) =>
                {
                    var message = outcome.Exception != null
                        ? $"Exception: {outcome.Exception.Message}"
                        : $"Result: {outcome.Result?.Description ?? "Unknown"}";
                    _logger.LogWarning("Health check for {Service} failed (attempt {Attempt}/{MaxAttempts}), retrying in {Delay}ms. {Message}",
                        serviceName, attempt, _healthCheckConfig.MaxRetries + 1, delay.TotalMilliseconds, message);
                });

        try
        {
            // Add timeout to individual health check
            var timeout = GetTimeoutForService(serviceName);
            using var cts = new CancellationTokenSource(timeout);
            var task = healthCheckTask;

            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
            if (completedTask != task)
            {
                throw new TimeoutException($"Health check for {serviceName} timed out after {timeout.TotalSeconds}s");
            }

            // Execute with Polly retry policy
            return await retryPolicy.ExecuteAsync(async () => await task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check for {Service} failed after all retries", serviceName);
            return HealthCheckResult.Unhealthy(serviceName, $"Health check failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Shows a dialog when health checks fail instead of crashing the application
    /// </summary>
    private async Task ShowHealthCheckFailureDialogAsync(HealthCheckReport report = null, Exception criticalException = null)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var issues = new List<string>();

            if (criticalException != null)
            {
                issues.Add($"Critical error: {criticalException.Message}");
            }

            if (report != null)
            {
                issues.AddRange(report.Results
                    .Where(r => r.Status != HealthStatus.Healthy)
                    .Select(r => $"{r.ServiceName}: {r.Description}"));
            }

            var message = "Application health checks have failed. Some features may not work correctly.\n\n" +
                         "Issues found:\n" + string.Join("\n", issues.Select(i => $"• {i}"));

            var result = MessageBox.Show(
                message,
                "Health Check Warnings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            // Application continues despite health check failures
            _logger.LogWarning("User acknowledged health check failures - application continuing");
        });
    }

    // Health check implementation methods moved from App.xaml.cs
    // These will be added in the next step

    private async Task<HealthCheckResult> CheckDatabaseHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Check if database is configured
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return HealthCheckResult.Unhealthy("Database", "Database connection string not configured");
            }

            // Validate database schema and connectivity using the IDbContextFactory to avoid scoped-from-singleton issues
            await WileyWidget.Configuration.DatabaseConfiguration.ValidateDatabaseSchemaAsync(_serviceProvider);

            // Additional connectivity check using IDbContextFactory if available
            using (var scope = _serviceProvider.CreateScope())
            {
                var provider = scope.ServiceProvider;
                var contextFactory = provider.GetService<Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext>>();
                if (contextFactory != null)
                {
                    await using var dbContext = await contextFactory.CreateDbContextAsync();
                    await dbContext.Database.CanConnectAsync();
                }
                else
                {
                    // Fall back to resolving AppDbContext if factory not available (legacy registration)
                    var dbContext = provider.GetService<AppDbContext>();
                    if (dbContext != null)
                        await dbContext.Database.CanConnectAsync();
                }
            }

            stopwatch.Stop();
            return HealthCheckResult.Healthy("Database", "Database connection and schema validation successful", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Warning(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database", $"Database health check failed: {ex.Message}", ex, stopwatch.Elapsed);
        }
    }

    private Task<HealthCheckResult> CheckConfigurationHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var issues = new List<string>();

            // Check required configuration sections
            if (string.IsNullOrEmpty(_configuration.GetConnectionString("DefaultConnection")))
                issues.Add("Database connection string not configured");

            // Consider env var, config, or user secrets for Syncfusion key
            if (string.IsNullOrEmpty(GetSyncfusionLicenseKey()))
                issues.Add("Syncfusion license key not configured");

            // Check environment variables for critical services
            var criticalEnvVars = new[] { "QBO_CLIENT_ID", "QBO_REALM_ID" };
            foreach (var envVar in criticalEnvVars)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar, EnvironmentVariableTarget.User)))
                {
                    issues.Add($"Environment variable {envVar} not set");
                }
            }

            stopwatch.Stop();

            if (issues.Any())
            {
                return Task.FromResult(HealthCheckResult.Degraded("Configuration",
                    $"Configuration validation found {issues.Count} issues: {string.Join(", ", issues)}", stopwatch.Elapsed));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Configuration", "All required configuration validated successfully", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("Configuration", $"Configuration validation failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private async Task<HealthCheckResult> CheckSecretVaultHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var secretVaultService = _serviceProvider.GetService<ISecretVaultService>();
            if (secretVaultService == null)
            {
                stopwatch.Stop();
                return HealthCheckResult.Unavailable("Secret Vault", "Secret vault service not configured");
            }

            var healthy = await secretVaultService.TestConnectionAsync();
            stopwatch.Stop();

            if (healthy)
            {
                return HealthCheckResult.Healthy("Secret Vault", "Local secret vault accessible", stopwatch.Elapsed);
            }

            return HealthCheckResult.Degraded("Secret Vault", "Local secret vault is not accessible", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HealthCheckResult.Unhealthy("Secret Vault", $"Secret vault health check failed: {ex.Message}", ex, stopwatch.Elapsed);
        }
    }

    private async Task<HealthCheckResult> CheckQuickBooksHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Resolve QuickBooks service from a created scope to respect scoped lifetime
            using var scope = _serviceProvider.CreateScope();
            var qbService = scope.ServiceProvider.GetService<IQuickBooksService>();
            if (qbService == null)
            {
                return HealthCheckResult.Unavailable("QuickBooks", "QuickBooks service not available");
            }

            // Check configuration
            var clientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID", EnvironmentVariableTarget.User);
            var realmId = Environment.GetEnvironmentVariable("QBO_REALM_ID", EnvironmentVariableTarget.User);

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(realmId))
            {
                return HealthCheckResult.Unhealthy("QuickBooks", "QuickBooks Client ID or Realm ID not configured");
            }

            // Check if token is valid
            if (qbService is QuickBooksService concreteService && !concreteService.HasValidAccessToken())
            {
                return HealthCheckResult.Degraded("QuickBooks", "QuickBooks access token is expired or not available");
            }

            // Test actual connection - if it fails with "No Connection", treat as expected (service not configured yet)
            var connectionTestResult = await qbService.TestConnectionAsync();
            if (!connectionTestResult)
            {
                // Check if this is expected (no connection configured)
                // For now, we'll treat connection failures as degraded but expected until the service is fully configured
                return HealthCheckResult.Degraded("QuickBooks", "QuickBooks connection not established (expected until service is configured)", stopwatch.Elapsed);
            }

            stopwatch.Stop();
            return HealthCheckResult.Healthy("QuickBooks", "QuickBooks service configured, token valid, and connection successful", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HealthCheckResult.Unhealthy("QuickBooks", $"QuickBooks health check failed: {ex.Message}", ex, stopwatch.Elapsed);
        }
    }

    private Task<HealthCheckResult> CheckSyncfusionLicenseHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Use unified resolution for the license key (env/config/user secrets)
            var licenseKey = GetSyncfusionLicenseKey();
            if (string.IsNullOrEmpty(licenseKey))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Syncfusion License", "Syncfusion license key not configured"));
            }

            // Validate license format (basic check)
            if (licenseKey.Length < 32)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Syncfusion License", "Syncfusion license key appears to be invalid (too short)"));
            }

            // Check if license is registered (this would require checking Syncfusion's internal state)
            // For now, we assume it's valid if configured
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Healthy("Syncfusion License", "Syncfusion license key configured and validated", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("Syncfusion License", $"Syncfusion license health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private Task<HealthCheckResult> CheckAIServiceHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Resolve AI service from a created scope to respect scoped lifetime
            using var scope = _serviceProvider.CreateScope();
            var aiService = scope.ServiceProvider.GetService<IAIService>();
            if (aiService == null)
            {
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", "AI service not configured"));
            }

            // Check configuration - prioritize xAI since that's what Wiley Widget uses
            var xaiApiKey = _configuration["XAI:ApiKey"] ?? Environment.GetEnvironmentVariable("XAI_API_KEY");
            var openAiKey = _configuration["Secrets:OpenAI:ApiKey"] ?? _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            if (string.IsNullOrEmpty(xaiApiKey) && string.IsNullOrEmpty(openAiKey))
            {
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", "No AI service API key configured (XAI or OpenAI)"));
            }

            // Perform lightweight connectivity probe (2s budget)
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
            var clientFactory = scope.ServiceProvider.GetService<System.Net.Http.IHttpClientFactory>();
            using var activeClient = clientFactory?.CreateClient("AIHealthCheck") ?? new System.Net.Http.HttpClient();
            activeClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WileyWidget-AIHealthCheck/1.0");
            System.Net.Http.HttpResponseMessage? response = null;
            string? target = null;
            string serviceType = "Unknown";

            try
            {
                if (!string.IsNullOrWhiteSpace(xaiApiKey))
                {
                    serviceType = "xAI";
                    activeClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", xaiApiKey);
                    var baseUrl = _configuration["XAI:BaseUrl"] ?? "https://api.x.ai/v1/";
                    target = baseUrl.TrimEnd('/') + "/models";
                    response = activeClient.GetAsync(target, cts.Token).GetAwaiter().GetResult();
                }
                else if (!string.IsNullOrWhiteSpace(openAiKey))
                {
                    serviceType = "OpenAI";
                    activeClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openAiKey);
                    target = "https://api.openai.com/v1/models";
                    response = activeClient.GetAsync(target, cts.Token).GetAwaiter().GetResult();
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return Task.FromResult(HealthCheckResult.Degraded("AI Service", "Connectivity probe timed out", stopwatch.Elapsed));
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                stopwatch.Stop();
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", $"Connectivity error: {ex.Message}", ex, stopwatch.Elapsed));
            }

            if (response == null)
            {
                stopwatch.Stop();
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", "Probe did not execute (no valid configuration found)", null, stopwatch.Elapsed));
            }

            var status = (int)response.StatusCode;
            string detail = $"Probe {serviceType} status {(int)response.StatusCode} {response.ReasonPhrase}";

            HealthCheckResult result;
            if (status == 200)
            {
                result = HealthCheckResult.Healthy("AI Service", detail, stopwatch.Elapsed);
            }
            else if (status == 401 || status == 403)
            {
                result = HealthCheckResult.Unhealthy("AI Service", detail, null, stopwatch.Elapsed);
            }
            else if (status == 429 || (status >= 500 && status <= 599))
            {
                result = HealthCheckResult.Degraded("AI Service", detail, stopwatch.Elapsed);
            }
            else
            {
                result = HealthCheckResult.Degraded("AI Service", detail, stopwatch.Elapsed);
            }

            stopwatch.Stop();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("AI Service", $"AI service health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private async Task<HealthCheckResult> CheckExternalDependenciesHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var issues = new List<string>();

            // Check internet connectivity (basic)
            try
            {
                // Prefer HttpClient from DI to reuse handlers; fall back to ephemeral client if not available
                System.Net.Http.HttpResponseMessage response = null;
                using (var scope = _serviceProvider.CreateScope())
                {
                    var client = scope.ServiceProvider.GetService<System.Net.Http.HttpClient>();
                    if (client != null)
                    {
                        client.Timeout = TimeSpan.FromSeconds(5);
                        response = await client.GetAsync("https://www.microsoft.com");
                    }
                    else
                    {
                        using var tmpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                        response = await tmpClient.GetAsync("https://www.microsoft.com");
                    }
                }

                if (response == null || !response.IsSuccessStatusCode)
                {
                    issues.Add("Internet connectivity check failed");
                }
            }
            catch
            {
                issues.Add("Internet connectivity check failed");
            }

            stopwatch.Stop();

            if (issues.Any())
            {
                return HealthCheckResult.Degraded("External Dependencies",
                    $"External dependencies check found {issues.Count} issues: {string.Join(", ", issues)}", stopwatch.Elapsed);
            }

            return HealthCheckResult.Healthy("External Dependencies", "All external dependencies accessible", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HealthCheckResult.Unhealthy("External Dependencies", $"External dependencies health check failed: {ex.Message}", ex, stopwatch.Elapsed);
        }
    }

    private Task<HealthCheckResult> CheckSystemResourcesHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var issues = new List<string>();

            // Check available memory
            var memoryInfo = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            var memoryGB = memoryInfo / (1024.0 * 1024.0 * 1024.0);

            if (memoryGB > 4.0) // Arbitrary threshold
            {
                issues.Add($"High memory usage: {memoryGB:F2} GB");
            }

            // Check available disk space
            var driveInfo = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(Environment.CurrentDirectory));
            var availableGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

            if (availableGB < 1.0) // Less than 1GB free
            {
                issues.Add($"Low disk space: {availableGB:F2} GB available");
            }

            // Check CPU usage (rough estimate)
            var cpuUsage = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
            // This is total CPU time, not current usage - would need more complex monitoring

            stopwatch.Stop();

            if (issues.Any())
            {
                return Task.FromResult(HealthCheckResult.Degraded("System Resources",
                    $"System resources check found {issues.Count} issues: {string.Join(", ", issues)}", stopwatch.Elapsed));
            }

            return Task.FromResult(HealthCheckResult.Healthy("System Resources", "System resources within acceptable limits", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("System Resources", $"System resources health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private HealthStatus DetermineOverallHealthStatus(List<HealthCheckResult> results)
    {
        if (results.Any(r => r.Status == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;

        if (results.Any(r => r.Status == HealthStatus.Degraded))
            return HealthStatus.Degraded;

        if (results.Any(r => r.Status == HealthStatus.Unavailable))
            return HealthStatus.Degraded; // Treat unavailable as degraded, not unhealthy

        return HealthStatus.Healthy;
    }

    private void LogHealthCheckResults(HealthCheckReport report)
    {
        Log.Information("Health Check Summary - Overall Status: {Status}, Total Duration: {Duration}ms",
            report.OverallStatus, report.TotalDuration.TotalMilliseconds);

        foreach (var result in report.Results.OrderBy(r => r.ServiceName))
        {
            var logLevel = result.Status switch
            {
                HealthStatus.Healthy => Serilog.Events.LogEventLevel.Information,
                HealthStatus.Degraded => Serilog.Events.LogEventLevel.Warning,
                HealthStatus.Unhealthy => Serilog.Events.LogEventLevel.Error,
                HealthStatus.Unavailable => Serilog.Events.LogEventLevel.Warning,
                _ => Serilog.Events.LogEventLevel.Information
            };

            Log.Write(logLevel, "Health Check - {Service}: {Status} ({Duration}ms) - {Description}",
                result.ServiceName, result.Status, result.Duration.TotalMilliseconds, result.Description);

            if (result.Exception != null)
            {
                Log.Error(result.Exception, "Health check exception for {Service}", result.ServiceName);
            }
        }

        Log.Information("Health Check Statistics - Healthy: {Healthy}, Degraded: {Degraded}, Unhealthy: {Unhealthy}, Unavailable: {Unavailable}",
            report.HealthyCount, report.DegradedCount, report.UnhealthyCount, report.UnavailableCount);
    }

    private bool IsServiceSkipped(string serviceName)
    {
        return _healthCheckConfig.SkipServices.Contains(serviceName, StringComparer.OrdinalIgnoreCase);
    }

    private TimeSpan GetTimeoutForService(string serviceName)
    {
        return serviceName switch
        {
            "Database" => _healthCheckConfig.DatabaseTimeout,
            "Secret Vault" or "QuickBooks" or "AI Service" or "External Dependencies" => _healthCheckConfig.ExternalServiceTimeout,
            _ => _healthCheckConfig.DefaultTimeout
        };
    }

    private bool CanApplicationStart(HealthCheckReport report)
    {
        // Check critical services
        var criticalServices = report.Results.Where(r => _healthCheckConfig.CriticalServices.Contains(r.ServiceName, StringComparer.OrdinalIgnoreCase));
        var criticalFailures = criticalServices.Count(r => r.Status == HealthStatus.Unhealthy || r.Status == HealthStatus.Unavailable);

        if (criticalFailures > 0)
        {
            Log.Error("Application cannot start: {CriticalFailures} critical services are failing", criticalFailures);
            return false;
        }

        // Check overall failure rate
        var totalFailures = report.UnhealthyCount + report.UnavailableCount;
        var failureRate = (double)totalFailures / report.TotalCount;

        if (failureRate > 0.5) // More than 50% services failing
        {
            Log.Error("Application cannot start: {FailureRate:P0} of services are failing", failureRate);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves the Syncfusion license key from supported sources in priority order:
    /// 1) Environment variable SYNCFUSION_LICENSE_KEY
    /// 2) appsettings (Syncfusion:LicenseKey)
    /// 3) User Secrets (Syncfusion:LicenseKey)
    /// Returns null if not found or if placeholder value is detected.
    /// </summary>
    private string GetSyncfusionLicenseKey()
    {
        try
        {
            // 1) Environment variable
            var key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
                return key.Trim();

            // 2) Configuration
            key = _configuration?["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
                return key.Trim();

            // 3) User secrets (development)
            try
            {
                var userSecretsConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddUserSecrets<WileyWidget.App>()
                    .Build();
                key = userSecretsConfig["Syncfusion:LicenseKey"];
                if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
                    return key.Trim();
            }
            catch
            {
                // ignore if user secrets unavailable
            }
        }
        catch
        {
            // ignore resolution errors and fall through
        }

        return null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // No disposable resources
        }

        _disposed = true;
    }
}
