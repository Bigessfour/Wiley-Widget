# AI-Powered Grid Features Implementation Guide

## Overview

This document provides implementation guidance for integrating Semantic Search and Anomaly Detection features into Syncfusion SfDataGrid controls throughout the WileyWidget application.

## Features Implemented

### 1. Semantic Search Service (`ISemanticSearchService`)

**Purpose**: Enable natural language search in DataGrids where users can find data by meaning rather than exact keywords.

**Example Use Cases**:

- AccountsPanel: "water service" finds "Water Utility - Residential"
- CustomersPanel: "John's house on Main" finds customer by fuzzy address
- BudgetPanel: "road repairs" finds "Street Maintenance - Asphalt Overlay"

**Implementation**:

```csharp
// Inject the service
private readonly ISemanticSearchService _semanticSearch;

// In async search method
var results = await _semanticSearch.SearchAsync(
    allAccounts,
    searchText,
    account => $"{account.Name} {account.Description} {account.Category}",
    threshold: 0.6
);

// Bind results to grid
_grid.DataSource = results.Select(r => r.Item).ToList();
```

**Fallback Behavior**: If embeddings API is unavailable, automatically falls back to enhanced keyword search.

### 2. Anomaly Detection Service (`IAnomalyDetectionService`)

**Purpose**: Automatically identify unusual patterns in financial data with AI-generated explanations.

**Example Use Cases**:

- Budget variance analysis: Explain why Q3 had 300% overspend
- Revenue trend analysis: Identify why August water revenue dropped 40%
- Utility bill analysis: Flag customers with unusual consumption

**Implementation**:

```csharp
// Inject the service
private readonly IAnomalyDetectionService _anomalyDetection;

// Detect anomalies in budget data
var anomalies = await _anomalyDetection.DetectAnomaliesAsync(
    budgetEntries,
    entry => (double)entry.ActualAmount,
    entry => $"{entry.AccountName} - {entry.Period}"
);

// Show results in grid or dialog
foreach (var anomaly in anomalies)
{
    Console.WriteLine($"[{anomaly.Severity:P0}] {anomaly.Explanation}");
    // anomaly.Item contains the original data item
}
```

**Budget Variance Analysis**:

```csharp
var analysis = await _anomalyDetection.AnalyzeBudgetVarianceAsync(
    budgetedAmount: 50000m,
    actualAmount: 150000m,
    accountName: "Street Maintenance",
    period: "Q3 2026"
);

if (analysis.IsAnomaly)
{
    MessageBox.Show(
        $"Anomaly Detected (Severity: {analysis.Severity:P0})\n\n" +
        $"{analysis.Explanation}\n\n" +
        $"Recommended Actions:\n" +
        string.Join("\n", analysis.RecommendedActions),
        "Budget Anomaly Alert"
    );
}
```

## Integration Points

### Priority 1: AccountsPanel (Semantic Search)

**File**: `src/WileyWidget.WinForms/Controls/AccountsPanel.cs`

**Add toolbar button**:

```csharp
var semanticSearchButton = new ToolStripButton
{
    Text = "🔍 Smart Search",
    ToolTipText = "Search by meaning (AI-powered)"
};
semanticSearchButton.Click += async (s, e) => await PerformSemanticSearchAsync();
```

**Implement search**:

```csharp
private async Task PerformSemanticSearchAsync()
{
    var searchDialog = new SemanticSearchDialog();
    if (searchDialog.ShowDialog() == DialogResult.OK)
    {
        var searchText = searchDialog.SearchText;
        var results = await _semanticSearch.SearchAsync(
            _viewModel.Accounts,
            searchText,
            account => $"{account.AccountCode} {account.Name} {account.Description}",
            threshold: 0.65
        );

        // Update grid data source
        _accountsGrid.DataSource = results.Select(r => r.Item).ToList();

        // Show match quality
        _statusLabel.Text = $"Found {results.Count} results (best match: {results.FirstOrDefault()?.SimilarityScore:P0})";
    }
}
```

### Priority 2: BudgetPanel (Anomaly Detection)

**File**: `src/WileyWidget.WinForms/Controls/BudgetPanel.cs`

**Add toolbar button**:

```csharp
var detectAnomaliesButton = new ToolStripButton
{
    Text = "⚠️ Detect Anomalies",
    ToolTipText = "AI-powered anomaly detection"
};
detectAnomaliesButton.Click += async (s, e) => await DetectBudgetAnomaliesAsync();
```

**Implement detection**:

```csharp
private async Task DetectBudgetAnomaliesAsync()
{
    using var loadingDialog = new LoadingDialog("Analyzing budget data...");
    loadingDialog.Show();

    var anomalies = await _anomalyDetection.DetectAnomaliesAsync(
        _viewModel.BudgetEntries,
        entry => (double)(entry.ActualAmount - entry.BudgetedAmount),
        entry => $"{entry.AccountName} ({entry.FiscalPeriod})"
    );

    loadingDialog.Close();

    if (anomalies.Count == 0)
    {
        MessageBox.Show("No anomalies detected in current budget data.", "Analysis Complete");
        return;
    }

    var anomalyForm = new AnomalyResultsForm(anomalies);
    anomalyForm.ShowDialog();
}
```

### Priority 3: CustomersPanel (Semantic Search)

Similar to AccountsPanel but search across customer name, address, and account number.

## UI Components to Create

### 1. SemanticSearchDialog.cs

Simple input dialog with:

- TextBox for natural language query
- Label showing "Try: 'water accounts', 'street maintenance', etc."
- Slider for similarity threshold (0.5 to 0.9)
- OK/Cancel buttons

### 2. AnomalyResultsForm.cs

Data grid showing anomalies with columns:

- Severity (progress bar)
- Item Description
- Actual Value
- Expected Range
- Explanation (expandable)
- Actions (drill-down to source data)

### 3. LoadingDialog.cs

Modal progress dialog for long-running AI operations.

## Configuration

### appsettings.json

No additional configuration required - services use existing `XAI_API_KEY`.

### Service Registration

Already registered in `DependencyInjection.cs`:

```csharp
services.AddSingleton<ISemanticSearchService, SemanticSearchService>();
services.AddSingleton<IAnomalyDetectionService, AnomalyDetectionService>();
```

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task SemanticSearch_FindsRelevantAccounts()
{
    // Arrange
    var accounts = GetTestAccounts();
    var searchService = new SemanticSearchService(config, logger);

    // Act
    var results = await searchService.SearchAsync(
        accounts,
        "water service",
        a => $"{a.Name} {a.Description}",
        threshold: 0.6
    );

    // Assert
    Assert.Contains(results, r => r.Item.Name.Contains("Water"));
}
```

### Integration Tests

Test with real grid controls and mock data.

### E2E Tests

Test full user workflow: click search button → enter query → verify results.

## Performance Considerations

### Semantic Search

- **First Call**: ~500ms (embedding generation)
- **Cached Results**: < 50ms
- **Max Items**: Optimize for < 1000 items (for larger datasets, batch processing)

### Anomaly Detection

- **Small Dataset** (< 100 items): ~2-3 seconds
- **Medium Dataset** (100-500 items): ~5-10 seconds
- **Large Dataset** (> 500 items): Use statistical fallback

### Optimization Tips

1. Cache embeddings for frequently searched datasets
2. Debounce search input (wait 300ms after typing stops)
3. Show progress indicator for operations > 1 second
4. Implement cancellation tokens for long operations

## Fallback Behavior

Both services gracefully degrade when AI is unavailable:

**Semantic Search Fallback**: Enhanced keyword matching

- Splits query into keywords
- Scores by keyword match percentage
- Returns ranked results

**Anomaly Detection Fallback**: Statistical analysis

- Z-score calculation (> 2.5 σ flagged)
- Standard deviation-based thresholds
- Generic recommendations

## API Limitations

### Grok API Notes

1. May not support embeddings endpoint yet (fallback implemented)
2. Rate limits: ~60 requests/minute
3. Token limits: ~4096 tokens per request

### Workarounds

- Batch similar searches
- Cache results aggressively
- Use statistical fallback for high-volume operations

## Next Steps

1. **Phase 1 (This Sprint)**: Implement semantic search in AccountsPanel
2. **Phase 2 (Next Sprint)**: Add anomaly detection to BudgetPanel
3. **Phase 3 (Future)**: Expand to CustomersPanel, UtilityBillPanel
4. **Phase 4 (Optional)**: Implement caching layer for embeddings

## References

- Syncfusion Smart AI Samples: https://github.com/syncfusion/smart-ai-samples
- Microsoft Semantic Kernel: https://learn.microsoft.com/en-us/semantic-kernel/
- Cosine Similarity: https://en.wikipedia.org/wiki/Cosine_similarity

## Support

For questions or issues:

1. Check logs in `logs/` directory (semantic search and anomaly detection are logged)
2. Verify API key configuration in environment variables
3. Test with fallback mode (should work without AI)
4. Review this document's troubleshooting section

---

**Last Updated**: January 7, 2026
**Status**: Phase 1 Services Implemented, Ready for Grid Integration
