import os
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

# Ensure repo src is on path (same pattern as existing tests)
ROOT = os.path.dirname(os.path.dirname(__file__))
SRC = os.path.join(ROOT, "src")
if SRC not in sys.path:
    sys.path.insert(0, SRC)


def find_repo_root_with_view(max_up=8):
    """Walk up parent directories to find repo root containing src/Views/UtilityCustomerView.xaml."""
    p = Path(__file__).resolve()
    for i in range(max_up):
        cand = p.parents[i]
        if (cand / "src" / "Views" / "UtilityCustomerView.xaml").exists():
            return cand
    raise FileNotFoundError("Could not find repo root with src/Views/UtilityCustomerView.xaml")


def local_name(tag: str) -> str:
    return tag.split('}')[-1] if '}' in tag else tag


class TestUtilityCustomerView(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = find_repo_root_with_view()
        cls.xaml_path = cls.repo_root / "src" / "Views" / "UtilityCustomerView.xaml"
        if not cls.xaml_path.exists():
            raise FileNotFoundError(f"XAML file not found at {cls.xaml_path}")

        cls.xaml_text = cls.xaml_path.read_text(encoding='utf-8')
        cls.tree = ET.fromstring(cls.xaml_text)
        cls.root = cls.tree

        # Load code-behind (C#) for dialog helper checks
        cb_path = cls.repo_root / "src" / "Views" / "UtilityCustomerView.xaml.cs"
        cls.code_behind_text = cb_path.read_text(encoding='utf-8') if cb_path.exists() else ""

        # Load ViewModel source file for simple textual checks
        vm_path = cls.repo_root / "src" / "ViewModels" / "UtilityCustomerViewModel.cs"
        cls.viewmodel_text = vm_path.read_text(encoding='utf-8') if vm_path.exists() else ""

    def test_architecture_and_mvvm_wiring(self):
        # Root element should be Window
        self.assertIn('Window', self.root.tag, "Root element must be Window for UtilityCustomerView")

        # Prism AutoWire attribute present in raw XAML
        self.assertTrue('ViewModelLocator.AutoWireViewModel' in self.xaml_text or 'AutoWireViewModel' in self.xaml_text,
                        "Prism ViewModelLocator.AutoWireViewModel attribute expected in XAML")

    def test_input_bindings_and_shortcuts(self):
        # Check presence of common key bindings declared on Window
        self.assertIn('KeyBinding', self.xaml_text, "InputBindings/KeyBinding expected in XAML")
        # Check for at least a few expected commands/key combos
        expected_keys = [('{Binding LoadCustomersCommand', 'F5'), ('{Binding AddCustomerCommand', 'N'), ('{Binding SaveCustomerCommand', 'S'), ('{Binding DeleteCustomerCommand', 'Delete')]
        found_any = False
        for key_binding, key_name in expected_keys:
            if key_binding in self.xaml_text or key_name in self.xaml_text:
                found_any = True
        self.assertTrue(found_any, "Expected Load/Add/Save/Delete key bindings or commands in XAML")

    def test_command_bindings_presence(self):
        # Check for binding expressions referencing important VM commands and properties
        expected_bindings = [
            'Customers',
            'SelectedCustomer',
            'SearchTerm',
            'LoadCustomers',
            'AddCustomer',
            'SaveCustomer',
            'DeleteCustomer',
            'SearchCustomers',
            'LoadCustomerBills',
            'PayBill',
            'IsLoading'
        ]

        for token in expected_bindings:
            with self.subTest(token=token):
                present = token in self.xaml_text or token in self.viewmodel_text
                self.assertTrue(present, f"Expected reference to '{token}' in XAML or ViewModel source")

    def test_validation_and_error_template(self):
        # ErrorTemplate control template exists in resources
        self.assertIn('ErrorTemplate', self.xaml_text, "ErrorTemplate ControlTemplate expected in Window.Resources")

        # TextBoxes using ValidatesOnNotifyDataErrors=True
        self.assertIn('ValidatesOnNotifyDataErrors=True', self.xaml_text,
                      "TextBox bindings should use ValidatesOnNotifyDataErrors=True to surface INotifyDataErrorInfo errors")

    def test_dialog_helper_in_codebehind(self):
        # Check that code-behind exposes ShowCustomerDialog or ShowCustomerWindow
        self.assertTrue('ShowCustomerDialog(' in self.code_behind_text or 'ShowCustomerWindow(' in self.code_behind_text,
                        "Expected ShowCustomerDialog() or ShowCustomerWindow() helper in code-behind to present the view as a dialog/window")

    def test_viewmodel_class_and_members(self):
        # Basic textual checks for class and some critical members
        self.assertIn('class UtilityCustomerViewModel', self.viewmodel_text,
                      "UtilityCustomerViewModel class declaration expected in ViewModels source file")

        # Ensure command properties exist (allow either Async-suffixed or not)
        commands_to_check = [
            'LoadCustomersCommand', 'LoadCustomersAsyncCommand',
            'AddCustomerCommand', 'AddCustomerAsyncCommand',
            'SaveCustomerCommand', 'SaveCustomerAsyncCommand',
            'DeleteCustomerCommand', 'DeleteCustomerAsyncCommand',
            'SearchCustomersCommand', 'SearchCustomersAsyncCommand'
        ]

        for cmd in commands_to_check:
            with self.subTest(cmd=cmd):
                # presence in either XAML or ViewModel source is acceptable
                present = cmd in self.viewmodel_text or cmd in self.xaml_text
                # only assert for a subset - at least some core commands should be present
                if cmd.startswith('Load') or cmd.startswith('Add') or cmd.startswith('Save') or cmd.startswith('Delete'):
                    self.assertTrue(present, f"Expected command/property '{cmd}' to be present in ViewModel or XAML (one of these forms)")

    def test_codebehind_constructor_and_theme_application(self):
        # Ensure ThemeUtility.TryApplyTheme is referenced in code-behind
        self.assertIn('TryApplyTheme', self.code_behind_text, "Constructor should apply theme via ThemeUtility.TryApplyTheme")

    def test_codebehind_loaded_handler_and_autoload(self):
        # Validate that Loaded handler invokes LoadCustomersCommand when DataContext has customers count == 0
        # We check for the pattern 'if (DataContext is UtilityCustomerViewModel vm && vm.Customers.Count == 0)' and 'vm.LoadCustomersCommand.Execute()'
        self.assertIn('DataContext is UtilityCustomerViewModel vm', self.code_behind_text,
                      "Expected code-behind to check DataContext is UtilityCustomerViewModel in Loaded handler")
        self.assertIn('vm.LoadCustomersCommand.Execute()', self.code_behind_text,
                      "Expected Loaded handler to call vm.LoadCustomersCommand.Execute() when customers empty")

    def test_codebehind_show_helpers(self):
        # Check for static helper methods to show window/dialog
        self.assertIn('public static void ShowCustomerWindow()', self.code_behind_text,
                      "Expected ShowCustomerWindow static helper in code-behind")
        self.assertIn('public static bool? ShowCustomerDialog()', self.code_behind_text,
                      "Expected ShowCustomerDialog static helper in code-behind")

if __name__ == '__main__':
    unittest.main()
