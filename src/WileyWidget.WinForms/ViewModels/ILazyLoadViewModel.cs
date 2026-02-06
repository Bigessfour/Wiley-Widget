using System.Threading.Tasks;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Interface for ViewModels that support lazy loading.
    /// Data loading is deferred until the associated UI panel becomes visible to the user.
    /// </summary>
    public interface ILazyLoadViewModel
    {
        /// <summary>
        /// Gets a value indicating whether the data has been loaded at least once.
        /// </summary>
        bool IsDataLoaded { get; }

        /// <summary>
        /// Called when the panel's visibility or dock state changes.
        /// ViewModels should use this to trigger their first heavy data load.
        /// </summary>
        /// <param name="isVisible">True if the panel is now visible to the user.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task OnVisibilityChangedAsync(bool isVisible);
    }
}
