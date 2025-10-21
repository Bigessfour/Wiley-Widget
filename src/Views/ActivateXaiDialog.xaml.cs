using System;
using System.Threading.Tasks;
using System.Windows;
using WileyWidget.ViewModels;

namespace WileyWidget.Views
{
    public partial class ActivateXaiDialog : Window
    {
        public System.Windows.Input.ICommand CloseCommand { get; private set; }
        public ActivateXaiDialog()
        {
            InitializeComponent();

            Loaded += (_, __) => ApiKeyBox.Focus();

            OpenConsoleBtn.Click += (_, __) => (DataContext as SettingsViewModel)?.OpenXaiConsolePublic();
            ValidateBtn.Click += async (_, __) => await ValidateAsync();
            SaveBtn.Click += async (_, __) => await SaveAsync();
            // Provide a window-local CloseCommand for binding
            CloseCommand = new Prism.Commands.DelegateCommand(() => Close());
        }

        private async Task ValidateAsync()
        {
            var vm = DataContext as SettingsViewModel;
            var key = ApiKeyBox.Password?.Trim();
            // Status and Save button enabled are bound to VM properties; just trigger validation
            ValidateBtn.IsEnabled = false;
            try
            {
                await vm?.ValidateXaiKeyAsyncPublic(key);
                // UI updates (StatusBlock text and SaveBtn state) are bound to VM properties
            }
            finally
            {
                ValidateBtn.IsEnabled = true;
            }
        }

        private async Task SaveAsync()
        {
            var vm = DataContext as SettingsViewModel;
            var key = ApiKeyBox.Password?.Trim();
            SaveBtn.IsEnabled = false;
            try
            {
                await vm?.ValidateAndSaveXaiKeyAsyncPublic(key);
                if (vm?.IsXaiKeyValidated == true)
                {
                    ApiKeyBox.Clear();
                    Close();
                }
                else
                {
                    SaveBtn.IsEnabled = true;
                }
            }
            finally
            {
                SaveBtn.IsEnabled = true;
            }
        }
    }
}
