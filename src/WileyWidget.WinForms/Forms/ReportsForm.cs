using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Themes;
using Syncfusion.WinForms.Input;
using Syncfusion.WinForms.Core;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// Reports form displaying BoldReports ReportViewer with WinForms integration via ElementHost.
/// Supports loading RDL/RDLC reports, exporting to PDF/Excel, and parameter binding.
///
/// Architecture:
/// - ReportViewer: control hosted via ElementHost (interop)
/// - ViewModel: ReportsViewModel (MVVM via CommunityToolkit.Mvvm)
/// - Service: IBoldReportService (reflection-based for /Services layer isolation)
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "WinForms UI")]
public partial class ReportsForm : Form
{
    private readonly ReportsViewModel _viewModel;
    /// <summary>
    /// Bind ViewModel properties to UI using INotifyPropertyChanged.
    /// </summary>
    private void BindViewModel()
    {
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ReportsViewModel.IsBusy))
            {
                if (_generateButton != null) _generateButton.Enabled = !_viewModel.IsBusy;
                if (_exportPdfButton != null) _exportPdfButton.Enabled = !_viewModel.IsBusy;
                if (_exportExcelButton != null) _exportExcelButton.Enabled = !_viewModel.IsBusy;
            }
            else if (e.PropertyName == nameof(ReportsViewModel.ErrorMessage))
            {
                if (_statusLabel != null && !string.IsNullOrEmpty(_viewModel.ErrorMessage))
                {
                    _statusLabel.Text = $"Error: {_viewModel.ErrorMessage}";
                    _statusLabel.ForeColor = Color.Red;
                }
            }
            else if (e.PropertyName == nameof(ReportsViewModel.StatusMessage))
            {
                if (_statusLabel != null)
                {
                    _statusLabel.Text = _viewModel.StatusMessage ?? "Ready";
                    _statusLabel.ForeColor = string.IsNullOrEmpty(_viewModel.ErrorMessage) ? Color.Green : Color.Red;
                }
            }
            else if (e.PropertyName == nameof(ReportsViewModel.PreviewData) ||
                     e.PropertyName == nameof(ReportsViewModel.CurrentPage) ||
                     e.PropertyName == nameof(ReportsViewModel.PageSize))
            {
                RefreshPreviewGrid();
            }
        };

        RefreshPreviewGrid();
        _logger.LogDebug("ViewModel binding established");
    }

    private void RefreshPreviewGrid()
    {
        try
        {
            if (_previewGrid == null) return;

            if (InvokeRequired)
            {
                Invoke(new Action(RefreshPreviewGrid));
                return;
            }

            var list = _viewModel.PreviewData?.ToList() ?? new System.Collections.Generic.List<ReportDataItem>();
            var binding = new BindingSource
            {
                DataSource = list.Select(p => new { p.Name, p.Value, p.Category }).ToList()
            };
            _previewGrid.DataSource = binding;

            if (_pageInfoLabel != null)
            {
                _pageInfoLabel.Text = $"Page: {_viewModel.CurrentPage} | Page Size: {_viewModel.PageSize} | Rows: {list.Count}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh preview grid");
        }
    }

    private (Control HostControl, object? ViewerInstance) CreateReportViewerHost()
    {
        try
        {
            var viewerType = Type.GetType("BoldReports.UI.Xaml.ReportViewer, BoldReports.WPF", throwOnError: false);
            if (viewerType == null)
            {
                _logger.LogWarning("BoldReports.UI.Xaml.ReportViewer type not found. Using placeholder host.");
                return (BuildPlaceholderLabel("BoldReports viewer not found. Restore BoldReports.WPF to enable viewing."), null);
            }

            var viewerInstance = Activator.CreateInstance(viewerType);
            if (viewerInstance is not UIElement uiElement)
            {
                _logger.LogWarning("BoldReports viewer instance is not a UIElement. Using placeholder host.");
                return (BuildPlaceholderLabel("BoldReports viewer failed to initialize UI element."), null);
            }

            var host = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = uiElement
            };

            return (host, viewerInstance);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create BoldReports viewer. Using placeholder host.");
            return (BuildPlaceholderLabel("Report viewing requires BoldReports.WPF packages. Restore packages and restart."), null);
        }
    }

    private static Control BuildPlaceholderLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10F),
            BackColor = Color.FromArgb(248, 248, 248)
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _elementHost?.Dispose();
            _reportTypeCombo?.Dispose();
            _fromDatePicker?.Dispose();
            _toDatePicker?.Dispose();
            _generateButton?.Dispose();
            _exportPdfButton?.Dispose();
            _exportExcelButton?.Dispose();
            _statusLabel?.Dispose();

            Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
        }

        base.Dispose(disposing);
    }
}
            _exportPdfButton.Click += async (s, e) => await Utilities.AsyncEventHelper.ExecuteAsync(async ct => await _viewModel.ExportToPdfCommand.ExecuteAsync(null), _cts, this, _logger, "Exporting to PDF");
            _exportExcelButton.Click += async (s, e) => await Utilities.AsyncEventHelper.ExecuteAsync(async ct => await _viewModel.ExportToExcelCommand.ExecuteAsync(null), _cts, this, _logger, "Exporting to Excel");
            _printButton.Click += async (s, e) => await Utilities.AsyncEventHelper.ExecuteAsync(async ct => await _viewModel.PrintCommand.ExecuteAsync(null), _cts, this, _logger, "Printing report");

            // Zoom options
            _zoomCombo.Items.AddRange(new object[] { "50%", "75%", "100%", "125%", "150%", "200%" });
            _zoomCombo.SelectedItem = "100%";
            _zoomCombo.SelectedIndexChanged += async (s, e) =>
            {
                if (_zoomCombo?.SelectedItem != null)
                {
                    var txt = _zoomCombo.SelectedItem.ToString()?.TrimEnd('%');
                    if (int.TryParse(txt, out var pct))
                    {
                        await Utilities.AsyncEventHelper.ExecuteAsync(async ct => await _viewModel.SetZoomCommand.ExecuteAsync(pct), _cts, this, _logger, "Setting zoom");
                    }
                }
            };

            _findButton.Click += async (s, e) =>
            {
                if (_findTextBox != null)
                {
                    _viewModel.SearchText = _findTextBox.Text;
                    await Utilities.AsyncEventHelper.ExecuteAsync(async ct => await _viewModel.FindCommand.ExecuteAsync(null), _cts, this, _logger, "Searching report");
                }
            };

            _toggleParamsButton.Click += async (s, e) => await Utilities.AsyncEventHelper.ExecuteAsync(async ct => await _viewModel.ToggleParametersPanelCommand.ExecuteAsync(null), _cts, this, _logger, "Toggling parameters panel");

            _generateButton.Click += async (s, e) =>
            {
                // Verify template exists before heavy generation to avoid BoldReports error dialogs
                var path = _viewModel.GetReportPathIfExists();
                if (string.IsNullOrEmpty(path))
                {
                    var expected = System.IO.Path.Combine(AppContext.BaseDirectory, "Reports", "<report>.rdlc");
                    var msg = $"Report template not found for '{_viewModel.SelectedReportType}'.\n\nEnsure the correct RDLC/RDL file exists under: {expected}\n\nYou can place templates in the application's 'Reports' folder or add them to the project output.";
                    _statusLabel.Text = $"Error: report template missing";
                    _statusLabel.ForeColor = Color.Red;
                    MessageBox.Show(msg, "Report Template Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await Utilities.AsyncEventHelper.ExecuteAsync(async ct => await _viewModel.GenerateReportCommand.ExecuteAsync(null), _cts, this, _logger, "Generating report");
            };

            // Add controls to toolbar flow in logical order
            toolbarFlow.Controls.Add(typeLabel);
            toolbarFlow.Controls.Add(_reportTypeCombo);
            toolbarFlow.Controls.Add(fromLabel);
            toolbarFlow.Controls.Add(_fromDatePicker);
            toolbarFlow.Controls.Add(toLabel);
            toolbarFlow.Controls.Add(_toDatePicker);
            toolbarFlow.Controls.Add(fiscalLabel);
            toolbarFlow.Controls.Add(_fiscalYearPicker);
            toolbarFlow.Controls.Add(_generateButton);
            toolbarFlow.Controls.Add(_exportPdfButton);
            toolbarFlow.Controls.Add(_exportExcelButton);
            toolbarFlow.Controls.Add(_printButton);
            toolbarFlow.Controls.Add(_zoomCombo);
            toolbarFlow.Controls.Add(_findTextBox);
            toolbarFlow.Controls.Add(_findButton);
            toolbarFlow.Controls.Add(_toggleParamsButton);
            toolbarFlow.Controls.Add(_prevPageButton);
            toolbarFlow.Controls.Add(_nextPageButton);
            toolbarFlow.Controls.Add(_statusLabel);

            Controls.Add(toolbarFlow);

            // === REPORT VIEWER PANEL (Main) ===
            var reportPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            // TODO: Add BoldReports and WindowsFormsIntegration packages to enable report viewing
            // Create WPF ReportViewer
            // _reportViewer = new ReportViewer();

            // Host WPF control in WinForms via ElementHost
            // _elementHost = new ElementHost
            // {
            //     Dock = DockStyle.Fill,
            //     Child = _reportViewer
            // };

            // reportPanel.Controls.Add(_elementHost);

            // Create BoldReports viewer host (fallbacks to placeholder if viewer is unavailable)
            var hostResult = CreateReportViewerHost();
            _elementHost = hostResult.HostControl;
            _reportViewer = hostResult.ViewerInstance;
            reportPanel.Controls.Add(_elementHost);

            // Build a split container so users can see the report and a lightweight preview grid
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = (int)(Height * 0.65),
                Panel1MinSize = 300,
                Panel2MinSize = 150
            };

            split.Panel1.Controls.Add(_elementHost);

            // Bottom panel: preview grid + paging controls
            _previewGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AccessibleName = "Report Preview",
                AccessibleDescription = "Shows a small preview of the report data"
            };

            var previewToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(6)
            };

            _pageInfoLabel = new Label
            {
                Text = $"Page: {_viewModel.CurrentPage} / {_viewModel.PageSize}",
                AutoSize = true,
                Location = new Point(6, 8)
            };

            _pageSizeControl = new NumericUpDown
            {
                Minimum = 5,
                Maximum = 500,
                Value = _viewModel.PageSize,
                Size = new Size(80, 24),
                Location = new Point(120, 4),
                AccessibleName = "Preview Page Size",
                AccessibleDescription = "Number of rows to show in preview pages"
            };
            _pageSizeControl.ValueChanged += (s, e) =>
            {
                _viewModel.PageSize = (int)_pageSizeControl.Value;
                _viewModel.CurrentPage = 1;
                /// <summary>
                /// Bind ViewModel properties to UI using INotifyPropertyChanged.
                /// </summary>
                private void BindViewModel()
                {
                    _viewModel.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(ReportsViewModel.IsBusy))
                        {
                            if (_generateButton != null) _generateButton.Enabled = !_viewModel.IsBusy;
                            if (_exportPdfButton != null) _exportPdfButton.Enabled = !_viewModel.IsBusy;
                            if (_exportExcelButton != null) _exportExcelButton.Enabled = !_viewModel.IsBusy;
                        }
                        else if (e.PropertyName == nameof(ReportsViewModel.ErrorMessage))
                        {
                            if (_statusLabel != null)
                            {
                                if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
                                {
                                    _statusLabel.Text = $"Error: {_viewModel.ErrorMessage}";
                                    _statusLabel.ForeColor = Color.Red;
                                }
                            }
                        }
                        else if (e.PropertyName == nameof(ReportsViewModel.StatusMessage))
                        {
                            if (_statusLabel != null)
                            {
                                _statusLabel.Text = _viewModel.StatusMessage ?? "Ready";
                                _statusLabel.ForeColor = string.IsNullOrEmpty(_viewModel.ErrorMessage) ? Color.Green : Color.Red;
                            }
                        }
                        else if (e.PropertyName == nameof(ReportsViewModel.PreviewData) || e.PropertyName == nameof(ReportsViewModel.CurrentPage) || e.PropertyName == nameof(ReportsViewModel.PageSize))
                        {
                            RefreshPreviewGrid();
                        }
                    };

                    RefreshPreviewGrid();
                    _logger.LogDebug("ViewModel binding established");
                }

                private void RefreshPreviewGrid()
                {
                    try
                    {
                        if (_previewGrid == null) return;

                        if (InvokeRequired)
                        {
                            Invoke(new Action(RefreshPreviewGrid));
                            return;
                        }

                        var list = _viewModel.PreviewData?.ToList() ?? new System.Collections.Generic.List<ReportDataItem>();
                        var binding = new BindingSource
                        {
                            DataSource = list.Select(p => new { p.Name, p.Value, p.Category }).ToList()
                        };
                        _previewGrid.DataSource = binding;

                        if (_pageInfoLabel != null)
                        {
                            _pageInfoLabel.Text = $"Page: {_viewModel.CurrentPage} | Page Size: {_viewModel.PageSize} | Rows: {list.Count}";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to refresh preview grid");
                    }
                }

                private (Control HostControl, object? ViewerInstance) CreateReportViewerHost()
                {
                    try
                    {
                        var viewerType = Type.GetType("BoldReports.UI.Xaml.ReportViewer, BoldReports.WPF", throwOnError: false);
                        if (viewerType == null)
                        {
                            _logger.LogWarning("BoldReports.UI.Xaml.ReportViewer type not found. Using placeholder host.");
                            return (BuildPlaceholderLabel("BoldReports viewer not found. Restore BoldReports.WPF to enable viewing."), null);
                        }

                        var viewerInstance = Activator.CreateInstance(viewerType);
                        if (viewerInstance is not UIElement uiElement)
                        {
                            _logger.LogWarning("BoldReports viewer instance is not a UIElement. Using placeholder host.");
                            return (BuildPlaceholderLabel("BoldReports viewer failed to initialize UI element."), null);
                        }

                        var host = new ElementHost
                        {
                            Dock = DockStyle.Fill,
                            Child = uiElement
                        };

                        return (host, viewerInstance);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create BoldReports viewer. Using placeholder host.");
                        return (BuildPlaceholderLabel("Report viewing requires BoldReports.WPF packages. Restore packages and restart."), null);
                    }
                }

                private static Control BuildPlaceholderLabel(string text)
                {
                    return new Label
                    {
                        Text = text,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Segoe UI", 10F),
                        BackColor = Color.FromArgb(248, 248, 248)
                    };
                }

                protected override void Dispose(bool disposing)
                {
                    if (disposing)
                    {
                        _elementHost?.Dispose();
                        _reportTypeCombo?.Dispose();
                        _fromDatePicker?.Dispose();
                        _toDatePicker?.Dispose();
                        _generateButton?.Dispose();
                        _exportPdfButton?.Dispose();
                        _exportExcelButton?.Dispose();
                        _statusLabel?.Dispose();

                        Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
                    }

                    base.Dispose(disposing);
                }
            }
                {
                    _pageInfoLabel.Text = $"Page: {_viewModel.CurrentPage} | Page Size: {_viewModel.PageSize} | Rows: {list.Count}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh preview grid");
            }
        }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _elementHost?.Dispose();
            _reportTypeCombo?.Dispose();
            _fromDatePicker?.Dispose();
            _toDatePicker?.Dispose();
            _generateButton?.Dispose();
            _exportPdfButton?.Dispose();
            _exportExcelButton?.Dispose();
            _statusLabel?.Dispose();

            // Cancel and dispose async operations
            Utilities.AsyncEventHelper.CancelAndDispose(ref _cts);
        }

        base.Dispose(disposing);
    }
}
