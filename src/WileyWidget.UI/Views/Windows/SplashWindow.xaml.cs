using System.ComponentModel;
using System.Windows;

namespace WileyWidget.Views.Windows
{
    /// <summary>
    /// Interaction logic for SplashWindow.xaml
    /// </summary>
    public partial class SplashWindow : Window, INotifyPropertyChanged
    {
        private string _statusMessage = "Starting up...";

        public SplashWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusMessage)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void UpdateStatus(string message)
        {
            StatusMessage = message;
        }

        public void CloseSplash()
        {
            Dispatcher.Invoke(() => Close());
        }
    }
}
