using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using System.Drawing;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Implementation of theme management service for applying Syncfusion themes and semantic colors
    /// </summary>
    public class ThemeManagerService : IThemeManagerService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<ThemeManagerService> _logger;
        private string _currentTheme;
        private bool _isSkinManagerAvailable;

        /// <summary>
        /// Event raised when the theme changes
        /// </summary>
        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        /// <summary>
        /// Available Syncfusion theme names
        /// </summary>
        private static readonly IReadOnlyList<string> AvailableThemes = new List<string>
        {
            "Office2019Colorful",
            "Office2019White",
            "Office2019Black",
            "Office2019DarkGray",
            "Office2016Colorful",
            "Office2016White",
            "Office2016Black",
            "Office2016DarkGray",
            "MaterialLight",
            "MaterialDark",
            "HighContrastBlack"
        }.AsReadOnly();

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeManagerService"/> class
        /// </summary>
        /// <param name="config">Application configuration</param>
        /// <param name="logger">Logger instance</param>
        public ThemeManagerService(IConfiguration config, ILogger<ThemeManagerService> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Determine initial theme from configuration
            _currentTheme = _config["UI:SyncfusionTheme"] ?? "Office2019DarkGray";

            // Check if SkinManager is available
            _isSkinManagerAvailable = SkinManager.ContainsSkinManager;

            if (_isSkinManagerAvailable)
            {
                _logger.LogInformation("ThemeManagerService initialized with theme: {ThemeName} (SkinManager available)", _currentTheme);
            }
            else
            {
                _logger.LogWarning("ThemeManagerService initialized with theme: {ThemeName} (SkinManager NOT available - using fallback colors)", _currentTheme);
            }
        }

        /// <inheritdoc/>
        public void ApplyTheme(Form form, string? themeName = null)
        {
            if (form == null)
            {
                throw new ArgumentNullException(nameof(form));
            }

            // Use provided theme or fall back to current/configured theme
            themeName ??= _currentTheme;

            try
            {
                if (_isSkinManagerAvailable)
                {
                    // Apply Syncfusion theme
                    SkinManager.SetVisualStyle(form, themeName);
                    _currentTheme = themeName;
                    _logger.LogDebug("Applied Syncfusion theme '{ThemeName}' to form '{FormName}'", themeName, form.Name);

                    // Also apply theme to all Syncfusion controls in the form
                    ApplyThemeToAllControls(form, themeName);

                    // Raise theme changed event
                    ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(themeName));
                }
                else
                {
                    // Fallback: Apply manual colors based on theme
                    ApplyManualTheme(form, themeName);
                    _logger.LogDebug("Applied manual theme '{ThemeName}' to form '{FormName}' (SkinManager unavailable)", themeName, form.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply theme '{ThemeName}' via SkinManager to form '{FormName}', falling back to manual theming", themeName, form.Name);

                // Fallback on error
                try
                {
                    ApplyManualTheme(form, themeName);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Failed to apply manual theme fallback to form '{FormName}'", form.Name);
                }
            }
        }

        /// <inheritdoc/>
        public string GetCurrentTheme()
        {
            return _currentTheme;
        }

        /// <inheritdoc/>
        public void ApplyThemeToControl(Control control, string? themeName = null)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }

            themeName ??= _currentTheme;

            try
            {
                // Check if the control has a ThemeName property (Syncfusion controls starting with Sf)
                var themeNameProperty = control.GetType().GetProperty("ThemeName");
                if (themeNameProperty != null && themeNameProperty.CanWrite)
                {
                    themeNameProperty.SetValue(control, themeName);
                    _logger.LogDebug("Applied theme '{ThemeName}' to Syncfusion control '{ControlType}'", themeName, control.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply theme '{ThemeName}' to control '{ControlType}'", themeName, control.GetType().Name);
            }
        }

        /// <inheritdoc/>
        public void ApplyThemeToAllControls(Control container, string? themeName = null)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            themeName ??= _currentTheme;

            try
            {
                // Recursively apply theme to all controls in the container
                foreach (Control control in container.Controls)
                {
                    // Apply theme if it's a Syncfusion control (has ThemeName property)
                    var controlType = control.GetType();
                    if (controlType.Name.StartsWith("Sf", StringComparison.Ordinal))
                    {
                        ApplyThemeToControl(control, themeName);
                    }

                    // Recursively process child controls
                    if (control.HasChildren)
                    {
                        ApplyThemeToAllControls(control, themeName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply theme to all controls in container '{ContainerName}'", container.Name);
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetAvailableThemes()
        {
            return AvailableThemes;
        }

        /// <inheritdoc/>
        public Color GetSemanticColor(SemanticColorType type)
        {
            var isDark = IsDarkTheme(_currentTheme);

            return type switch
            {
                SemanticColorType.Success => Color.FromArgb(52, 168, 83),
                SemanticColorType.Error => Color.FromArgb(234, 67, 53),
                SemanticColorType.Warning => Color.FromArgb(251, 188, 4),
                SemanticColorType.Info => Color.FromArgb(66, 133, 244),
                SemanticColorType.Primary => Color.FromArgb(66, 133, 244),
                SemanticColorType.Secondary => Color.FromArgb(95, 99, 104),
                SemanticColorType.Background => isDark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(255, 255, 255),
                SemanticColorType.Foreground => isDark ? Color.FromArgb(241, 241, 241) : Color.FromArgb(32, 33, 36),
                SemanticColorType.CardBackground => isDark ? Color.FromArgb(37, 37, 38) : Color.FromArgb(248, 249, 250),
                SemanticColorType.HeaderBackground => isDark ? Color.FromArgb(30, 30, 30) : Color.FromArgb(242, 242, 242),
                SemanticColorType.Border => isDark ? Color.FromArgb(63, 63, 70) : Color.FromArgb(218, 220, 224),
                SemanticColorType.Disabled => isDark ? Color.FromArgb(108, 117, 125) : Color.FromArgb(173, 181, 189),
                _ => isDark ? Color.White : Color.Black
            };
        }

        /// <inheritdoc/>
        public bool IsSkinManagerAvailable()
        {
            return _isSkinManagerAvailable;
        }

        /// <summary>
        /// Applies manual theme colors to a form when SkinManager is unavailable
        /// </summary>
        /// <param name="form">The form to theme</param>
        /// <param name="themeName">The theme name</param>
        private void ApplyManualTheme(Form form, string themeName)
        {
            var isDark = IsDarkTheme(themeName);

            if (isDark)
            {
                form.BackColor = Color.FromArgb(45, 45, 48);
                form.ForeColor = Color.FromArgb(241, 241, 241);
            }
            else
            {
                form.BackColor = Color.White;
                form.ForeColor = Color.FromArgb(32, 33, 36);
            }

            _logger.LogDebug("Applied manual {ThemeType} theme to form '{FormName}'", isDark ? "dark" : "light", form.Name);
        }

        /// <summary>
        /// Determines if a theme is a dark theme based on its name
        /// </summary>
        /// <param name="themeName">The theme name</param>
        /// <returns>True if the theme is dark, false otherwise</returns>
        private static bool IsDarkTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
            {
                return true; // Default to dark
            }

            var lowerTheme = themeName.ToLowerInvariant();
            return lowerTheme.Contains("dark") ||
                   lowerTheme.Contains("black") ||
                   themeName.Equals("Office2019DarkGray", StringComparison.OrdinalIgnoreCase) ||
                   themeName.Equals("Office2016DarkGray", StringComparison.OrdinalIgnoreCase);
        }
    }
}
