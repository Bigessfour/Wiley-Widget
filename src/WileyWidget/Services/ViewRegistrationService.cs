using System;
using System.Collections.Generic;
using Prism.Regions;
using Serilog;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for managing view registration with Prism regions
    /// </summary>
    public class ViewRegistrationService
    {
        private readonly IRegionManager _regionManager;
        private readonly Dictionary<string, Type> _registeredViews;

        public ViewRegistrationService(IRegionManager regionManager)
        {
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _registeredViews = new Dictionary<string, Type>();
        }

        /// <summary>
        /// Registers a view type with a region
        /// </summary>
        public void RegisterViewWithRegion(string regionName, Type viewType)
        {
            if (string.IsNullOrEmpty(regionName))
                throw new ArgumentNullException(nameof(regionName));
            if (viewType == null)
                throw new ArgumentNullException(nameof(viewType));

            try
            {
                _regionManager.RegisterViewWithRegion(regionName, viewType);
                _registeredViews[regionName] = viewType;
                Log.Debug("Registered view {ViewType} with region {RegionName}", viewType.Name, regionName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register view {ViewType} with region {RegionName}", viewType.Name, regionName);
                throw;
            }
        }

        /// <summary>
        /// Gets the registered view type for a region
        /// </summary>
        public Type GetRegisteredView(string regionName)
        {
            return _registeredViews.TryGetValue(regionName, out var viewType) ? viewType : null;
        }

        /// <summary>
        /// Checks if a region has a registered view
        /// </summary>
        public bool IsRegionRegistered(string regionName)
        {
            return _registeredViews.ContainsKey(regionName);
        }
    }
}