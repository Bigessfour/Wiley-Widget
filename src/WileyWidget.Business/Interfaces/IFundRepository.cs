#nullable enable

using WileyWidget.Models;
using WileyWidget.Models.Entities;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for Fund entities.
/// Defines data access operations for municipal funds.
/// </summary>
public interface IFundRepository
{
    /// <summary>
    /// Gets all funds.
    /// </summary>
    Task<IEnumerable<Fund>> GetAllAsync();

    /// <summary>
    /// Gets only active funds.
    /// </summary>
    Task<IEnumerable<Fund>> GetActiveAsync();

    /// <summary>
    /// Gets a fund by ID.
    /// </summary>
    Task<Fund?> GetByIdAsync(int id);

    /// <summary>
    /// Gets a fund by fund code.
    /// </summary>
    Task<Fund?> GetByCodeAsync(string fundCode);

    /// <summary>
    /// Adds a new fund.
    /// </summary>
    Task AddAsync(Fund fund);

    /// <summary>
    /// Adds multiple funds.
    /// </summary>
    Task AddRangeAsync(IEnumerable<Fund> funds);

    /// <summary>
    /// Updates an existing fund.
    /// </summary>
    Task UpdateAsync(Fund fund);

    /// <summary>
    /// Deletes a fund by ID. Returns true when an entity was removed.
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks existence of a fund by code.
    /// </summary>
    Task<bool> ExistsByCodeAsync(string fundCode);

    /// <summary>
    /// Gets funds by type.
    /// </summary>
    Task<IEnumerable<Fund>> GetByTypeAsync(FundType fundType);
}
