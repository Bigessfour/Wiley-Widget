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
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.ListView;
using CheckBoxAdv = Syncfusion.Windows.Forms.Tools.CheckBoxAdv;
using Microsoft.Extensions.Logging;
using CsvHelper;
using CsvHelper.Configuration;

using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Utilities;

namespace WileyWidget.WinForms.Controls.Supporting
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
        private string? _mode = "View";

        // Stored event delegates for safe unsubscribe in Dispose
        private EventHandler? _btnApplyClickHandler;
        private EventHandler? _btnCancelClickHandler;
        private EventHandler? _comboChangedHandler;
        private EventHandler? _chkHasHeaderChangedHandler;

        public CsvMappingWizardPanel()
        {
            // Apply Syncfusion theme to panel
            try
            {
                var theme = Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, theme);
            }
            catch { /* Theme application is best-effort */ }
        }
        private readonly ILogger? _logger;
        private readonly SyncfusionControlFactory? _factory;
        private PanelHeader? _panelHeader;
        private SfDataGrid _previewGrid = null!;
        private SfComboBox _cbAccount = null!;
        private SfComboBox _cbDescription = null!;
        private SfComboBox _cbBudgeted = null!;
        private SfComboBox _cbActual = null!;
        private SfComboBox _cbFiscalYear = null!;
        private SfComboBox _cbEntity = null!;
        private SfButton _btnApply = null!;
        private SfButton _btnCancel = null!;
        private CheckBoxAdv _chkHasHeader = null!;

        private string _filePath = string.Empty;
        private List<string> _headers = new();
        private List<string[]> _previewRows = new();

        public event EventHandler<MappingAppliedEventArgs>? MappingApplied;
        public event EventHandler? Cancelled;

        public CsvMappingWizardPanel(ILogger? logger = null, SyncfusionControlFactory? factory = null)
        {
            _logger = logger;
            _factory = factory;
            InitializeControls();
            WireEvents();

            // Accessibility, tooltips and ErrorProvider
            _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            _toolTip = new ToolTip();

            _previewGrid.AccessibleName = "CSV Preview";
            _previewGrid.TabIndex = 0;

            _chkHasHeader.AccessibleName = "Has Header";
            _chkHasHeader.TabIndex = 1;
            _toolTip.SetToolTip(_chkHasHeader, "Toggle whether the first row contains column headers");

            _cbAccount.TabIndex = 2;
            _cbDescription.TabIndex = 3;
            _cbBudgeted.TabIndex = 4;
            _cbActual.TabIndex = 5;
            _cbFiscalYear.TabIndex = 6;
            _cbEntity.TabIndex = 7;

            _btnApply.TabIndex = 8;
            _btnCancel.TabIndex = 9;
        }

        private void InitializeControls()
        {
            this.SuspendLayout();
            Dock = DockStyle.Fill;
            Name = "CsvMappingWizardPanel";
            MinimumSize = LayoutTokens.GetScaled(LayoutTokens.StandardPanelMinimumSize);

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                AutoSize = false,
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, LayoutTokens.GetScaled(LayoutTokens.HeaderHeightLarge)));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _panelHeader = _factory?.CreatePanelHeader(header =>
            {
                header.Title = "Data Mapper";
                header.Dock = DockStyle.Fill;
                header.Height = LayoutTokens.GetScaled(LayoutTokens.HeaderHeightLarge);
                header.MinimumSize = new Size(0, LayoutTokens.GetScaled(LayoutTokens.HeaderHeightLarge));
                header.AccessibleName = "Data Mapper Header";
                header.AccessibleDescription = "Header for the data mapper panel";
            }) ?? new PanelHeader
            {
                Title = "Data Mapper",
                Dock = DockStyle.Fill,
                Height = LayoutTokens.GetScaled(LayoutTokens.HeaderHeightLarge),
                MinimumSize = new Size(0, LayoutTokens.GetScaled(LayoutTokens.HeaderHeightLarge)),
                AccessibleName = "Data Mapper Header",
                AccessibleDescription = "Header for the data mapper panel",
            };
            rootLayout.Controls.Add(_panelHeader, 0, 0);

            var rightColumnWidth = LayoutTokens.GetScaled(360);
            var mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = LayoutTokens.GetScaled(LayoutTokens.PanelPaddingCompact),
                Margin = Padding.Empty
            };
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, rightColumnWidth));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _previewGrid = (_factory?.CreateSfDataGrid(grid =>
            {
                grid.Dock = DockStyle.Fill;
                grid.AllowEditing = false;
                grid.AutoGenerateColumns = true;
                grid.ShowRowHeader = false;
                grid.RowHeight = LayoutTokens.GetScaled(LayoutTokens.GridRowHeightMedium);
                grid.HeaderRowHeight = LayoutTokens.GetScaled(LayoutTokens.GridHeaderRowHeightMedium);
                grid.AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill;
            }) ?? new SfDataGrid
            {
                Dock = DockStyle.Fill,
                AllowEditing = false,
                AutoGenerateColumns = true,
                ShowRowHeader = false,
                RowHeight = LayoutTokens.GetScaled(LayoutTokens.GridRowHeightMedium),
                HeaderRowHeight = LayoutTokens.GetScaled(LayoutTokens.GridHeaderRowHeightMedium),
                AutoSizeColumnsMode = Syncfusion.WinForms.DataGrid.Enums.AutoSizeColumnsMode.Fill
            }).PreventStringRelationalFilters(_logger);
            mainTable.Controls.Add(_previewGrid, 0, 0);

            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = LayoutTokens.GetScaled(LayoutTokens.PanelPaddingCompact),
                MinimumSize = new Size(rightColumnWidth, 0)
            };
            var mappingFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = LayoutTokens.GetScaled(LayoutTokens.FilterRowPadding),
                Margin = Padding.Empty
            };
            SetupMappingPanel(mappingFlow);
            rightPanel.Controls.Add(mappingFlow);

            mainTable.Controls.Add(rightPanel, 1, 0);
            rootLayout.Controls.Add(mainTable, 0, 1);
            this.Controls.Add(rootLayout);

            // Apply theme via SfSkinManager
            try
            {
                var theme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                SfSkinManager.SetVisualStyle(this, theme);
            }
            catch { }

            this.ResumeLayout(false);
            this.PerformLayout();

            _logger?.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
        }

        private void SetupMappingPanel(FlowLayoutPanel parent)
        {
            parent.WrapContents = false;

            _chkHasHeader = _factory?.CreateCheckBoxAdv("First row contains headers", checkBox =>
            {
                checkBox.Checked = true;
                checkBox.AutoSize = true;
                checkBox.Margin = new Padding(0, 0, 0, 10);
            }) ?? new CheckBoxAdv { Text = "First row contains headers", Checked = true, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            parent.Controls.Add(_chkHasHeader);

            _cbAccount = CreateMappingComboBox("Account Mapping");
            AddMappingField(parent, "Map Account Number", _cbAccount);

            _cbDescription = CreateMappingComboBox("Description Mapping");
            AddMappingField(parent, "Map Description", _cbDescription);

            _cbBudgeted = CreateMappingComboBox("Budgeted Amount Mapping");
            AddMappingField(parent, "Map Budgeted Amount", _cbBudgeted);

            _cbActual = CreateMappingComboBox("Actual Amount Mapping");
            AddMappingField(parent, "Map Actual Amount", _cbActual);

            _cbFiscalYear = CreateMappingComboBox("Fiscal Year Override");
            AddMappingField(parent, "Fiscal Year (override)", _cbFiscalYear);

            _cbEntity = CreateMappingComboBox("Entity Mapping");
            AddMappingField(parent, "Bulk assign Entity/Fund", _cbEntity);

            var buttonHeight = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
            var buttonWidth = LayoutTokens.GetScaled(112);
            var btnPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                MinimumSize = new Size(0, buttonHeight),
                Margin = new Padding(0, 20, 0, 0)
            };
            _btnApply = _factory?.CreateSfButton("Import", btn =>
            {
                btn.Width = buttonWidth;
                btn.Height = buttonHeight;
            }) ?? new SfButton { Text = "Import", Width = buttonWidth, Height = buttonHeight };

            _btnCancel = _factory?.CreateSfButton("Cancel", btn =>
            {
                btn.Width = buttonWidth;
                btn.Height = buttonHeight;
                btn.Margin = new Padding(0, 0, 10, 0);
            }) ?? new SfButton { Text = "Cancel", Width = buttonWidth, Height = buttonHeight, Margin = new Padding(0, 0, 10, 0) };

            btnPanel.Controls.Add(_btnApply);
            btnPanel.Controls.Add(_btnCancel);
            parent.Controls.Add(btnPanel);
        }

        private SfComboBox CreateMappingComboBox(string accessibleName)
        {
            var combo = _factory?.CreateSfComboBox(createdCombo =>
            {
                createdCombo.DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList;
                createdCombo.AllowNull = false;
                createdCombo.AllowDropDownResize = false;
                createdCombo.MaxDropDownItems = 12;
                createdCombo.AccessibleName = accessibleName;
            }) ?? new SfComboBox
            {
                DropDownStyle = Syncfusion.WinForms.ListView.Enums.DropDownStyle.DropDownList,
                AllowNull = false,
                AllowDropDownResize = false,
                MaxDropDownItems = 12,
                AccessibleName = accessibleName,
            };

            if (_factory == null)
            {
                var theme = SfSkinManager.ApplicationVisualTheme ?? WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                combo.ApplySyncfusionTheme(theme, _logger);
            }

            return combo;
        }

        private void AddMappingField(FlowLayoutPanel parent, string label, Control field)
        {
            var labelControl = _factory?.CreateLabel(label, createdLabel =>
            {
                createdLabel.AutoSize = true;
                createdLabel.Margin = new Padding(0, 10, 0, 0);
            }) ?? new Label { Text = label, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };

            parent.Controls.Add(labelControl);
            var selectorWidth = LayoutTokens.GetScaled(320);
            var selectorHeight = LayoutTokens.GetScaled(LayoutTokens.StandardControlHeightLarge);
            field.Width = selectorWidth;
            field.Height = selectorHeight;
            field.MinimumSize = new Size(selectorWidth, selectorHeight);
            field.Margin = new Padding(0, 4, 0, 8);
            parent.Controls.Add(field);
        }

        private void WireEvents()
        {
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

            _chkHasHeaderChangedHandler = (s, e) => { LoadPreviewAndHeaders(); SetHasUnsavedChanges(true); };
            _chkHasHeader.CheckedChanged += _chkHasHeaderChangedHandler;
        }

        public void Initialize(string filePath, IEnumerable<string>? availableEntities = null, int defaultFiscalYear = 2025)
        {
            _filePath = filePath ?? string.Empty;

            var entityOptions = availableEntities?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            if (entityOptions.Count == 0)
            {
                entityOptions.Add("(None)");
            }

            _cbEntity.DataSource = null;
            _cbEntity.DataSource = entityOptions;
            _cbEntity.SelectedIndex = 0;

            var fiscalYearOptions = Enumerable.Range(defaultFiscalYear - 2, 5).ToList();
            _cbFiscalYear.DataSource = null;
            _cbFiscalYear.DataSource = fiscalYearOptions;
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
            var dt = new DataTable();
            var indexToName = _headers.Select((h, i) => (Index: i, Name: string.IsNullOrWhiteSpace(h) ? $"Column{i}" : h)).ToList();

            foreach (var col in indexToName)
            {
                dt.Columns.Add(col.Name);
            }

            foreach (var r in _previewRows)
            {
                var row = dt.NewRow();
                for (int i = 0; i < Math.Min(r.Length, dt.Columns.Count); i++)
                {
                    row[i] = r[i] ?? string.Empty;
                }
                dt.Rows.Add(row);
            }

            _previewGrid.DataSource = dt;

            // Fill combobox options
            var options = indexToName.Select(x => x.Name).ToList();
            options.AddRange(indexToName.Select(x => $"Column{x.Index}"));

            void PopulateCombo(SfComboBox cb)
            {
                cb.DataSource = null;
                cb.DataSource = new List<string>(options);
                if (options.Count > 0)
                {
                    cb.SelectedIndex = 0;
                }
            }

            PopulateCombo(_cbAccount);
            PopulateCombo(_cbDescription);
            PopulateCombo(_cbBudgeted);
            PopulateCombo(_cbActual);
        }

        private void OnMappingChanged(object? sender, EventArgs e) => SetHasUnsavedChanges(true);

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            // Queue async validation and save on the UI thread
            BeginInvoke(new Func<Task>(async () =>
            {
                var validation = await ValidateAsync(CancellationToken.None);
                if (!validation.IsValid)
                {
                    _logger?.LogDebug("CSV mapping validation failed");
                    return;
                }

                await SaveAsync(CancellationToken.None);
            }));
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

        public string? Mode => _mode;

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
                try { _btnApply?.SafeDispose(); } catch { }
                try { _btnCancel?.SafeDispose(); } catch { }
                try { _chkHasHeader?.SafeDispose(); } catch { }
                try { _cbAccount?.SafeClearDataSource(); _cbAccount?.SafeDispose(); } catch { }
                try { _cbDescription?.SafeClearDataSource(); _cbDescription?.SafeDispose(); } catch { }
                try { _cbBudgeted?.SafeClearDataSource(); _cbBudgeted?.SafeDispose(); } catch { }
                try { _cbActual?.SafeClearDataSource(); _cbActual?.SafeDispose(); } catch { }
                try { _cbFiscalYear?.SafeClearDataSource(); _cbFiscalYear?.SafeDispose(); } catch { }
                try { _cbEntity?.SafeClearDataSource(); _cbEntity?.SafeDispose(); } catch { }
            }

            base.Dispose(disposing);
        }
    }
}
