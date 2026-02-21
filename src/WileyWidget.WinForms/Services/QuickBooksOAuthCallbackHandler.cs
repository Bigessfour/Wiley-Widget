using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using DI = Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.WinForms.Services;

/// <summary>
/// HTTP listener for QuickBooks OAuth 2.0 callback.
/// Listens on http://localhost:5000/callback for authorization code from Intuit.
/// Exchanges the code for access tokens and stores them.
/// Per Intuit docs: https://developer.intuit.com/app/developer/qbo/docs/develop/authentication-and-authorization/oauth-2.0
/// </summary>
public sealed class QuickBooksOAuthCallbackHandler : IDisposable
{
    private readonly ILogger<QuickBooksOAuthCallbackHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _listenUrl;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenerTask;
    private volatile bool _isListening;
    private readonly List<Task> _activeHandlers = new();
    private readonly object _activeHandlersLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="QuickBooksOAuthCallbackHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="serviceProvider">Service provider for resolving dependencies</param>
    /// <param name="listenUrl">URL to listen on (default: http://localhost:5000/)</param>
    public QuickBooksOAuthCallbackHandler(
        ILogger<QuickBooksOAuthCallbackHandler> logger,
        IServiceProvider serviceProvider,
        string? listenUrl = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _listenUrl = listenUrl ?? "http://localhost:5000/";

        if (!_listenUrl.EndsWith("/"))
        {
            _listenUrl += "/";
        }
    }

    /// <summary>
    /// Starts listening for OAuth callback requests.
    /// Must be called before authorization URL is opened.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when listener is ready</returns>
    public async Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        if (_isListening)
        {
            _logger.LogWarning("OAuth callback handler is already listening on {Url}", _listenUrl);
            return;
        }

        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(_listenUrl);
            _httpListener.Start();

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _isListening = true;

            // Start background listener task
            _listenerTask = ListenForCallbackAsync(_cancellationTokenSource.Token);

            _logger.LogInformation("OAuth callback handler listening on {Url}", _listenUrl);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start OAuth callback handler on {Url}", _listenUrl);
            _isListening = false;
            throw;
        }
    }

    /// <summary>
    /// Stops listening for OAuth callbacks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task StopListeningAsync(CancellationToken cancellationToken = default)
    {
        if (!_isListening)
        {
            return;
        }

        try
        {
            _cancellationTokenSource?.Cancel();
            _isListening = false;

            if (_listenerTask != null)
            {
                await _listenerTask.ConfigureAwait(false);
            }

            // Wait briefly for any active handlers to complete before stopping the listener.
            Task[] handlersCopy;
            lock (_activeHandlersLock)
            {
                handlersCopy = _activeHandlers.ToArray();
            }

            if (handlersCopy.Length > 0)
            {
                _logger.LogInformation("Waiting for {Count} active OAuth handler(s) to complete...", handlersCopy.Length);
                var allHandlers = Task.WhenAll(handlersCopy);
                var completed = await Task.WhenAny(allHandlers, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
                if (completed != allHandlers)
                {
                    _logger.LogWarning("Timeout waiting for active OAuth handlers to complete; proceeding to stop listener.");
                }
            }

            _httpListener?.Stop();
            _logger.LogInformation("OAuth callback handler stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping OAuth callback handler");
        }
    }

    /// <summary>
    /// Background task that listens for incoming OAuth callback requests.
    /// </summary>
    private async Task ListenForCallbackAsync(CancellationToken cancellationToken)
    {
        if (_httpListener == null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested && _isListening)
        {
            try
            {
                var contextTask = _httpListener.GetContextAsync();
                var completedTask = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, cancellationToken))
                    .ConfigureAwait(false);

                if (completedTask == contextTask && contextTask.IsCompleted)
                {
                    var context = await contextTask.ConfigureAwait(false);

                    // Track active handler tasks so StopListeningAsync can wait for them
                    var handlerTask = HandleCallbackAsync(context, cancellationToken);
                    lock (_activeHandlersLock)
                    {
                        _activeHandlers.Add(handlerTask);
                    }

                    _ = handlerTask.ContinueWith(t =>
                    {
                        lock (_activeHandlersLock)
                        {
                            _activeHandlers.Remove(t);
                        }
                    }, TaskScheduler.Default);
                }
            }
            catch (ObjectDisposedException)
            {
                // HTTP listener was disposed
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OAuth callback listener");
            }
        }
    }

    /// <summary>
    /// Handles an incoming OAuth callback request.
    /// Extracts authorization code and realm ID, exchanges for tokens.
    /// </summary>
    private async Task HandleCallbackAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            _logger.LogInformation("OAuth callback received: {Path}?{Query}", request.RawUrl, request.QueryString);

            // Parse query string
            var queryParams = ParseQueryString(request.Url?.Query ?? "");

            // Check for error response from Intuit
            if (queryParams.TryGetValue("error", out var error))
            {
                var errorDescription = queryParams.ContainsKey("error_description")
                    ? queryParams["error_description"]
                    : "Unknown error";

                _logger.LogError("OAuth error from Intuit: {Error} - {Description}", error, errorDescription);
                await SendErrorResponseAsync(response, error, errorDescription, cancellationToken);
                return;
            }

            // Extract authorization code and realm ID
            if (!queryParams.TryGetValue("code", out var authorizationCode))
            {
                _logger.LogError("OAuth callback missing authorization code");
                await SendErrorResponseAsync(response, "missing_code", "Authorization code not provided", cancellationToken);
                return;
            }

            if (!queryParams.TryGetValue("realmId", out var realmId))
            {
                _logger.LogWarning("OAuth callback missing realm ID (company ID)");
                realmId = string.Empty; // Realm ID is optional for initial token, can be fetched from company info
            }

            _logger.LogInformation("Exchanging authorization code for tokens (realmId: {RealmId})", realmId);

            // Get auth service and exchange code for tokens
            using (var scope = _serviceProvider.CreateScope())
            {
                var authService = DI.ServiceProviderServiceExtensions.GetRequiredService<IQuickBooksAuthService>(scope.ServiceProvider);
                var tokenStore = DI.ServiceProviderServiceExtensions.GetService<QuickBooksTokenStore>(scope.ServiceProvider);

                var result = await authService.ExchangeCodeForTokenAsync(authorizationCode, cancellationToken)
                    .ConfigureAwait(false);

                if (!result.IsSuccess)
                {
                    _logger.LogError("Failed to exchange authorization code: {Message}", result.ErrorMessage);
                    await SendErrorResponseAsync(response, "token_exchange_failed", result.ErrorMessage ?? "Unknown error", cancellationToken);
                    return;
                }

                // Store token if we have a token store
                if (tokenStore != null && result.Token != null)
                {
                    await tokenStore.SaveTokenAsync(result.Token).ConfigureAwait(false);
                    _logger.LogInformation("OAuth token stored successfully");

                    // Also store the realm ID if provided
                    if (!string.IsNullOrEmpty(realmId))
                    {
                        tokenStore.SetRealmId(realmId);
                    }
                }

                // Send success response
                await SendSuccessResponseAsync(response, realmId, cancellationToken);
                _logger.LogInformation("OAuth authorization completed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling OAuth callback");
            await SendErrorResponseAsync(response, "handler_error", ex.Message, cancellationToken);
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch { }
        }
    }

    /// <summary>
    /// Sends success response to browser after successful OAuth authorization.
    /// </summary>
    private async Task SendSuccessResponseAsync(HttpListenerResponse response, string realmId, CancellationToken cancellationToken)
    {
        var html = $@"<!DOCTYPE html>
<html>
<head>
    <title>QuickBooks Authorization - Success</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }}
        .container {{ background: white; padding: 40px; border-radius: 8px; box-shadow: 0 10px 25px rgba(0,0,0,0.2); text-align: center; max-width: 400px; }}
        .icon {{ font-size: 48px; margin-bottom: 16px; }}
        h1 {{ color: #333; margin: 0 0 8px 0; font-size: 24px; }}
        .subtitle {{ color: #666; margin-bottom: 24px; }}
        .info {{ background: #f0f9ff; border-left: 4px solid #3b82f6; padding: 12px; text-align: left; margin-bottom: 20px; border-radius: 4px; }}
        .info-label {{ font-weight: 600; color: #333; font-size: 12px; text-transform: uppercase; margin-bottom: 4px; }}
        .info-value {{ color: #666; font-family: 'Courier New', monospace; word-break: break-all; }}
        .footer {{ color: #999; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""icon"">✅</div>
        <h1>Authorization Successful!</h1>
        <p class=""subtitle"">Your QuickBooks Online account is now connected.</p>
        <div class=""info"">
            <div class=""info-label"">Company ID (Realm ID)</div>
            <div class=""info-value"">{realmId}</div>
        </div>
        <p>You can now close this window and return to Wiley Widget.</p>
        <p class=""footer"">If the window does not close automatically, please close it manually.</p>
    </div>
    <script>
        // Auto-close after 5 seconds
        setTimeout(function() {{ window.close(); }}, 5000);
    </script>
</body>
</html>";

        response.StatusCode = 200;
        response.ContentType = "text/html; charset=utf-8";

        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;

        try
        {
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Response write canceled while sending success response (likely shutdown).");
        }
        catch (IOException ioEx)
        {
            _logger.LogDebug(ioEx, "IOException when writing success response (likely connection aborted by client).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error writing success response to browser.");
        }
    }

    /// <summary>
    /// Sends error response to browser when OAuth authorization fails.
    /// </summary>
    private async Task SendErrorResponseAsync(HttpListenerResponse response, string error, string description, CancellationToken cancellationToken)
    {
        var html = $@"<!DOCTYPE html>
<html>
<head>
    <title>QuickBooks Authorization - Error</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }}
        .container {{ background: white; padding: 40px; border-radius: 8px; box-shadow: 0 10px 25px rgba(0,0,0,0.2); text-align: center; max-width: 400px; }}
        .icon {{ font-size: 48px; margin-bottom: 16px; }}
        h1 {{ color: #d32f2f; margin: 0 0 8px 0; font-size: 24px; }}
        .subtitle {{ color: #666; margin-bottom: 24px; }}
        .error-box {{ background: #ffebee; border-left: 4px solid #d32f2f; padding: 12px; text-align: left; margin-bottom: 20px; border-radius: 4px; }}
        .error-label {{ font-weight: 600; color: #333; font-size: 12px; text-transform: uppercase; margin-bottom: 4px; }}
        .error-value {{ color: #d32f2f; font-family: 'Courier New', monospace; word-break: break-all; margin-bottom: 8px; }}
        .error-description {{ color: #666; font-size: 13px; }}
        .footer {{ color: #999; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""icon"">❌</div>
        <h1>Authorization Failed</h1>
        <p class=""subtitle"">Unable to connect your QuickBooks Online account.</p>
        <div class=""error-box"">
            <div class=""error-label"">Error</div>
            <div class=""error-value"">{error}</div>
            <div class=""error-description"">{description}</div>
        </div>
        <p>Please try again. If the problem persists, check your app credentials and permissions.</p>
        <p class=""footer"">You can close this window and return to Wiley Widget.</p>
    </div>
    <script>
        // Auto-close after 8 seconds
        setTimeout(function() {{ window.close(); }}, 8000);
    </script>
</body>
</html>";

        response.StatusCode = 400;
        response.ContentType = "text/html; charset=utf-8";

        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;

        try
        {
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Response write canceled while sending error response (likely shutdown).");
        }
        catch (IOException ioEx)
        {
            _logger.LogDebug(ioEx, "IOException when writing error response (likely connection aborted by client).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error writing error response to browser.");
        }
    }

    /// <summary>
    /// Parses query string into a dictionary.
    /// </summary>
    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(query) || query == "?")
        {
            return result;
        }

        if (query.StartsWith("?"))
        {
            query = query.Substring(1);
        }

        var pairs = query.Split('&');
        foreach (var pair in pairs)
        {
            var parts = pair.Split(new[] { '=' }, 2);
            if (parts.Length == 2)
            {
                var key = Uri.UnescapeDataString(parts[0]);
                var value = Uri.UnescapeDataString(parts[1]);
                result[key] = value;
            }
        }

        return result;
    }

    public void Dispose()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _httpListener?.Stop();
            (_httpListener as IDisposable)?.Dispose();
        }
        catch { }
    }
}
