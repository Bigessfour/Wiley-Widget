using System;
using System.Collections.Generic;
using System.Linq;
using Prism;
using Prism.Navigation;
using Serilog;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services
{
    /// <summary>
    /// Enhanced navigation service that provides journal support and navigation history.
    /// Wraps Prism's IRegionManager to add back/forward navigation capabilities.
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly IRegionManager _regionManager;
        private readonly ILogger<NavigationService> _logger;
        private readonly Dictionary<string, NavigationJournal> _journals = new();
        private object? _currentContent;
        private readonly Stack<object> _navigationHistory = new();

        public NavigationService(IRegionManager regionManager, ILogger<NavigationService> logger)
        {
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a value indicating whether navigation can go back.
        /// </summary>
        public bool CanGoBack => _navigationHistory.Count > 0;

        /// <summary>
        /// Gets the current content being displayed.
        /// </summary>
        public object? CurrentContent => _currentContent;

        /// <summary>
        /// Event raised when navigation occurs.
        /// </summary>
        public event EventHandler<NavigationEventArgs>? Navigated;

        /// <summary>
        /// Navigates to a view in the specified region with optional navigation parameters.
        /// </summary>
        public void Navigate(string regionName, string viewName, NavigationParameters? parameters = null)
        {
            if (string.IsNullOrEmpty(regionName))
                throw new ArgumentNullException(nameof(regionName));
            if (string.IsNullOrEmpty(viewName))
                throw new ArgumentNullException(nameof(viewName));

            _logger.LogInformation("Navigating to {ViewName} in region {RegionName}", viewName, regionName);

            // Get or create journal for this region
            var journal = GetOrCreateJournal(regionName);

            // Add current navigation to journal before navigating
            var currentEntry = journal.CurrentEntry;
            if (currentEntry != null)
            {
                journal.RecordNavigation(currentEntry);
            }

            // Perform navigation
            _regionManager.RequestNavigate(regionName, viewName, result =>
            {
                if (result.Success)
                {
                    // Record successful navigation in journal
                    var entry = new NavigationJournalEntry(viewName, parameters);
                    journal.RecordNavigation(entry);
                    _logger.LogDebug("Navigation to {ViewName} in {RegionName} completed successfully", viewName, regionName);
                }
                else
                {
                    _logger.LogWarning("Navigation to {ViewName} in {RegionName} failed", viewName, regionName);
                }
            });
        }

        /// <summary>
        /// Navigates back in the specified region's navigation history.
        /// </summary>
        public bool GoBack(string regionName)
        {
            if (string.IsNullOrEmpty(regionName))
                throw new ArgumentNullException(nameof(regionName));

            var journal = GetJournal(regionName);
            if (journal?.CanGoBack == true)
            {
                var entry = journal.GoBack();
                if (entry != null)
                {
                    _logger.LogInformation("Navigating back to {ViewName} in region {RegionName}", entry.ViewName, regionName);
                    _regionManager.RequestNavigate(regionName, entry.ViewName, result =>
                    {
                        if (result.Success)
                        {
                            _logger.LogInformation("Successfully navigated back to {ViewName}", entry.ViewName);
                        }
                    });
                    return true;
                }
            }

            _logger.LogDebug("Cannot go back in region {RegionName} - no history available", regionName);
            return false;
        }

        /// <summary>
        /// Navigates forward in the specified region's navigation history.
        /// </summary>
        public bool GoForward(string regionName)
        {
            if (string.IsNullOrEmpty(regionName))
                throw new ArgumentNullException(nameof(regionName));

            var journal = GetJournal(regionName);
            if (journal?.CanGoForward == true)
            {
                var entry = journal.GoForward();
                if (entry != null)
                {
                    _logger.LogInformation("Navigating forward to {ViewName} in region {RegionName}", entry.ViewName, regionName);
                    _regionManager.RequestNavigate(regionName, entry.ViewName, result =>
                    {
                        if (result.Success)
                        {
                            _logger.LogInformation("Successfully navigated forward to {ViewName}", entry.ViewName);
                        }
                    });
                    return true;
                }
            }

            _logger.LogDebug("Cannot go forward in region {RegionName} - no forward history available", regionName);
            return false;
        }

        /// <summary>
        /// Gets the navigation journal for the specified region.
        /// </summary>
        public INavigationJournal? GetJournal(string regionName)
        {
            if (string.IsNullOrEmpty(regionName))
                return null;

            return _journals.TryGetValue(regionName, out var journal) ? journal : null;
        }

        /// <summary>
        /// Clears the navigation history for the specified region.
        /// </summary>
        public void ClearHistory(string regionName)
        {
            if (string.IsNullOrEmpty(regionName))
                return;

            if (_journals.TryGetValue(regionName, out var journal))
            {
                journal.Clear();
                _logger.LogDebug("Cleared navigation history for region {RegionName}", regionName);
            }
        }

        /// <summary>
        /// Gets or creates a navigation journal for the specified region.
        /// </summary>
        private NavigationJournal GetOrCreateJournal(string regionName)
        {
            if (!_journals.TryGetValue(regionName, out var journal))
            {
                journal = new NavigationJournal();
                _journals[regionName] = journal;
                _logger.LogDebug("Created navigation journal for region {RegionName}", regionName);
            }
            return journal;
        }

        /// <summary>
        /// Navigates to the specified view model or content.
        /// </summary>
        public void Navigate(object content, object? parameter = null)
        {
            var previousContent = _currentContent;
            _currentContent = content;

            if (previousContent != null)
            {
                _navigationHistory.Push(previousContent);
            }

            _logger.LogInformation("Navigated to content: {Content}", content);
            Navigated?.Invoke(this, new NavigationEventArgs(content, parameter, previousContent));
        }

        /// <summary>
        /// Navigates to a view model of the specified type.
        /// </summary>
        public void Navigate<TViewModel>(object? parameter = null) where TViewModel : class
        {
            // For now, just navigate to the type itself
            Navigate(typeof(TViewModel), parameter);
        }

        /// <summary>
        /// Navigates back to the previous view.
        /// </summary>
        public void GoBack()
        {
            if (_navigationHistory.Count > 0)
            {
                var previousContent = _navigationHistory.Pop();
                var currentContent = _currentContent;
                _currentContent = previousContent;

                _logger.LogInformation("Navigated back to content: {Content}", previousContent);
                Navigated?.Invoke(this, new NavigationEventArgs(previousContent, null, currentContent));
            }
        }

        /// <summary>
        /// Clears the navigation history.
        /// </summary>
        public void ClearHistory()
        {
            _navigationHistory.Clear();
            _logger.LogDebug("Cleared navigation history");
        }
    }

    /// <summary>
    /// Navigation journal entry representing a single navigation.
    /// </summary>
    public class NavigationJournalEntry
    {
        public string ViewName { get; }
        public NavigationParameters? Parameters { get; }
        public DateTime Timestamp { get; }

        public NavigationJournalEntry(string viewName, NavigationParameters? parameters = null)
        {
            ViewName = viewName ?? throw new ArgumentNullException(nameof(viewName));
            Parameters = parameters;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Navigation journal that maintains back/forward navigation history.
    /// </summary>
    public class NavigationJournal : INavigationJournal
    {
        private readonly List<NavigationJournalEntry> _backStack = new();
        private readonly List<NavigationJournalEntry> _forwardStack = new();
        private NavigationJournalEntry? _currentEntry;

        public NavigationJournalEntry? CurrentEntry => _currentEntry;
        public bool CanGoBack => _backStack.Count > 0;
        public bool CanGoForward => _forwardStack.Count > 0;

        public void RecordNavigation(NavigationJournalEntry entry)
        {
            if (_currentEntry != null)
            {
                _backStack.Add(_currentEntry);
            }

            _currentEntry = entry;
            _forwardStack.Clear(); // Clear forward stack when new navigation occurs
        }

        public NavigationJournalEntry? GoBack()
        {
            if (!CanGoBack)
                return null;

            if (_currentEntry != null)
            {
                _forwardStack.Insert(0, _currentEntry);
            }

            _currentEntry = _backStack.Last();
            _backStack.RemoveAt(_backStack.Count - 1);

            return _currentEntry;
        }

        public NavigationJournalEntry? GoForward()
        {
            if (!CanGoForward)
                return null;

            if (_currentEntry != null)
            {
                _backStack.Add(_currentEntry);
            }

            _currentEntry = _forwardStack.First();
            _forwardStack.RemoveAt(0);

            return _currentEntry;
        }

        public void Clear()
        {
            _backStack.Clear();
            _forwardStack.Clear();
            _currentEntry = null;
        }
    }

    /// <summary>
    /// Interface for navigation journal functionality.
    /// </summary>
    public interface INavigationJournal
    {
        NavigationJournalEntry? CurrentEntry { get; }
        bool CanGoBack { get; }
        bool CanGoForward { get; }
        void RecordNavigation(NavigationJournalEntry entry);
        NavigationJournalEntry? GoBack();
        NavigationJournalEntry? GoForward();
        void Clear();
    }
}
