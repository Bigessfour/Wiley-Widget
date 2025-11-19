using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Media;
using WileyWidget.ViewModels;
using Serilog;

namespace WileyWidget
{
    /// <summary>
    /// Main application window hosting the QuickBooks Dashboard.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow(MainViewModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);
            
            Log.Information("MainWindow constructor called");
            ViewModel = vm;
            Log.Information("ViewModel assigned: {ViewModelType}", vm.GetType().Name);
            
            try
            {
                Log.Information("Initializing XAML components...");
                InitializeComponent();
                Log.Information("XAML components initialized");
                
                // Apply Mica backdrop for modern Windows 11 look
                if (MicaController.IsSupported())
                {
                    SystemBackdrop = new MicaBackdrop();
                    Log.Information("Mica backdrop applied successfully");
                }
                else
                {
                    Log.Warning("Mica backdrop not supported on this system, using default background");
                }
                
                Log.Information("Setting DataContext on RootGrid...");
                // Window in WinUI 3 doesn't expose DataContext; set it on the root element instead.
                RootGrid.DataContext = ViewModel;
                Log.Information("DataContext set successfully");
                Log.Information("MainWindow initialized - Dashboard will load automatically");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize MainWindow");
                throw;
            }
        }
    }
}
