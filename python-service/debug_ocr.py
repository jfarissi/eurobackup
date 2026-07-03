import sys
import os

# Mock the logger
import logging
logging.basicConfig(level=logging.INFO)

try:
    import pytesseract
    from pdf2image import convert_from_path
    from PIL import Image
    
    print("Imports success.")
    
    tesseract_path = r"C:\Program Files\Tesseract-OCR\tesseract.exe"
    if os.path.exists(tesseract_path):
        pytesseract.pytesseract.tesseract_cmd = tesseract_path
        print(f"Tesseract path confirmed: {tesseract_path}")
    else:
        print(f"Tesseract path MISSING: {tesseract_path}")

    poppler_path = r"D:\poppler-25.12.0\Library\bin"
    if os.path.exists(poppler_path):
        print(f"Poppler path confirmed: {poppler_path}")
    else:
        print(f"Poppler path MISSING: {poppler_path}")

    try:
        langs = pytesseract.get_languages()
        if 'pol' in langs:
             print("Language 'pol' FOUND.")
        else:
             print(f"Language 'pol' MISSING! Available: {langs}")
    except Exception as e:
        print(f"Error getting languages: {e}")

except Exception as e:
    print(f"Error: {e}")
