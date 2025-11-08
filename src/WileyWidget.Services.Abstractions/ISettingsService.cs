using WileyWidget.Models;

namespace WileyWidget.Services
{
    public interface ISettingsService
    {
        // Existing key/value helpers (legacy)
        string Get(string key);
        void Set(string key, string value);

        // AppSettings-backed API used by the application
        AppSettings Current { get; }

        /// <summary>
        /// Saves all application settings
        /// </summary>
        void Save();

        /// <summary>
        /// Saves fiscal year settings with the specified month and day
        /// </summary>
        /// <param name="month">The fiscal year start month (1-12)</param>
        /// <param name="day">The fiscal year start day</param>
        void SaveFiscalYearSettings(int month, int day);

        /// <summary>
        /// Loads application settings asynchronously
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        System.Threading.Tasks.Task LoadAsync();
    }
}
