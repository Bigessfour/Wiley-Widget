using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// QuickBooks service using raw HTTP calls + OAuth2 PKCE flow.
/// </summary>
public sealed class QuickBooksService : IQuickBooksService, IDisposable
{
    private readonly string _clientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID", EnvironmentVariableTarget.User) ?? throw new InvalidOperationException("QBO_CLIENT_ID not set.");
    private readonly string _clientSecret = Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET", EnvironmentVariableTarget.User) ?? string.Empty; // optional
    private readonly string _realmId = Environment.GetEnvironmentVariable("QBO_REALM_ID", EnvironmentVariableTarget.User) ?? throw new InvalidOperationException("QBO_REALM_ID not set.");
    private readonly string _environment = "sandbox";
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settings;

    public QuickBooksService(SettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(_environment == "sandbox"
            ? "https://sandbox-quickbooks.api.intuit.com/"
            : "https://quickbooks.api.intuit.com/");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
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

    public async Task RefreshTokenIfNeededAsync()
    {
        var s = _settings.Current;
        if (HasValidAccessToken()) return;
        if (string.IsNullOrWhiteSpace(s.QboRefreshToken)) throw new InvalidOperationException("No QBO refresh token available. Perform initial authorization.");
        await RefreshTokenAsync();
    }

    public async Task RefreshTokenAsync()
    {
        var s = _settings.Current;

        // Use raw HTTP call to refresh token
        var tokenEndpoint = _environment == "sandbox"
            ? "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer"
            : "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

        using var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", s.QboRefreshToken),
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret)
        });

        var response = await _httpClient.PostAsync(tokenEndpoint, content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);

        s.QboAccessToken = tokenResponse.AccessToken;
        s.QboRefreshToken = tokenResponse.RefreshToken;
        s.QboTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
        _settings.Save();

        Serilog.Log.Information("QBO token refreshed (exp {Expiry})", s.QboTokenExpiry);
    }

    private void SetAuthorizationHeader()
    {
        var s = _settings.Current;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.QboAccessToken);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<List<QboCustomer>> GetCustomersAsync()
    {
        try
        {
            await RefreshTokenIfNeededAsync();
            SetAuthorizationHeader();

            var url = $"v3/company/{_realmId}/customer?fetchAll=false&maxresults=1000";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<QboQueryResponse<QboCustomer>>(content);

            return result?.QueryResponse?.Customer ?? new List<QboCustomer>();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "QBO customers fetch failed");
            throw;
        }
    }

    public async Task<List<QboInvoice>> GetInvoicesAsync(string enterprise = null)
    {
        try
        {
            await RefreshTokenIfNeededAsync();
            SetAuthorizationHeader();

            var url = $"v3/company/{_realmId}/invoice?fetchAll=false&maxresults=1000";
            if (!string.IsNullOrWhiteSpace(enterprise))
            {
                // Note: This is a simplified filter - actual QBO API may require different syntax
                url += $"&where=CustomField.Name='Enterprise' AND CustomField.StringValue='{enterprise}'";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<QboQueryResponse<QboInvoice>>(content);

            return result?.QueryResponse?.Invoice ?? new List<QboInvoice>();
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
            SetAuthorizationHeader();

            QboClass qbClass;

            if (string.IsNullOrEmpty(enterprise.QboClassId))
            {
                // Create new class
                qbClass = new QboClass
                {
                    Name = enterprise.Name + "Fund",
                    Active = true
                };

                var createUrl = $"v3/company/{_realmId}/class";
                var jsonContent = JsonSerializer.Serialize(qbClass);
                using var content = new StringContent(jsonContent, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

                var response = await _httpClient.PostAsync(createUrl, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var createResult = JsonSerializer.Deserialize<QboClassResponse>(responseContent);
                qbClass = createResult.Class;

                enterprise.QboClassId = qbClass.Id;
                enterprise.QboSyncStatus = QboSyncStatus.Synced;
                enterprise.QboLastSync = DateTime.UtcNow;
                Serilog.Log.Information("Created QBO Class {ClassId} for enterprise {EnterpriseName}", qbClass.Id, enterprise.Name);
            }
            else
            {
                // Update existing class
                var getUrl = $"v3/company/{_realmId}/class/{enterprise.QboClassId}";
                var response = await _httpClient.GetAsync(getUrl);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var getResult = JsonSerializer.Deserialize<QboClassResponse>(responseContent);
                qbClass = getResult.Class;

                qbClass.Name = enterprise.Name + "Fund";

                var updateUrl = $"v3/company/{_realmId}/class";
                var jsonContent = JsonSerializer.Serialize(qbClass);
                using var content = new StringContent(jsonContent, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

                var updateResponse = await _httpClient.PostAsync(updateUrl, content);
                updateResponse.EnsureSuccessStatusCode();

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
            SetAuthorizationHeader();

            QboAccount qbAccount;

            if (string.IsNullOrEmpty(interaction.QboAccountId))
            {
                // Create new account
                qbAccount = new QboAccount
                {
                    Name = $"{interaction.PrimaryEnterprise?.Name ?? "Unknown"}_{interaction.InteractionType}",
                    AccountType = interaction.IsCost ? "Expense" : "Income",
                    Classification = "Revenue",
                    Active = true,
                    Description = interaction.Description
                };

                var createUrl = $"v3/company/{_realmId}/account";
                var jsonContent = JsonSerializer.Serialize(qbAccount);
                using var content = new StringContent(jsonContent, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

                var response = await _httpClient.PostAsync(createUrl, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var createResult = JsonSerializer.Deserialize<QboAccountResponse>(responseContent);
                qbAccount = createResult.Account;

                interaction.QboAccountId = qbAccount.Id;
                interaction.QboSyncStatus = QboSyncStatus.Synced;
                interaction.QboLastSync = DateTime.UtcNow;
                Serilog.Log.Information("Created QBO Account {AccountId} for budget interaction {InteractionId}", qbAccount.Id, interaction.Id);
            }
            else
            {
                // Update existing account
                var getUrl = $"v3/company/{_realmId}/account/{interaction.QboAccountId}";
                var response = await _httpClient.GetAsync(getUrl);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var getResult = JsonSerializer.Deserialize<QboAccountResponse>(responseContent);
                qbAccount = getResult.Account;

                qbAccount.Name = $"{interaction.PrimaryEnterprise?.Name ?? "Unknown"}_{interaction.InteractionType}";
                qbAccount.Description = interaction.Description;

                var updateUrl = $"v3/company/{_realmId}/account";
                var jsonContent = JsonSerializer.Serialize(qbAccount);
                using var content = new StringContent(jsonContent, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

                var updateResponse = await _httpClient.PostAsync(updateUrl, content);
                updateResponse.EnsureSuccessStatusCode();

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

// QuickBooks API Response Models
public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }
}

public class QboQueryResponse<T>
{
    [JsonPropertyName("QueryResponse")]
    public QboQueryResult<T> QueryResponse { get; set; }
}

public class QboQueryResult<T>
{
    [JsonPropertyName("Customer")]
    public List<T> Customer { get; set; }

    [JsonPropertyName("Invoice")]
    public List<T> Invoice { get; set; }
}

public class QboCustomer
{
    [JsonPropertyName("Id")]
    public string Id { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; }

    [JsonPropertyName("Active")]
    public bool Active { get; set; }

    [JsonPropertyName("CompanyName")]
    public string CompanyName { get; set; }

    [JsonPropertyName("GivenName")]
    public string GivenName { get; set; }

    [JsonPropertyName("FamilyName")]
    public string FamilyName { get; set; }
}

public class QboInvoice
{
    [JsonPropertyName("Id")]
    public string Id { get; set; }

    [JsonPropertyName("DocNumber")]
    public string DocNumber { get; set; }

    [JsonPropertyName("TxnDate")]
    public DateTime TxnDate { get; set; }

    [JsonPropertyName("TotalAmt")]
    public decimal TotalAmt { get; set; }

    [JsonPropertyName("CustomerRef")]
    public QboReference CustomerRef { get; set; }
}

public class QboReference
{
    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class QboClass
{
    [JsonPropertyName("Id")]
    public string Id { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; }

    [JsonPropertyName("Active")]
    public bool Active { get; set; }
}

public class QboClassResponse
{
    [JsonPropertyName("Class")]
    public QboClass Class { get; set; }
}

public class QboAccount
{
    [JsonPropertyName("Id")]
    public string Id { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; }

    [JsonPropertyName("AccountType")]
    public string AccountType { get; set; }

    [JsonPropertyName("Classification")]
    public string Classification { get; set; }

    [JsonPropertyName("Active")]
    public bool Active { get; set; }

    [JsonPropertyName("Description")]
    public string Description { get; set; }
}

public class QboAccountResponse
{
    [JsonPropertyName("Account")]
    public QboAccount Account { get; set; }
}
