import os
import sys
import unittest
from pathlib import Path


def find_repo_root_with_dialog(max_up=8):
    p = Path(__file__).resolve()
    for i in range(max_up):
        cand = p.parents[i]
        if (cand / "src" / "Views" / "CustomerEditDialogView.xaml").exists():
            return cand
    raise FileNotFoundError("Could not find repo root with CustomerEditDialogView.xaml")


class TestCustomerEditDialogExtra(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = find_repo_root_with_dialog()
        cls.xaml_path = cls.repo_root / "src" / "Views" / "CustomerEditDialogView.xaml"
        cls.xaml_text = cls.xaml_path.read_text(encoding='utf-8')

    def test_acrylic_panel_present(self):
        self.assertIn('SfAcrylicPanel', self.xaml_text, 'Expected SfAcrylicPanel wrapper in dialog XAML')

    def test_validationsummary_binding(self):
        self.assertIn('ValidationSummary', self.xaml_text, 'Expected ValidationSummary binding in dialog XAML')


if __name__ == '__main__':
    unittest.main()
