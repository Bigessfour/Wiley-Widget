#nullable enable

using System.Threading;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using WileyWidget.Models;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Logging;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Display model for a single proactive insight card with priority badge and action button.
    /// </summary>
    public partial class InsightCardModel : ObservableObject
    {
        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string explanation = string.Empty;

        [ObservableProperty]
        private string priority = "Medium";

        [ObservableProperty]
        private string category = string.Empty;

        [ObservableProperty]
        private DateTime timestamp = DateTime.UtcNow;

        [ObservableProperty]
        private bool isActioned = false;

        // Reference to original insight for context passing to chat
        public AIInsight? SourceInsight { get; set; }
    }

    /// <summary>
    /// ViewModel for the Insight Feed tab/panel.
    /// Binds to ProactiveInsightsService and provides commands for interaction.
    /// Uses MVVM Toolkit for automatic property change notification.
    /// </summary>
    public partial class InsightFeedViewModel : ViewModelBase, IInsightFeedViewModel
    {
        private readonly ProactiveInsightsService? _insightsService;
        private readonly ILogger<InsightFeedViewModel> _logger;
        private readonly SynchronizationContext? _uiContext;

        [ObservableProperty]
        private ObservableCollection<InsightCardModel> insightCards = new();

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private string statusMessage = "Loading proactive insights...";

        [ObservableProperty]
        private int highPriorityCount = 0;

        [ObservableProperty]
        private int mediumPriorityCount = 0;

        [ObservableProperty]
        private int lowPriorityCount = 0;

        /// <summary>
        /// Initializes a new instance of the InsightFeedViewModel with default dependencies.
        /// This parameterless constructor supports Moq proxy mocking in unit tests.
        /// </summary>
        public InsightFeedViewModel() : this(null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the InsightFeedViewModel.
        /// </summary>
        /// <param name="insightsService">Service providing proactive insights.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        public InsightFeedViewModel(
            ProactiveInsightsService? insightsService = null,
            ILogger<InsightFeedViewModel>? logger = null)
            : base(logger)
        {
            _logger = logger ?? CreateNullLogger();
            _insightsService = insightsService;
            _uiContext = SynchronizationContext.Current;

            _logger.LogInformation("InsightFeedViewModel initialized");

            // Subscribe to insights collection changes if service is available
            if (_insightsService != null)
            {
                _insightsService.Insights.CollectionChanged += (s, e) =>
                {
                    OnInsightsChanged();
                };

                // Initial sync
                OnInsightsChanged();
            }
            else
            {
                StatusMessage = "Proactive insights service unavailable";
                _logger.LogWarning("InsightsService is null - Insight Feed will be empty");
            }
        }

        /// <summary>
        /// Handles changes to the insights collection from the ProactiveInsightsService.
        /// Converts AIInsight domain models to InsightCardModel UI models for grid display.
        /// Updates priority counts (High/Medium/Low) and status messages.
        /// </summary>
        private void OnInsightsChanged()
        {
            if (_uiContext != null && _uiContext != SynchronizationContext.Current)
            {
                _uiContext.Post(_ => OnInsightsChangedCore(), null);
                return;
            }

            OnInsightsChangedCore();
        }

        private void OnInsightsChangedCore()
        {
            try
            {
                IsLoading = true;

                // Clear current UI cards
                InsightCards.Clear();

                if (_insightsService?.Insights == null || _insightsService.Insights.Count == 0)
                {
                    StatusMessage = "No proactive insights available. Keep an eye on the dashboard!";
                    _logger.LogInformation("OnInsightsChanged: No insights available");
                    return;
                }

                // Convert AIInsight domain model to InsightCardModel UI model
                int highCount = 0, mediumCount = 0, lowCount = 0;

                foreach (var insight in _insightsService.Insights)
                {
                    try
                    {
                        var cardModel = new InsightCardModel
                        {
                            Title = $"{insight.Category} Alert",
                            Explanation = insight.Response ?? string.Empty,
                            Priority = insight.Priority ?? "Medium",
                            Category = insight.Category ?? "Unknown",
                            Timestamp = insight.Timestamp,
                            IsActioned = insight.IsActioned,
                            SourceInsight = insight
                        };

                        InsightCards.Add(cardModel);

                        // Count by priority for status message
                        switch (insight.Priority?.ToUpperInvariant())
                        {
                            case "HIGH":
                                highCount++;
                                break;
                            case "MEDIUM":
                                mediumCount++;
                                break;
                            case "LOW":
                                lowCount++;
                                break;
                            default:
                                mediumCount++; // Default to medium if unspecified
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error converting insight to card model: {InsightCategory}", insight.Category);
                        // Continue processing other insights
                    }
                }

                // Update observable priority counts
                HighPriorityCount = highCount;
                MediumPriorityCount = mediumCount;
                LowPriorityCount = lowCount;

                // Update status message based on priority distribution
                StatusMessage = highCount > 0
                    ? $"⚠ {highCount} high-priority items require attention!"
                    : $"Monitoring {InsightCards.Count} insights";

                _logger.LogInformation(
                    "Insight feed updated: {TotalCount} insights ({HighCount} high, {MediumCount} medium, {LowCount} low priority)",
                    InsightCards.Count, highCount, mediumCount, lowCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating insight cards collection");
                StatusMessage = "Error loading insights";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Command to open JARVIS AI chat with the selected insight as context.
        /// Triggered when user clicks on an insight row in the grid.
        ///
        /// The insight context is passed to the JARVIS modal form so JARVIS can provide
        /// specific recommendations related to the selected insight.
        /// Example: "Budget variance alert" → JARVIS provides investigation recommendations.
        /// </summary>
        [RelayCommand]
        public async Task AskJarvis(InsightCardModel? card, CancellationToken cancellationToken = default)
        {
            if (card?.SourceInsight == null)
            {
                MessageBox.Show(
                    "No insight context available. Please select a valid insight.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _logger.LogWarning("AskJarvis called with null insight context");
                return;
            }

            try
            {
                _logger.LogInformation(
                    "User asked JARVIS about insight: Category={Category}, Priority={Priority}",
                    card.Category,
                    card.Priority);

                // Build context for JARVIS chat
                var insightContext = new StringBuilder();
                insightContext.Append("Regarding the ");
                insightContext.Append(card.Category);
                insightContext.AppendLine(" alert:");
                insightContext.AppendLine();
                insightContext.Append("Details: ");
                insightContext.AppendLine(card.Explanation);
                insightContext.Append("Priority: ");
                insightContext.AppendLine(card.Priority);
                insightContext.Append("Generated: ");
                insightContext.AppendLine(card.Timestamp.ToString("G", CultureInfo.InvariantCulture));
                insightContext.AppendLine();
                insightContext.AppendLine("What additional analysis or recommendations do you have about this insight?");

                // Switch to JARVIS Chat tab in right dock panel and set the initial prompt
                var serviceProvider = WileyWidget.WinForms.Program.Services;
                // Resolve the main form from open application forms rather than relying on a non-existent
                // Program.MainFormInstance property.
                var mainForm = System.Windows.Forms.Application.OpenForms
                    .OfType<WileyWidget.WinForms.Forms.MainForm>()
                    .FirstOrDefault();

                if (serviceProvider != null && mainForm != null)
                {
                    // Get JARVIS chat control from the right panel
                    var rightPanel = mainForm.GetRightDockPanel();

                    // Guard against empty control collections to avoid ArgumentOutOfRangeException
                    if (rightPanel != null && rightPanel.Controls != null && rightPanel.Controls.Count > 0 && rightPanel.Controls[0] is TabControl tabControl)
                    {
                        var jarvisTab = tabControl.TabPages.Cast<TabPage>()
                            .FirstOrDefault(tp => tp.Name == "JARVISChatTab");

                        if (jarvisTab != null && jarvisTab.Controls != null && jarvisTab.Controls.Count > 0 && jarvisTab.Controls[0] is WileyWidget.WinForms.Controls.Supporting.JARVISChatUserControl jarvisControl)
                        {
                            // Set initial prompt and switch tab
                            jarvisControl.InitialPrompt = insightContext.ToString();
                            mainForm.SwitchRightPanel("JarvisChat");
                            _logger.LogInformation("Switched to JARVIS Chat tab with insight context ({ContextLength} chars)", insightContext.Length);
                        }
                        else
                        {
                            _logger.LogDebug("JARVIS tab or control not found in right panel or control collection empty");
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Right panel or its controls are not ready for JARVIS chat switch");
                    }
                }
                else
                {
                    _logger.LogWarning("Program.Services or MainForm is null, cannot open JARVIS Chat");
                    MessageBox.Show(insightContext.ToString(), "Ask JARVIS Context", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error asking JARVIS about insight: {Category}", card.Category);
                MessageBox.Show(
                    $"Failed to open JARVIS chat: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Command to mark an insight as actioned (resolved/handled).
        /// Updates both the UI card model and the underlying domain model.
        /// </summary>
        [RelayCommand]
        public void MarkAsActioned(InsightCardModel? card)
        {
            if (card == null)
            {
                _logger.LogWarning("MarkAsActioned called with null card");
                return;
            }

            try
            {
                card.IsActioned = true;

                if (card.SourceInsight != null)
                {
                    card.SourceInsight.IsActioned = true;
                    _logger.LogInformation(
                        "Marked insight as actioned: {Category} ({Priority})",
                        card.Category,
                        card.Priority);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking insight as actioned: {Category}", card.Category);
            }
        }

        /// <summary>
        /// Command to manually refresh the insights feed immediately.
        /// Useful for testing or when user wants to force a refresh.
        /// Triggers synchronization with ProactiveInsightsService.
        /// </summary>
        [RelayCommand]
        public void RefreshInsights()
        {
            try
            {
                _logger.LogInformation("Manual refresh requested by user");
                StatusMessage = "Refreshing insights...";

                // Trigger a manual refresh by syncing with service
                OnInsightsChanged();

                _logger.LogInformation("Manual refresh completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing insights");
                StatusMessage = "Error refreshing insights";
            }
        }

        /// <summary>
        /// Refreshes the insights data asynchronously.
        /// </summary>
        public async Task RefreshAsync(CancellationToken ct = default)
        {
            await Task.Run(() => RefreshInsights(), ct);
        }

        /// <summary>
        /// Creates a null logger instance specific to this ViewModel type.
        /// </summary>
        private ILogger<InsightFeedViewModel> CreateNullLogger()
        {
            return Microsoft.Extensions.Logging.Abstractions.NullLogger<InsightFeedViewModel>.Instance;
        }
    }
}
