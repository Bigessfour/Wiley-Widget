import json
from pathlib import Path
import tempfile

from scripts.tools import sql_enum_audit_v2 as audit


def test_basic_insert_and_update(tmp_path: Path):
    # create a small SQL file that contains an INSERT and UPDATE with string enum values
    sql_text = """
    -- seed departments
    INSERT INTO Departments (Id, Code, Name, Fund, ParentDepartmentId) VALUES
      (1, 'GEN', 'General Government', 'General', NULL),
      (2, 'HWY', 'Highways', 'General', NULL);

    UPDATE Departments SET Fund = 'General' WHERE Code = 'GEN';
    """

    (tmp_path / "sql").mkdir()
    fpath = tmp_path / "sql" / "seed_test.sql"
    fpath.write_text(sql_text, encoding="utf-8")

    # create mappings file
    mapping = {"columns": {"Fund": {"General": 0}}}
    map_path = tmp_path / "sql_enum_mappings.json"
    map_path.write_text(json.dumps(mapping), encoding="utf-8")

    out_dir = tmp_path / "logs"
    findings_count = audit.run_scan(tmp_path, mapping["columns"], out_dir)

    assert findings_count >= 3
    # ensure output files created
    assert (out_dir / "sql_enum_audit_v2.json").exists()
    assert (out_dir / "sql_enum_fix_suggestions.sql").exists()
