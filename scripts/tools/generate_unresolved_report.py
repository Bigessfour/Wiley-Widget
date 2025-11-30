#!/usr/bin/env python3
"""Produce a human-readable summary of unresolved audit findings.

Reads logs/audit_after_fix/sql_enum_audit_v2.json and writes
logs/audit_after_fix/unresolved_summary.md with counts, top values by column
and sample file listings to help triage and mapping decisions.
"""
import json
from pathlib import Path
from collections import Counter, defaultdict

ROOT = Path(__file__).resolve().parents[2]
audit_path = ROOT / "logs" / "audit_after_fix" / "sql_enum_audit_v2.json"
out_md = ROOT / "logs" / "audit_after_fix" / "unresolved_summary.md"

if not audit_path.exists():
    print("Audit JSON not found:", audit_path)
    raise SystemExit(1)

data = json.loads(audit_path.read_text(encoding="utf-8"))

# Collect unresolved items (mapped == None)
unresolved = [d for d in data if d.get("mapped") is None]

by_column_value = defaultdict(Counter)
files_by_value = defaultdict(lambda: defaultdict(set))

for row in unresolved:
    col = row.get("column") or "<unknown>"
    val = str(row.get("value"))
    by_column_value[col][val] += 1
    files_by_value[col][val].add(row.get("file"))

lines = []
lines.append("# Unresolved enum audit summary")
lines.append("")
lines.append(f"Total findings: {len(data)}  ")
lines.append(f"Unresolved / requiring review: {len(unresolved)}  ")
lines.append("")

for col, counter in sorted(by_column_value.items(), key=lambda t: -sum(t[1].values())):
    total = sum(counter.values())
    lines.append(f"## Column: {col} — {total} unresolved occurrences")
    lines.append("")
    lines.append("Top candidate literal values (value — count):")
    for val, cnt in counter.most_common(12):
        sample_files = list(files_by_value[col][val])[:5]
        sample_files_txt = ", ".join(sample_files)
        # short values
        val_short = val.strip()[:120]
        lines.append(
            f"- `{val_short}` — {cnt} occurrences — sample files: {sample_files_txt}"
        )
    lines.append("")

lines.append("---")
lines.append("")
lines.append("**Next steps / triage**")
lines.append("")
lines.append(
    "- Review the top values per column above and decide which are true enum values and which are free text/other data."
)
lines.append(
    "- Update scripts/tools/sql_enum_mappings.json for any canonical mappings you agree to, then re-run fix_seed_enums.py + sql_enum_audit_v2.py to iterate."
)
lines.append(
    "- For ambiguous values, add them to a manual todo or fix in source SQL files after domain review."
)

out_md.parent.mkdir(parents=True, exist_ok=True)
out_md.write_text("\n".join(lines), encoding="utf-8")
print("Wrote unresolved summary to", out_md)
