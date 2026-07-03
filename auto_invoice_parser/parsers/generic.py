import re
from typing import List, Dict

EAN_REGEX = re.compile(r"\b\d{13}\b")
PRICE_REGEX = re.compile(r"\d+[\.,]\d{2}")

def normalize_number(s: str) -> str:
    return s.replace(',', '.').replace('\u00A0', '').strip()

def find_ean(text: str):
    m = EAN_REGEX.search(text)
    return m.group(0) if m else None

def simple_line_parser(page_text: str) -> List[Dict]:
    items = []
    lines = [l for l in page_text.splitlines() if l.strip()]

    for line in lines:
        ean = find_ean(line)
        if re.match(r"^\s*\d+\s", line) or ean:
            prices = PRICE_REGEX.findall(line)
            item = {
                "raw": line,
                "ean": ean,
                "sku": None,
                "description": None,
                "qty": None,
                "unit_price": None,
                "total": None
            }
            parts = line.split()
            if re.match(r"^\s*\d+\s", line) and len(parts) > 1:
                item['sku'] = parts[1]

            qty_search = re.search(r"(\d+[\,\.]?\d*)\s*(ST|PC|KG|PAC|PAX)?", line)
            if qty_search:
                item['qty'] = normalize_number(qty_search.group(1))

            if prices:
                item['unit_price'] = normalize_number(prices[-1])

            items.append(item)

    return items

def parse(pdf_raw: Dict) -> Dict:
    items = []
    for p in pdf_raw['pages']:
        items += simple_line_parser(p['text'])
    return {"items": items, "metadata": {"page_count": pdf_raw['page_count']}}
