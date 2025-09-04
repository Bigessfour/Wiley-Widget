using System.Threading.Tasks;
using WileyWidget.Services;
using WileyWidget.Configuration;

namespace WileyWidget.Services
{
    /// <summary>
    /// Thin facade combining API key operations + settings persistence for UI binding.
    /// </summary>
    public interface IApiKeyFacade
    {
        Task<bool> TestAsync(string key);
        bool SaveSecure(string key);
        bool Remove();
        ApiKeyInfo Info();
    }

    public sealed class ApiKeyFacade : IApiKeyFacade
    {
        private readonly ApiKeyService _service;
        private readonly SettingsService _settings;
        public ApiKeyFacade(ApiKeyService service, SettingsService settings)
        {
            _service = service;
            _settings = settings;
        }

        public Task<bool> TestAsync(string key) => _service.TestApiKeyAsync(key);

        public bool SaveSecure(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            var ok = _service.StoreApiKey(key, StorageMethod.Auto);
            if (ok)
            {
                _settings.Current.XaiApiKey = "[SECURELY_STORED]";
                _settings.Save();
            }
            return ok;
        }

        public bool Remove()
        {
            var ok = _service.RemoveApiKey();
            if (ok)
            {
                _settings.Current.XaiApiKey = null;
                _settings.Save();
            }
            return ok;
        }

        public ApiKeyInfo Info() => _service.GetApiKeyInfo();
    }
}
