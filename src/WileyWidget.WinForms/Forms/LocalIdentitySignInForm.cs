using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Services.Abstractions;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Forms;

internal sealed class LocalIdentitySignInForm : Form
{
    private readonly Func<CancellationToken, Task<bool>> _hasUsersAsync;
    private readonly Func<string, string, CancellationToken, Task<AuthenticationSessionResult>> _signInAsync;
    private readonly Func<LocalIdentityRegistrationRequest, CancellationToken, Task<AuthenticationSessionResult>> _bootstrapAsync;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _dialogCancellation = new();

    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly Label _identityLabel;
    private readonly TextBox _identityTextBox;
    private readonly Label _displayNameLabel;
    private readonly TextBox _displayNameTextBox;
    private readonly Label _emailLabel;
    private readonly TextBox _emailTextBox;
    private readonly Label _passwordLabel;
    private readonly TextBox _passwordTextBox;
    private readonly Label _confirmPasswordLabel;
    private readonly TextBox _confirmPasswordTextBox;
    private readonly Label _statusLabel;
    private readonly Button _submitButton;
    private readonly Button _cancelButton;

    private AuthenticationSessionResult? _session;
    private bool _bootstrapMode;

    private LocalIdentitySignInForm(
        Func<CancellationToken, Task<bool>> hasUsersAsync,
        Func<string, string, CancellationToken, Task<AuthenticationSessionResult>> signInAsync,
        Func<LocalIdentityRegistrationRequest, CancellationToken, Task<AuthenticationSessionResult>> bootstrapAsync,
        ILogger logger)
    {
        _hasUsersAsync = hasUsersAsync ?? throw new ArgumentNullException(nameof(hasUsersAsync));
        _signInAsync = signInAsync ?? throw new ArgumentNullException(nameof(signInAsync));
        _bootstrapAsync = bootstrapAsync ?? throw new ArgumentNullException(nameof(bootstrapAsync));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Text = "Wiley Widget Sign-In";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(620, 420);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(18)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var row = 0; row < 7; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(0, 0, 0, 8),
            Text = "Wiley Widget Sign-In"
        };
        layout.SetColumnSpan(_titleLabel, 2);
        layout.Controls.Add(_titleLabel, 0, 0);

        _subtitleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            Padding = new Padding(0, 0, 0, 12),
            Text = "Sign in with the local Wiley Widget account stored in the application database."
        };
        layout.SetColumnSpan(_subtitleLabel, 2);
        layout.Controls.Add(_subtitleLabel, 0, 1);

        _identityLabel = CreateFieldLabel("Username or email");
        _identityTextBox = CreateInputBox(false);
        layout.Controls.Add(_identityLabel, 0, 2);
        layout.Controls.Add(_identityTextBox, 1, 2);

        _displayNameLabel = CreateFieldLabel("Display name");
        _displayNameTextBox = CreateInputBox(false);
        layout.Controls.Add(_displayNameLabel, 0, 3);
        layout.Controls.Add(_displayNameTextBox, 1, 3);

        _emailLabel = CreateFieldLabel("Email");
        _emailTextBox = CreateInputBox(false);
        layout.Controls.Add(_emailLabel, 0, 4);
        layout.Controls.Add(_emailTextBox, 1, 4);

        _passwordLabel = CreateFieldLabel("Password");
        _passwordTextBox = CreateInputBox(true);
        layout.Controls.Add(_passwordLabel, 0, 5);
        layout.Controls.Add(_passwordTextBox, 1, 5);

        _confirmPasswordLabel = CreateFieldLabel("Confirm password");
        _confirmPasswordTextBox = CreateInputBox(true);
        layout.Controls.Add(_confirmPasswordLabel, 0, 6);
        layout.Controls.Add(_confirmPasswordTextBox, 1, 6);

        var footerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 12, 0, 0)
        };
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        footerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _statusLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(0, 8, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Checking local user store..."
        };
        footerPanel.SetColumnSpan(_statusLabel, 2);
        footerPanel.Controls.Add(_statusLabel, 0, 0);

        var buttonRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0)
        };

        _cancelButton = new Button
        {
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(8, 0, 0, 0),
            MinimumSize = new Size(100, 34),
            Text = "Cancel"
        };
        _cancelButton.Click += HandleCancelClick;

        _submitButton = new Button
        {
            AutoSize = true,
            Margin = new Padding(8, 0, 0, 0),
            MinimumSize = new Size(140, 34),
            Text = "Sign In"
        };
        _submitButton.Click += HandleSubmitClick;

        buttonRow.Controls.Add(_cancelButton);
        buttonRow.Controls.Add(_submitButton);
        footerPanel.Controls.Add(buttonRow, 1, 1);

        layout.SetColumnSpan(footerPanel, 2);
        layout.Controls.Add(footerPanel, 0, 7);

        Controls.Add(layout);

        AcceptButton = _submitButton;
        CancelButton = _cancelButton;

        ThemeColors.ApplyTheme(this);
        UpdateModeUi(bootstrapMode: false);

        Shown += HandleShown;
        FormClosed += (_, _) => _dialogCancellation.Dispose();
    }

    public static AuthenticationSessionResult ShowForInitialSignIn(
        IWin32Window? ownerWindow,
        Func<CancellationToken, Task<bool>> hasUsersAsync,
        Func<string, string, CancellationToken, Task<AuthenticationSessionResult>> signInAsync,
        Func<LocalIdentityRegistrationRequest, CancellationToken, Task<AuthenticationSessionResult>> bootstrapAsync,
        ILogger logger)
    {
        using var form = new LocalIdentitySignInForm(hasUsersAsync, signInAsync, bootstrapAsync, logger);
        var result = ownerWindow == null ? form.ShowDialog() : form.ShowDialog(ownerWindow);

        if (result == DialogResult.OK && form._session != null)
        {
            return form._session;
        }

        throw new OperationCanceledException("Local identity authentication was canceled.");
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(0, 9, 12, 0),
            Text = text,
            TextAlign = ContentAlignment.TopLeft
        };
    }

    private static TextBox CreateInputBox(bool password)
    {
        return new TextBox
        {
            Dock = DockStyle.Top,
            Margin = new Padding(0, 4, 0, 4),
            UseSystemPasswordChar = password,
            Width = 360
        };
    }

    private async void HandleShown(object? sender, EventArgs e)
    {
        try
        {
            SetBusy(true);
            var hasUsers = await _hasUsersAsync(_dialogCancellation.Token).ConfigureAwait(true);
            UpdateModeUi(bootstrapMode: !hasUsers);
            SetStatus(hasUsers
                ? "Enter your local Wiley Widget credentials."
                : "No local users exist yet. Create the initial administrator account.", isError: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inspect local identity store.");
            SetStatus("Unable to inspect the local user store. Close the dialog and review application logs.", isError: true);
            _submitButton.Enabled = false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void HandleCancelClick(object? sender, EventArgs e)
    {
        _dialogCancellation.Cancel();
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private async void HandleSubmitClick(object? sender, EventArgs e)
    {
        try
        {
            SetBusy(true);

            if (_bootstrapMode)
            {
                ValidateBootstrapInput();

                _session = await _bootstrapAsync(
                    new LocalIdentityRegistrationRequest(
                        UserName: GetText(_identityTextBox),
                        DisplayName: GetText(_displayNameTextBox),
                        Email: GetText(_emailTextBox),
                        Password: _passwordTextBox.Text),
                    _dialogCancellation.Token).ConfigureAwait(true);
            }
            else
            {
                var identity = GetText(_identityTextBox);
                var password = _passwordTextBox.Text;

                if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(password))
                {
                    throw new InvalidOperationException("Username/email and password are required.");
                }

                _session = await _signInAsync(identity, password, _dialogCancellation.Token).ConfigureAwait(true);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Authentication canceled.", isError: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local identity sign-in failed.");
            SetStatus(ex.Message, isError: true);
            _passwordTextBox.SelectAll();
            _passwordTextBox.Focus();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ValidateBootstrapInput()
    {
        if (string.IsNullOrWhiteSpace(GetText(_identityTextBox)))
        {
            throw new InvalidOperationException("Administrator username is required.");
        }

        if (string.IsNullOrWhiteSpace(GetText(_displayNameTextBox)))
        {
            throw new InvalidOperationException("Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(GetText(_emailTextBox)))
        {
            throw new InvalidOperationException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(_passwordTextBox.Text))
        {
            throw new InvalidOperationException("Password is required.");
        }

        if (!string.Equals(_passwordTextBox.Text, _confirmPasswordTextBox.Text, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Password and confirmation do not match.");
        }
    }

    private void UpdateModeUi(bool bootstrapMode)
    {
        _bootstrapMode = bootstrapMode;

        _titleLabel.Text = bootstrapMode
            ? "Create Local Administrator"
            : "Wiley Widget Sign-In";

        _subtitleLabel.Text = bootstrapMode
            ? "The app did not find any local accounts. Create the first administrator to bootstrap ASP.NET Core Identity."
            : "Sign in with the local Wiley Widget account stored in the application database.";

        _identityLabel.Text = bootstrapMode ? "Admin username" : "Username or email";
        _submitButton.Text = bootstrapMode ? "Create Admin" : "Sign In";

        _displayNameLabel.Visible = bootstrapMode;
        _displayNameTextBox.Visible = bootstrapMode;
        _emailLabel.Visible = bootstrapMode;
        _emailTextBox.Visible = bootstrapMode;
        _confirmPasswordLabel.Visible = bootstrapMode;
        _confirmPasswordTextBox.Visible = bootstrapMode;

        if (!bootstrapMode)
        {
            _displayNameTextBox.Clear();
            _emailTextBox.Clear();
            _confirmPasswordTextBox.Clear();
        }
    }

    private void SetBusy(bool busy)
    {
        _identityTextBox.Enabled = !busy;
        _displayNameTextBox.Enabled = !busy;
        _emailTextBox.Enabled = !busy;
        _passwordTextBox.Enabled = !busy;
        _confirmPasswordTextBox.Enabled = !busy;
        _submitButton.Enabled = !busy;
        _cancelButton.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void SetStatus(string message, bool isError)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = isError ? ThemeColors.Error : SystemColors.ControlText;
    }

    private static string GetText(TextBox textBox)
    {
        return textBox.Text?.Trim() ?? string.Empty;
    }
}
