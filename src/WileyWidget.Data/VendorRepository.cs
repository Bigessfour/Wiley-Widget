#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for vendor/supplier management
/// </summary>
public class VendorRepository : IVendorRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<VendorRepository> _logger;

    public VendorRepository(AppDbContext context, ILogger<VendorRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public async Task<IReadOnlyList<Vendor>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("VendorRepository: Getting all vendors");
        return await _context.Vendors
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Vendor>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("VendorRepository: Getting active vendors");
        return await _context.Vendors
            .AsNoTracking()
            .Where(v => v.IsActive)
            .OrderBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Vendor?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("VendorRepository: Getting vendor by ID {Id}", id);
        return await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<Vendor?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalized = name.Trim();
        _logger.LogDebug("VendorRepository: Getting vendor by name {Name}", normalized);

        return await _context.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Name.ToLower() == normalized.ToLower(), cancellationToken);
    }

    public async Task<IReadOnlyList<Vendor>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(cancellationToken);
        }

        var normalized = searchTerm.Trim();
        _logger.LogDebug("VendorRepository: Searching vendors by term {SearchTerm}", normalized);

        return await _context.Vendors
            .AsNoTracking()
            .Where(v => EF.Functions.Like(v.Name, $"%{normalized}%"))
            .OrderBy(v => v.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Vendor> AddAsync(Vendor vendor, CancellationToken cancellationToken = default)
    {
        if (vendor == null)
        {
            throw new ArgumentNullException(nameof(vendor));
        }

        vendor.Name = vendor.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(vendor.Name))
        {
            throw new ArgumentException("Vendor name is required", nameof(vendor));
        }

        vendor.ContactInfo = NormalizeOptional(vendor.ContactInfo);
        vendor.Email = NormalizeOptional(vendor.Email);
        vendor.Phone = NormalizeOptional(vendor.Phone);
        vendor.MailingAddressLine1 = NormalizeOptional(vendor.MailingAddressLine1);
        vendor.MailingAddressLine2 = NormalizeOptional(vendor.MailingAddressLine2);
        vendor.MailingAddressCity = NormalizeOptional(vendor.MailingAddressCity);
        vendor.MailingAddressState = NormalizeOptional(vendor.MailingAddressState);
        vendor.MailingAddressPostalCode = NormalizeOptional(vendor.MailingAddressPostalCode);
        vendor.MailingAddressCountry = NormalizeOptional(vendor.MailingAddressCountry);
        vendor.QuickBooksId = NormalizeOptional(vendor.QuickBooksId);

        var existing = await GetByNameAsync(vendor.Name, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Vendor '{vendor.Name}' already exists");
        }

        _context.Vendors.Add(vendor);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("VendorRepository: Vendor {Id} added successfully", vendor.Id);
        return vendor;
    }

    public async Task UpdateAsync(Vendor vendor, CancellationToken cancellationToken = default)
    {
        if (vendor == null)
        {
            throw new ArgumentNullException(nameof(vendor));
        }

        vendor.Name = vendor.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(vendor.Name))
        {
            throw new ArgumentException("Vendor name is required", nameof(vendor));
        }

        var existing = await _context.Vendors.FindAsync(new object[] { vendor.Id }, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException($"Vendor {vendor.Id} not found");
        }

        if (!string.Equals(existing.Name, vendor.Name, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await GetByNameAsync(vendor.Name, cancellationToken);
            if (duplicate != null && duplicate.Id != vendor.Id)
            {
                throw new InvalidOperationException($"Vendor '{vendor.Name}' already exists");
            }
        }

        existing.Name = vendor.Name;
        existing.ContactInfo = NormalizeOptional(vendor.ContactInfo);
        existing.Email = NormalizeOptional(vendor.Email);
        existing.Phone = NormalizeOptional(vendor.Phone);
        existing.MailingAddressLine1 = NormalizeOptional(vendor.MailingAddressLine1);
        existing.MailingAddressLine2 = NormalizeOptional(vendor.MailingAddressLine2);
        existing.MailingAddressCity = NormalizeOptional(vendor.MailingAddressCity);
        existing.MailingAddressState = NormalizeOptional(vendor.MailingAddressState);
        existing.MailingAddressPostalCode = NormalizeOptional(vendor.MailingAddressPostalCode);
        existing.MailingAddressCountry = NormalizeOptional(vendor.MailingAddressCountry);
        existing.QuickBooksId = NormalizeOptional(vendor.QuickBooksId);
        existing.IsActive = vendor.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var vendor = await _context.Vendors.FindAsync(new object[] { id }, cancellationToken);
        if (vendor == null)
        {
            throw new InvalidOperationException($"Vendor {id} not found");
        }

        _context.Vendors.Remove(vendor);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
