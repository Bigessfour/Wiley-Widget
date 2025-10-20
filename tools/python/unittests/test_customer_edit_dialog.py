import os
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = os.path.dirname(os.path.dirname(__file__))
SRC = os.path.join(ROOT, "src")
if SRC not in sys.path:
    sys.path.insert(0, SRC)


def find_repo_root_with_dialog(max_up=8):
    p = Path(__file__).resolve()
    for i in range(max_up):
        cand = p.parents[i]
        if (cand / "src" / "Views" / "CustomerEditDialogView.xaml").exists():
            return cand
    raise FileNotFoundError("Could not find repo root with src/Views/CustomerEditDialogView.xaml")


def local_name(tag: str) -> str:
    return tag.split('}')[-1] if '}' in tag else tag


class TestCustomerEditDialog(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = find_repo_root_with_dialog()
        cls.xaml_path = cls.repo_root / "src" / "Views" / "CustomerEditDialogView.xaml"
        cls.xaml_text = cls.xaml_path.read_text(encoding='utf-8')
        cls.tree = ET.fromstring(cls.xaml_text)
        cls.root = cls.tree

        vm_path = cls.repo_root / "src" / "ViewModels" / "CustomerEditDialogViewModel.cs"
        cls.viewmodel_text = vm_path.read_text(encoding='utf-8') if vm_path.exists() else ""

    def test_root_and_autowire(self):
        self.assertIn('UserControl', self.root.tag, "Root should be UserControl for dialog")
        self.assertIn('AutoWireViewModel', self.xaml_text, "Dialog should have ViewModelLocator.AutoWireViewModel=True")

    def test_bindings_present(self):
        expected = ['Customer.AccountNumber', 'Customer.FirstName', 'Customer.LastName', 'Customer.ServiceAddress', 'Customer.PhoneNumber', 'Customer.EmailAddress']
        for e in expected:
            with self.subTest(field=e):
                self.assertIn(e, self.xaml_text or self.viewmodel_text, f"Expected binding to {e} in dialog XAML or VM")

    def test_viewmodel_dialog_interface(self):
        # Ensure IDialogAware presence in ViewModel
        self.assertIn('IDialogAware', self.viewmodel_text, "CustomerEditDialogViewModel should implement IDialogAware")
        # Ensure SaveCommand and CancelCommand properties exist
        self.assertIn('SaveCommand', self.viewmodel_text, "SaveCommand expected in ViewModel")
        self.assertIn('CancelCommand', self.viewmodel_text, "CancelCommand expected in ViewModel")

if __name__ == '__main__':
    unittest.main()
