using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Abstractions.Models;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Interface for AI tool functions that Grok can invoke for municipal finance tasks
/// Provides structured data access and analysis capabilities via function calling
/// </summary>
public interface IAIToolService
{
    /// <summary>
    /// Retrieves budget data for a fiscal year and optional fund type
    /// Used by: Query Data tool in Grok for budget questions
    /// </summary>
    /// <param name="fiscalYear">The fiscal year to retrieve data for</param>
    /// <param name="fundType">Optional fund type filter (e.g., "Enterprise", "General")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Budget data including entries and summaries</returns>
    Task<object> GetBudgetDataAsync(
        int fiscalYear,
        string? fundType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes budget trends for an account over a time period
    /// Used by: Generate Insights tool for trend analysis
    /// </summary>
    /// <param name="accountId">The municipal account ID to analyze</param>
    /// <param name="period">Time period for analysis ("monthly", "quarterly", "annual")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of budget trend items with calculations</returns>
    Task<List<BudgetTrendItem>> AnalyzeBudgetTrendsAsync(
        int accountId,
        string period = "monthly",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates AI insights based on query and optional data context
    /// Used by: Generate Insights tool for recommendations
    /// </summary>
    /// <param name="query">The user's query or question</param>
    /// <param name="dataContext">Optional structured data context for analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI-generated insights and recommendations</returns>
    Task<BudgetInsights> GenerateInsightAsync(
        string query,
        object? dataContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a report of specified type with given parameters
    /// Used by: Create Reports tool for generating compliance/summary reports
    /// </summary>
    /// <param name="reportType">Type of report ("compliance", "summary", "variance", "budget")</param>
    /// <param name="parameters">Report-specific parameters (fiscal year, fund type, date range, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated report data</returns>
    Task<object> CreateReportAsync(
        string reportType,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes utility charges and recommends optimizations
    /// Used by: Interactive Tools for "what-if" scenarios
    /// </summary>
    /// <param name="utilityType">Type of utility service (e.g., "Water", "Sewer", "Garbage")</param>
    /// <param name="customerId">The customer ID to analyze charges for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Charge recommendations with analysis</returns>
    Task<ServiceChargeRecommendation> RecommendChargesAsync(
        string utilityType,
        int customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries municipal accounts with optional filtering
    /// Used by: Query Data tool for account information
    /// </summary>
    /// <param name="filter">Optional filter criteria (account number, name, type, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of municipal account display models</returns>
    Task<List<MunicipalAccountDisplay>> QueryAccountsAsync(
        string? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates financial scenarios (what-if analysis)
    /// Used by: Interactive Tools for scenario planning
    /// </summary>
    /// <param name="scenarioType">Type of scenario ("tax_increase", "rate_change", "expense_cut", etc.)</param>
    /// <param name="parameters">Scenario-specific parameters (percentage change, duration, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scenario results and projections</returns>
    Task<object> SimulateScenarioAsync(
        string scenarioType,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects anomalies in budget or financial data
    /// Used by: Generate Insights tool for risk identification
    /// </summary>
    /// <param name="dataType">Type of data to analyze ("budget", "revenue", "expenses")</param>
    /// <param name="timeWindowDays">Number of days to look back for analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected anomalies with details</returns>
    Task<List<object>> DetectAnomaliesAsync(
        string dataType = "budget",
        int timeWindowDays = 90,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the JSON schema for a tool function for Grok function calling
    /// </summary>
    /// <param name="toolName">The name of the tool function</param>
    /// <returns>JSON schema definition for the tool</returns>
    string GetToolSchema(string toolName);

    /// <summary>
    /// Gets all available tool schemas for Grok function calling setup
    /// </summary>
    /// <returns>Dictionary of tool names to their schemas</returns>
    Dictionary<string, string> GetAllToolSchemas();
}
