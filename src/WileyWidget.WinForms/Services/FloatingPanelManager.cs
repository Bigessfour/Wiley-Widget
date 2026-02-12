using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WileyWidget.WinForms.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Manages floating windows for docked panels.
    /// Allows panels to be detached from the main docking container and floated as independent windows.
    /// Tier 3 Feature: Professional floating window support for advanced users.
    /// </summary>
    public class FloatingPanelManager : IDisposable
    {
        private readonly ILogger<FloatingPanelManager> _logger;
        private readonly Dictionary<string, FloatingPanelWindow> _floatingWindows = new();
        private readonly Form _parentForm;

        public FloatingPanelManager(Form parentForm, ILogger<FloatingPanelManager>? logger = null)
        {
            _parentForm = parentForm ?? throw new ArgumentNullException(nameof(parentForm));
            _logger = logger ?? NullLogger<FloatingPanelManager>.Instance;
        }

        /// <summary>
        /// Creates a floating window for a docked panel.
        /// </summary>
        /// <param name="panelName">Unique name for the panel</param>
        /// <param name="panelControl">Control to float</param>
        /// <param name="initialLocation">Initial window position</param>
        /// <param name="initialSize">Initial window size</param>
        /// <returns>The floating window instance</returns>
        public FloatingPanelWindow CreateFloatingPanel(
            string panelName,
            Control panelControl,
            Point initialLocation,
            Size initialSize)
        {
            if (string.IsNullOrWhiteSpace(panelName))
                throw new ArgumentException("Panel name required", nameof(panelName));
            if (panelControl == null)
                throw new ArgumentNullException(nameof(panelControl));

            if (_floatingWindows.ContainsKey(panelName))
            {
                _logger.LogWarning("Floating panel '{PanelName}' already exists, bringing to front", panelName);
                _floatingWindows[panelName].SafeInvoke(() => { _floatingWindows[panelName].BringToFront(); });
                return _floatingWindows[panelName];
            }

            try
            {
                var floatingWindow = new FloatingPanelWindow(panelName, panelControl, _parentForm, _logger)
                {
                    StartPosition = FormStartPosition.Manual,
                    Location = initialLocation,
                    Size = initialSize
                };

                floatingWindow.FormClosed += (s, e) =>
                {
                    _floatingWindows.Remove(panelName);
                    _logger.LogDebug("Floating panel '{PanelName}' closed", panelName);
                };

                _floatingWindows[panelName] = floatingWindow;
                floatingWindow.Show(_parentForm);

                _logger.LogInformation("Floating panel created: {PanelName}", panelName);
                return floatingWindow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create floating panel '{PanelName}'", panelName);
                throw;
            }
        }

        /// <summary>
        /// Gets a floating panel by name.
        /// </summary>
        public FloatingPanelWindow? GetFloatingPanel(string panelName)
        {
            return _floatingWindows.TryGetValue(panelName, out var window) ? window : null;
        }

        /// <summary>
        /// Closes a floating panel by name.
        /// </summary>
        public void CloseFloatingPanel(string panelName)
        {
            if (_floatingWindows.TryGetValue(panelName, out var window))
            {
                try
                {
                    window.Close();
                    window.Dispose();
                    _floatingWindows.Remove(panelName);
                    _logger.LogDebug("Floating panel closed: {PanelName}", panelName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing floating panel '{PanelName}'", panelName);
                }
            }
        }

        /// <summary>
        /// Closes all floating panels.
        /// </summary>
        public void CloseAll()
        {
            var panelNames = new List<string>(_floatingWindows.Keys);
            foreach (var name in panelNames)
            {
                CloseFloatingPanel(name);
            }
            _logger.LogDebug("All floating panels closed");
        }

        /// <summary>
        /// Gets all currently floating panels.
        /// </summary>
        public IReadOnlyDictionary<string, FloatingPanelWindow> GetAllFloatingPanels()
        {
            return _floatingWindows;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseAll();
            }
        }
    }

    /// <summary>
    /// A floating window that hosts a docked panel.
    /// Provides minimize, maximize, and close buttons.
    /// Automatically returns panel to parent when closed.
    /// </summary>
    public class FloatingPanelWindow : SfForm
    {
        private readonly Control _panelControl;
        private readonly Form _parentForm;
        private readonly ILogger _logger;
        private readonly Panel _contentPanel;

        public string PanelName { get; }

        internal FloatingPanelWindow(
            string panelName,
            Control panelControl,
            Form parentForm,
            ILogger logger)
        {
            PanelName = panelName;
            _panelControl = panelControl ?? throw new ArgumentNullException(nameof(panelControl));
            _parentForm = parentForm ?? throw new ArgumentNullException(nameof(parentForm));
            _logger = logger;

            // Configure window
            Text = panelName;
            Name = $"FloatingPanel_{panelName}";
            ShowIcon = false;
            ShowInTaskbar = true;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            MinimumSize = new Size(200, 150);
            Size = new Size(400, 300);

            // Apply Syncfusion theme (consistent with parent form)
            try
            {
                WileyWidget.WinForms.Themes.ThemeColors.ApplyTheme(this);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to apply theme to floating panel window '{PanelName}'", panelName);
            }

            this.Style.Border = new Pen(SystemColors.WindowFrame, 2);
            this.Style.InactiveBorder = new Pen(SystemColors.GrayText, 2);

            // Create container for the panel
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None
            };

            Controls.Add(_contentPanel);

            // Move panel to floating window
            _panelControl.Parent = _contentPanel;
            _panelControl.Dock = DockStyle.Fill;

            FormClosing += (s, e) =>
            {
                try
                {
                    // Return panel to parent when window closes
                    _panelControl.Parent = _parentForm;
                    _logger?.LogDebug("Panel '{PanelName}' returned to parent on window close", panelName);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error returning panel to parent");
                }
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _contentPanel.Focus();
            _panelControl.Focus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _contentPanel?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
