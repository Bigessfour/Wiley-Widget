// NOTE: This interface is deprecated. Prefer Prism's IDialogService directly in ViewModels.
// The file is retained as an obsolete shim to ease incremental refactors across the codebase.
// Once all references are removed, this file can be deleted.
using System;
using System.Threading.Tasks;
using Prism.Dialogs;

namespace WileyWidget.Services
{
    [Obsolete("IInteractionRequestService is deprecated. Use Prism.Dialogs.IDialogService instead.")]
    public interface IInteractionRequestService
    {
        Task<bool> ShowConfirmationAsync(string title, string message, string confirmButtonText = "Yes", string cancelButtonText = "No");
        Task ShowInformationAsync(string title, string message, string buttonText = "OK");
        Task ShowWarningAsync(string title, string message, string buttonText = "OK");
        Task ShowErrorAsync(string title, string message, string buttonText = "OK");
        Task<IDialogResult?> ShowDialogAsync<TViewModel>(string title, IDialogParameters parameters) where TViewModel : IDialogAware;
        Task<IDialogResult?> ShowDialogAsync(string dialogName, string title, IDialogParameters parameters);
    }
}
