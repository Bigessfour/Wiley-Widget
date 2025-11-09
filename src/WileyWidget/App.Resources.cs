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
using WileyWidget.Startup;
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
                    Log.Warning("[STARTUP] Container not ready, using direct EnterpriseResourceLoader instantiation");
                    resourceLoader = new Startup.EnterpriseResourceLoader(
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<Startup.EnterpriseResourceLoader>.Instance,
                        Services.ErrorReportingService.Instance ?? new Services.ErrorReportingService(),
                        Services.Telemetry.SigNozTelemetryService.Instance ?? new Services.Telemetry.SigNozTelemetryService()
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
        /// </summary>
        private void VerifyAndApplyTheme()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                Log.Information("[THEME] Verifying and applying Syncfusion theme");

                // Check available memory before theme application (128MB minimum)
                var availableMemoryMB = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
                const int minMemoryMB = 128;

                if (availableMemoryMB < minMemoryMB)
                {
                    var errorMsg = $"Insufficient memory for theme application: {availableMemoryMB}MB available, {minMemoryMB}MB required";
                    Log.Fatal("[THEME] {Error}", errorMsg);
                    throw new InsufficientMemoryException(errorMsg);
                }

                // Apply FluentLight theme using SfSkinManager
                // This sets SfSkinManager.ApplicationTheme which is required for region adapters
                var theme = new Syncfusion.SfSkinManager.Theme("FluentLight");
                SfSkinManager.ApplyTheme(Application.Current, theme);

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
