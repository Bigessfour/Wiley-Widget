using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Services.Abstractions;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// First-show local host window for MSAL interactive authentication.
/// This keeps sign-in anchored in a native WinForms surface before MainForm loads.
/// </summary>
internal sealed class MsalSignInHostForm : Form
{
    private const string BrowserGuidanceUrl = "https://learn.microsoft.com/entra/msal/dotnet/acquiring-tokens/using-web-browsers";
    private const string DesktopConfigurationUrl = "https://learn.microsoft.com/entra/identity-platform/scenario-desktop-app-configuration";

    private readonly IAuthenticationBootstrapper _authenticationBootstrapper;
    private readonly ILogger _logger;
    private readonly MsalSignInDisplayOptions _displayOptions;
    private readonly Label _statusLabel;
    private readonly CancellationTokenSource _signInCancellation = new();
    private AuthenticationSessionResult? _session;
    private Exception? _failure;
    private bool _signInStarted;

    private MsalSignInHostForm(
        IAuthenticationBootstrapper authenticationBootstrapper,
        ILogger logger,
        MsalSignInDisplayOptions displayOptions)
    {
        _authenticationBootstrapper = authenticationBootstrapper ?? throw new ArgumentNullException(nameof(authenticationBootstrapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _displayOptions = displayOptions;

        Text = "Wiley Widget Sign-In";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ControlBox = false;
        ClientSize = new Size(760, 360);
        BackColor = Color.White;

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 58,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 12, 20, 0),
            Text = "Welcome to Wiley Widget"
        };

        var subtitleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
            Padding = new Padding(20, 0, 20, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Sign in to continue. Credentials, password reset, MFA, and consent are handled by Microsoft Entra."
        };

        var guidanceLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 120,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            Padding = new Padding(20, 0, 20, 0),
            TextAlign = ContentAlignment.TopLeft,
            Text = BuildSetupGuidanceText(_displayOptions)
        };

        var linkLabel = new LinkLabel
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            Padding = new Padding(20, 0, 20, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Open Microsoft Learn setup guidance"
        };

        linkLabel.Links.Clear();
        linkLabel.Links.Add(0, linkLabel.Text.Length, BrowserGuidanceUrl);
        linkLabel.LinkClicked += HandleGuidanceLinkClicked;

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(20, 0, 20, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Preparing Microsoft authentication..."
        };

        var progress = new ProgressBar
        {
            Dock = DockStyle.Bottom,
            Height = 14,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 22
        };

        var cancelButton = new Button
        {
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Width = 88,
            Height = 30,
            Text = "Cancel",
            DialogResult = DialogResult.Cancel
        };

        cancelButton.Click += (_, _) =>
        {
            _signInCancellation.Cancel();
            _statusLabel.Text = "Canceling sign-in...";
        };

        var modeBadgeLabel = new Label
        {
            AutoSize = true,
            Text = _displayOptions.UseEmbeddedWebView
                ? "Mode: Embedded web view"
                : "Mode: System browser",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Left,
            Padding = new Padding(20, 7, 0, 0)
        };

        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 44
        };

        cancelButton.Location = new Point(footerPanel.Width - cancelButton.Width - 20, 7);
        cancelButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        footerPanel.Controls.Add(modeBadgeLabel);
        footerPanel.Controls.Add(cancelButton);

        Controls.Add(_statusLabel);
        Controls.Add(linkLabel);
        Controls.Add(guidanceLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(titleLabel);
        Controls.Add(footerPanel);
        Controls.Add(progress);

        Shown += HandleShown;
        FormClosed += (_, _) => _signInCancellation.Dispose();
    }

    public static AuthenticationSessionResult ShowForInitialSignIn(
        IAuthenticationBootstrapper authenticationBootstrapper,
        ILogger logger,
        AuthenticationOptions authenticationOptions)
    {
        if (authenticationOptions == null)
        {
            throw new ArgumentNullException(nameof(authenticationOptions));
        }

        var displayOptions = new MsalSignInDisplayOptions(
            Authority: authenticationOptions.ExternalId.Authority,
            RedirectUri: authenticationOptions.ExternalId.RedirectUri,
            UseEmbeddedWebView: authenticationOptions.ExternalId.UseEmbeddedWebView);

        using var form = new MsalSignInHostForm(authenticationBootstrapper, logger, displayOptions);
        var dialogResult = form.ShowDialog();

        if (dialogResult == DialogResult.OK && form._session != null)
        {
            return form._session;
        }

        if (form._failure != null)
        {
            throw new InvalidOperationException("Interactive authentication failed.", form._failure);
        }

        throw new OperationCanceledException("Interactive authentication was canceled.");
    }

    private async void HandleShown(object? sender, EventArgs e)
    {
        if (_signInStarted)
        {
            return;
        }

        _signInStarted = true;
        _statusLabel.Text = _displayOptions.UseEmbeddedWebView
            ? "Opening secure Microsoft sign-in in local embedded mode..."
            : "Opening secure Microsoft sign-in in system browser mode...";

        try
        {
            _session = await _authenticationBootstrapper
                .EnsureAuthenticatedAsync(this, _signInCancellation.Token)
                .ConfigureAwait(true);

            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            _failure = ex;

            if (IsAuthenticationCanceled(ex))
            {
                _logger.LogInformation("MSAL interactive authentication canceled by user.");
                DialogResult = DialogResult.Cancel;
            }
            else
            {
                _logger.LogError(ex, "MSAL interactive authentication failed.");
                _statusLabel.Text = "Sign-in failed.";

                MessageBox.Show(
                    this,
                    "Sign-in failed. Please verify your Microsoft Entra settings and try again.",
                    "Authentication Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                DialogResult = DialogResult.Abort;
            }
        }
        finally
        {
            Close();
        }
    }

    private static bool IsAuthenticationCanceled(Exception ex)
    {
        if (ex is OperationCanceledException)
        {
            return true;
        }

        if (ex is MsalException msalException)
        {
            return string.Equals(msalException.ErrorCode, "authentication_canceled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(msalException.ErrorCode, "user_canceled", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string BuildSetupGuidanceText(MsalSignInDisplayOptions options)
    {
        var expectedRedirect = options.UseEmbeddedWebView
            ? ExternalIdRedirectUris.EmbeddedNativeClient
            : ExternalIdRedirectUris.SystemBrowserLocalhost;

        var actualRedirect = string.IsNullOrWhiteSpace(options.RedirectUri)
            ? "(not set)"
            : options.RedirectUri;

        var authorityDisplay = string.IsNullOrWhiteSpace(options.Authority)
            ? "(not set)"
            : options.Authority;

        return
            "Before production rollout, confirm these Microsoft Learn requirements:" + Environment.NewLine +
            "- App registration platform: Mobile and desktop applications" + Environment.NewLine +
            "- Allow public client flows: Enabled" + Environment.NewLine +
            $"- Authority configured: {authorityDisplay}" + Environment.NewLine +
            $"- Redirect URI expected for this mode: {expectedRedirect}" + Environment.NewLine +
            $"- Redirect URI currently configured: {actualRedirect}" + Environment.NewLine +
            "- If using Google social with b2clogin.com, prefer system browser mode";
    }

    private void HandleGuidanceLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            var url = e.Link.LinkData as string ?? BrowserGuidanceUrl;
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            _logger.LogInformation(
                "Opened Microsoft Learn guidance: {PrimaryUrl} (desktop configuration reference: {SecondaryUrl})",
                BrowserGuidanceUrl,
                DesktopConfigurationUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to open Microsoft Learn guidance link.");
        }
    }

    private sealed record MsalSignInDisplayOptions(
        string? Authority,
        string? RedirectUri,
        bool UseEmbeddedWebView);
}
