import chardet

file_path = (
    r"c:\Users\biges\Desktop\Wiley_Widget\scripts\profile-syncfusion-license-init.ps1"
)

with open(file_path, "rb") as f:
    raw_data = f.read()

result = chardet.detect(raw_data)
print(f"Encoding: {result['encoding']}")
print(f"Confidence: {result['confidence']}")

# Check for BOM
if raw_data.startswith(b"\xef\xbb\xbf"):
    print("File has UTF-8 BOM")
elif raw_data.startswith(b"\xff\xfe"):
    print("File has UTF-16 LE BOM")
elif raw_data.startswith(b"\xfe\xff"):
    print("File has UTF-16 BE BOM")
else:
    print("No BOM detected")

# Check first few bytes
print(f"First 10 bytes: {raw_data[:10]}")
