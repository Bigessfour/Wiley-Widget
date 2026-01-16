#!/usr/bin/env python3
"""
ViewModel Interface Generator
Generates IXxxViewModel interfaces from XXXViewModel classes for improved testability.
"""

import re
import sys
from pathlib import Path

# List of ViewModels to generate interfaces for
VIEWMODELS_TO_PROCESS = [
    "AnalyticsViewModel",
    "AuditLogViewModel",
    "BudgetOverviewViewModel",
    "BudgetViewModel",
    "ChartViewModel",
    "CustomersViewModel",
    "DashboardViewModel",
    "DepartmentSummaryViewModel",
    "QuickBooksViewModel",
    "RecommendedMonthlyChargeViewModel",
    "ReportsViewModel",
    "RevenueTrendsViewModel",
    "SettingsViewModel",
    "UtilityBillViewModel",
    "WarRoomViewModel",
]

# Mapping of ViewModel names to public observable properties (primary interface contract)
# These are the core properties that should appear in each interface
INTERFACE_PROPERTIES = {
    "AnalyticsViewModel": [
        "bool IsLoading",
        "string StatusText",
        "ObservableCollection<AnalyticsMetric> Metrics",
        "ObservableCollection<VarianceAnalysis> TopVariances",
        "ObservableCollection<MonthlyTrend> TrendData",
        "ObservableCollection<YearlyProjection> ScenarioProjections",
        "ObservableCollection<ForecastPoint> ForecastData",
        "ObservableCollection<string> Insights",
        "ObservableCollection<string> Recommendations",
        "decimal RateIncreasePercentage",
        "decimal ExpenseIncreasePercentage",
        "decimal RevenueTargetPercentage",
        "int ProjectionYears",
        "decimal TotalBudgetedAmount",
        "decimal TotalActualAmount",
        "decimal TotalVarianceAmount",
        "decimal AverageVariancePercentage",
        "string RecommendationExplanation",
    ],
    "AuditLogViewModel": [
        "ObservableCollection<AuditEntry> Entries",
        "ObservableCollection<AuditChartPoint> ChartData",
        "bool IsLoading",
        "bool IsChartLoading",
        "string? ErrorMessage",
        "DateTime StartDate",
        "DateTime EndDate",
        "string? SelectedActionType",
        "string? SelectedUser",
        "int Skip",
        "int Take",
        "int TotalEvents",
        "int PeakEvents",
        "DateTime LastChartUpdated",
        "ChartGroupingPeriod ChartGrouping",
    ],
    "BudgetOverviewViewModel": [
        "ObservableCollection<BudgetCategoryDto> Categories",
        "ObservableCollection<int> AvailableFiscalYears",
        "ObservableCollection<BudgetMetric> Metrics",
        "int FiscalYear",
        "BudgetCategoryDto? SelectedCategory",
        "decimal TotalBudget",
        "decimal TotalActual",
        "decimal TotalEncumbrance",
        "decimal TotalVariance",
        "decimal OverallVariancePercent",
        "int OverBudgetCount",
        "int UnderBudgetCount",
        "DateTime LastUpdated",
        "bool IsLoading",
        "string? ErrorMessage",
    ],
    "BudgetViewModel": [
        "ObservableCollection<BudgetEntry> BudgetEntries",
        "ObservableCollection<BudgetEntry> FilteredBudgetEntries",
        "BudgetPeriod? SelectedPeriod",
        "int SelectedFiscalYear",
        "string ErrorMessage",
        "bool IsLoading",
        "string StatusText",
        "string SearchText",
        "int? SelectedDepartmentId",
        "FundType? SelectedFundType",
        "decimal? VarianceThreshold",
        "bool ShowOnlyOverBudget",
        "bool ShowOnlyUnderBudget",
        "decimal TotalBudgeted",
        "decimal TotalActual",
        "decimal TotalVariance",
        "decimal TotalEncumbrance",
        "decimal PercentUsed",
        "int EntriesOverBudget",
        "int EntriesUnderBudget",
        "string GroupBy",
        "bool ShowHierarchy",
    ],
    "DashboardViewModel": [
        "string MunicipalityName",
        "string FiscalYear",
        "DateTime LastUpdated",
        "bool IsLoading",
        "bool HasError",
        "string? ErrorMessage",
        "ObservableCollection<DashboardMetric> Metrics",
        "float TotalBudgetGauge",
        "float RevenueGauge",
        "float ExpensesGauge",
        "float NetPositionGauge",
        "BudgetVarianceAnalysis? BudgetAnalysis",
        "decimal TotalBudgeted",
        "decimal TotalActual",
        "decimal TotalVariance",
        "decimal VariancePercentage",
        "ObservableCollection<FundSummary> FundSummaries",
        "ObservableCollection<DepartmentSummary> DepartmentSummaries",
        "ObservableCollection<AccountVariance> TopVariances",
        "decimal TotalRevenue",
        "decimal TotalExpenses",
        "decimal NetIncome",
        "int AccountCount",
        "int ActiveDepartments",
        "ObservableCollection<MonthlyRevenue> MonthlyRevenueData",
        "string StatusText",
        "DateTime? LastRefreshTime",
    ],
    "ChartViewModel": [
        "bool IsLoading",
        "string StatusText",
        "string? ErrorMessage",
        "ObservableCollection<ChartDataPoint> ChartData",
        "string ChartType",
        "string Title",
    ],
    "CustomersViewModel": [
        "ObservableCollection<Customer> Customers",
        "Customer? SelectedCustomer",
        "bool IsLoading",
        "string? ErrorMessage",
        "string StatusText",
    ],
    "DepartmentSummaryViewModel": [
        "ObservableCollection<Department> Departments",
        "Department? SelectedDepartment",
        "bool IsLoading",
        "string? ErrorMessage",
        "string StatusText",
        "decimal TotalBudget",
        "decimal TotalActual",
    ],
    "QuickBooksViewModel": [
        "bool IsLoading",
        "bool IsConnected",
        "string ConnectionStatus",
        "string? ErrorMessage",
        "string StatusText",
    ],
    "RecommendedMonthlyChargeViewModel": [
        "bool IsLoading",
        "string StatusText",
        "string? ErrorMessage",
        "ObservableCollection<RecommendedCharge> Charges",
    ],
    "ReportsViewModel": [
        "ObservableCollection<ReportDefinition> Reports",
        "ReportDefinition? SelectedReport",
        "bool IsLoading",
        "string? ErrorMessage",
        "string StatusText",
    ],
    "RevenueTrendsViewModel": [
        "ObservableCollection<RevenueTrendPoint> TrendData",
        "bool IsLoading",
        "string? ErrorMessage",
        "string StatusText",
        "decimal TotalRevenue",
        "decimal AverageMonthlyRevenue",
    ],
    "SettingsViewModel": [
        "string ThemeName",
        "bool EnableNotifications",
        "bool EnableAutoSave",
        "bool IsLoading",
        "string? ErrorMessage",
        "string StatusText",
    ],
    "UtilityBillViewModel": [
        "ObservableCollection<UtilityBill> Bills",
        "UtilityBill? SelectedBill",
        "bool IsLoading",
        "string? ErrorMessage",
        "string StatusText",
        "decimal TotalAmount",
    ],
    "WarRoomViewModel": [
        "ObservableCollection<Alert> Alerts",
        "Alert? SelectedAlert",
        "bool IsLoading",
        "string? ErrorMessage",
        "string StatusText",
        "int CriticalAlertCount",
    ],
}

def generate_interface_file(viewmodel_name: str, properties: list) -> str:
    """Generate interface code for a ViewModel."""
    interface_name = f"I{viewmodel_name}"
    
    # Extract property types and names
    props_code = []
    for prop in properties:
        props_code.append(f"        {prop} {{ get; set; }}")
    
    properties_section = "\n".join(props_code)
    
    interface_code = f'''#nullable enable

using System;
using System.Collections.ObjectModel;

namespace WileyWidget.WinForms.ViewModels
{{
    /// <summary>
    /// Interface for the {viewmodel_name}.
    /// Defines the contract for dependency injection and testability.
    /// Excludes auto-generated RelayCommand properties since they're generated by MVVM Toolkit.
    /// </summary>
    public interface {interface_name} : System.ComponentModel.INotifyPropertyChanged
    {{
{properties_section}
    }}
}}
'''
    return interface_code

def main():
    """Generate all ViewModel interfaces."""
    workspace_root = Path("src/WileyWidget.WinForms/ViewModels")
    
    if not workspace_root.exists():
        print(f"Error: ViewModels directory not found: {workspace_root}")
        sys.exit(1)
    
    generated_count = 0
    for viewmodel_name, properties in INTERFACE_PROPERTIES.items():
        interface_name = f"I{viewmodel_name}.cs"
        interface_path = workspace_root / interface_name
        
        # Skip if already exists
        if interface_path.exists():
            print(f"⊘ {interface_name} already exists - skipping")
            continue
        
        code = generate_interface_file(viewmodel_name, properties)
        interface_path.write_text(code, encoding="utf-8")
        print(f"✓ Generated {interface_name}")
        generated_count += 1
    
    print(f"\n{generated_count} interfaces generated successfully")
    return 0

if __name__ == "__main__":
    sys.exit(main())
