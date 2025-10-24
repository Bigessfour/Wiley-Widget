#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Interface for budget import operations
/// </summary>
public interface IBudgetImporter
{
    /// <summary>
    /// Imports budget data from a file
    /// </summary>
    /// <param name="filePath">The path to the file to import</param>
    /// <returns>A collection of imported budget entries</returns>
    Task<IEnumerable<BudgetEntry>> ImportBudgetAsync(string filePath);

    /// <summary>
    /// Validates the import file format and content
    /// </summary>
    /// <param name="filePath">The path to the file to validate</param>
    /// <returns>A list of validation errors, empty if valid</returns>
    Task<List<string>> ValidateImportFileAsync(string filePath);

    /// <summary>
    /// Gets the supported file extensions for import
    /// </summary>
    IEnumerable<string> SupportedExtensions { get; }

    /// <summary>
    /// Gets a description of the importer
    /// </summary>
    string Description { get; }
}
