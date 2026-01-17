using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Excel;

namespace WileyWidget.WinForms.Services
{
    public class DataImportService
    {
        private readonly IExcelReaderService _excelReader;
        private readonly IBudgetRepository _budgetRepository;
        private readonly ILogger<DataImportService> _logger;

        public DataImportService(
            IExcelReaderService excelReader,
            IBudgetRepository budgetRepository,
            ILogger<DataImportService> logger)
        {
            _excelReader = excelReader ?? throw new ArgumentNullException(nameof(excelReader));
            _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ImportBudgetDataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path required", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Import file not found", filePath);

            _logger.LogInformation("Starting budget import from: {FilePath}", filePath);

            try
            {
                // 1. Read Data (using the multi-sheet capable reader)
                var entries = await _excelReader.ReadBudgetDataAsync(filePath);
                var entryList = entries.ToList();

                if (!entryList.Any())
                {
                    _logger.LogWarning("No budget entries found in file.");
                    return;
                }

                _logger.LogInformation("Found {Count} entries. Beginning database import...", entryList.Count);

                // 2. Group by Fiscal Year to optimize fetching existing data
                var entriesByYear = entryList.GroupBy(e => e.FiscalYear);

                int addedCount = 0;
                int updatedCount = 0;
                int skippedCount = 0;

                foreach (var yearGroup in entriesByYear)
                {
                    int fiscalYear = yearGroup.Key;
                    _logger.LogInformation("Processing FY {Year} ({Count} entries)...", fiscalYear, yearGroup.Count());

                    // Fetch existing for this year to prevent duplicates/contamination
                    var existingEntries = await _budgetRepository.GetByFiscalYearAsync(fiscalYear, cancellationToken);

                    // Create lookup: AccountNumber -> Entry
                    // Note: AccountNumber must be unique per Fiscal Year per the DB index
                    // (entity.HasIndex(e => new { e.AccountNumber, e.FiscalYear }).IsUnique())
                    var existingMap = existingEntries
                        .Where(e => !string.IsNullOrEmpty(e.AccountNumber))
                        .ToDictionary(e => e.AccountNumber!, StringComparer.OrdinalIgnoreCase);

                    foreach (var incoming in yearGroup)
                    {
                        if (string.IsNullOrEmpty(incoming.AccountNumber))
                        {
                            skippedCount++;
                            continue;
                        }

                        if (existingMap.TryGetValue(incoming.AccountNumber, out var existing))
                        {
                            // Update existing
                            bool changed = false;

                            // Only update if substantially different? Or always update?
                            // Let's update Actuals and basic metadata, maybe preserve Budgeted if locked?
                            // For now, valid import implies overwrite of values from source.

                            if (existing.ActualAmount != incoming.ActualAmount)
                            {
                                existing.ActualAmount = incoming.ActualAmount;
                                changed = true;
                            }

                            if (existing.BudgetedAmount != incoming.BudgetedAmount && incoming.BudgetedAmount > 0)
                            {
                                // Only update budget if import has value, assuming Excel is source of truth
                                existing.BudgetedAmount = incoming.BudgetedAmount;
                                changed = true;
                            }

                            if (existing.Description != incoming.Description)
                            {
                                existing.Description = incoming.Description;
                                changed = true;
                            }

                            if (changed)
                            {
                                existing.UpdatedAt = DateTime.UtcNow;
                                existing.SourceFilePath = filePath; // Track source
                                await _budgetRepository.UpdateAsync(existing, cancellationToken);
                                updatedCount++;
                            }
                            else
                            {
                                skippedCount++;
                            }
                        }
                        else
                        {
                            // Insert new
                            incoming.CreatedAt = DateTime.UtcNow;
                            incoming.UpdatedAt = DateTime.UtcNow;
                            // Ensure required relationships if missing (defaulting to Dept 1, Fund 1 if not parsed)
                            /*
                               Note: BudgetRepository.AddAsync will handle basic saves, but Foreign Keys
                               Validation is enforced by DB. ExcelReaderService defaults DepartmentId to 1
                               if not found. Ensuring FundId is valid is prudent.
                            */
                            if (incoming.DepartmentId == 0) incoming.DepartmentId = 1;

                            // Infer default fund from filename if not specified in row
                            if (incoming.FundId == 0)
                            {
                                bool isSanitation = Path.GetFileName(filePath).IndexOf("Sanitation", StringComparison.OrdinalIgnoreCase) >= 0;
                                incoming.FundId = isSanitation ? 3 : 1; // 3=Utility, 1=General
                            }

                            await _budgetRepository.AddAsync(incoming, cancellationToken);
                            addedCount++;
                        }
                    }
                }

                _logger.LogInformation("Import Complete. Added: {Added}, Updated: {Updated}, Skipped/Unchanged: {Skipped}",
                    addedCount, updatedCount, skippedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error during budget import");
                throw;
            }
        }
    }
}
