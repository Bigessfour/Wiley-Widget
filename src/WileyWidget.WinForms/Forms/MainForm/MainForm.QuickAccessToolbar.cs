using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Quick Access Toolbar (QAT) implementation for RibbonControlAdv.
/// Provides frequently-used commands above the ribbon tabs.
///
/// SYNCFUSION API: RibbonControlAdv.QuickPanel
/// Reference: https://help.syncfusion.com/windowsforms/ribbon/quick-access-toolbar
/// </summary>
public partial class MainForm
{
    /// <summary>
    /// Initializes Quick Access Toolbar with common commands.
    /// </summary>
    private void InitializeQuickAccessToolbar(string theme, bool isUiTestRuntime)
    {
        if (_ribbon?.Header == null)
        {
            _logger?.LogWarning("Cannot initialize QAT - ribbon is null");
            return;
        }

        try
        {
            if (isUiTestRuntime)
            {
                _ribbon.QuickPanelVisible = false;
                _ribbon.ShowQuickItemsDropDownButton = false;
                return;
            }

            _ribbon.QuickPanelVisible = true;
            _ribbon.ShowQuickItemsDropDownButton = true;

            var buttons = CreateDefaultQuickAccessToolbarButtons(theme);
            foreach (var button in buttons.Where(static b => b != null && b.Enabled))
            {
                try
                {
                    _ribbon.Header.AddQuickItem(button);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to add quick access button {ButtonName}", button.Name);
                }
            }

            _logger?.LogDebug("Quick Access Toolbar initialized with {ButtonCount} buttons", buttons.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Quick Access Toolbar");
        }
    }

    /// <summary>
    /// Creates default Quick Access Toolbar buttons with icons and tooltips.
    /// </summary>
    private ToolStripButton[] CreateDefaultQuickAccessToolbarButtons(string theme)
    {
        var iconColor = ResolveQuickAccessIconColor(theme);

        var saveButton = new ToolStripButton
        {
            Name = "QAT_Save",
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = CreateIconFromSegoeGlyph("\uE74E", 16, iconColor),
            ToolTipText = "Save layout (Ctrl+Shift+S)",
            AutoSize = true,
            Enabled = true
        };
#pragma warning disable CS0618
        saveButton.Click += (_, _) => SafeExecute((RibbonCommand)SaveCurrentLayout, "QAT_SaveLayout", _logger);
#pragma warning restore CS0618

        var vitalSignsButton = new ToolStripButton
        {
            Name = "QAT_VitalSigns",
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = CreateIconFromSegoeGlyph("\uE80F", 16, iconColor),
            ToolTipText = "Open Enterprise Vital Signs",
            AutoSize = true,
            Enabled = true
        };
        vitalSignsButton.Click += (_, _) =>
        {
            var vitalSignsEntry = PanelRegistry.Panels.FirstOrDefault(p => p.DisplayName == "Enterprise Vital Signs");
            if (vitalSignsEntry != null)
            {
                SafeNavigate(this, "Enterprise Vital Signs", CreatePanelNavigationCommand(this, vitalSignsEntry, _logger), _logger);
            }
        };

        var budgetButton = new ToolStripButton
        {
            Name = "QAT_Budget",
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = CreateIconFromSegoeGlyph("\uE8F0", 16, iconColor),
            ToolTipText = "Budget Management",
            AutoSize = true,
            Enabled = true
        };
        budgetButton.Click += (_, _) =>
        {
            var budgetEntry = PanelRegistry.Panels.FirstOrDefault(p => p.DisplayName == "Budget Management & Analysis");
            if (budgetEntry != null)
            {
                SafeNavigate(this, "Budget Management & Analysis", CreatePanelNavigationCommand(this, budgetEntry, _logger), _logger);
                return;
            }

            SafeNavigate(
                this,
                "Budget Management & Analysis",
                () => ShowPanel<WileyWidget.WinForms.Controls.Panels.BudgetPanel>("Budget Management & Analysis", DockingStyle.Right, allowFloating: true),
                _logger);
        };

        var settingsButton = new ToolStripButton
        {
            Name = "QAT_Settings",
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = CreateIconFromSegoeGlyph("\uE713", 16, iconColor),
            ToolTipText = "Settings (Ctrl+Alt+S)",
            AutoSize = true,
            Enabled = true
        };
        settingsButton.Click += (_, _) =>
        {
            var settingsEntry = PanelRegistry.Panels.FirstOrDefault(p => p.DisplayName == "Settings");
            if (settingsEntry != null)
            {
                SafeNavigate(this, "Settings", CreatePanelNavigationCommand(this, settingsEntry, _logger), _logger);
            }
        };

        try { SetAutomationId(saveButton, saveButton.Name, _logger); } catch (Exception ex) { _logger?.LogDebug(ex, "Failed to set AutomationId for QAT_Save"); }
        try { SetAutomationId(vitalSignsButton, vitalSignsButton.Name, _logger); } catch (Exception ex) { _logger?.LogDebug(ex, "Failed to set AutomationId for QAT_VitalSigns"); }
        try { SetAutomationId(budgetButton, budgetButton.Name, _logger); } catch (Exception ex) { _logger?.LogDebug(ex, "Failed to set AutomationId for QAT_Budget"); }
        try { SetAutomationId(settingsButton, settingsButton.Name, _logger); } catch (Exception ex) { _logger?.LogDebug(ex, "Failed to set AutomationId for QAT_Settings"); }

        return new[] { saveButton, vitalSignsButton, budgetButton, settingsButton };
    }

    private static Color ResolveQuickAccessIconColor(string theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return SystemColors.ControlText;
        }

        if (theme.Contains("dark", StringComparison.OrdinalIgnoreCase)
            || theme.Contains("black", StringComparison.OrdinalIgnoreCase)
            || theme.Contains("highcontrast", StringComparison.OrdinalIgnoreCase))
        {
            return Color.WhiteSmoke;
        }

        return SystemColors.ControlText;
    }

}
