using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Diagnostics;

namespace WileyWidget.WinForms.Themes
{
    /// <summary>
    /// Theme application helper for consistent Syncfusion theme management.
    ///
    /// Per Syncfusion documentation, SfSkinManager's cascade mechanism automatically
    /// applies theme to all child controls when you call SetVisualStyle on parent form.
    /// This helper handles:
    /// 1. Theme validation before application
    /// 2. DockingManager edge case (cascade assistance)
    /// 3. Performance instrumentation
    /// 4. Graceful error handling
    ///
    /// Reference: Syncfusion documented issue - DockingManager sometimes doesn't
    /// receive theme via cascade if theme is applied before DockingManager is fully initialized.
    /// </summary>
    public static class ThemeApplicationHelper
    {
        /// <summary>
        /// Validates that a theme name is recognized and assemblies are loaded.
        /// </summary>
        /// <param name="themeName">Theme name to validate</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <returns>True if theme is valid and can be applied</returns>
        public static bool ValidateTheme(string? themeName, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(themeName))
            {
                logger?.LogWarning("Theme name is null or empty");
                return false;
            }

            // Known valid theme names in Syncfusion Windows Forms
            var knownThemes = new[]
            {
                "Office2019Colorful",
                "Office2019Black",
                "Office2019DarkGray",
                "Office2019HighContrast",
                "Office2019HighContrastWhite",
                "FluentLight",
                "FluentDark",
                "MaterialLight",
                "MaterialDark",
                "Bootstrap",
                "BootstrapDark",
                "HighContrastBlack",
                "HighContrastWhite"
            };

            var isKnownTheme = Array.Exists(knownThemes, theme =>
                theme.Equals(themeName, StringComparison.OrdinalIgnoreCase));

            if (!isKnownTheme)
            {
                logger?.LogWarning("Theme '{ThemeName}' is not a recognized Syncfusion theme name", themeName);
            }

            return true;
        }

        /// <summary>
        /// Applies theme to the parent form (main initialization point).
        ///
        /// Call this from MainForm.OnShown() AFTER validating initialization state.
        /// All child controls theme automatically via cascade.
        /// </summary>
        /// <param name="form">The main form to apply theme to</param>
        /// <param name="themeName">Theme name (e.g., "Office2019Colorful")</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <returns>True if theme application succeeded</returns>
        public static bool ApplyThemeToForm(
            Form? form,
            string? themeName,
            ILogger? logger = null)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (form is null)
                {
                    logger?.LogWarning("Cannot apply theme: Form is null");
                    return false;
                }

                if (!ValidateTheme(themeName, logger))
                {
                    return false;
                }

                logger?.LogDebug("Applying theme '{ThemeName}' to form", themeName);
                SfSkinManager.SetVisualStyle(form, themeName);

                sw.Stop();
                StartupInstrumentation.RecordPhaseTime("Form Theme Application", sw.ElapsedMilliseconds);
                logger?.LogInformation("Form theme applied in {Elapsed}ms", sw.ElapsedMilliseconds);
                return true;
            }
            catch (Exception ex)
            {
                sw.Stop();
                logger?.LogError(ex, "Failed to apply theme to form");
                return false;
            }
        }

        /// <summary>
        /// Applies theme explicitly to DockingManager using the adapter pattern.
        ///
        /// Call this from MainForm.OnShown() AFTER DockingManager creation and form theme application.
        /// Handles the edge case where DockingManager doesn't receive theme via automatic cascade.
        ///
        /// Uses DockingManagerThemeAdapter which translates theme names to VisualStyle enum.
        /// </summary>
        /// <param name="dockingManager">The DockingManager instance</param>
        /// <param name="themeName">Theme name to apply</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <returns>True if theme application succeeded</returns>
        public static bool ApplyThemeToDockingManager(
            DockingManager? dockingManager,
            string? themeName,
            ILogger? logger)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (dockingManager is null)
                {
                    logger?.LogWarning("Cannot apply theme: DockingManager is null");
                    return false;
                }

                if (!ValidateTheme(themeName, logger))
                {
                    return false;
                }

                logger?.LogDebug("Applying theme '{ThemeName}' to DockingManager", themeName);

                // Use adapter pattern with modern VisualStyle enum
                var adapter = new DockingManagerThemeAdapter(dockingManager, logger);
                adapter.ApplyTheme(themeName);

                sw.Stop();
                StartupInstrumentation.RecordPhaseTime("DockingManager Theme Application", sw.ElapsedMilliseconds);
                logger?.LogInformation("DockingManager theme applied in {Elapsed}ms", sw.ElapsedMilliseconds);
                return true;
            }
            catch (Exception ex)
            {
                sw.Stop();
                logger?.LogError(ex, "Failed to apply theme to DockingManager");
                return false;
            }
        }
    }

    /// <summary>
    /// Handles runtime theme switching.
    /// Updates global theme via SFSkinManager (cascade handles all controls automatically).
    /// Also updates DockingManager explicitly using the adapter pattern.
    /// </summary>
    public class ThemeSwitchHandler
    {
        private readonly IThemeService _themeService;
        private readonly ILogger _logger;
        private readonly DockingManager? _dockingManager;
        private readonly DockingManagerThemeAdapter? _dockingManagerAdapter;

        public ThemeSwitchHandler(
            IThemeService themeService,
            ILogger logger,
            DockingManager? dockingManager = null)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dockingManager = dockingManager;

            // Create adapter if DockingManager is provided
            if (dockingManager is not null)
            {
                _dockingManagerAdapter = new DockingManagerThemeAdapter(dockingManager, logger);
            }
        }

        /// <summary>
        /// Handles theme change event from IThemeService.
        /// Applies new theme globally via SFSkinManager (cascade handles all controls).
        /// Also updates DockingManager explicitly via adapter for edge case.
        /// </summary>
        public void OnThemeChanged(string newThemeName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newThemeName))
                {
                    _logger.LogWarning("Theme name is null or empty");
                    return;
                }

                _logger.LogInformation("Switching theme to '{NewTheme}'", newThemeName);

                // Update global theme - cascade handles all controls automatically
                SfSkinManager.ApplicationVisualTheme = newThemeName;

                // Also update DockingManager explicitly via adapter for edge case
                if (_dockingManagerAdapter is not null)
                {
                    _dockingManagerAdapter.ApplyTheme(newThemeName);
                }

                _logger.LogInformation("Theme switched to '{NewTheme}'", newThemeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch theme to '{NewTheme}'", newThemeName);
            }
        }
    }
}
