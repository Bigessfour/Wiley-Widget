using System;

namespace WileyWidget.Models;

/// <summary>
/// Represents a what-if scenario for utility charge planning
/// Used for simulating the impact of proposed rate increases or expense changes
/// </summary>
public class WhatIfScenario
{
    /// <summary>
    /// Gets or sets the scenario identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the enterprise ID this scenario applies to
    /// </summary>
    public int EnterpriseId { get; set; }

    /// <summary>
    /// Gets or sets the proposed rate increase percentage
    /// </summary>
    public decimal ProposedRateIncrease { get; set; }

    /// <summary>
    /// Gets or sets the proposed expense change percentage
    /// </summary>
    public decimal ProposedExpenseChange { get; set; }

    /// <summary>
    /// Gets or sets the current total revenue baseline
    /// </summary>
    public decimal CurrentRevenue { get; set; }

    /// <summary>
    /// Gets or sets the projected revenue after scenario
    /// </summary>
    public decimal ProjectedRevenue { get; set; }

    /// <summary>
    /// Gets or sets the current total expenses baseline
    /// </summary>
    public decimal CurrentExpenses { get; set; }

    /// <summary>
    /// Gets or sets the projected expenses after scenario
    /// </summary>
    public decimal ProjectedExpenses { get; set; }

    /// <summary>
    /// Gets or sets the current net income (revenue - expenses)
    /// </summary>
    public decimal CurrentNetIncome { get; set; }

    /// <summary>
    /// Gets or sets the projected net income after scenario
    /// </summary>
    public decimal ProjectedNetIncome { get; set; }

    /// <summary>
    /// Gets or sets whether this scenario has been reviewed
    /// </summary>
    public bool IsReviewed { get; set; }

    /// <summary>
    /// Gets or sets whether this scenario has been approved for implementation
    /// </summary>
    public bool IsApproved { get; set; }

    /// <summary>
    /// Gets or sets the date the scenario was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets optional notes about the scenario
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Gets the change in revenue from scenario implementation
    /// </summary>
    public decimal RevenueChange => ProjectedRevenue - CurrentRevenue;

    /// <summary>
    /// Gets the change in expenses from scenario implementation
    /// </summary>
    public decimal ExpenseChange => ProjectedExpenses - CurrentExpenses;

    /// <summary>
    /// Gets the change in net income from scenario implementation
    /// </summary>
    public decimal NetIncomeChange => ProjectedNetIncome - CurrentNetIncome;

    /// <summary>
    /// Gets the percentage change in net income
    /// </summary>
    public decimal NetIncomeChangePercent => 
        CurrentNetIncome != 0 ? (NetIncomeChange / CurrentNetIncome) * 100 : 0;

    // ----- Additional compatibility properties used by services -----

    /// <summary>
    /// Human readable scenario name
    /// </summary>
    public string ScenarioName { get; set; } = string.Empty;

    /// <summary>
    /// Current base rate for the enterprise
    /// </summary>
    public decimal CurrentRate { get; set; }

    /// <summary>
    /// Proposed rate in the scenario
    /// </summary>
    public decimal ProposedRate { get; set; }

    /// <summary>
    /// Current total monthly expenses (baseline)
    /// </summary>
    public decimal CurrentMonthlyExpenses { get; set; }

    /// <summary>
    /// Proposed monthly expenses after scenario adjustments
    /// </summary>
    public decimal ProposedMonthlyExpenses { get; set; }

    /// <summary>
    /// Current monthly revenue baseline
    /// </summary>
    public decimal CurrentMonthlyRevenue { get; set; }

    /// <summary>
    /// Proposed monthly revenue after scenario changes
    /// </summary>
    public decimal ProposedMonthlyRevenue { get; set; }

    /// <summary>
    /// Current monthly balance (surplus/deficit)
    /// </summary>
    public decimal CurrentMonthlyBalance { get; set; }

    /// <summary>
    /// Proposed monthly balance (surplus/deficit)
    /// </summary>
    public decimal ProposedMonthlyBalance { get; set; }

    /// <summary>
    /// Textual impact analysis summary
    /// </summary>
    public string ImpactAnalysis { get; set; } = string.Empty;

    /// <summary>
    /// A list of recommended next steps or mitigations for this scenario
    /// </summary>
    public List<string> Recommendations { get; set; } = new();
}

