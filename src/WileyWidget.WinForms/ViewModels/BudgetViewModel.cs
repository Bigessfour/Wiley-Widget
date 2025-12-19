using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for budget management with full CRUD operations.
    /// Provides filtering, analysis, and export capabilities for budget entries.
    /// </summary>
    public partial class BudgetViewModel : ObservableObject, IDisposable
    {
        private readonly ILogger<BudgetViewModel> _logger;
        private readonly IBudgetRepository _budgetRepository;
        private readonly IReportExportService _reportExportService;

        [ObservableProperty]
        private ObservableCollection<BudgetEntry> budgetEntries = new();

        [ObservableProperty]
        private ObservableCollection<BudgetEntry> filteredBudgetEntries = new();

        [ObservableProperty]
        private BudgetPeriod? selectedPeriod;

        [ObservableProperty]
        private int selectedFiscalYear = DateTime.Now.Year;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string statusText = "Ready";

        // Advanced filtering
        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private int? selectedDepartmentId;

        [ObservableProperty]
        private FundType? selectedFundType;

        [ObservableProperty]
        private decimal? varianceThreshold;

        [ObservableProperty]
        private bool showOnlyOverBudget;

        [ObservableProperty]
        private bool showOnlyUnderBudget;

        // Analysis properties
        [ObservableProperty]
        private decimal totalBudgeted;

        [ObservableProperty]
        private decimal totalActual;

        [ObservableProperty]
        private decimal totalVariance;

        [ObservableProperty]
        private decimal totalEncumbrance;

        [ObservableProperty]
        private decimal percentUsed;

        [ObservableProperty]
        private int entriesOverBudget;

        [ObservableProperty]
        private int entriesUnderBudget;

        // Grouping
        [ObservableProperty]
        private string groupBy = "None";

        [ObservableProperty]
        private bool showHierarchy;

        /// <summary>Gets the command to load budget entries.</summary>
        public IAsyncRelayCommand LoadBudgetsCommand { get; }

        /// <summary>Gets the command to load budgets by fiscal year.</summary>
        public IAsyncRelayCommand LoadByYearCommand { get; }

        /// <summary>Gets the command to import budget entries from CSV.</summary>
        public IAsyncRelayCommand<string> ImportFromCsvCommand { get; }

        /// <summary>Gets the command to export budget entries to CSV.</summary>
        public IAsyncRelayCommand<string> ExportToCsvCommand { get; }

        /// <summary>Gets the command to export budget entries to PDF.</summary>
        public IAsyncRelayCommand<string> ExportToPdfCommand { get; }

        /// <summary>Gets the command to export budget entries to Excel.</summary>
        public IAsyncRelayCommand<string> ExportToExcelCommand { get; }

        /// <summary>Gets the command to apply current filters.</summary>
        public IAsyncRelayCommand ApplyFiltersCommand { get; }

        /// <summary>Gets the command to clear all filters.</summary>
        public IAsyncRelayCommand ClearFiltersCommand { get; }

        /// <summary>Gets the command to copy a budget entry to next year.</summary>
        public IAsyncRelayCommand<BudgetEntry> CopyToNextYearCommand { get; }

        /// <summary>Gets the command to bulk adjust budget amounts.</summary>
        public IAsyncRelayCommand<decimal> BulkAdjustCommand { get; }

        /// <summary>Gets the command to calculate variances.</summary>
        public IAsyncRelayCommand CalculateVariancesCommand { get; }

        /// <summary>Gets the command to refresh analysis totals.</summary>
        public IAsyncRelayCommand RefreshAnalysisCommand { get; }

        public BudgetViewModel(ILogger<BudgetViewModel>? logger, IBudgetRepository? budgetRepository, IReportExportService? reportExportService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));

            LoadBudgetsCommand = new AsyncRelayCommand(LoadBudgetsAsync);
            LoadByYearCommand = new AsyncRelayCommand(LoadBudgetsAsync);
            ImportFromCsvCommand = new AsyncRelayCommand<string>(ImportFromCsvAsync);
            ExportToCsvCommand = new AsyncRelayCommand<string>(ExportToCsvAsync);

            // Export service - DI should provide implementations; tests must mock this
            _reportExportService = reportExportService ?? throw new ArgumentNullException(nameof(reportExportService));

            ExportToPdfCommand = new AsyncRelayCommand<string>(ExportToPdfAsync);
            ExportToExcelCommand = new AsyncRelayCommand<string>(ExportToExcelAsync);
            ApplyFiltersCommand = new AsyncRelayCommand(ApplyFiltersAsync);
            ClearFiltersCommand = new AsyncRelayCommand(ClearFiltersAsync);
            CopyToNextYearCommand = new AsyncRelayCommand<BudgetEntry>(CopyToNextYearAsync);
            BulkAdjustCommand = new AsyncRelayCommand<decimal>(BulkAdjustAsync);
            CalculateVariancesCommand = new AsyncRelayCommand(CalculateVariancesAsync);
            RefreshAnalysisCommand = new AsyncRelayCommand(RefreshAnalysisAsync);

            // Property change handlers for auto-filtering
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SearchText) ||
                    e.PropertyName == nameof(SelectedDepartmentId) ||
                    e.PropertyName == nameof(SelectedFundType) ||
                    e.PropertyName == nameof(ShowOnlyOverBudget) ||
                    e.PropertyName == nameof(ShowOnlyUnderBudget) ||
                    e.PropertyName == nameof(VarianceThreshold))
                {
                    _ = ApplyFiltersAsync();
                }
            };
        }

        private async Task LoadBudgetsAsync()
        {
            IsLoading = true;
            try
            {
                var year = SelectedFiscalYear > 0 ? SelectedFiscalYear : (SelectedPeriod?.Year ?? DateTime.Now.Year);
                var entries = await _budgetRepository.GetByFiscalYearAsync(year);
                BudgetEntries = new ObservableCollection<BudgetEntry>(entries);
                _logger.LogInformation("Loaded {Count} budget entries for year {Year}", BudgetEntries.Count, year);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading budgets: {ex.Message}";
                _logger.LogError(ex, "Budget load failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Adds a new budget entry asynchronously.
        /// </summary>
        /// <param name="entry">The budget entry to add.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task AddEntryAsync(BudgetEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry == null) return;
            try
            {
                await _budgetRepository.AddAsync(entry);
                BudgetEntries.Add(entry);
                _logger.LogInformation("Added budget entry {Id}", entry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddEntryAsync failed");
                ErrorMessage = ex.Message;
            }
        }

        /// <summary>
        /// Updates an existing budget entry asynchronously.
        /// </summary>
        /// <param name="entry">The budget entry to update.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UpdateEntryAsync(BudgetEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry == null) return;
            try
            {
                await _budgetRepository.UpdateAsync(entry);
                var existing = BudgetEntries.FirstOrDefault(e => e.Id == entry.Id);
                if (existing != null)
                {
                    var idx = BudgetEntries.IndexOf(existing);
                    if (idx >= 0)
                    {
                        BudgetEntries[idx] = entry;
                    }
                }
                _logger.LogInformation("Updated budget entry {Id}", entry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateEntryAsync failed for {Id}", entry.Id);
                ErrorMessage = ex.Message;
            }
        }

        /// <summary>
        /// Deletes a budget entry asynchronously.
        /// </summary>
        /// <param name="id">The ID of the budget entry to delete.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task DeleteEntryAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                await _budgetRepository.DeleteAsync(id);
                var existing = BudgetEntries.FirstOrDefault(e => e.Id == id);
                if (existing != null)
                {
                    BudgetEntries.Remove(existing);
                }
                _logger.LogInformation("Deleted budget entry {Id}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteEntryAsync failed for {Id}", id);
                ErrorMessage = ex.Message;
            }
        }

        private async Task ImportFromCsvAsync(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ErrorMessage = "No file selected for import.";
                return;
            }

            if (!File.Exists(filePath))
            {
                ErrorMessage = "Selected file does not exist.";
                return;
            }

            IsLoading = true;
            var imported = new List<BudgetEntry>();
            var errors = new List<string>();

            try
            {
                using var stream = File.OpenRead(filePath);
                using var reader = new StreamReader(stream);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    HeaderValidated = null,
                    TrimOptions = TrimOptions.Trim
                };

                using var csv = new CsvReader(reader, config);

                // Use a small DTO to avoid trying to map complex navigation properties
                var records = csv.GetRecordsAsync<ImportBudgetRow>();
                int rowNumber = 0;

                await foreach (var rec in records)
                {
                    rowNumber++;
                    try
                    {
                        // Basic required checks
                        if (string.IsNullOrWhiteSpace(rec.AccountNumber))
                        {
                            errors.Add($"Row {rowNumber}: AccountNumber is required.");
                            continue;
                        }

                        // Build domain object
                        var entry = new BudgetEntry
                        {
                            AccountNumber = rec.AccountNumber.Trim(),
                            Description = rec.Description ?? string.Empty,
                            BudgetedAmount = rec.BudgetedAmount,
                            ActualAmount = rec.ActualAmount,
                            FiscalYear = rec.FiscalYear == 0 ? (SelectedPeriod?.Year ?? DateTime.Now.Year) : rec.FiscalYear,
                            DepartmentId = rec.DepartmentId,
                            FundId = rec.FundId,
                            SourceFilePath = filePath,
                            SourceRowNumber = rowNumber,
                            CreatedAt = DateTime.UtcNow
                        };

                        // Validate using data annotations on the model
                        var validationResults = new List<ValidationResult>();
                        var context = new ValidationContext(entry);
                        if (!Validator.TryValidateObject(entry, context, validationResults, true))
                        {
                            var msg = string.Join(';', validationResults.Select(v => v.ErrorMessage));
                            errors.Add($"Row {rowNumber}: {msg}");
                            continue;
                        }

                        await _budgetRepository.AddAsync(entry);
                        BudgetEntries.Add(entry);
                        imported.Add(entry);
                    }
                    catch (Exception exRow)
                    {
                        errors.Add($"Row {rowNumber}: {exRow.Message}");
                    }
                }

                if (imported.Count > 0)
                {
                    _logger.LogInformation("Imported {Count} budget entries from {File}", imported.Count, filePath);
                }
                if (errors.Count > 0)
                {
                    ErrorMessage = string.Join("\n", errors.Take(25));
                    _logger.LogWarning("Import had {Count} issues. Example: {Sample}", errors.Count, errors.FirstOrDefault());
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                _logger.LogError(ex, "ImportFromCsvAsync failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportToCsvAsync(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ErrorMessage = "No file selected for export.";
                return;
            }

            IsLoading = true;
            try
            {
                using var fs = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(fs);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    NewLine = Environment.NewLine
                };

                using var csv = new CsvWriter(writer, config);

                // Simple export DTO to keep columns stable
                csv.WriteHeader<ExportBudgetRow>();
                await csv.NextRecordAsync();

                foreach (var be in BudgetEntries)
                {
                    var row = new ExportBudgetRow
                    {
                        AccountNumber = be.AccountNumber,
                        Description = be.Description,
                        BudgetedAmount = be.BudgetedAmount,
                        ActualAmount = be.ActualAmount,
                        FiscalYear = be.FiscalYear,
                        DepartmentId = be.DepartmentId,
                        FundId = be.FundId
                    };

                    csv.WriteRecord(row);
                    await csv.NextRecordAsync();
                }

                await writer.FlushAsync();
                _logger.LogInformation("Exported {Count} budget entries to {File}", BudgetEntries.Count, filePath);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                _logger.LogError(ex, "ExportToCsvAsync failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportToPdfAsync(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ErrorMessage = "No file selected for export.";
                return;
            }

            IsLoading = true;
            try
            {
                await _reportExportService.ExportToPdfAsync(BudgetEntries.ToList(), filePath);
                _logger.LogInformation("Exported {Count} budget entries to PDF {File}", BudgetEntries.Count, filePath);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                _logger.LogError(ex, "ExportToPdfAsync failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportToExcelAsync(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ErrorMessage = "No file selected for export.";
                return;
            }

            IsLoading = true;
            try
            {
                await _reportExportService.ExportToExcelAsync(BudgetEntries.ToList(), filePath);
                _logger.LogInformation("Exported {Count} budget entries to Excel {File}", BudgetEntries.Count, filePath);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                _logger.LogError(ex, "ExportToExcelAsync failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Small DTO used for CSV import mapping
        /// </summary>
        private sealed record ImportBudgetRow
        {
            public string? AccountNumber { get; init; }
            public string? Description { get; init; }
            public decimal BudgetedAmount { get; init; }
            public decimal ActualAmount { get; init; }
            public int FiscalYear { get; init; }
            public int DepartmentId { get; init; }
            public int? FundId { get; init; }
        }

        /// <summary>
        /// Small DTO used for CSV export mapping
        /// </summary>
        private sealed record ExportBudgetRow
        {
            public string AccountNumber { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public decimal BudgetedAmount { get; init; }
            public decimal ActualAmount { get; init; }
            public int FiscalYear { get; init; }
            public int DepartmentId { get; init; }
            public int? FundId { get; init; }
        }

        // ============= Advanced Filtering Methods =============

        private async Task ApplyFiltersAsync()
        {
            var filteredList = await Task.Run(() =>
            {
                var filtered = BudgetEntries.AsEnumerable();

                // Search text filter
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var search = SearchText.Trim().ToLowerInvariant();
                    filtered = filtered.Where(e =>
                        e.AccountNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        e.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
                }

                // Department filter
                if (SelectedDepartmentId.HasValue)
                {
                    filtered = filtered.Where(e => e.DepartmentId == SelectedDepartmentId.Value);
                }

                // Fund type filter
                if (SelectedFundType.HasValue)
                {
                    filtered = filtered.Where(e => e.FundType == SelectedFundType.Value);
                }

                // Variance filters
                if (ShowOnlyOverBudget)
                {
                    filtered = filtered.Where(e => e.ActualAmount > e.BudgetedAmount);
                }
                else if (ShowOnlyUnderBudget)
                {
                    filtered = filtered.Where(e => e.ActualAmount < e.BudgetedAmount);
                }

                if (VarianceThreshold.HasValue)
                {
                    filtered = filtered.Where(e => Math.Abs(e.BudgetedAmount - e.ActualAmount) >= VarianceThreshold.Value);
                }

                return filtered.ToList();
            });

            FilteredBudgetEntries = new ObservableCollection<BudgetEntry>(filteredList);
            _logger.LogInformation("Applied filters: {Count} entries match criteria", FilteredBudgetEntries.Count);

            await RefreshAnalysisAsync();
        }

        private Task ClearFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedDepartmentId = null;
            SelectedFundType = null;
            VarianceThreshold = null;
            ShowOnlyOverBudget = false;
            ShowOnlyUnderBudget = false;
            FilteredBudgetEntries = new ObservableCollection<BudgetEntry>(BudgetEntries);
            _logger.LogInformation("Filters cleared");
            return Task.CompletedTask;
        }

        // ============= Analysis Methods =============

        private async Task RefreshAnalysisAsync()
        {
            var entries = FilteredBudgetEntries.Any() ? FilteredBudgetEntries : BudgetEntries;

            // Compute analysis totals on background thread
            var totals = await Task.Run(() =>
            {
                var list = entries.ToList();
                var totalBudgeted = list.Sum(e => e.BudgetedAmount);
                var totalActual = list.Sum(e => e.ActualAmount);
                var totalVariance = totalBudgeted - totalActual;
                var totalEncumbrance = list.Sum(e => e.EncumbranceAmount);
                var percentUsed = totalBudgeted > 0 ? (totalActual / totalBudgeted) * 100 : 0;
                var entriesOverBudget = list.Count(e => e.ActualAmount > e.BudgetedAmount);
                var entriesUnderBudget = list.Count(e => e.ActualAmount < e.BudgetedAmount);
                return (totalBudgeted, totalActual, totalVariance, totalEncumbrance, percentUsed, entriesOverBudget, entriesUnderBudget);
            });

            // Assign computed values to properties on UI thread
            TotalBudgeted = totals.totalBudgeted;
            TotalActual = totals.totalActual;
            TotalVariance = totals.totalVariance;
            TotalEncumbrance = totals.totalEncumbrance;
            PercentUsed = totals.percentUsed;
            EntriesOverBudget = totals.entriesOverBudget;
            EntriesUnderBudget = totals.entriesUnderBudget;

            _logger.LogInformation(
                "Budget analysis: Total Budgeted={Budgeted:C}, Actual={Actual:C}, Variance={Variance:C}, {PercentUsed:F2}% used",
                TotalBudgeted, TotalActual, TotalVariance, PercentUsed);
        }

        private async Task CalculateVariancesAsync()
        {
            IsLoading = true;
            try
            {
                foreach (var entry in BudgetEntries)
                {
                    entry.Variance = entry.BudgetedAmount - entry.ActualAmount;
                    await _budgetRepository.UpdateAsync(entry);
                }
                _logger.LogInformation("Calculated variances for {Count} entries", BudgetEntries.Count);
                await RefreshAnalysisAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error calculating variances: {ex.Message}";
                _logger.LogError(ex, "CalculateVariancesAsync failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ============= Bulk Operations =============

        private async Task CopyToNextYearAsync(BudgetEntry? entry)
        {
            if (entry == null) return;

            IsLoading = true;
            try
            {
                var newEntry = new BudgetEntry
                {
                    AccountNumber = entry.AccountNumber,
                    Description = entry.Description,
                    BudgetedAmount = entry.BudgetedAmount,
                    ActualAmount = 0,
                    Variance = entry.BudgetedAmount,
                    FiscalYear = entry.FiscalYear + 1,
                    DepartmentId = entry.DepartmentId,
                    FundId = entry.FundId,
                    FundType = entry.FundType,
                    EncumbranceAmount = 0,
                    ParentId = entry.ParentId,
                    StartPeriod = entry.StartPeriod.AddYears(1),
                    EndPeriod = entry.EndPeriod.AddYears(1),
                    CreatedAt = DateTime.UtcNow
                };

                await _budgetRepository.AddAsync(newEntry);
                _logger.LogInformation("Copied budget entry {AccountNumber} to FY {Year}", entry.AccountNumber, newEntry.FiscalYear);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error copying to next year: {ex.Message}";
                _logger.LogError(ex, "CopyToNextYearAsync failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task BulkAdjustAsync(decimal adjustmentPercent)
        {
            if (adjustmentPercent == 0) return;

            IsLoading = true;
            try
            {
                var entries = FilteredBudgetEntries.Any() ? FilteredBudgetEntries : BudgetEntries;
                var adjustmentFactor = 1 + (adjustmentPercent / 100);

                foreach (var entry in entries)
                {
                    entry.BudgetedAmount *= adjustmentFactor;
                    entry.Variance = entry.BudgetedAmount - entry.ActualAmount;
                    await _budgetRepository.UpdateAsync(entry);
                }

                _logger.LogInformation("Applied {Percent}% adjustment to {Count} entries", adjustmentPercent, entries.Count);
                await RefreshAnalysisAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error applying bulk adjustment: {ex.Message}";
                _logger.LogError(ex, "BulkAdjustAsync failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Disposes of resources used by the ViewModel.
        /// </summary>
        public void Dispose()
        {
            // Clean up any resources if needed
            _logger.LogDebug("BudgetViewModel disposed");
            GC.SuppressFinalize(this);
        }
    }
}
