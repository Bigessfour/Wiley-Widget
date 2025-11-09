using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Services;
using WileyWidget.ViewModels;

#nullable enable

namespace WileyWidget.Views.Main {
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Attempt to apply a Syncfusion theme; falls back to Fluent Light if requested theme fails.
        /// </summary>
        private void TryApplyTheme(string themeName)
        {
            // Theme application is handled globally via SfSkinManager.ApplicationTheme
        }
    }
}
