using System.Windows;

namespace WileyWidget.Views.Windows
{
    /// <summary>
    /// Interaction logic for SplashWindow.xaml
    /// </summary>
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        public void UpdateStatus(string status)
        {
            if (StatusTextBlock != null)
            {
                StatusTextBlock.Text = status;
            }
        }

        public void CloseSplash()
        {
            this.Close();
        }
    }
}