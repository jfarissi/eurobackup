
import sys
import unittest
from unittest.mock import MagicMock
import os
import re

# Add path to find app module
current_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.append(current_dir)

from app.parsers import knauf

class TestKnaufBLDeepSplit(unittest.TestCase):
    def test_parse_bon_livraison_3_lines(self):
        def mock_build_lines(words_input):
            result = []
            for line_str in words_input:
                result.append([(None, line_str)])
            return result

        knauf.build_lines = mock_build_lines
        
        # Scenario from log.txt
        # Pos on line 1, SKU on line 2, EAN on line 3, Desc on line 4, Qty on line 5
        raw_lines = [
            "Van bestelling  400392682", # Noise
            "10",                      # Pos
            "545753",                  # SKU
            "5413503590100",           # EAN
            "Flex-voegmortel beige 2kg (360)", # Desc
            "8 ST",                    # Qty
            "16 KG",                   # Weight (ignored)
            "20",                      # Next Pos
            "545751",                  # Next SKU
            "5413503590094",           # Next EAN
            "Flex voegmortel beige 5kg (164)",
            "8 ST",
            "40 KG"
        ]
        
        pages = [{"words": raw_lines}]
        
        items = knauf.parse_bon_livraison(pages)
        
        print(f"\nFound {len(items)} items:")
        for item in items:
            print(f"Pos: {item.get('sku')} | EAN: {item.get('ean')} | Desc: {item.get('description')} | Qty: {item.get('qty')}")
            
        self.assertEqual(len(items), 2, "Should find 2 items")
        self.assertEqual(items[0]['sku'], "545753")
        self.assertEqual(items[0]['ean'], "5413503590100")
        self.assertEqual(items[1]['sku'], "545751")

if __name__ == '__main__':
    unittest.main()
