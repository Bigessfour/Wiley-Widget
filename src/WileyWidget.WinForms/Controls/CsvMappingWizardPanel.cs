using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;
using CheckBoxAdv = Syncfusion.Windows.Forms.Tools.CheckBoxAdv;
using Microsoft.Extensions.Logging;
using CsvHelper;
using CsvHelper.Configuration;

namespace WileyWidget.WinForms.Controls
{
    public sealed class MappingAppliedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public Dictionary<string, string> ColumnMap { get; }
        public string? SelectedEntity { get; }
        public int FiscalYear { get; }

        public MappingAppliedEventArgs(string filePath, Dictionary<string, string> columnMap, string? selectedEntity, int fiscalYear)
        {
            FilePath = filePath;
            ColumnMap = columnMap;
            SelectedEntity = selectedEntity;
            FiscalYear = fiscalYear;
        }
    }

    public partial class CsvMappingWizardPanel : UserControl, ICompletablePanel
    {
        // ICompletablePanel backing fields
        private ErrorProvider? _errorProvider;
        private ToolTip? _toolTip;
        private CancellationTokenSource? _currentOperationCts = null;
        private bool _isLoaded;
        private bool _hasUnsavedChanges;
        private List<ValidationItem>? _validationErrors;
        private PanelMode? _mode = PanelMode.View;

        // Stored event delegates for safe unsubscribe in Dispose
        private EventHandler? _btnApplyClickHandler;
        private EventHandler? _btnCancelClickHandler;
        private EventHandler? _comboChangedHandler;
        private EventHandler? _chkHasHeaderChangedHandler;
        private readonly ILogger? _logger;
        private readonly DataGridView _previewGrid;
        private readonly ComboBox _cbAccount;
        private readonly ComboBox _cbDescription;
        private readonly ComboBox _cbBudgeted;
        private readonly ComboBox _cbActual;
        private readonly ComboBox _cbFiscalYear;
        private readonly ComboBox _cbEntity;
        private readonly SfButton _btnApply;
        private readonly SfButton _btnCancel;
        private readonly CheckBoxAdv _chkHasHeader;

        private string _filePath = string.Empty;
        private List<string> _headers = new();
        private List<string[]> _previewRows = new();

        public event EventHandler<MappingAppliedEventArgs>? MappingApplied;
        public event EventHandler? Cancelled;

        public CsvMappingWizardPanel(ILogger? logger = null)
        {
            _logger = logger;
            // Build a lightweight UI programmatically so no designer changes required
            Dock = DockStyle.Fill;

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            _previewGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            main.Controls.Add(_previewGrid, 0, 0);
            main.SetRowSpan(_previewGrid, 3);

            var rightPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(8), AutoScroll = true };

            _chkHasHeader = new CheckBoxAdv { Text = "First row contains headers", Checked = true, AutoSize = true };
            rightPanel.Controls.Add(_chkHasHeader);

            rightPanel.Controls.Add(new Label { Text = "Map Account Number", AutoSize = true });
            _cbAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 }; rightPanel.Controls.Add(_cbAccount);

            rightPanel.Controls.Add(new Label { Text = "Map Description", AutoSize = true });
            _cbDescription = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 }; rightPanel.Controls.Add(_cbDescription);

            rightPanel.Controls.Add(new Label { Text = "Map Budgeted Amount", AutoSize = true });
            _cbBudgeted = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 }; rightPanel.Controls.Add(_cbBudgeted);

            rightPanel.Controls.Add(new Label { Text = "Map Actual Amount", AutoSize = true });
            _cbActual = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 }; rightPanel.Controls.Add(_cbActual);

            rightPanel.Controls.Add(new Label { Text = "Fiscal Year (override)", AutoSize = true });
            _cbFiscalYear = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 }; rightPanel.Controls.Add(_cbFiscalYear);

            rightPanel.Controls.Add(new Label { Text = "Bulk assign Entity/Fund", AutoSize = true });
            _cbEntity = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 }; rightPanel.Controls.Add(_cbEntity);

            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Height = 40 };
            _btnApply = new SfButton { Text = "Import", Width = 90 };
            btnPanel.Controls.Add(_btnApply);
            _btnCancel = new SfButton { Text = "Cancel", Width = 90 };
            btnPanel.Controls.Add(_btnCancel);

            var rightContainer = new Panel { Dock = DockStyle.Fill }; rightContainer.Controls.Add(rightPanel); rightContainer.Controls.Add(btnPanel);
            main.Controls.Add(rightContainer, 1, 0);

            Controls.Add(main);

            this.PerformLayout();
            this.Refresh();

            _logger?.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);

            // Accessibility, tooltips and ErrorProvider
            _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            _toolTip = new ToolTip();

            _previewGrid.AccessibleName = "CSV Preview";
            _previewGrid.AccessibleDescription = "Preview of CSV rows";
            _previewGrid.TabIndex = 0;

            _chkHasHeader.AccessibleName = "Has Header";
            _chkHasHeader.TabIndex = 1;
            _toolTip.SetToolTip(_chkHasHeader, "Toggle whether the first row contains column headers");

            _cbAccount.AccessibleName = "Account Mapping";
            _cbAccount.TabIndex = 2;
            _cbDescription.AccessibleName = "Description Mapping";
            _cbDescription.TabIndex = 3;
            _cbBudgeted.AccessibleName = "Budgeted Amount Mapping";
            _cbBudgeted.TabIndex = 4;
            _cbActual.AccessibleName = "Actual Amount Mapping";
            _cbActual.TabIndex = 5;
            _cbFiscalYear.AccessibleName = "Fiscal Year Override";
            _cbFiscalYear.TabIndex = 6;
            _cbEntity.AccessibleName = "Entity Mapping";
            _cbEntity.TabIndex = 7;

            _btnApply.TabIndex = 8;
            _btnCancel.TabIndex = 9;

            // Wire stored event handlers for safe unsubscribe
            _btnApplyClickHandler = BtnApply_Click;
            _btnApply.Click += _btnApplyClickHandler;

            _btnCancelClickHandler = BtnCancel_Click;
            _btnCancel.Click += _btnCancelClickHandler;

            _comboChangedHandler = (s, e) => SetHasUnsavedChanges(true);
            _cbAccount.SelectedIndexChanged += _comboChangedHandler;
            _cbDescription.SelectedIndexChanged += _comboChangedHandler;
            _cbBudgeted.SelectedIndexChanged += _comboChangedHandler;
            _cbActual.SelectedIndexChanged += _comboChangedHandler;
            _cbFiscalYear.SelectedIndexChanged += _comboChangedHandler;
            _cbEntity.SelectedIndexChanged += _comboChangedHandler;

            // Header toggle should reload preview and mark dirty (store delegate for unsubscribe)
            _chkHasHeaderChangedHandler = (s, e) => { LoadPreviewAndHeaders(); SetHasUnsavedChanges(true); };
            _chkHasHeader.CheckedChanged += _chkHasHeaderChangedHandler;

            // Apply current theme to dynamic controls (no hard-coded theme names)
            try
            {
                var theme = SfSkinManager.ApplicationVisualTheme;
                if (!string.IsNullOrWhiteSpace(theme))
                {
                    SfSkinManager.SetVisualStyle(this, theme);
                    SfSkinManager.SetVisualStyle(_btnApply, theme);
                    SfSkinManager.SetVisualStyle(_btnCancel, theme);
                    SfSkinManager.SetVisualStyle(_previewGrid, theme);
                }
            }
            catch
            {
                // Non-fatal: theming will still be applied by form-level SFSkinManager
            }
        }

        public void Initialize(string filePath, IEnumerable<string>? availableEntities = null, int defaultFiscalYear = 2025)
        {
            _filePath = filePath ?? string.Empty;

            _cbEntity.Items.Clear();
            if (availableEntities != null)
            {
                foreach (var e in availableEntities.Where(x => !string.IsNullOrWhiteSpace(x)))
                    _cbEntity.Items.Add(e.Trim());
            }
            if (_cbEntity.Items.Count == 0)
                _cbEntity.Items.Add("(None)");
            _cbEntity.SelectedIndex = 0;

            _cbFiscalYear.Items.Clear();
            for (int y = defaultFiscalYear - 2; y <= defaultFiscalYear + 2; y++)
                _cbFiscalYear.Items.Add(y);
            _cbFiscalYear.SelectedItem = defaultFiscalYear;

            LoadPreviewAndHeaders();
            _isLoaded = true;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void LoadPreviewAndHeaders()
        {
            _headers.Clear();
            _previewRows.Clear();

            try
            {
                using var reader = new StreamReader(_filePath);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = _chkHasHeader.Checked,
                    MissingFieldFound = null,
                    HeaderValidated = null,
                    TrimOptions = TrimOptions.Trim,
                    BadDataFound = null
                };

                using var csv = new CsvReader(reader, config);

                if (config.HasHeaderRecord)
                {
                    csv.Read();
                    csv.ReadHeader();
                    _headers = csv.HeaderRecord?.ToList() ?? new List<string>();
                }

                int rowCount = 0;
                while (csv.Read() && rowCount < 10)
                {
                    var row = new List<string>();
                    for (int i = 0; i < (csv.HeaderRecord?.Length ?? csv.Parser.Count); i++)
                    {
                        try { row.Add(csv.GetField(i) ?? string.Empty); } catch { row.Add(string.Empty); }
                    }
                    _previewRows.Add(row.ToArray());
                    rowCount++;
                }

                if (_headers.Count == 0 && _previewRows.Any())
                {
                    var cols = _previewRows.First().Length;
                    for (int i = 0; i < cols; i++) _headers.Add($"Column{i}");
                }
            }
            catch
            {
                // Best-effort fallback: read a few lines and split by comma
                try
                {
                    var lines = File.ReadLines(_filePath).Take(11).ToArray();
                    if (lines.Length > 0)
                    {
                        var first = lines[0].Split(',');
                        _headers = first.Select((h, i) => string.IsNullOrWhiteSpace(h) ? $"Column{i}" : h.Trim()).ToList();
                        foreach (var line in lines.Skip(1).Take(10))
                            _previewRows.Add(line.Split(','));
                    }
                }
                catch
                {
                    // swallow - preview will be empty
                }
            }

            FillPreviewGridAndCombos();
        }

        private void FillPreviewGridAndCombos()
        {
            _previewGrid.Columns.Clear();
            var indexToName = _headers.Select((h, i) => (Index: i, Name: string.IsNullOrWhiteSpace(h) ? $"Column{i}" : h)).ToList();
            foreach (var col in indexToName)
            {
                _previewGrid.Columns.Add($"c{col.Index}", col.Name);
            }

            _previewGrid.Rows.Clear();
            foreach (var r in _previewRows)
            {
                var cells = r.Select(c => c ?? string.Empty).ToArray();
                // Ensure length matches columns
                if (cells.Length < _previewGrid.Columns.Count)
                {
                    var extended = new string[_previewGrid.Columns.Count];
                    Array.Copy(cells, extended, cells.Length);
                    for (int i = cells.Length; i < extended.Length; i++) extended[i] = string.Empty;
                    cells = extended;
                }
                _previewGrid.Rows.Add(cells);
            }

            // Fill combobox options
            var options = indexToName.Select(x => x.Name).ToList();
            options.AddRange(indexToName.Select(x => $"Column{x.Index}"));

            void PopulateCombo(ComboBox cb)
            {
                cb.Items.Clear();
                foreach (var o in options) cb.Items.Add(o);
                if (cb.Items.Count > 0) cb.SelectedIndex = 0;
            }

            PopulateCombo(_cbAccount);
            PopulateCombo(_cbDescription);
            PopulateCombo(_cbBudgeted);
            PopulateCombo(_cbActual);
        }

        private void OnMappingChanged(object? sender, EventArgs e) => SetHasUnsavedChanges(true);

        private async void BtnApply_Click(object? sender, EventArgs e)
        {
            var validation = await ValidateAsync(CancellationToken.None);
            if (!validation.IsValid)
            {
                _logger?.LogInformation("CSV mapping validation failed");
                return;
            }

            await SaveAsync(CancellationToken.None);
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        // ICompletablePanel implementation
        public bool IsLoaded => _isLoaded;

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsBusy
        {
            get => field;
            set
            {
                if (field == value) return;
                field = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool HasUnsavedChanges => _hasUnsavedChanges;

        public bool IsValid => _validationErrors == null || _validationErrors.Count == 0;

        public IReadOnlyList<ValidationItem> ValidationErrors => _validationErrors != null ? (IReadOnlyList<ValidationItem>)_validationErrors : Array.Empty<ValidationItem>();

        public PanelMode? Mode => _mode;

        public CancellationTokenSource? CurrentOperationCts => _currentOperationCts;

        public event EventHandler? StateChanged;

        public Task<ValidationResult> ValidateAsync(CancellationToken ct)
        {
            _errorProvider?.Clear();
            var errors = new List<ValidationItem>();

            if (string.IsNullOrWhiteSpace(_cbAccount.SelectedItem?.ToString()))
            {
                errors.Add(new ValidationItem("AccountNumber", "Account mapping is required", ValidationSeverity.Error, _cbAccount));
                _errorProvider?.SetError(_cbAccount, "Account mapping is required");
            }

            if (string.IsNullOrWhiteSpace(_cbBudgeted.SelectedItem?.ToString()) && string.IsNullOrWhiteSpace(_cbActual.SelectedItem?.ToString()))
            {
                errors.Add(new ValidationItem("AmountMapping", "At least one of Budgeted or Actual must be mapped", ValidationSeverity.Error, _cbBudgeted));
                _errorProvider?.SetError(_cbBudgeted, "Map Budgeted or Actual amount");
            }

            _validationErrors = errors;
            return Task.FromResult(errors.Count == 0 ? ValidationResult.Success : ValidationResult.Failed(errors.ToArray()));
        }

        public Task SaveAsync(CancellationToken ct)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AccountNumber"] = _cbAccount.SelectedItem?.ToString() ?? string.Empty,
                ["Description"] = _cbDescription.SelectedItem?.ToString() ?? string.Empty,
                ["BudgetedAmount"] = _cbBudgeted.SelectedItem?.ToString() ?? string.Empty,
                ["ActualAmount"] = _cbActual.SelectedItem?.ToString() ?? string.Empty
            };

            var selectedEntity = _cbEntity.SelectedItem?.ToString();
            var fy = 2025;
            if (_cbFiscalYear.SelectedItem != null && int.TryParse(_cbFiscalYear.SelectedItem.ToString(), out var y)) fy = y;

            MappingApplied?.Invoke(this, new MappingAppliedEventArgs(_filePath, map, selectedEntity, fy));
            SetHasUnsavedChanges(false);
            return Task.CompletedTask;
        }

        public Task LoadAsync(CancellationToken ct) => Task.CompletedTask;

        public void FocusFirstError()
        {
            if (_validationErrors != null && _validationErrors.Count > 0)
            {
                _validationErrors[0].ControlRef?.Focus();
            }
        }

        private void SetHasUnsavedChanges(bool value)
        {
            if (_hasUnsavedChanges == value) return;
            _hasUnsavedChanges = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (_btnApply != null && _btnApplyClickHandler != null) _btnApply.Click -= _btnApplyClickHandler;
                    if (_btnCancel != null && _btnCancelClickHandler != null) _btnCancel.Click -= _btnCancelClickHandler;
                    if (_cbAccount != null && _comboChangedHandler != null) _cbAccount.SelectedIndexChanged -= _comboChangedHandler;
                    if (_cbDescription != null && _comboChangedHandler != null) _cbDescription.SelectedIndexChanged -= _comboChangedHandler;
                    if (_cbBudgeted != null && _comboChangedHandler != null) _cbBudgeted.SelectedIndexChanged -= _comboChangedHandler;
                    if (_cbActual != null && _comboChangedHandler != null) _cbActual.SelectedIndexChanged -= _comboChangedHandler;
                    if (_cbFiscalYear != null && _comboChangedHandler != null) _cbFiscalYear.SelectedIndexChanged -= _comboChangedHandler;
                    if (_cbEntity != null && _comboChangedHandler != null) _cbEntity.SelectedIndexChanged -= _comboChangedHandler;
                    if (_chkHasHeader != null && _chkHasHeaderChangedHandler != null) _chkHasHeader.CheckedChanged -= _chkHasHeaderChangedHandler;
                }
                catch { }

                _errorProvider?.Dispose();
                _toolTip?.Dispose();
                _previewGrid?.Dispose();
                _btnApply?.Dispose();
                _btnCancel?.Dispose();
                _chkHasHeader?.Dispose();
                _cbAccount?.Dispose();
                _cbDescription?.Dispose();
                _cbBudgeted?.Dispose();
                _cbActual?.Dispose();
                _cbFiscalYear?.Dispose();
                _cbEntity?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
