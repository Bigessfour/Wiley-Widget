using CommunityToolkit.Mvvm.ComponentModel;

namespace WileyWidget.WinForms.ViewModels
{
    public partial class SettingsViewModel
    {
        private readonly ObservableValidator _validationHelper = new ObservableValidator();
        public void EnsureValidationInitialized() => _ = _validationHelper;
    }
}
