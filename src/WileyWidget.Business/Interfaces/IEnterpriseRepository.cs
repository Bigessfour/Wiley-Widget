#nullable enable

using System.Threading;
using WileyWidget.Models;
using WileyWidget.Business;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for Enterprise entities.
/// Defines data access operations for municipal enterprises.
/// </summary>
public interface IEnterpriseRepository
{
    /// <summary>
    /// Gets all enterprises.
    /// </summary>
    Task<IEnumerable<Enterprise>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an enterprise by ID.
    /// </summary>
    Task<Enterprise?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets enterprises by type.
    /// </summary>
    Task<IEnumerable<Enterprise>> GetByTypeAsync(string type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new enterprise.
    /// </summary>
    Task<Enterprise> AddAsync(Enterprise enterprise, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing enterprise.
    /// </summary>
    Task<Enterprise> UpdateAsync(Enterprise enterprise, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an enterprise by ID.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of enterprises.
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}

