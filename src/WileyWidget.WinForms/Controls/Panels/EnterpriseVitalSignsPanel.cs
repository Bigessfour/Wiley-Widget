// ──────────────────────────────────────────────────────────────────────────────────
// EnterpriseVitalSignsPanel — Sacred Panel Skeleton v2026
// Wiley Widget Municipal Finance · WileyWidgetUIStandards §1 compliant
// ──────────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Utilities;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls.Panels
{
    /// <summary>
    /// Enterprise Vital Signs panel — displays enterprise financial snapshots with
    /// radial gauges and comparative charts.  Implements the Sacred Panel Skeleton
    /// (WileyWidgetUIStandards §1): constructor takes pre-built ViewModel + factory,
    /// all Syncfusion controls created via <see cref="SyncfusionControlFactory"/>.
    /// </summary>
    public partial class EnterpriseVitalSignsPanel : ScopedPanelBase<EnterpriseVitalSignsViewModel>, ICompletablePanel
    {
        private const string RateStudySourceUrl = "https://trace.tennessee.edu/utk_mtaspubs/164";
        private const string RateStudyFootnoteText = "Configured according to the University of Tennessee MTAS guide \"How Any City Can Conduct a Utility Rate Study and Successfully Increase Rates\" (2012). Source: https://trace.tennessee.edu/utk_mtaspubs/164";

        // ── Sacred Panel Skeleton mandatory fields ───────────────────────────────
        private readonly EnterpriseVitalSignsViewModel _vm;
        private readonly SyncfusionControlFactory _factory;
        private PanelHeader _header = null!;
        private TableLayoutPanel _content = null!;
        private LoadingOverlay _loader = null!;

        // ── Panel-specific fields ────────────────────────────────────────────────
        private TabControlAdv _enterpriseTabs = null!;
        private Label _statusLabel = null!;
        private Label _studyFootnoteLabel = null!;
        private NoDataOverlay _noDataOverlay = null!;
        private ToolTip? _toolTip;
        private string? _lastErrorMessage;
        private bool _loadTriggered;
        private int _refreshAllVisualsQueued;
        private NotifyCollectionChangedEventHandler? _snapshotsChangedHandler;
        private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;

        /// <summary>
        /// Sacred Panel Skeleton constructor: ViewModel and factory are injected directly
        /// by the DI container — no <see cref="IServiceScopeFactory"/> required.
        /// </summary>
        public EnterpriseVitalSignsPanel(
            EnterpriseVitalSignsViewModel vm,
            SyncfusionControlFactory factory)
            : base(vm, Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<EnterpriseVitalSignsPanel>>(Program.Services))
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));

            SafeSuspendAndLayout(InitializeLayout);
            BindViewModel();
            Load += EnterpriseVitalSignsPanel_Load;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            MinimumSize = RecommendedDockedPanelMinimumLogicalSize;
            PerformLayout();
        }

        private void InitializeLayout()
        {
            var headerHeight = Math.Max(
                LayoutTokens.GetScaled(LayoutTokens.HeaderHeightLarge),
                LayoutTokens.GetScaled(LayoutTokens.HeaderMinimumHeight));

            // ── Sacred Panel Skeleton layout properties ──────────────────────
            Dock = DockStyle.Fill;
            MinimumSize = new Size(
                RecommendedDockedPanelMinimumLogicalWidth,
                RecommendedDockedPanelMinimumLogicalHeight);
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(0);

            // ── Header ──────────────────────────────────────────────────────
            _header = _factory.CreatePanelHeader(header =>
            {
                header.Dock = DockStyle.Fill;
                header.MinimumSize = new Size(0, headerHeight);
                header.Height = headerHeight;
                header.Margin = Padding.Empty;
                header.Title = "Enterprise Vital Signs — FY 2026";
                header.AccessibleName = "Enterprise vital signs header";
                header.ShowHelpButton = false;
                header.ShowPinButton = false;
                header.ShowCloseButton = true;
                header.ShowRefreshButton = true;
            });
            _header.RefreshClicked += (s, e) => _vm.RefreshCommand.Execute(null);
            _header.CloseClicked += (s, e) => ClosePanel();
            _toolTip = _factory.CreateToolTip();
            _toolTip.SetToolTip(_header, "Refresh enterprise metrics or close this panel.");

            // ── Root content (header / body / status / footnote) ─────────────
            _content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Margin = Padding.Empty,
                Padding = LayoutTokens.GetScaled(LayoutTokens.PanelPaddingCompact)
            };
            _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _content.RowStyles.Add(new RowStyle(SizeType.Absolute, headerHeight));
            _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _content.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutTokens.GetScaled(32)));
            _content.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutTokens.GetScaled(56)));
            _content.Controls.Add(_header, 0, 0);

            // ── Enterprise tabs ─────────────────────────────────────────────
            _enterpriseTabs = _factory.CreateTabControlAdv(tabControl =>
            {
                tabControl.Dock = DockStyle.Fill;
                tabControl.Multiline = false;
                tabControl.Margin = Padding.Empty;
                tabControl.ItemSize = new Size(
                    LayoutTokens.GetScaled(LayoutTokens.TabItemSizeTall).Width,
                    Math.Max(LayoutTokens.GetScaled(LayoutTokens.TabItemSizeTall).Height, 44));
                tabControl.Alignment = TabAlignment.Top;
                tabControl.Padding = new Point(18, 10);
                tabControl.AccessibleName = "Enterprise tabs";
                tabControl.AccessibleDescription = "Separate tabs for each enterprise financial view";
            });
            _toolTip.SetToolTip(_enterpriseTabs, "Switch between enterprise-specific financial analysis tabs.");

            // ── Content host ─────────────────────────────────────────────────
            var contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoScroll = false
            };
            contentHost.Controls.Add(_enterpriseTabs);

            // ── Overlays (via factory) ───────────────────────────────────────
            _noDataOverlay = _factory.CreateNoDataOverlay(overlay =>
            {
                overlay.Message = "No enterprise vital signs available\r\nImport or sync budget data to populate.";
                overlay.Dock = DockStyle.Fill;
                overlay.Visible = false;
                overlay.AccessibleName = "No enterprise data overlay";
            });
            contentHost.Controls.Add(_noDataOverlay);
            _noDataOverlay.BringToFront();

            _loader = _factory.CreateLoadingOverlay();
            contentHost.Controls.Add(_loader);
            _loader.BringToFront();

            _content.Controls.Add(contentHost, 0, 1);

            // ── Status bar ───────────────────────────────────────────────────
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0),
                Text = "Ready",
                AccessibleName = "Enterprise vital signs status"
            };
            _content.Controls.Add(_statusLabel, 0, 2);

            _studyFootnoteLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 2, 4, 0),
                Text = RateStudyFootnoteText,
                AccessibleName = "Enterprise vital signs study footnote",
                AutoEllipsis = true,
                Font = new Font(Font.FontFamily, 8f, FontStyle.Regular)
            };
            _toolTip.SetToolTip(_studyFootnoteLabel, RateStudySourceUrl);
            _content.Controls.Add(_studyFootnoteLabel, 0, 3);

            // ── Wire to panel ─────────────────────────────────────────────────
            Controls.Add(_content);
        }

        // RefreshClicked and CloseClicked are now wired inline in InitializeLayout.

        private void BindViewModel()
        {
            _snapshotsChangedHandler = (_, _) =>
            {
                QueueRefreshAllVisuals();
            };
            _vm.EnterpriseSnapshots.CollectionChanged += _snapshotsChangedHandler;

            _viewModelPropertyChangedHandler = OnViewModelPropertyChanged;
            _vm.PropertyChanged += _viewModelPropertyChangedHandler;

            QueueRefreshAllVisuals();
            UpdateLoadingState();
        }

        private void QueueRefreshAllVisuals()
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            if (Interlocked.Exchange(ref _refreshAllVisualsQueued, 1) == 1)
            {
                return;
            }

            void RefreshQueued()
            {
                Interlocked.Exchange(ref _refreshAllVisualsQueued, 0);

                if (IsDisposed || Disposing)
                {
                    return;
                }

                RefreshAllVisuals();
            }

            if (IsHandleCreated)
            {
                BeginInvoke((System.Action)RefreshQueued);
                return;
            }

            HandleCreated += HandleCreatedRefreshOnce;

            void HandleCreatedRefreshOnce(object? sender, EventArgs args)
            {
                HandleCreated -= HandleCreatedRefreshOnce;
                if (!IsDisposed)
                {
                    BeginInvoke((System.Action)RefreshQueued);
                }
            }
        }

        private async void EnterpriseVitalSignsPanel_Load(object? sender, EventArgs e)
        {
            if (_loadTriggered) return;
            _loadTriggered = true;

            try
            {
                await LoadAsync();
                Logger.LogDebug("[{Panel}] Initial load completed. Snapshots: {Count}",
                    GetType().Name, _vm.EnterpriseSnapshots.Count);
            }
            catch (Exception ex)
            {
                _loadTriggered = false;
                Logger.LogError(ex, "[{Panel}] Initial load failed", GetType().Name);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(EnterpriseVitalSignsViewModel.IsLoading)
                                or nameof(EnterpriseVitalSignsViewModel.ErrorMessage))
            {
                if (IsHandleCreated && InvokeRequired)
                    BeginInvoke((System.Action)UpdateLoadingState);
                else
                    UpdateLoadingState();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);

            if (Visible && !_loadTriggered && _vm.EnterpriseSnapshots.Count == 0)
            {
                _loadTriggered = true;
                _ = LoadAsync();
            }
        }

        private void UpdateLoadingState()
        {
            _header.IsLoading = _vm.IsLoading;
            _loader.Visible = _vm.IsLoading;
            if (_vm.IsLoading)
            {
                _loader.BringToFront();
                SetStatusMessage("Loading enterprise vital signs…");
            }

            var showNoData = !_vm.IsLoading && _vm.EnterpriseSnapshots.Count == 0;
            _noDataOverlay.Visible = showNoData;
            if (showNoData) _noDataOverlay.BringToFront();

            if (!string.IsNullOrWhiteSpace(_vm.ErrorMessage) &&
                !string.Equals(_lastErrorMessage, _vm.ErrorMessage, StringComparison.Ordinal))
            {
                _lastErrorMessage = _vm.ErrorMessage;
                SetStatusMessage($"Data update issue: {_vm.ErrorMessage}");
            }
        }

        private void RefreshAllVisuals()
        {
            _enterpriseTabs.TabPages.Clear();

            if (_vm.EnterpriseSnapshots.Count == 0)
            {
                UpdateLoadingState();
                SetStatusMessage("No enterprise data available.");
                return;
            }

            int index = 0;
            foreach (var snapshot in _vm.EnterpriseSnapshots)
            {
                var chartPanel = new Panel()
                {
                    Dock = DockStyle.Fill,
                    BorderStyle = BorderStyle.None,
                    Padding = LayoutTokens.GetScaled(LayoutTokens.PanelPaddingCompact),
                    Margin = Padding.Empty,
                    AutoScroll = true
                };

                var card = _factory.CreateEnterpriseFinancialCard(snapshot);
                card.Dock = DockStyle.Top;
                card.AutoSize = true;
                chartPanel.Controls.Add(card);

                var page = _factory.CreateTabPageAdv(snapshot.Name, chartPanel, tabPage =>
                {
                    tabPage.AccessibleName = $"{snapshot.Name} enterprise tab";
                    tabPage.AccessibleDescription = $"Detailed financial tab for {snapshot.Name}";
                });

                _enterpriseTabs.TabPages.Add(page);
                index++;
            }

            if (_enterpriseTabs.TabPages.Count > 0)
            {
                _enterpriseTabs.SelectedTab = _enterpriseTabs.TabPages[0];
            }

            UpdateLoadingState();

            Logger.LogDebug("[{Panel}] Refreshed visuals. Tabs: {TabCount}",
                GetType().Name, _enterpriseTabs.TabPages.Count);

            SetStatusMessage(_vm.EnterpriseSnapshots.Count == 1
                ? "Showing 1 enterprise snapshot."
                : $"Showing {_vm.EnterpriseSnapshots.Count} enterprise snapshots.");
        }

        private void SetStatusMessage(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = message;
            }
        }

        public bool IsComplete => _vm.EnterpriseSnapshots.Count > 0;

        public string CompletionStatus => $"Loaded {_vm.EnterpriseSnapshots.Count} enterprises — ready for council";

        public override async Task LoadAsync(CancellationToken ct = default)
        {
            try
            {
                _loader.Visible = true;
                _loader.BringToFront();
                await _vm.OnVisibilityChangedAsync(true);
            }
            finally
            {
                _loader.Visible = false;
            }
        }

        public override Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;

        public override Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
            => Task.FromResult(ValidationResult.Success);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Load -= EnterpriseVitalSignsPanel_Load;

                if (_snapshotsChangedHandler != null)
                    _vm.EnterpriseSnapshots.CollectionChanged -= _snapshotsChangedHandler;

                if (_viewModelPropertyChangedHandler != null)
                    _vm.PropertyChanged -= _viewModelPropertyChangedHandler;

                _toolTip?.Dispose();
            }

            base.Dispose(disposing);
        }

        public override void FocusFirstError() { }
    }
}
