#!/usr/bin/env python3
"""
Scan repository SQL files for INSERT/MERGE statements that include string literals for columns commonly mapped to enums (Type, Fund, Status).
Writes a simple JSON report and prints results to stdout.
"""
import json
import re
from pathlib import Path


def find_repo_root(start: Path, markers=None, max_up=6) -> Path:
    if markers is None:
        markers = ["WileyWidget.sln", "Wiley-Widget.csproj", ".git", "sql", "README.md"]
    cur = start
    steps = 0
    while True:
        for m in markers:
            if (cur / m).exists():
                return cur
        if steps >= max_up or cur.parent == cur:
            break
        cur = cur.parent
        steps += 1
    try:
        return start.parents[2]
    except Exception:
        return start


root = find_repo_root(Path(__file__).resolve())
print("Scanning SQL files under", root)
patterns = [
    r"\b(Type)\b\s*,?\s*'([A-Za-z_ ]+)'",
    r"\b(Fund)\b\s*,?\s*'([A-Za-z_ ]+)'",
    r"\b(Status)\b\s*,?\s*'([A-Za-z_ ]+)'",
]
compiled = [re.compile(p, re.IGNORECASE) for p in patterns]

results = []
for f in root.rglob("*.sql"):
    try:
        text = f.read_text(encoding="utf-8")
    except Exception:
        continue
    for i, line in enumerate(text.splitlines(), start=1):
        for rx in compiled:
            m = rx.search(line)
            if m:
                results.append(
                    {"file": str(f.relative_to(root)), "line": i, "match": line.strip()}
                )

# Save report
out_path = root / "logs" / "sql_enum_audit.json"
out_path.parent.mkdir(parents=True, exist_ok=True)
out_path.write_text(json.dumps(results, indent=2), encoding="utf-8")
print(f"Found {len(results)} occurrences; report saved to {out_path}")
for r in results[:200]:
    print(r["file"], r["line"], r["match"])
