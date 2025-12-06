using System.Text.Json;

namespace WileyWidget.Models;

/// <summary>
/// Finance-specific AI tool definitions for xAI integration with IAIToolService
/// These tools provide structured access to municipal finance operations
/// </summary>
public static class FinancialAITools
{
    /// <summary>
    /// Available finance tools for xAI function calling
    /// Maps to IAIToolService methods for actual execution
    /// </summary>
    public static readonly AITool[] AvailableTools =
    [
        new AITool(
            "get_budget_data",
            "Retrieves budget data for a fiscal year and optional fund type. Returns budget entries with actual/budgeted amounts.",
            JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    fiscalYear = new { type = "integer", description = "The fiscal year to retrieve data for (e.g., 2026)" },
                    fundType = new { type = "string", description = "Optional fund type filter: 'Enterprise', 'General', 'ConservationTrust', 'CapitalProjects'" }
                },
                required = new[] { "fiscalYear" }
            })
        ),
        new AITool(
            "analyze_budget_trends",
            "Analyzes budget trends for an account over a time period. Returns trend data with percentage changes and forecasts.",
            JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    accountId = new { type = "integer", description = "The municipal account ID to analyze" },
                    period = new { type = "string", description = "Time period: 'monthly', 'quarterly', or 'annual'", @enum = new[] { "monthly", "quarterly", "annual" } }
                },
                required = new[] { "accountId" }
            })
        ),
        new AITool(
            "generate_insight",
            "Generates AI-powered insights and recommendations based on query and optional data context.",
            JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "The user's query or question about financial data" },
                    dataContext = new { type = "object", description = "Optional structured data for analysis (budget entries, accounts, etc.)" }
                },
                required = new[] { "query" }
            })
        ),
        new AITool(
            "create_report",
            "Creates financial reports of specified type with given parameters.",
            JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    reportType = new { type = "string", description = "Report type: 'compliance', 'summary', 'variance', 'budget'", @enum = new[] { "compliance", "summary", "variance", "budget" } },
                    parameters = new 
                    { 
                        type = "object", 
                        description = "Report parameters (fiscalYear, fundType, dateRange, etc.)",
                        properties = new
                        {
                            fiscalYear = new { type = "integer", description = "Fiscal year for the report" },
                            fundType = new { type = "string", description = "Fund type filter" },
                            startDate = new { type = "string", format = "date", description = "Start date for date range" },
                            endDate = new { type = "string", format = "date", description = "End date for date range" }
                        }
                    }
                },
                required = new[] { "reportType", "parameters" }
            })
        ),
        new AITool(
            "recommend_charges",
            "Analyzes utility charges and recommends optimizations for customers.",
            JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    utilityType = new { type = "string", description = "Type of utility: 'Water', 'Sewer', 'Garbage', 'Electric'", @enum = new[] { "Water", "Sewer", "Garbage", "Electric" } },
                    customerId = new { type = "integer", description = "The customer ID to analyze" }
                },
                required = new[] { "utilityType", "customerId" }
            })
        ),
        new AITool(
            "query_accounts",
            "Queries municipal accounts with optional filtering by number, name, type, or fund.",
            JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    filter = new { type = "string", description = "Optional search filter (account number, name, type, etc.)" }
                }
            })
        ),
        new AITool(
            "simulate_scenario",
            "Simulates financial scenarios for what-if analysis and planning.",
            JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    scenarioType = new { type = "string", description = "Scenario type: 'tax_increase', 'rate_change', 'expense_cut', 'revenue_growth'", @enum = new[] { "tax_increase", "rate_change", "expense_cut", "revenue_growth" } },
                    parameters = new 
                    { 
                        type = "object", 
                        description = "Scenario parameters",
                        properties = new
                        {
                            percentageChange = new { type = "number", description = "Percentage change (e.g., 5 for 5% increase)" },
                            durationMonths = new { type = "integer", description = "Duration in months for the scenario" },
                            affectedAccounts = new { type = "array", items = new { type = "integer" }, description = "Account IDs affected by scenario" }
                        }
                    }
                },
                required = new[] { "scenarioType", "parameters" }
            })
        ),
        new AITool(
            "detect_anomalies",
            "Detects anomalies in budget or financial data for risk identification.",
            JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    dataType = new { type = "string", description = "Data type: 'budget', 'revenue', 'expenses'", @enum = new[] { "budget", "revenue", "expenses" } },
                    timeWindowDays = new { type = "integer", description = "Days to look back for analysis (default: 90)" }
                }
            })
        ),
        new AITool(
            "get_account_details",
            "Retrieves detailed information for a specific municipal account including transactions and balances.",
            JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    accountId = new { type = "integer", description = "The municipal account ID" },
                    includeTransactions = new { type = "boolean", description = "Include transaction history (default: true)" },
                    includeChildren = new { type = "boolean", description = "Include child accounts in hierarchy (default: false)" }
                },
                required = new[] { "accountId" }
            })
        )
    ];

    /// <summary>
    /// Get all available tools (combines IDE tools and financial tools)
    /// </summary>
    public static AITool[] GetAllTools()
    {
        return [..AITool.AvailableTools, ..AvailableTools];
    }

    /// <summary>
    /// Get tool definition by name for xAI function calling
    /// </summary>
    public static AITool? GetToolByName(string name)
    {
        return Array.Find(GetAllTools(), t => t.Name == name);
    }
}
