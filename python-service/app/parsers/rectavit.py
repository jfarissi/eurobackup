"""
Parser pour les factures Rectavit N.V. (Lochristi).

Layout par bloc :
  Materiaal (6 chiffres)
  Hoeveelheid (ex. 12 ST)
  Prijs (ex. 3,68 EUR / 1 ST)
  Korting (ex. 10,00 %) — optionnel, peut être multiple
  Bedrag(EUR) (ex. 44,16 ou 1.604,88)
  BTW (ex. 21,00 %)
  Description (ligne suivante)
"""

import re
from typing import Dict, List, Optional

MATERIAL_RE = re.compile(r"^\d{6}$")
QTY_RE = re.compile(r"^\s*(\d+)\s+ST\s*$", re.IGNORECASE)
UNIT_PRICE_RE = re.compile(
    r"^\s*([\d.,]+)\s+EUR\s*/\s*\d+\s+ST\s*$",
    re.IGNORECASE,
)
PERCENT_RE = re.compile(r"^\s*([\d.,]+)\s*%\s*$")
AMOUNT_RE = re.compile(r"^\s*([\d.,]+)\s*$")

NOISE_PREFIXES = (
    "order n",
    "besteldatum:",
    "leverbon:",
    "leverdatum:",
    "uw order n",
    "zending:",
    "factuur n",
    "pagina ",
    "materiaal",
    "hoeveelheid",
    "prijs",
    "korting",
    "bedrag",
    "btw",
    "rectavit n.v",
    "ambachtenlaan",
    "info@rectavit",
    "ing be",
    "kbc be",
    "nr of euro",
    "nr of half",
    "bruto bedrag",
    "netto bedrag",
    "basis",
    "btw totaal",
    "totaal",
    "algemene verkoop",
)

FOOTER_CONTAINS = (
    "rectavit n.v",
    "ambachtenlaan 4",
    "info@rectavit.be",
    "lochristi-belgium",
    "tel: +32",
    "fax: +32",
)


def _is_noise_line(line: str) -> bool:
    low = line.lower().strip()
    if not low:
        return True
    if any(low.startswith(p) for p in NOISE_PREFIXES):
        return True
    return any(m in low for m in FOOTER_CONTAINS)


def _to_float(value: Optional[str]) -> Optional[float]:
    if value is None:
        return None
    s = str(value).strip().replace(" ", "")
    if not s:
        return None
    if "," in s and "." in s:
        s = s.replace(".", "").replace(",", ".")
    elif "," in s:
        s = s.replace(",", ".")
    try:
        return float(s)
    except ValueError:
        return None


def _find_material_indices(lines: List[str]) -> List[int]:
    return [i for i, line in enumerate(lines) if MATERIAL_RE.match(line.strip())]


def _parse_block(lines: List[str], start: int, end: int) -> Optional[Dict]:
    material = lines[start].strip()
    if not MATERIAL_RE.match(material):
        return None

    i = start + 1
    if i >= end:
        return None

    qty_match = QTY_RE.match(lines[i])
    if not qty_match:
        return None
    qty = _to_float(qty_match.group(1)) or 0.0
    i += 1

    if i >= end:
        return None
    price_match = UNIT_PRICE_RE.match(lines[i])
    if not price_match:
        return None
    unit_price = _to_float(price_match.group(1))
    i += 1

    percentages: List[str] = []
    amounts: List[float] = []
    description: Optional[str] = None

    while i < end:
        line = lines[i].strip()
        if _is_noise_line(line):
            i += 1
            continue
        if MATERIAL_RE.match(line):
            break
        if line.lower().startswith("order n"):
            break

        pct_match = PERCENT_RE.match(line)
        if pct_match:
            percentages.append(pct_match.group(1).strip())
            i += 1
            continue

        amt_match = AMOUNT_RE.match(line)
        if amt_match and "eur" not in line.lower():
            val = _to_float(amt_match.group(1))
            if val is not None:
                amounts.append(val)
            i += 1
            continue

        if not QTY_RE.match(line) and not UNIT_PRICE_RE.match(line):
            description = line
            break
        i += 1

    if description is None or not amounts:
        return None

    line_total = amounts[-1]
    vat = percentages[-1] if percentages else None
    discounts = percentages[:-1] if len(percentages) > 1 else (
        [percentages[0]] if len(percentages) == 1 and not _looks_like_vat(percentages[0]) else []
    )
    if len(percentages) == 1 and _looks_like_vat(percentages[0]):
        vat = percentages[0]
        discounts = []

    discount_str: Optional[str] = None
    if discounts:
        discount_str = " + ".join(f"{d}%" for d in discounts)

    return {
        "sku": material,
        "ean": None,
        "description": description,
        "qty": qty,
        "unit": "ST",
        "unit_price": unit_price,
        "discount": discount_str,
        "line_total": line_total,
        "vat": f"{vat}%" if vat else None,
    }


def _looks_like_vat(pct: str) -> bool:
    val = _to_float(pct)
    if val is None:
        return False
    return val in (6.0, 12.0, 21.0)


def parse_items(text: str) -> List[Dict]:
    lines = [ln.strip() for ln in (text or "").splitlines() if ln and ln.strip()]
    starts = _find_material_indices(lines)
    items: List[Dict] = []

    for idx, start in enumerate(starts):
        end = starts[idx + 1] if idx + 1 < len(starts) else len(lines)
        parsed = _parse_block(lines, start, end)
        if parsed:
            items.append(parsed)

    return items


def extract_number(text: str) -> Optional[str]:
    m = re.search(r"Factuur\s*N[°º]\s*(\d{6,})", text, re.IGNORECASE)
    if m:
        return m.group(1)
    m = re.search(r"Factuur\s*N[°º]:\s*(\d{6,})", text, re.IGNORECASE)
    return m.group(1) if m else None


def extract_date(text: str) -> Optional[str]:
    m = re.search(
        r"Factuur\s*N[°º][^\n]*\n\s*(\d{6,})\s*\n\s*(\d{2}\.\d{2}\.\d{4})",
        text,
        re.IGNORECASE,
    )
    if m:
        return m.group(2)
    m = re.search(r"\b(\d{2}\.\d{2}\.\d{4})\b", text)
    return m.group(1) if m else None


def extract_client(text: str) -> Optional[str]:
    m = re.search(r"Goederenontvanger\s+\d+\s*\n\s*([^\n]+)", text, re.IGNORECASE)
    if m:
        return m.group(1).strip()
    m = re.search(r"(EURO\s*BRICO[^\n]*)", text, re.IGNORECASE)
    return m.group(1).strip() if m else None


def extract_delivery_ref(text: str) -> Optional[str]:
    m = re.search(r"Leverbon:\s*(\d+)", text, re.IGNORECASE)
    return m.group(1) if m else None


def parse(pdf_raw: Dict) -> Dict:
    text = pdf_raw.get("full_text", "") or pdf_raw.get("text", "") or ""
    items = parse_items(text)

    metadata = {
        "type": "Invoice",
        "doc_type": "invoice",
        "number": extract_number(text),
        "date": extract_date(text),
        "client": extract_client(text),
        "delivery_ref": extract_delivery_ref(text),
        "supplier": "Rectavit",
        "supplier_email": "info@rectavit.be",
        "supplier_address": "Ambachtenlaan 4, 9080 Lochristi",
        "count": len(items),
        "method": "rectavit_v1",
    }

    return {"items": items, "metadata": metadata}
