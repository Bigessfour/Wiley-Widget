using System.Threading;
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
using WileyWidget.Models.Entities;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// ViewModel for budget management with full CRUD operations.
    /// Provides filtering, analysis, and export capabilities for budget entries.
    /// Supports multi-year budgets, hierarchical entries, GASB compliance, and advanced analytics.
    /// </summary>
    public partial class BudgetViewModel : ObservableObject, IBudgetViewModel, IDisposable
    {
        private readonly ILogger<BudgetViewModel> _logger;
        private readonly IBudgetRepository _budgetRepository;
        private readonly IReportExportService _reportExportService;
        private readonly IEnterpriseRepository _enterpriseRepository;
        private readonly WileyWidget.Services.Abstractions.IAppEventBus? _eventBus;
        private readonly Action<WileyWidget.Services.Abstractions.BudgetActualsUpdatedEvent>? _budgetUpdatedHandler;
        private readonly SynchronizationContext? _uiSynchronizationContext;

        /// <summary>Gets or sets the collection of all budget entries.</summary>
        [ObservableProperty]
        private ObservableCollection<BudgetEntry> budgetEntries = new();

        /// <summary>Gets or sets the filtered collection of budget entries based on current filters.</summary>
        [ObservableProperty]
        private ObservableCollection<BudgetEntry> filteredBudgetEntries = new();

        /// <summary>Gets or sets the selected budget period.</summary>
        [ObservableProperty]
        private BudgetPeriod? selectedPeriod;

        /// <summary>Gets or sets the selected fiscal year for filtering.</summary>
        [ObservableProperty]
        private int selectedFiscalYear = DateTime.Now.Year;

        /// <summary>Gets or sets the current error message.</summary>
        [ObservableProperty]
        private string errorMessage = string.Empty;

        /// <summary>Gets or sets a value indicating whether data is currently loading.</summary>
        [ObservableProperty]
        private bool isLoading;

        /// <summary>Gets or sets the current status text for the UI.</summary>
        [ObservableProperty]
        private string statusText = "Ready";

        // Advanced filtering properties
        /// <summary>Gets or sets the search text for filtering entries.</summary>
        [ObservableProperty]
        private string searchText = string.Empty;

        /// <summary>Gets or sets the selected department ID filter.</summary>
        [ObservableProperty]
        private int? selectedDepartmentId;

        /// <summary>Gets or sets the selected fund type filter.</summary>
        [ObservableProperty]
        private FundType? selectedFundType;

        /// <summary>Gets or sets the selected entity/fund name for scoping (e.g., "Wiley Sanitation District").</summary>
        [ObservableProperty]
        private string? selectedEntity;

        /// <summary>Gets or sets the available entities/fund names to choose from.</summary>
        [ObservableProperty]
        private ObservableCollection<string> availableEntities = new();

        /// <summary>Gets or sets the minimum variance threshold filter.</summary>
        [ObservableProperty]
        private decimal? varianceThreshold;

        /// <summary>Gets or sets a value indicating whether to show only over-budget entries.</summary>
        [ObservableProperty]
        private bool showOnlyOverBudget;

        /// <summary>Gets or sets a value indicating whether to show only under-budget entries.</summary>
        [ObservableProperty]
        private bool showOnlyUnderBudget;

        // Analysis properties

        /// <summary>Gets or sets the total budgeted amount across all entries.</summary>
        [ObservableProperty]
        private decimal totalBudgeted;

        /// <summary>Gets or sets the total actual spent amount across all entries.</summary>
        [ObservableProperty]
        private decimal totalActual;

        /// <summary>Gets or sets the total variance amount (Budgeted - Actual).</summary>
        [ObservableProperty]
        private decimal totalVariance;

        /// <summary>Gets or sets the total encumbrance amount across all entries.</summary>
        [ObservableProperty]
        private decimal totalEncumbrance;

        /// <summary>Gets or sets the percentage of budget used.</summary>
        [ObservableProperty]
        private decimal percentUsed;

        /// <summary>Gets or sets the count of entries that are over budget.</summary>
        [ObservableProperty]
        private int entriesOverBudget;

        /// <summary>Gets or sets the count of entries that are under budget.</summary>
        [ObservableProperty]
        private int entriesUnderBudget;

        /// <summary>Gets or sets total budgeted revenues (positive = good).</summary>
        [ObservableProperty]
        private decimal totalRevenuesBudgeted;

        /// <summary>Gets or sets actual revenues collected.</summary>
        [ObservableProperty]
        private decimal totalRevenuesActual;

        /// <summary>Gets or sets total budgeted expenditures.</summary>
        [ObservableProperty]
        private decimal totalExpendituresBudgeted;

        /// <summary>Gets or sets actual expenditures spent.</summary>
        [ObservableProperty]
        private decimal totalExpendituresActual;

        /// <summary>Gets or sets net budget position (Revenues - Expenditures). Positive = surplus.</summary>
        [ObservableProperty]
        private decimal netBudgetPosition;

        // Grouping properties

        /// <summary>Gets or sets the field name to group entries by.</summary>
        [ObservableProperty]
        private string groupBy = "None";

        /// <summary>Gets or sets a value indicating whether to display entries in hierarchical view.</summary>
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

        /// <summary>
        /// Initializes a new instance of the <see cref="BudgetViewModel"/> class with full dependency injection.
        /// </summary>
        /// <param name="logger">Logger for diagnostic and error logging.</param>
        /// <param name="budgetRepository">Repository for budget entry CRUD operations.</param>
        /// <param name="reportExportService">Service for exporting reports to various formats.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required service is null.</exception>
        /// <summary>
        /// Initializes a new instance of the <see cref="BudgetViewModel"/> class with full dependency injection.
        /// </summary>
        /// <param name="logger">Logger for diagnostic and error logging.</param>
        /// <param name="budgetRepository">Repository for budget entry CRUD operations.</param>
        /// <param name="reportExportService">Service for exporting reports to various formats.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required service is null.</exception>
        public BudgetViewModel(ILogger<BudgetViewModel>? logger, IBudgetRepository? budgetRepository, IReportExportService? reportExportService, IEnterpriseRepository? enterpriseRepository, WileyWidget.Services.Abstractions.IAppEventBus? eventBus = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
            _reportExportService = reportExportService ?? throw new ArgumentNullException(nameof(reportExportService));
            _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
            _uiSynchronizationContext = SynchronizationContext.Current;

            // Initialize commands
            LoadBudgetsCommand = new AsyncRelayCommand(LoadBudgetsAsync);
            LoadByYearCommand = new AsyncRelayCommand(LoadBudgetsAsync);
            ImportFromCsvCommand = new AsyncRelayCommand<string>(ImportFromCsvAsync);
            ExportToCsvCommand = new AsyncRelayCommand<string>(ExportToCsvAsync);
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
                    e.PropertyName == nameof(VarianceThreshold) ||
                    e.PropertyName == nameof(SelectedEntity))
                {
                    _ = ApplyFiltersAsync();
                }
            };

            _logger.LogDebug("BudgetViewModel initialized");

            // Wire application-level event subscription (optional)
            _eventBus = eventBus;
            if (_eventBus != null)
            {
                _budgetUpdatedHandler = ev =>
                {
                    RunOnUiThread(() =>
                    {
                        _logger.LogInformation("BudgetViewModel: Budget actuals updated event received: FY {FiscalYear} Updated {UpdatedCount}", ev.FiscalYear, ev.UpdatedCount);
                        if (ev.FiscalYear == SelectedFiscalYear)
                        {
                            _ = LoadBudgetsCommand.ExecuteAsync(null);
                            _ = RefreshAnalysisCommand.ExecuteAsync(null);
                            StatusText = $"Budget actuals updated: {ev.UpdatedCount} rows";
                        }
                    });
                };

                try { _eventBus.Subscribe(_budgetUpdatedHandler); } catch { /* best-effort subscribe */ }
            }
        }

        private void RunOnUiThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            var uiContext = _uiSynchronizationContext;
            if (uiContext == null || SynchronizationContext.Current == uiContext)
            {
                action();
                return;
            }

            try
            {
                uiContext.Post(static state =>
                {
                    if (state is Action callback)
                    {
                        callback();
                    }
                }, action);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to marshal action to UI thread; running action on current thread");
                action();
            }
        }

        /// <summary>
        /// Loads all budget entries for the selected fiscal year asynchronously.
        /// </summary>
        /// <returns>A task representing the async load operation.</returns>
        /// <summary>
        /// Loads all budget entries for the selected fiscal year asynchronously.
        /// </summary>
        /// <returns>A task representing the async load operation.</returns>
        private async Task LoadBudgetsAsync(CancellationToken cancellationToken = default)
        {
            IsLoading = true;
            StatusText = "Loading budget entries...";
            try
            {
                var year = SelectedFiscalYear > 0 ? SelectedFiscalYear : (SelectedPeriod?.Year ?? DateTime.Now.Year);
                _logger.LogInformation("Loading budget entries for fiscal year {Year}", year);

                var entries = (await _budgetRepository.GetByFiscalYearAsync(year, cancellationToken)).ToList();
                BudgetEntries = new ObservableCollection<BudgetEntry>(entries);
                FilteredBudgetEntries = new ObservableCollection<BudgetEntry>(entries);

                // Populate available entities/fund names and enterprise names for the entity selector
                try
                {
                    var entitySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var n in entries.Select(be => be.Fund?.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim()))
                        entitySet.Add(n);

                    var enterprises = await _enterpriseRepository.GetAllAsync();
                    foreach (var en in enterprises.Select(e => e.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim()))
                        entitySet.Add(en);

                    var entityList = entitySet.OrderBy(n => n).ToList();
                    entityList.Insert(0, "All Entities");
                    AvailableEntities = new ObservableCollection<string>(entityList);
                }
                catch (Exception exEnt)
                {
                    _logger.LogWarning(exEnt, "Failed to populate AvailableEntities. Falling back to fund names.");
                    var entityList = entries.Select(be => be.Fund?.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
                    if (!entityList.Contains("All Entities"))
                        entityList.Insert(0, "All Entities");
                    AvailableEntities = new ObservableCollection<string>(entityList);
                }

                await RefreshAnalysisAsync();

                StatusText = $"Loaded all {BudgetEntries.Count} budget entries for fiscal year {year}";
                _logger.LogInformation("Loaded {Count} budget entries for year {Year}", BudgetEntries.Count, year);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading budgets: {ex.Message}";
                StatusText = "Error loading budgets";
                _logger.LogError(ex, "Budget load failed");

                ResetBudgetDataOnFailure();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ResetBudgetDataOnFailure()
        {
            try
            {
                _logger.LogWarning("ResetBudgetDataOnFailure called: loading empty budget state.");

                BudgetEntries = new ObservableCollection<BudgetEntry>();
                FilteredBudgetEntries = new ObservableCollection<BudgetEntry>();

                TotalBudgeted = 0m;
                TotalActual = 0m;
                TotalVariance = 0m;
                PercentUsed = 0;
                EntriesOverBudget = 0;
                EntriesUnderBudget = 0;

                StatusText = "No budget entries available yet.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while resetting budget data state");
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
                // Check if it already exists (defensive)
                var exists = await _budgetRepository.ExistsAsync(entry.AccountNumber, entry.FiscalYear, cancellationToken);
                if (exists)
                {
                    // If dialog already asked, this should not happen — but safety net
                    var dupMsg = $"Account {entry.AccountNumber} already exists for FY {entry.FiscalYear}. Use Edit instead.";
                    _logger.LogWarning("AddEntryAsync duplicate prevented: Account={Account} FY={Year}", entry.AccountNumber, entry.FiscalYear);
                    ErrorMessage = dupMsg;
                    StatusText = "Duplicate prevented";
                    return;
                }

                await _budgetRepository.AddAsync(entry, cancellationToken);

                RunOnUiThread(() =>
                {
                    BudgetEntries.Insert(0, entry);
                    FilteredBudgetEntries.Insert(0, entry);
                    NotifyCollectionRefresh();
                });

                await ApplyFiltersAsync(cancellationToken);
                _logger.LogInformation("Added new budget entry {Account} FY {Year}", entry.AccountNumber, entry.FiscalYear);
                StatusText = "Budget entry added successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddEntryAsync failed");
                ErrorMessage = $"Error adding entry: {ex.Message}";
                StatusText = "Add failed";
            }
        }

        // Optional helper — add to IBudgetRepository too if you want clean architecture
        public async Task<bool> ExistsAsync(string accountNumber, int fiscalYear, CancellationToken ct = default)
        {
            return await _budgetRepository.ExistsAsync(accountNumber, fiscalYear, ct);
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
                await _budgetRepository.UpdateAsync(entry, cancellationToken);

                RunOnUiThread(() =>
                {
                    ReplaceEntry(BudgetEntries, entry);
                    ReplaceEntry(FilteredBudgetEntries, entry);
                    NotifyCollectionRefresh();
                });

                await ApplyFiltersAsync(cancellationToken);
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
                await _budgetRepository.DeleteAsync(id, cancellationToken);

                RunOnUiThread(() =>
                {
                    RemoveEntry(BudgetEntries, id);
                    RemoveEntry(FilteredBudgetEntries, id);
                    NotifyCollectionRefresh();
                });

                await ApplyFiltersAsync(cancellationToken);
                _logger.LogInformation("Deleted budget entry {Id}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteEntryAsync failed for {Id}", id);
                ErrorMessage = ex.Message;
            }
        }

        private async Task ImportFromCsvAsync(string? filePath, CancellationToken cancellationToken = default)
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

                        await _budgetRepository.AddAsync(entry, cancellationToken);

                        RunOnUiThread(() =>
                        {
                            BudgetEntries.Add(entry);
                            FilteredBudgetEntries.Add(entry);
                        });

                        imported.Add(entry);
                    }
                    catch (Exception exRow)
                    {
                        errors.Add($"Row {rowNumber}: {exRow.Message}");
                    }
                }

                if (imported.Count > 0)
                {
                    await ApplyFiltersAsync(cancellationToken);
                    NotifyCollectionRefresh();
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

        /// <summary>
        /// Import CSV using a user-supplied column mapping. Mapping keys should include: "AccountNumber","Description","BudgetedAmount","ActualAmount".
        /// Supports bulk entity assignment and fiscal year override. Reports progress via IProgress&lt;string&gt;.
        /// </summary>
        public async Task ImportFromCsvWithMappingAsync(string? filePath, Dictionary<string, string> columnMap, string? bulkEntity, int fiscalYearOverride, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
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
                    TrimOptions = TrimOptions.Trim,
                    BadDataFound = null
                };

                using var csv = new CsvReader(reader, config);

                // ensure header read
                try { csv.Read(); csv.ReadHeader(); } catch { /* ignore */ }

                int rowNumber = 0;
                while (await csv.ReadAsync())
                {
                    rowNumber++;
                    try
                    {
                        string GetField(string key)
                        {
                            if (!columnMap.TryGetValue(key, out var map) || string.IsNullOrWhiteSpace(map)) return string.Empty;
                            // support ColumnN tokens
                            if (map.StartsWith("Column", StringComparison.OrdinalIgnoreCase) && int.TryParse(map.Substring(6), out var idx))
                            {
                                try { return csv.GetField(idx) ?? string.Empty; } catch { return string.Empty; }
                            }

                            try { return csv.GetField(map) ?? string.Empty; } catch { return string.Empty; }
                        }

                        string accountRaw = GetField("AccountNumber");
                        string descRaw = GetField("Description");
                        string budgetRaw = GetField("BudgetedAmount");
                        string actualRaw = GetField("ActualAmount");

                        decimal ParseDecimalSafe(string s)
                        {
                            if (string.IsNullOrWhiteSpace(s)) return 0m;
                            s = s.Trim();
                            // Try current culture then invariant
                            if (decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out var v)) return v;
                            if (decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out v)) return v;
                            // Strip common characters and retry
                            var cleaned = s.Replace("$", string.Empty).Replace(",", string.Empty).Replace("(", "-").Replace(")", string.Empty);
                            if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v)) return v;
                            return 0m;
                        }

                        if (string.IsNullOrWhiteSpace(accountRaw))
                        {
                            errors.Add($"Row {rowNumber}: Account number is required.");
                            continue;
                        }

                        var entry = new BudgetEntry
                        {
                            AccountNumber = accountRaw.Trim(),
                            Description = descRaw.Trim(),
                            BudgetedAmount = ParseDecimalSafe(budgetRaw),
                            ActualAmount = ParseDecimalSafe(actualRaw),
                            FiscalYear = fiscalYearOverride > 0 ? fiscalYearOverride : (SelectedPeriod?.Year ?? DateTime.Now.Year),
                            SourceFilePath = filePath,
                            SourceRowNumber = rowNumber,
                            CreatedAt = DateTime.UtcNow,
                        };

                        // Apply bulk entity as Fund name if provided
                        if (!string.IsNullOrWhiteSpace(bulkEntity))
                        {
                            entry.Fund = new Fund { Name = bulkEntity! };
                        }

                        // Calculate variance
                        entry.Variance = entry.BudgetedAmount - entry.ActualAmount;

                        // Validate
                        var validationResults = new List<ValidationResult>();
                        var context = new ValidationContext(entry);
                        if (!Validator.TryValidateObject(entry, context, validationResults, true))
                        {
                            var msg = string.Join(';', validationResults.Select(v => v.ErrorMessage));
                            errors.Add($"Row {rowNumber}: {msg}");
                            continue;
                        }

                        await _budgetRepository.AddAsync(entry, cancellationToken);

                        RunOnUiThread(() =>
                        {
                            BudgetEntries.Add(entry);
                            FilteredBudgetEntries.Add(entry);
                        });

                        imported.Add(entry);

                        progress?.Report($"Imported {imported.Count} rows...");
                    }
                    catch (Exception exRow)
                    {
                        errors.Add($"Row {rowNumber}: {exRow.Message}");
                    }
                }

                if (imported.Count > 0)
                {
                    await ApplyFiltersAsync(cancellationToken);
                    NotifyCollectionRefresh();
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
                _logger.LogError(ex, "ImportFromCsvWithMappingAsync failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportToCsvAsync(string? filePath, CancellationToken cancellationToken = default)
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

        private async Task ExportToPdfAsync(string? filePath, CancellationToken cancellationToken = default)
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

        private async Task ExportToExcelAsync(string? filePath, CancellationToken cancellationToken = default)
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

        /// <summary>
        /// Applies current filters to the budget entries collection.
        /// Updates the FilteredBudgetEntries collection and refreshes analysis.
        /// </summary>
        /// <returns>A task representing the async filter operation.</returns>
        private async Task ApplyFiltersAsync(CancellationToken cancellationToken = default)
        {
            var filteredList = await Task.Run(() =>
            {
                var filtered = BudgetEntries.AsEnumerable();

                // Search text filter - searches in account number and description
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

                // Entity filter (by Fund.Name or via heuristics for Sanitation/Utility)
                if (!string.IsNullOrWhiteSpace(SelectedEntity) && !string.Equals(SelectedEntity, "All Entities", StringComparison.OrdinalIgnoreCase))
                {
                    var sel = SelectedEntity.Trim();
                    filtered = filtered.Where(e =>
                        (e.Fund != null && !string.IsNullOrWhiteSpace(e.Fund.Name) && string.Equals(e.Fund.Name, sel, StringComparison.OrdinalIgnoreCase))
                        || (sel.IndexOf("Sanitation", StringComparison.OrdinalIgnoreCase) >= 0 && ((e.Fund?.Name?.IndexOf("Sewer", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (e.Fund?.Name?.IndexOf("Sanitation", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0))
                        || (sel.IndexOf("Utility", StringComparison.OrdinalIgnoreCase) >= 0 && ((e.Fund?.Name?.IndexOf("Water", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (e.Fund?.Name?.IndexOf("Trash", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0))
                        || (e.MunicipalAccount != null && e.MunicipalAccount.Name != null && e.MunicipalAccount.Name.IndexOf(sel, StringComparison.OrdinalIgnoreCase) >= 0)
                    );
                }

                // Variance filters - mutually exclusive
                if (ShowOnlyOverBudget)
                {
                    filtered = filtered.Where(e => e.ActualAmount > e.BudgetedAmount);
                }
                else if (ShowOnlyUnderBudget)
                {
                    filtered = filtered.Where(e => e.ActualAmount < e.BudgetedAmount);
                }

                // Variance threshold filter
                if (VarianceThreshold.HasValue)
                {
                    filtered = filtered.Where(e => Math.Abs(e.BudgetedAmount - e.ActualAmount) >= VarianceThreshold.Value);
                }

                return filtered.ToList();
            }, cancellationToken).ConfigureAwait(false);

            RunOnUiThread(() =>
            {
                FilteredBudgetEntries = new ObservableCollection<BudgetEntry>(filteredList);
                StatusText = $"{FilteredBudgetEntries.Count} of {BudgetEntries.Count} entries match filters";
            });
            _logger.LogInformation("Applied filters: {Count} entries match criteria", FilteredBudgetEntries.Count);

            await RefreshAnalysisAsync();
        }

        private void ReplaceEntry(ObservableCollection<BudgetEntry> targetCollection, BudgetEntry replacement)
        {
            var existing = targetCollection.FirstOrDefault(entry => entry.Id == replacement.Id);
            if (existing == null)
            {
                return;
            }

            var existingIndex = targetCollection.IndexOf(existing);
            if (existingIndex >= 0)
            {
                targetCollection[existingIndex] = replacement;
            }
        }

        private void RemoveEntry(ObservableCollection<BudgetEntry> targetCollection, int entryId)
        {
            var existing = targetCollection.FirstOrDefault(entry => entry.Id == entryId);
            if (existing != null)
            {
                targetCollection.Remove(existing);
            }
        }

        private void NotifyCollectionRefresh()
        {
            OnPropertyChanged(nameof(BudgetEntries));
            OnPropertyChanged(nameof(FilteredBudgetEntries));
        }

        /// <summary>
        /// Clears all active filters and resets the filtered collection.
        /// </summary>
        /// <returns>A task representing the async clear operation.</returns>
        private Task ClearFiltersAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Clearing all filters");

            SearchText = string.Empty;
            SelectedDepartmentId = null;
            SelectedFundType = null;
            SelectedEntity = null;
            VarianceThreshold = null;
            ShowOnlyOverBudget = false;
            ShowOnlyUnderBudget = false;
            FilteredBudgetEntries = new ObservableCollection<BudgetEntry>(BudgetEntries);

            StatusText = $"Filters cleared - showing all {BudgetEntries.Count} entries";
            _logger.LogInformation("Filters cleared");

            return Task.CompletedTask;
        }

        // ============= Analysis Methods =============

        /// <summary>
        /// Refreshes all analysis totals and counts based on current filtered entries.
        /// </summary>
        /// <returns>A task representing the async refresh operation.</returns>
        /// <summary>
        /// Refreshes all analysis totals and counts based on current filtered entries.
        /// </summary>
        /// <returns>A task representing the async refresh operation.</returns>
        private async Task RefreshAnalysisAsync(CancellationToken cancellationToken = default)
        {
            var entries = FilteredBudgetEntries.Any() ? FilteredBudgetEntries : BudgetEntries;

            var totals = await Task.Run(() =>
            {
                var list = entries.ToList();

                var revBudget = list.Where(e => e.MunicipalAccount?.Type == AccountType.Revenue ||
                                               string.Equals(e.AccountTypeName, "Revenue", StringComparison.OrdinalIgnoreCase))
                                    .Sum(e => e.BudgetedAmount);

                var revActual = list.Where(e => e.MunicipalAccount?.Type == AccountType.Revenue ||
                                               string.Equals(e.AccountTypeName, "Revenue", StringComparison.OrdinalIgnoreCase))
                                    .Sum(e => e.ActualAmount);

                var expBudget = list.Where(e => e.MunicipalAccount?.Type == AccountType.Expense ||
                                               string.Equals(e.AccountTypeName, "Expenditure", StringComparison.OrdinalIgnoreCase))
                                    .Sum(e => e.BudgetedAmount);

                var expActual = list.Where(e => e.MunicipalAccount?.Type == AccountType.Expense ||
                                               string.Equals(e.AccountTypeName, "Expenditure", StringComparison.OrdinalIgnoreCase))
                                    .Sum(e => e.ActualAmount);

                var netPosition = revActual - expActual;  // or (revBudget - expBudget) depending on your preference

                var totalBudgeted = revBudget + expBudget;
                var totalActual = revActual + expActual;
                var totalVariance = totalBudgeted - totalActual;
                var totalEncumbrance = list.Sum(e => e.EncumbranceAmount);
                var percentUsed = totalBudgeted > 0 ? (totalActual / totalBudgeted) * 100 : 0;
                var entriesOver = list.Count(e => e.ActualAmount > e.BudgetedAmount); // still useful for expenditures
                var entriesUnder = list.Count(e => e.ActualAmount <= e.BudgetedAmount);

                return (totalBudgeted, totalActual, totalVariance, totalEncumbrance, percentUsed, entriesOver, entriesUnder,
                        revBudget, revActual, expBudget, expActual, netPosition);
            }, cancellationToken).ConfigureAwait(false);

            RunOnUiThread(() =>
            {
                TotalBudgeted = totals.totalBudgeted;
                TotalActual = totals.totalActual;
                TotalVariance = totals.totalVariance;
                TotalEncumbrance = totals.totalEncumbrance;
                PercentUsed = totals.percentUsed;
                EntriesOverBudget = totals.entriesOver;
                EntriesUnderBudget = totals.entriesUnder;

                TotalRevenuesBudgeted = totals.revBudget;
                TotalRevenuesActual = totals.revActual;
                TotalExpendituresBudgeted = totals.expBudget;
                TotalExpendituresActual = totals.expActual;
                NetBudgetPosition = totals.netPosition;
            });

            _logger.LogInformation("Budget analysis refreshed — Revenues {RevB:C} / {RevA:C} | Expenditures {ExpB:C} / {ExpA:C} | Net {Net:C}",
                totals.revBudget, totals.revActual, totals.expBudget, totals.expActual, totals.netPosition);
        }

        /// <summary>
        /// Calculates and updates variance for all budget entries.
        /// </summary>
        /// <returns>A task representing the async calculation operation.</returns>
        /// <summary>
        /// Calculates and updates variance for all budget entries.
        /// </summary>
        /// <returns>A task representing the async calculation operation.</returns>
        private async Task CalculateVariancesAsync(CancellationToken cancellationToken = default)
        {
            IsLoading = true;
            StatusText = "Calculating variances...";
            try
            {
                var count = 0;
                foreach (var entry in BudgetEntries)
                {
                    entry.Variance = entry.BudgetedAmount - entry.ActualAmount;
                    await _budgetRepository.UpdateAsync(entry);
                    count++;
                }

                StatusText = $"Calculated variances for {count} entries";
                _logger.LogInformation("Calculated variances for {Count} entries", BudgetEntries.Count);

                await RefreshAnalysisAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error calculating variances: {ex.Message}";
                StatusText = "Error calculating variances";
                _logger.LogError(ex, "CalculateVariancesAsync failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ============= Bulk Operations =============

        /// <summary>
        /// Copies a budget entry to the next fiscal year with zero actuals.
        /// </summary>
        /// <param name="entry">The entry to copy.</param>
        /// <returns>A task representing the async copy operation.</returns>
        /// <summary>
        /// Copies a budget entry to the next fiscal year with zero actuals.
        /// </summary>
        /// <param name="entry">The entry to copy.</param>
        /// <returns>A task representing the async copy operation.</returns>
        private async Task CopyToNextYearAsync(BudgetEntry? entry, CancellationToken cancellationToken = default)
        {
            if (entry == null) return;

            IsLoading = true;
            StatusText = $"Copying entry {entry.AccountNumber} to next year...";
            try
            {
                _logger.LogInformation("Copying budget entry {AccountNumber} to FY {Year}", entry.AccountNumber, entry.FiscalYear + 1);

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

                if (newEntry.FiscalYear == SelectedFiscalYear)
                {
                    RunOnUiThread(() =>
                    {
                        BudgetEntries.Insert(0, newEntry);
                        FilteredBudgetEntries.Insert(0, newEntry);
                    });
                }

                NotifyCollectionRefresh();
                await ApplyFiltersAsync(cancellationToken);

                StatusText = $"Copied entry {entry.AccountNumber} to FY {newEntry.FiscalYear}";
                _logger.LogInformation("Copied budget entry {AccountNumber} to FY {Year}", entry.AccountNumber, newEntry.FiscalYear);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error copying to next year: {ex.Message}";
                StatusText = "Error copying entry";
                _logger.LogError(ex, "CopyToNextYearAsync failed");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Applies a percentage adjustment to all filtered budget entries.
        /// </summary>
        /// <param name="adjustmentPercent">The percentage adjustment (e.g., 5.0 for +5%, -3.0 for -3%).</param>
        /// <returns>A task representing the async adjustment operation.</returns>
        /// <summary>
        /// Applies a percentage adjustment to all filtered budget entries.
        /// </summary>
        /// <param name="adjustmentPercent">The percentage adjustment (e.g., 5.0 for +5%, -3.0 for -3%).</param>
        /// <returns>A task representing the async adjustment operation.</returns>
        private async Task BulkAdjustAsync(decimal adjustmentPercent, CancellationToken cancellationToken = default)
        {
            if (adjustmentPercent == 0) return;

            IsLoading = true;
            StatusText = $"Applying {adjustmentPercent:+0.##;-0.##}% adjustment...";
            try
            {
                var entries = FilteredBudgetEntries.Any() ? FilteredBudgetEntries : BudgetEntries;
                var adjustmentFactor = 1 + (adjustmentPercent / 100);
                var count = 0;

                _logger.LogInformation("Applying {Percent}% adjustment to {Count} entries", adjustmentPercent, entries.Count);

                foreach (var entry in entries)
                {
                    entry.BudgetedAmount *= adjustmentFactor;
                    entry.Variance = entry.BudgetedAmount - entry.ActualAmount;
                    await _budgetRepository.UpdateAsync(entry);
                    count++;
                }

                StatusText = $"Applied {adjustmentPercent:+0.##;-0.##}% adjustment to {count} entries";
                _logger.LogInformation("Applied {Percent}% adjustment to {Count} entries", adjustmentPercent, entries.Count);

                NotifyCollectionRefresh();
                await ApplyFiltersAsync(cancellationToken);
                await RefreshAnalysisAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error applying bulk adjustment: {ex.Message}";
                StatusText = "Error applying adjustment";
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of resources used by the ViewModel.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Clean up managed resources if needed
                if (_eventBus != null && _budgetUpdatedHandler != null)
                {
                    try { _eventBus.Unsubscribe(_budgetUpdatedHandler); } catch { }
                }
            }
            // Clean up unmanaged resources if any
            _logger.LogDebug("BudgetViewModel disposed");
        }
    }
}
