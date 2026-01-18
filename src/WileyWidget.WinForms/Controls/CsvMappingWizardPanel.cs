using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
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

    public partial class CsvMappingWizardPanel : UserControl
    {
        private readonly ILogger? _logger;
        private readonly DataGridView _previewGrid;
        private readonly ComboBox _cbAccount;
        private readonly ComboBox _cbDescription;
        private readonly ComboBox _cbBudgeted;
        private readonly ComboBox _cbActual;
        private readonly ComboBox _cbFiscalYear;
        private readonly ComboBox _cbEntity;
        private readonly Button _btnApply;
        private readonly Button _btnCancel;
        private readonly CheckBox _chkHasHeader;

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

            _chkHasHeader = new CheckBox { Text = "First row contains headers", Checked = true, AutoSize = true };
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
            _btnApply = new Button { Text = "Import", Width = 90 }; _btnApply.Click += BtnApply_Click; btnPanel.Controls.Add(_btnApply);
            _btnCancel = new Button { Text = "Cancel", Width = 90 }; _btnCancel.Click += BtnCancel_Click; btnPanel.Controls.Add(_btnCancel);

            var rightContainer = new Panel { Dock = DockStyle.Fill }; rightContainer.Controls.Add(rightPanel); rightContainer.Controls.Add(btnPanel);
            main.Controls.Add(rightContainer, 1, 0);

            Controls.Add(main);
            
            this.PerformLayout();
            this.Refresh();
            
            _logger?.LogDebug("[PANEL] {PanelName} content anchored and refreshed", this.Name);
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

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            // Build mapping - selected item strings will be header names or ColumnN tokens
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
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
    }
}
