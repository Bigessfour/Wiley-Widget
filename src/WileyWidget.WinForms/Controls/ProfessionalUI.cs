using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls
{
    /// <summary>
    /// Professional UI enhancements for Wiley Widget - shadows, animations, and accessibility.
    /// Provides enterprise-grade visual polish comparable to Office 365 and VS Code.
    /// </summary>
    public static class ProfessionalUI
    {
        #region Shadow Effects

        /// <summary>
        /// Applies subtle drop shadows to panels for professional depth (like Office 365 cards).
        /// </summary>
        public static void ApplyShadowEffect(Panel panel, int shadowSize = 4)
        {
            if (panel == null) return;

            // Create shadow bitmap
            var shadowBitmap = new Bitmap(panel.Width + shadowSize * 2, panel.Height + shadowSize * 2);
            using (var g = Graphics.FromImage(shadowBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw shadow gradient
                using (var shadowBrush = new LinearGradientBrush(
                    new Rectangle(0, 0, shadowBitmap.Width, shadowBitmap.Height),
                    Color.FromArgb(40, 0, 0, 0), Color.Transparent, LinearGradientMode.ForwardDiagonal))
                {
                    g.FillRectangle(shadowBrush, 0, 0, shadowBitmap.Width, shadowBitmap.Height);
                }

                // Draw panel content
                g.DrawImage(panel.BackgroundImage ?? new Bitmap(panel.Width, panel.Height), shadowSize, shadowSize);
            }

            // Apply as background image with offset
            panel.BackgroundImage = shadowBitmap;
            panel.BackgroundImageLayout = ImageLayout.None;
            panel.Padding = new Padding(shadowSize);
        }

        /// <summary>
        /// Applies modern card shadow effect with rounded corners.
        /// </summary>
        public static void ApplyCardShadow(Panel card, Color shadowColor = default)
        {
            if (card == null) return;

            shadowColor = shadowColor == default ? Color.FromArgb(30, 0, 0, 0) : shadowColor;

            // Create rounded rectangle shadow
            var shadowBitmap = new Bitmap(card.Width + 8, card.Height + 8);
            using (var g = Graphics.FromImage(shadowBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var shadowBrush = new SolidBrush(shadowColor))
                using (var path = CreateRoundedRectangle(new Rectangle(4, 4, card.Width, card.Height), 8))
                {
                    g.FillPath(shadowBrush, path);
                }
            }

            card.BackgroundImage = shadowBitmap;
            card.BackgroundImageLayout = ImageLayout.None;
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            path.AddArc(rect.X + rect.Width - radius, rect.Y, radius, radius, 270, 90);
            path.AddArc(rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, radius, 0, 90);
            path.AddArc(rect.X, rect.Y + rect.Height - radius, radius, radius, 90, 90);
            path.CloseFigure();
            return path;
        }

        #endregion

        #region Hover Animations

        /// <summary>
        /// Adds smooth hover animations to buttons and cards (like VS Code elements).
        /// </summary>
        public static void EnableHoverAnimation(Control control, Color hoverColor, Color originalColor)
        {
            if (control == null) return;

            control.MouseEnter += (s, e) =>
            {
                AnimateColorTransition(control, control.BackColor, hoverColor, 200);
            };

            control.MouseLeave += (s, e) =>
            {
                AnimateColorTransition(control, control.BackColor, originalColor, 200);
            };
        }

        /// <summary>
        /// Adds scale animation on hover for interactive elements.
        /// </summary>
        public static void EnableScaleAnimation(Control control, float scaleFactor = 1.05f)
        {
            if (control == null) return;

            var originalSize = control.Size;

            control.MouseEnter += (s, e) =>
            {
                AnimateScale(control, originalSize, new Size((int)(originalSize.Width * scaleFactor), (int)(originalSize.Height * scaleFactor)), 150);
            };

            control.MouseLeave += (s, e) =>
            {
                AnimateScale(control, control.Size, originalSize, 150);
            };
        }

        private static void AnimateColorTransition(Control control, Color fromColor, Color toColor, int durationMs)
        {
            var timer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 FPS
            var startTime = DateTime.Now;
            var originalColor = control.BackColor;

            timer.Tick += (s, e) =>
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                var progress = Math.Min(elapsed / durationMs, 1.0);

                // Smooth easing function (ease-out)
                var easedProgress = 1 - Math.Pow(1 - progress, 3);

                var r = (int)(fromColor.R + (toColor.R - fromColor.R) * easedProgress);
                var g = (int)(fromColor.G + (toColor.G - fromColor.G) * easedProgress);
                var b = (int)(fromColor.B + (toColor.B - fromColor.B) * easedProgress);

                control.BackColor = Color.FromArgb(r, g, b);

                if (progress >= 1.0)
                {
                    timer.Stop();
                    timer.Dispose();
                }
            };

            timer.Start();
        }

        private static void AnimateScale(Control control, Size fromSize, Size toSize, int durationMs)
        {
            var timer = new System.Windows.Forms.Timer { Interval = 16 };
            var startTime = DateTime.Now;

            timer.Tick += (s, e) =>
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                var progress = Math.Min(elapsed / durationMs, 1.0);

                var easedProgress = 1 - Math.Pow(1 - progress, 3);

                var width = (int)(fromSize.Width + (toSize.Width - fromSize.Width) * easedProgress);
                var height = (int)(fromSize.Height + (toSize.Height - fromSize.Height) * easedProgress);

                control.Size = new Size(width, height);

                if (progress >= 1.0)
                {
                    timer.Stop();
                    timer.Dispose();
                }
            };

            timer.Start();
        }

        #endregion

        #region Enhanced Theme Application

        /// <summary>
        /// Applies professional theme with vibrancy boost and full recursive coverage.
        /// </summary>
        public static void ApplyProfessionalTheme(Form form, string themeName = "Office2019Colorful")
        {
            if (form == null) return;

            ArgumentNullException.ThrowIfNull(themeName);

            try
            {
                // Apply base Syncfusion theme
                SfSkinManager.SetVisualStyle(form, themeName);

                // Boost vibrancy for OfficeColorful (like Excel/PowerPoint)
                if (themeName.Contains("Colorful", StringComparison.Ordinal))
                {
                    BoostThemeVibrancy(form);
                }

                // Apply professional styling recursively
                ApplyRecursiveStyling(form);

                // Add accessibility support
                EnableHighContrastSupport(form);

                Serilog.Log.Information("Professional theme '{Theme}' applied with enhancements", themeName);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Professional theme application failed");
            }
        }

        private static void BoostThemeVibrancy(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                // Boost background colors for more vibrant appearance
                if (control.BackColor != Color.Transparent && control.BackColor.GetBrightness() > 0.7)
                {
                    control.BackColor = IncreaseSaturation(control.BackColor, 1.3f);
                }

                // Boost foreground colors for better contrast
                if (control.ForeColor != Color.Transparent && control.ForeColor.GetBrightness() > 0.5)
                {
                    control.ForeColor = IncreaseSaturation(control.ForeColor, 1.2f);
                }

                // Recurse
                BoostThemeVibrancy(control);
            }
        }

        private static void ApplyRecursiveStyling(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                // Apply professional padding and margins
                if (control is Panel panel)
                {
                    if (panel.Padding == Padding.Empty)
                        panel.Padding = new Padding(8);

                    // Add subtle shadows to card-like panels
                    if (panel.Height < 200 && panel.Width > 200) // Card dimensions
                    {
                        ApplyCardShadow(panel);
                    }
                }

                // Enhanced button styling
                if (control is Button button)
                {
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 1;
                    EnableHoverAnimation(button, Color.FromArgb(230, 230, 230), button.BackColor);
                }

                // Enhanced label styling
                if (control is Label label && label.Font.Size > 12)
                {
                    // Make large labels bold for hierarchy
                    label.Font = new Font(label.Font.FontFamily, label.Font.Size, FontStyle.Bold);
                }

                // Recurse
                ApplyRecursiveStyling(control);
            }
        }

        private static Color IncreaseSaturation(Color color, float factor)
        {
            // Convert RGB to HSL, boost saturation, convert back
            float h, s, l;
            RGBToHSL(color, out h, out s, out l);
            s = Math.Min(1f, s * factor);
            return HSLToRGB(h, s, l);
        }

        private static void RGBToHSL(Color color, out float h, out float s, out float l)
        {
            float r = color.R / 255f;
            float g = color.G / 255f;
            float b = color.B / 255f;

            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;

            if (max == min)
            {
                h = s = 0;
            }
            else
            {
                float delta = max - min;
                s = l > 0.5 ? delta / (2 - max - min) : delta / (max + min);

                if (max == r) h = (g - b) / delta + (g < b ? 6 : 0);
                else if (max == g) h = (b - r) / delta + 2;
                else h = (r - g) / delta + 4;

                h /= 6;
            }
        }

        private static Color HSLToRGB(float h, float s, float l)
        {
            float r, g, b;

            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                float HueToRGB(float p, float q, float t)
                {
                    if (t < 0) t += 1;
                    if (t > 1) t -= 1;
                    if (t < 1f / 6) return p + (q - p) * 6 * t;
                    if (t < 1f / 2) return q;
                    if (t < 2f / 3) return p + (q - p) * (2f / 3 - t) * 6;
                    return p;
                }

                float q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                float p = 2 * l - q;

                r = HueToRGB(p, q, h + 1f / 3);
                g = HueToRGB(p, q, h);
                b = HueToRGB(p, q, h - 1f / 3);
            }

            return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        #endregion

        #region Accessibility Support

        /// <summary>
        /// Enables high contrast mode support for accessibility compliance.
        /// </summary>
        public static void EnableHighContrastSupport(Form form)
        {
            if (form == null) return;

            // Check for high contrast mode
            bool isHighContrast = SystemInformation.HighContrast;

            if (isHighContrast)
            {
                ApplyHighContrastTheme(form);
            }

            // Monitor for high contrast changes
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == Microsoft.Win32.UserPreferenceCategory.Accessibility)
                {
                    if (SystemInformation.HighContrast)
                    {
                        ApplyHighContrastTheme(form);
                    }
                    else
                    {
                        // Reapply normal theme
                        ApplyProfessionalTheme(form);
                    }
                }
            };
        }

        private static void ApplyHighContrastTheme(Form form)
        {
            foreach (Control control in GetAllControls(form))
            {
                // High contrast colors
                control.BackColor = SystemColors.Window;
                control.ForeColor = SystemColors.WindowText;

                if (control is Button button)
                {
                    button.BackColor = SystemColors.ButtonFace;
                    button.ForeColor = SystemColors.ControlText;
                    button.FlatAppearance.BorderColor = SystemColors.WindowText;
                }
            }
        }

        private static IEnumerable<Control> GetAllControls(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                yield return control;
                foreach (Control child in GetAllControls(control))
                {
                    yield return child;
                }
            }
        }

        #endregion

        #region Sample Data Generation

        /// <summary>
        /// Generates professional sample data for UI demonstration and testing.
        /// </summary>
        public static class SampleData
        {
            public static List<ActivityItem> GenerateActivityData(int count = 20)
            {
                var activities = new List<ActivityItem>();
                var random = new Random();
                var actions = new[] { "UserLogin", "ReportGenerated", "DataExported", "SettingsChanged", "FileUploaded", "SyncCompleted" };
                var users = new[] { "john.doe@company.com", "jane.smith@company.com", "admin@company.com", "user@company.com" };

                for (int i = 0; i < count; i++)
                {
                    activities.Add(new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddMinutes(-random.Next(1, 1440)), // Last 24 hours
                        Activity = actions[random.Next(actions.Length)],
                        Details = $"Sample action {i + 1}",
                        User = users[random.Next(users.Length)]
                    });
                }

                return activities.OrderByDescending(a => a.Timestamp).ToList();
            }

            public static Dictionary<string, object> GenerateDashboardMetrics()
            {
                var random = new Random();
                return new Dictionary<string, object>
                {
                    ["TotalRevenue"] = random.Next(100000, 500000),
                    ["ActiveUsers"] = random.Next(1000, 5000),
                    ["ConversionRate"] = random.Next(25, 85) / 100.0,
                    ["GrowthRate"] = (random.Next(-50, 150) / 100.0)
                };
            }
        }

        #endregion
    }

    /// <summary>
    /// Simple activity item for demonstration.
    /// </summary>
    public class ActivityItem
    {
        public DateTime Timestamp { get; set; }
        public string Activity { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
    }
}
