using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.Windows.Forms.Tools;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Provides keyboard navigation support for docked panels.
    /// Tier 3 Feature: Professional keyboard navigation (Alt+Arrow keys, Tab cycles panels).
    /// Improves accessibility and productivity for power users.
    /// </summary>
    public class DockingKeyboardNavigator
    {
        private readonly DockingManager _dockingManager;
        private readonly ILogger<DockingKeyboardNavigator> _logger;
        private readonly List<Panel> _dockedPanels = new();

        public DockingKeyboardNavigator(DockingManager dockingManager, ILogger<DockingKeyboardNavigator>? logger = null)
        {
            _dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));
            _logger = logger ?? NullLogger<DockingKeyboardNavigator>.Instance;
        }

        /// <summary>
        /// Registers a panel for keyboard navigation.
        /// </summary>
        public void RegisterPanel(Panel panel)
        {
            if (panel == null) return;

            if (!_dockedPanels.Contains(panel))
            {
                _dockedPanels.Add(panel);
                _logger.LogDebug("Panel registered for keyboard navigation: {PanelName}", panel.Name);
            }
        }

        /// <summary>
        /// Unregisters a panel from keyboard navigation.
        /// </summary>
        public void UnregisterPanel(Panel panel)
        {
            if (panel != null && _dockedPanels.Remove(panel))
            {
                _logger.LogDebug("Panel unregistered from keyboard navigation: {PanelName}", panel.Name);
            }
        }

        /// <summary>
        /// Handles keyboard navigation commands.
        /// Returns true if the key was handled, false otherwise.
        /// </summary>
        public bool HandleKeyboardCommand(Keys keyData)
        {
            // Alt+Left: Activate panel to the left
            if (keyData == (Keys.Alt | Keys.Left))
            {
                ActivateAdjacentPanel(DockingStyle.Left);
                return true;
            }

            // Alt+Right: Activate panel to the right
            if (keyData == (Keys.Alt | Keys.Right))
            {
                ActivateAdjacentPanel(DockingStyle.Right);
                return true;
            }

            // Alt+Up: Activate panel above
            if (keyData == (Keys.Alt | Keys.Up))
            {
                ActivateAdjacentPanel(DockingStyle.Top);
                return true;
            }

            // Alt+Down: Activate panel below
            if (keyData == (Keys.Alt | Keys.Down))
            {
                ActivateAdjacentPanel(DockingStyle.Bottom);
                return true;
            }

            // Alt+Tab: Cycle through panels
            if (keyData == (Keys.Alt | Keys.Tab))
            {
                CycleNextPanel();
                return true;
            }

            // Shift+Alt+Tab: Cycle through panels in reverse
            if (keyData == (Keys.Alt | Keys.Shift | Keys.Tab))
            {
                CyclePreviousPanel();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Activates an adjacent panel in the specified direction.
        /// </summary>
        private void ActivateAdjacentPanel(DockingStyle direction)
        {
            try
            {
                var activeControl = _dockingManager.ActiveControl;
                if (activeControl == null)
                {
                    // No active control, activate first panel
                    if (_dockedPanels.Count > 0)
                    {
                        _dockingManager.SetActiveControl(_dockedPanels[0]);
                        _logger.LogDebug("Activated first panel: {PanelName}", _dockedPanels[0].Name);
                    }
                    return;
                }

                // Find panels in the requested direction
                var targetPanel = FindPanelInDirection(activeControl, direction);
                if (targetPanel != null)
                {
                    _dockingManager.SetActiveControl(targetPanel);
                    _logger.LogDebug("Activated adjacent panel: {Direction} -> {PanelName}", direction, targetPanel.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to activate adjacent panel");
            }
        }

        /// <summary>
        /// Cycles to the next docked panel.
        /// </summary>
        private void CycleNextPanel()
        {
            if (_dockedPanels.Count == 0) return;

            try
            {
                var activeControl = _dockingManager.ActiveControl;
                var currentIndex = _dockedPanels.IndexOf(activeControl as Panel);
                var nextIndex = (currentIndex + 1) % _dockedPanels.Count;

                _dockingManager.SetActiveControl(_dockedPanels[nextIndex]);
                _logger.LogDebug("Cycled to next panel: {PanelName}", _dockedPanels[nextIndex].Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cycle to next panel");
            }
        }

        /// <summary>
        /// Cycles to the previous docked panel.
        /// </summary>
        private void CyclePreviousPanel()
        {
            if (_dockedPanels.Count == 0) return;

            try
            {
                var activeControl = _dockingManager.ActiveControl;
                var currentIndex = _dockedPanels.IndexOf(activeControl as Panel);
                var prevIndex = (currentIndex - 1 + _dockedPanels.Count) % _dockedPanels.Count;

                _dockingManager.SetActiveControl(_dockedPanels[prevIndex]);
                _logger.LogDebug("Cycled to previous panel: {PanelName}", _dockedPanels[prevIndex].Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cycle to previous panel");
            }
        }

        /// <summary>
        /// Finds a panel in the specified direction from the active control.
        /// </summary>
        private Panel? FindPanelInDirection(Control currentControl, DockingStyle direction)
        {
            if (currentControl == null) return null;

            var currentBounds = currentControl.Bounds;

            return direction switch
            {
                DockingStyle.Left => _dockedPanels
                    .Where(p => p.Right <= currentBounds.Left)
                    .OrderByDescending(p => p.Right)
                    .FirstOrDefault(),

                DockingStyle.Right => _dockedPanels
                    .Where(p => p.Left >= currentBounds.Right)
                    .OrderBy(p => p.Left)
                    .FirstOrDefault(),

                DockingStyle.Top => _dockedPanels
                    .Where(p => p.Bottom <= currentBounds.Top)
                    .OrderByDescending(p => p.Bottom)
                    .FirstOrDefault(),

                DockingStyle.Bottom => _dockedPanels
                    .Where(p => p.Top >= currentBounds.Bottom)
                    .OrderBy(p => p.Top)
                    .FirstOrDefault(),

                _ => null
            };
        }

        /// <summary>
        /// Gets all registered panels.
        /// </summary>
        public IReadOnlyList<Panel> GetRegisteredPanels() => _dockedPanels.AsReadOnly();
    }
}
