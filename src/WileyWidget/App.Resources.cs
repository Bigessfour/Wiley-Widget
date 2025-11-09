// App.Resources.cs - Resource Loading & Theme Management Partial Class
// Contains: Resource loading, theme application, and resource management methods
// Part of App.xaml.cs partial class split for maintainability

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Prism.Ioc;
using Serilog;
using Syncfusion.SfSkinManager;
using WileyWidget.Abstractions;
using WileyWidget.Services;
using WileyWidget.Services.Telemetry;

namespace WileyWidget
{
    /// <summary>
    /// Resource loading and theme management partial class.
    /// Contains synchronous resource loading and theme application logic.
    /// </summary>
    public partial class App
    {
        #region Resource Loading

        /// <summary>
        /// Loads application resources synchronously during WPF startup.
        /// This is the SINGLE CANONICAL METHOD for resource loading to avoid UI thread deadlocks.
        /// Uses the EnterpriseResourceLoader for resilient resource loading.
        /// </summary>
        private void LoadApplicationResourcesSync()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                Log.Information("[STARTUP] Loading application resources synchronously");

                // Get the enterprise resource loader from DI container
                // Note: Container may not be fully initialized yet, so use direct instantiation if needed
                IResourceLoader resourceLoader;
                try
                {
                    resourceLoader = Container.Resolve<IResourceLoader>();
                }
                catch
                {
                    // Fallback: create new instance if container not ready
                    // Note: This should rarely happen - container should be available by this point
                    Log.Warning("[STARTUP] Container not ready, using NullLogger for EnterpriseResourceLoader");
                    resourceLoader = new Startup.EnterpriseResourceLoader(
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<Startup.EnterpriseResourceLoader>.Instance,
                        null, // ErrorReportingService - will use Serilog directly
                        null  // SigNozTelemetryService - telemetry unavailable in fallback
                    );
                }

                // Load resources synchronously (no async/await to avoid WPF UI thread deadlocks)
                var result = Task.Run(() => resourceLoader.LoadApplicationResourcesAsync()).GetAwaiter().GetResult();

                if (result.Success)
                {
                    Log.Information("[STARTUP] ✓ Resources loaded successfully ({Count} resources in {Ms}ms)",
                        result.LoadedCount, sw.ElapsedMilliseconds);
                }
                else
                {
                    Log.Error("[STARTUP] ✗ Resource loading failed: {Errors} errors, {Loaded} loaded",
                        result.ErrorCount, result.LoadedCount);

                    // Don't fail startup for resource loading issues - log and continue
                    // Critical resources are handled by the EnterpriseResourceLoader's criticality system
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[STARTUP] ✗ Critical failure loading application resources");
                throw; // This is critical - rethrow to fail startup
            }
        }

        #endregion

        #region Theme Management

        /// <summary>
        /// Verifies and applies the Syncfusion theme for the application.
        /// This method ensures theme is applied before Prism's ConfigureRegionAdapterMappings is called.
        /// CRITICAL: Sets SfSkinManager.ApplyThemeAsDefaultStyle = true for global theme application.
        /// </summary>
        private void VerifyAndApplyTheme()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                Log.Information("[THEME] Verifying and applying Syncfusion theme");

                // CRITICAL: Enable automatic theme application to all controls
                // Per Syncfusion docs: https://help.syncfusion.com/wpf/themes/skin-manager#apply-a-theme-globally-in-the-application
                // This MUST be set before setting ApplicationTheme
                SfSkinManager.ApplyThemeAsDefaultStyle = true;
                Log.Debug("[THEME] ApplyThemeAsDefaultStyle enabled for global theme propagation");

                // Check available system memory before theme application
                // Note: Using GC memory info to get available memory, not process working set
                try
                {
                    var gcMemInfo = GC.GetGCMemoryInfo();
                    var totalMemoryMB = gcMemInfo.TotalAvailableMemoryBytes / (1024 * 1024);
                    var currentMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                    var availableMemoryMB = totalMemoryMB - currentMemoryMB;
                    const int minMemoryMB = 128;

                    Log.Debug("[THEME] Memory status: {Available}MB available of {Total}MB total (current usage: {Current}MB)",
                        availableMemoryMB, totalMemoryMB, currentMemoryMB);

                    if (availableMemoryMB < minMemoryMB)
                    {
                        Log.Warning("[THEME] Low memory detected: {Available}MB available, {Required}MB recommended. " +
                                    "Proceeding with theme application - this may cause performance issues.",
                            availableMemoryMB, minMemoryMB);
                        // Don't throw - this is informational only. Theme will work with less memory.
                    }
                }
                catch (Exception memEx)
                {
                    Log.Warning(memEx, "[THEME] Could not check memory status - proceeding with theme application");
                }

                // Apply FluentLight theme using SfSkinManager
                // This sets SfSkinManager.ApplicationTheme which is required for region adapters
                // NOTE: MainWindow doesn't exist yet (CreateShell hasn't been called), so we only set ApplicationTheme
                var theme = new Syncfusion.SfSkinManager.Theme("FluentLight");
                SfSkinManager.ApplicationTheme = theme;

                // The theme will be automatically applied to the Shell window when it's created
                // because ApplicationTheme is already set

                // Verify theme was applied successfully
                if (SfSkinManager.ApplicationTheme == null)
                {
                    var errorMsg = "Theme application failed - SfSkinManager.ApplicationTheme is null after ApplyTheme()";
                    Log.Fatal("[THEME] {Error}", errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }

                Log.Information("[THEME] ✓ Theme applied successfully: {Theme} ({Ms}ms)",
                    SfSkinManager.ApplicationTheme.ToString(), sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[THEME] ✗ Critical failure applying theme");
                throw; // This is critical - rethrow to fail startup
            }
        }

        #endregion
    }
}
