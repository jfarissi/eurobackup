import sys

file_path = "full.log"
try:
    with open(file_path, "rb") as f:
        content = f.read()
        decoded = content.decode('utf-8', errors='replace')
        print(decoded)
except Exception as e:
    print(f"Error reading {file_path}: {e}")
