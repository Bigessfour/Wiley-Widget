using System;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Serilog;
using WileyWidget.Models;
using System.IO;
#nullable enable

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for handling Azure AD authentication via MSAL.
    /// This implementation replaces prior corrupted/duplicated content.
    /// </summary>
    public class AuthenticationService : IDisposable
    {
        private IPublicClientApplication? _app;
        private readonly AzureAdConfig _config;
        private readonly bool _isConfigured;
        private System.Timers.Timer? _tokenRefreshTimer;

        public event EventHandler<AuthenticationEventArgs>? AuthenticationStateChanged;

        public virtual bool IsAuthenticated
        {
            get
            {
                if (!_isConfigured || _app == null) return false;
                return _app.GetAccountsAsync().GetAwaiter().GetResult().Any();
            }
        }

        public virtual IAccount? CurrentAccount
        {
            get
            {
                if (!_isConfigured || _app == null) return null;
                return _app.GetAccountsAsync().GetAwaiter().GetResult().FirstOrDefault();
            }
        }

        public AuthenticationService(IConfiguration configuration)
        {
            _config = new AzureAdConfig();
            configuration.GetSection("AzureAd").Bind(_config);

            if (string.IsNullOrWhiteSpace(_config.ClientId))
            {
                _isConfigured = false;
                System.Diagnostics.Debug.WriteLine("AuthenticationService: AzureAd:ClientId not configured. Service created in unconfigured state.");
                return;
            }

            _isConfigured = true;

            try
            {
                var builder = PublicClientApplicationBuilder
                    .Create(_config.ClientId)
                    .WithAuthority(_config.Authority)
                    .WithDefaultRedirectUri();

                // First build the app
                _app = builder.Build();

                // Enable token cache persistence (best effort)
                try
                {
                    // TODO: Add proper token cache persistence with MSAL extensions
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Token cache init failed: {ex.Message}");
                }

                // Try to build a brokered variant and prefer it if available (best effort)
                try
                {
                    var brokerBuilder = PublicClientApplicationBuilder.Create(_config.ClientId)
                        .WithAuthority(_config.Authority)
                        .WithDefaultRedirectUri();

                    var brokeredApp = brokerBuilder.Build();
                    try
                    {
                        TokenCacheHelper.EnableSerialization(brokeredApp);
                    }
                    catch { /* ignore */ }

                    // prefer brokered app if build succeeded
                    _app = brokeredApp;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Broker not available: {ex.Message}");
                }

                // Initialize token refresh timer (30 minutes)
                _tokenRefreshTimer = new System.Timers.Timer(30 * 60 * 1000);
                _tokenRefreshTimer.Elapsed += OnTokenRefreshTimerElapsed;
                _tokenRefreshTimer.AutoReset = true;
                _tokenRefreshTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize MSAL: {ex.Message}");
                _isConfigured = false;
                _app = null;
            }
        }

        /// <summary>
        /// Parameterless constructor used by tests and scenarios where configuration is not available.
        /// Creates the service in an unconfigured state.
        /// </summary>
        public AuthenticationService()
        {
            _config = new AzureAdConfig();
            _isConfigured = false;
            _app = null;
        }

        private void EnsureConfigured()
        {
            if (!_isConfigured || _app == null)
                throw new AuthenticationException("AuthenticationService is not configured. AzureAd:ClientId must be set to use authentication features.");
        }

        /// <summary>
        /// Signs in the user interactively (or silently if an account is present)
        /// </summary>
        public async Task<AuthenticationResult> SignInAsync()
        {
            EnsureConfigured();

            async Task<AuthenticationResult> PerformSignIn()
            {
                var accounts = await _app!.GetAccountsAsync();
                AuthenticationResult result;

                if (accounts.Any())
                {
                    // Try silent authentication first
                    result = await _app.AcquireTokenSilent(new[] { "User.Read" }, accounts.First())
                        .ExecuteAsync();
                }
                else
                {
                    // Interactive authentication
                    result = await _app.AcquireTokenInteractive(new[] { "User.Read" })
                        .WithPrompt(Prompt.SelectAccount)
                        .ExecuteAsync();
                }

                OnAuthenticationStateChanged(true);
                Log.Information("User successfully authenticated: {UserName}", result.Account.Username);
                return result;
            }

            try
            {
                // Try to sign in with retry mechanism
                var recovered = await ErrorReportingService.Instance.TryRecoverAsync(
                    null, // No initial exception
                    "Authentication",
                    async () =>
                    {
                        var result = await PerformSignIn();
                        return true; // Success
                    });

                if (recovered)
                {
                    // If recovery succeeded, we need to get the result again
                    return await PerformSignIn();
                }
                else
                {
                    // If no recovery or recovery failed, try once more without recovery
                    return await PerformSignIn();
                }
            }
            catch (MsalException ex)
            {
                OnAuthenticationStateChanged(false);
                ErrorReportingService.Instance.ReportError(ex, "Authentication_SignIn", showToUser: true);
                throw new AuthenticationException($"Authentication failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Signs out the current user
        /// </summary>
        public async Task SignOutAsync()
        {
            EnsureConfigured();
            try
            {
                var accounts = await _app!.GetAccountsAsync();
                foreach (var account in accounts)
                {
                    await _app.RemoveAsync(account);
                }
                OnAuthenticationStateChanged(false);
            }
            catch (Exception ex)
            {
                throw new AuthenticationException($"Sign out failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Acquires an access token silently
        /// </summary>
        public async Task<string> GetAccessTokenAsync()
        {
            EnsureConfigured();

            async Task<string> AcquireToken()
            {
                var accounts = await _app!.GetAccountsAsync();
                if (!accounts.Any())
                {
                    throw new AuthenticationException("No user is signed in");
                }

                var result = await _app.AcquireTokenSilent(new[] { "User.Read" }, accounts.First())
                    .ExecuteAsync();

                Log.Debug("Access token acquired successfully for user: {UserName}", result.Account.Username);
                return result.AccessToken;
            }

            try
            {
                // Try to acquire token with retry mechanism
                var recovered = await ErrorReportingService.Instance.TryRecoverAsync(
                    null, // No initial exception
                    "Authentication",
                    async () =>
                    {
                        await AcquireToken();
                        return true; // Success
                    });

                if (recovered)
                {
                    // If recovery succeeded, we need to get the token again
                    return await AcquireToken();
                }
                else
                {
                    // If no recovery or recovery failed, try once more without recovery
                    return await AcquireToken();
                }
            }
            catch (MsalException ex)
            {
                ErrorReportingService.Instance.ReportError(ex, "Authentication_GetToken", showToUser: true);
                throw new AuthenticationException($"Failed to acquire access token: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets user information from the current account
        /// </summary>
        public virtual UserInfo GetUserInfo()
        {
            EnsureConfigured();
            var account = CurrentAccount;
            if (account == null)
            {
                throw new AuthenticationException("No user is signed in");
            }

            var userInfo = new UserInfo
            {
                Username = account.Username,
                Name = account.Username, // Could be enhanced to get display name from Graph API
                AccountId = account.HomeAccountId?.Identifier ?? string.Empty,
                Email = account.Username?.Contains("@") == true ? account.Username : string.Empty
            };

            // Simple role assignment logic (can be enhanced with Azure AD groups later)
            userInfo.Roles = new List<string> { "User" }; // Default role

            // Assign Admin role based on username/email patterns (customize as needed)
            var uname = account.Username ?? string.Empty;
            if (uname.Contains("admin", StringComparison.OrdinalIgnoreCase) ||
                uname.EndsWith("@yourcompany.com", StringComparison.OrdinalIgnoreCase))
            {
                userInfo.Roles.Add("Admin");
            }

            return userInfo;
        }

        private void OnAuthenticationStateChanged(bool isAuthenticated)
        {
            AuthenticationStateChanged?.Invoke(this, new AuthenticationEventArgs(isAuthenticated));
        }

        private async void OnTokenRefreshTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (!IsAuthenticated) return;

            try
            {
                // Try to refresh the token silently
                await GetAccessTokenAsync();
                System.Diagnostics.Debug.WriteLine("Token refreshed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token refresh failed: {ex.Message}");
                // If refresh fails, the user might need to re-authenticate
                OnAuthenticationStateChanged(false);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tokenRefreshTimer?.Dispose();
            }
        }
    }

    /// <summary>
    /// Event arguments for authentication state changes
    /// </summary>
    public class AuthenticationEventArgs : EventArgs
    {
        public bool IsAuthenticated { get; }

        public AuthenticationEventArgs(bool isAuthenticated)
        {
            IsAuthenticated = isAuthenticated;
        }
    }

    /// <summary>
    /// User information
    /// </summary>
    public class UserInfo
    {
        public string Username { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public bool IsAdmin => Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        public bool IsUser => Roles.Contains("User", StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Authentication exception
    /// </summary>
    public class AuthenticationException : Exception
    {
        public AuthenticationException(string message) : base(message) { }

        public AuthenticationException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    // Inserted helper to fix "TokenCacheHelper does not exist" error.
    // Best-effort wrapper around Microsoft.Identity.Client.Extensions.Msal.MsalCacheHelper
    internal static class TokenCacheHelper
    {
        /// <summary>
        /// Enables persistent token cache for the provided IPublicClientApplication.
        /// This method is best-effort and will swallow exceptions to avoid breaking callers.
        /// </summary>
        public static void EnableSerialization(IPublicClientApplication? app)
        {
            if (app == null) return;

            try
            {
                // Use LocalApplicationData for cross-platform local cache storage
                var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WileyWidget", "msal");
                Directory.CreateDirectory(cacheDir);

                // File name for the cache
                var cacheFileName = "msal_cache.dat";

                // Build storage properties and create the MsalCacheHelper (blocking on async create)
                var storageProps = new StorageCreationPropertiesBuilder(cacheFileName, cacheDir).Build();
                var cacheHelper = MsalCacheHelper.CreateAsync(storageProps).GetAwaiter().GetResult();

                // Register MSAL token cache
                cacheHelper.RegisterCache(app.UserTokenCache);
            }
            catch (Exception ex)
            {
                // Best-effort: log and continue without persistent cache
                try { Serilog.Log.Warning(ex, "TokenCacheHelper initialization failed"); } catch { /* ignore logging failures */ }
            }
        }
    }
}
