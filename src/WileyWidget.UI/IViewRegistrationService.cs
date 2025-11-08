using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Serilog;
using WileyWidget.Views;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implementation of the view registration service
    /// </summary>
    public class ViewRegistrationService : IViewRegistrationService
    {
        // private readonly IRegionManager _regionManager;
        private readonly Dictionary<string, List<Type>> _registeredViews;

        public ViewRegistrationService() // IRegionManager regionManager
        {
            // _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _registeredViews = new Dictionary<string, List<Type>>();
        }

        [Obsolete("RegisterAllViews is deprecated. Register views in Prism modules instead.")]
        public void RegisterAllViews()
        {
            Log.Information("ViewRegistrationService: RegisterAllViews() is deprecated");
            Log.Information("View registration is now handled by Prism modules");

            // This method is kept for backward compatibility but does nothing
            // All view registration should be done in Prism modules:
            // - DashboardModule registers DashboardView
            // - EnterpriseModule should register EnterpriseView
            // - BudgetModule should register BudgetView
            // etc.
        }

        public bool RegisterView(string regionName, Type viewType)
        {
            if (string.IsNullOrEmpty(regionName))
                throw new ArgumentException("Region name cannot be null or empty", nameof(regionName));

            if (viewType == null)
                throw new ArgumentNullException(nameof(viewType));

            try
            {
                // Check if region exists before registering
                // if (!_regionManager.Regions.ContainsRegionWithName(regionName))
                // {
                //     Log.Warning("Region '{RegionName}' not found during view registration - will register when region becomes available", regionName);
                // }

                // Register with Prism region manager
                // _regionManager.RegisterViewWithRegion(regionName, viewType);

                // Track registration internally
                if (!_registeredViews.ContainsKey(regionName))
                    _registeredViews[regionName] = new List<Type>();

                if (!_registeredViews[regionName].Contains(viewType))
                {
                    _registeredViews[regionName].Add(viewType);
                }

                Log.Debug("Successfully registered {ViewType} with region {RegionName}", viewType.Name, regionName);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register {ViewType} with region {RegionName}", viewType.Name, regionName);
                return false;
            }
        }

        public bool IsViewRegistered(string viewName)
        {
            if (string.IsNullOrEmpty(viewName))
                return false;

            foreach (var regionViews in _registeredViews.Values)
            {
                foreach (var viewType in regionViews)
                {
                    if (viewType.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase) ||
                        viewType.Name.Replace("View", "", StringComparison.Ordinal).Equals(viewName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public IEnumerable<Type> GetRegisteredViews(string regionName)
        {
            if (string.IsNullOrEmpty(regionName) || !_registeredViews.ContainsKey(regionName))
                return new List<Type>();

            return _registeredViews[regionName];
        }

        public RegionValidationResult ValidateRegions()
        {
            Log.Information("Validating region configuration");

            var result = new RegionValidationResult();
            var requiredRegions = new[]
            {
                "DashboardRegion", "EnterpriseRegion", "BudgetRegion",
                "MunicipalAccountRegion", "UtilityCustomerRegion",
                "ReportsRegion", "AnalyticsRegion",
                "LeftPanelRegion", "RightPanelRegion", "BottomPanelRegion"
            };

            foreach (var regionName in requiredRegions)
            {
                // if (_regionManager.Regions.ContainsRegionWithName(regionName))
                // {
                //     var region = _regionManager.Regions[regionName];
                //     result.ValidRegions.Add(regionName);
                //     result.RegionViewCounts[regionName] = region.Views?.Count() ?? 0;
                // }
                // else
                // {
                //     result.MissingRegions.Add(regionName);
                // }
                result.MissingRegions.Add(regionName); // All regions missing since no region manager
            }

            result.TotalRegions = requiredRegions.Length;
            result.ValidRegionsCount = result.ValidRegions.Count;
            result.IsValid = result.MissingRegions.Count == 0;

            Log.Information("Region validation complete: {ValidCount}/{TotalCount} regions valid",
                result.ValidRegionsCount, result.TotalRegions);

            if (!result.IsValid)
            {
                Log.Warning("Missing regions: [{MissingRegions}]",
                    string.Join(", ", result.MissingRegions));
            }

            return result;
        }
    }

    // RegionValidationResult is defined in WileyWidget.Abstractions to avoid duplicate definitions.
}
