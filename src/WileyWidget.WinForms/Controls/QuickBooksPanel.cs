using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// QuickBooks integration panel that provides access to QuickBooks Online data and operations.
/// Allows users to connect to QuickBooks, sync data, and manage QuickBooks integration.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public sealed class QuickBooksPanel : UserControl
{
    /// <summary>
    /// Represents the _serviceprovider.
    /// </summary>
    /// <summary>
    /// Represents the _serviceprovider.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QuickBooksPanel> _logger;
    private QuickBooksViewModel? _viewModel;
    private BindingSource? _bindingSource;

    // UI Controls
    private Label? _statusLabel;
    private Button? _connectButton;
    private Button? _disconnectButton;
    private Button? _syncButton;
    private Button? _importAccountsButton;
    private TextBox? _statusTextBox;
    private ProgressBar? _progressBar;
    private GroupBox? _connectionGroup;
    private GroupBox? _operationsGroup;


    /// <summary>
    /// Constructor accepting service provider for DI resolution.
    /// </summary>
    public QuickBooksPanel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        _logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<QuickBooksPanel>>(serviceProvider);

        // Resolve ViewModel from DI (transient per view)
        _viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<QuickBooksViewModel>(serviceProvider);

        InitializeComponent();

        // Bind view-model properties to controls
        _bindingSource = new BindingSource { DataSource = _viewModel };
        _statusTextBox?.DataBindings.Add("Text", _bindingSource, nameof(QuickBooksViewModel.StatusText), true, DataSourceUpdateMode.OnPropertyChanged);
        _progressBar?.DataBindings.Add("Visible", _bindingSource, nameof(QuickBooksViewModel.IsLoading), true, DataSourceUpdateMode.OnPropertyChanged);
        _progressBar?.DataBindings.Add("Value", _bindingSource, nameof(QuickBooksViewModel.Progress), true, DataSourceUpdateMode.OnPropertyChanged);
        _connectButton?.DataBindings.Add("Enabled", _bindingSource, nameof(QuickBooksViewModel.CanConnect), true, DataSourceUpdateMode.OnPropertyChanged);
        _disconnectButton?.DataBindings.Add("Enabled", _bindingSource, nameof(QuickBooksViewModel.CanDisconnect), true, DataSourceUpdateMode.OnPropertyChanged);
        _syncButton?.DataBindings.Add("Enabled", _bindingSource, nameof(QuickBooksViewModel.CanSync), true, DataSourceUpdateMode.OnPropertyChanged);
        _importAccountsButton?.DataBindings.Add("Enabled", _bindingSource, nameof(QuickBooksViewModel.CanImportAccounts), true, DataSourceUpdateMode.OnPropertyChanged);
        _statusLabel?.DataBindings.Add("Text", _bindingSource, nameof(QuickBooksViewModel.ConnectionLabel), true, DataSourceUpdateMode.OnPropertyChanged);

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        _logger.LogInformation("QuickBooksPanel created");
    }
    /// <summary>
    /// Performs initializecomponent.
    /// </summary>

    private void InitializeComponent()
    {
        // Panel properties - theme inherited from parent via SkinManager cascade
        Name = "QuickBooksPanel";
        Dock = DockStyle.Fill;
        AutoScroll = true;
        Padding = new Padding(20);

        // Connection Status Group
        _connectionGroup = new GroupBox
        {
            Text = "Connection Status",
            Location = new Point(20, 20),
            Size = new Size(540, 120),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            TabIndex = 0,
            AccessibleName = "Connection Status Group"
        };

        _statusLabel = new Label
        {
            Text = "Checking connection...",
            Location = new Point(20, 30),
            Size = new Size(200, 20),
            AutoSize = false,
            TabIndex = 1,
            AccessibleName = "Connection Status Label"
        };

        _connectButton = new Button
        {
            Name = "connectButton",
            Text = "Connect",
            Location = new Point(20, 60),
            Size = new Size(100, 35),
            Enabled = false,
            TabIndex = 2,
            AccessibleName = "Connect to QuickBooks Button",
            AccessibleDescription = "Establish connection to QuickBooks Online"
        };
        _connectButton.Click += async (s, e) => await _viewModel!.ConnectCommand.ExecuteAsync(null);

        _disconnectButton = new Button
        {
            Name = "disconnectButton",
            Text = "Disconnect",
            Location = new Point(130, 60),
            Size = new Size(100, 35),
            Enabled = false,
            TabIndex = 3,
            AccessibleName = "Disconnect from QuickBooks Button",
            AccessibleDescription = "Disconnect from QuickBooks Online"
        };
        _disconnectButton.Click += async (s, e) => await _viewModel!.DisconnectCommand.ExecuteAsync(null);

        _connectionGroup.Controls.AddRange(new Control[] { _statusLabel, _connectButton, _disconnectButton });

        // Operations Group
        _operationsGroup = new GroupBox
        {
            Text = "Operations",
            Location = new Point(20, 150),
            Size = new Size(540, 120),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            TabIndex = 4,
            AccessibleName = "QuickBooks Operations Group"
        };

        _syncButton = new Button
        {
            Name = "syncButton",
            Text = "Sync Data",
            Location = new Point(20, 30),
            Size = new Size(120, 35),
            Enabled = false,
            TabIndex = 5,
            AccessibleName = "Sync QuickBooks Data Button",
            AccessibleDescription = "Synchronize data from QuickBooks Online"
        };
        _syncButton.Click += async (s, e) => await _viewModel!.SyncCommand.ExecuteAsync(null);

        _importAccountsButton = new Button
        {
            Name = "importAccountsButton",
            Text = "Import Chart of Accounts",
            Location = new Point(150, 30),
            Size = new Size(160, 35),
            Enabled = false,
            TabIndex = 6,
            AccessibleName = "Import Chart of Accounts Button",
            AccessibleDescription = "Import account structure from QuickBooks"
        };
        _importAccountsButton.Click += async (s, e) => await _viewModel!.ImportAccountsCommand.ExecuteAsync(null);

        _operationsGroup.Controls.AddRange(new Control[] { _syncButton, _importAccountsButton });

        // Status Text Box
        _statusTextBox = new TextBox
        {
            Name = "statusTextBox",
            Location = new Point(20, 280),
            Size = new Size(540, 150),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            TabIndex = 7,
            AccessibleName = "Status Log Text Box",
            AccessibleDescription = "Operation status and log messages"
        };

        // Progress Bar
        _progressBar = new ProgressBar
        {
            Name = "progressBar",
            Location = new Point(20, 440),
            Size = new Size(540, 25),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Visible = false,
            TabIndex = 8,
            AccessibleName = "Operation Progress Bar"
        };

        // Add controls to panel
        Controls.AddRange(new Control[] { _connectionGroup, _operationsGroup, _statusTextBox, _progressBar });

        _logger.LogInformation("QuickBooksPanel UI initialized");
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Initialize ViewModel UI context and refresh status
        var uiContext = System.Threading.SynchronizationContext.Current;
        if (uiContext != null)
        {
            _viewModel?.SetUiContext(uiContext);
        }

        if (_viewModel != null)
        {
            await _viewModel.RefreshConnectionStatusAsync();
        }
    }





    private void UpdateStatusLabelForeColor()
    {
        if (_statusLabel == null) return;
        if (_viewModel?.IsConnected == true)
        {
            _statusLabel.ForeColor = Color.Green;
        }
        else
        {
            _statusLabel.ForeColor = Color.Red;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuickBooksViewModel.IsConnected) || e.PropertyName == nameof(QuickBooksViewModel.ConnectionLabel))
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateStatusLabelForeColor));
            }
            else
            {
                UpdateStatusLabelForeColor();
            }
        }
    }


    /// <summary>
    /// Cleanup panel resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _bindingSource?.Dispose();

            _statusLabel?.Dispose();
            _connectButton?.Dispose();
            _disconnectButton?.Dispose();
            _syncButton?.Dispose();
            _importAccountsButton?.Dispose();
            _statusTextBox?.Dispose();
            _progressBar?.Dispose();
            _connectionGroup?.Dispose();
            _operationsGroup?.Dispose();
        }

        base.Dispose(disposing);
    }
}
