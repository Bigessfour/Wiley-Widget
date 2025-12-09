#!/usr/bin/env python3
"""
Pytest tests for validate-views.py script.

Tests all validation rules and edge cases to ensure the script works correctly.
"""

import importlib.util
import sys
from pathlib import Path

import pytest  # type: ignore[import-not-found]

# Import by loading the module directly since filename has hyphen
spec = importlib.util.spec_from_file_location(
    "validate_views",
    Path(__file__).parent.parent / "scripts" / "testing" / "validate-views.py",
)
assert spec is not None and spec.loader is not None, "Failed to load validate-views.py"
validate_views = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validate_views)

Severity = validate_views.Severity  # type: ignore[attr-defined]
Violation = validate_views.Violation  # type: ignore[attr-defined]
ViewValidator = validate_views.ViewValidator  # type: ignore[attr-defined]


class TestViewValidator:
    """Test suite for ViewValidator class."""

    @pytest.fixture
    def validator(self) -> ViewValidator:
        """Create a validator instance for testing."""
        return ViewValidator(verbose=False)

    # Test VW001: Form Inheritance
    def test_check_form_inheritance_valid(self, validator: ViewValidator):
        """Test that forms inheriting from Form pass validation."""
        content = """
        public partial class MainForm : Form
        {
            public MainForm()
            {
                InitializeComponent();
            }
        }
        """
        violations = validator._check_form_inheritance(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_form_inheritance_invalid(self, validator: ViewValidator):
        """Test that forms not inheriting from Form are flagged."""
        content = """
        public partial class MainForm : UserControl
        {
            public MainForm()
            {
                InitializeComponent();
            }
        }
        """
        violations = validator._check_form_inheritance(content, "MainForm.cs")
        assert len(violations) == 1
        assert violations[0].rule == "VW001"
        assert violations[0].severity == Severity.ERROR

    # Test VW002-VW003: Syncfusion Controls
    def test_check_syncfusion_controls_present(self, validator: ViewValidator):
        """Test that Syncfusion controls are detected."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid dataGrid;
            private DockingManager dockingManager;
        }
        """
        violations = validator._check_syncfusion_controls(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_syncfusion_controls_missing(self, validator: ViewValidator):
        """Test that forms without Syncfusion controls are flagged."""
        content = """
        public partial class MainForm : Form
        {
            private DataGridView dataGrid;
        }
        """
        violations = validator._check_syncfusion_controls(content, "MainForm.cs")
        assert len(violations) == 1
        assert violations[0].rule == "VW002"
        assert violations[0].severity == Severity.WARNING

    def test_check_syncfusion_controls_mixed(self, validator: ViewValidator):
        """Test that mixing Syncfusion and standard controls is flagged."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid sfDataGrid;
            private DataGridView standardGrid;
        }
        """
        violations = validator._check_syncfusion_controls(content, "MainForm.cs")
        assert len(violations) == 1
        assert violations[0].rule == "VW003"
        assert violations[0].severity == Severity.WARNING

    # Test VW004-VW005: Theming
    def test_check_theming_applied(self, validator: ViewValidator):
        """Test that themed controls pass validation."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid dataGrid;

            void InitControls()
            {
                dataGrid.ThemeName = "Office2019Colorful";
            }
        }
        """
        violations = validator._check_theming(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_theming_missing(self, validator: ViewValidator):
        """Test that unthemed Syncfusion controls are flagged."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid dataGrid;
        }
        """
        violations = validator._check_theming(content, "MainForm.cs")
        assert len(violations) == 1
        assert violations[0].rule == "VW004"
        assert violations[0].severity == Severity.WARNING

    def test_check_theming_inconsistent(self, validator: ViewValidator):
        """Test that inconsistent themes are flagged."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid dataGrid;
            private ButtonAdv button;

            void InitControls()
            {
                dataGrid.ThemeName = "Office2019Colorful";
                button.ThemeName = "Office2016Black";
            }
        }
        """
        violations = validator._check_theming(content, "MainForm.cs")
        assert len(violations) == 1
        assert violations[0].rule == "VW005"
        assert violations[0].severity == Severity.WARNING
        assert "Office2019Colorful" in violations[0].message
        assert "Office2016Black" in violations[0].message

    # Test VW006: ViewModel Binding
    def test_check_viewmodel_binding_with_binding(self, validator: ViewValidator):
        """Test that ViewModels with proper binding pass."""
        content = """
        public partial class MainForm : Form
        {
            private MainViewModel _viewModel;

            void InitBindings()
            {
                DataSource = _viewModel;
            }
        }
        """
        violations = validator._check_viewmodel_binding(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_viewmodel_binding_without_binding(self, validator: ViewValidator):
        """Test that ViewModels without binding are flagged."""
        content = """
        public partial class MainForm : Form
        {
            private MainViewModel _viewModel;
        }
        """
        violations = validator._check_viewmodel_binding(content, "MainForm.cs")
        assert len(violations) == 1
        assert violations[0].rule == "VW006"
        assert violations[0].severity == Severity.INFO

    # Test VW007: Disposal
    def test_check_disposal_with_override(self, validator: ViewValidator):
        """Test that forms with proper Dispose override pass."""
        content = """
        public partial class MainForm : Form
        {
            private DockingManager dockingManager;

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    dockingManager?.Dispose();
                }
                base.Dispose(disposing);
            }
        }
        """
        violations = validator._check_disposal(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_disposal_missing_override(self, validator: ViewValidator):
        """Test that forms using disposables without Dispose are flagged."""
        content = """
        public partial class MainForm : Form
        {
            private DockingManager dockingManager;
        }
        """
        violations = validator._check_disposal(content, "MainForm.cs")
        assert len(violations) == 1
        assert violations[0].rule == "VW007"
        assert violations[0].severity == Severity.WARNING

    # Test VW008-VW009: DockingManager
    def test_check_docking_manager_proper_init(self, validator: ViewValidator):
        """Test that properly initialized DockingManager passes."""
        content = """
        public partial class MainForm : Form
        {
            private DockingManager dockingManager;

            void InitDocking()
            {
                dockingManager = new DockingManager(this, components);
                dockingManager.EnableDocumentMode = true;
            }
        }
        """
        violations = validator._check_docking_manager(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_docking_manager_missing_init(self, validator: ViewValidator):
        """Test that DockingManager without initialization is flagged."""
        content = """
        public partial class MainForm : Form
        {
            private DockingManager dockingManager;
        }
        """
        violations = validator._check_docking_manager(content, "MainForm.cs")
        errors = [v for v in violations if v.rule == "VW008"]
        assert len(errors) == 1
        assert errors[0].severity == Severity.ERROR

    def test_check_docking_manager_missing_config(self, validator: ViewValidator):
        """Test that DockingManager without essential props is flagged as info."""
        content = """
        public partial class MainForm : Form
        {
            private DockingManager dockingManager;

            void InitDocking()
            {
                dockingManager = new DockingManager(this, components);
            }
        }
        """
        violations = validator._check_docking_manager(content, "MainForm.cs")
        info = [v for v in violations if v.rule == "VW009"]
        assert len(info) == 1
        assert info[0].severity == Severity.INFO

    # Test VW010-VW011: Ribbon Controls
    def test_check_ribbon_proper_init(self, validator: ViewValidator):
        """Test that properly initialized Ribbon passes."""
        content = """
        public partial class MainForm : Form
        {
            private RibbonControlAdv ribbon;

            void InitRibbon()
            {
                ribbon = new RibbonControlAdv();
                ToolStripTabItem tab = new ToolStripTabItem();
                ribbon.Tabs.Add(tab);
            }
        }
        """
        violations = validator._check_ribbon_controls(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_ribbon_missing_init(self, validator: ViewValidator):
        """Test that Ribbon without initialization is flagged."""
        content = """
        public partial class MainForm : Form
        {
            private RibbonControlAdv ribbon;
        }
        """
        violations = validator._check_ribbon_controls(content, "MainForm.cs")
        errors = [v for v in violations if v.rule == "VW010"]
        assert len(errors) == 1
        assert errors[0].severity == Severity.ERROR

    def test_check_ribbon_no_tabs(self, validator: ViewValidator):
        """Test that Ribbon without tabs is flagged."""
        content = """
        public partial class MainForm : Form
        {
            private RibbonControlAdv ribbon;

            void InitRibbon()
            {
                ribbon = new RibbonControlAdv();
            }
        }
        """
        violations = validator._check_ribbon_controls(content, "MainForm.cs")
        warnings = [v for v in violations if v.rule == "VW011"]
        assert len(warnings) == 1
        assert warnings[0].severity == Severity.WARNING

    # Test VW012-VW013: Data Binding
    def test_check_data_binding_complete(self, validator: ViewValidator):
        """Test that SfDataGrid with proper binding passes."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid dataGrid;

            void InitGrid()
            {
                dataGrid.DataSource = viewModel.Items;
                dataGrid.AutoGenerateColumns = true;
            }
        }
        """
        violations = validator._check_data_binding(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_data_binding_missing_source(self, validator: ViewValidator):
        """Test that SfDataGrid without DataSource is flagged."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid dataGrid;
        }
        """
        violations = validator._check_data_binding(content, "MainForm.cs")
        warnings = [v for v in violations if v.rule == "VW012"]
        assert len(warnings) == 1
        assert warnings[0].severity == Severity.WARNING

    def test_check_data_binding_missing_columns(self, validator: ViewValidator):
        """Test that SfDataGrid without columns config is flagged."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid dataGrid;

            void InitGrid()
            {
                dataGrid.DataSource = viewModel.Items;
            }
        }
        """
        violations = validator._check_data_binding(content, "MainForm.cs")
        info = [v for v in violations if v.rule == "VW013"]
        assert len(info) == 1
        assert info[0].severity == Severity.INFO

    # Test VW014: Thread Safety
    def test_check_thread_safety_proper(self, validator: ViewValidator):
        """Test that async methods with InvokeRequired pass."""
        content = """
        public partial class MainForm : Form
        {
            private async Task LoadDataAsync()
            {
                var data = await GetDataAsync();

                if (InvokeRequired)
                {
                    Invoke(() => UpdateUI(data));
                }
                else
                {
                    UpdateUI(data);
                }
            }
        }
        """
        violations = validator._check_thread_safety(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_thread_safety_missing(self, validator: ViewValidator):
        """Test that async methods without InvokeRequired are flagged."""
        content = """
        public partial class MainForm : Form
        {
            private async Task LoadDataAsync()
            {
                var data = await GetDataAsync();
                UpdateUI(data);
            }
        }
        """
        violations = validator._check_thread_safety(content, "MainForm.cs")
        assert len(violations) == 1
        assert violations[0].rule == "VW014"
        assert violations[0].severity == Severity.WARNING

    # Test VW015-VW016: Initialization
    def test_check_initialization_proper(self, validator: ViewValidator):
        """Test that constructor with InitializeComponent first passes."""
        content = """
        public partial class MainForm : Form
        {
            public MainForm()
            {
                InitializeComponent();
                LoadData();
            }
        }
        """
        violations = validator._check_initialization(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_initialization_missing(self, validator: ViewValidator):
        """Test that constructor without InitializeComponent is flagged."""
        content = """
        public partial class MainForm : Form
        {
            public MainForm()
            {
                LoadData();
            }
        }
        """
        violations = validator._check_initialization(content, "MainForm.cs")
        errors = [v for v in violations if v.rule == "VW015"]
        assert len(errors) == 1
        assert errors[0].severity == Severity.ERROR

    def test_check_initialization_wrong_order(self, validator: ViewValidator):
        """Test that InitializeComponent not called first is flagged."""
        content = """
        public partial class MainForm : Form
        {
            public MainForm()
            {
                LoadData();
                InitializeComponent();
            }
        }
        """
        violations = validator._check_initialization(content, "MainForm.cs")
        warnings = [v for v in violations if v.rule == "VW016"]
        assert len(warnings) == 1
        assert warnings[0].severity == Severity.WARNING

    # Test VW017-VW018: Accessibility
    def test_check_accessibility_complete(self, validator: ViewValidator):
        """Test that controls with accessibility features pass."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid dataGrid;

            void InitAccessibility()
            {
                dataGrid.TabIndex = 0;
                dataGrid.AccessibleName = "Main Data Grid";
            }
        }
        """
        violations = validator._check_accessibility(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_accessibility_missing_tabindex(self, validator: ViewValidator):
        """Test that controls without TabIndex are flagged."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid dataGrid;
        }
        """
        violations = validator._check_accessibility(content, "MainForm.cs")
        info = [v for v in violations if v.rule == "VW017"]
        assert len(info) == 1
        assert info[0].severity == Severity.INFO

    def test_check_accessibility_missing_accessible_props(
        self, validator: ViewValidator
    ):
        """Test that controls without AccessibleName are flagged."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid dataGrid;

            void InitAccessibility()
            {
                dataGrid.TabIndex = 0;
            }
        }
        """
        violations = validator._check_accessibility(content, "MainForm.cs")
        info = [v for v in violations if v.rule == "VW018"]
        assert len(info) == 1
        assert info[0].severity == Severity.INFO

    # Test VW019-VW020: Performance
    def test_check_performance_with_suspend(self, validator: ViewValidator):
        """Test that SuspendLayout/ResumeLayout usage passes."""
        content = """
        public partial class MainForm : Form
        {
            void AddControls()
            {
                SuspendLayout();
                Controls.Add(button1);
                Controls.Add(button2);
                ResumeLayout();
            }
        }
        """
        violations = validator._check_performance(content, "MainForm.cs")
        info = [v for v in violations if v.rule == "VW020"]
        assert len(info) == 0

    def test_check_performance_missing_suspend(self, validator: ViewValidator):
        """Test that Controls.Add without SuspendLayout is flagged."""
        content = """
        public partial class MainForm : Form
        {
            void AddControls()
            {
                Controls.Add(button1);
                Controls.Add(button2);
            }
        }
        """
        violations = validator._check_performance(content, "MainForm.cs")
        info = [v for v in violations if v.rule == "VW020"]
        assert len(info) == 1
        assert info[0].severity == Severity.INFO

    # Test VW021-VW022: Error Handling
    def test_check_error_handling_present(self, validator: ViewValidator):
        """Test that event handlers with try-catch pass."""
        content = """
        public partial class MainForm : Form
        {
            private void Button_Click(object sender, EventArgs e)
            {
                try
                {
                    PerformAction();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }
        """
        violations = validator._check_error_handling(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_error_handling_missing(self, validator: ViewValidator):
        """Test that event handlers without try-catch are flagged."""
        content = """
        public partial class MainForm : Form
        {
            private void Button_Click(object sender, EventArgs e)
            {
                PerformAction();
            }
        }
        """
        violations = validator._check_error_handling(content, "MainForm.cs")
        warnings = [v for v in violations if v.rule == "VW021"]
        assert len(warnings) == 1
        assert warnings[0].severity == Severity.WARNING

    # Test VW023-VW024: Localization
    def test_check_localization_with_attribute(self, validator: ViewValidator):
        """Test that forms with Localizable attribute pass."""
        content = """
        [Localizable(true)]
        public partial class MainForm : Form
        {
            void InitControls()
            {
                button1.Text = "Click Me";
                button2.Text = "Cancel";
            }
        }
        """
        violations = validator._check_localization(content, "MainForm.cs")
        info = [v for v in violations if v.rule == "VW024"]
        assert len(info) == 0

    def test_check_localization_many_hardcoded_strings(self, validator: ViewValidator):
        """Test that many hardcoded strings are flagged."""
        content = """
        public partial class MainForm : Form
        {
            void InitControls()
            {
                button1.Text = "One";
                button2.Text = "Two";
                button3.Text = "Three";
                button4.Text = "Four";
                button5.Text = "Five";
            }
        }
        """
        violations = validator._check_localization(content, "MainForm.cs")
        info = [v for v in violations if v.rule == "VW023"]
        assert len(info) == 1
        assert info[0].severity == Severity.INFO

    # Test VW025-VW026: Control Naming
    def test_check_control_naming_good_names(self, validator: ViewValidator):
        """Test that descriptively named controls pass."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid customersDataGrid;
            private ButtonAdv saveButton;
        }
        """
        violations = validator._check_control_naming(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_control_naming_generic_names(self, validator: ViewValidator):
        """Test that generic control names are flagged."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid control1;
            private ButtonAdv button2;
        }
        """
        violations = validator._check_control_naming(content, "MainForm.cs")
        warnings = [v for v in violations if v.rule == "VW026"]
        assert len(warnings) == 1
        assert warnings[0].severity == Severity.WARNING

    def test_check_control_naming_hungarian_notation(self, validator: ViewValidator):
        """Test that Hungarian notation is flagged."""
        content = """
        public partial class MainForm : Form
        {
            private SfDataGrid dgvCustomers;
            private ButtonAdv btnSave;
        }
        """
        violations = validator._check_control_naming(content, "MainForm.cs")
        info = [v for v in violations if v.rule == "VW025"]
        assert len(info) >= 1
        assert info[0].severity == Severity.INFO

    # Test VW027-VW028: Layout Patterns
    def test_check_layout_with_dock(self, validator: ViewValidator):
        """Test that Dock/Anchor usage passes."""
        content = """
        public partial class MainForm : Form
        {
            void InitLayout()
            {
                Controls.Add(panel1);
                panel1.Dock = DockStyle.Fill;
            }
        }
        """
        violations = validator._check_layout_patterns(content, "MainForm.cs")
        info = [v for v in violations if v.rule == "VW027"]
        assert len(info) == 0

    def test_check_layout_missing_dock(self, validator: ViewValidator):
        """Test that Controls.Add without Dock/Anchor is flagged."""
        content = """
        public partial class MainForm : Form
        {
            void InitLayout()
            {
                Controls.Add(panel1);
            }
        }
        """
        violations = validator._check_layout_patterns(content, "MainForm.cs")
        info = [v for v in violations if v.rule == "VW027"]
        assert len(info) == 1
        assert info[0].severity == Severity.INFO

    # Test VW029-VW030: Event Handlers
    def test_check_event_handlers_proper_cleanup(self, validator: ViewValidator):
        """Test that subscribed/unsubscribed events pass."""
        content = """
        public partial class MainForm : Form
        {
            void AttachEvents()
            {
                button.Click += Button_Click;
            }

            void DetachEvents()
            {
                button.Click -= Button_Click;
            }
        }
        """
        violations = validator._check_event_handlers(content, "MainForm.cs")
        warnings = [v for v in violations if v.rule == "VW029"]
        assert len(warnings) == 0

    def test_check_event_handlers_no_cleanup(self, validator: ViewValidator):
        """Test that unsubscribed events are flagged."""
        content = """
        public partial class MainForm : Form
        {
            void AttachEvents()
            {
                button.Click += Button_Click;
            }
        }
        """
        violations = validator._check_event_handlers(content, "MainForm.cs")
        warnings = [v for v in violations if v.rule == "VW029"]
        assert len(warnings) == 1
        assert warnings[0].severity == Severity.WARNING

    def test_check_event_handlers_async_void(self, validator: ViewValidator):
        """Test that async void event handlers are flagged."""
        content = """
        public partial class MainForm : Form
        {
            private async void Button_Click(object sender, EventArgs e)
            {
                await DoWorkAsync();
            }
        }
        """
        violations = validator._check_event_handlers(content, "MainForm.cs")
        warnings = [v for v in violations if v.rule == "VW030"]
        assert len(warnings) == 1
        assert warnings[0].severity == Severity.WARNING

    # Test VW031-VW033: Async Patterns
    def test_check_async_patterns_clean(self, validator: ViewValidator):
        """Test that proper async/await usage passes."""
        content = """
        public partial class MainForm : Form
        {
            private async Task LoadDataAsync()
            {
                var data = await GetDataAsync();
                UpdateUI(data);
            }
        }
        """
        violations = validator._check_async_patterns(content, "MainForm.cs")
        assert len(violations) == 0

    def test_check_async_patterns_blocking_calls(self, validator: ViewValidator):
        """Test that .Result/.Wait() are flagged as errors."""
        content = """
        public partial class MainForm : Form
        {
            private void LoadData()
            {
                var data = GetDataAsync().Result;
                UpdateUI(data);
            }
        }
        """
        violations = validator._check_async_patterns(content, "MainForm.cs")
        errors = [v for v in violations if v.rule == "VW033"]
        assert len(errors) == 1
        assert errors[0].severity == Severity.ERROR

    def test_check_async_patterns_configure_await(self, validator: ViewValidator):
        """Test that ConfigureAwait(false) in UI code is flagged."""
        content = """
        public partial class MainForm : Form
        {
            private async Task LoadDataAsync()
            {
                var data = await GetDataAsync().ConfigureAwait(false);
                UpdateUI(data);
            }
        }
        """
        violations = validator._check_async_patterns(content, "MainForm.cs")
        warnings = [v for v in violations if v.rule == "VW031"]
        assert len(warnings) == 1
        assert warnings[0].severity == Severity.WARNING

    # Test helper methods
    def test_get_line_number(self, validator: ViewValidator):
        """Test line number calculation."""
        content = "Line 1\nLine 2\nLine 3\nLine 4"

        # Position 0 is line 1
        assert validator._get_line_number(content, 0) == 1

        # After first newline is line 2
        assert validator._get_line_number(content, 7) == 2

        # After second newline is line 3
        assert validator._get_line_number(content, 14) == 3

    # Integration test
    def test_validate_file_error_handling(
        self, validator: ViewValidator, tmp_path: Path
    ):
        """Test that file validation handles errors gracefully."""
        # Create a temporary file with malformed encoding
        test_file = tmp_path / "test.cs"
        test_file.write_bytes(b"\xff\xfe\x00\x00Invalid")

        violations = validator.validate_file(test_file)

        # Should return a VW000 error for parse failure
        assert len(violations) >= 1
        parse_errors = [v for v in violations if v.rule == "VW000"]
        assert len(parse_errors) == 1
        assert parse_errors[0].severity == Severity.ERROR


class TestViolation:
    """Test suite for Violation dataclass."""

    def test_violation_to_dict(self):
        """Test Violation serialization to dict."""
        violation = Violation(
            rule="VW001",
            severity=Severity.ERROR,
            message="Test message",
            file="test.cs",
            line=42,
        )

        result = violation.to_dict()

        assert result["Rule"] == "VW001"
        assert result["Severity"] == "Error"
        assert result["Message"] == "Test message"
        assert result["File"] == "test.cs"
        assert result["Line"] == 42

    def test_violation_to_dict_no_line(self):
        """Test Violation serialization without line number."""
        violation = Violation(
            rule="VW002",
            severity=Severity.WARNING,
            message="Test warning",
            file="test.cs",
        )

        result = violation.to_dict()

        assert result["Rule"] == "VW002"
        assert result["Severity"] == "Warning"
        assert result["Line"] is None


class TestSeverity:
    """Test suite for Severity enum."""

    def test_severity_values(self):
        """Test that Severity enum has correct values."""
        assert Severity.ERROR.value == "Error"
        assert Severity.WARNING.value == "Warning"
        assert Severity.INFO.value == "Info"


if __name__ == "__main__":
    pytest.main([__file__, "-v", "--tb=short"])
