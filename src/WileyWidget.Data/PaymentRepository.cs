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
/// Repository implementation for payment/check management
/// </summary>
public class PaymentRepository : IPaymentRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<PaymentRepository> _logger;

    public PaymentRepository(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<PaymentRepository> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting all payments");
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Include(p => p.Invoice)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetRecentAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting {Count} most recent payments", count);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Include(p => p.Invoice)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .Take(count)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payment by ID {Id}", id);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Include(p => p.Invoice)
            .AsNoTracking()
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByCheckNumberAsync(string checkNumber, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payments by check number {CheckNumber}", checkNumber);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Where(p => p.CheckNumber == checkNumber)
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByPayeeAsync(string payee, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payments by payee {Payee}", payee);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Where(p => p.Payee.Contains(payee))
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payments between {StartDate} and {EndDate}", startDate, endDate);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Where(p => p.PaymentDate >= startDate && p.PaymentDate <= endDate)
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByAccountAsync(int accountId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payments for account {AccountId}", accountId);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Where(p => p.MunicipalAccountId == accountId)
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByVendorAsync(int vendorId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payments for vendor {VendorId}", vendorId);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Where(p => p.VendorId == vendorId)
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PaymentRepository: Getting payments with status {Status}", status);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Payments
            .Include(p => p.MunicipalAccount)
            .Include(p => p.Vendor)
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.PaymentDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Payment> AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (payment == null) throw new ArgumentNullException(nameof(payment));

        NormalizePaymentState(payment);

        _logger.LogInformation("PaymentRepository: Adding payment {CheckNumber} to {Payee}", payment.CheckNumber, payment.Payee);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Check for duplicate check number
        if (await CheckNumberExistsAsync(context, payment.CheckNumber, null, cancellationToken))
        {
            throw new InvalidOperationException($"Check number {payment.CheckNumber} already exists");
        }

        payment.CreatedAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;

        context.Payments.Add(payment);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PaymentRepository: Payment {Id} added successfully", payment.Id);
        return payment;
    }

    public async Task<Payment> UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (payment == null) throw new ArgumentNullException(nameof(payment));

        NormalizePaymentState(payment);

        _logger.LogInformation("PaymentRepository: Updating payment {Id}", payment.Id);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Check for duplicate check number (excluding this payment)
        if (await CheckNumberExistsAsync(context, payment.CheckNumber, payment.Id, cancellationToken))
        {
            throw new InvalidOperationException($"Check number {payment.CheckNumber} already exists for another payment");
        }

        var existing = await context.Payments.FindAsync(new object[] { payment.Id }, cancellationToken);
        if (existing == null)
        {
            throw new InvalidOperationException($"Payment {payment.Id} not found");
        }

        // Update properties
        existing.CheckNumber = payment.CheckNumber;
        existing.PaymentDate = payment.PaymentDate;
        existing.Payee = payment.Payee;
        existing.Amount = payment.Amount;
        existing.Description = payment.Description;
        existing.MunicipalAccountId = payment.MunicipalAccountId;
        existing.VendorId = payment.VendorId;
        existing.InvoiceId = payment.InvoiceId;
        existing.Status = payment.Status;
        existing.IsCleared = payment.IsCleared;
        existing.Memo = payment.Memo;
        existing.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PaymentRepository: Payment {Id} updated successfully", payment.Id);
        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PaymentRepository: Deleting payment {Id}", id);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var payment = await context.Payments.FindAsync(new object[] { id }, cancellationToken);
        if (payment == null)
        {
            throw new InvalidOperationException($"Payment {id} not found");
        }

        context.Payments.Remove(payment);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PaymentRepository: Payment {Id} deleted successfully", id);
    }

    public async Task<bool> CheckNumberExistsAsync(string checkNumber, int? excludePaymentId = null, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await CheckNumberExistsAsync(context, checkNumber, excludePaymentId, cancellationToken);
    }

    public async Task<decimal> GetTotalAmountAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = context.Payments.Where(p => p.Status == "Cleared");

        if (startDate.HasValue)
        {
            query = query.Where(p => p.PaymentDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(p => p.PaymentDate <= endDate.Value);
        }

        return await query.SumAsync(p => p.Amount, cancellationToken);
    }

    private static Task<bool> CheckNumberExistsAsync(
        AppDbContext context,
        string checkNumber,
        int? excludePaymentId,
        CancellationToken cancellationToken)
    {
        var query = context.Payments.Where(p => p.CheckNumber == checkNumber);

        if (excludePaymentId.HasValue)
        {
            query = query.Where(p => p.Id != excludePaymentId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    private static void NormalizePaymentState(Payment payment)
    {
        var status = payment.Status?.Trim();
        payment.Status = string.IsNullOrWhiteSpace(status) ? "Pending" : status;

        if (string.Equals(payment.Status, "Void", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(payment.Status, "Voided", StringComparison.OrdinalIgnoreCase))
        {
            payment.Amount = 0m;
            payment.IsCleared = false;
            return;
        }

        if (string.Equals(payment.Status, "Cleared", StringComparison.OrdinalIgnoreCase))
        {
            payment.IsCleared = true;
        }
    }
}
