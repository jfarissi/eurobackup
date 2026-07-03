import sys
import os
import json

# Add parent dir to path to import app
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from app.parsers.parser_factory import create_parser

def test_file(filename):
    # Files are in Backup.Web.Api root (2 levels up from scripts)
    root_dir = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    pdf_path = os.path.join(root_dir, filename)
    
    print(f"\n--- Testing {filename} ---")
    if not os.path.exists(pdf_path):
        print(f"File not found: {pdf_path}")
        return

    try:
        parser = create_parser(pdf_path)
        print(f"Parser detected: {parser.__class__.__name__}")
        
        metadata = parser.extract_metadata()
        print("Metadata:", json.dumps(metadata, indent=2, ensure_ascii=False))
        
        products = parser.extract_products()
        print(f"Products found: {len(products)}")
        # Print first 3 products
        for p in products[:3]:
            print(json.dumps(p, ensure_ascii=False))
            
    except Exception as e:
        print(f"Error: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    test_file("facturekolorsystem.pdf")
    test_file("blkolorsystem.pdf")
