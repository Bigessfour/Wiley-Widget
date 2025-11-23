# ...existing imports...
from toolkit_validator import validate_communitytoolkit_mvvm
from winui_validator import validate_winui


def run_startup_checks():
    # ...existing checks...
    validate_winui()
    validate_communitytoolkit_mvvm()
    # ...existing checks...
