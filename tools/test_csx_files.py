#!/usr/bin/env python3
from pathlib import Path

repo_root = Path("C:/Users/biges/Desktop/Wiley_Widget")
csx_files = list(repo_root.rglob("*.csx"))

print(f"Found {len(csx_files)} .csx files using rglob")
print("\nFirst 10 .csx files:")
for f in csx_files[:10]:
    rel_path = f.relative_to(repo_root)
    print(f"  {rel_path}")

# Check specifically in scripts/examples/csharp
csharp_dir = repo_root / "scripts" / "examples" / "csharp"
if csharp_dir.exists():
    csx_in_csharp = list(csharp_dir.glob("*.csx"))
    print(f"\n.csx files in scripts/examples/csharp: {len(csx_in_csharp)}")
    for f in csx_in_csharp[:5]:
        print(f"  {f.name}")
else:
    print("\nscripts/examples/csharp directory not found!")
