using System.Threading.Tasks;

namespace WileyWidget.Abstractions
{
    /// <summary>
    /// Interface for ViewModels that support lazy loading of data when their associated panel becomes visible.
    /// Implementations should defer heavy data operations until the panel is displayed to improve startup performance.
    /// </summary>
    public interface ILazyLoadViewModel
    {
        /// <summary>
        /// Called when the visibility state of the associated panel changes.
        /// Implementations should defer data loading until isVisible is true.
        /// </summary>
        /// <param name="isVisible">True if the panel is now visible; false if hidden.</param>
        /// <returns>A task that completes when the visibility change handling is done.</returns>
        Task OnVisibilityChangedAsync(bool isVisible);
    }
}
