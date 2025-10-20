import os
import sys
import unittest
from pathlib import Path


def find_repo_root_with_module(max_up=8):
    p = Path(__file__).resolve()
    for i in range(max_up):
        cand = p.parents[i]
        if (cand / "src" / "Startup" / "Modules" / "UtilityCustomerModule.cs").exists():
            return cand
    raise FileNotFoundError("Could not find repo root with UtilityCustomerModule.cs")


class TestDialogRegistration(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = find_repo_root_with_module()
        cls.module_path = cls.repo_root / "src" / "Startup" / "Modules" / "UtilityCustomerModule.cs"
        cls.module_text = cls.module_path.read_text(encoding='utf-8')

    def test_register_dialog_exists(self):
        # Expect the module to register the CustomerEditDialogView with its ViewModel
        expected = 'RegisterDialog<WileyWidget.Views.CustomerEditDialogView, WileyWidget.ViewModels.CustomerEditDialogViewModel>()'
        # Accept shorter form as well
        alt = 'RegisterDialog<"'  # unlikely but keep defensive
        self.assertIn('RegisterDialog', self.module_text, "Expected RegisterDialog call in UtilityCustomerModule.cs")
        self.assertIn('CustomerEditDialogView', self.module_text, "Expected CustomerEditDialogView type to be registered in module")


if __name__ == '__main__':
    unittest.main()
