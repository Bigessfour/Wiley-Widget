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
        void Save();
    }
}
