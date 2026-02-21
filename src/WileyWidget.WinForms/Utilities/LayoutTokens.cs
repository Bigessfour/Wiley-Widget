using System;
using System.Drawing;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Utilities
{
    /// <summary>
    /// Shared layout constants for spacing, sizing, and DPI-aware scaling.
    /// All hardcoded heights, paddings, and margins throughout the panel layer
    /// should be expressed via these tokens so values stay consistent and
    /// DPI-correct across every display configuration.
    /// </summary>
    public static class LayoutTokens
    {
        // ── DPI scale (cached at type initialisation, single read per process) ──

        private static readonly float _dpiScale = ComputeDpiScale();

        private static float ComputeDpiScale()
        {
            try
            {
                using var g = Graphics.FromHwnd(IntPtr.Zero);
                return g.DpiX / 96f;
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>
        /// Converts a logical pixel value (at 96 DPI) to physical pixels at the
        /// primary display scale factor.
        /// </summary>
        public static int Dp(int px) => (int)Math.Round(px * _dpiScale);

        // ── Spacing tokens (logical pixels — pass through Dp() for physical) ──

        /// <summary>Standard inner padding for panels and containers.</summary>
        public const int PanelPadding = 12;

        /// <summary>Standard panel / section header height.</summary>
        public const int HeaderHeight = 48;

        /// <summary>Standard action button height.</summary>
        public const int ButtonHeight = 32;

        /// <summary>Standard text input / combo height.</summary>
        public const int StandardControlHeight = 28;

        /// <summary>Margin between major content blocks.</summary>
        public const int ContentMargin = 16;
    }
}
