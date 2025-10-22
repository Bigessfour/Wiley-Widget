using System;
using System.Collections.Generic;
using Prism;
using Serilog;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for managing scoped regions that provide isolated navigation contexts.
    /// Scoped regions allow different parts of the application to maintain separate navigation state.
    /// </summary>
    public class ScopedRegionService : IScopedRegionService
    {
        private readonly IRegionManager _regionManager;
        private readonly ILogger<ScopedRegionService> _logger;
        private readonly Dictionary<string, IRegionManager> _scopedManagers = new();

        public ScopedRegionService(IRegionManager regionManager, ILogger<ScopedRegionService> logger)
        {
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new scoped region manager for the specified scope.
        /// </summary>
        public IRegionManager CreateScopedRegionManager(string scopeName)
        {
            if (string.IsNullOrEmpty(scopeName))
                throw new ArgumentNullException(nameof(scopeName));

            if (_scopedManagers.ContainsKey(scopeName))
            {
                _logger.LogWarning("Scoped region manager already exists for scope: {ScopeName}", scopeName);
                return _scopedManagers[scopeName];
            }

            var scopedManager = _regionManager.CreateRegionManager();
            _scopedManagers[scopeName] = scopedManager;

            _logger.LogInformation("Created scoped region manager for scope: {ScopeName}", scopeName);
            return scopedManager;
        }

        /// <summary>
        /// Gets the scoped region manager for the specified scope.
        /// </summary>
        public IRegionManager? GetScopedRegionManager(string scopeName)
        {
            if (string.IsNullOrEmpty(scopeName))
                return null;

            return _scopedManagers.TryGetValue(scopeName, out var manager) ? manager : null;
        }

        /// <summary>
        /// Removes and disposes the scoped region manager for the specified scope.
        /// </summary>
        public void RemoveScopedRegionManager(string scopeName)
        {
            if (string.IsNullOrEmpty(scopeName))
                return;

            if (_scopedManagers.TryGetValue(scopeName, out var manager))
            {
                // Dispose of the scoped manager if it implements IDisposable
                if (manager is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _scopedManagers.Remove(scopeName);
                _logger.LogInformation("Removed scoped region manager for scope: {ScopeName}", scopeName);
            }
        }

        /// <summary>
        /// Navigates within a scoped region.
        /// </summary>
        public void NavigateInScope(string scopeName, string regionName, string viewName, NavigationParameters? parameters = null)
        {
            var scopedManager = GetScopedRegionManager(scopeName);
            if (scopedManager == null)
            {
                _logger.LogWarning("Cannot navigate in scope {ScopeName} - scope not found", scopeName);
                return;
            }

            scopedManager.RequestNavigate(regionName, viewName, parameters);
            _logger.LogDebug("Navigated to {ViewName} in region {RegionName} within scope {ScopeName}",
                viewName, regionName, scopeName);
        }

        /// <summary>
        /// Gets all regions within a specific scope.
        /// </summary>
        public IEnumerable<IRegion> GetRegionsInScope(string scopeName)
        {
            var scopedManager = GetScopedRegionManager(scopeName);
            return scopedManager?.Regions ?? Enumerable.Empty<IRegion>();
        }

        /// <summary>
        /// Gets all active scope names.
        /// </summary>
        public IEnumerable<string> GetActiveScopes()
        {
            return _scopedManagers.Keys;
        }

        /// <summary>
        /// Clears all scoped region managers.
        /// </summary>
        public void ClearAllScopes()
        {
            foreach (var scopeName in _scopedManagers.Keys.ToList())
            {
                RemoveScopedRegionManager(scopeName);
            }

            _logger.LogInformation("Cleared all scoped region managers");
        }

        /// <summary>
        /// Creates a workspace scope for isolated document management.
        /// </summary>
        public IRegionManager CreateWorkspaceScope(string workspaceId)
        {
            var scopeName = $"Workspace_{workspaceId}";
            return CreateScopedRegionManager(scopeName);
        }

        /// <summary>
        /// Creates a dialog scope for modal dialog management.
        /// </summary>
        public IRegionManager CreateDialogScope(string dialogId)
        {
            var scopeName = $"Dialog_{dialogId}";
            return CreateScopedRegionManager(scopeName);
        }

        /// <summary>
        /// Creates a tab scope for tabbed interface management.
        /// </summary>
        public IRegionManager CreateTabScope(string tabId)
        {
            var scopeName = $"Tab_{tabId}";
            return CreateScopedRegionManager(scopeName);
        }
    }

    /// <summary>
    /// Interface for the scoped region service.
    /// </summary>
    public interface IScopedRegionService
    {
        IRegionManager CreateScopedRegionManager(string scopeName);
        IRegionManager? GetScopedRegionManager(string scopeName);
        void RemoveScopedRegionManager(string scopeName);
        void NavigateInScope(string scopeName, string regionName, string viewName, NavigationParameters? parameters = null);
        IEnumerable<IRegion> GetRegionsInScope(string scopeName);
        IEnumerable<string> GetActiveScopes();
        void ClearAllScopes();
        IRegionManager CreateWorkspaceScope(string workspaceId);
        IRegionManager CreateDialogScope(string dialogId);
        IRegionManager CreateTabScope(string tabId);
    }
}
