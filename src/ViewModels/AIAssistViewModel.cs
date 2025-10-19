using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using System.Text.RegularExpressions;
using System.Globalization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Syncfusion.UI.Xaml.Chat;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for AI Assistant functionality
/// </summary>
public partial class AIAssistViewModel : ObservableObject
{
    private readonly IAIService _aiService;
    private readonly IChargeCalculatorService _chargeCalculator;
    private readonly IWhatIfScenarioEngine _scenarioEngine;
    private readonly IGrokSupercomputer _grokSupercomputer;
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly IDispatcherHelper _dispatcherHelper;
    private readonly Microsoft.Extensions.Logging.ILogger<AIAssistViewModel> _logger;

    /// <summary>
    /// Expose GrokSupercomputer for real-time data refresh in View
    /// </summary>
    public IGrokSupercomputer GrokSupercomputer => _grokSupercomputer;

    public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

    /// <summary>
    /// Responses collection for chat UI binding (chat history)
    /// Notifies on collection changes for auto-scroll
    /// </summary>
    private ObservableCollection<object> _responses = new();
    public ObservableCollection<object> Responses
    {
        get => _responses;
        private set
        {
            if (SetProperty(ref _responses, value))
            {
                // Subscribe to collection changes for notifications
                _responses.CollectionChanged += (s, e) => OnPropertyChanged(nameof(Responses));
        }
    }
}

/// <summary>
/// Represents conversation mode information for UI display
/// </summary>
public class ConversationModeInfo
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }
}
    private string queryText = string.Empty;

    public string QueryText
    {
        get => queryText;
        set => SetProperty(ref queryText, value);
    }

    private string messageText = string.Empty;

    public string MessageText
    {
        get => messageText;
        set => SetProperty(ref messageText, value);
    }

    private string response = string.Empty;
    public string Response
    {
        get => response;
        set => SetProperty(ref response, value);
    }

    [ObservableProperty]
    private string selectedHistoryItem;

    private bool isTyping = false;
    public bool IsTyping
    {
        get => isTyping;
        set => SetProperty(ref isTyping, value);
    }

    private string errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => errorMessage;
        set => SetProperty(ref errorMessage, value);
    }

    /// <summary>
    /// Available conversation modes
    /// </summary>
    public List<ConversationModeInfo> AvailableModes { get; } = new()
    {
        new ConversationModeInfo { Name = "General Assistant", Description = "General questions and analysis", Icon = "🤖" },
        new ConversationModeInfo { Name = "Service Charge Calculator", Description = "Calculate service charges and fees", Icon = "💰" },
        new ConversationModeInfo { Name = "What-If Planner", Description = "Plan financial scenarios and upgrades", Icon = "🔮" },
        new ConversationModeInfo { Name = "Proactive Advisor", Description = "Anticipate needs and provide insights", Icon = "🎯" }
    };

    /// <summary>
    /// Currently selected conversation mode
    /// </summary>
    private ConversationModeInfo selectedMode;
    public ConversationModeInfo SelectedMode
    {
        get => selectedMode;
        set => SetProperty(ref selectedMode, value);
    }

    // Conversation mode properties
    private bool isGeneralMode = true;
    public bool IsGeneralMode
    {
        get => isGeneralMode;
        set => SetProperty(ref isGeneralMode, value);
    }

    private bool isServiceChargeMode = false;
    public bool IsServiceChargeMode
    {
        get => isServiceChargeMode;
        set => SetProperty(ref isServiceChargeMode, value);
    }

    private bool isWhatIfMode = false;
    public bool IsWhatIfMode
    {
        get => isWhatIfMode;
        set => SetProperty(ref isWhatIfMode, value);
    }

    private bool isProactiveMode = false;
    public bool IsProactiveMode
    {
        get => isProactiveMode;
        set => SetProperty(ref isProactiveMode, value);
    }

    // Financial input properties
    [ObservableProperty]
    private decimal annualExpenses;

    [ObservableProperty]
    private decimal targetReservePercentage = 10;

    [ObservableProperty]
    private decimal payRaisePercentage;

    [ObservableProperty]
    private decimal benefitsIncreasePercentage;

    [ObservableProperty]
    private decimal equipmentCost;

    [ObservableProperty]
    private decimal reserveAllocationPercentage = 15;

    // UI visibility properties
    private bool showFinancialInputs;
    public bool ShowFinancialInputs
    {
        get => showFinancialInputs;
        set => SetProperty(ref showFinancialInputs, value);
    }

    /// <summary>
    /// Loading state for UI feedback
    /// </summary>
    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Processing state for busy indicator
    /// </summary>
    [ObservableProperty]
    private bool isProcessing;

    /// <summary>
    /// Status message for user feedback
    /// </summary>
    [ObservableProperty]
    private string statusMessage = string.Empty;

    /// <summary>
    /// Current user for chat interface
    /// </summary>
    public Author CurrentUser { get; } = new Author { Name = "You" };

    /// <summary>
    /// Messages collection for SfAIAssistView binding (alias for ChatMessages)
    /// </summary>
    public ObservableCollection<ChatMessage> Messages => ChatMessages;

    /// <summary>
    /// Conversation history for combo box
    /// </summary>
    public ObservableCollection<string> ConversationHistory { get; } = new() { "Budget Analysis - Q1", "Rate Increase Scenario", "Reserve Fund Planning" };

    /// <summary>
    /// Enable proactive monitoring feature
    /// </summary>
    [ObservableProperty]
    private bool enableProactiveMonitoring;

    /// <summary>
    /// Threshold for proactive alerts
    /// </summary>
    [ObservableProperty]
    private decimal proactiveAlertThreshold = 80;

    /// <summary>
    /// Current value for what-if scenarios
    /// </summary>
    [ObservableProperty]
    private decimal whatIfNewValue;

    /// <summary>
    /// Selected what-if scenario
    /// </summary>
    [ObservableProperty]
    private string whatIfScenario = string.Empty;

    /// <summary>
    /// Variable being analyzed in what-if scenario
    /// </summary>
    [ObservableProperty]
    private string whatIfVariable = string.Empty;

    // Prism DelegateCommand properties (replacing CommunityToolkit RelayCommand source-generated commands)
    public Prism.Commands.DelegateCommand SendCommand { get; private set; }
    public Prism.Commands.DelegateCommand ClearResponsesCommand { get; private set; }
    public Prism.Commands.DelegateCommand SendMessageCommand { get; private set; }
    public Prism.Commands.DelegateCommand GenerateCommand { get; private set; }
    public Prism.Commands.DelegateCommand ClearChatCommand { get; private set; }
    public Prism.Commands.DelegateCommand ExportChatCommand { get; private set; }
    public Prism.Commands.DelegateCommand ConfigureAICommand { get; private set; }
    public Prism.Commands.DelegateCommand CalculateServiceChargeCommand { get; private set; }
    public Prism.Commands.DelegateCommand GenerateWhatIfScenarioCommand { get; private set; }
    public Prism.Commands.DelegateCommand GetProactiveAdviceCommand { get; private set; }
    public Prism.Commands.DelegateCommand RefreshLiveDataCommand { get; private set; }
    public Prism.Commands.DelegateCommand<string> SetConversationModeCommand { get; private set; }

    /// <summary>
    /// Constructor with AI service dependency
    /// </summary>
    public AIAssistViewModel(IAIService aiService, IChargeCalculatorService chargeCalculator, IWhatIfScenarioEngine scenarioEngine, IGrokSupercomputer grokSupercomputer, IEnterpriseRepository enterpriseRepository, IDispatcherHelper dispatcherHelper, Microsoft.Extensions.Logging.ILogger<AIAssistViewModel> logger)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _chargeCalculator = chargeCalculator ?? throw new ArgumentNullException(nameof(chargeCalculator));
        _scenarioEngine = scenarioEngine ?? throw new ArgumentNullException(nameof(scenarioEngine));
        _grokSupercomputer = grokSupercomputer ?? throw new ArgumentNullException(nameof(grokSupercomputer));
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _dispatcherHelper = dispatcherHelper ?? throw new ArgumentNullException(nameof(dispatcherHelper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // Initialize Responses collection with notification
    Responses = new ObservableCollection<object>();
    Responses.CollectionChanged += (s, e) => OnPropertyChanged(nameof(Responses));

    // Initialize Prism DelegateCommands for UI bindings (replaces CommunityToolkit RelayCommand)
    SendCommand = new Prism.Commands.DelegateCommand(async () => await Send(), () => CanSend());
    ClearResponsesCommand = new Prism.Commands.DelegateCommand(ClearResponses);
    SendMessageCommand = new Prism.Commands.DelegateCommand(async () => await SendMessage(), () => CanSendMessage());
    GenerateCommand = new Prism.Commands.DelegateCommand(async () => await Generate(), () => CanGenerate());
    ClearChatCommand = new Prism.Commands.DelegateCommand(ClearChat);
    ExportChatCommand = new Prism.Commands.DelegateCommand(async () => await ExportChat());
    ConfigureAICommand = new Prism.Commands.DelegateCommand(ConfigureAI);
    CalculateServiceChargeCommand = new Prism.Commands.DelegateCommand(async () => await CalculateServiceCharge());
    GenerateWhatIfScenarioCommand = new Prism.Commands.DelegateCommand(async () => await GenerateWhatIfScenario());
    GetProactiveAdviceCommand = new Prism.Commands.DelegateCommand(async () => await GetProactiveAdvice());
    RefreshLiveDataCommand = new Prism.Commands.DelegateCommand(async () => await RefreshLiveData());
    SetConversationModeCommand = new Prism.Commands.DelegateCommand<string>(SetConversationMode);

        // Set default mode to General Assistant
        SetConversationMode("General");
    }

    /// <summary>
    /// Send command - Processes query with IChargeCalculatorService and populates Responses collection
    /// </summary>
    private async Task Send()
    {
        if (string.IsNullOrWhiteSpace(QueryText))
        {
            ErrorMessage = "Please enter a query.";
            return;
        }

        var userQuery = QueryText.Trim();
        QueryText = string.Empty;
        ErrorMessage = string.Empty;

        // Add user message to Responses
        Responses.Add(new ChatMessage
        {
            Author = CurrentUser,
            Text = userQuery,
            DateTime = DateTime.Now
        });

        // Show typing indicator and processing
        IsTyping = true;
        IsProcessing = true;

        try
        {
            // Parse enterprise ID from query (e.g., "Calculate charge for Enterprise 1")
            var enterpriseId = ExtractEnterpriseId(userQuery);

            // Execute charge calculation asynchronously without Task.Run
            var recommendation = await _chargeCalculator.CalculateRecommendedChargeAsync(enterpriseId ?? 1);

            // Format response message
            var responseText = FormatServiceChargeResponse(recommendation);

            // Add AI response to Responses
            Responses.Add(new ChatMessage
            {
                Author = new Author { Name = "AI Assistant" },
                Text = responseText,
                DateTime = DateTime.Now
            });

            Log.Information("Service charge calculation completed for enterprise {EnterpriseId}", enterpriseId ?? 1);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing query: {Query}", userQuery);
            ErrorMessage = $"Error: {ex.Message}";

            // Add error message to chat
            Responses.Add(new ChatMessage
            {
                Author = new Author { Name = "AI Assistant" },
                Text = $"I encountered an error processing your request: {ex.Message}\n\nPlease try again or rephrase your query.",
                DateTime = DateTime.Now
            });
        }
        finally
        {
            IsTyping = false;
            IsProcessing = false;
        }
    }

    private bool CanSend() => !string.IsNullOrWhiteSpace(QueryText) && !IsProcessing;

    /// <summary>
    /// Clear responses command
    /// </summary>
    private void ClearResponses()
    {
        Responses.Clear();
        ErrorMessage = string.Empty;
        Log.Information("Chat responses cleared");
    }

    /// <summary>
    /// Extract enterprise ID from query string
    /// </summary>
    private int? ExtractEnterpriseId(string query)
    {
        // Look for patterns like "Enterprise 1", "enterprise id 2", "ID:3", etc.
        var match = Regex.Match(query, @"(?:enterprise|id)[:\s]+(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int id))
        {
            return id;
        }
        return null;
    }

    /// <summary>
    /// Format service charge recommendation into readable response
    /// </summary>
    private string FormatServiceChargeResponse(ServiceChargeRecommendation recommendation)
    {
        var response = new System.Text.StringBuilder();
        response.AppendLine($"📊 **Service Charge Analysis for Enterprise {recommendation.EnterpriseId}**");
        response.AppendLine($"**{recommendation.EnterpriseName}**");
        response.AppendLine();
        response.AppendLine($"**Recommended Rate:** ${recommendation.RecommendedRate:N2}/month");
        response.AppendLine($"**Current Rate:** ${recommendation.CurrentRate:N2}/month");
        response.AppendLine();
        response.AppendLine($"**Financial Details:**");
        response.AppendLine($"- Total Monthly Expenses: ${recommendation.TotalMonthlyExpenses:N2}");
        response.AppendLine($"- Monthly Revenue at Recommended: ${recommendation.MonthlyRevenueAtRecommended:N2}");
        response.AppendLine($"- Monthly Surplus: ${recommendation.MonthlySurplus:N2}");
        response.AppendLine($"- Reserve Allocation: ${recommendation.ReserveAllocation:N2}");
        response.AppendLine();
        response.AppendLine($"**Break-Even Analysis:**");
        response.AppendLine($"- Break-Even Rate: ${recommendation.BreakEvenAnalysis.BreakEvenRate:N2}");
        response.AppendLine($"- Current Surplus/Deficit: ${recommendation.BreakEvenAnalysis.CurrentSurplusDeficit:N2}");
        response.AppendLine($"- Required Rate Increase: {recommendation.BreakEvenAnalysis.RequiredRateIncrease:F1}%");
        response.AppendLine($"- Coverage Ratio: {recommendation.BreakEvenAnalysis.CoverageRatio:F2}");
        response.AppendLine();
        if (recommendation.Assumptions.Any())
        {
            response.AppendLine($"**Assumptions:**");
            foreach (var assumption in recommendation.Assumptions)
            {
                response.AppendLine($"- {assumption}");
            }
            response.AppendLine();
        }
        response.AppendLine($"*Analysis generated on {recommendation.CalculationDate:g}*");

        return response.ToString();
    }

    /// <summary>
    /// Send message command (legacy - for ChatMessages collection)
    /// </summary>
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageText))
        {
            MessageText = string.Empty;
            return;
        }

        var userMessage = MessageText.Trim();
        MessageText = string.Empty;

        // Add user message
        ChatMessages.Add(new ChatMessage
        {
            Text = userMessage,
            IsUser = true,
            Timestamp = DateTime.Now
        });

        // Show typing indicator and processing
        IsTyping = true;
        IsProcessing = true;

        try
        {
            // Get AI response
            var aiResponse = await _aiService.GetInsightsAsync(
                "Wiley Widget Municipal Utility Management Application",
                userMessage
            );

            // Add AI response
            ChatMessages.Add(new ChatMessage
            {
                Text = aiResponse,
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating AI response");

            ChatMessages.Add(new ChatMessage
            {
                Text = "Sorry, I encountered an error processing your request. Please try again.",
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsTyping = false;
            IsProcessing = false;
        }
    }

    private bool CanSendMessage() => !string.IsNullOrWhiteSpace(MessageText);

    /// <summary>
    /// Generate response command using AI service
    /// </summary>
    private async Task Generate()
    {
        if (string.IsNullOrWhiteSpace(QueryText))
        {
            Response = "Please enter a query to generate a response.";
            return;
        }

        IsProcessing = true;
        try
        {
            // Use AI service for general queries and analysis
            var context = "Municipal utility management and budgeting system";
            var result = await _aiService.GetInsightsAsync(context, QueryText);

            Response = result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating AI response");
            Response = $"Error generating response: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private bool CanGenerate() => !string.IsNullOrWhiteSpace(QueryText) && !IsProcessing;

    /// <summary>
    /// Clear chat command
    /// </summary>
    private void ClearChat()
    {
        ChatMessages.Clear();
        Log.Information("Chat history cleared");
    }

    /// <summary>
    /// Export chat command
    /// </summary>
    private async Task ExportChat()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Chat History",
                Filter = "Text Files (*.txt)|*.txt|Markdown Files (*.md)|*.md",
                DefaultExt = ".txt",
                FileName = $"AI_Chat_History_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                var content = new System.Text.StringBuilder();
                content.AppendLine("# AI Assistant Chat History");
                content.AppendLine($"Exported on: {DateTime.Now:g}");
                content.AppendLine();

                foreach (var message in ChatMessages)
                {
                    var sender = message.IsUser ? "You" : "AI Assistant";
                    content.AppendLine($"**{sender}** ({message.Timestamp:g}):");
                    content.AppendLine(message.Text);
                    content.AppendLine();
                }

                await System.IO.File.WriteAllTextAsync(dialog.FileName, content.ToString());
                Log.Information("Chat history exported to {FileName}", dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exporting chat history");
        }
    }

    /// <summary>
    /// Configure AI command
    /// </summary>
    private void ConfigureAI()
    {
        try
        {
            // Navigate to settings - AI configuration is in the settings view
            Log.Information("AI configuration requested - please access via Settings menu");
            // Could publish navigation message if needed, but for now just log
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in AI configuration request");
        }
    }

    /// <summary>
    /// Calculate service charge command
    /// </summary>
    private async Task CalculateServiceCharge()
    {
        if (SelectedMode?.Name != "Service Charge Calculator")
            return;

        IsTyping = true;

        try
        {
            // Get enterprise data for calculation
            var enterprise = await GetCurrentEnterpriseAsync();
            if (enterprise == null)
            {
                ChatMessages.Add(new ChatMessage
                {
                    Text = "Unable to calculate service charges: No enterprise data available.",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
                return;
            }

            var result = await _chargeCalculator.CalculateRecommendedChargeAsync(enterprise.Id);

            var response = $"**Service Charge Calculation Results:**\n\n" +
                          $"**Recommended Monthly Charge:** ${result.RecommendedRate:F2}\n" +
                          $"**Break-even Analysis:** ${result.BreakEvenAnalysis.BreakEvenRate:F2}\n" +
                          $"**Reserve Allocation:** ${result.ReserveAllocation:F2}\n\n" +
                          $"**Current Rate:** ${result.CurrentRate:F2}\n" +
                          $"**Total Monthly Expenses:** ${result.TotalMonthlyExpenses:F2}\n" +
                          $"**Monthly Revenue at Recommended:** ${result.MonthlyRevenueAtRecommended:F2}\n" +
                          $"**Monthly Surplus:** ${result.MonthlySurplus:F2}";

            ChatMessages.Add(new ChatMessage
            {
                Text = response,
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error calculating service charge");

            ChatMessages.Add(new ChatMessage
            {
                Text = "Sorry, I encountered an error calculating the service charge. Please try again.",
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsTyping = false;
        }
    }

    /// <summary>
    /// Generate what-if scenario command
    /// </summary>
    private async Task GenerateWhatIfScenario()
    {
        if (SelectedMode?.Name != "What-If Planner")
            return;

        if (string.IsNullOrWhiteSpace(MessageText))
        {
            ChatMessages.Add(new ChatMessage
            {
                Text = "Please describe your what-if scenario (e.g., '15% pay raise, benefits improvement, 10% reserve, equipment purchase').",
                IsUser = false,
                Timestamp = DateTime.Now
            });
            return;
        }

        var scenario = MessageText.Trim();
        MessageText = string.Empty;

        // Add user scenario
        ChatMessages.Add(new ChatMessage
        {
            Text = scenario,
            IsUser = true,
            Timestamp = DateTime.Now
        });

        IsTyping = true;

        try
        {
            var enterprise = await GetCurrentEnterpriseAsync();
            if (enterprise == null)
            {
                ChatMessages.Add(new ChatMessage
                {
                    Text = "Unable to generate scenario: No enterprise data available.",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
                return;
            }

            var parameters = ParseScenarioParameters(scenario);
            var result = await _scenarioEngine.GenerateComprehensiveScenarioAsync(enterprise.Id, parameters);

            var response = $"**What-If Scenario Analysis:**\n\n" +
                          $"**Scenario:** {result.ScenarioName}\n\n" +
                          $"**Total Impact:**\n" +
                          $"- Annual Expense Increase: ${result.TotalImpact.TotalAnnualExpenseIncrease:N2}\n" +
                          $"- Monthly Expense Increase: ${result.TotalImpact.TotalMonthlyExpenseIncrease:N2}\n" +
                          $"- Required Rate Increase: ${result.TotalImpact.RequiredRateIncrease:N2}\n" +
                          $"- New Monthly Rate: ${result.TotalImpact.NewMonthlyRate:N2}\n\n" +
                          $"**Recommendations:**\n{string.Join("\n", result.Recommendations)}\n\n" +
                          $"**Risk Assessment:** {result.RiskAssessment.RiskLevel}\n" +
                          $"**Concerns:** {string.Join(", ", result.RiskAssessment.Concerns)}";

            ChatMessages.Add(new ChatMessage
            {
                Text = response,
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating what-if scenario");

            ChatMessages.Add(new ChatMessage
            {
                Text = "Sorry, I encountered an error generating the scenario analysis. Please try again.",
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsTyping = false;
        }
    }

    /// <summary>
    /// Get proactive advice command
    /// </summary>
    private async Task GetProactiveAdvice()
    {
        if (SelectedMode?.Name != "Proactive Advisor")
            return;

        IsTyping = true;

        try
        {
            await Task.CompletedTask; // Suppress async warning
            var recentActivity = GetRecentActivitySummary();
            var userProfile = await GetUserProfileSummary();

            var insights = new LocalAnticipatoryInsights
            {
                RecentActivity = recentActivity,
                Insights = "Proactive insights temporarily disabled due to service compilation issues.",
                GeneratedDate = DateTime.Now,
                SuggestedActions = new List<string> { "Check service status", "Review recent activity" }
            };

            // Insights is always created above, so no null check needed
            var response = $"**Proactive Insights & Recommendations:**\n\n" +
                          $"**Insights:** {insights.Insights}\n\n" +
                          $"**Suggested Actions:**\n{string.Join("\n", insights.SuggestedActions)}\n\n" +
                          $"*Generated on {insights.GeneratedDate:g}*";

            ChatMessages.Add(new ChatMessage
            {
                Text = response,
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating proactive advice");

            ChatMessages.Add(new ChatMessage
            {
                Text = "Sorry, I encountered an error generating proactive advice. Please try again.",
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsTyping = false;
        }
    }

    /// <summary>
    /// Refresh live enterprise data command
    /// </summary>
    private async Task RefreshLiveData()
    {
        try
        {
            Log.Information("Refreshing live enterprise data from GrokSupercomputer");

            // Fetch latest enterprise data
            var reportData = await GrokSupercomputer.FetchEnterpriseDataAsync();

            // Add system message to chat using the correct ChatMessage from WileyWidget.Models
            var systemMessage = new
            {
                Author = new { Name = "System" },
                Text = $"✓ Live data refreshed: {reportData?.EnterpriseCount ?? 0} enterprises loaded. Context updated with latest municipal data.",
                DateTime = DateTime.Now
            };

            Responses.Add(systemMessage);

            Log.Information("Live data refresh completed: {Count} enterprises", reportData?.EnterpriseCount ?? 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error refreshing live data");

            var errorMessage = new
            {
                Author = new { Name = "System" },
                Text = "❌ Error refreshing live data. Please check your connection and try again.",
                DateTime = DateTime.Now
            };

            Responses.Add(errorMessage);
        }
    }

    /// <summary>
    /// Get current enterprise from repository
    /// </summary>
    private async Task<Enterprise> GetCurrentEnterpriseAsync()
    {
        try
        {
            // Get the first enterprise as current (you may want to implement proper selection logic)
            var enterprises = await _enterpriseRepository.GetAllAsync();
            return enterprises.FirstOrDefault() ?? new Enterprise
            {
                Id = 0,
                Name = "No Enterprise Found",
                CurrentRate = 0.00M,
                CitizenCount = 0,
                MonthlyExpenses = 0.00M
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error retrieving current enterprise");
            return new Enterprise
            {
                Id = 0,
                Name = "Error Loading Enterprise",
                CurrentRate = 0.00M,
                CitizenCount = 0,
                MonthlyExpenses = 0.00M
            };
        }
    }

    /// <summary>
    /// Get recent activity summary
    /// </summary>
    private string GetRecentActivitySummary()
    {
        // Summarize recent chat messages and user actions
        var recentMessages = ChatMessages.TakeLast(5).Select(m => m.Text);
        return string.Join("; ", recentMessages);
    }

    /// <summary>
    /// Get user profile summary
    /// </summary>
    private async Task<string> GetUserProfileSummary()
    {
        try
        {
            var enterprise = await GetCurrentEnterpriseAsync();

            // Build profile summary based on enterprise and context
            var profileParts = new List<string>();

            if (enterprise.Id > 0)
            {
                profileParts.Add($"Municipal utility manager for {enterprise.Name}");
                profileParts.Add($"Managing service operations for {enterprise.CitizenCount:N0} citizens");
                profileParts.Add($"Current service rate: ${enterprise.CurrentRate:F2} per unit");
                profileParts.Add($"Monthly operating expenses: ${enterprise.MonthlyExpenses:N0}");
            }
            else
            {
                profileParts.Add("Municipal utility manager");
            }

            profileParts.Add("Focused on financial planning, service optimization, and regulatory compliance");
            profileParts.Add("Experienced with budget analysis, rate setting, and infrastructure planning");

            return string.Join(". ", profileParts);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating user profile summary");
            return "Municipal utility manager focused on financial planning and service optimization";
        }
    }

    /// <summary>
    /// Parse scenario string into parameters
    /// </summary>
    private ScenarioParameters ParseScenarioParameters(string scenario)
    {
        var parameters = new ScenarioParameters();

        // Parse pay raise percentage
        var payRaiseMatch = Regex.Match(scenario, @"(\d+(?:\.\d+)?)%?\s*pay\s*raise", RegexOptions.IgnoreCase);
        if (payRaiseMatch.Success)
        {
            parameters.PayRaisePercentage = decimal.Parse(payRaiseMatch.Groups[1].Value);
        }

        // Parse benefits increase
        var benefitsMatch = Regex.Match(scenario, @"benefits?\s*improvement|\$\s*(\d+(?:,\d+)*(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (benefitsMatch.Success && benefitsMatch.Groups[1].Success)
        {
            parameters.BenefitsIncreaseAmount = decimal.Parse(benefitsMatch.Groups[1].Value.Replace(",", ""));
        }

        // Parse reserve percentage
        var reserveMatch = Regex.Match(scenario, @"(\d+(?:\.\d+)?)%?\s*reserve", RegexOptions.IgnoreCase);
        if (reserveMatch.Success)
        {
            parameters.ReservePercentage = decimal.Parse(reserveMatch.Groups[1].Value);
        }

        // Parse equipment purchase
        var equipmentMatch = Regex.Match(scenario, @"(?:equipment\s*purchase(?:\s*for)?\s*)?\$\s*(\d+(?:,\d+)*(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (equipmentMatch.Success && equipmentMatch.Groups[1].Success)
        {
            parameters.EquipmentPurchaseAmount = decimal.Parse(equipmentMatch.Groups[1].Value.Replace(",", ""));
        }

        return parameters;
    }

    /// <summary>
    /// Set conversation mode command
    /// </summary>
    private void SetConversationMode(string mode)
    {
        // Reset all modes
        IsGeneralMode = false;
        IsServiceChargeMode = false;
        IsWhatIfMode = false;
        IsProactiveMode = false;

        // Set selected mode
        switch (mode?.ToLowerInvariant())
        {
            case "general":
                IsGeneralMode = true;
                SelectedMode = AvailableModes[0];
                ShowFinancialInputs = false;
                break;
            case "servicecharge":
                IsServiceChargeMode = true;
                SelectedMode = AvailableModes[1];
                ShowFinancialInputs = true;
                break;
            case "whatif":
                IsWhatIfMode = true;
                SelectedMode = AvailableModes[2];
                ShowFinancialInputs = true;
                break;
            case "proactive":
                IsProactiveMode = true;
                SelectedMode = AvailableModes[3];
                ShowFinancialInputs = true;
                break;
            default:
                IsGeneralMode = true;
                SelectedMode = AvailableModes[0];
                ShowFinancialInputs = false;
                break;
        }

        Log.Information("Conversation mode changed to: {Mode}", SelectedMode?.Name ?? "Unknown");
    }
    /// <summary>
    /// Local anticipatory insights for temporary use
    /// </summary>
    public class LocalAnticipatoryInsights
    {
        public string RecentActivity { get; set; } = string.Empty;
        public string Insights { get; set; } = string.Empty;
        public DateTime GeneratedDate { get; set; }
        public List<string> SuggestedActions { get; set; } = new();
    }
}