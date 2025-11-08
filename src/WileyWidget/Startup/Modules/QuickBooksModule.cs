using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Events;
using Serilog;
using WileyWidget.Services;
using WileyWidget.Services.Events;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Prism module responsible for QuickBooks Online initialization.
    /// Replaces QuickBooksStartupTask in the new bootstrapper-based architecture.
    ///
    /// Registers IQuickBooksService as singleton (real implementation with QBO API) in RegisterTypes().
    /// On module initialization, publishes QuickBooksServiceReadyEvent to trigger lazy swap.
    /// This allows LazyQuickBooksService to swap from stub to real implementation without re-resolving.
    ///
    /// Pattern based on Prism-Samples-Wpf EventAggregator and module communication patterns:
    /// https://github.com/PrismLibrary/Prism-Samples-Wpf
    /// </summary>
    [Module(ModuleName = "QuickBooksModule")]
    public class QuickBooksModule : IModule
    {
        private IContainerProvider? _containerProvider;

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register real QuickBooksService implementation with QBO API
            containerRegistry.RegisterSingleton<IQuickBooksService, QuickBooksService>();

            Log.Information("QuickBooksModule.RegisterTypes() - Registered real QuickBooksService as singleton");
            Log.Debug("QuickBooksServiceReadyEvent will be published in OnInitialized()");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            _containerProvider = containerProvider;
            Log.Information("Initializing QuickBooksModule");

            // Publish ModuleLoadedEvent to signal module is loading
            try
            {
                var eventAggregator = containerProvider.Resolve<IEventAggregator>();
                eventAggregator.GetEvent<ModuleLoadedEvent>().Publish(new ModuleLoadedEventPayload
                {
                    ModuleName = "QuickBooksModule",
                    ModuleInstance = this,
                    Timestamp = DateTime.UtcNow
                });
                Log.Debug("Published ModuleLoadedEvent for QuickBooksModule");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to publish ModuleLoadedEvent");
            }

            // Trigger lazy swap via event - publish the resolved QuickBooksService
            try
            {
                var eventAggregator = containerProvider.Resolve<IEventAggregator>();
                var realQuickBooksService = containerProvider.Resolve<IQuickBooksService>();

                // Publish the service ready event for LazyQuickBooksService to swap
                eventAggregator.GetEvent<QuickBooksServiceReadyEvent>().Publish(realQuickBooksService);

                Log.Information("✓ Published QuickBooksServiceReadyEvent with resolved IQuickBooksService");
                Log.Debug("LazyQuickBooksService should now swap to real implementation");

                // Publish QuickBooksModuleLoadedEvent as alternate trigger
                eventAggregator.GetEvent<QuickBooksModuleLoadedEvent>().Publish(this);
                Log.Debug("✓ Published QuickBooksModuleLoadedEvent");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to resolve and publish QuickBooksService - LazyQuickBooksService will remain as stub");
            }

            try
            {
                // QuickBooks services are optional - may not be registered yet
                IQuickBooksService quickBooksService = null;
                ISecretVaultService secretVaultService = null;

                try
                {
                    quickBooksService = containerProvider.Resolve<IQuickBooksService>();
                }
                catch
                {
                    Log.Warning("IQuickBooksService not registered - skipping QuickBooks initialization");
                    return;
                }

                try
                {
                    secretVaultService = containerProvider.Resolve<ISecretVaultService>();
                }
                catch
                {
                    Log.Debug("ISecretVaultService not available");
                }

                Log.Information("Starting QuickBooks Online service initialization");

                // Test secret vault connectivity for QBO secrets
                if (secretVaultService != null)
                {
                    try
                    {
                        var svTestResult = secretVaultService.TestConnectionAsync().GetAwaiter().GetResult();
                        if (svTestResult)
                        {
                            Log.Information("Secret vault connection verified for QBO secrets");
                        }
                        else
                        {
                            Log.Warning("Secret vault not available - QBO secrets will be loaded from environment variables");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to test secret vault connection");
                    }
                }

                // Test basic QBO connectivity (lightweight test)
                try
                {
                    var connectionTest = quickBooksService.TestConnectionAsync().GetAwaiter().GetResult();
                    if (connectionTest)
                    {
                        Log.Information("QuickBooks Online API connection test successful");
                    }
                    else
                    {
                        Log.Warning("QuickBooks Online API connection test failed - may require user authentication");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "QuickBooks Online connection test failed - service may not be fully configured yet");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize QuickBooks module");
            }

            // Seed QuickBooks secrets programmatically from environment variables so setup can proceed without the Settings UI
            try
            {
                ISecretVaultService secretVaultService = null;
                try
                {
                    secretVaultService = containerProvider.Resolve<ISecretVaultService>();
                }
                catch
                {
                    Log.Debug("ISecretVaultService not available");
                }

                if (secretVaultService != null)
                {
                    SeedQuickBooksSecretsFromEnvironmentAsync(secretVaultService).GetAwaiter().GetResult();
                }
                else
                {
                    Log.Warning("Secret vault service not available; cannot seed QuickBooks secrets programmatically.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to seed QuickBooks secrets from environment");
            }

            Log.Information("QuickBooksModule initialization completed");
        }

        /// <summary>
        /// Programmatically installs QuickBooks settings into the encrypted secret vault from environment variables.
        /// It writes values only when the corresponding secret is missing or empty.
        /// Supported env vars (checked in order):
        ///   QBO_CLIENT_ID | QUICKBOOKS_CLIENT_ID
        ///   QBO_CLIENT_SECRET | QUICKBOOKS_CLIENT_SECRET
        ///   QBO_REDIRECT_URI | QUICKBOOKS_REDIRECT_URI
        ///   QBO_ENVIRONMENT | QUICKBOOKS_ENVIRONMENT
        /// Secrets written (for compatibility with existing loaders):
        ///   QuickBooks-ClientId, QBO-CLIENT-ID
        ///   QuickBooks-ClientSecret, QBO-CLIENT-SECRET
        ///   QuickBooks-RedirectUri, QBO-REDIRECT-URI
        ///   QuickBooks-Environment, QBO-ENVIRONMENT
        /// </summary>
        private static async System.Threading.Tasks.Task SeedQuickBooksSecretsFromEnvironmentAsync(WileyWidget.Services.ISecretVaultService secretVault)
        {
            static string? ReadEnv(string name)
            {
                // Prefer User scope, then Process scope
                return System.Environment.GetEnvironmentVariable(name, System.EnvironmentVariableTarget.User)
                       ?? System.Environment.GetEnvironmentVariable(name, System.EnvironmentVariableTarget.Process)
                       ?? System.Environment.GetEnvironmentVariable(name);
            }

            async System.Threading.Tasks.Task EnsureSecretAsync(string primaryKey, string altKey, string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;

                var existing = await secretVault.GetSecretAsync(primaryKey).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(existing))
                {
                    await secretVault.SetSecretAsync(primaryKey, value!).ConfigureAwait(false);
                    Log.Information("Seeded secret '{SecretKey}' from environment.", primaryKey);
                }

                // Also write alternate key for broader compatibility if it doesn't exist
                if (!string.IsNullOrWhiteSpace(altKey))
                {
                    var existingAlt = await secretVault.GetSecretAsync(altKey).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(existingAlt))
                    {
                        await secretVault.SetSecretAsync(altKey, value!).ConfigureAwait(false);
                        Log.Information("Seeded secret '{SecretKey}' from environment.", altKey);
                    }
                }
            }

            // Read env vars (multiple aliases supported)
            var clientId = ReadEnv("QBO_CLIENT_ID") ?? ReadEnv("QUICKBOOKS_CLIENT_ID");
            var clientSecret = ReadEnv("QBO_CLIENT_SECRET") ?? ReadEnv("QUICKBOOKS_CLIENT_SECRET");
            var redirectUri = ReadEnv("QBO_REDIRECT_URI") ?? ReadEnv("QUICKBOOKS_REDIRECT_URI") ?? "http://localhost:8080/callback";
            var environment = ReadEnv("QBO_ENVIRONMENT") ?? ReadEnv("QUICKBOOKS_ENVIRONMENT") ?? "Sandbox";

            // Persist into vault (both canonical and alternate keys)
            await EnsureSecretAsync("QuickBooks-ClientId", "QBO-CLIENT-ID", clientId);
            await EnsureSecretAsync("QuickBooks-ClientSecret", "QBO-CLIENT-SECRET", clientSecret);
            await EnsureSecretAsync("QuickBooks-RedirectUri", "QBO-REDIRECT-URI", redirectUri);
            await EnsureSecretAsync("QuickBooks-Environment", "QBO-ENVIRONMENT", environment);
        }
    }
}
