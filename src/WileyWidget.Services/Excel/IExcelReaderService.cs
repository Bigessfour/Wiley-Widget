using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Interface for reading Excel files and extracting municipal budget data
/// </summary>
public interface IExcelReaderService
{
    /// <summary>
    /// Reads municipal budget data from an Excel file
    /// </summary>
    /// <param name="filePath">Path to the Excel file</param>
    /// <returns>Collection of budget entries</returns>
    Task<IEnumerable<BudgetEntry>> ReadBudgetDataAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the structure of an Excel file for budget import
    /// </summary>
    /// <param name="filePath">Path to the Excel file</param>
    /// <returns>True if the file structure is valid</returns>
    Task<bool> ValidateExcelStructureAsync(string filePath, CancellationToken cancellationToken = default);
}
