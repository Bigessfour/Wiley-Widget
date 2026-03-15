using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
    private Label? _searchResultsSummaryLabel;
    private Label? _searchDialogHintLabel;
    private TextBoxExt? _globalSearchBox;
    private SfListView? _searchResultsList;
    private AutoComplete? _globalSearchAutoComplete;
    private readonly HashSet<string> _globalSearchSuggestions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SearchResult> _searchDialogResults = new();
    private readonly List<int> _searchResultIndexMap = new();
    private readonly List<SearchResult> _recentSearchResults = new();
    private const int MaxRecentSearchResults = 8;
    private static readonly Dictionary<string, string> PanelShortcutHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Settings"] = "Alt+F, T",
        ["Activity Log"] = "Ctrl+Shift+A",
        ["Analytics Hub"] = "Ctrl+2",
        ["War Room"] = "Ctrl+3",
        ["Reports"] = "Ctrl+R",
        ["Dashboard"] = "Ctrl+1",
        ["Customers"] = "Ctrl+Shift+C",
        ["Accounts"] = "Ctrl+Shift+L",
        ["Payments"] = "Ctrl+Shift+P",
        ["QuickBooks"] = "Ctrl+Shift+Q"
    };

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
                Size = new Size(720, 460),
                FormBorderStyle = FormBorderStyle.Sizable,
                ShowIcon = false,
                ShowInTaskbar = false,
                MinimumSize = new Size(520, 340),
                Padding = new Padding(16, 16, 16, 12),
                KeyPreview = true
            };

            var currentTheme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            SfSkinManager.SetVisualStyle(_searchDialog, currentTheme);

            var searchLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
            };
            searchLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            searchLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            searchLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            searchLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _searchResultsSummaryLabel = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Text = "Search panels, commands, and recent activity",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Padding(0, 0, 0, 8)
            };
            searchLayout.Controls.Add(_searchResultsSummaryLabel, 0, 0);

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

            searchLayout.Controls.Add(_globalSearchBox, 0, 1);
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

            searchLayout.Controls.Add(_searchResultsList, 0, 2);

            _searchDialogHintLabel = new Label
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                Text = "Enter to open  •  Up/Down to navigate  •  Esc to close",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                Padding = new Padding(0, 8, 0, 0)
            };
            searchLayout.Controls.Add(_searchDialogHintLabel, 0, 3);

            _searchDialog.Controls.Add(searchLayout);

            _searchResultsList.DoubleClick += OnSearchResultDoubleClick;
            _searchResultsList.KeyDown += OnSearchResultsKeyDown;

            _searchDialog.FormClosed += (s, e) =>
            {
                _globalSearchAutoComplete?.Dispose();
                _globalSearchAutoComplete = null;
                _searchDialog = null;
                _globalSearchBox = null;
                _searchResultsSummaryLabel = null;
                _searchDialogHintLabel = null;
                _searchResultsList = null;
                _globalSearchSuggestions.Clear();
                _searchDialogResults.Clear();
                _searchResultIndexMap.Clear();
            };

            _searchDialog.Show(this);
            ShowPaletteHome();
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
        if (_searchResultsList == null)
        {
            return;
        }

        try
        {
            var normalizedQuery = query?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                ShowPaletteHome();
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
                results = globalResult.Matches
                    .Select(match => new SearchResult
                    {
                        Name = match.Title,
                        Type = match.Category,
                        Description = match.Description,
                        Source = string.IsNullOrWhiteSpace(match.TargetPanelName) ? match.Category : match.TargetPanelName,
                        ShortcutHint = ResolveShortcutHint(match.TargetPanelName ?? match.Title),
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
            BindSearchResults(results, normalizedQuery);
            UpdateSearchSummary(results.Count == 0
                ? $"No results for '{normalizedQuery}'"
                : $"{results.Count} result{(results.Count == 1 ? string.Empty : "s")} for '{normalizedQuery}'");
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

            _globalSearchAutoComplete.DataSource = _globalSearchSuggestions.OrderBy(value => value).ToList();
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

        if (!string.IsNullOrWhiteSpace(query))
        {
            _globalSearchSuggestions.Add(query.Trim());
        }

        foreach (var result in results)
        {
            if (!string.IsNullOrWhiteSpace(result.Name))
            {
                _globalSearchSuggestions.Add(result.Name);
            }

            if (!string.IsNullOrWhiteSpace(result.Description) && result.Description.Length <= 120)
            {
                _globalSearchSuggestions.Add(result.Description);
            }
        }

        _globalSearchAutoComplete.DataSource = _globalSearchSuggestions.OrderBy(value => value).ToList();
    }

    private void ShowPaletteHome()
    {
        var homeResults = BuildPaletteHomeResults();
        _searchDialogResults.Clear();
        _searchDialogResults.AddRange(homeResults);
        BindSearchResults(homeResults, null);
        UpdateSearchSummary(_recentSearchResults.Count == 0
            ? "Search panels, commands, and recent activity"
            : $"{_recentSearchResults.Count} recent item{(_recentSearchResults.Count == 1 ? string.Empty : "s")} and quick actions");
    }

    private List<SearchResult> BuildPaletteHomeResults()
    {
        var quickActions = BuildQuickActionResults();
        var results = new List<SearchResult>();

        results.AddRange(_recentSearchResults.Select(result => result.CloneAsRecent()));
        results.AddRange(quickActions.Where(action => !_recentSearchResults.Any(recent => recent.Matches(action))));

        return results;
    }

    private List<SearchResult> BuildQuickActionResults()
    {
        return new List<SearchResult>
        {
            new()
            {
                Name = "Open Settings",
                Type = "Quick Action",
                Description = "Review application configuration and credentials",
                Source = "Navigation",
                ShortcutHint = ResolveShortcutHint("Settings"),
                Action = () => ShowPanel<Controls.Panels.SettingsPanel>("Settings", DockingStyle.Fill, allowFloating: true)
            },
            new()
            {
                Name = "Open Activity Log",
                Type = "Quick Action",
                Description = "Review recent navigation and system events",
                Source = "Navigation",
                ShortcutHint = ResolveShortcutHint("Activity Log"),
                Action = () => ShowPanel<Controls.Panels.ActivityLogPanel>("Activity Log", DockingStyle.Bottom, allowFloating: true)
            },
            new()
            {
                Name = "Open Analytics Hub",
                Type = "Quick Action",
                Description = "Jump to analytics and scenario exploration",
                Source = "Navigation",
                ShortcutHint = ResolveShortcutHint("Analytics Hub"),
                Action = () => ShowPanel<Controls.Panels.AnalyticsHubPanel>("Analytics Hub", DockingStyle.Right, allowFloating: true)
            },
            new()
            {
                Name = "Open War Room",
                Type = "Quick Action",
                Description = "Run scenario analysis and export forecasts",
                Source = "Navigation",
                ShortcutHint = ResolveShortcutHint("War Room"),
                Action = () => ShowPanel<Controls.Panels.WarRoomPanel>("War Room", DockingStyle.Right, allowFloating: true)
            },
            new()
            {
                Name = "Open Reports",
                Type = "Quick Action",
                Description = "View and export reporting outputs",
                Source = "Navigation",
                ShortcutHint = ResolveShortcutHint("Reports"),
                Action = () => ShowPanel<Controls.Panels.ReportsPanel>("Reports", DockingStyle.Right, allowFloating: true)
            }
        };
    }

    private void BindSearchResults(IEnumerable<SearchResult> results, string? query)
    {
        if (_searchResultsList == null)
        {
            return;
        }

        var orderedResults = results.ToList();
        _searchResultIndexMap.Clear();
        var displayRows = new List<string>();

        if (orderedResults.Count == 0)
        {
            displayRows.Add($"No results{(string.IsNullOrWhiteSpace(query) ? string.Empty : $" for '{query}'")}");
            _searchResultIndexMap.Add(-1);
        }
        else
        {
            foreach (var group in orderedResults.GroupBy(result => result.Type, StringComparer.OrdinalIgnoreCase))
            {
                displayRows.Add(group.Key.ToUpperInvariant());
                _searchResultIndexMap.Add(-1);

                foreach (var result in group)
                {
                    displayRows.Add(result.DisplayText);
                    _searchResultIndexMap.Add(_searchDialogResults.IndexOf(result));
                }
            }
        }

        _searchResultsList.DataSource = displayRows;
        SelectFirstSearchResult();
    }

    private IEnumerable<string> BuildInitialGlobalSearchSuggestions()
    {
        var panelSuggestions = PanelRegistry.Panels
            .SelectMany(panel => new[] { panel.DisplayName, panel.DefaultGroup })
            .Where(value => !string.IsNullOrWhiteSpace(value));

        return panelSuggestions
            .Concat(new[]
            {
                "Activity Log",
                "Dashboard",
                "Reports",
                "Customers",
                "Accounts",
                "Payments",
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
            ExecuteSelectedSearchResult();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Down)
        {
            FocusNextSearchResult();
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
            RegisterRecentSearchResult(selected);
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
            if (selectedIndex >= 0 && selectedIndex < _searchResultIndexMap.Count)
            {
                return _searchResultIndexMap[selectedIndex];
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
            var panel = PanelRegistry.Panels.FirstOrDefault(entry =>
                string.Equals(entry.DisplayName, match.TargetPanelName, StringComparison.OrdinalIgnoreCase));

            if (panel != null)
            {
                return () => ShowPanel(panel.PanelType, panel.DisplayName, panel.DefaultDock);
            }
        }

        if (string.Equals(match.Category, "Activity", StringComparison.OrdinalIgnoreCase))
        {
            return () => ShowPanel<Controls.Panels.ActivityLogPanel>("Activity Log", DockingStyle.Bottom, allowFloating: true);
        }

        return null;
    }

    private void UpdateSearchSummary(string text)
    {
        if (_searchResultsSummaryLabel == null || _searchResultsSummaryLabel.IsDisposed)
        {
            return;
        }

        _searchResultsSummaryLabel.Text = text;
    }

    private void SelectFirstSearchResult()
    {
        if (_searchResultsList == null)
        {
            return;
        }

        for (var index = 0; index < _searchResultIndexMap.Count; index++)
        {
            if (_searchResultIndexMap[index] >= 0)
            {
                _searchResultsList.SelectedIndex = index;
                return;
            }
        }
    }

    private void FocusNextSearchResult()
    {
        if (_searchResultsList == null)
        {
            return;
        }

        var startIndex = Math.Max(_searchResultsList.SelectedIndex, -1);
        for (var index = startIndex + 1; index < _searchResultIndexMap.Count; index++)
        {
            if (_searchResultIndexMap[index] >= 0)
            {
                _searchResultsList.Focus();
                _searchResultsList.SelectedIndex = index;
                return;
            }
        }

        SelectFirstSearchResult();
    }

    private void RegisterRecentSearchResult(SearchResult result)
    {
        _recentSearchResults.RemoveAll(existing => existing.Matches(result));
        _recentSearchResults.Insert(0, result.CloneAsRecent(DateTime.Now));

        if (_recentSearchResults.Count > MaxRecentSearchResults)
        {
            _recentSearchResults.RemoveRange(MaxRecentSearchResults, _recentSearchResults.Count - MaxRecentSearchResults);
        }
    }

    private static string? ResolveShortcutHint(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return PanelShortcutHints.TryGetValue(key.Trim(), out var shortcut)
            ? shortcut
            : null;
    }

    /// <summary>
    /// Represents a search result item.
    /// </summary>
    private class SearchResult
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? ShortcutHint { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public System.Action? Action { get; set; }
        public bool IsRecent { get; set; }

        public string DisplayText => string.IsNullOrWhiteSpace(Description)
            ? $"{Name}{BuildMetadataSuffix()}"
            : $"{Name}   •   {Description}{BuildMetadataSuffix()}";

        public SearchResult CloneAsRecent(DateTime? usedAt = null) => new()
        {
            Name = Name,
            Type = "Recent",
            Description = Description,
            Source = Source,
            ShortcutHint = ShortcutHint,
            LastUsedAt = usedAt ?? LastUsedAt ?? DateTime.Now,
            Action = Action,
            IsRecent = true
        };

        public bool Matches(SearchResult other) =>
            string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Description, other.Description, StringComparison.OrdinalIgnoreCase);

        private string BuildMetadataSuffix()
        {
            var metadata = new List<string>();

            if (IsRecent)
            {
                metadata.Add(LastUsedAt.HasValue ? $"Recent {LastUsedAt.Value:t}" : "Recent");
            }

            if (!string.IsNullOrWhiteSpace(Source))
            {
                metadata.Add(Source);
            }

            if (!string.IsNullOrWhiteSpace(ShortcutHint))
            {
                metadata.Add(ShortcutHint);
            }

            return metadata.Count == 0 ? string.Empty : $"   •   {string.Join("   •   ", metadata)}";
        }
    }
}
