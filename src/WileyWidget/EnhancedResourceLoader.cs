using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using Microsoft.Extensions.Logging;
using Serilog;
using WileyWidget.Services;

namespace WileyWidget
{
    /// <summary>
    /// Enhanced resource loading implementation that fixes critical bugs in the original LoadApplicationResources method
    /// </summary>
    public class EnhancedResourceLoader
    {
        private readonly ILogger<EnhancedResourceLoader> _logger;

        public EnhancedResourceLoader(ILogger<EnhancedResourceLoader> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Loads application resources with comprehensive error handling and validation.
        /// This method fixes the critical bugs found in the original implementation:
        /// 1. Stream closure bug - streams are not closed prematurely
        /// 2. Uses SafeResourceDictionaryLoader for validation
        /// 3. Returns success/failure information
        /// 4. Uses proper pack URIs
        /// 5. Provides detailed error reporting
        /// </summary>
        /// <returns>ResourceLoadResult indicating success/failure and performance metrics</returns>
        public ResourceLoadResult LoadApplicationResources()
        {
            var result = new ResourceLoadResult();
            var sw = Stopwatch.StartNew();

            _logger.LogInformation("[RESOURCES] Starting enhanced application resource loading with bug fixes...");

            try
            {
                var resources = Application.Current?.Resources ?? new ResourceDictionary();

                // ✅ FIX: Use proper pack URIs instead of relative paths
                var resourcePaths = new[]
                {
                    "pack://application:,,,/src/Themes/Generic.xaml",
                    "pack://application:,,,/src/Themes/WileyTheme-Syncfusion.xaml"
                };

                // Load each resource dictionary with individual error handling
                foreach (var path in resourcePaths)
                {
                    LoadSingleResource(path, resources, result);
                }

                // ✅ FIX: Register converters safely with duplication check
                RegisterConvertersSafely(resources, result);

                // ✅ FIX: Provide success/failure feedback
                result.Success = result.ErrorCount == 0;
                result.LoadTimeMs = sw.ElapsedMilliseconds;

                _logger.LogInformation("[RESOURCES] ✓ Enhanced resource loading completed. Success: {Success}, Loaded: {LoadedCount}, Errors: {ErrorCount}, Time: {LoadTimeMs}ms",
                    result.Success, result.LoadedCount, result.ErrorCount, result.LoadTimeMs);

                // Log assembly context for troubleshooting if there were any issues
                if (result.ErrorCount > 0)
                {
                    LogAssemblyContext();
                }

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Success = false;
                result.Errors.Add(ex);
                result.LoadTimeMs = sw.ElapsedMilliseconds;

                _logger.LogError(ex, "[RESOURCES] ✗ Critical failure in enhanced LoadApplicationResources after {LoadTimeMs}ms: {Message}",
                    sw.ElapsedMilliseconds, ex.Message);

                LogAssemblyContext();
                return result;
            }
        }

        /// <summary>
        /// Loads a single resource with proper error handling and validation
        /// </summary>
        private void LoadSingleResource(string path, ResourceDictionary resources, ResourceLoadResult result)
        {
            try
            {
                _logger.LogDebug("[RESOURCES] Loading resource dictionary: {Path}", path);

                // ✅ FIX: Use SafeResourceDictionaryLoader instead of direct loading
                // This handles XAML validation, color token validation, and proper stream management
                var resourceDict = SafeResourceDictionaryLoader.Load(path, _logger);

                if (resourceDict != null && resourceDict.Keys.Count > 0)
                {
                    resources.MergedDictionaries.Add(resourceDict);
                    result.LoadedPaths.Add(path);
                    result.LoadedCount++;
                    _logger.LogDebug("[RESOURCES] ✓ Successfully loaded {Path} with {KeyCount} keys", path, resourceDict.Keys.Count);
                }
                else
                {
                    result.FailedPaths.Add(path);
                    result.ErrorCount++;
                    _logger.LogWarning("[RESOURCES] ✗ Resource dictionary empty or failed to load: {Path}", path);
                }
            }
            catch (System.Windows.Markup.XamlParseException xamlEx)
            {
                result.FailedPaths.Add(path);
                result.ErrorCount++;
                result.Errors.Add(xamlEx);

                _logger.LogError(xamlEx, "[RESOURCES] ✗ XAML Parse Error loading {Path}: {Message} at Line {LineNumber}, Position {LinePosition}",
                    path, xamlEx.Message, xamlEx.LineNumber, xamlEx.LinePosition);

                if (xamlEx.InnerException != null)
                {
                    _logger.LogError("[RESOURCES] XAML Parse Inner Exception: {InnerMessage}", xamlEx.InnerException.Message);
                }
            }
            catch (System.IO.FileNotFoundException fileEx)
            {
                result.FailedPaths.Add(path);
                result.ErrorCount++;
                result.Errors.Add(fileEx);
                _logger.LogWarning(fileEx, "[RESOURCES] ✗ Resource file not found: {Path}", path);
            }
            catch (Exception ex)
            {
                result.FailedPaths.Add(path);
                result.ErrorCount++;
                result.Errors.Add(ex);
                _logger.LogError(ex, "[RESOURCES] ✗ Unexpected error loading {Path}: {Message}", path, ex.Message);
            }
        }

        /// <summary>
        /// Registers converters safely with duplication checking
        /// </summary>
        private void RegisterConvertersSafely(ResourceDictionary resources, ResourceLoadResult result)
        {
            try
            {
                // ✅ FIX: Check for existing converters before adding
                if (!resources.Contains("BooleanToVisibilityConverter"))
                {
                    resources["BooleanToVisibilityConverter"] = new WileyWidget.Converters.BooleanToVisibilityConverter();
                    _logger.LogDebug("[RESOURCES] ✓ BooleanToVisibilityConverter registered");
                }
                else
                {
                    _logger.LogDebug("[RESOURCES] ℹ BooleanToVisibilityConverter already present, skipping registration");
                }
            }
            catch (Exception converterEx)
            {
                result.ErrorCount++;
                result.Errors.Add(converterEx);
                _logger.LogError(converterEx, "[RESOURCES] ✗ Failed to register BooleanToVisibilityConverter");
            }
        }

        /// <summary>
        /// Logs assembly context for troubleshooting
        /// </summary>
        private void LogAssemblyContext()
        {
            try
            {
                var currentAssembly = Assembly.GetExecutingAssembly();
                var assemblyLocation = currentAssembly.Location;
                var assemblyName = currentAssembly.GetName();

                _logger.LogInformation("[RESOURCES] Assembly Context - Name: {AssemblyName}, Version: {Version}, Location: {Location}",
                    assemblyName.Name, assemblyName.Version, assemblyLocation);
            }
            catch (Exception contextEx)
            {
                _logger.LogWarning(contextEx, "[RESOURCES] Failed to log assembly context");
            }
        }
    }

    /// <summary>
    /// Result object for resource loading operations with detailed metrics and error information
    /// </summary>
    public class ResourceLoadResult
    {
        /// <summary>
        /// Indicates whether the resource loading operation completed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Number of resources successfully loaded
        /// </summary>
        public int LoadedCount { get; set; }

        /// <summary>
        /// Number of errors encountered during loading
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Total time taken for the loading operation in milliseconds
        /// </summary>
        public long LoadTimeMs { get; set; }

        /// <summary>
        /// List of successfully loaded resource paths
        /// </summary>
        public List<string> LoadedPaths { get; set; } = new();

        /// <summary>
        /// List of resource paths that failed to load
        /// </summary>
        public List<string> FailedPaths { get; set; } = new();

        /// <summary>
        /// Collection of all errors encountered during loading
        /// </summary>
        public List<Exception> Errors { get; set; } = new();

        /// <summary>
        /// Returns a summary string of the loading results
        /// </summary>
        public override string ToString()
        {
            return $"ResourceLoadResult: Success={Success}, Loaded={LoadedCount}, Errors={ErrorCount}, Time={LoadTimeMs}ms";
        }
    }

    // Extension method for the existing App class to use the enhanced loader
    public static class AppResourceLoadingExtensions
    {
        /// <summary>
        /// Extension method to replace the buggy LoadApplicationResources method in App.xaml.cs
        /// Usage: this.LoadApplicationResourcesEnhanced() in App.xaml.cs OnStartup method
        /// </summary>
        public static ResourceLoadResult LoadApplicationResourcesEnhanced(this App app, ILogger<EnhancedResourceLoader> logger = null)
        {
            // Use Serilog if no logger provided (maintains compatibility with existing code)
            if (logger == null)
            {
                // Create a temporary logger adapter for Serilog
                var enhancedLoader = new EnhancedResourceLoader(new SerilogLoggerAdapter<EnhancedResourceLoader>());
                return enhancedLoader.LoadApplicationResources();
            }
            else
            {
                var enhancedLoader = new EnhancedResourceLoader(logger);
                return enhancedLoader.LoadApplicationResources();
            }
        }
    }

    /// <summary>
    /// Adapter to use Serilog with ILogger interface for backward compatibility
    /// </summary>
    internal class SerilogLoggerAdapter<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);

            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    Serilog.Log.Debug(exception, message);
                    break;
                case LogLevel.Information:
                    Serilog.Log.Information(exception, message);
                    break;
                case LogLevel.Warning:
                    Serilog.Log.Warning(exception, message);
                    break;
                case LogLevel.Error:
                    Serilog.Log.Error(exception, message);
                    break;
                case LogLevel.Critical:
                    Serilog.Log.Fatal(exception, message);
                    break;
            }
        }
    }
}
