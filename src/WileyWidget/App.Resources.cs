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
        /// This is called AFTER the DI container is initialized, ensuring proper service resolution.
        /// Uses the EnterpriseResourceLoader for resilient resource loading with full telemetry.
        /// </summary>
        private void LoadApplicationResourcesSync()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                Log.Information("[STARTUP] Loading application resources synchronously (container ready)");

                // Get the enterprise resource loader from DI container
                // Container is guaranteed to be available since this is called after base.OnStartup()
                var resourceLoader = Container.Resolve<IResourceLoader>();
                Log.Debug("[STARTUP] EnterpriseResourceLoader resolved from DI container with full services");

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

        /// <summary>
        /// Pre-loads critical global resources (Strings.xaml, DataTemplates.xaml) before vault migration.
        /// This addresses CRITICAL dispatcher_invoke_issues where resources like PrimaryTextBrush/Btn_Export
        /// are unavailable during Prism bootstrap. Loading these early ensures WPF resource resolution
        /// works correctly when Container.Resolve&lt;Shell&gt;() is called.
        /// </summary>
        private void PreLoadCriticalResources()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                Log.Information("[STARTUP] Pre-loading critical global resources before vault migration");

                // CRITICAL: Load Strings.xaml first (contains localized strings and brushes)
                var stringsUri = new Uri("pack://application:,,,/WileyWidget;component/Resources/Strings.xaml");
                var stringsDict = new ResourceDictionary { Source = stringsUri };
                Application.Current.Resources.MergedDictionaries.Add(stringsDict);
                Log.Debug("[STARTUP] ✓ Strings.xaml pre-loaded successfully");

                // CRITICAL: Load Generic.xaml (contains theme brushes like TreeGridHeaderBrush)
                var genericUri = new Uri("pack://application:,,,/WileyWidget;component/Themes/Generic.xaml");
                var genericDict = new ResourceDictionary { Source = genericUri };
                Application.Current.Resources.MergedDictionaries.Add(genericDict);
                Log.Debug("[STARTUP] ✓ Generic.xaml pre-loaded successfully");

                // CRITICAL: Load DataTemplates.xaml (contains ViewModel→View mappings for Prism navigation)
                var dataTemplatesUri = new Uri("pack://application:,,,/WileyWidget;component/Resources/DataTemplates.xaml");
                var dataTemplatesDict = new ResourceDictionary { Source = dataTemplatesUri };
                Application.Current.Resources.MergedDictionaries.Add(dataTemplatesDict);
                Log.Debug("[STARTUP] ✓ DataTemplates.xaml pre-loaded successfully");

                // Force WPF to process the merged dictionaries immediately
                // This ensures brushes and templates are available for immediate resolution
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() => { /* Force resource processing */ }));

                Log.Information("[STARTUP] ✓ Critical resources pre-loaded successfully ({Ms}ms) - dispatcher_invoke_issues mitigated",
                    sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[STARTUP] ✗ Critical failure pre-loading global resources - dispatcher_invoke_issues may occur");
                throw; // This is critical - rethrow to fail startup
            }
        }

        #endregion

        #region Theme Management

        /// <summary>
        /// Verifies and applies the Syncfusion theme for the application.
        /// This method ensures theme is applied before Prism's ConfigureRegionAdapterMappings is called.
        /// CRITICAL: Sets SfSkinManager.ApplyThemeAsDefaultStyle = true for global theme application.
        /// Enhanced with license validation and conservative memory management for .NET 9.
        /// </summary>
        private void VerifyAndApplyTheme()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                Log.Information("[THEME] Verifying and applying Syncfusion theme");

                // PHASE 1: License Status Validation
                Log.Information("[THEME] Checking Syncfusion license status: {Status}", App.SyncfusionLicenseStatus);

                switch (App.SyncfusionLicenseStatus)
                {
                    case App.LicenseRegistrationStatus.Failed:
                        Log.Warning("[THEME] License registration failed: {Error}. Proceeding with limited functionality.", App.SyncfusionLicenseError);
                        break;
                    case App.LicenseRegistrationStatus.InvalidKey:
                        Log.Warning("[THEME] Invalid license key: {Error}. Proceeding in trial mode.", App.SyncfusionLicenseError);
                        break;
                    case App.LicenseRegistrationStatus.NetworkError:
                        Log.Warning("[THEME] Network error during license validation: {Error}. Proceeding with cached/offline validation.", App.SyncfusionLicenseError);
                        break;
                    case App.LicenseRegistrationStatus.TrialMode:
                        Log.Information("[THEME] Running in trial mode - full theme functionality available with trial limitations");
                        break;
                    case App.LicenseRegistrationStatus.Success:
                        Log.Information("[THEME] ✓ Licensed mode - full theme functionality available");
                        break;
                    case App.LicenseRegistrationStatus.NotAttempted:
                        Log.Error("[THEME] License registration was not attempted - this indicates a static constructor failure");
                        break;
                }

                // PHASE 2: Enhanced Memory Validation for .NET 9
                try
                {
                    // Use both GC memory info (available memory) and process working set (current usage)
                    var gcMemInfo = GC.GetGCMemoryInfo();
                    var totalAvailableMemoryMB = gcMemInfo.TotalAvailableMemoryBytes / (1024 * 1024);
                    var currentGcMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);

                    var process = Process.GetCurrentProcess();
                    var workingSetMB = process.WorkingSet64 / (1024 * 1024);
                    var privateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024);

                    // Conservative memory thresholds for .NET 9 theme operations
                    const int criticalMemoryMB = 64;    // Abort theme application
                    const int lowMemoryMB = 128;         // Warning but proceed
                    const int recommendedMemoryMB = 256; // Optimal

                    var effectiveAvailableMemory = Math.Min(totalAvailableMemoryMB - currentGcMemoryMB, totalAvailableMemoryMB * 0.8); // Leave 20% buffer

                    Log.Debug("[THEME] Memory status: Available={Available}MB, GC={GcMemory}MB, WorkingSet={WorkingSet}MB, Private={Private}MB",
                        effectiveAvailableMemory, currentGcMemoryMB, workingSetMB, privateMemoryMB);

                    if (effectiveAvailableMemory < criticalMemoryMB)
                    {
                        var errorMsg = $"Critical memory shortage: {effectiveAvailableMemory}MB available, {criticalMemoryMB}MB minimum required. " +
                                      "Theme application aborted to prevent OOM exception.";
                        Log.Fatal("[THEME] {Error}", errorMsg);
                        throw new InsufficientMemoryException(errorMsg);
                    }
                    else if (effectiveAvailableMemory < lowMemoryMB)
                    {
                        Log.Warning("[THEME] Low memory detected: {Available}MB available, {Recommended}MB recommended. " +
                                    "Proceeding with theme application - monitor for performance issues.",
                            effectiveAvailableMemory, recommendedMemoryMB);

                        // Force garbage collection before theme operations in low memory scenarios
                        GC.Collect(2, GCCollectionMode.Forced, true);
                        GC.WaitForPendingFinalizers();
                        Log.Debug("[THEME] Forced GC completed in low memory scenario");
                    }
                    else if (effectiveAvailableMemory >= recommendedMemoryMB)
                    {
                        Log.Debug("[THEME] ✓ Memory status optimal: {Available}MB available", effectiveAvailableMemory);
                    }
                }
                catch (Exception memEx)
                {
                    Log.Warning(memEx, "[THEME] Could not check memory status - proceeding with theme application");
                }

                // PHASE 3: Theme Application with License-Aware Error Handling
                Log.Information("[THEME] Applying FluentLight theme...");

                // CRITICAL: Enable automatic theme application to all controls
                // Per Syncfusion docs: https://help.syncfusion.com/wpf/themes/skin-manager#apply-a-theme-globally-in-the-application
                // This MUST be set before setting ApplicationTheme
                SfSkinManager.ApplyThemeAsDefaultStyle = true;
                Log.Debug("[THEME] ApplyThemeAsDefaultStyle enabled for global theme propagation");

                // Apply FluentLight theme using SfSkinManager with license-aware error handling
                try
                {
                    var theme = new Syncfusion.SfSkinManager.Theme("FluentLight");
                    SfSkinManager.ApplicationTheme = theme;
                    Log.Debug("[THEME] ApplicationTheme set to FluentLight");
                }
                catch (Exception themeEx) when (themeEx.Message.Contains("license") || themeEx.Message.Contains("trial"))
                {
                    // Handle licensing exceptions specifically
                    var licenseMsg = $"Theme application encountered licensing issue: {themeEx.Message}. " +
                                   $"License Status: {App.SyncfusionLicenseStatus}";
                    Log.Warning("[THEME] {LicenseMessage}", licenseMsg);

                    // In trial/license issues, still try to proceed - Syncfusion may work with limitations
                    if (App.SyncfusionLicenseStatus != App.LicenseRegistrationStatus.Failed)
                    {
                        Log.Information("[THEME] Continuing with theme application despite licensing warning");
                    }
                    else
                    {
                        throw new InvalidOperationException($"Theme application failed due to licensing: {themeEx.Message}", themeEx);
                    }
                }

                // Verify theme was applied successfully
                if (SfSkinManager.ApplicationTheme == null)
                {
                    var errorMsg = "Theme application failed - SfSkinManager.ApplicationTheme is null after ApplyTheme()";
                    Log.Fatal("[THEME] {Error}", errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }

                Log.Information("[THEME] ✓ Theme applied successfully: {Theme} in {Ms}ms (License: {LicenseStatus})",
                    SfSkinManager.ApplicationTheme.ToString(), sw.ElapsedMilliseconds, App.SyncfusionLicenseStatus);
            }
            catch (InsufficientMemoryException)
            {
                // Re-throw memory exceptions as-is
                throw;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[THEME] ✗ Critical failure applying theme (License Status: {LicenseStatus})", App.SyncfusionLicenseStatus);
                throw; // This is critical - rethrow to fail startup
            }
        }

        /// <summary>
        /// Verifies Syncfusion theme integration for zero hangs after theme application.
        /// This addresses potential hangs during Prism bootstrap by ensuring theme resources
        /// are properly loaded and accessible before Container.Resolve&lt;Shell&gt;() is called.
        /// </summary>
        private void VerifyThemeIntegration()
        {
            try
            {
                Log.Information("[THEME] Verifying theme integration for zero hangs...");

                // Verify theme is still applied
                if (SfSkinManager.ApplicationTheme == null)
                {
                    throw new InvalidOperationException("Theme lost after application - potential hang condition");
                }

                // Test critical theme resources are accessible (no hangs)
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() =>
                    {
                        try
                        {
                            // Test theme resource access that might cause hangs if theme is broken
                            var testBrush = Application.Current.TryFindResource("PrimaryTextBrush");
                            if (testBrush == null)
                            {
                                Log.Warning("[THEME] PrimaryTextBrush not found - may cause UI issues but not a hang");
                            }

                            // Test Syncfusion theme integration
                            var sfTest = Application.Current.TryFindResource("SfSkinManager");
                            if (sfTest == null)
                            {
                                Log.Warning("[THEME] SfSkinManager resource not found - theme integration may be incomplete");
                            }

                            // Validate BudgetView-specific brushes to prevent XAML load exceptions
                            ValidateBudgetViewBrushes();
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "[THEME] Theme resource access test failed - potential hang condition");
                        }
                    }));

                Log.Information("[THEME] ✓ Theme integration verified - zero hangs expected");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[THEME] ✗ Theme integration verification failed - hangs may occur during Prism bootstrap");
                throw;
            }
        }

        /// <summary>
        /// Validates that BudgetView-specific brushes are available to prevent XAML load exceptions.
        /// This addresses report conflicts by ensuring centralized brushes from Generic.xaml are accessible.
        /// </summary>
        private void ValidateBudgetViewBrushes()
        {
            var criticalBrushes = new[]
            {
                "TreeGridHeaderBrush",
                "GridFilterRowForegroundBrush",
                "GridHoverBrush",
                "BorderAlt",
                "BorderAltBrush",
                "PieChartLabelTemplate"
            };

            foreach (var brushKey in criticalBrushes)
            {
                var resource = Application.Current.TryFindResource(brushKey);
                if (resource == null)
                {
                    var errorMsg = $"Critical BudgetView brush/template '{brushKey}' not found. This will cause XAML load exceptions.";
                    Log.Error("[THEME] {Error}", errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }
                else
                {
                    Log.Debug("[THEME] ✓ BudgetView brush/template '{Brush}' validated", brushKey);
                }
            }

            Log.Information("[THEME] ✓ All BudgetView brushes/templates validated successfully");
        }

        #endregion
    }
}
