using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Composition.SystemBackdrops;
using WileyWidget.Views;
using System;

namespace WileyWidget
{
    public sealed partial class MainWindow : Window
    {
        private Frame ContentFrame;

        public MainWindow()
        {
            // Create UI programmatically since XAML compiler is disabled
            var rootGrid = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
            ContentFrame = new Frame();
            rootGrid.Children.Add(ContentFrame);
            this.Content = rootGrid;

            this.SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.Base };

            // Navigate to Dashboard by default
            ContentFrame.Navigate(typeof(Views.DashboardView));

            // Wire up navigation selection - commented out since NavView not in XAML
            // if (this.FindName("NavView") is Microsoft.UI.Xaml.Controls.NavigationView nav)
            // {
            //     nav.SelectionChanged += Nav_SelectionChanged;
            // }
        }

        private void Nav_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer is Microsoft.UI.Xaml.Controls.NavigationViewItem item && ContentFrame != null)
            {
                var tag = item.Tag as string;
                if (string.Equals(tag, "BudgetOverview", StringComparison.OrdinalIgnoreCase))
                {
                    // Navigate using the Frame
                    ContentFrame.Navigate(typeof(BudgetOverviewPage));
                }
                else if (string.Equals(tag, "Dashboard", StringComparison.OrdinalIgnoreCase))
                {
                    ContentFrame.Navigate(typeof(Views.DashboardView));
                }
            }
        }
    }
}
