# Python Utility & No-Config Debugging Guide

This project includes auxiliary Python scripts under `scripts/` for analysis, CI insight, and maintenance. VS Code's Python Debugger extension enables **no-config debugging** via the `debugpy` shim – no `launch.json` required.

## Bootstrap Environment
Use the provided task or script.

### Task (recommended)
Run task: `python: bootstrap env`

### Manual
```
py -3 -m venv .venv
./.venv/Scripts/Activate.ps1
python -m pip install --upgrade pip
python -m pip install -r scripts/requirements.txt
```

`.venv/` is git-ignored.

## No-Config Debugging Usage
From an integrated terminal (after activation):
```
debugpy scripts/project-analyzer.py
```
Set breakpoints in the editor first; they bind automatically.

Modules:
```
debugpy -m somepackage.tool --flag value
```

Pass arguments:
```
debugpy scripts/json-validator.py -- verbose
```

Stopping the VS Code debug session does NOT terminate the script; use `Ctrl+C` in the terminal.

## Added Settings
`settings.json` opts out of `pythonTerminalEnvVarActivation` experiment to avoid PATH conflicts and sets the default interpreter to `.venv`.

## Common Issues
| Symptom | Fix |
|---------|-----|
| `command not found: debugpy` | Open a new integrated terminal; ensure Python Debugger extension installed |
| `python3: command not found` | Use `py` launcher or ensure Python on PATH |
| Breakpoints not hit | Confirm you launched with `debugpy` and file paths match |
| `--listen or --connect required` output | Uninstall globally installed `debugpy` (`python -m pip uninstall debugpy`) |

## Maintenance
Update dependencies:
```
# via task
python: install deps
# or manually
python -m pip install -r scripts/requirements.txt --upgrade
```

Regenerate environment:
```
powershell -ExecutionPolicy Bypass -File scripts/bootstrap_python_env.ps1 -Force
```

## When to Use Python Here
- Log & metrics inspection
- CI/CD artifact validation
- Quick data shaping for tests
- Prototyping algorithms before C# implementation

## Next Steps
1. Run `python: bootstrap env` task.
2. Set a breakpoint inside a script.
3. Invoke `debugpy scripts/<script>.py`.
4. Iterate rapidly without changing any C# config.

Happy debugging.
