import sys
import os
import traceback

# Add the project root to sys.path so imports work
# Assuming this script is in d:\GitHub\Backup.Web.Api\python-service
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

print("--- DIAGNOSTIC START ---")

print("1. Testing import of app.parsers.coek")
try:
    from app.parsers import coek
    print("   [OK] app.parsers.coek imported successfully")
except Exception as e:
    print(f"   [FAIL] Error importing app.parsers.coek: {e}")
    traceback.print_exc()

print("\n2. Testing import of app.parsers.coek_parser")
try:
    from app.parsers import coek_parser
    print("   [OK] app.parsers.coek_parser imported successfully")
except Exception as e:
    print(f"   [FAIL] Error importing app.parsers.coek_parser: {e}")
    traceback.print_exc()

print("\n3. Testing instantiation of CoekParser (mocking BaseParser)")
try:
    # Need to check if we can instantiate it without a real PDF
    # BaseParser likely needs a file path. We'll give it a dummy one.
    # We might fail on __init__ but we want to see IF the class exists and imports.
    from app.parsers.coek_parser import CoekParser
    print("   [OK] CoekParser class found")
except Exception as e:
    print(f"   [FAIL] Error accessing CoekParser class: {e}")
    traceback.print_exc()

print("--- DIAGNOSTIC END ---")
