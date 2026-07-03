import re
from typing import Dict, List

def parse(pdf_raw: Dict) -> Dict:
    items = []

    for p in pdf_raw['pages']:
        lines = [l for l in p['text'].splitlines() if l.strip()]

        for i, line in enumerate(lines):
            if re.match(r"^\s*\d+\s", line):
                block = line
                for j in range(1,3):
                    if i+j < len(lines) and not re.match(r"^\s*\d+\s", lines[i+j]):
                        block += ' ' + lines[i+j]

                m_art = re.search(r"Artikel:\s*(\w+)", block)
                sku = m_art.group(1) if m_art else None

                m_ean = re.search(r"(\d{13})", block)
                ean = m_ean.group(1) if m_ean else None

                m_qty = re.search(r"(\d+)\s*(PAC|PC|ST|KG)?", block)
                qty = int(m_qty.group(1)) if m_qty else None

                m_prices = re.findall(r"(\d+[\.,]\d{2})", block)
                unit_price = None
                total = None
                if m_prices:
                    if len(m_prices) >= 2:
                        unit_price = m_prices[-2]
                        total = m_prices[-1]
                    else:
                        unit_price = m_prices[-1]

                items.append({
                    "sku": sku,
                    "ean": ean,
                    "description": block,
                    "qty": qty,
                    "unit_price": unit_price,
                    "line_total": total
                })

    return {"items": items}
