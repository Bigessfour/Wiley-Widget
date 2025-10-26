using System.Threading.Tasks;
using Prism.Dialogs;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for the interaction request service.
    /// </summary>
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
