using System.Drawing;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Semantic color categories for theme-aware UI elements
    /// </summary>
    public enum SemanticColorType
    {
        /// <summary>Success states (e.g., connected, completed)</summary>
        Success,
        
        /// <summary>Error states (e.g., failed, disconnected)</summary>
        Error,
        
        /// <summary>Warning states (e.g., attention needed)</summary>
        Warning,
        
        /// <summary>Informational states</summary>
        Info,
        
        /// <summary>Primary action buttons and accents</summary>
        Primary,
        
        /// <summary>Secondary actions and less prominent elements</summary>
        Secondary,
        
        /// <summary>Main background color</summary>
        Background,
        
        /// <summary>Main foreground/text color</summary>
        Foreground,
        
        /// <summary>Card/panel background color</summary>
        CardBackground,
        
        /// <summary>Header/toolbar background color</summary>
        HeaderBackground,
        
        /// <summary>Divider/border color</summary>
        Border,
        
        /// <summary>Disabled state color</summary>
        Disabled
    }

    /// <summary>
    /// Service for managing application-wide theme settings and applying Syncfusion themes
    /// </summary>
    public interface IThemeManagerService
    {
        /// <summary>
        /// Applies the specified theme to a form and all its controls
        /// </summary>
        /// <param name="form">The form to apply the theme to</param>
        /// <param name="themeName">Optional theme name (defaults to configuration or Office2019DarkGray)</param>
        void ApplyTheme(Form form, string? themeName = null);

        /// <summary>
        /// Applies the specified theme directly to a Syncfusion control via ThemeName property
        /// </summary>
        /// <param name="control">The Syncfusion control to apply the theme to</param>
        /// <param name="themeName">Optional theme name (defaults to current theme)</param>
        void ApplyThemeToControl(Control control, string? themeName = null);

        /// <summary>
        /// Recursively applies theme to all Syncfusion controls in a container
        /// </summary>
        /// <param name="container">The container control to process</param>
        /// <param name="themeName">Optional theme name (defaults to current theme)</param>
        void ApplyThemeToAllControls(Control container, string? themeName = null);

        /// <summary>
        /// Gets the currently active theme name
        /// </summary>
        /// <returns>The current theme name</returns>
        string GetCurrentTheme();

        /// <summary>
        /// Gets a list of all available Syncfusion theme names
        /// </summary>
        /// <returns>Read-only list of theme names</returns>
        IReadOnlyList<string> GetAvailableThemes();

        /// <summary>
        /// Gets a semantic color for the current theme
        /// </summary>
        /// <param name="type">The semantic color type</param>
        /// <returns>The color for the specified semantic type</returns>
        Color GetSemanticColor(SemanticColorType type);

        /// <summary>
        /// Checks if Syncfusion SkinManager is available and loaded
        /// </summary>
        /// <returns>True if SkinManager is available, false otherwise</returns>
        bool IsSkinManagerAvailable();

        /// <summary>
        /// Event raised when the theme changes
        /// </summary>
        event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
    }

    /// <summary>
    /// Event args for theme change notifications
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The name of the newly applied theme
        /// </summary>
        public string ThemeName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeChangedEventArgs"/> class
        /// </summary>
        /// <param name="themeName">The name of the newly applied theme</param>
        public ThemeChangedEventArgs(string themeName)
        {
            ThemeName = themeName;
        }
    }
}
