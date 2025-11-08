using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Prism;
using Prism.Navigation;
using Prism.Navigation.Regions;
using Serilog;
using WileyWidget.Services;

namespace WileyWidget.Regions
{
    /// <summary>
    /// Region behavior that automatically logs navigation events for debugging and monitoring.
    /// </summary>
    public class NavigationLoggingBehavior : RegionBehavior
    {
        private readonly ILogger<NavigationLoggingBehavior> _logger;
        private readonly IPrismErrorHandler _errorHandler;

        public const string BehaviorKey = "NavigationLogging";

        public NavigationLoggingBehavior(ILogger<NavigationLoggingBehavior> logger, IPrismErrorHandler errorHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        }

        protected override void OnAttach()
        {
            Region.NavigationService.Navigated += OnNavigated;
            Region.NavigationService.Navigating += OnNavigating;
            Region.NavigationService.NavigationFailed += OnNavigationFailed;

            _logger.LogDebug("Attached navigation logging behavior to region: {RegionName}", Region.Name);
        }

        private void OnNavigating(object? sender, RegionNavigationEventArgs e)
        {
            _logger.LogInformation("Navigating to {ViewType} in region {RegionName}",
                e.Uri?.ToString() ?? "Unknown", Region.Name);
        }

        private void OnNavigated(object? sender, RegionNavigationEventArgs e)
        {
            var view = e.NavigationContext.NavigationService.Region.ActiveViews.FirstOrDefault();
            _logger.LogInformation("Successfully navigated to {ViewType} in region {RegionName}",
                view?.GetType().Name ?? "Unknown", Region.Name);
        }

        private void OnNavigationFailed(object? sender, RegionNavigationFailedEventArgs e)
        {
            // Extract navigation target information
            var uri = e.NavigationContext?.Uri?.ToString() ?? "Unknown";

            // Use the centralized error handler for consistent error handling and event publishing
            _errorHandler.HandleNavigationError(
                Region.Name,
                uri,
                e.Error,
                e.Error?.Message ?? "Unknown navigation error");
        }
    }

    /// <summary>
    /// Region behavior that automatically saves data when navigating away from views.
    /// </summary>
    public class AutoSaveBehavior : RegionBehavior
    {
        private readonly ILogger<AutoSaveBehavior> _logger;

        public const string BehaviorKey = "AutoSave";

        public AutoSaveBehavior(ILogger<AutoSaveBehavior> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override void OnAttach()
        {
            Region.NavigationService.Navigating += OnNavigating;
            _logger.LogDebug("Attached auto-save behavior to region: {RegionName}", Region.Name);
        }

        private async void OnNavigating(object? sender, RegionNavigationEventArgs e)
        {
            // Check if the current view implements ISaveable
            var currentView = Region.NavigationService.Region.ActiveViews.FirstOrDefault();
            if (currentView is ISaveable saveable)
            {
                try
                {
                    _logger.LogDebug("Auto-saving data for view {ViewType} in region {RegionName}",
                        currentView.GetType().Name, Region.Name);

                    await saveable.SaveAsync();
                    _logger.LogInformation("Auto-saved data for view {ViewType}", currentView.GetType().Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-save data for view {ViewType}", currentView.GetType().Name);
                    // Don't block navigation due to save failure
                }
            }
        }
    }

    /// <summary>
    /// Region behavior that provides navigation history and back/forward functionality.
    /// </summary>
    public class NavigationHistoryBehavior : RegionBehavior
    {
        private readonly ILogger<NavigationHistoryBehavior> _logger;
        private readonly List<NavigationHistoryEntry> _history = new();
        private int _currentIndex = -1;

        public const string BehaviorKey = "NavigationHistory";

        public NavigationHistoryBehavior(ILogger<NavigationHistoryBehavior> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override void OnAttach()
        {
            Region.NavigationService.Navigated += OnNavigated;
            _logger.LogDebug("Attached navigation history behavior to region: {RegionName}", Region.Name);
        }

        private void OnNavigated(object? sender, RegionNavigationEventArgs e)
        {
            var entry = new NavigationHistoryEntry(e.Uri, e.NavigationContext.Parameters);

            // Remove any forward history when new navigation occurs
            if (_currentIndex < _history.Count - 1)
            {
                _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);
            }

            _history.Add(entry);
            _currentIndex = _history.Count - 1;

            _logger.LogDebug("Added navigation entry to history for region {RegionName}: {Uri}",
                Region.Name, e.Uri?.ToString());
        }

        public bool CanGoBack => _currentIndex > 0;
        public bool CanGoForward => _currentIndex < _history.Count - 1;

        public void GoBack()
        {
            if (CanGoBack)
            {
                _currentIndex--;
                var entry = _history[_currentIndex];
                Region.NavigationService.RequestNavigate(entry.Uri, entry.Parameters);
                _logger.LogInformation("Navigated back in region {RegionName}", Region.Name);
            }
        }

        public void GoForward()
        {
            if (CanGoForward)
            {
                _currentIndex++;
                var entry = _history[_currentIndex];
                Region.NavigationService.RequestNavigate(entry.Uri, entry.Parameters);
                _logger.LogInformation("Navigated forward in region {RegionName}", Region.Name);
            }
        }

        public void ClearHistory()
        {
            _history.Clear();
            _currentIndex = -1;
            _logger.LogDebug("Cleared navigation history for region {RegionName}", Region.Name);
        }
    }

    /// <summary>
    /// Region behavior that automatically activates views when they are added to the region.
    /// </summary>
    public class AutoActivateBehavior : RegionBehavior
    {
        private readonly ILogger<AutoActivateBehavior> _logger;

        public const string BehaviorKey = "AutoActivate";

        public AutoActivateBehavior(ILogger<AutoActivateBehavior> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override void OnAttach()
        {
            Region.Views.CollectionChanged += OnViewsChanged;
            Region.ActiveViews.CollectionChanged += OnActiveViewsChanged;
            _logger.LogDebug("Attached auto-activate behavior to region: {RegionName}", Region.Name);
        }

        private void OnViewsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (var view in e.NewItems)
                {
                    if (view != null && !Region.ActiveViews.Contains(view))
                    {
                        Region.Activate(view);
                        _logger.LogDebug("Auto-activated view {ViewType} in region {RegionName}",
                            view.GetType().Name, Region.Name);
                    }
                }
            }
        }

        private void OnActiveViewsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Ensure only one view is active at a time (optional behavior)
            if (Region.ActiveViews.Count() > 1)
            {
                var viewsToDeactivate = Region.ActiveViews.Skip(1).ToList();
                foreach (var view in viewsToDeactivate)
                {
                    Region.Deactivate(view);
                    _logger.LogDebug("Deactivated additional view {ViewType} in region {RegionName} (single active view policy)",
                        view.GetType().Name, Region.Name);
                }
            }
        }
    }

    /// <summary>
    /// Region behavior that delays region creation and updates during startup to prevent duplicate registrations
    /// and improve performance. Logs all region update operations for debugging.
    /// </summary>
    public class DelayedRegionCreationBehavior : RegionBehavior
    {
        private readonly ILogger<DelayedRegionCreationBehavior> _logger;
        private bool _isSuspended = false;
        private readonly Queue<PendingRegionUpdate> _pendingUpdates = new();

        public const string BehaviorKey = "DelayedRegionCreation";

        public DelayedRegionCreationBehavior(ILogger<DelayedRegionCreationBehavior> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override void OnAttach()
        {
            Region.Views.CollectionChanged += OnViewsChanged;
            Region.ActiveViews.CollectionChanged += OnActiveViewsChanged;
            _logger.LogDebug("Attached delayed region creation behavior to region: {RegionName}", Region.Name);
        }

        private void OnViewsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_isSuspended)
            {
                _pendingUpdates.Enqueue(new PendingRegionUpdate(PendingUpdateType.ViewsChanged, e));
                _logger.LogDebug("Queued views changed update for region {RegionName} (suspended)", Region.Name);
                return;
            }

            OnUpdatingRegions("ViewsChanged", e);
        }

        private void OnActiveViewsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_isSuspended)
            {
                _pendingUpdates.Enqueue(new PendingRegionUpdate(PendingUpdateType.ActiveViewsChanged, e));
                _logger.LogDebug("Queued active views changed update for region {RegionName} (suspended)", Region.Name);
                return;
            }

            OnUpdatingRegions("ActiveViewsChanged", e);
        }

        /// <summary>
        /// Called when regions are being updated. Override in derived classes to customize behavior.
        /// </summary>
        protected virtual void OnUpdatingRegions(string updateType, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            _logger.LogInformation("Region {RegionName} updating ({UpdateType}): Action={Action}, NewItems={NewCount}, OldItems={OldCount}",
                Region.Name, updateType, e.Action, e.NewItems?.Count ?? 0, e.OldItems?.Count ?? 0);
        }

        /// <summary>
        /// Suspends region updates to allow batch operations.
        /// </summary>
        public void SuspendUpdates()
        {
            _isSuspended = true;
            _logger.LogDebug("Suspended region updates for region: {RegionName}", Region.Name);
        }

        /// <summary>
        /// Resumes region updates and processes any pending updates.
        /// </summary>
        public void ResumeUpdates()
        {
            _isSuspended = false;
            _logger.LogDebug("Resuming region updates for region: {RegionName}", Region.Name);

            // Process pending updates
            while (_pendingUpdates.Count > 0)
            {
                var update = _pendingUpdates.Dequeue();
                switch (update.UpdateType)
                {
                    case PendingUpdateType.ViewsChanged:
                        OnUpdatingRegions("ViewsChanged (deferred)", update.Args);
                        break;
                    case PendingUpdateType.ActiveViewsChanged:
                        OnUpdatingRegions("ActiveViewsChanged (deferred)", update.Args);
                        break;
                }
            }

            _logger.LogDebug("Processed {Count} pending region updates for region: {RegionName}",
                _pendingUpdates.Count, Region.Name);
        }

        /// <summary>
        /// Gets whether region updates are currently suspended.
        /// </summary>
        public bool IsSuspended => _isSuspended;

        /// <summary>
        /// Gets the number of pending updates.
        /// </summary>
        public int PendingUpdateCount => _pendingUpdates.Count;
    }

    /// <summary>
    /// Types of pending region updates.
    /// </summary>
    public enum PendingUpdateType
    {
        ViewsChanged,
        ActiveViewsChanged
    }

    /// <summary>
    /// Represents a pending region update.
    /// </summary>
    public class PendingRegionUpdate
    {
        public PendingUpdateType UpdateType { get; }
        public System.Collections.Specialized.NotifyCollectionChangedEventArgs Args { get; }

        public PendingRegionUpdate(PendingUpdateType updateType, System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
        {
            UpdateType = updateType;
            Args = args;
        }
    }

    /// <summary>
    /// Interface for views that support auto-saving.
    /// </summary>
    public interface ISaveable
    {
        Task SaveAsync();
        bool HasUnsavedChanges { get; }
    }

    /// <summary>
    /// Navigation history entry.
    /// </summary>
    public class NavigationHistoryEntry
    {
        public Uri Uri { get; }
        public INavigationParameters Parameters { get; }

        public NavigationHistoryEntry(Uri uri, INavigationParameters? parameters)
        {
            Uri = uri;
            Parameters = parameters ?? new NavigationParameters();
        }
    }
}
