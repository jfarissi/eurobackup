
import re
import sys
import os

# Add parent directory to path to import app
sys.path.append(os.path.join(os.path.dirname(__file__), '..'))

def run_test():
    # Simulate lines from a Knauf BL
    # Item 1: Standard (Pos SKU EAN)
    # Item 2: Missing EAN
    # Item 3: SKU with 7 digits
    # Item 4: Standard
    
    lines = [
        "10 545753 5413503590100",  # Works
        "Flex-voegmortel beige 2kg (360)",
        "8 ST 4,73 /1 ST 37,84",
        "",
        "20 1234567 5413503590101", # Might fail (SKU 7 digits)
        "Product Description 2",
        "10 ST 5,00 /1 ST 50,00",
        "",
        "30 545754",                # Might fail (Missing EAN)
        "Product Description 3",
        "5 ST 10,00 /1 ST 50,00",
        "",
        "40 545755 5413503590102",  # Should work
        "Product Description 4",
        "2 ST 20,00 /1 ST 40,00"
    ]
    
    # Mocking the build_lines logic result
    # In the real code, we work with global_lines which are (line, page_idx)
    global_lines = [(l, 0) for l in lines]
    
    # Copy pasted logic from knauf.py -> parse_bon_livraison (simplified for testing regexes)
    POS_RE = re.compile(r"^(\d{1,3})\s+(\d{4,6})\s+(\d{13})\s*$")
    
    print(f"Testing {len(lines)} lines...")
    
    count = 0
    extracted_items = []
    
    i = 0
    while i < len(global_lines):
        line, _ = global_lines[i]
        
        # Logic from parse_bon_livraison
        pos_match = POS_RE.match(line)
        
        if pos_match:
            print(f"Line {i}: MATCH - {line}")
            extracted_items.append(line)
            # Simulate skipping description/qty
            i += 1
            # logic to skip description... simplified here
            while i < len(global_lines) and not POS_RE.match(global_lines[i][0]):
                i += 1
        else:
            print(f"Line {i}: NO MATCH - {line}")
            i += 1
            
    print(f"\nFound {len(extracted_items)} items.")
    expected = 3 # With current strict regex, maybe only 1 and 4 work? Or 1?
    
    # Item 1: Matches
    # Item 2 (Pos 20): SKU 7 digits. POS_RE expects 4-6. Fails.
    # Item 3 (Pos 30): Missing EAN. POS_RE expects EAN. Fails.
    # Item 4: Matches.
    
    # So we expect 2 items if logic holds.
    
run_test()
