using System.Threading.Tasks;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for Syncfusion license operations
    /// </summary>
    public interface ISyncfusionLicenseService
    {
        Task<bool> ValidateLicenseAsync(string licenseKey);
        void RegisterLicense(string licenseKey);
    }
}
