#!/usr/bin/env python3
"""
Fix seed SQL files by converting textual enum strings to numeric values using mapping JSON.

This script edits files in-place and creates a .bak file as backup.
"""
import argparse
import json
import re
from pathlib import Path

parser = argparse.ArgumentParser(
    description="Fix seed SQL files by converting textual enum strings to numeric values."
)
parser.add_argument(
    "--root",
    "-r",
    default=None,
    help="Project root or target directory to scan for sql files. Defaults to repo root.",
)


def main(argv=None) -> int:
    args = parser.parse_args(argv)

    # Determine root paths
    default_repo_root = find_repo_root(Path(__file__).resolve())
    target_root = Path(args.root).resolve() if args.root else default_repo_root
    mapping_file = target_root / "scripts" / "tools" / "sql_enum_mappings.json"
    sql_dir = target_root / "sql"

    if not mapping_file.exists():
        print("Mappings file not found at", mapping_file)
        return 2

    with mapping_file.open("r", encoding="utf8") as f:
        mapping = json.load(f)

    # Build quick lookup for Type and Fund text->int
    type_map = mapping.get("columns", {}).get("Type", {})
    fund_map = mapping.get("columns", {}).get("Fund", {})

    if not sql_dir.exists():
        print("SQL folder not found at", sql_dir)
        return 3

    # We'll walk all .sql files and replace standalone tuple tokens when possible.
    type_tokens = list(type_map.keys())
    fund_tokens = list(fund_map.keys())

    # Regexes that match tokens that are standalone values inside tuples (between commas)
    type_regex = re.compile(
        r"([,(]\s*)'(?P<t>"
        + "|".join(map(re.escape, type_tokens))
        + r")'(?=\s*(?:,|\)))",
        re.IGNORECASE,
    )
    fund_regex = re.compile(
        r"([,(]\s*)'(?P<f>"
        + "|".join(map(re.escape, fund_tokens))
        + r")'(?=\s*(?:,|\)|NULL))",
        re.IGNORECASE,
    )

    processed = 0
    for p in sql_dir.rglob("*.sql"):
        # skip backups
        if p.name.endswith(".bak"):
            continue
        data = p.read_text(encoding="utf8")
        new, orig = replace_tokens(data, p, type_map, fund_map, type_regex, fund_regex)
        if new is not None:
            bak = p.with_suffix(p.suffix + ".bak")
            bak.write_text(orig, encoding="utf8")
            p.write_text(new, encoding="utf8")
            print("Updated", p)
            print("Backup saved to", bak)
            processed += 1

    print("Files updated:", processed)
    print("Done")
    return 0


# Determine root paths
def find_repo_root(start: Path, markers=None, max_up=6) -> Path:
    """Walk upwards from `start` looking for a repository root marker.

    Markers are file or directory names (e.g. 'WileyWidget.sln', '.git', 'sql').
    Returns the first directory that contains any marker, or falls back to
    `start.parents[2]` behavior if none are found within max_up steps.
    """
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
    # best-effort fallback: previous two-levels like original behavior
    try:
        return start.parents[2]
    except Exception:
        return start


def replace_tokens(text, file_path, type_map, fund_map, type_regex, fund_regex):
    orig = text

    def repl_type(m):
        prefix = m.group(1)
        tok = m.group("t")
        # mapping is case-sensitive keys; we try direct then titlecase
        val = type_map.get(tok) or type_map.get(tok.title())
        if val is not None:
            return f"{prefix}{val}"
        return m.group(0)

    def repl_fund(m):
        prefix = m.group(1)
        tok = m.group("f")
        val = fund_map.get(tok) or fund_map.get(
            tok.replace("&", "").replace("  ", " ").title()
        )
        # If not direct match, try substring matches (e.g., 'Highways & Streets' contains 'Highway')
        if val is None:
            low = tok.lower()
            for k in fund_map.keys():
                if k.lower() in low or low in k.lower():
                    val = fund_map[k]
                    break
        if val is not None:
            return f"{prefix}{val}"
        return m.group(0)

    # We will attempt a safer strategy: only replace values in INSERT statements
    # where a columns list exists and the column index corresponds to Type/Fund/FundClass.
    def is_quoted_string(token: str) -> bool:
        t = token.strip()
        return (t.startswith("N'") and t.endswith("'")) or (
            t.startswith("'") and t.endswith("'")
        )

    # helper to unquote
    def unquote(token: str) -> str:
        t = token.strip()
        if t.startswith("N'") and t.endswith("'"):
            return t[2:-1].replace("''", "'")
        if t.startswith("'") and t.endswith("'"):
            return t[1:-1].replace("''", "'")
        return t

    # match INSERT statements that include an explicit column list
    insert_rx = re.compile(
        r"INSERT\s+INTO\s+([^\s(]+)\s*(\([^)]*\))\s*VALUES\s*(\(.*?\));", re.I | re.S
    )

    def build_tuples_block(block: str):
        # keep existing top-level tuples (naive but workable for seed files)
        # reuse a simple state machine: find '(' ... ')' at depth 1
        tuples = []
        depth = 0
        cur = []
        in_single = False
        for i, ch in enumerate(block):
            cur.append(ch)
            if ch == "'":
                if in_single and i + 1 < len(block) and block[i + 1] == "'":
                    cur.append("'")
                else:
                    in_single = not in_single
            elif not in_single:
                if ch == "(":
                    depth += 1
                elif ch == ")":
                    depth -= 1
                    if depth == 0:
                        tuples.append("".join(cur).strip())
                        cur = []
        return tuples

    def split_tuple_fields_local(tuple_text: str):
        t = tuple_text.strip()
        if t.startswith("(") and t.endswith(")"):
            t = t[1:-1]
        fields = []
        cur = []
        in_single = False
        i = 0
        while i < len(t):
            ch = t[i]
            if ch == "'":
                cur.append(ch)
                if in_single and i + 1 < len(t) and t[i + 1] == "'":
                    cur.append("'")
                    i += 1
                else:
                    in_single = not in_single
            elif ch == "," and not in_single:
                fields.append("".join(cur).strip())
                cur = []
            else:
                cur.append(ch)
            i += 1
        if cur:
            fields.append("".join(cur).strip())
        return fields

    def join_tuple_fields(fields: list) -> str:
        return "(" + ", ".join(fields) + ")"

    # operate on INSERTs with explicit column lists (safe target)
    for m in insert_rx.finditer(text):
        cols_raw = m.group(2)
        block_after = text[m.start(3) :]
        # try to find full values block until the end of the statement
        stmt_end = block_after.find(";")
        block = block_after[: stmt_end + 1] if stmt_end >= 0 else block_after
        columns = [c.strip().strip('[]"') for c in cols_raw.strip()[1:-1].split(",")]
        tuples = build_tuples_block(block)
        changed_any = False
        new_tuples = []
        for tup in tuples:
            fields = split_tuple_fields_local(tup)
            # for each column of interest, replace if it matches mapping
            for idx, col in enumerate(columns):
                cname = col.strip()
                if cname in ("Type", "Fund", "FundClass") and idx < len(fields):
                    field = fields[idx]
                    if is_quoted_string(field):
                        sval = unquote(field)
                        if cname == "Type":
                            tval = type_map.get(sval) or type_map.get(sval.title())
                            if tval is not None:
                                fields[idx] = str(tval)
                                changed_any = True
                        else:
                            fval = fund_map.get(sval) or fund_map.get(sval.title())
                            if fval is None:
                                # substring match
                                low = sval.lower()
                                for k in fund_map.keys():
                                    if k.lower() in low or low in k.lower():
                                        fval = fund_map[k]
                                        break
                            if fval is not None:
                                fields[idx] = str(fval)
                                changed_any = True
            new_tuples.append(join_tuple_fields(fields))

        if changed_any:
            # replace the first block occurrence (the block variable) with new constructed content
            new_block = ", ".join(new_tuples) + ";"
            text = text.replace(block, new_block, 1)

    # fallback: still apply previous token-based replacements (where safe)
    text = type_regex.sub(repl_type, text)
    text = fund_regex.sub(repl_fund, text)

    # Also update verification queries: Type IN ('Expenditure','Expense') -> Type IN (1)
    text = re.sub(
        r"Type\s+IN\s*\(\s*'(?i:Expenditure)'\s*,\s*'(?i:Expense)'\s*\)",
        "Type IN (1)",
        text,
    )
    text = re.sub(r"Type\s*=\s*'(?i:Revenue)'", "Type = 0", text)

    if text != orig:
        return text, orig
    return None, None

    # note: processing loop moved into main() so importing this module is side-effect free


if __name__ == "__main__":
    raise SystemExit(main())
