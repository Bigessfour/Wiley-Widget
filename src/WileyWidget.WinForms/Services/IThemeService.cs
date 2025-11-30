using System;
using WileyWidget.WinForms.Theming;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Abstraction for application theme management.
    /// Provides a DI-friendly, observable API and handles persistence + system follow behavior.
    /// </summary>
    public interface IThemeService : IDisposable
    {
        /// <summary>
        /// The user's theme preference. Can be Dark, Light or System (follow OS).
        /// Setting this will take effect immediately and persist the preference.
        /// </summary>
        AppTheme Preference { get; set; }

        /// <summary>
        /// The effective theme currently applied to the application (Dark or Light).
        /// When Preference==System this mirrors the OS preference.
        /// </summary>
        AppTheme CurrentTheme { get; }

        /// <summary>
        /// Event fired when the effective theme changes (Dark/Light).
        /// </summary>
        event EventHandler<AppTheme>? ThemeChanged;

        /// <summary>
        /// Initialize the service (load persisted preference and apply effective theme).
        /// </summary>
        void Initialize();
    }
}
