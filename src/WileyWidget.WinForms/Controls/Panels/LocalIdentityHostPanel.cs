using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.Themes;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Utilities;

namespace WileyWidget.WinForms.Controls.Panels;

internal sealed class LocalIdentityHostPanel : UserControl, IThemable
{
    internal sealed class AuthenticationCompletedEventArgs : EventArgs
    {
        public AuthenticationCompletedEventArgs(AuthenticationSessionResult session)
        {
            Session = session;
        }

        public AuthenticationSessionResult Session { get; }
    }

    private readonly AuthenticationBootstrapper _authenticationBootstrapper;
    private readonly SyncfusionControlFactory _controlFactory;
    private readonly ILogger<LocalIdentityHostPanel> _logger;
    private readonly IThemeService _themeService;
    private readonly CancellationTokenSource _panelCancellation = new();
    private EventHandler<string>? _themeChangedHandler;

    private readonly TextBoxExt _identityTextBox;
    private readonly TextBoxExt _displayNameTextBox;
    private readonly TextBoxExt _emailTextBox;
    private readonly TextBoxExt _passwordTextBox;
    private readonly TextBoxExt _confirmPasswordTextBox;
    private readonly CheckBoxAdv _showPasswordCheckBox;
    private readonly CheckBoxAdv _rememberMeCheckBox;
    private readonly Label _statusLabel;
    private readonly Label _subtitleLabel;
    private readonly Label _displayNameLabel;
    private readonly Label _emailLabel;
    private readonly Label _confirmPasswordLabel;
    private readonly SfButton _primaryButton;
    private readonly SfButton _secondaryButton;
    private readonly LoadingOverlay _loadingOverlay;

    private bool _bootstrapMode;

    public event EventHandler<AuthenticationCompletedEventArgs>? AuthenticationCompleted;
    public event EventHandler? AuthenticationCanceled;

    public LocalIdentityHostPanel(
        AuthenticationBootstrapper authenticationBootstrapper,
        SyncfusionControlFactory controlFactory,
        IThemeService themeService,
        ILogger<LocalIdentityHostPanel> logger)
    {
        _authenticationBootstrapper = authenticationBootstrapper ?? throw new ArgumentNullException(nameof(authenticationBootstrapper));
        _controlFactory = controlFactory ?? throw new ArgumentNullException(nameof(controlFactory));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Name = "LocalIdentityHostPanel";
        Dock = DockStyle.Fill;
        AutoScaleMode = AutoScaleMode.Dpi;
        AccessibleName = "Local identity authentication panel";

        var themeName = _themeService.CurrentTheme;
        SfSkinManager.SetVisualStyle(this, themeName);

        var rootLayout = _controlFactory.CreateTableLayoutPanel(layout =>
        {
            layout.Name = "LocalIdentityRootLayout";
            layout.Dock = DockStyle.Fill;
            layout.Padding = LayoutTokens.GetScaled(LayoutTokens.PanelOuterPadding);
            layout.ColumnCount = 3;
            layout.RowCount = 3;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 640F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        });

        var authenticationSurface = _controlFactory.CreateAuthenticationSurfacePanel(panel =>
        {
            panel.Name = "AuthenticationSurfacePanel";
            panel.Padding = new Padding(28);
            panel.Margin = Padding.Empty;
        });

        var surfaceLayout = _controlFactory.CreateTableLayoutPanel(layout =>
        {
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 10;
            layout.Margin = Padding.Empty;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var row = 0; row < 9; row++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        });

        var header = _controlFactory.CreatePanelHeader(panelHeader =>
        {
            panelHeader.Dock = DockStyle.Fill;
            panelHeader.Title = "Sign In";
            panelHeader.ShowCloseButton = false;
            panelHeader.ShowHelpButton = false;
            panelHeader.ShowPinButton = false;
            panelHeader.ShowRefreshButton = false;
            panelHeader.Height = LayoutTokens.GetScaled(LayoutTokens.HeaderHeightLarge);
            panelHeader.AccessibleName = "Authentication header";
        });
        surfaceLayout.SetColumnSpan(header, 2);
        surfaceLayout.Controls.Add(header, 0, 0);

        var titleLabel = _controlFactory.CreateLabel("Wiley Widget Identity", label =>
        {
            label.Dock = DockStyle.Fill;
            label.AutoSize = true;
            label.Font = new Font("Segoe UI", 15F, FontStyle.Bold, GraphicsUnit.Point);
            label.Padding = new Padding(0, 12, 0, 4);
        });
        surfaceLayout.SetColumnSpan(titleLabel, 2);
        surfaceLayout.Controls.Add(titleLabel, 0, 1);

        _subtitleLabel = _controlFactory.CreateLabel("Sign in with the local ASP.NET Core Identity account stored in the Wiley Widget database.", label =>
        {
            label.Dock = DockStyle.Fill;
            label.AutoSize = true;
            label.Padding = new Padding(0, 0, 0, 12);
        });
        surfaceLayout.SetColumnSpan(_subtitleLabel, 2);
        surfaceLayout.Controls.Add(_subtitleLabel, 0, 2);

        surfaceLayout.Controls.Add(CreateFieldLabel("Username or email"), 0, 3);
        _identityTextBox = _controlFactory.CreateAuthenticationTextBox("Identity input");
        surfaceLayout.Controls.Add(_identityTextBox, 1, 3);

        _displayNameLabel = CreateFieldLabel("Display name");
        surfaceLayout.Controls.Add(_displayNameLabel, 0, 4);
        _displayNameTextBox = _controlFactory.CreateAuthenticationTextBox("Display name input");
        surfaceLayout.Controls.Add(_displayNameTextBox, 1, 4);

        _emailLabel = CreateFieldLabel("Email");
        surfaceLayout.Controls.Add(_emailLabel, 0, 5);
        _emailTextBox = _controlFactory.CreateAuthenticationTextBox("Email input");
        surfaceLayout.Controls.Add(_emailTextBox, 1, 5);

        surfaceLayout.Controls.Add(CreateFieldLabel("Password"), 0, 6);
        _passwordTextBox = _controlFactory.CreateAuthenticationTextBox("Password input", password: true);
        surfaceLayout.Controls.Add(_passwordTextBox, 1, 6);

        _confirmPasswordLabel = CreateFieldLabel("Confirm password");
        surfaceLayout.Controls.Add(_confirmPasswordLabel, 0, 7);
        _confirmPasswordTextBox = _controlFactory.CreateAuthenticationTextBox("Confirm password input", password: true);
        surfaceLayout.Controls.Add(_confirmPasswordTextBox, 1, 7);

        var footerLayout = _controlFactory.CreateTableLayoutPanel(layout =>
        {
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 3;
            layout.Margin = new Padding(0, 16, 0, 0);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        });

        _showPasswordCheckBox = _controlFactory.CreateCheckBoxAdv("Show password", checkBox =>
        {
            checkBox.AccessibleName = "Show password";
            checkBox.CheckedChanged += HandleShowPasswordChanged;
        });

        _rememberMeCheckBox = _controlFactory.CreateCheckBoxAdv("Remember me on this device", checkBox =>
        {
            checkBox.AccessibleName = "Remember me";
            checkBox.Visible = false;
        });

        var optionRow = _controlFactory.CreateFlowLayoutPanel(flow =>
        {
            flow.AutoSize = true;
            flow.FlowDirection = FlowDirection.LeftToRight;
            flow.WrapContents = false;
            flow.Dock = DockStyle.Fill;
            flow.Margin = Padding.Empty;
        });
        optionRow.Controls.Add(_showPasswordCheckBox);
        optionRow.Controls.Add(_rememberMeCheckBox);
        footerLayout.Controls.Add(optionRow, 0, 0);

        _statusLabel = _controlFactory.CreateAuthenticationStatusLabel(label =>
        {
            label.Text = "Checking local identity store...";
        });
        footerLayout.SetColumnSpan(_statusLabel, 2);
        footerLayout.Controls.Add(_statusLabel, 0, 1);

        var buttonRow = _controlFactory.CreateFlowLayoutPanel(flow =>
        {
            flow.AutoSize = true;
            flow.FlowDirection = FlowDirection.RightToLeft;
            flow.WrapContents = false;
            flow.Dock = DockStyle.Fill;
            flow.Margin = Padding.Empty;
        });

        _secondaryButton = _controlFactory.CreateSfButton("Exit", button =>
        {
            button.MinimumSize = new Size(100, LayoutTokens.StandardControlHeightLarge);
            button.Click += HandleCancelClick;
        });

        _primaryButton = _controlFactory.CreateSfButton("Sign In", button =>
        {
            button.MinimumSize = new Size(140, LayoutTokens.StandardControlHeightLarge);
            button.Click += HandleSubmitClick;
        });

        buttonRow.Controls.Add(_secondaryButton);
        buttonRow.Controls.Add(_primaryButton);
        footerLayout.Controls.Add(buttonRow, 1, 2);

        surfaceLayout.SetColumnSpan(footerLayout, 2);
        surfaceLayout.Controls.Add(footerLayout, 0, 8);

        authenticationSurface.Controls.Add(surfaceLayout);
        rootLayout.Controls.Add(authenticationSurface, 1, 1);
        Controls.Add(rootLayout);

        _loadingOverlay = _controlFactory.CreateLoadingOverlay(overlay =>
        {
            overlay.Name = "AuthenticationLoadingOverlay";
            overlay.Dock = DockStyle.Fill;
            overlay.Message = "Signing in...";
            overlay.Visible = false;
        });
        Controls.Add(_loadingOverlay);
        _loadingOverlay.BringToFront();

        UpdateModeUi(bootstrapMode: false);
        _themeChangedHandler = HandleThemeServiceChanged;
        _themeService.ThemeChanged -= _themeChangedHandler;
        _themeService.ThemeChanged += _themeChangedHandler;
        _logger.LogInformation(
            "LocalIdentityHostPanel constructed. Theme={Theme} Visible={Visible} HandleCreated={HandleCreated}",
            themeName,
            Visible,
            IsHandleCreated);
        ApplyTheme(themeName);
        Load += HandlePanelLoad;
    }

    public void ApplyTheme(string themeName)
    {
        if (IsDisposed || Disposing || string.IsNullOrWhiteSpace(themeName))
        {
            return;
        }

        void ApplyCore()
        {
            try
            {
                _logger.LogDebug(
                    "Applying theme {Theme} to LocalIdentityHostPanel. Visible={Visible} HandleCreated={HandleCreated} Parent={Parent} Bounds={Bounds}",
                    themeName,
                    Visible,
                    IsHandleCreated,
                    Parent?.Name ?? "<null>",
                    Bounds);
                ThemeColors.EnsureThemeAssemblyLoadedForTheme(themeName);
                SfSkinManager.SetVisualStyle(this, themeName);
                SyncfusionControlFactory.ApplyThemeToAllControls(this, themeName);
                Invalidate(true);
                Update();
                _logger.LogInformation(
                    "Applied theme {Theme} to LocalIdentityHostPanel. ControlCount={ControlCount} Visible={Visible} Bounds={Bounds}",
                    themeName,
                    Controls.Count,
                    Visible,
                    Bounds);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to apply theme {Theme} to LocalIdentityHostPanel", themeName);
            }
        }

        if (InvokeRequired)
        {
            BeginInvoke((System.Action)ApplyCore);
            return;
        }

        ApplyCore();
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        _logger.LogInformation(
            "LocalIdentityHostPanel parent changed. Parent={Parent} Visible={Visible} Bounds={Bounds}",
            Parent?.Name ?? "<null>",
            Visible,
            Bounds);
        WireHostFormButtons();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _logger.LogInformation(
            "LocalIdentityHostPanel handle created. Parent={Parent} Visible={Visible} Bounds={Bounds}",
            Parent?.Name ?? "<null>",
            Visible,
            Bounds);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        _logger.LogInformation(
            "LocalIdentityHostPanel visibility changed. Visible={Visible} Parent={Parent} Bounds={Bounds}",
            Visible,
            Parent?.Name ?? "<null>",
            Bounds);
    }

    public void FocusInitialField()
    {
        if (!IsDisposed && IsHandleCreated)
        {
            _identityTextBox.Focus();
            _identityTextBox.SelectAll();
            _logger.LogInformation("LocalIdentityHostPanel focused initial identity field.");
        }
    }

    private void WireHostFormButtons()
    {
        var hostForm = FindForm();
        if (hostForm == null)
        {
            return;
        }

        hostForm.AcceptButton = _primaryButton;
        hostForm.CancelButton = _secondaryButton;
    }

    private void HandleThemeServiceChanged(object? sender, string themeName)
    {
        _logger.LogInformation(
            "LocalIdentityHostPanel received theme change notification. Theme={Theme} Visible={Visible} HandleCreated={HandleCreated}",
            themeName,
            Visible,
            IsHandleCreated);
        ApplyTheme(themeName);
    }

    private Label CreateFieldLabel(string text)
    {
        return _controlFactory.CreateLabel(text, label =>
        {
            label.Dock = DockStyle.Fill;
            label.AutoSize = true;
            label.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            label.Padding = new Padding(0, 10, 12, 0);
            label.TextAlign = ContentAlignment.TopLeft;
        });
    }

    private async void HandlePanelLoad(object? sender, EventArgs e)
    {
        try
        {
            _logger.LogInformation(
                "LocalIdentityHostPanel load started. Visible={Visible} Parent={Parent} Bounds={Bounds}",
                Visible,
                Parent?.Name ?? "<null>",
                Bounds);
            SetBusy(true, "Checking local identity store...");
            var hasUsers = await _authenticationBootstrapper.HasHostedLocalIdentityUsersAsync(_panelCancellation.Token).ConfigureAwait(true);
            UpdateModeUi(bootstrapMode: !hasUsers);
            _rememberMeCheckBox.Visible = _authenticationBootstrapper.SupportsPersistentRememberMe;
            _rememberMeCheckBox.Checked = _authenticationBootstrapper.DefaultRememberMeSelection;
            SetStatus(
                hasUsers
                    ? "Enter your local Wiley Widget credentials."
                    : "No local users exist yet. Create the initial administrator account.",
                isError: false);
            _logger.LogInformation(
                "LocalIdentityHostPanel load completed. BootstrapMode={BootstrapMode} RememberMeVisible={RememberMeVisible} RememberMeDefault={RememberMeDefault}",
                _bootstrapMode,
                _rememberMeCheckBox.Visible,
                _rememberMeCheckBox.Checked);
            FocusInitialField();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Authentication canceled.", isError: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inspect hosted local identity state.");
            SetStatus("Unable to inspect the local user store. Review application logs.", isError: true);
            _primaryButton.Enabled = false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void HandleShowPasswordChanged(object? sender, EventArgs e)
    {
        var showPassword = _showPasswordCheckBox.Checked;
        _passwordTextBox.UseSystemPasswordChar = !showPassword;
        _confirmPasswordTextBox.UseSystemPasswordChar = !showPassword;
    }

    private void HandleCancelClick(object? sender, EventArgs e)
    {
        _panelCancellation.Cancel();
        AuthenticationCanceled?.Invoke(this, EventArgs.Empty);
    }

    private async void HandleSubmitClick(object? sender, EventArgs e)
    {
        try
        {
            SetBusy(true, _bootstrapMode ? "Creating administrator..." : "Signing in...");

            var rememberMe = _rememberMeCheckBox.Visible && _rememberMeCheckBox.Checked;

            AuthenticationSessionResult session;
            if (_bootstrapMode)
            {
                var request = BuildBootstrapRequest();
                session = await _authenticationBootstrapper.RegisterHostedLocalIdentityAsync(
                    request,
                    rememberMe: rememberMe,
                    _panelCancellation.Token).ConfigureAwait(true);
            }
            else
            {
                var (identity, password) = ValidateSignInInput();

                session = await _authenticationBootstrapper
                    .SignInHostedLocalIdentityAsync(
                        identity,
                        password,
                        rememberMe: rememberMe,
                        _panelCancellation.Token)
                    .ConfigureAwait(true);
            }

            SetStatus($"Authenticated as {session.DisplayName}.", isError: false);
            AuthenticationCompleted?.Invoke(this, new AuthenticationCompletedEventArgs(session));
        }
        catch (OperationCanceledException)
        {
            SetStatus("Authentication canceled.", isError: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hosted local identity authentication failed.");
            HandleAuthenticationFailure(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private (string Identity, string Password) ValidateSignInInput()
    {
        var identity = GetTrimmedText(_identityTextBox);
        if (string.IsNullOrWhiteSpace(identity))
        {
            _identityTextBox.Focus();
            _identityTextBox.SelectAll();
            throw new InvalidOperationException("Username or email is required.");
        }

        if (string.IsNullOrWhiteSpace(_passwordTextBox.Text))
        {
            _passwordTextBox.Focus();
            _passwordTextBox.SelectAll();
            throw new InvalidOperationException("Password is required.");
        }

        return (identity, _passwordTextBox.Text);
    }

    private LocalIdentityRegistrationRequest BuildBootstrapRequest()
    {
        if (string.IsNullOrWhiteSpace(GetTrimmedText(_identityTextBox)))
        {
            _identityTextBox.Focus();
            _identityTextBox.SelectAll();
            throw new InvalidOperationException("Administrator username is required.");
        }

        if (string.IsNullOrWhiteSpace(GetTrimmedText(_displayNameTextBox)))
        {
            _displayNameTextBox.Focus();
            _displayNameTextBox.SelectAll();
            throw new InvalidOperationException("Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(GetTrimmedText(_emailTextBox)))
        {
            _emailTextBox.Focus();
            _emailTextBox.SelectAll();
            throw new InvalidOperationException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(_passwordTextBox.Text))
        {
            _passwordTextBox.Focus();
            _passwordTextBox.SelectAll();
            throw new InvalidOperationException("Password is required.");
        }

        if (!string.Equals(_passwordTextBox.Text, _confirmPasswordTextBox.Text, StringComparison.Ordinal))
        {
            _confirmPasswordTextBox.Focus();
            _confirmPasswordTextBox.SelectAll();
            throw new InvalidOperationException("Password confirmation does not match.");
        }

        return new LocalIdentityRegistrationRequest(
            UserName: GetTrimmedText(_identityTextBox),
            DisplayName: GetTrimmedText(_displayNameTextBox),
            Email: GetTrimmedText(_emailTextBox),
            Password: _passwordTextBox.Text);
    }

    private void HandleAuthenticationFailure(string message)
    {
        SetStatus(message, isError: true);

        if (_bootstrapMode)
        {
            _passwordTextBox.SelectAll();
            _passwordTextBox.Focus();
            return;
        }

        _passwordTextBox.Clear();
        _passwordTextBox.Focus();
    }

    private void UpdateModeUi(bool bootstrapMode)
    {
        _bootstrapMode = bootstrapMode;

        _subtitleLabel.Text = bootstrapMode
            ? "Create the first local ASP.NET Core Identity administrator for Wiley Widget."
            : "Sign in with the local ASP.NET Core Identity account stored in the Wiley Widget database.";
        _displayNameLabel.Visible = bootstrapMode;
        _displayNameTextBox.Visible = bootstrapMode;
        _emailLabel.Visible = bootstrapMode;
        _emailTextBox.Visible = bootstrapMode;
        _confirmPasswordLabel.Visible = bootstrapMode;
        _confirmPasswordTextBox.Visible = bootstrapMode;
        _primaryButton.Text = bootstrapMode ? "Create Administrator" : "Sign In";
        _logger.LogInformation(
            "LocalIdentityHostPanel mode updated. BootstrapMode={BootstrapMode} PrimaryAction={PrimaryAction}",
            _bootstrapMode,
            _primaryButton.Text);
    }

    private void SetBusy(bool busy, string? overlayMessage = null)
    {
        _identityTextBox.Enabled = !busy;
        _displayNameTextBox.Enabled = !busy;
        _emailTextBox.Enabled = !busy;
        _passwordTextBox.Enabled = !busy;
        _confirmPasswordTextBox.Enabled = !busy;
        _showPasswordCheckBox.Enabled = !busy;
        _rememberMeCheckBox.Enabled = !busy;
        _primaryButton.Enabled = !busy;
        _secondaryButton.Enabled = !busy;

        if (!string.IsNullOrWhiteSpace(overlayMessage))
        {
            _loadingOverlay.Message = overlayMessage;
        }

        _loadingOverlay.Visible = busy;
        if (busy)
        {
            _loadingOverlay.BringToFront();
        }
    }

    private void SetStatus(string message, bool isError)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = isError ? Color.Red : Color.Green;
    }

    private static string GetTrimmedText(TextBoxBase textBox)
    {
        return textBox.Text.Trim();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_themeChangedHandler != null)
            {
                _themeService.ThemeChanged -= _themeChangedHandler;
                _themeChangedHandler = null;
            }

            _panelCancellation.Dispose();
        }

        base.Dispose(disposing);
    }
}
