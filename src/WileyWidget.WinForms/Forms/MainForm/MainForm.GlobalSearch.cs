using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.ListView;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Services;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Global search functionality for MainForm.
/// Searches across panels, commands, and data using Ctrl+K.
/// </summary>
public partial class MainForm
{
    private Form? _searchDialog;
    private TextBoxExt? _globalSearchBox;
    private SfListView? _searchResultsList;

    /// <summary>
    /// Shows the global search dialog (Ctrl+K).
    /// </summary>
    private void ShowGlobalSearchDialog()
    {
        try
        {
            if (_searchDialog != null && !_searchDialog.IsDisposed)
            {
                _searchDialog.Activate();
                _globalSearchBox?.Focus();
                return;
            }

            _logger?.LogDebug("Opening global search dialog");

            // Create search dialog
            _searchDialog = new Form
            {
                Text = "Search Everything (Ctrl+K)",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(600, 400),
                FormBorderStyle = FormBorderStyle.SizableToolWindow,
                ShowIcon = false,
                ShowInTaskbar = false,
                MinimumSize = new Size(400, 300)
            };

            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            SfSkinManager.SetVisualStyle(_searchDialog, currentTheme);

            // Search box (created via SyncfusionControlFactory)
            _globalSearchBox = _controlFactory?.CreateTextBoxExt(textBox =>
            {
                textBox.Dock = DockStyle.Top;
                textBox.Height = 40;
                textBox.Font = new Font("Segoe UI", 12F, FontStyle.Regular);
            });

            if (_globalSearchBox == null)
            {
                throw new InvalidOperationException("SyncfusionControlFactory is not available for global search");
            }

            _globalSearchBox.TextChanged += async (s, e) => await PerformGlobalSearchDialogAsync(_globalSearchBox.Text);
            _globalSearchBox.KeyDown += OnSearchBoxKeyDown;

            _searchDialog.Controls.Add(_globalSearchBox);

            // Results list (created via SyncfusionControlFactory)
            _searchResultsList = _controlFactory?.CreateSfListView(listView =>
            {
                listView.Dock = DockStyle.Fill;
                listView.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            });

            if (_searchResultsList == null)
            {
                throw new InvalidOperationException("SyncfusionControlFactory is not available for global search");
            }

            _searchDialog.Controls.Add(_searchResultsList);

            _searchDialog.FormClosed += (s, e) =>
            {
                _searchDialog = null;
                _globalSearchBox = null;
                _searchResultsList = null;
            };

            _searchDialog.Show(this);
            _globalSearchBox.Focus();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error showing global search dialog");
        }
    }

    /// <summary>
    /// Performs global search across panels, commands, and data.
    /// </summary>
    private async Task PerformGlobalSearchDialogAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || _searchResultsList == null)
        {
            return;
        }

        try
        {
            var results = new List<SearchResult>();

            // Search panels
            var panelResults = PanelRegistry.Panels
                .Where(p => p.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(p => new SearchResult
                {
                    Name = p.DisplayName,
                    Type = "Panel",
                    Description = $"Open {p.DisplayName} in {p.DefaultGroup} group",
                    Action = () => ShowPanel(p.PanelType, p.DisplayName, p.DefaultDock)
                })
                .ToList();

            results.AddRange(panelResults);

            // Search ribbon commands (simplified - expand based on your needs)
            if ("settings".Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new SearchResult
                {
                    Name = "Application Settings",
                    Type = "Command",
                    Description = "Open application settings panel",
                    Action = () => _panelNavigator?.ShowPanel<Controls.Panels.SettingsPanel>("Settings")
                });
            }

            if ("save".Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new SearchResult
                {
                    Name = "Save Layout",
                    Type = "Command",
                    Description = "Save current workspace layout",
                    Action = () => SaveWorkspaceLayout()
                });
            }

            if ("reset".Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new SearchResult
                {
                    Name = "Reset Layout",
                    Type = "Command",
                    Description = "Reset workspace to default layout",
                    Action = () => ResetLayoutToDefault()
                });
            }

            _logger?.LogDebug("Global search returned {Count} results for query: {Query}", results.Count, query);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error performing global search");
        }
    }

    /// <summary>
    /// Handles search box key down events.
    /// </summary>
    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_searchResultsList == null) return;

        // Handle key events
        if (e.KeyCode == Keys.Escape)
        {
            _searchDialog?.Close();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles search result double-click to execute action.
    /// </summary>
    private void OnSearchResultDoubleClick(object? sender, EventArgs e)
    {
        try
        {
            _logger?.LogDebug("Search result selected");
            _searchDialog?.Close();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing search result action");
        }
    }

    /// <summary>
    /// Represents a search result item.
    /// </summary>
    private class SearchResult
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public System.Action? Action { get; set; }
    }
}
