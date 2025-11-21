using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WileyWidget.ViewModels;
using Serilog;

namespace WileyWidget.Views
{
    /// <summary>
    /// QuickBooks Dashboard landing page displaying connection status and financial KPIs.
    /// </summary>
    public sealed partial class DashboardView : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            "ViewModel", typeof(QuickBooksDashboardViewModel), typeof(DashboardView), new PropertyMetadata(null));

        public QuickBooksDashboardViewModel ViewModel
        {
            get => (QuickBooksDashboardViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public DashboardView()
        {
            // It's possible DI is not configured during diagnostic/minimal runs. Resolve if available, otherwise leave null.
            Log.Information("DashboardView constructor called");

            ViewModel = App.Services?.GetService(typeof(QuickBooksDashboardViewModel)) as QuickBooksDashboardViewModel ?? null!;

            if (ViewModel is not null)
            {
                this.DataContext = ViewModel;
                _ = ViewModel.LoadAsync();
                Log.Information("DashboardView initialized with ViewModel");
            }
            else
            {
                Log.Warning("DashboardView: QuickBooksDashboardViewModel not available from DI. Running in diagnostic mode.");
            }
        }
    }
}
