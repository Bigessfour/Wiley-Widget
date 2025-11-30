using CommunityToolkit.Mvvm.ComponentModel;

namespace WileyWidget.WinForms.ViewModels
{
    public partial class MainViewModel
    {
        private readonly ObservableValidator _validationHelper = new ObservableValidator();
        public void EnsureValidationInitialized() => _ = _validationHelper;
    }
}
