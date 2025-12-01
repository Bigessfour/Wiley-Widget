#!/usr/bin/env python3
"""
MEGA AUDIT 3.14 — Uncheatable Edition
Now with Copilot Evasion Resistance™
"""
import json
import os
import re
import subprocess
import time
import platform
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).parent.parent.parent
# The WinForms project lives at the repository root as `WileyWidget.WinForms`.
# Use that path if the `src/` layout isn't present.
possible_build = (
    REPO_ROOT / "src" / "WileyWidget.WinForms" / "bin" / "Debug" / "net9.0-windows"
)
if possible_build.exists():
    BUILD = possible_build
else:
    BUILD = REPO_ROOT / "WileyWidget.WinForms" / "bin" / "Debug" / "net9.0-windows"
EXE = BUILD / "WileyWidget.WinForms.exe"


def run_and_capture(command, timeout=30):
    proc = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    try:
        outs, errs = proc.communicate(timeout=timeout)
        return (
            proc.returncode,
            outs.decode(errors="ignore"),
            errs.decode(errors="ignore"),
        )
    except subprocess.TimeoutExpired:
        proc.kill()
        return -9, "", "TIMEOUT — UI HANG DETECTED"


def audit():
    score = 100
    issues = []

    # 1. Build fresh (solution-aware)
    print("Building solution (Debug)...")
    build_cmd = ["dotnet", "build", str(REPO_ROOT / "WileyWidget.sln"), "-c", "Debug"]
    if subprocess.call(build_cmd, cwd=REPO_ROOT) != 0:
        print("Build failed")
        return 0, ["Build failed"]

    # 2. Launch + runtime checks
    if not EXE.exists():
        issues.append(f"Expected EXE not found at: {EXE}")
        print("Executable not found after build — aborting runtime checks")
        return 0, issues

    print("Launching app for smoke test...")
    code, out, err = run_and_capture([str(EXE)], timeout=45)

    if code == -9:
        score -= 50
        issues.append("CRITICAL: UI froze on launch → no real async loading")

    combined = out + "\n" + err
    # look for runtime evidence instead of naive presence in source
    if "DockingManager" not in combined and "Floating" not in combined:
        score -= 40
        issues.append("CRITICAL: No evidence of real DockingManager usage at runtime")

    if "License" in err and "expired" in err.lower():
        score -= 30
        issues.append("Syncfusion license invalid or missing")

    # 3. Layout persistence test (best-effort)
    layout_file = BUILD / "user-layout.xml"
    if layout_file.exists():
        original = layout_file.read_text(errors="ignore")
        backup = layout_file.with_suffix(".bak")
        try:
            os.replace(layout_file, backup)
        except Exception:
            pass

        # restart once to allow save flow
        run_and_capture([str(EXE)], timeout=20)
        time.sleep(3)
        if layout_file.exists():
            restored = layout_file.read_text(errors="ignore")
            if restored == original or len(restored) < 100:
                score -= 35
                issues.append("Layout persistence broken or fake")
        else:
            score -= 35
            issues.append("No layout saved → DockingManager not real")
        print("Launching app for smoke test...")
        # Launch the app (non-blocking) so we can inspect its process and optionally capture a screenshot
        proc = subprocess.Popen(
            [str(EXE)], stdout=subprocess.PIPE, stderr=subprocess.PIPE
        )
        # Give the app a little time to initialize its main window
        time.sleep(3)
        # Try to capture stdout/stderr non-blocking (best-effort)
        try:
            outs, errs = proc.communicate(timeout=5)
            out = outs.decode(errors="ignore")
            err = errs.decode(errors="ignore")
            code = proc.returncode
        except subprocess.TimeoutExpired:
            # Process still running — that's expected for GUI app. We'll not kill it here; leave running briefly.
            code = 0
            out = ""
            err = ""

        # Try to capture a screenshot of the main window for visual audit (best-effort)
        try:
            screenshots_dir = REPO_ROOT / "audit_screenshots"
            screenshots_dir.mkdir(exist_ok=True)
            screenshot_file = screenshots_dir / f"main_window_{int(time.time())}.png"
            if capture_window_screenshot(proc.pid, screenshot_file):
                print(f"Screenshot saved to {screenshot_file}")
            else:
                print("Screenshot capture unavailable on this environment")
        except Exception:
            pass
    print(f"\nMEGA AUDIT 3.14 FINAL SCORE: {score}%")
    if issues:
        print("REAL ISSUES:")
        for i in issues:
            print(f"  • {i}")
    else:
        print("Actually, legitimately 100%. You win the internet today.")

    return score, issues


def run_static_view_checks(repo_root: Path):
    def capture_window_screenshot(pid: int, out_path: Path) -> bool:
        """Attempt to capture the main window for the given process id and save to out_path (PNG).
        Uses PowerShell/.NET clipboard-free screen capture when available. Returns True if saved.
        This is best-effort and will gracefully fail on headless CI or non-Windows platforms.
        """
        # Only support Windows for now
        if platform.system() != "Windows":
            return False

        # Ensure output directory exists
        try:
            out_path.parent.mkdir(parents=True, exist_ok=True)
        except Exception:
            pass

        # Build PowerShell script to capture main window rectangle and copy from screen
        ps_script = f"""
    $ErrorActionPreference = 'SilentlyContinue'
    Add-Type @'
    using System;
    using System.Runtime.InteropServices;
    public struct RECT {{ public int Left; public int Top; public int Right; public int Bottom; }}
    public static class Win32 {{ [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect); }}
    '@
    try {{
        $p = Get-Process -Id {pid} -ErrorAction Stop
        $hwnd = $p.MainWindowHandle
        if ($hwnd -eq 0) {{ exit 2 }}
        $rect = New-Object RECT
        [Win32]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
        $width = $rect.Right - $rect.Left
        $height = $rect.Bottom - $rect.Top
        if ($width -le 0 -or $height -le 0) {{ exit 3 }}
        Add-Type -AssemblyName System.Drawing
        Add-Type -AssemblyName System.Windows.Forms
        $bmp = New-Object System.Drawing.Bitmap $width, $height
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
        $g.Dispose()
        $bmp.Save("{str(out_path)}", [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        exit 0
    }} catch {{ exit 4 }}
    """
        )

        # Write script to a temp file to avoid quoting issues
        try:
            with tempfile.NamedTemporaryFile("w", suffix=".ps1", delete=False) as tf:
                tf.write(ps_script)
                tfname = tf.name
        except Exception:
            return False

        try:
            # Run PowerShell to execute the script
            cmd = ["pwsh", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", tfname]
            res = subprocess.run(
                cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=10
            )
            if res.returncode == 0 and out_path.exists():
                return True
            return False
        except Exception:
            return False
        finally:
            try:
                os.unlink(tfname)
            except Exception:
                pass

    """Run static checks on Forms and ViewModels according to docs/view-completeness.md.
    Returns (passed: bool, details: dict) where passed is True only if all required checks pass.
    """
    # 're' is already imported at module level; no need to re-import here.

    details = {}
    passed_all = True

    # locate forms and viewmodels
    possible_forms = [
        repo_root / "src" / "WileyWidget.WinForms" / "Forms",
        repo_root / "WileyWidget.WinForms" / "Forms",
    ]
    possible_vms = [
        repo_root / "src" / "WileyWidget.WinForms" / "ViewModels",
        repo_root / "WileyWidget.WinForms" / "ViewModels",
    ]

    forms_dir = next((p for p in possible_forms if p.exists()), None)
    vms_dir = next((p for p in possible_vms if p.exists()), None)

    print(f"Static checks — Forms dir: {forms_dir}, ViewModels dir: {vms_dir}")

    form_files = []
    vm_files = []
    if forms_dir:
        form_files = [
            f
            for f in sorted(forms_dir.rglob("*Form.cs"))
            if "Designer.cs" not in f.name
        ]
    if vms_dir:
        vm_files = [f for f in sorted(vms_dir.rglob("*ViewModel.cs"))]

    # Define required checks per checklist (a strict subset, uncheatable heuristics)
    def check_form(fpath: Path):
        text = fpath.read_text(encoding="utf-8", errors="ignore")
        res = {"path": str(fpath), "issues": []}

        # 1. InitializeComponent present
        if "InitializeComponent();" not in text:
            res["issues"].append("Missing InitializeComponent()")

        # 2. No direct ViewModel instantiation in View
        if re.search(r"=\s*new\s+\w+ViewModel\s*\(", text):
            res["issues"].append("View instantiates ViewModel (should be injected)")

        # 3. Dock/Anchor or DockingManager
        if not ("Dock =" in text or "Anchor =" in text or "DockingManager" in text):
            res["issues"].append("No Dock/Anchor or DockingManager usage")

        # 4. Data binding present
        if not (
            "BindingSource" in text
            or "DataBindings.Add" in text
            or "DataSource =" in text
        ):
            res["issues"].append("No data binding / BindingSource / DataSource usage")

        # 5. Accessibility markers
        if not (
            "AccessibleName" in text
            or "AccessibleDescription" in text
            or "AccessibleRole" in text
        ):
            res["issues"].append("Missing AccessibleName/AccessibleDescription")

        # 6. ThemeManager or SfSkinManager
        if not (
            "ThemeManager" in text or "SfSkinManager" in text or "Syncfusion" in text
        ):
            # not mandatory for tiny helper dialogs, but warn
            res["issues"].append(
                "ThemeManager/SfSkinManager/Syncfusion theming not detected"
            )

        # 7. No hard-coded UI literals in InitializeComponent (simple heuristic)
        init_block = ""
        m = re.search(r"void\s+InitializeComponent\s*\([\s\S]*?\}\n\s*\}", text)
        if m:
            init_block = m.group(0)
        literals = re.findall(r'"([^"\\]{2,})"', init_block)
        # Count non-resource string literals (those not containing Resources.)
        bad_literals = [s for s in literals if "Resources." not in s]
        if len(bad_literals) > 5:
            res["issues"].append(
                f"Too many hard-coded strings in InitializeComponent ({len(bad_literals)})"
            )

        # 8. No direct DB/service calls in view
        if re.search(
            r"\b(DbContext|UnitOfWork|WileyWidget\.Business|WileyWidget\.Data)\b", text
        ):
            res["issues"].append("Direct DB/business code found in view")

        # 9. Buttons should use themed icons via IThemeIconService
        # Find button creations and check for a subsequent assignment of Image via iconService
        btn_defs = re.findall(
            r"(\w+)\s*=\s*new\s+(?:\w+\.|)?(?:Button|SfButton|ToolStripButton)\b", text
        )
        for btn in set(btn_defs):
            # look for lines assigning Image for this button
            pattern = rf"{re.escape(btn)}\s*\.\s*Image\s*=\s*(?:iconService|.*GetIcon)"
            if not re.search(pattern, text):
                res["issues"].append(
                    f'Button "{btn}" missing themed icon assignment (iconService.GetIcon)'
                )

        return res

    def check_vm(fpath: Path):
        text = fpath.read_text(encoding="utf-8", errors="ignore")
        res = {"path": str(fpath), "issues": []}

        # 1. Implements INotifyPropertyChanged or uses ObservableProperty/ObservableObject
        if not (
            "INotifyPropertyChanged" in text
            or "[ObservableProperty]" in text
            or "ObservableObject" in text
            or "ObservableRecipient" in text
        ):
            res["issues"].append(
                "ViewModel does not implement INotifyPropertyChanged / ObservableProperty"
            )

        # 2. Contains at least one RelayCommand / AsyncRelayCommand / ICommand
        if not re.search(
            r"RelayCommand|AsyncRelayCommand|ICommand|\[RelayCommand\]", text
        ):
            res["issues"].append(
                "No commands found in ViewModel (RelayCommand/AsyncRelayCommand/ICommand)"
            )

        # 3. Async loading or await usage
        if not ("async" in text or "await" in text or re.search(r"Load\w*Async", text)):
            res["issues"].append("No async/await load patterns detected in ViewModel")

        # 4. Exposes IsLoading or similar property
        if not re.search(r"IsLoading|isLoading|Busy|IsBusy", text):
            res["issues"].append("No IsLoading/IsBusy property in ViewModel")

        # 5. Logging / ILogger injection
        if not ("ILogger<" in text or "_logger" in text or "Serilog" in text):
            res["issues"].append("No ILogger usage or logging detected in ViewModel")

        # 6. Validation (FluentValidation or ObservableValidator)
        if not (
            "FluentValidation" in text
            or "ObservableValidator" in text
            or "Validation" in text
            or "IValidatableObject" in text
        ):
            res["issues"].append("No validation patterns detected in ViewModel")

        # 7. CancellationToken support on long-running ops
        if not re.search(r"CancellationToken", text):
            res["issues"].append("No CancellationToken support in async methods")

        return res

    form_results = [check_form(f) for f in form_files]
    vm_results = [check_vm(f) for f in vm_files]

    details["forms"] = form_results
    details["viewmodels"] = vm_results

    # Determine overall pass (strict: no issues allowed)
    for r in form_results + vm_results:
        if r["issues"]:
            passed_all = False

    return passed_all, details


def main_entry():
    repo = Path(__file__).parent.parent.parent
    # First run the runtime audit (build & smoke tests)
    try:
        runtime_score, runtime_issues = audit()
    except Exception as e:
        runtime_score, runtime_issues = 0, [f"Runtime audit error: {e}"]

    # Then run static checks
    static_passed, static_details = run_static_view_checks(repo)

    # Print static details to terminal for immediate visibility
    try:
        print("\nStatic check details:")
        forms = (
            static_details.get("forms", []) if isinstance(static_details, dict) else []
        )
        if forms:
            print("\nForms:")
            for f in forms:
                issues = f.get("issues", [])
                if issues:
                    print(f" - {f.get('path')}")
                    for it in issues:
                        print(f"    • {it}")
                else:
                    print(f" - {f.get('path')} — OK")

        vms = (
            static_details.get("viewmodels", [])
            if isinstance(static_details, dict)
            else []
        )
        if vms:
            print("\nViewModels:")
            for v in vms:
                issues = v.get("issues", [])
                if issues:
                    print(f" - {v.get('path')}")
                    for it in issues:
                        print(f"    • {it}")
                else:
                    print(f" - {v.get('path')} — OK")
    except Exception as e:
        print(f"Failed to pretty-print static details: {e}")

    # Compose report
    out = Path.cwd() / "mega-audit-report.json"
    report = {
        "runtime": {"score": runtime_score, "issues": runtime_issues},
        "static": {"passed": static_passed, "details": static_details},
    }
    try:
        out.write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(f"Audit report written to {out}")
    except Exception as e:
        print(f"Failed to write JSON report: {e}")

    # Fail CI if either runtime score is <100 or static checks failed
    if runtime_score < 100 or not static_passed:
        print("\nMEGA AUDIT: FAIL — see mega-audit-report.json for details")
        raise SystemExit(2)
    print("\nMEGA AUDIT: ALL CHECKS PASSED")


if __name__ == "__main__":
    main_entry()


if __name__ == "__main__":
    audit()
