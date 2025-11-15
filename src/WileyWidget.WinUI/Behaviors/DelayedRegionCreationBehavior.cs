using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

namespace WileyWidget.WinUI.Behaviors
{
    /// <summary>
    /// Region behavior that delays the creation of views until they are actually needed
    /// </summary>
    public class DelayedRegionCreationBehavior : RegionBehavior
    {
        public const string BehaviorKey = "DelayedRegionCreation";

        private readonly ILogger<DelayedRegionCreationBehavior> _logger;
        private readonly SemaphoreSlim _creationLock = new(1, 1);
        private bool _isInitialized;

        public DelayedRegionCreationBehavior(ILogger<DelayedRegionCreationBehavior> logger)
        {
            _logger = logger;
        }

        protected override void OnAttach()
        {
            Region.NavigationService.Navigating += NavigationService_Navigating;
            _logger.LogDebug("DelayedRegionCreationBehavior attached to region '{RegionName}'", Region.Name);
        }

        private async void NavigationService_Navigating(object sender, RegionNavigationEventArgs e)
        {
            if (!_isInitialized)
            {
                await InitializeRegionAsync();
            }
        }

        private async Task InitializeRegionAsync()
        {
            await _creationLock.WaitAsync();
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogDebug("Initializing delayed region creation for '{RegionName}'", Region.Name);

                    // Perform any delayed initialization here
                    // For example, register views, create view models, etc.

                    _isInitialized = true;
                    _logger.LogInformation("Delayed region creation completed for '{RegionName}'", Region.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize delayed region creation for '{RegionName}'", Region.Name);
            }
            finally
            {
                _creationLock.Release();
            }
        }

        public bool IsInitialized => _isInitialized;
    }
}