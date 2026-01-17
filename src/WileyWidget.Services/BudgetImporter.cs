using System.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Abstractions;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Services;

/// <summary>
/// Service for importing budget data from various file formats
/// </summary>
public class BudgetImporter : IBudgetImporter
{
    private readonly IExcelReaderService _excelReaderService;
    private readonly ILogger<BudgetImporter> _logger;
    private readonly IBudgetRepository _budgetRepository;

    public BudgetImporter(
        IExcelReaderService excelReaderService,
        ILogger<BudgetImporter> logger,
        IBudgetRepository budgetRepository)
    {
        _excelReaderService = excelReaderService ?? throw new ArgumentNullException(nameof(excelReaderService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));
    }

    /// <inheritdoc/>
    public IEnumerable<string> SupportedExtensions => new[] { ".xlsx", ".xls", ".csv" };

    /// <inheritdoc/>
    public string Description => "Excel and CSV budget data importer supporting hierarchical account structures";

    /// <inheritdoc/>
    public async Task<IEnumerable<BudgetEntry>> ImportBudgetAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Budget file not found", filePath);

        _logger.LogInformation("Starting budget import from file: {FilePath}", filePath);

        try
        {
            // Validate the file first
            var validationErrors = await ValidateImportFileAsync(filePath);
            if (validationErrors.Any())
            {
                throw new InvalidOperationException($"File validation failed: {string.Join(", ", validationErrors)}");
            }

            // Read budget data from the file
            var budgetEntries = await _excelReaderService.ReadBudgetDataAsync(filePath);

            // Validate and enrich the budget entries
            var validatedEntries = await ValidateAndEnrichBudgetEntriesAsync(budgetEntries, filePath);

            _logger.LogInformation("Successfully imported {Count} budget entries from {FilePath}",
                validatedEntries.Count(), filePath);

            return validatedEntries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import budget from file: {FilePath}", filePath);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> ValidateImportFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            errors.Add("File path cannot be null or empty");
            return errors;
        }

        if (!File.Exists(filePath))
        {
            errors.Add("File does not exist");
            return errors;
        }

        // Check file extension
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
        {
            errors.Add($"Unsupported file extension: {extension}. Supported: {string.Join(", ", SupportedExtensions)}");
            return errors;
        }

        // For Excel files, validate structure
        if (extension == ".xlsx" || extension == ".xls")
        {
            try
            {
                var isValid = await _excelReaderService.ValidateExcelStructureAsync(filePath);
                if (!isValid)
                {
                    errors.Add("Excel file does not have the expected budget structure");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Excel validation failed: {ex.Message}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates and enriches budget entries with additional data
    /// </summary>
    private Task<IEnumerable<BudgetEntry>> ValidateAndEnrichBudgetEntriesAsync(IEnumerable<BudgetEntry> entries, string sourceFilePath, CancellationToken cancellationToken = default)
    {
        var validatedEntries = new List<BudgetEntry>();

        foreach (var entry in entries)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(entry.AccountNumber))
            {
                _logger.LogWarning("Skipping budget entry with missing account number");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Description))
            {
                _logger.LogWarning("Skipping budget entry with missing description for account {AccountNumber}", entry.AccountNumber);
                continue;
            }

            if (entry.FiscalYear <= 0)
            {
                _logger.LogWarning("Setting default fiscal year for account {AccountNumber}", entry.AccountNumber);
                entry.FiscalYear = DateTime.Now.Year;
            }

            // Set default values for dates if not provided
            if (entry.StartPeriod == default)
            {
                entry.StartPeriod = new DateTime(entry.FiscalYear, 1, 1);
            }

            if (entry.EndPeriod == default)
            {
                entry.EndPeriod = new DateTime(entry.FiscalYear, 12, 31);
            }

            // Ensure GASB compliance
            entry.IsGASBCompliant = true;

            // Set audit timestamps
            entry.CreatedAt = DateTime.UtcNow;

            // Store actual source file path for tracking
            entry.SourceFilePath = sourceFilePath;

            validatedEntries.Add(entry);
        }

        _logger.LogInformation("Validated and enriched {Count} budget entries", validatedEntries.Count);
        return Task.FromResult<IEnumerable<BudgetEntry>>(validatedEntries);
    }

    /// <inheritdoc/>
    public async void Import(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be null or empty", nameof(sourcePath));

        _logger.LogInformation("Importing budget data from: {SourcePath}", sourcePath);

        try
        {
            var entries = await ImportBudgetAsync(sourcePath);

            // Save the imported entries to the repository
            foreach (var entry in entries)
            {
                await _budgetRepository.AddAsync(entry);
            }

            _logger.LogInformation("Successfully imported {Count} budget entries from {SourcePath}", entries.Count(), sourcePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import budget data from {SourcePath}", sourcePath);
            throw;
        }
    }
}
