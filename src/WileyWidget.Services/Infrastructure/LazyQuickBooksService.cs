#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Intuit.Ipp.Data;
using Microsoft.Extensions.Logging;
using Prism.Events;
using Serilog;
using WileyWidget.Services.Events;
using QBTask = Intuit.Ipp.Data.Task;

namespace WileyWidget.Services.Infrastructure
{
    /// <summary>
    /// Lazy-loading stub implementation of IQuickBooksService that defers to the real service once QuickBooksModule loads.
    /// Prevents constructor dependency resolution failures in ViewModels (like SettingsViewModel) that depend on IQuickBooksService
    /// but are created before QuickBooksModule initializes.
    ///
    /// Pattern based on Prism-Samples-Wpf EventAggregator and Lazy&lt;T&gt; patterns:
    /// - Registered as singleton in App.RegisterTypes() (before module catalog configuration)
    /// - Subscribes to ModuleLoadedEvent to detect when QuickBooksModule loads
    /// - Swaps internal reference to real QuickBooksService on module load
    /// - Provides no-op/stub implementations until real service is available
    ///
    /// Reference: https://github.com/PrismLibrary/Prism-Samples-Wpf/tree/master/10-CustomPopupDialogs
    /// </summary>
    public class LazyQuickBooksService : IQuickBooksService
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger<LazyQuickBooksService> _logger;
        private IQuickBooksService? _realService;
        private readonly object _swapLock = new object();
        private bool _isSwapped = false;

        public LazyQuickBooksService(
            IEventAggregator eventAggregator,
            ILogger<LazyQuickBooksService> logger)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to module loaded event to detect when QuickBooksModule is ready
            _eventAggregator.GetEvent<ModuleLoadedEvent>().Subscribe(OnModuleLoaded, ThreadOption.PublisherThread);

            // Subscribe to service ready event (published by QuickBooksModule.OnInitialized)
            _eventAggregator.GetEvent<QuickBooksServiceReadyEvent>().Subscribe(OnServiceReady, ThreadOption.PublisherThread);

            Log.Information("LazyQuickBooksService: Stub instance created (will swap to real service on module load)");
            _logger.LogInformation("LazyQuickBooksService initialized as stub - awaiting QuickBooksModule");
        }

        /// <summary>
        /// Called when any module is loaded. Checks if it's QuickBooksModule.
        /// </summary>
        private void OnModuleLoaded(ModuleLoadedEventPayload payload)
        {
            if (payload.ModuleName == "QuickBooksModule")
            {
                Log.Information("LazyQuickBooksService: QuickBooksModule loaded - awaiting service registration");
                _logger.LogInformation("QuickBooksModule loaded, waiting for IQuickBooksService registration");
            }
        }

        /// <summary>
        /// Called when the real QuickBooksService is registered and ready.
        /// Swaps the internal reference to delegate all calls to the real implementation.
        /// </summary>
        private void OnServiceReady(IQuickBooksService realService)
        {
            lock (_swapLock)
            {
                if (_isSwapped)
                {
                    Log.Warning("LazyQuickBooksService: Service swap already completed - ignoring duplicate");
                    return;
                }

                _realService = realService ?? throw new ArgumentNullException(nameof(realService));
                _isSwapped = true;

                Log.Information("✓ LazyQuickBooksService: Swapped to real QuickBooksService implementation");
                _logger.LogInformation("LazyQuickBooksService successfully swapped to real implementation");
            }
        }

        // ========================
        // IQuickBooksService Implementation
        // All methods delegate to real service if available, otherwise return stub/default values
        // ========================

        public async Task<bool> AuthorizeAsync()
        {
            if (_realService != null)
            {
                return await _realService.AuthorizeAsync();
            }

            Log.Debug("LazyQuickBooksService.AuthorizeAsync() called before module load - returning false (stub)");
            _logger.LogDebug("AuthorizeAsync called on stub - QuickBooksModule not yet loaded");
            return await System.Threading.Tasks.Task.FromResult(false);
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (_realService != null)
            {
                return await _realService.TestConnectionAsync();
            }

            Log.Debug("LazyQuickBooksService.TestConnectionAsync() called before module load - returning false (stub)");
            _logger.LogDebug("TestConnectionAsync called on stub - QuickBooksModule not yet loaded");
            return await System.Threading.Tasks.Task.FromResult(false);
        }

        public async Task<UrlAclCheckResult> CheckUrlAclAsync(string? redirectUri = null)
        {
            if (_realService != null)
            {
                return await _realService.CheckUrlAclAsync(redirectUri);
            }

            Log.Debug("LazyQuickBooksService.CheckUrlAclAsync() called before module load - returning not ready (stub)");
            _logger.LogDebug("CheckUrlAclAsync called on stub - returning placeholder result");

            return await System.Threading.Tasks.Task.FromResult(new UrlAclCheckResult
            {
                IsReady = false,
                ListenerPrefix = redirectUri ?? "http://localhost:8080/",
                Guidance = "QuickBooks module not yet loaded. Please wait for application startup to complete.",
                RawNetshOutput = null
            });
        }

        public async Task<List<Customer>> GetCustomersAsync()
        {
            if (_realService != null)
            {
                return await _realService.GetCustomersAsync();
            }

            Log.Debug("LazyQuickBooksService.GetCustomersAsync() called before module load - returning empty list (stub)");
            _logger.LogDebug("GetCustomersAsync called on stub - returning empty collection");
            return await System.Threading.Tasks.Task.FromResult(new List<Customer>());
        }

        public async Task<List<Invoice>> GetInvoicesAsync(string? enterprise = null)
        {
            if (_realService != null)
            {
                return await _realService.GetInvoicesAsync(enterprise);
            }

            Log.Debug("LazyQuickBooksService.GetInvoicesAsync() called before module load - returning empty list (stub)");
            _logger.LogDebug("GetInvoicesAsync called on stub - returning empty collection");
            return await System.Threading.Tasks.Task.FromResult(new List<Invoice>());
        }

        public async Task<List<Account>> GetChartOfAccountsAsync()
        {
            if (_realService != null)
            {
                return await _realService.GetChartOfAccountsAsync();
            }

            Log.Debug("LazyQuickBooksService.GetChartOfAccountsAsync() called before module load - returning empty list (stub)");
            _logger.LogDebug("GetChartOfAccountsAsync called on stub - returning empty collection");
            return await System.Threading.Tasks.Task.FromResult(new List<Account>());
        }

        public async Task<List<JournalEntry>> GetJournalEntriesAsync(DateTime startDate, DateTime endDate)
        {
            if (_realService != null)
            {
                return await _realService.GetJournalEntriesAsync(startDate, endDate);
            }

            Log.Debug("LazyQuickBooksService.GetJournalEntriesAsync() called before module load - returning empty list (stub)");
            _logger.LogDebug("GetJournalEntriesAsync called on stub - returning empty collection");
            return await System.Threading.Tasks.Task.FromResult(new List<JournalEntry>());
        }

        public async Task<List<Budget>> GetBudgetsAsync()
        {
            if (_realService != null)
            {
                return await _realService.GetBudgetsAsync();
            }

            Log.Debug("LazyQuickBooksService.GetBudgetsAsync() called before module load - returning empty list (stub)");
            _logger.LogDebug("GetBudgetsAsync called on stub - returning empty collection");
            return await System.Threading.Tasks.Task.FromResult(new List<Budget>());
        }

        public async Task<SyncResult> SyncBudgetsToAppAsync(IEnumerable<Budget> budgets, CancellationToken cancellationToken = default)
        {
            if (_realService != null)
            {
                return await _realService.SyncBudgetsToAppAsync(budgets, cancellationToken);
            }

            Log.Debug("LazyQuickBooksService.SyncBudgetsToAppAsync() called before module load - returning failure (stub)");
            _logger.LogDebug("SyncBudgetsToAppAsync called on stub - returning error result");

            return await System.Threading.Tasks.Task.FromResult(new SyncResult
            {
                Success = false,
                RecordsSynced = 0,
                ErrorMessage = "QuickBooks module not yet loaded. Please wait for application startup to complete.",
                Duration = TimeSpan.Zero
            });
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_realService != null)
            {
                return await _realService.ConnectAsync(cancellationToken);
            }

            Log.Debug("LazyQuickBooksService.ConnectAsync() called before module load - returning false (stub)");
            _logger.LogDebug("ConnectAsync called on stub - QuickBooksModule not yet loaded");
            return await System.Threading.Tasks.Task.FromResult(false);
        }

        public async Task<bool> IsConnectedAsync()
        {
            if (_realService != null)
            {
                return await _realService.IsConnectedAsync();
            }

            Log.Debug("LazyQuickBooksService.IsConnectedAsync() called before module load - returning false (stub)");
            _logger.LogDebug("IsConnectedAsync called on stub - QuickBooksModule not yet loaded");
            return await System.Threading.Tasks.Task.FromResult(false);
        }

        public async System.Threading.Tasks.Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_realService != null)
            {
                await _realService.DisconnectAsync(cancellationToken);
                return;
            }

            Log.Debug("LazyQuickBooksService.DisconnectAsync() called before module load - no-op (stub)");
            _logger.LogDebug("DisconnectAsync called on stub - no-op");
            await System.Threading.Tasks.Task.CompletedTask;
        }
        public async Task<ConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken = default)
        {
            if (_realService != null)
            {
                return await _realService.GetConnectionStatusAsync(cancellationToken);
            }

            Log.Debug("LazyQuickBooksService.GetConnectionStatusAsync() called before module load - returning disconnected (stub)");
            _logger.LogDebug("GetConnectionStatusAsync called on stub - returning placeholder status");

            return await System.Threading.Tasks.Task.FromResult(new ConnectionStatus
            {
                IsConnected = false,
                CompanyName = null,
                LastSyncTime = null,
                StatusMessage = "QuickBooks module not yet loaded. Please wait for application startup to complete."
            });
        }

        public async Task<ImportResult> ImportChartOfAccountsAsync(CancellationToken cancellationToken = default)
        {
            if (_realService != null)
            {
                return await _realService.ImportChartOfAccountsAsync(cancellationToken);
            }

            Log.Debug("LazyQuickBooksService.ImportChartOfAccountsAsync() called before module load - returning failure (stub)");
            _logger.LogDebug("ImportChartOfAccountsAsync called on stub - returning error result");

            return await System.Threading.Tasks.Task.FromResult(new ImportResult
            {
                Success = false,
                AccountsImported = 0,
                AccountsUpdated = 0,
                AccountsSkipped = 0,
                ErrorMessage = "QuickBooks module not yet loaded. Please wait for application startup to complete.",
                Duration = TimeSpan.Zero,
                ValidationErrors = new List<string> { "Service not initialized" }
            });
        }

        public async Task<SyncResult> SyncDataAsync(CancellationToken cancellationToken = default)
        {
            if (_realService != null)
            {
                return await _realService.SyncDataAsync(cancellationToken);
            }

            Log.Debug("LazyQuickBooksService.SyncDataAsync() called before module load - returning failure (stub)");
            _logger.LogDebug("SyncDataAsync called on stub - returning error result");

            return await System.Threading.Tasks.Task.FromResult(new SyncResult
            {
                Success = false,
                RecordsSynced = 0,
                ErrorMessage = "QuickBooks module not yet loaded. Please wait for application startup to complete.",
                Duration = TimeSpan.Zero
            });
        }
    }
}
