import re
from typing import Dict

def parse(pdf_raw: Dict) -> Dict:
    items = []
    for p in pdf_raw['pages']:
        lines = [l for l in p['text'].splitlines() if l.strip()]
        for i, line in enumerate(lines):
            if re.match(r"^\s*\d+\s", line):
                items.append({
                    "sku": None,
                    "ean": None,
                    "description": line,
                    "qty": None,
                    "unit_price": None,
                    "line_total": None
                })
    return {"items": items}
