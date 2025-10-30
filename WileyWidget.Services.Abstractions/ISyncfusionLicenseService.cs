namespace WileyWidget.Services
{
    public interface ISyncfusionLicenseService
    {
        void RegisterLicense(string licenseKey);
        bool IsLicenseValid();
        System.Threading.Tasks.Task<bool> ValidateLicenseAsync(string licenseKey);
    }
}
