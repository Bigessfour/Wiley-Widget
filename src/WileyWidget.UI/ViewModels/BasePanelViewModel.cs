using Prism.Mvvm;
using System.Globalization;

namespace WileyWidget.UI.ViewModels
{
    /// <summary>
    /// Base class for panel ViewModels with common properties.
    /// Provides shared functionality for loading state, status messages, and update tracking.
    /// </summary>
    public abstract class BasePanelViewModel : BindableBase
    {
        #region Common Properties

        private bool _isLoading;
        /// <summary>
        /// Gets or sets a value indicating whether the panel is currently loading data.
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _statusMessage = string.Empty;
        /// <summary>
        /// Gets or sets the current status message to display to the user.
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _lastUpdated = "Never";
        /// <summary>
        /// Gets or sets the timestamp of the last data update.
        /// </summary>
        public string LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Updates the LastUpdated property to the current time.
        /// </summary>
        protected void UpdateTimestamp()
        {
            LastUpdated = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Sets IsLoading to true and clears the status message.
        /// </summary>
        protected void BeginLoading()
        {
            IsLoading = true;
            StatusMessage = string.Empty;
        }

        /// <summary>
        /// Sets IsLoading to false and updates the timestamp.
        /// </summary>
        protected void EndLoading()
        {
            IsLoading = false;
            UpdateTimestamp();
        }

        #endregion
    }
}
