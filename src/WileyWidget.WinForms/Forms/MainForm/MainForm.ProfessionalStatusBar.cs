using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using AppThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Professional status bar with rich system information.
/// Shows memory usage, connection status, user info, row count, and time.
/// </summary>
public partial class MainForm
{
    private StatusBarAdvPanel? _memoryPanel;
    private StatusBarAdvPanel? _connectionPanel;
    private StatusBarAdvPanel? _userPanel;
    private StatusBarAdvPanel? _rowCountPanel;
    private System.Windows.Forms.Timer? _memoryUpdateTimer;
    private ToolTip? _statusBarToolTip;
    private bool _professionalStatusBarInitialized;

    /// <summary>
    /// Initializes professional status bar with system information panels.
    ///
    /// SYNCFUSION API: StatusBarAdv, StatusBarAdvPanel
    /// Reference: https://help.syncfusion.com/windowsforms/statusbar/overview
    /// </summary>
    private void InitializeProfessionalStatusBar()
    {
        if (_statusBar == null || _professionalStatusBarInitialized)
        {
            return;
        }

        try
        {
            _logger?.LogInformation("Initializing professional status bar");

            // Create tooltip for status panels
            _statusBarToolTip = new ToolTip
            {
                InitialDelay = 500,
                ReshowDelay = 100,
                AutoPopDelay = 5000,
                ShowAlways = true
            };

            // Add system information panels
            CreateMemoryPanel();
            CreateConnectionPanel();
            CreateRowCountPanel();
            CreateUserPanel();

            var panels = (_statusBar.Panels ?? Array.Empty<StatusBarAdvPanel>())
                .Where(panel => panel != null)
                .ToList();
            panels.RemoveAll(panel => panel.Name is "MemoryPanel" or "ConnectionPanel" or "RowCountPanel" or "UserPanel");

            var clockIndex = panels.FindIndex(panel => string.Equals(panel.Name, "ClockPanel", StringComparison.Ordinal));
            if (clockIndex < 0)
            {
                clockIndex = panels.Count;
            }

            panels.Insert(clockIndex, _rowCountPanel!);
            panels.Insert(clockIndex + 1, _connectionPanel!);
            panels.Insert(clockIndex + 2, _memoryPanel!);
            panels.Insert(clockIndex + 3, _userPanel!);

            _statusBar.Panels = panels.ToArray();
            _professionalStatusBarInitialized = true;

            // Capture an initial snapshot only. Repeated live updates on StatusBarAdv panels can
            // trigger excessive Syncfusion window-message churn in the RibbonForm shell.
            RefreshProfessionalStatusBarSnapshot();

            _logger?.LogInformation("Professional status bar initialized");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize professional status bar");
        }
    }

    /// <summary>
    /// Creates memory usage panel.
    /// </summary>
    private void CreateMemoryPanel()
    {
        _memoryPanel = new StatusBarAdvPanel
        {
            Name = "MemoryPanel",
            Text = "Memory: 0 MB",
            Width = 120,
            BorderStyle = BorderStyle.Fixed3D
        };

        _memoryPanel.Click += OnMemoryPanelClick;

        // Note: StatusBarAdv.Panels is a read-only array - panels are configured during design time or initialization
        _statusBarToolTip?.SetToolTip(_memoryPanel, "Application memory usage\nClick for detailed diagnostics");
    }

    /// <summary>
    /// Creates connection status panel.
    /// </summary>
    private void CreateConnectionPanel()
    {
        // ✅ Semantic status color exception: Connected=Green, Disconnected=Red.
        // See UpdateConnectionStatus() for the runtime toggle. Both colors are standard .NET
        // Color values used as connection-state indicators, not theme overrides.
        _connectionPanel = new StatusBarAdvPanel
        {
            Name = "ConnectionPanel",
            Text = "● Services Ready",
            Width = 130,
            BorderStyle = BorderStyle.Fixed3D,
            ForeColor = AppThemeColors.Success
        };

        _connectionPanel.Click += OnConnectionPanelClick;

        _statusBarToolTip?.SetToolTip(_connectionPanel, "Application service readiness\nClick for runtime details");
    }

    /// <summary>
    /// Creates row count panel for active grid.
    /// </summary>
    private void CreateRowCountPanel()
    {
        _rowCountPanel = new StatusBarAdvPanel
        {
            Name = "RowCountPanel",
            Text = "0 items",
            Width = 95,
            BorderStyle = BorderStyle.Fixed3D
        };

        _statusBarToolTip?.SetToolTip(_rowCountPanel, "Row count in active grid");
    }

    /// <summary>
    /// Creates user panel.
    /// </summary>
    private void CreateUserPanel()
    {
        var currentUserDisplayName = GetAuthenticatedUserDisplayName();

        _userPanel = new StatusBarAdvPanel
        {
            Name = "UserPanel",
            Text = $"👤 {currentUserDisplayName}",
            Width = 150,
            BorderStyle = BorderStyle.Fixed3D
        };

        _userPanel.Click += OnUserPanelClick;

        _statusBarToolTip?.SetToolTip(_userPanel, $"Current user: {currentUserDisplayName}\nClick for user menu");
    }

    private string GetAuthenticatedUserDisplayName()
    {
        var currentUser = _serviceProvider == null
            ? null
            : Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<WileyWidget.Services.Abstractions.IUserContext>(_serviceProvider)?.DisplayName;
        return string.IsNullOrWhiteSpace(currentUser) ? Environment.UserName : currentUser;
    }

    private void UpdateUserDisplay()
    {
        if (_userPanel == null || _userPanel.IsDisposed)
        {
            return;
        }

        var currentUserDisplayName = GetAuthenticatedUserDisplayName();
        _userPanel.Text = $"👤 {currentUserDisplayName}";
        _statusBarToolTip?.SetToolTip(_userPanel, $"Current user: {currentUserDisplayName}\nClick for user menu");
    }

    /// <summary>
    /// Starts memory update timer (updates every 5 seconds).
    /// </summary>
    private void StartMemoryUpdateTimer()
    {
        _memoryUpdateTimer = new System.Windows.Forms.Timer
        {
            Interval = 5000 // 5 seconds
        };

        _memoryUpdateTimer.Tick += (s, e) => RefreshProfessionalStatusBarSnapshot();
        _memoryUpdateTimer.Start();

        RefreshProfessionalStatusBarSnapshot();
    }

    /// <summary>
    /// Updates memory usage display.
    /// </summary>
    private void UpdateMemoryDisplay()
    {
        try
        {
            if (_memoryPanel == null || _memoryPanel.IsDisposed) return;

            var process = Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / 1024 / 1024;

            _memoryPanel.Text = $"Memory: {memoryMB} MB";

            // Semantic threshold colors (allowed exception — warning/error indicators).
            // In the normal state ResetForeColor() removes any prior explicit assignment
            // so SfSkinManager theme cascade governs the text color (Req 1 compliance).
            if (memoryMB > 1000) // Over 1 GB
            {
                _memoryPanel.ForeColor = AppThemeColors.Error;
            }
            else if (memoryMB > 500) // Over 500 MB
            {
                _memoryPanel.ForeColor = AppThemeColors.Warning;
            }
            else
            {
                _memoryPanel.ResetForeColor(); // Let SfSkinManager theme cascade govern color
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error updating memory display");
        }
    }

    /// <summary>
    /// Updates clock display.
    /// </summary>
    private void UpdateClockDisplay()
    {
        try
        {
            if (_clockPanel == null || _clockPanel.IsDisposed) return;

            _clockPanel.Text = DateTime.Now.ToString("h:mm:ss tt");
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error updating clock display");
        }
    }

    private void RefreshProfessionalStatusBarSnapshot()
    {
        if (!_professionalStatusBarInitialized)
        {
            return;
        }

        UpdateMemoryDisplay();
        UpdateRowCountDisplay(GetActiveGridRowCount());

        if (_hostedAuthenticationStartupFailed)
        {
            UpdateConnectionStatus(false, "Authentication unavailable");
        }
        else if (IsHostedAuthenticationPanelActive() || IsHostedAuthenticationPending())
        {
            UpdateConnectionStatus(false, "Authentication required");
        }
        else
        {
            UpdateConnectionStatus(_serviceProvider != null, _serviceProvider != null ? "Services ready" : "Services unavailable");
        }

        UpdateUserDisplay();
    }

    private int GetActiveGridRowCount()
    {
        try
        {
            var activeGrid = GetActiveGrid();
            if (activeGrid == null || activeGrid.IsDisposed)
            {
                return 0;
            }

            if (activeGrid.DataSource is BindingSource bindingSource)
            {
                return bindingSource.Count;
            }

            if (activeGrid.DataSource is ICollection collection)
            {
                return collection.Count;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to determine active grid row count");
        }

        return 0;
    }

    private void UpdatePrimaryStatusLabel(string statusText)
    {
        if (_statusLabel == null || _statusLabel.IsDisposed)
        {
            return;
        }

        var normalized = statusText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            _statusLabel.Text = "Ready";
            _statusLabel.ForeColor = AppThemeColors.Success;
            return;
        }

        if (normalized.Contains("error", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("unable", StringComparison.OrdinalIgnoreCase))
        {
            _statusLabel.Text = normalized;
            _statusLabel.ForeColor = AppThemeColors.Error;
            return;
        }

        if (normalized.Contains("warning", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("retry", StringComparison.OrdinalIgnoreCase))
        {
            _statusLabel.Text = normalized;
            _statusLabel.ForeColor = AppThemeColors.Warning;
            return;
        }

        if (normalized.Contains("loading", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("opening", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("initializing", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("refresh", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("import", StringComparison.OrdinalIgnoreCase))
        {
            _statusLabel.Text = normalized;
            _statusLabel.ResetForeColor();
            return;
        }

        _statusLabel.Text = normalized;
        _statusLabel.ForeColor = AppThemeColors.Success;
    }

    /// <summary>
    /// Updates row count display for active grid.
    /// </summary>
    private void UpdateRowCountDisplay(int rowCount)
    {
        try
        {
            if (_rowCountPanel == null || _rowCountPanel.IsDisposed) return;

            _rowCountPanel.Text = rowCount == 1 ? "1 item" : $"{rowCount:N0} items";
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error updating row count display");
        }
    }

    /// <summary>
    /// Updates connection status display.
    /// </summary>
    private void UpdateConnectionStatus(bool connected, string? message = null)
    {
        try
        {
            if (_connectionPanel == null || _connectionPanel.IsDisposed) return;

            if (connected)
            {
                _connectionPanel.Text = $"● {message ?? "Services Ready"}";
                _connectionPanel.ForeColor = AppThemeColors.Success;
            }
            else
            {
                _connectionPanel.Text = $"● {message ?? "Attention"}";
                _connectionPanel.ForeColor = AppThemeColors.Error;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error updating connection status");
        }
    }

    /// <summary>
    /// Handles memory panel click to show diagnostics.
    /// </summary>
    private void OnMemoryPanelClick(object? sender, EventArgs e)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var workingSet = process.WorkingSet64 / 1024 / 1024;
            var privateMemory = process.PrivateMemorySize64 / 1024 / 1024;
            var gcMemory = GC.GetTotalMemory(false) / 1024 / 1024;
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            var diagnostics = $@"Memory Diagnostics

Working Set: {workingSet} MB
Private Memory: {privateMemory} MB
GC Managed Memory: {gcMemory} MB

Garbage Collections:
  Gen 0: {gen0}
  Gen 1: {gen1}
  Gen 2: {gen2}

Threads: {process.Threads.Count}
Handles: {process.HandleCount}

Process ID: {process.Id}
Start Time: {process.StartTime}
CPU Time: {process.TotalProcessorTime}";

            MessageBoxAdv.Show(this, diagnostics, "Memory Diagnostics",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            _logger?.LogDebug("Displayed memory diagnostics");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error showing memory diagnostics");
        }
    }

    /// <summary>
    /// Handles connection panel click to show connection details.
    /// </summary>
    private void OnConnectionPanelClick(object? sender, EventArgs e)
    {
        try
        {
            var details = $@"Runtime Status

Services: {(_serviceProvider != null ? "Ready" : "Unavailable")}
Theme: {SfSkinManager.ApplicationVisualTheme ?? AppThemeColors.DefaultTheme}
UI Test Harness: {_uiConfig.IsUiTestHarness}
Automation Mode: {IsUiAutomationMode()}

Click OK to refresh the status snapshot.";

            var result = MessageBoxAdv.Show(this, details, "Connection Status",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

            if (result == DialogResult.OK)
            {
                RefreshProfessionalStatusBarSnapshot();
                _logger?.LogDebug("Professional status snapshot refreshed");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error showing connection details");
        }
    }

    /// <summary>
    /// Handles user panel click to show user menu.
    /// </summary>
    private void OnUserPanelClick(object? sender, EventArgs e)
    {
        try
        {
            var currentUserDisplayName = GetAuthenticatedUserDisplayName();
            var userMenu = new ContextMenuStrip();

            userMenu.Items.Add($"User: {currentUserDisplayName}").Enabled = false;
            userMenu.Items.Add(new ToolStripSeparator());
            userMenu.Items.Add("Profile Settings", null, (s, args) =>
            {
                _logger?.LogDebug("Profile settings clicked");
                ShowPanel<Controls.Panels.SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true);
                ApplyStatus("Opened profile settings.");
            });
            userMenu.Items.Add("Change Password", null, (s, args) =>
            {
                _logger?.LogDebug("Change password clicked");
                ShowPanel<Controls.Panels.SettingsPanel>("Settings", DockingStyle.Right, allowFloating: true);
                ApplyStatus("Open Settings to manage security options.");
            });
            userMenu.Items.Add(new ToolStripSeparator());
            userMenu.Items.Add("Sign Out", null, (s, args) =>
            {
                _logger?.LogDebug("Sign out clicked");
                this.Close();
            });

            // Show menu at cursor position
            userMenu.Show(Cursor.Position);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error showing user menu");
        }
    }

    /// <summary>
    /// Stops and disposes status bar timers.
    /// </summary>
    private void DisposeStatusBarTimers()
    {
        try
        {
            _memoryUpdateTimer?.Stop();
            _memoryUpdateTimer?.Dispose();
            _memoryUpdateTimer = null;

            _statusBarToolTip?.Dispose();
            _statusBarToolTip = null;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error disposing status bar timers");
        }
    }
}
