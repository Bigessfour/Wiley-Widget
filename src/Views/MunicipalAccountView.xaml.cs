using System;
using System.Windows;
using System.Windows.Controls;
using WileyWidget.ViewModels;
using Syncfusion.SfSkinManager;
using Serilog;

namespace WileyWidget.Views
{
    /// <summary>
    /// Interaction logic for MunicipalAccountView.xaml
    /// </summary>
    public partial class MunicipalAccountView : UserControl
    {
        private bool _loadedOnce;
        private readonly bool _isTestMode;

        /// <summary>
        /// Initializes a new instance of the MunicipalAccountView
        /// </summary>
        public MunicipalAccountView()
        {
            InitializeComponent();

            // Detect test mode from environment variable
            try
            {
                var testEnv = Environment.GetEnvironmentVariable("WILEY_WIDGET_TESTMODE");
                _isTestMode = !string.IsNullOrEmpty(testEnv) && (testEnv == "1" || testEnv.Equals("true", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                _isTestMode = false;
            }

            if (_isTestMode)
            {
                // Reveal test controls if they exist in the visual tree
                try
                {
                    var seed = this.FindName("Btn_SeedTestData") as FrameworkElement;
                    var clear = this.FindName("Btn_ClearTestData") as FrameworkElement;
                    var exp = this.FindName("Btn_ExportStub") as FrameworkElement;
                    if (seed != null) seed.Visibility = Visibility.Visible;
                    if (clear != null) clear.Visibility = Visibility.Visible;
                    if (exp != null) exp.Visibility = Visibility.Visible;
                }
                catch { /* ignore when controls missing at design time */ }
            }

            // ViewModel is auto-wired by Prism ViewModelLocator
            // Load data when control is loaded
            this.Loaded += MunicipalAccountView_Loaded;
        }

        private async void MunicipalAccountView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_loadedOnce) return;
                _loadedOnce = true;
                if (DataContext is MunicipalAccountViewModel viewModel)
                {
                    await viewModel.InitializeAsync();

                    // populate theme name for tests (best-effort)
                    try
                    {
                        var root = this.Content as FrameworkElement;
                        if (root != null)
                        {
                            var themeObj = SfSkinManager.GetTheme(root);
                            if (themeObj != null)
                            {
                                var themeNameProp = themeObj.GetType().GetProperty("ThemeName");
                                var name = themeNameProp?.GetValue(themeObj) as string;
                                var themeText = this.FindName("ThemeName") as TextBlock;
                                if (themeText != null && name != null)
                                    themeText.Text = name;
                            }
                        }
                    }
                    catch { }

                    Log.Information("MunicipalAccountView data initialized");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize MunicipalAccountView data");
                MessageBox.Show(
                    $"Failed to load account data: {ex.Message}",
                    "Data Loading Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void SeedTestData_Click(object sender, RoutedEventArgs e)
        {
            // Try to call a test hook on the ViewModel: SeedTestData(int count)
            try
            {
                if (DataContext != null)
                {
                    var vmType = DataContext.GetType();
                    var method = vmType.GetMethod("SeedTestData", new Type[] { typeof(int) });
                    if (method != null)
                    {
                        method.Invoke(DataContext, new object[] { 25 });
                        return;
                    }

                    // fallback: look for parameterless SeedTestData
                    method = vmType.GetMethod("SeedTestData", Type.EmptyTypes);
                    method?.Invoke(DataContext, null);
                }
            }
            catch { /* best-effort only for tests */ }
        }

        private void ClearTestData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext != null)
                {
                    var vmType = DataContext.GetType();
                    var method = vmType.GetMethod("ClearTestData", Type.EmptyTypes);
                    method?.Invoke(DataContext, null);
                }
            }
            catch { }
        }

        private void ExportStub_Click(object sender, RoutedEventArgs e)
        {
            // Invoke a test-friendly export path on the ViewModel if present
            try
            {
                if (DataContext != null)
                {
                    var vmType = DataContext.GetType();
                    var method = vmType.GetMethod("ExportToCsvStub", new Type[] { typeof(string) });
                    if (method != null)
                    {
                        // export to a deterministic temp path
                        var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wileywidget_export_stub.csv");
                        method.Invoke(DataContext, new object[] { tmp });
                    }
                    else
                    {
                        // fallback: try parameterless stub
                        method = vmType.GetMethod("ExportToCsvStub", Type.EmptyTypes);
                        method?.Invoke(DataContext, null);
                    }
                }
            }
            catch { }
        }
    }
}
