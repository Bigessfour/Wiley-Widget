using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Dialogs;

public sealed class JarvisChatDialog : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JarvisChatDialog>? _logger;
    private JARVISChatUserControl? _chatControl;
    private bool _initializationTriggered;

    public JarvisChatDialog(IServiceProvider serviceProvider, ILogger<JarvisChatDialog>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;

        InitializeDialog();
        Shown += OnDialogShown;
    }

    private JARVISChatUserControl CreateChatControl()
    {
        IServiceScopeFactory scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IServiceScopeFactory>(_serviceProvider);
        ILogger<JARVISChatUserControl> chatLogger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<JARVISChatUserControl>>(_serviceProvider)
            ?? NullLogger<JARVISChatUserControl>.Instance;

        return new JARVISChatUserControl(scopeFactory, _serviceProvider, chatLogger);
    }

    private void InitializeDialog()
    {
        Text = "JARVIS Chat";
        Name = "JarvisChatDialog";
        Size = new Size(980, 720);
        MinimumSize = new Size(760, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowIcon = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;

        // Chat control is created lazily when the dialog is first shown to ensure
        // the control has a valid handle and non-zero client size before Blazor init.
        ThemeColors.ApplyTheme(this);
    }

    private void OnDialogShown(object? sender, EventArgs e)
    {
        if (_initializationTriggered)
        {
            return;
        }

        // Create and attach the chat control when the dialog is visible and sized.
        if (_chatControl == null || _chatControl.IsDisposed)
        {
            _chatControl = CreateChatControl();
            _chatControl.Dock = DockStyle.Fill;
            Controls.Add(_chatControl);
        }

        _initializationTriggered = true;
        _ = _chatControl.InitializeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Shown -= OnDialogShown;
        }

        base.Dispose(disposing);
    }
}
