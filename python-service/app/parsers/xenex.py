"""
Parser pour les factures Xenex (Kluisbergen).

Layout par bloc :
  CODE (ex. DC-MN1500)
  OMSCHRIJVING
  AANTAL
  EHP (prix unitaire)
  PER (St)
  KORTING (ex. -70%)  — optionnel
  TOTAAL
  REC/BEB (poids)      — optionnel
"""

import re
from typing import Dict, List, Optional

SKU_RE = re.compile(r"^[A-Z]{2,4}-[A-Z0-9][A-Z0-9.,-]*$")
QTY_RE = re.compile(r"^\d+$")
UNIT_RE = re.compile(r"^St$", re.IGNORECASE)
PRICE_RE = re.compile(r"^\d+\.\d{2}$")
DISCOUNT_RE = re.compile(r"^-?\d+%$")

EXACT_HEADER_LINES = {
    "code",
    "omschrijving",
    "aantal",
    "ehp",
    "per",
    "korting",
    "totaal",
    "rec/beb",
    "factuur",
    "nummer",
    "datum",
    "klant",
    "btw/ondernemings-nummer",
    "uw referentie",
    "vert",
    "pag.",
    "btw",
    "goederen",
}

FOOTER_CONTAINS = (
    "oudenaardebaan",
    "info@xenex",
    "xenex.be",
    "kluisbergen",
    "ma-vrij",
    "iban",
    "bic ",
    "rpr gent",
)


def _is_noise_line(line: str) -> bool:
    low = line.lower().strip()
    if not low:
        return True
    if low in EXACT_HEADER_LINES:
        return True
    if low.startswith("levering ") or low.startswith("order "):
        return True
    return any(m in low for m in FOOTER_CONTAINS)


def _to_float(value: Optional[str]) -> Optional[float]:
    if value is None:
        return None
    try:
        return float(str(value).replace(",", ".").strip())
    except ValueError:
        return None


def _find_sku_indices(lines: List[str]) -> List[int]:
    return [i for i, line in enumerate(lines) if SKU_RE.match(line)]


def _parse_block(lines: List[str], start: int, end: int) -> Optional[Dict]:
    sku = lines[start].strip()
    if not SKU_RE.match(sku):
        return None

    if start + 1 >= end:
        return None
    description = lines[start + 1].strip()
    if _is_noise_line(description) or SKU_RE.match(description):
        return None

    i = start + 2
    if i >= end or not QTY_RE.match(lines[i]):
        return None
    qty = _to_float(lines[i]) or 0.0
    i += 1

    if i >= end or not PRICE_RE.match(lines[i]):
        return None
    unit_price = _to_float(lines[i])
    i += 1

    if i >= end or not UNIT_RE.match(lines[i]):
        return None
    i += 1

    discount: Optional[str] = None
    line_total: Optional[float] = None

    if i < end and DISCOUNT_RE.match(lines[i]):
        discount = lines[i].strip()
        i += 1
        if i < end and PRICE_RE.match(lines[i]):
            line_total = _to_float(lines[i])
            i += 1
    elif i < end and PRICE_RE.match(lines[i]):
        line_total = _to_float(lines[i])
        i += 1

    if line_total is None:
        return None

    return {
        "sku": sku,
        "ean": None,
        "description": description,
        "qty": qty,
        "unit": "ST",
        "unit_price": unit_price,
        "discount": discount,
        "line_total": line_total,
    }


def parse_items(text: str) -> List[Dict]:
    lines = [ln.strip() for ln in (text or "").splitlines() if ln and ln.strip()]
    starts = _find_sku_indices(lines)
    items: List[Dict] = []
    seen_skus = set()

    for idx, start in enumerate(starts):
        end = starts[idx + 1] if idx + 1 < len(starts) else len(lines)
        parsed = _parse_block(lines, start, end)
        if not parsed:
            continue
        sku = parsed["sku"]
        if sku in seen_skus:
            continue
        seen_skus.add(sku)
        items.append(parsed)

    return items


def extract_number(text: str) -> Optional[str]:
    m = re.search(
        r"Factuur\s+NUMMER\s+DATUM\s+KLANT\s+BTW/ONDERNEMINGS-NUMMER\s+UW REFERENTIE\s+VERT\s+PAG\.\s+(\d{4,})",
        text,
        re.IGNORECASE | re.DOTALL,
    )
    if m:
        return m.group(1)
    lines = [ln.strip() for ln in (text or "").splitlines() if ln.strip()]
    for i, line in enumerate(lines):
        if line.upper() == "PAG." and i >= 1:
            # NUMMER est quelques lignes avant PAG. sur la 1re page
            for j in range(max(0, i - 8), i):
                if re.fullmatch(r"\d{5,}", lines[j]):
                    return lines[j]
        if line.upper() == "NUMMER" and i + 1 < len(lines):
            nxt = lines[i + 1].strip()
            if re.fullmatch(r"\d{4,}", nxt):
                return nxt
    return None


def extract_date(text: str) -> Optional[str]:
    lines = [ln.strip() for ln in (text or "").splitlines() if ln.strip()]
    for i, line in enumerate(lines):
        if line.upper() == "DATUM" and i + 1 < len(lines):
            nxt = lines[i + 1].strip()
            if re.fullmatch(r"\d{2}/\d{2}/\d{4}", nxt):
                return nxt
    m = re.search(r"\b(\d{2}/\d{2}/\d{4})\b", text)
    return m.group(1) if m else None


def extract_client(text: str) -> Optional[str]:
    m = re.search(r"(OCES\s+GROUP[^\n]*)", text, re.IGNORECASE)
    if m:
        return re.sub(r"\s+", " ", m.group(1)).strip()
    m = re.search(r"(EURO\s*BRICO[^\n]*)", text, re.IGNORECASE)
    return m.group(1).strip() if m else None


def extract_delivery_ref(text: str) -> Optional[str]:
    m = re.search(r"Levering\s+(\d+)", text, re.IGNORECASE)
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
        "supplier": "Xenex",
        "supplier_email": "info@xenex.be",
        "supplier_address": "Oudenaardebaan 64, 9690 Kluisbergen",
        "count": len(items),
        "method": "xenex_v1",
    }

    return {"items": items, "metadata": metadata}
