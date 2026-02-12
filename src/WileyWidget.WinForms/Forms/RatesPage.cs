using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.DataGrid.Styles;
using Syncfusion.WinForms.Input.Enums;
using Syncfusion.Windows.Forms;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Forms
{
    // Lightweight expense provider interface (allows DI / testing)
    public interface IExpenseProvider
    {
        Task<decimal> GetAverageMonthlyExpensesAsync(string enterpriseName, CancellationToken ct = default);
    }

    // Default adapter for existing synchronous calculator
    public class DefaultExpenseProvider : IExpenseProvider
    {
        public Task<decimal> GetAverageMonthlyExpensesAsync(string enterpriseName, CancellationToken ct = default)
            => Task.FromResult(WileyWidgetCalculator.GetAverageMonthlyExpenses(enterpriseName));
    }

    // ViewModels
    public class EnterpriseRateViewModel : INotifyPropertyChanged, IEditableObject
    {
        private decimal _currentCharge;
        private decimal _averageExpenses;
        private decimal _similarCommunitiesAverage;
        private bool _inEdit;
        private decimal _backupCurrentCharge;
        private decimal _backupAverageExpenses;
        private decimal _backupSimilarCommunitiesAverage;

        public string EnterpriseName { get; set; }

        public decimal CurrentCharge
        {
            get => _currentCharge;
            set
            {
                _currentCharge = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProfitLoss));
            }
        }

        public decimal AverageExpenses
        {
            get => _averageExpenses;
            set
            {
                _averageExpenses = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProfitLoss));
            }
        }

        public decimal ProfitLoss => CurrentCharge - AverageExpenses;

        public decimal SimilarCommunitiesAverage
        {
            get => _similarCommunitiesAverage;
            set { _similarCommunitiesAverage = value; OnPropertyChanged(); }
        }

        // Support WinForms edit transactions (Esc rollback / commit)
        public void BeginEdit()
        {
            if (_inEdit) return;
            _inEdit = true;
            _backupCurrentCharge = _currentCharge;
            _backupAverageExpenses = _averageExpenses;
            _backupSimilarCommunitiesAverage = _similarCommunitiesAverage;
        }

        public void CancelEdit()
        {
            if (!_inEdit) return;
            _inEdit = false;
            CurrentCharge = _backupCurrentCharge;
            AverageExpenses = _backupAverageExpenses;
            SimilarCommunitiesAverage = _backupSimilarCommunitiesAverage;
        }

        public void EndEdit()
        {
            if (!_inEdit) return;
            _inEdit = false;
            // changes already applied to properties
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));
    }

    public class RatesPageViewModel
    {
        private readonly ObservableCollection<EnterpriseRateViewModel> _enterprises = new();
        public ReadOnlyObservableCollection<EnterpriseRateViewModel> Enterprises { get; }

        private readonly IExpenseProvider _expenseProvider;
        private readonly SynchronizationContext? _syncContext;

        public RatesPageViewModel(IExpenseProvider expenseProvider)
        {
            _expenseProvider = expenseProvider ?? throw new ArgumentNullException(nameof(expenseProvider));
            Enterprises = new ReadOnlyObservableCollection<EnterpriseRateViewModel>(_enterprises);
            _syncContext = SynchronizationContext.Current;
        }

#pragma warning disable CS0067 // Disable unused event warning for INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        public void LoadData()
        {
            _enterprises.Clear();
            _enterprises.Add(new EnterpriseRateViewModel { EnterpriseName = "Water" });
            _enterprises.Add(new EnterpriseRateViewModel { EnterpriseName = "Sewer" });
            _enterprises.Add(new EnterpriseRateViewModel { EnterpriseName = "Trash" });
            _enterprises.Add(new EnterpriseRateViewModel { EnterpriseName = "Apartments" });
            LoadSimilarCommunitiesAverages();
        }

        public async Task RefreshExpensesFromWileyAsync(CancellationToken ct)
        {
            foreach (var enterprise in _enterprises)
            {
                try
                {
                    var value = await _expenseProvider.GetAverageMonthlyExpensesAsync(enterprise.EnterpriseName, ct).ConfigureAwait(false);
                    var local = enterprise;
                    if (_syncContext != null)
                        _syncContext.Post(_ => local.AverageExpenses = value, null);
                    else
                        local.AverageExpenses = value;
                }
                catch
                {
                    var local = enterprise;
                    if (_syncContext != null)
                        _syncContext.Post(_ => local.AverageExpenses = 0m, null);
                    else
                        local.AverageExpenses = 0m;
                }
            }
        }

        public void RefreshColoradoRates()
        {
            LoadSimilarCommunitiesAverages();
        }

        private void LoadSimilarCommunitiesAverages()
        {
            // Latest known statewide residential averages (Dec 2025 Coloradoan – no newer statewide averages published as of Feb 2026)
            var water = _enterprises.FirstOrDefault(e => e.EnterpriseName == "Water");
            if (water != null) water.SimilarCommunitiesAverage = 45.00m;

            var sewer = _enterprises.FirstOrDefault(e => e.EnterpriseName == "Sewer");
            if (sewer != null) sewer.SimilarCommunitiesAverage = 76.00m;

            var trash = _enterprises.FirstOrDefault(e => e.EnterpriseName == "Trash");
            if (trash != null) trash.SimilarCommunitiesAverage = 62.50m;

            var apartments = _enterprises.FirstOrDefault(e => e.EnterpriseName == "Apartments");
            if (apartments != null) apartments.SimilarCommunitiesAverage = 0m; // No reliable multi-family statewide average
        }

        // Allow the UI to request collection changes via the ViewModel (safe marshal to UI thread)
        public void InsertEnterprise(int index, EnterpriseRateViewModel item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            var idx = Math.Max(0, Math.Min(index, _enterprises.Count));
            if (_syncContext != null)
                _syncContext.Post(_ => _enterprises.Insert(idx, item), null);
            else
                _enterprises.Insert(idx, item);
        }

        public void AddEnterprise(EnterpriseRateViewModel item) => InsertEnterprise(_enterprises.Count, item);

        public void RemoveEnterpriseAt(int index)
        {
            if (index < 0) return;
            if (_syncContext != null)
                _syncContext.Post(_ => { if (index < _enterprises.Count) _enterprises.RemoveAt(index); }, null);
            else if (index < _enterprises.Count)
                _enterprises.RemoveAt(index);
        }

        public void RemoveEnterprise(EnterpriseRateViewModel item)
        {
            if (item == null) return;
            if (_syncContext != null)
                _syncContext.Post(_ => _enterprises.Remove(item), null);
            else
                _enterprises.Remove(item);
        }
    }

    // Main Form (RatesPage.cs)
    public partial class RatesPage : SfForm
    {
        private readonly RatesPageViewModel _viewModel;
        private SfDataGrid _sfDataGrid;
        private ToolStrip _toolStrip;
        private ToolStripButton _refreshButton;
        private LinkLabel _sourceLink;
        private BindingSource _bindingSource;

        public RatesPage() : this(new DefaultExpenseProvider()) { }

        public RatesPage(IExpenseProvider expenseProvider)
        {
            _viewModel = new RatesPageViewModel(expenseProvider);
            InitializeComponent();
            WileyWidget.WinForms.Themes.ThemeColors.ApplyTheme(this);
            this.Style.Border = new Pen(SystemColors.WindowFrame, 1);
            this.Style.InactiveBorder = new Pen(SystemColors.GrayText, 1);
            this.Text = "Rates";
            this.Size = new Size(1300, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Normal;
        }

        private void InitializeComponent()
        {
            // SfDataGrid
            _sfDataGrid = new SfDataGrid
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowEditing = true,
                EditMode = EditMode.SingleClick,
                EditorSelectionBehavior = EditorSelectionBehavior.SelectAll,
                NavigationMode = NavigationMode.Cell,
                SelectionMode = GridSelectionMode.Single,
                ShowRowHeader = false, // Plain look – no row index column
                AutoSizeColumnsMode = AutoSizeColumnsMode.Fill // Responsive – columns fill available width, no horizontal scroll needed
            };

            // Force header visibility and height
            _sfDataGrid.HeaderRowHeight = 38;
            _sfDataGrid.Style.HeaderStyle.Font = new GridFontInfo(new Font("Segoe UI", 10F, FontStyle.Bold));
            _sfDataGrid.Style.HeaderStyle.HorizontalAlignment = HorizontalAlignment.Center;

            _sfDataGrid.Style.CellStyle.Font = new GridFontInfo(new Font("Segoe UI", 10F));
            _sfDataGrid.Style.CellStyle.HorizontalAlignment = HorizontalAlignment.Right;

            // Columns
            var enterpriseCol = new GridTextColumn { MappingName = "EnterpriseName", HeaderText = "Enterprise", AllowEditing = false };
            enterpriseCol.CellStyle.HorizontalAlignment = HorizontalAlignment.Left;
            _sfDataGrid.Columns.Add(enterpriseCol);
            _sfDataGrid.Columns.Add(new GridNumericColumn { MappingName = "CurrentCharge", HeaderText = "Current Monthly Charge", Format = "C2" });
            _sfDataGrid.Columns.Add(new GridNumericColumn { MappingName = "AverageExpenses", HeaderText = "Average Monthly Expenses", Format = "C2", AllowEditing = false });
            _sfDataGrid.Columns.Add(new GridNumericColumn { MappingName = "ProfitLoss", HeaderText = "Monthly Profit or Loss (Average)", Format = "C2", AllowEditing = false });
            _sfDataGrid.Columns.Add(new GridNumericColumn { MappingName = "SimilarCommunitiesAverage", HeaderText = "Avg Charge Similar CO Communities (Statewide Proxy)", Format = "C2", AllowEditing = false });

            // Conditional formatting for Profit/Loss
            _sfDataGrid.QueryCellStyle += (s, e) =>
            {
                if (e.Column.MappingName == "ProfitLoss" && e.DataRow?.RowData is EnterpriseRateViewModel vm)
                {
                    if (vm.ProfitLoss < 0) e.Style.TextColor = Color.Red;
                    else if (vm.ProfitLoss > 0) e.Style.TextColor = Color.Green;
                }
            };

            // ToolStrip with Refresh button (professional toolbar look)
            _toolStrip = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, RenderMode = ToolStripRenderMode.System };
            _refreshButton = new ToolStripButton("Refresh Colorado Rates") { Alignment = ToolStripItemAlignment.Left };
            _refreshButton.Click += (s, e) =>
            {
                _viewModel.RefreshColoradoRates();
                MessageBox.Show("Colorado comparison rates refreshed to latest known statewide averages (Dec 2025 – no newer statewide data published as of Feb 2026).", "Refresh Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _toolStrip.Items.Add(_refreshButton);

            // Source Link
            _sourceLink = new LinkLabel
            {
                Text = "Source: Latest statewide averages from Coloradoan (Dec 2025) | Colorado Municipal League State of Our Cities & Towns",
                Dock = DockStyle.Bottom,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                LinkColor = Color.Blue
            };
            _sourceLink.Links.Add(0, _sourceLink.Text.Length, "https://cmlresource.com/state-of-our-cities-and-towns-2025/");
            _sourceLink.LinkClicked += (s, e) =>
            {
                var url = e.Link?.LinkData as string;
                if (!string.IsNullOrEmpty(url))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            };

            // Correct docking order: Fill first, then Top, then Bottom
            this.Controls.Add(_sfDataGrid);
            this.Controls.Add(_toolStrip);
            this.Controls.Add(_sourceLink);

            // Data binding
            _viewModel.LoadData();

            // Create a BindingList adapter and keep it synced both ways with the VM's ReadOnlyObservableCollection
            var bindingList = new BindingList<EnterpriseRateViewModel>(_viewModel.Enterprises.ToList());
            bool updatingFromModel = false;
            bool updatingFromList = false;
            var snapshot = bindingList.ToList();

            if (_viewModel.Enterprises is INotifyCollectionChanged incc)
            {
                incc.CollectionChanged += (sender, ea) =>
                {
                    if (updatingFromList) return;
                    try
                    {
                        updatingFromModel = true;
                        switch (ea.Action)
                        {
                            case NotifyCollectionChangedAction.Add:
                                if (ea.NewItems != null)
                                {
                                    var idx = ea.NewStartingIndex >= 0 ? ea.NewStartingIndex : bindingList.Count;
                                    foreach (EnterpriseRateViewModel it in ea.NewItems)
                                    {
                                        bindingList.Insert(idx++, it);
                                    }
                                }
                                break;
                            case NotifyCollectionChangedAction.Remove:
                                if (ea.OldItems != null)
                                {
                                    foreach (EnterpriseRateViewModel it in ea.OldItems)
                                        bindingList.Remove(it);
                                }
                                break;
                            case NotifyCollectionChangedAction.Reset:
                                bindingList.Clear();
                                foreach (var it in _viewModel.Enterprises) bindingList.Add(it);
                                break;
                            case NotifyCollectionChangedAction.Replace:
                                if (ea.NewItems != null && ea.OldStartingIndex >= 0)
                                {
                                    var idx = ea.OldStartingIndex;
                                    for (int i = 0; i < ea.NewItems.Count; i++)
                                    {
                                        if (ea.NewItems[i] is EnterpriseRateViewModel newItem)
                                            bindingList[idx + i] = newItem;
                                    }
                                }
                                break;
                        }
                    }
                    finally
                    {
                        updatingFromModel = false;
                        snapshot = bindingList.ToList();
                    }
                };
            }

            bindingList.ListChanged += (sender, ea) =>
            {
                if (updatingFromModel) return;
                try
                {
                    updatingFromList = true;
                    if (ea.ListChangedType == ListChangedType.ItemAdded)
                    {
                        var item = bindingList[ea.NewIndex];
                        _viewModel.InsertEnterprise(ea.NewIndex, item);
                    }
                    else if (ea.ListChangedType == ListChangedType.ItemDeleted)
                    {
                        // Determine removed item by comparing with snapshot
                        var removed = snapshot.Except(bindingList).FirstOrDefault();
                        if (removed != null)
                            _viewModel.RemoveEnterprise(removed);
                        else if (ea.OldIndex >= 0)
                            _viewModel.RemoveEnterpriseAt(ea.OldIndex);
                    }
                    else if (ea.ListChangedType == ListChangedType.Reset)
                    {
                        // Not attempting full reconcile here; for complex scenarios replace with a proper diff.
                    }
                }
                finally
                {
                    updatingFromList = false;
                    snapshot = bindingList.ToList();
                }
            };

            _bindingSource = new BindingSource { DataSource = bindingList };
            _sfDataGrid.DataSource = _bindingSource;

            // Show busy indicator while fetching async expenses (Shown ensures the UI is ready)
            this.Shown += async (s, e) =>
            {
                try
                {
                    _sfDataGrid.ShowBusyIndicator = true;
                    await _viewModel.RefreshExpensesFromWileyAsync(CancellationToken.None);
                }
                finally
                {
                    _sfDataGrid.ShowBusyIndicator = false;
                }
            };

            // Enable sorting at grid level by default (column AllowSorting can override)
            _sfDataGrid.AllowSorting = true;
        }
    }

    // Placeholder for your existing calculator
    public static class WileyWidgetCalculator
    {
        public static decimal GetAverageMonthlyExpenses(string enterpriseName)
        {
            return 0m; // Replace with real implementation
        }
    }
}
