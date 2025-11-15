using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

namespace WileyWidget.WinUI.Behaviors
{
    /// <summary>
    /// Region behavior that automatically saves data when navigating away from views
    /// </summary>
    public class AutoSaveBehavior : RegionBehavior
    {
        public const string BehaviorKey = "AutoSave";

        private readonly ILogger<AutoSaveBehavior> _logger;

        public AutoSaveBehavior(ILogger<AutoSaveBehavior> logger)
        {
            _logger = logger;
        }

        protected override void OnAttach()
        {
            Region.NavigationService.Navigating += NavigationService_Navigating;
            _logger.LogDebug("AutoSaveBehavior attached to region '{RegionName}'", Region.Name);
        }

        private async void NavigationService_Navigating(object sender, RegionNavigationEventArgs e)
        {
            try
            {
                // Check if the current view implements IAutoSave
                if (Region.ActiveViews.Count > 0)
                {
                    foreach (var view in Region.ActiveViews)
                    {
                        if (view is IAutoSave autoSaveView)
                        {
                            _logger.LogDebug("Auto-saving data for view in region '{RegionName}'", Region.Name);
                            await autoSaveView.SaveAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save data in region '{RegionName}'", Region.Name);
            }
        }
    }

    /// <summary>
    /// Interface for views that support automatic saving
    /// </summary>
    public interface IAutoSave
    {
        Task SaveAsync();
    }
}