using CommunityToolkit.Mvvm.ComponentModel;

namespace WileyWidget.WinForms.ViewModels
{
    public partial class MainViewModel
    {
        private readonly WinFormsObservableValidator _validationHelper = new WinFormsObservableValidator();
        public void EnsureValidationInitialized() => _ = _validationHelper;
    }
}
