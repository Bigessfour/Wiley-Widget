using System;
using System.Windows;
using System.Windows.Controls;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Grid;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;

namespace WileyWidget.Views.Main {
    /// <summary>
    /// Interaction logic for MunicipalAccountView.xaml
    /// </summary>
    public partial class MunicipalAccountView : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the MunicipalAccountView
        /// </summary>
        public MunicipalAccountView()
        {
            InitializeComponent();
            Loaded += MunicipalAccountView_Loaded;
        }

        private void MunicipalAccountView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MunicipalAccountViewModel viewModel)
            {
                viewModel.AccountsDataGrid = AccountsGrid;
            }
        }

        // Test hooks were removed from the view to enforce MVVM.
        // If test-only seeding/export is required, implement ICommands on the ViewModel and bind to the buttons' Command property.
    }
}
