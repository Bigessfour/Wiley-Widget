using System.Collections.ObjectModel;
using WileyWidget.Models;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Interface for Enterprise Vital Signs ViewModel.
    /// </summary>
    public interface IEnterpriseVitalSignsViewModel : ILazyLoadViewModel
    {
        /// <summary>
        /// Gets the collection of enterprise snapshots.
        /// </summary>
        ObservableCollection<EnterpriseSnapshot> EnterpriseSnapshots { get; }

        /// <summary>
        /// Gets the overall city net position.
        /// </summary>
        decimal OverallCityNet { get; }
    }
}
