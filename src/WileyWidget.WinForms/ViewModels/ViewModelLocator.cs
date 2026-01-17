using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.WinForms.ViewModels
{
    public static class ViewModelLocator
    {
        public static T ResolveRequired<T>() where T : class
        {
            if (Program.Services == null)
            {
                if (IsDesignMode())
                {
                    var designInstance = TryCreate<T>();
                    if (designInstance != null)
                    {
                        return designInstance;
                    }
                }

                throw new InvalidOperationException($"Program.Services is not initialized. Cannot resolve {typeof(T).Name}.");
            }

            return ServiceProviderServiceExtensions.GetRequiredService<T>(Program.Services);
        }

        public static T? ResolveOptional<T>() where T : class
        {
            if (Program.Services == null)
            {
                return IsDesignMode() ? TryCreate<T>() : null;
            }

            return ServiceProviderServiceExtensions.GetService<T>(Program.Services);
        }

        private static bool IsDesignMode()
        {
            try
            {
                return LicenseManager.UsageMode == LicenseUsageMode.Designtime;
            }
            catch
            {
                return false;
            }
        }

        private static T? TryCreate<T>() where T : class
        {
            try
            {
                return Activator.CreateInstance(typeof(T)) as T;
            }
            catch
            {
                return null;
            }
        }
    }
}
