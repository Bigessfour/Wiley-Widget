# Upgrade to Python 3.14 — notes for developers

This project has standardized on Python 3.14. The following short note explains what changed and recommends the best practice for local development.

## Key points

- The project tooling and developer guidance now use Python 3.14.
- Old, store-based installs such as Microsoft Store Python 3.11 were removed from the guidance and may have been uninstalled in some developer environments.

## Recommended local setup (best practice)

1. Use a workspace-local virtual environment so the project is self-contained and reproducible.
   - From the repo root run:

   ```powershell
   python -m venv .venv
   .\.venv\Scripts\Activate.ps1
   python -m pip install --upgrade pip
   # then install project dev requirements if any
   ```

2. Configure VS Code to use the workspace interpreter:
   - In the workspace, open the Command Palette and choose "Python: Select Interpreter" and pick the interpreter under the workspace `.venv` (e.g. `.venv\Scripts\python.exe`).
   - Alternatively add the following to `.vscode/settings.json`:

   ```json
   {
     "python.defaultInterpreterPath": "${workspaceFolder}\\.venv\\Scripts\\python.exe"
   }
   ```

## Troubleshooting: replaced Store / old-interpreter references

If you previously used the Microsoft Store or an explicit `python3.11` interpreter path in VS Code settings, those paths may now be invalid. We added a troubleshooting helper script which can find those settings and replace them with the workspace venv interpreter when present:

- Script: `scripts/tools/fix-vscode-python-paths.ps1`
- Dry-run (detect only):

```powershell
# from repo root
.\scripts\tools\fix-vscode-python-paths.ps1
```

- Auto-fix (makes backup and updates settings.json):

```powershell
.\scripts\tools\fix-vscode-python-paths.ps1 -AutoFix -BackupDir .\.backup\vscode-fix
```

The script will search for `.vscode/settings.json` files referencing Microsoft Store or Python 3.11-based locations and, if your workspace contains `.venv`, replace the interpreter settings and create a backup.

## CI / docs / badges

We standardized the repo on Python 3.14 but did not change pipelines or badges automatically. Consider updating CI images, runner tags, or README badges to explicitly reference `python:3.14` if you also want CI to run with 3.14.

If you'd like, I can also update CI pipelines and README badges to reflect Python 3.14 across the board — tell me which files or pipelines to change and I'll do it.
