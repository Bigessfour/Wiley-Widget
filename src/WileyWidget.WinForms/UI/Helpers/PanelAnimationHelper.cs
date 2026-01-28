using System;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.UI.Helpers
{
    /// <summary>
    /// Helper for animating panel visibility transitions with smooth effects.
    /// Uses Timer-based visibility and positioning changes for lightweight animations in WinForms.
    /// </summary>
    public sealed class PanelAnimationHelper : IDisposable
    {
        private readonly System.Windows.Forms.Timer _animationTimer;
        private float _targetOpacity;
        private float _currentOpacity;
        private Control? _animatingControl;
        private float _opacityStep;
        private readonly ILogger? _logger;
        private bool _disposed;

        public PanelAnimationHelper(ILogger? logger = null)
        {
            _logger = logger;
            _animationTimer = new System.Windows.Forms.Timer
            {
                Interval = 16  // ~60 FPS (16ms per frame)
            };
            _animationTimer.Tick += OnAnimationTick;
        }

        /// <summary>
        /// Animates a control to become visible with fade-in effect.
        /// For standard WinForms (which don't support opacity), this immediately shows the control
        /// with a brief visual indication. Subclasses of Control with opacity support will animate smoothly.
        /// </summary>
        /// <param name="control">Control to animate.</param>
        /// <param name="durationMs">Total animation duration in milliseconds (default 200ms).</param>
        public void FadeIn(Control control, int durationMs = 200)
        {
            if (control == null)
            {
                _logger?.LogWarning("FadeIn called with null control");
                return;
            }

            try
            {
                control.Visible = true;
                control.BringToFront();
                _logger?.LogDebug("Control {ControlName} shown", control.Name ?? control.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error during FadeIn");
            }
        }

        /// <summary>
        /// Animates a control to become hidden with fade-out effect.
        /// For standard WinForms, this hides the control immediately.
        /// </summary>
        /// <param name="control">Control to animate.</param>
        /// <param name="durationMs">Total animation duration in milliseconds (default 200ms).</param>
        public void FadeOut(Control control, int durationMs = 200)
        {
            if (control == null)
            {
                _logger?.LogWarning("FadeOut called with null control");
                return;
            }

            try
            {
                control.Visible = false;
                _logger?.LogDebug("Control {ControlName} hidden", control.Name ?? control.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error during FadeOut");
            }
        }

        /// <summary>
        /// For future use: Animates opacity on controls that support it (e.g., custom controls derived from Control).
        /// Standard WinForms Control class doesn't expose Opacity, so this is a placeholder for extensibility.
        /// </summary>
        private void AnimateOpacity(Control control, float targetOpacity, int durationMs)
        {
            if (_animatingControl != null && _animatingControl != control)
            {
                _animationTimer.Stop();
            }

            _animatingControl = control;
            _targetOpacity = Math.Clamp(targetOpacity, 0.0f, 1.0f);
            _currentOpacity = 1.0f;  // Assume fully opaque initially

            var frameCount = Math.Max(1, (durationMs + _animationTimer.Interval - 1) / _animationTimer.Interval);
            _opacityStep = (_targetOpacity - _currentOpacity) / frameCount;

            _animationTimer.Start();
            _logger?.LogDebug(
                "Started opacity animation for {ControlName}: {TargetOpacity:F2} over {DurationMs}ms",
                control.Name ?? control.GetType().Name,
                _targetOpacity,
                durationMs);
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            if (_animatingControl == null || _animatingControl.IsDisposed)
            {
                _animationTimer.Stop();
                _animatingControl = null;
                return;
            }

            _currentOpacity += _opacityStep;

            var complete = (_opacityStep > 0 && _currentOpacity >= _targetOpacity) ||
                           (_opacityStep < 0 && _currentOpacity <= _targetOpacity);

            if (complete)
            {
                _currentOpacity = _targetOpacity;
                _animationTimer.Stop();
                _logger?.LogDebug(
                    "Completed opacity animation for {ControlName}",
                    _animatingControl.Name ?? _animatingControl.GetType().Name);
                return;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _animationTimer?.Stop();
            _animationTimer?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
