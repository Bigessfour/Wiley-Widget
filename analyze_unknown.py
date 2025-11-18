import json

with open("ai-fetchable-manifest.json", "r") as f:
    data = json.load(f)

unknown_files = [f for f in data["files"] if f.get("category") == "unknown"]
print(f"Found {len(unknown_files)} unknown files:")
for f in unknown_files[:20]:  # Show first 20
    print(f'  {f["path"]} - {f.get("language", "no lang")}')
