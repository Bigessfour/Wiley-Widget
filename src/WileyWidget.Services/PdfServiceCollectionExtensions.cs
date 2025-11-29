using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    public static class PdfServiceCollectionExtensions
    {
        /// <summary>
        /// Register PDF-related services including <see cref="IPdfService"/>.
        /// If a Syncfusion license key is available in environment variable 'SYNCFUSION_LICENSE_KEY'
        /// or configuration at 'Syncfusion:LicenseKey', it will be registered using the documented API.
        /// </summary>
        public static IServiceCollection AddPdfServices(this IServiceCollection services, IConfiguration? configuration = null)
        {
            // Register license if provided (documented method)
            var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (string.IsNullOrWhiteSpace(licenseKey) && configuration != null)
            {
                licenseKey = configuration.GetValue<string>("Syncfusion:LicenseKey");
            }

            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                // Documented Syncfusion license registration
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
            }

            services.AddSingleton<IPdfService, SyncfusionPdfService>();
            return services;
        }
    }
}
