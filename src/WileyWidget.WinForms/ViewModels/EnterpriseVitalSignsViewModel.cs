using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Configuration;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for Enterprise Vital Signs panel displaying enterprise snapshots with gauges and charts.
    /// </summary>
    public partial class EnterpriseVitalSignsViewModel : ObservableObject, IEnterpriseVitalSignsViewModel
    {
        /// <summary>
        /// Gets or sets a value indicating whether data has been loaded.
        /// </summary>
        [ObservableProperty]
        private bool isDataLoaded;

        public async Task OnVisibilityChangedAsync(bool isVisible)
        {
            if (isVisible && !IsDataLoaded && !IsLoading)
            {
                await LoadDataCommand.ExecuteAsync(null);
                IsDataLoaded = true;
            }
        }

        private readonly ILogger<EnterpriseVitalSignsViewModel> _logger;
        private readonly IDashboardService _dashboardService;

        /// <summary>Gets the collection of enterprise snapshots.</summary>
        public ObservableCollection<EnterpriseSnapshot> EnterpriseSnapshots { get; } = new();

        /// <summary>Gets the overall city net position.</summary>
        [ObservableProperty]
        private decimal overallCityNet;

        /// <summary>Gets the command to load/refresh enterprise data.</summary>
        public IAsyncRelayCommand LoadDataCommand { get; }

        /// <summary>Gets the command to refresh enterprise data.</summary>
        public IAsyncRelayCommand RefreshCommand { get; }

        /// <summary>Gets or sets a value indicating whether data is loading.</summary>
        [ObservableProperty]
        private bool isLoading;

        /// <summary>Gets or sets the error message.</summary>
        [ObservableProperty]
        private string? errorMessage;

        /// <summary>
        /// Constructor for EnterpriseVitalSignsViewModel.
        /// </summary>
        public EnterpriseVitalSignsViewModel(
            ILogger<EnterpriseVitalSignsViewModel> logger,
            IDashboardService dashboardService)
        {
            _logger = logger ?? NullLogger<EnterpriseVitalSignsViewModel>.Instance;
            _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));

            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var snapshots = await _dashboardService.GetEnterpriseSnapshotsAsync();
                EnterpriseSnapshots.Clear();
                foreach (var snap in snapshots)
                {
                    EnterpriseSnapshots.Add(snap);
                }
                OnPropertyChanged(nameof(EnterpriseSnapshots));
                OverallCityNet = snapshots.Sum(s => s.NetPosition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load enterprise snapshots");
                ErrorMessage = $"Failed to load data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshAsync()
        {
            await LoadDataAsync();
        }
    }
}
