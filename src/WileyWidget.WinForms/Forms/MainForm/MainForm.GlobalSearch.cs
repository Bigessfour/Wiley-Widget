using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
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
    private AutoComplete? _globalSearchAutoComplete;
    private System.Windows.Forms.Timer? _globalSearchDebounceTimer;
    private readonly HashSet<string> _globalSearchSuggestions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SearchResult> _searchDialogResults = new();
    private string[] _globalSearchSuggestionSnapshot = Array.Empty<string>();
    private string _pendingGlobalSearchQuery = string.Empty;
    private long _globalSearchRequestVersion;

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
                Size = new Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(600f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(400f)),
                FormBorderStyle = FormBorderStyle.SizableToolWindow,
                ShowIcon = false,
                ShowInTaskbar = false,
                MinimumSize = new Size(
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(400f),
                    (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(300f))
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

            _globalSearchBox.TextChanged += OnGlobalSearchTextChanged;
            _globalSearchBox.KeyDown += OnSearchBoxKeyDown;

            _searchDialog.Controls.Add(_globalSearchBox);
            InitializeGlobalSearchAutoComplete();

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

            _searchResultsList.DoubleClick += OnSearchResultDoubleClick;
            _searchResultsList.KeyDown += OnSearchResultsKeyDown;

            _searchDialog.FormClosed += (s, e) =>
            {
                ResetGlobalSearchDialogResources();
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
    private async Task PerformGlobalSearchDialogAsync(string query, long requestVersion = 0)
    {
        if (_searchResultsList == null)
        {
            return;
        }

        try
        {
            var normalizedQuery = query?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                if (requestVersion != 0 && requestVersion != Volatile.Read(ref _globalSearchRequestVersion))
                {
                    return;
                }

                _searchDialogResults.Clear();
                _searchResultsList.DataSource = Array.Empty<string>();
                return;
            }

            List<SearchResult> results = new();

            using var scope = _serviceProvider?.CreateScope();
            var searchService = scope != null
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IGlobalSearchService>(scope.ServiceProvider)
                : null;

            if (searchService != null)
            {
                var globalResult = await searchService.SearchAsync(normalizedQuery).ConfigureAwait(true);
                if (requestVersion != 0 && requestVersion != Volatile.Read(ref _globalSearchRequestVersion))
                {
                    return;
                }

                results = globalResult.Matches
                    .Select(match => new SearchResult
                    {
                        Name = match.Title,
                        Type = match.Category,
                        Description = match.Description,
                        Action = BuildSearchAction(match)
                    })
                    .OrderBy(result => result.Type)
                    .ThenBy(result => result.Name)
                    .ToList();
            }
            else
            {
                _logger?.LogWarning("Global search service is unavailable; no results can be produced for '{Query}'", normalizedQuery);
            }

            _searchDialogResults.Clear();
            _searchDialogResults.AddRange(results);
            RefreshGlobalSearchSuggestions(results, normalizedQuery);

            var displayRows = results.Count == 0
                ? new List<string> { "No matches found." }
                : results.Select(result => result.DisplayText).ToList();

            _searchResultsList.DataSource = displayRows;
            if (results.Count == 1)
            {
                _searchResultsList.SelectedIndex = 0;
            }
            _logger?.LogDebug("Global search dialog rendered {Count} results for query: {Query}", results.Count, normalizedQuery);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error performing global search");
        }
    }

    private void InitializeGlobalSearchAutoComplete()
    {
        if (_searchDialog == null || _globalSearchBox == null)
        {
            return;
        }

        try
        {
            _globalSearchAutoComplete = _controlFactory?.CreateAutoComplete(autoComplete =>
            {
                autoComplete.ParentForm = _searchDialog;
                autoComplete.MatchMode = AutoCompleteMatchModes.Automatic;
            });

            if (_globalSearchAutoComplete == null)
            {
                _logger?.LogWarning("SyncfusionControlFactory did not provide AutoComplete for global search dialog");
                return;
            }

            foreach (var suggestion in BuildInitialGlobalSearchSuggestions())
            {
                _globalSearchSuggestions.Add(suggestion);
            }

            RefreshGlobalSearchAutoCompleteDataSource();
            _globalSearchAutoComplete.SetAutoComplete(_globalSearchBox, AutoCompleteModes.AutoSuggest);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize global search AutoComplete; global search will continue without suggestions");
        }
    }

    private void RefreshGlobalSearchSuggestions(IEnumerable<SearchResult> results, string query)
    {
        if (_globalSearchAutoComplete == null)
        {
            return;
        }

        var suggestionsChanged = false;

        if (!string.IsNullOrWhiteSpace(query))
        {
            suggestionsChanged |= _globalSearchSuggestions.Add(query.Trim());
        }

        foreach (var result in results)
        {
            if (!string.IsNullOrWhiteSpace(result.Name))
            {
                suggestionsChanged |= _globalSearchSuggestions.Add(result.Name);
            }

            if (!string.IsNullOrWhiteSpace(result.Description) && result.Description.Length <= 120)
            {
                suggestionsChanged |= _globalSearchSuggestions.Add(result.Description);
            }
        }

        if (suggestionsChanged)
        {
            RefreshGlobalSearchAutoCompleteDataSource();
        }
    }

    private void OnGlobalSearchTextChanged(object? sender, EventArgs e)
    {
        QueueGlobalSearchDialogSearch(_globalSearchBox?.Text ?? string.Empty);
    }

    private async Task EnsureSearchDialogVisibleAsync(string query)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return;
        }

        if (_searchDialog == null || _searchDialog.IsDisposed)
        {
            ShowGlobalSearchDialog();
        }

        if (_searchDialog == null || _searchDialog.IsDisposed || _globalSearchBox == null)
        {
            return;
        }

        if (!string.Equals(_globalSearchBox.Text, normalizedQuery, StringComparison.Ordinal))
        {
            _globalSearchBox.TextChanged -= OnGlobalSearchTextChanged;
            try
            {
                _globalSearchBox.Text = normalizedQuery;
            }
            finally
            {
                _globalSearchBox.TextChanged += OnGlobalSearchTextChanged;
            }
        }

        await PerformGlobalSearchDialogAsync(normalizedQuery).ConfigureAwait(true);

        if (_searchResultsList != null && _searchDialogResults.Count > 0 && _searchResultsList.SelectedIndex < 0)
        {
            _searchResultsList.SelectedIndex = 0;
        }

        _searchDialog.Activate();
        _globalSearchBox.Focus();
        _globalSearchBox.SelectAll();
    }

    private void QueueGlobalSearchDialogSearch(string query)
    {
        _pendingGlobalSearchQuery = query;
        EnsureGlobalSearchDebounceTimer();
        _globalSearchDebounceTimer?.Stop();
        _globalSearchDebounceTimer?.Start();
    }

    private void EnsureGlobalSearchDebounceTimer()
    {
        if (_globalSearchDebounceTimer != null)
        {
            return;
        }

        _globalSearchDebounceTimer = new System.Windows.Forms.Timer
        {
            Interval = 180,
        };
        _globalSearchDebounceTimer.Tick += async (_, _) => await FlushQueuedGlobalSearchAsync().ConfigureAwait(true);
    }

    private async Task FlushQueuedGlobalSearchAsync()
    {
        if (_searchResultsList == null)
        {
            return;
        }

        _globalSearchDebounceTimer?.Stop();
        var requestVersion = Interlocked.Increment(ref _globalSearchRequestVersion);
        await PerformGlobalSearchDialogAsync(_pendingGlobalSearchQuery, requestVersion).ConfigureAwait(true);
    }

    private void RefreshGlobalSearchAutoCompleteDataSource()
    {
        if (_globalSearchAutoComplete == null)
        {
            return;
        }

        var orderedSuggestions = _globalSearchSuggestions
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (_globalSearchSuggestionSnapshot.SequenceEqual(orderedSuggestions, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _globalSearchSuggestionSnapshot = orderedSuggestions;
        _globalSearchAutoComplete.DataSource = orderedSuggestions;
    }

    private void ResetGlobalSearchDialogResources()
    {
        _globalSearchDebounceTimer?.Stop();
        _globalSearchDebounceTimer?.Dispose();
        _globalSearchDebounceTimer = null;

        _globalSearchAutoComplete?.Dispose();
        _globalSearchAutoComplete = null;
        _searchDialog = null;
        _globalSearchBox = null;
        _searchResultsList = null;
        _globalSearchSuggestions.Clear();
        _globalSearchSuggestionSnapshot = Array.Empty<string>();
        _searchDialogResults.Clear();
        _pendingGlobalSearchQuery = string.Empty;
        Interlocked.Exchange(ref _globalSearchRequestVersion, 0);
    }

    private void DisposeGlobalSearchResources()
    {
        ResetGlobalSearchDialogResources();
    }

    private IEnumerable<string> BuildInitialGlobalSearchSuggestions()
    {
        var panelSuggestions = PanelRegistry.GetTownReleasePanels()
            .SelectMany(panel => new[] { panel.DisplayName, panel.DefaultGroup })
            .Where(value => !string.IsNullOrWhiteSpace(value));

        return panelSuggestions
            .Concat(new[]
            {
                "Dashboard",
                "Reports",
                "Accounts",
                "Rates",
                "QuickBooks",
                "Settings"
            })
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Handles search box key down events.
    /// </summary>
    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_searchResultsList == null) return;

        // Handle key events
        if (e.KeyCode == Keys.Enter)
        {
            _ = ExecuteQueuedGlobalSearchAndSelectionAsync();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            _searchDialog?.Close();
            e.Handled = true;
        }
    }

    private void OnSearchResultsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        ExecuteSelectedSearchResult();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    /// <summary>
    /// Handles search result double-click to execute action.
    /// </summary>
    private void OnSearchResultDoubleClick(object? sender, EventArgs e)
    {
        try
        {
            ExecuteSelectedSearchResult();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing search result action");
        }
    }

    private async Task ExecuteQueuedGlobalSearchAndSelectionAsync()
    {
        await FlushQueuedGlobalSearchAsync().ConfigureAwait(true);
        ExecuteSelectedSearchResult();
    }

    private void ExecuteSelectedSearchResult()
    {
        if (_searchDialogResults.Count == 0)
        {
            return;
        }

        var selectedIndex = TryGetSelectedSearchResultIndex();
        if (selectedIndex < 0 || selectedIndex >= _searchDialogResults.Count)
        {
            return;
        }

        var selected = _searchDialogResults[selectedIndex];
        try
        {
            selected.Action?.Invoke();
            _logger?.LogDebug("Executed global search result {Type}:{Name}", selected.Type, selected.Name);
            _searchDialog?.Close();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed executing global search result {Type}:{Name}", selected.Type, selected.Name);
        }
    }

    private int TryGetSelectedSearchResultIndex()
    {
        if (_searchResultsList == null)
        {
            return -1;
        }

        try
        {
            var selectedIndex = _searchResultsList.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _searchDialogResults.Count)
            {
                return selectedIndex;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Unable to read selected index from global search results list");
        }

        return _searchDialogResults.Count == 1 ? 0 : -1;
    }

    private void UpdateGlobalSearchTheme(string themeName)
    {
        try
        {
            if (_searchDialog == null || _searchDialog.IsDisposed)
            {
                return;
            }

            SfSkinManager.SetVisualStyle(_searchDialog, themeName);

            if (_globalSearchBox != null && !_globalSearchBox.IsDisposed)
            {
                _globalSearchBox.ThemeName = themeName;
                SfSkinManager.SetVisualStyle(_globalSearchBox, themeName);
            }

            if (_searchResultsList != null && !_searchResultsList.IsDisposed)
            {
                _searchResultsList.ThemeName = themeName;
                SfSkinManager.SetVisualStyle(_searchResultsList, themeName);
            }

            if (_globalSearchAutoComplete != null)
            {
                _globalSearchAutoComplete.ThemeName = themeName;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to refresh global search theme to {Theme}", themeName);
        }
    }

    private System.Action? BuildSearchAction(GlobalSearchMatch match)
    {
        if (!string.IsNullOrWhiteSpace(match.TargetPanelName))
        {
            var panel = PanelRegistry.GetTownReleasePanels(includeHidden: true).FirstOrDefault(entry =>
                string.Equals(entry.DisplayName, match.TargetPanelName, StringComparison.OrdinalIgnoreCase));

            if (panel != null)
            {
                return () => ShowPanel(panel.PanelType, panel.DisplayName, panel.DefaultDock);
            }
        }

        return null;
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
        public string DisplayText => $"[{Type}] {Name} — {Description}";
    }
}
