import sys
import os
import fitz  # PyMuPDF

# Use the temp file path from the previous log
# C:\Users\Lenovo\AppData\Local\Temp\9b5fa582bd9a426ea8b1f20b1fa53cd5_coeck_assortimentsboek_bouw_2026_v01_web.pdf
# or
# C:\Users\Lenovo\AppData\Local\Temp\ec25500df08648a7a08a6ca617ed3d46_coeck_assortimentsboek_bouw_2026_v01_web.pdf

# We'll try to find the latest PDF path from debug_trace.log
log_path = "d:\\GitHub\\Backup.Web.Api\\python-service\\debug_trace.log"
pdf_path = None

if os.path.exists(log_path):
    with open(log_path, "r") as f:
        # Read lines in reverse order to find the last "START parsing:"
        lines = f.readlines()
        for line in reversed(lines):
            if "START parsing:" in line:
                # Extract path: [timestamp] START parsing: PATH
                parts = line.split("START parsing: ")
                if len(parts) > 1:
                    pdf_path = parts[1].strip()
                    break

if not pdf_path or not os.path.exists(pdf_path):
    print(f"PDF not found from log: {pdf_path}")
    # Fallback to listing temp dir
    temp_dir = os.environ.get("TEMP", "C:\\Users\\Lenovo\\AppData\\Local\\Temp")
    pdf_files = [f for f in os.listdir(temp_dir) if f.endswith(".pdf") and "coeck" in f.lower()]
    
    if pdf_files:
        latest_pdf = sorted(pdf_files, key=lambda x: os.path.getmtime(os.path.join(temp_dir, x)))[-1]
        pdf_path = os.path.join(temp_dir, latest_pdf)

if not pdf_path or not os.path.exists(pdf_path):
    print("No VALID Coeck PDF found.")
    sys.exit(1)

print(f"Analyzing PDF: {pdf_path}")

doc = fitz.open(pdf_path)
search_terms = ["Betonmortel is een industrieel", "Le mortier de béton est un", "102"]

found = False
for page_num, page in enumerate(doc):
    text = page.get_text("text")
    for term in search_terms:
        if term in text:
            print(f"--- Found '{term}' on Page {page_num + 1} ---")
            # Print context (surrounding lines)
            lines = text.split('\n')
            for i, line in enumerate(lines):
                if term in line:
                    start = max(0, i - 5)
                    end = min(len(lines), i + 10)
                    print(f"Context lines {start}-{end}:")
                    for j in range(start, end):
                        prefix = ">> " if j == i else "   "
                        print(f"{prefix}{lines[j]}")
            found = True

if not found:
    print("Search terms not found in PDF text layer.")
