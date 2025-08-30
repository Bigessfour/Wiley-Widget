using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intuit.Ipp.Core;
using Intuit.Ipp.Data;
using Intuit.Ipp.DataService;
using Intuit.Ipp.OAuth2PlatformClient;
using Intuit.Ipp.Security;
using Intuit.Ipp.QueryFilter;
using Microsoft.Identity.Client;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// QuickBooks service using Intuit SDK + (placeholder) interactive flow. NOTE: MSAL does not directly broker Intuit auth codes; retained for future refinement.
/// For now implement token refresh + DataService access; initial interactive acquisition still handled by prior manual flow (to be unified later).
/// </summary>
public sealed class QuickBooksService
{
    private readonly string _clientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID", EnvironmentVariableTarget.User) ?? throw new InvalidOperationException("QBO_CLIENT_ID not set.");
    private readonly string _clientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET", EnvironmentVariableTarget.User) ?? string.Empty; // optional
    private readonly string _redirectUri = "http://localhost:8080/callback";
    private readonly string _realmId = Environment.GetEnvironmentVariable("QBO_REALM_ID", EnvironmentVariableTarget.User) ?? throw new InvalidOperationException("QBO_REALM_ID not set.");
    private readonly string _environment = "sandbox";
    private readonly OAuth2Client _oauthClient;
    private readonly SettingsService _settings;

    public QuickBooksService(SettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _oauthClient = new OAuth2Client(_clientId, _clientSecret, _redirectUri, _environment);
    }

    public bool HasValidAccessToken()
    {
        var s = _settings.Current;
    // Consider token valid if set and expires more than 60s from now (renew early to avoid edge expiry in-flight)
    if (string.IsNullOrWhiteSpace(s.QboAccessToken)) return false;
    // Default(DateTime) means 'unset'
    if (s.QboTokenExpiry == default) return false;
    return s.QboTokenExpiry > DateTime.UtcNow.AddSeconds(60);
    }

    public async System.Threading.Tasks.Task RefreshTokenIfNeededAsync()
    {
        var s = _settings.Current;
        if (HasValidAccessToken()) return;
        if (string.IsNullOrWhiteSpace(s.QboRefreshToken)) throw new InvalidOperationException("No QBO refresh token available. Perform initial authorization.");
        await RefreshTokenAsync();
    }

    public async System.Threading.Tasks.Task RefreshTokenAsync()
    {
        var s = _settings.Current;
        var response = await _oauthClient.RefreshTokenAsync(s.QboRefreshToken);
        s.QboAccessToken = response.AccessToken;
        s.QboRefreshToken = response.RefreshToken;
        // SDK response no longer exposes ExpiresIn strongly-typed; assume 55 minutes (typical 60) unless reflection finds property.
        var assumedLifetime = TimeSpan.FromMinutes(55);
        var expiresInProp = response.GetType().GetProperty("ExpiresIn");
        if (expiresInProp != null)
        {
            try
            {
                var val = expiresInProp.GetValue(response);
                if (val is int seconds && seconds > 0) assumedLifetime = TimeSpan.FromSeconds(seconds);
            }
            catch { }
        }
        s.QboTokenExpiry = DateTime.UtcNow.Add(assumedLifetime);
        _settings.Save();
        Serilog.Log.Information("QBO token refreshed (exp {Expiry})", s.QboTokenExpiry);
    }

    private (ServiceContext Ctx, DataService Ds) GetDataService()
    {
        var s = _settings.Current;
        if (!HasValidAccessToken()) throw new InvalidOperationException("Access token invalid – refresh required.");
        var validator = new OAuth2RequestValidator(s.QboAccessToken);
        var ctx = new ServiceContext(_realmId, IntuitServicesType.QBO, validator);
        ctx.IppConfiguration.BaseUrl.Qbo = _environment == "sandbox" ? "https://sandbox-quickbooks.api.intuit.com/" : "https://quickbooks.api.intuit.com/";
        return (ctx, new DataService(ctx));
    }

    public async System.Threading.Tasks.Task<List<Customer>> GetCustomersAsync()
    {
        try
        {
            await RefreshTokenIfNeededAsync();
            var p = GetDataService();
            return p.Ds.FindAll(new Customer(), 1, 100).ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO customers fetch failed");
            throw;
        }
    }

    public async System.Threading.Tasks.Task<List<Invoice>> GetInvoicesAsync(string enterprise = null)
    {
        try
        {
            await RefreshTokenIfNeededAsync();
            var p = GetDataService();
            if (string.IsNullOrWhiteSpace(enterprise))
                return p.Ds.FindAll(new Invoice(), 1, 100).ToList();
            var query = $"SELECT * FROM Invoice WHERE Metadata.CustomField['Enterprise'] = '{enterprise}'";
            var qs = new QueryService<Invoice>(p.Ctx);
            return qs.ExecuteIdsQuery(query).ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO invoices fetch failed");
            throw;
        }
    }

    public async Task<string> SyncEnterpriseToQboClassAsync(Enterprise enterprise)
    {
        try
        {
            await RefreshTokenIfNeededAsync();
            var (ctx, ds) = GetDataService();

            Class qbClass;
            if (string.IsNullOrEmpty(enterprise.QboClassId))
            {
                // Create new class
                qbClass = new Class
                {
                    Name = enterprise.Name + "Fund",
                    Active = true
                };
                qbClass = ds.Add(qbClass);
                enterprise.QboClassId = qbClass.Id;
                enterprise.QboSyncStatus = QboSyncStatus.Synced;
                enterprise.QboLastSync = DateTime.UtcNow;
                Serilog.Log.Information("Created QBO Class {ClassId} for enterprise {EnterpriseName}", qbClass.Id, enterprise.Name);
            }
            else
            {
                // Update existing class
                qbClass = ds.FindById(new Class { Id = enterprise.QboClassId });
                if (qbClass == null)
                {
                    throw new InvalidOperationException($"QBO Class {enterprise.QboClassId} not found");
                }
                qbClass.Name = enterprise.Name + "Fund";
                qbClass = ds.Update(qbClass);
                enterprise.QboSyncStatus = QboSyncStatus.Synced;
                enterprise.QboLastSync = DateTime.UtcNow;
                Serilog.Log.Information("Updated QBO Class {ClassId} for enterprise {EnterpriseName}", qbClass.Id, enterprise.Name);
            }

            return qbClass.Id;
        }
        catch (Exception ex)
        {
            enterprise.QboSyncStatus = QboSyncStatus.Failed;
            Serilog.Log.Error(ex, "Failed to sync enterprise {EnterpriseName} to QBO Class", enterprise.Name);
            throw;
        }
    }

    public async Task<string> SyncBudgetInteractionToQboAccountAsync(BudgetInteraction interaction, string classId)
    {
        try
        {
            await RefreshTokenIfNeededAsync();
            var (ctx, ds) = GetDataService();

            Account qbAccount;
            if (string.IsNullOrEmpty(interaction.QboAccountId))
            {
                // Create new account
                qbAccount = new Account
                {
                    Name = $"{interaction.PrimaryEnterprise?.Name ?? "Unknown"}_{interaction.InteractionType}",
                    AccountType = interaction.IsCost ? AccountTypeEnum.Expense : AccountTypeEnum.Income,
                    Classification = AccountClassificationEnum.Revenue,
                    Active = true,
                    Description = interaction.Description
                };
                qbAccount = ds.Add(qbAccount);
                interaction.QboAccountId = qbAccount.Id;
                interaction.QboSyncStatus = QboSyncStatus.Synced;
                interaction.QboLastSync = DateTime.UtcNow;
                Serilog.Log.Information("Created QBO Account {AccountId} for budget interaction {InteractionId}", qbAccount.Id, interaction.Id);
            }
            else
            {
                // Update existing account
                qbAccount = ds.FindById(new Account { Id = interaction.QboAccountId });
                if (qbAccount == null)
                {
                    throw new InvalidOperationException($"QBO Account {interaction.QboAccountId} not found");
                }
                qbAccount.Name = $"{interaction.PrimaryEnterprise?.Name ?? "Unknown"}_{interaction.InteractionType}";
                qbAccount.Description = interaction.Description;
                qbAccount = ds.Update(qbAccount);
                interaction.QboSyncStatus = QboSyncStatus.Synced;
                interaction.QboLastSync = DateTime.UtcNow;
                Serilog.Log.Information("Updated QBO Account {AccountId} for budget interaction {InteractionId}", qbAccount.Id, interaction.Id);
            }

            return qbAccount.Id;
        }
        catch (Exception ex)
        {
            interaction.QboSyncStatus = QboSyncStatus.Failed;
            Serilog.Log.Error(ex, "Failed to sync budget interaction {InteractionId} to QBO Account", interaction.Id);
            throw;
        }
    }
}
