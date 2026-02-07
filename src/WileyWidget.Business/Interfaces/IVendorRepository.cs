#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for vendor/supplier management
/// </summary>
public interface IVendorRepository
{
    /// <summary>
    /// Gets all vendors
    /// </summary>
    Task<IReadOnlyList<Vendor>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active vendors
    /// </summary>
    Task<IReadOnlyList<Vendor>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a vendor by ID
    /// </summary>
    Task<Vendor?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a vendor by name (exact match)
    /// </summary>
    Task<Vendor?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for vendors by name (partial match)
    /// </summary>
    Task<IReadOnlyList<Vendor>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new vendor
    /// </summary>
    Task<Vendor> AddAsync(Vendor vendor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing vendor
    /// </summary>
    Task UpdateAsync(Vendor vendor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a vendor
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
