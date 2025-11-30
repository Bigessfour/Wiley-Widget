import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

# scripts/tools/sql_enum_audit_v2.py is a standalone script (not a module)
# so call it via subprocess the same way we invoke the fixer below.


def test_bulk_fix_and_audit(tmp_path: Path):
    # Always find the repository root reliably relative to this test file
    repo_root = Path(__file__).resolve().parents[1]
    src_sql = repo_root / "sql"

    # copy sql folder to temporary location
    target = tmp_path / "sql"
    # copy the repo sql folder to a temporary target; allow existing dirs
    shutil.copytree(src_sql, target, dirs_exist_ok=True)

    # copy mappings to temp scripts/tools
    src_map = repo_root / "scripts" / "tools" / "sql_enum_mappings.json"
    target_map_dir = tmp_path / "scripts" / "tools"
    target_map_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src_map, target_map_dir / "sql_enum_mappings.json")

    # run the fixer script against our temporary copy
    fixer = repo_root / "scripts" / "tools" / "fix_seed_enums.py"
    res = subprocess.run(
        [sys.executable, str(fixer), "--root", str(tmp_path)],
        capture_output=True,
        text=True,
    )
    assert res.returncode == 0, f"Fixer failed: {res.stderr}\n{res.stdout}"

    # run the auditor against the temp copy
    # Run the auditor as a subprocess (script has CLI args similar to fixer)
    mapping = target_map_dir / "sql_enum_mappings.json"
    out_dir = tmp_path / "logs"
    out_dir.mkdir(parents=True, exist_ok=True)
    auditor = repo_root / "scripts" / "tools" / "sql_enum_audit_v2.py"
    audit_res = subprocess.run(
        [
            sys.executable,
            str(auditor),
            "--root",
            str(tmp_path),
            "--mappings",
            str(mapping),
            "--out",
            str(out_dir),
        ],
        capture_output=True,
        text=True,
    )
    # auditor may return a non-zero number of findings (exit code),
    # but it should emit an audit JSON in the --out directory; assert that
    # the file exists and is parseable.
    audit_json = out_dir / "sql_enum_audit_v2.json"
    assert (
        audit_json.exists()
    ), f"Auditor didn't produce expected JSON at {audit_json}\nstdout:{audit_res.stdout}\nstderr:{audit_res.stderr}"
    audit_data = json.loads(audit_json.read_text(encoding="utf-8"))
    assert isinstance(
        audit_data, (list, dict)
    ), "Audit JSON did not parse into a list or dict"

    # ensure the largest seed file uses numeric Type/Fund values (no literal 'Expenditure'/'Revenue'/'Expense')
    seed_fy = tmp_path / "sql" / "seed_fy26_budget_data.sql"
    seed_text = seed_fy.read_text(encoding="utf-8")
    # No textual enums remain
    assert "'Expenditure'" not in seed_text
    assert "'Revenue'" not in seed_text
    assert "'Expense'" not in seed_text

    # ensure that a set of important files were updated and have backups created
    # backups should be created for updated SQL files; discover dynamically
    sql_dir = tmp_path / "sql"
    backups = list(sql_dir.glob("*.sql.bak"))
    assert len(backups) >= 6, f"Expected 6+ backups, found {len(backups)}: {backups}"

    # Optional / smoke-check: ensure numeric Type/Fund tuples appear in the seed file
    # Example pattern: (..., <fund>, <type>, ...). We'll do a few loose checks.
    # There should be numeric Type/Fund tuples in the seed file —
    # e.g. (123, 0, 1, ...) or (123, 0, 0, ...). Ensure at least one
    # numeric tuple for Type (0 or 1) exists and that expense (1) tuples appear.
    assert re.search(
        r"\(\d+,\s*\d+,\s*1,", seed_text
    ), "No Expense (Type=1) numeric tuple found"
    assert re.search(
        r"\(\d+,\s*\d+,\s*[01],", seed_text
    ), "No numeric Type/Fund tuple found"
