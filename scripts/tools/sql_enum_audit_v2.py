#!/usr/bin/env python3
"""sql_enum_audit_v2.py

Purpose: Robust SQL seed auditing for string-to-int enum conversions.

Features:
- Walks repository .sql files and extracts INSERT/UPDATE/MERGE occurrences that reference enum-like columns.
- Parses multi-line INSERT ... (col1,col2,...) VALUES (...) constructs and maps columns to values.
- Uses a JSON mapping (scripts/tools/sql_enum_mappings.json) for known string->int conversions.
- Produces three outputs in logs/:
  * sql_enum_audit_v2.json — detailed findings
  * sql_enum_fix_suggestions.sql — suggested UPDATE statements to fix existing DB rows (where mapping exists)
  * sql_enum_fix_todo.json — per-file suggestions for manual changes (when automatic mapping not available)

This is designed for Python 3.11 and includes careful SQL comment removal and basic tokenization of VALUES tuples.
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple


def load_mappings(path: Path) -> Dict[str, Dict[str, int]]:
    if path.exists():
        try:
            payload = json.loads(path.read_text(encoding="utf-8"))
            # Expecting {'columns': { 'Status': { 'Draft':0, ... }, ... }}
            return payload.get("columns", {})
        except Exception:
            print(
                f"Warning: failed to parse mapping file {path}, continuing with defaults"
            )
    return {}


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


def remove_sql_comments(sql: str) -> str:
    # remove block comments /* ... */
    sql_no_block = re.sub(r"/\*.*?\*/", " ", sql, flags=re.S)
    # remove line comments -- ...\n
    sql_no_line = re.sub(r"--.*?$", " ", sql_no_block, flags=re.M)
    return sql_no_line


def split_top_level_tuples(values_text: str) -> List[str]:
    """Given the part after VALUES, return a list of tuples (as strings), e.g.
    "(a,b),(c,d)" -> ["(a,b)", "(c,d)"]
    Respects nesting and quoted strings.
    """
    tuples = []
    depth = 0
    cur = []
    in_single = False
    i = 0
    while i < len(values_text):
        ch = values_text[i]
        cur.append(ch)
        if ch == "'":
            # skip escaped quotes: '' within T-SQL
            if in_single and i + 1 < len(values_text) and values_text[i + 1] == "'":
                cur.append("'")
                i += 1
            else:
                in_single = not in_single
        elif not in_single:
            if ch == "(":
                depth += 1
            elif ch == ")":
                depth -= 1
                if depth == 0:
                    # close tuple
                    t = "".join(cur).strip()
                    tuples.append(t)
                    cur = []
                    # skip possible comma and whitespace after tuple
                    # continue from next char
        i += 1
    # sanitize
    return [t for t in tuples if t.strip()]


def split_tuple_fields(tuple_text: str) -> List[str]:
    # Remove leading/trailing parentheses
    text = tuple_text.strip()
    if text.startswith("(") and text.endswith(")"):
        text = text[1:-1]

    fields = []
    cur = []
    in_single = False
    i = 0
    while i < len(text):
        ch = text[i]
        if ch == "'":
            cur.append(ch)
            # handle escaped single-quote inside string as two single quotes
            if in_single and i + 1 < len(text) and text[i + 1] == "'":
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


def extract_inserts(sql: str) -> Iterable[Tuple[str, Optional[List[str]], List[str]]]:
    """Yield (table, columns(list)|None, list_of_tuple_text) for each INSERT ... VALUES ..."""
    # find INSERT INTO <name> (opt cols) VALUES <tuples...> ; allow multiline and many tuples
    # Accept table identifiers with brackets, dots, or quoted names; capture the VALUES starting point
    insert_rx = re.compile(
        r"INSERT\s+INTO\s+([^\s(]+)\s*(\([^)]*\))?\s*VALUES\s*(\(.*?\));", re.I | re.S
    )
    for m in insert_rx.finditer(sql):
        tbl = m.group(1).strip()
        cols_raw = m.group(2)
        vals_raw = sql[m.start(3) : m.end(3)]
        columns = None
        if cols_raw:
            # strip parentheses and split by comma
            cols_text = cols_raw.strip()[1:-1]
            columns = [c.strip().strip('[]"') for c in cols_text.split(",")]
        # The regex only captured first tuple; but we want the full values list.
        # Expand forward from the match start to find the full VALUES (...) list
        after = sql[m.start(3) :]
        # read until a trailing semicolon that closes the statement (naive but works for seed files)
        stmt_end = after.find(";")
        block = after[: stmt_end + 1] if stmt_end >= 0 else after
        tuple_texts = split_top_level_tuples(block)
        yield (tbl, columns, tuple_texts)


def extract_updates(sql: str) -> Iterable[Tuple[str, List[Tuple[str, str]]]]:
    """Yield (table, [(column, value),...]) for simple UPDATE ... SET a='x', b=... statements (single row)"""
    # capture UPDATE <table> SET <clause> WHERE ...  (table can include brackets or dot prefixes)
    update_rx = re.compile(r"UPDATE\s+([^\s]+)\s+SET\s+(.*?)\s+WHERE\b", re.I | re.S)
    for m in update_rx.finditer(sql):
        table = m.group(1)
        set_clause = m.group(2)
        # split on commas outside single quotes
        parts = []
        cur = []
        in_single = False
        i = 0
        while i < len(set_clause):
            ch = set_clause[i]
            if ch == "'":
                cur.append(ch)
                if in_single and i + 1 < len(set_clause) and set_clause[i + 1] == "'":
                    cur.append("'")
                    i += 1
                else:
                    in_single = not in_single
            elif ch == "," and not in_single:
                parts.append("".join(cur).strip())
                cur = []
            else:
                cur.append(ch)
            i += 1
        if cur:
            parts.append("".join(cur).strip())
        pairs = []
        for p in parts:
            if "=" in p:
                col, val = p.split("=", 1)
                pairs.append((col.strip().strip('[]"'), val.strip()))
        yield (table, pairs)


def is_quoted_string(token: str) -> Optional[str]:
    # returns unquoted string if token is a quoted SQL string like 'Foo' or N'Foo'
    t = token.strip()
    if t.startswith("N'") and t.endswith("'"):
        return t[2:-1].replace("''", "'")
    if t.startswith("'") and t.endswith("'"):
        return t[1:-1].replace("''", "'")
    return None


def normalize_identifier(ident: str) -> str:
    return ident.strip().strip('[]"')


def run_scan(root: Path, mappings: Dict[str, Dict[str, int]], out_dir: Path) -> int:
    findings = []
    suggestions = []  # tuples (table, column, string_val, int_val)
    per_file_todo: Dict[str, List[Dict]] = {}

    sql_files = list(root.rglob("*.sql"))
    for sql_file in sql_files:
        try:
            text = sql_file.read_text(encoding="utf-8")
        except Exception:
            continue
        orig = text
        cleaned = remove_sql_comments(orig)

        # INSERTs
        for tbl, cols, tuple_texts in extract_inserts(cleaned):
            table_name = normalize_identifier(tbl)
            for ti, tup in enumerate(tuple_texts, start=1):
                fields = split_tuple_fields(tup)
                for idx, val in enumerate(fields):
                    col_name = None
                    if cols and idx < len(cols):
                        col_name = normalize_identifier(cols[idx])
                    # map column name direct, else fallback to value-aware detection
                    sval = is_quoted_string(val)
                    if sval is None:
                        continue
                    # check if the column is one we care about
                    if col_name and (col_name in mappings):
                        m_map = mappings[col_name]
                        mapped = m_map.get(sval)
                        findings.append(
                            {
                                "file": str(sql_file.relative_to(root)),
                                "type": "INSERT",
                                "table": table_name,
                                "columns": cols,
                                "tuple_index": ti,
                                "column_index": idx,
                                "column": col_name,
                                "value": sval,
                                "mapped": mapped,
                            }
                        )
                        if mapped is not None:
                            suggestions.append((table_name, col_name, sval, mapped))
                    else:
                        # try global column keys (name only)
                        simple_col = None
                        if cols and idx < len(cols):
                            simple_col = cols[idx]
                        # also check if the value itself is a known token for some mapping
                        found_mapping = None
                        found_column = None
                        # check the mappings per column name keys
                        for ckey, cmap in mappings.items():
                            if sval in cmap:
                                found_mapping = cmap[sval]
                                found_column = ckey
                                break
                        findings.append(
                            {
                                "file": str(sql_file.relative_to(root)),
                                "type": "INSERT",
                                "table": table_name,
                                "columns": cols,
                                "tuple_index": ti,
                                "column_index": idx,
                                "column": simple_col,
                                "value": sval,
                                "mapped": found_mapping,
                                "inferred_column": found_column,
                            }
                        )
                        if found_mapping is not None:
                            suggestions.append(
                                (table_name, found_column, sval, found_mapping)
                            )

        # simple UPDATEs
        for table, pairs in extract_updates(cleaned):
            for col, val in pairs:
                sval = is_quoted_string(val)
                if sval is None:
                    continue
                column = normalize_identifier(col)
                mapped = None
                m_map = mappings.get(column)
                if m_map:
                    mapped = m_map.get(sval)
                else:
                    # search mappings for value
                    for ckey, cm in mappings.items():
                        if sval in cm:
                            mapped = cm[sval]
                            column = ckey
                            break

                findings.append(
                    {
                        "file": str(sql_file.relative_to(root)),
                        "type": "UPDATE",
                        "table": normalize_identifier(table),
                        "column": column,
                        "value": sval,
                        "mapped": mapped,
                    }
                )
                if mapped is not None:
                    suggestions.append(
                        (normalize_identifier(table), column, sval, mapped)
                    )

    # deduplicate suggestions
    uniq = {}
    for table, col, sval, mapped in suggestions:
        key = (table, col, sval)
        uniq[key] = mapped

    # build SQL UPDATEs to fix seeded rows
    fix_sql_lines = [
        "-- sql_enum_fix_suggestions.sql — suggested UPDATE statements to replace textual enum values with numeric equivalents",
        "-- Run carefully — these set columns to the integer mapping where the string exists. Back up your DB first.",
        "BEGIN TRANSACTION;",
    ]
    for (table, col, sval), mapped in uniq.items():
        line = f"UPDATE {table} SET {col} = {mapped} WHERE {col} = '{sval}';"
        fix_sql_lines.append(line)

    fix_sql_lines.append("COMMIT;")

    # build per-file todo — simpler: list unmatched findings to be manually edited
    for f in findings:
        if f.get("mapped") is None:
            per_file_todo.setdefault(f["file"], []).append(f)

    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "sql_enum_audit_v2.json").write_text(
        json.dumps(findings, indent=2, default=str), encoding="utf-8"
    )
    (out_dir / "sql_enum_fix_suggestions.sql").write_text(
        "\n".join(fix_sql_lines) + "\n", encoding="utf-8"
    )
    (out_dir / "sql_enum_fix_todo.json").write_text(
        json.dumps(per_file_todo, indent=2), encoding="utf-8"
    )

    print(f"Scanned {len(sql_files)} SQL files")
    print(
        f"Findings: {len(findings)}; suggested fixes (unique): {len(uniq)}; files with unresolved values: {len(per_file_todo)}"
    )

    return len(findings)


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        default=None,
        help="Repository root (defaults to two parents of script location)",
    )
    parser.add_argument(
        "--mappings",
        default=None,
        help="Path to mappings JSON (default: scripts/tools/sql_enum_mappings.json)",
    )
    parser.add_argument("--out", default=None, help="Output logs dir (default: logs)")
    args = parser.parse_args(argv)

    script_path = Path(__file__).resolve()

    repo_root = Path(args.root) if args.root else find_repo_root(script_path)
    mappings_path = (
        Path(args.mappings)
        if args.mappings
        else script_path.parents[0] / "sql_enum_mappings.json"
    )
    out_dir = Path(args.out) if args.out else repo_root / "logs"

    mappings = load_mappings(mappings_path)
    print(f"Using mappings from {mappings_path}")
    return run_scan(repo_root, mappings, out_dir)


if __name__ == "__main__":
    raise SystemExit(main())
