using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;
using Serilog;
using WileyWidget.Abstractions;
using WileyWidget.Services;
using WileyWidget.Services.Telemetry;

namespace WileyWidget.Startup
{
    /// <summary>
    /// Enterprise-grade resource loader with comprehensive error handling, retry logic,
    /// timeout enforcement, idempotency guarantees, and telemetry integration.
    /// This is the SINGLE CANONICAL implementation for application resource loading.
    /// </summary>
    public class EnterpriseResourceLoader : IResourceLoader
    {
        private readonly ILogger<EnterpriseResourceLoader> _logger;
        private readonly ErrorReportingService _errorReporting;
        private readonly SigNozTelemetryService _telemetry;
        private readonly ConcurrentDictionary<string, bool> _loadedResources = new();
        private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
        private DateTimeOffset? _lastLoadTimestamp;
        private bool _resourcesLoaded = false;
        private int _totalLoadAttempts = 0;
        private int _consecutiveFailures = 0;
        private TimeSpan? _lastLoadDuration;

        // Resource configuration
        private readonly Dictionary<string, ResourceCriticality> _resourceCatalog = new()
        {
            // Critical resources - application cannot start without these
            // App.xaml resources - loaded at application startup
            { "pack://application:,,,/WileyWidget;component/Themes/Generic.xaml", ResourceCriticality.Critical },
            { "pack://application:,,,/WileyWidget;component/Themes/WileyTheme-Syncfusion.xaml", ResourceCriticality.Critical },

            // Shell.xaml resources - required for main window
            { "pack://application:,,,/WileyWidget;component/Resources/DataTemplates.xaml", ResourceCriticality.Critical },

            // Optional resources - application can start with degraded functionality
            { "pack://application:,,,/WileyWidget;component/Resources/Strings.xaml", ResourceCriticality.Optional }
        };

    // Polly resilience policies (using Polly v8 API)
    private readonly ResiliencePipeline<ResourceDictionary?> _resiliencePipeline;

        public bool AreResourcesLoaded => _resourcesLoaded;
        public DateTimeOffset? LastLoadTimestamp => _lastLoadTimestamp;

        /// <summary>
        /// Gets health metrics for monitoring and alerting.
        /// </summary>
        public ResourceLoaderHealth GetHealth() => new()
        {
            IsHealthy = _resourcesLoaded && _consecutiveFailures < 3,
            ResourcesLoaded = _resourcesLoaded,
            LastLoadTimestamp = _lastLoadTimestamp,
            LastLoadDuration = _lastLoadDuration,
            TotalLoadAttempts = _totalLoadAttempts,
            ConsecutiveFailures = _consecutiveFailures,
            LoadedResourceCount = _loadedResources.Count,
            ExpectedResourceCount = _resourceCatalog.Count
        };

        public EnterpriseResourceLoader(
            ILogger<EnterpriseResourceLoader> logger,
            ErrorReportingService errorReporting,
            SigNozTelemetryService telemetry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorReporting = errorReporting ?? throw new ArgumentNullException(nameof(errorReporting));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

            // Create Polly v8 resilience pipeline with modern API
            // Following Microsoft's recommended patterns for resilience
            _resiliencePipeline = new ResiliencePipelineBuilder<ResourceDictionary?>()
                // 1. TIMEOUT POLICY - Outermost layer (60s per resource for cold starts)
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(60),
                    OnTimeout = args =>
                    {
                        // ResourcePath retrieval via ResilienceProperties is optional. Keep logs simple.
                        var resourcePath = "unknown";
                        _logger.LogError(
                            "[RESOURCE_LOADER] ⏱️ Timeout loading {ResourcePath} after {TimeoutSeconds}s",
                            resourcePath,
                            args.Timeout.TotalSeconds);

                        _errorReporting.TrackEvent("ResourceLoad_Timeout", new Dictionary<string, object>
                        {
                            ["ResourcePath"] = resourcePath ?? "unknown",
                            ["TimeoutSeconds"] = args.Timeout.TotalSeconds
                        });

                        return ValueTask.CompletedTask;
                    }
                })
                // 2. CIRCUIT BREAKER - Prevents cascade failures
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions<ResourceDictionary?>
                {
                    FailureRatio = 0.5,                    // Open if 50% of requests fail
                    SamplingDuration = TimeSpan.FromSeconds(30),  // Sample window
                    MinimumThroughput = 5,                 // Minimum requests before evaluating
                    BreakDuration = TimeSpan.FromMinutes(2), // Stay open for 2 minutes
                    ShouldHandle = new PredicateBuilder<ResourceDictionary?>()
                        .Handle<IOException>()
                        .Handle<UnauthorizedAccessException>()
                        .Handle<System.Windows.Markup.XamlParseException>(ex => IsTransientXamlError(ex))
                        .Handle<TimeoutRejectedException>(),
                    OnOpened = args =>
                    {
                        _logger.LogError(
                            args.Outcome.Exception,
                            "[RESOURCE_LOADER] ⚠️ Circuit breaker OPEN - too many failures. Breaking for {Minutes} minute(s)",
                            args.BreakDuration.TotalMinutes);

                        _errorReporting.TrackEvent("ResourceLoad_CircuitBreakerOpen", new Dictionary<string, object>
                        {
                            ["BreakDurationMinutes"] = args.BreakDuration.TotalMinutes,
                            ["ExceptionType"] = args.Outcome.Exception?.GetType().Name ?? "unknown"
                        });

                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        _logger.LogInformation("[RESOURCE_LOADER] ✓ Circuit breaker CLOSED - resuming normal operation");
                        _errorReporting.TrackEvent("ResourceLoad_CircuitBreakerReset", new Dictionary<string, object>());
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = args =>
                    {
                        _logger.LogInformation("[RESOURCE_LOADER] Circuit breaker HALF-OPEN - testing recovery");
                        return ValueTask.CompletedTask;
                    }
                })
                // 3. RETRY POLICY - Exponential backoff with jitter (modern best practice)
                .AddRetry(new RetryStrategyOptions<ResourceDictionary?>
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromMilliseconds(100),        // Base delay
                    UseJitter = true,                               // Add randomness to prevent thundering herd
                    ShouldHandle = new PredicateBuilder<ResourceDictionary?>()
                        .Handle<IOException>()
                        .Handle<UnauthorizedAccessException>()
                        .Handle<System.Windows.Markup.XamlParseException>(ex => IsTransientXamlError(ex)),
                    OnRetry = args =>
                    {
                        // ResourcePath retrieval via ResilienceProperties is optional. Keep logs simple.
                        var resourcePath = "unknown";
                        _logger.LogWarning(
                            args.Outcome.Exception,
                            "[RESOURCE_LOADER] Retry {RetryCount}/3 for {ResourcePath} after {Delay}ms due to {ExceptionType}",
                            args.AttemptNumber + 1,
                            resourcePath,
                            args.RetryDelay.TotalMilliseconds,
                            args.Outcome.Exception?.GetType().Name);

                        _errorReporting.TrackEvent("ResourceLoad_Retry", new Dictionary<string, object>
                        {
                            ["ResourcePath"] = resourcePath ?? "unknown",
                            ["RetryCount"] = args.AttemptNumber + 1,
                            ["DelayMs"] = args.RetryDelay.TotalMilliseconds,
                            ["ExceptionType"] = args.Outcome.Exception?.GetType().Name ?? "unknown"
                        });

                        return ValueTask.CompletedTask;
                    }
                })
                .Build();

            // NOTE: ResiliencePipelineProvider is optional and not required for current implementation.
            // Keep constructor lightweight to avoid hard dependency on Polly.Registry types in startup code.

            _logger.LogInformation(
                "[RESOURCE_LOADER] ✓ Enterprise resource loader initialized with Polly v8 resilience pipeline " +
                "(timeout: 60s, circuit breaker: 5 failures/2min break, retry: 3x exponential backoff with jitter)");
        }

        /// <summary>
        /// Loads application resources with comprehensive error handling and resilience.
        /// This method is idempotent and safe to call multiple times.
        /// </summary>
        public async Task<ResourceLoadResult> LoadApplicationResourcesAsync(CancellationToken cancellationToken = default)
        {
            // Idempotency check (fast path - no lock needed)
            if (_resourcesLoaded)
            {
                _logger.LogDebug("[RESOURCE_LOADER] Resources already loaded at {Timestamp}, skipping duplicate load",
                    _lastLoadTimestamp);
                return new ResourceLoadResult
                {
                    Success = true,
                    LoadedCount = _loadedResources.Count,
                    ErrorCount = 0,
                    Diagnostics = new Dictionary<string, object>
                    {
                        ["Reason"] = "Already loaded",
                        ["LastLoadTimestamp"] = _lastLoadTimestamp ?? DateTimeOffset.MinValue
                    }
                };
            }

            // Thread-safe loading with async semaphore
            await _loadSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                if (_resourcesLoaded)
                {
                    return new ResourceLoadResult { Success = true, LoadedCount = _loadedResources.Count };
                }

                return await LoadResourcesInternal(cancellationToken);
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }

        private async Task<ResourceLoadResult> LoadResourcesInternal(CancellationToken cancellationToken)
        {
            var result = new ResourceLoadResult();
            var sw = Stopwatch.StartNew();

            Interlocked.Increment(ref _totalLoadAttempts);

            using var activity = SigNozTelemetryService.ActivitySource.StartActivity("resource.load.startup");
            activity?.SetTag("resource.count", _resourceCatalog.Count);
            activity?.SetTag("resource.loader", "EnterpriseResourceLoader");            _logger.LogInformation("[RESOURCE_LOADER] Starting enterprise resource loading with {Count} resources (attempt #{Attempt})",
                _resourceCatalog.Count, _totalLoadAttempts);

            try
            {
                var resources = Application.Current?.Resources ?? new ResourceDictionary();
                var loadTasks = new List<Task>();

                // Load all resources with individual error handling
                foreach (var (path, criticality) in _resourceCatalog)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("[RESOURCE_LOADER] Resource loading cancelled");
                        result.Success = false;
                        result.Diagnostics["CancellationRequested"] = true;
                        break;
                    }

                    var loadResult = await LoadSingleResourceAsync(path, criticality, resources, cancellationToken);

                    if (loadResult.Success)
                    {
                        result.LoadedCount++;
                        result.LoadedPaths.Add(path);
                        _loadedResources.TryAdd(path, true);
                    }
                    else
                    {
                        result.ErrorCount++;
                        result.FailedPaths.Add(path);
                        if (loadResult.Errors.Any())
                        {
                            result.Errors.AddRange(loadResult.Errors);
                        }

                        // Critical resource failure means overall failure
                        if (criticality == ResourceCriticality.Critical)
                        {
                            result.HasCriticalFailures = true;
                            _logger.LogError("[RESOURCE_LOADER] Critical resource failed: {Path}", path);
                        }
                    }

                    result.RetryCount += loadResult.RetryCount;
                }

                // Register converters with error handling
                await RegisterConvertersAsync(resources, result);

                // Determine overall success
                result.Success = !result.HasCriticalFailures;
                sw.Stop();
                result.LoadTimeMs = sw.ElapsedMilliseconds;

                // Update state
                if (result.Success)
                {
                    _resourcesLoaded = true;
                    _lastLoadTimestamp = DateTimeOffset.UtcNow;
                    _lastLoadDuration = sw.Elapsed;
                    _consecutiveFailures = 0; // Reset on success
                }
                else
                {
                    Interlocked.Increment(ref _consecutiveFailures);
                    _lastLoadDuration = sw.Elapsed;
                }

                // Update telemetry
                activity?.SetTag("resource.loaded_count", result.LoadedCount);
                activity?.SetTag("resource.error_count", result.ErrorCount);
                activity?.SetTag("resource.retry_count", result.RetryCount);
                activity?.SetTag("resource.duration_ms", result.LoadTimeMs);
                activity?.SetTag("resource.success", result.Success);
                activity?.SetTag("resource.critical_failures", result.HasCriticalFailures);

                // Report to error tracking
                _errorReporting.TrackEvent("ResourceLoad_Completed", new Dictionary<string, object>
                {
                    ["Success"] = result.Success,
                    ["LoadedCount"] = result.LoadedCount,
                    ["ErrorCount"] = result.ErrorCount,
                    ["RetryCount"] = result.RetryCount,
                    ["LoadTimeMs"] = result.LoadTimeMs,
                    ["HasCriticalFailures"] = result.HasCriticalFailures
                });

                _logger.LogInformation(
                    "[RESOURCE_LOADER] ✓ Enterprise resource loading completed. " +
                    "Success: {Success}, Loaded: {LoadedCount}/{Total}, Errors: {ErrorCount}, Retries: {RetryCount}, Time: {LoadTimeMs}ms",
                    result.Success, result.LoadedCount, _resourceCatalog.Count, result.ErrorCount, result.RetryCount, result.LoadTimeMs);

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Success = false;
                result.HasCriticalFailures = true;
                result.Errors.Add(ex);
                result.LoadTimeMs = sw.ElapsedMilliseconds;

                activity?.SetTag("resource.exception", ex.GetType().Name);
                _telemetry.RecordException(ex, ("resource.load", "critical_failure"));

                _logger.LogError(ex, "[RESOURCE_LOADER] ✗ Critical failure in enterprise resource loading after {LoadTimeMs}ms",
                    sw.ElapsedMilliseconds);

                _errorReporting.TrackEvent("ResourceLoad_CriticalFailure", new Dictionary<string, object>
                {
                    ["ExceptionType"] = ex.GetType().Name,
                    ["ExceptionMessage"] = ex.Message,
                    ["LoadTimeMs"] = result.LoadTimeMs
                });

                return result;
            }
        }

        private async Task<ResourceLoadResult> LoadSingleResourceAsync(
            string path,
            ResourceCriticality criticality,
            ResourceDictionary resources,
            CancellationToken cancellationToken)
        {
            var result = new ResourceLoadResult();
            var sw = Stopwatch.StartNew();

            using var activity = SigNozTelemetryService.ActivitySource.StartActivity("resource.load.single");
            activity?.SetTag("resource.path", path);
            activity?.SetTag("resource.criticality", criticality.ToString());

            _logger.LogDebug("[RESOURCE_LOADER] Loading resource: {Path} (criticality: {Criticality})",
                path, criticality);

            try
            {
                // Validate resource URI exists before attempting load
                var uri = new Uri(path, UriKind.Absolute);
                var streamInfo = Application.GetResourceStream(uri);
                if (streamInfo?.Stream == null)
                {
                    throw new FileNotFoundException($"Resource stream not found: {path}");
                }
                streamInfo.Stream.Close(); // Close validation stream

                // Execute with Polly v8 resilience pipeline
                var resilienceContext = ResilienceContextPool.Shared.Get(cancellationToken);
                resilienceContext.Properties.Set(new ResiliencePropertyKey<string>("ResourcePath"), path);

                try
                {
                    var resourceDict = await _resiliencePipeline.ExecuteAsync(
                        async (ctx) =>
                        {
                            // Track retry count using context
                            if (ctx.Properties.TryGetValue(new ResiliencePropertyKey<int>("RetryCount"), out var retryCount))
                            {
                                result.RetryCount = retryCount + 1;
                                ctx.Properties.Set(new ResiliencePropertyKey<int>("RetryCount"), result.RetryCount);
                            }
                            else
                            {
                                ctx.Properties.Set(new ResiliencePropertyKey<int>("RetryCount"), 0);
                            }

                            // Use SafeResourceDictionaryLoader for validation
                            return await Task.Run(() =>
                            {
                                // Convert pack URI to file path for SafeResourceDictionaryLoader
                                var uri = new Uri(path, UriKind.Absolute);

                                // For pack:// URIs, load directly through WPF
                                var streamInfo = Application.GetResourceStream(uri);
                                if (streamInfo?.Stream == null)
                                {
                                    throw new FileNotFoundException($"Resource stream not found for {path}");
                                }

                                // Load via ResourceDictionary directly (SafeResourceDictionaryLoader is for file paths)
                                var dict = new ResourceDictionary { Source = uri };

                                if (dict.Keys.Count == 0)
                                {
                                    _logger.LogWarning("[RESOURCE_LOADER] Resource dictionary loaded but contains no keys: {Path}", path);
                                }

                                streamInfo.Stream.Close();
                                return dict;
                            }, ctx.CancellationToken);
                        },
                        resilienceContext);

                    if (resourceDict != null && resourceDict.Keys.Count > 0)
                    {
                        resources.MergedDictionaries.Add(resourceDict);
                        result.Success = true;
                        result.LoadedCount = 1;

                        sw.Stop();
                        activity?.SetTag("resource.key_count", resourceDict.Keys.Count);
                        activity?.SetTag("resource.load_duration_ms", sw.ElapsedMilliseconds);

                        _logger.LogDebug("[RESOURCE_LOADER] ✓ Successfully loaded {Path} with {KeyCount} keys in {Ms}ms (retries: {RetryCount})",
                            path, resourceDict.Keys.Count, sw.ElapsedMilliseconds, result.RetryCount);
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorCount = 1;
                        var emptyError = new InvalidOperationException($"Resource dictionary empty or null: {path}");
                        result.Errors.Add(emptyError);

                        _logger.LogError("[RESOURCE_LOADER] ✗ Resource dictionary empty or null: {Path}", path);
                        _telemetry.RecordException(emptyError, ("resource.load", "empty_dictionary"));
                    }
                }
                finally
                {
                    // Return context to pool for reuse (Polly v8 best practice)
                    ResilienceContextPool.Shared.Return(resilienceContext);
                }

                return result;
            }
            catch (System.Windows.Markup.XamlParseException xamlEx)
            {
                sw.Stop();
                result.Success = false;
                result.ErrorCount = 1;
                result.Errors.Add(xamlEx);

                _telemetry.RecordException(xamlEx, ("resource.load", "xaml_parse_error"));

                _logger.LogError(xamlEx,
                    "[RESOURCE_LOADER] ✗ XAML Parse Error loading {Path}: {Message} at Line {LineNumber}, Position {LinePosition}",
                    path, xamlEx.Message, xamlEx.LineNumber, xamlEx.LinePosition);

                // Log for optional resources as warning, critical as error
                if (criticality == ResourceCriticality.Optional)
                {
                    _logger.LogWarning("[RESOURCE_LOADER] Optional resource failed to load, continuing: {Path}", path);
                }

                return result;
            }
            catch (TimeoutRejectedException timeoutEx)
            {
                sw.Stop();
                result.Success = false;
                result.ErrorCount = 1;
                result.Errors.Add(timeoutEx);

                _telemetry.RecordException(timeoutEx, ("resource.load", "timeout"));

                _logger.LogError(timeoutEx,
                    "[RESOURCE_LOADER] ⏱️ TIMEOUT loading {Path} after {Ms}ms (criticality: {Criticality}). " +
                    "This may indicate slow disk I/O, large XAML files, or cold start delays.",
                    path, sw.ElapsedMilliseconds, criticality);

                // Track detailed timeout metrics
                _errorReporting.TrackEvent("ResourceLoad_Timeout_Detailed", new Dictionary<string, object>
                {
                    ["ResourcePath"] = path,
                    ["Criticality"] = criticality.ToString(),
                    ["ElapsedMs"] = sw.ElapsedMilliseconds,
                    ["RetryCount"] = result.RetryCount,
                    ["ExceptionType"] = timeoutEx.GetType().Name
                });

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Success = false;
                result.ErrorCount = 1;
                result.Errors.Add(ex);

                _telemetry.RecordException(ex, ("resource.load", "unexpected_error"));

                _logger.LogError(ex, "[RESOURCE_LOADER] ✗ Unexpected error loading {Path}: {Message}",
                    path, ex.Message);

                return result;
            }
        }

        private async Task RegisterConvertersAsync(ResourceDictionary resources, ResourceLoadResult result)
        {
            try
            {
                await Task.Run(() =>
                {
                    // Check for existing converters before adding (idempotency)
                    if (!resources.Contains("BooleanToVisibilityConverter"))
                    {
                        resources["BooleanToVisibilityConverter"] = new WileyWidget.Converters.BooleanToVisibilityConverter();
                        _logger.LogDebug("[RESOURCE_LOADER] ✓ BooleanToVisibilityConverter registered");
                    }
                    else
                    {
                        _logger.LogDebug("[RESOURCE_LOADER] ℹ BooleanToVisibilityConverter already present, skipping");
                    }
                });
            }
            catch (Exception ex)
            {
                result.ErrorCount++;
                result.Errors.Add(ex);
                _logger.LogError(ex, "[RESOURCE_LOADER] ✗ Failed to register converters");
            }
        }

        /// <summary>
        /// Determines if a XAML parse exception is transient and should be retried.
        /// </summary>
        private bool IsTransientXamlError(System.Windows.Markup.XamlParseException ex)
        {
            // File access errors embedded in XAML exceptions are transient
            var inner = ex.InnerException;
            while (inner != null)
            {
                if (inner is IOException || inner is UnauthorizedAccessException)
                {
                    return true;
                }
                inner = inner.InnerException;
            }

            return false;
        }
    }

    /// <summary>
    /// Health metrics for ResourceLoader monitoring and alerting.
    /// </summary>
    public class ResourceLoaderHealth
    {
        public bool IsHealthy { get; init; }
        public bool ResourcesLoaded { get; init; }
        public DateTimeOffset? LastLoadTimestamp { get; init; }
        public TimeSpan? LastLoadDuration { get; init; }
        public int TotalLoadAttempts { get; init; }
        public int ConsecutiveFailures { get; init; }
        public int LoadedResourceCount { get; init; }
        public int ExpectedResourceCount { get; init; }

        public override string ToString() =>
            $"Health: {(IsHealthy ? "✓ Healthy" : "✗ Unhealthy")} | " +
            $"Loaded: {LoadedResourceCount}/{ExpectedResourceCount} | " +
            $"Attempts: {TotalLoadAttempts} | " +
            $"Failures: {ConsecutiveFailures} | " +
            $"LastLoad: {LastLoadDuration?.TotalMilliseconds:F0}ms";
    }
}
