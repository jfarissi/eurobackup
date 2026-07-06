"""
Parser pour les factures Sadu Abrasives.

Layout PDF par bloc produit (souvent vertical) :
  [SKU] Description…
  EAN: …
  [N Per M]   (optionnel)
  N Stuk(s)
  PRIJS → KORT.% → BTW → SUBTOTAAL

Le parsing s'ancre sur chaque ligne EAN pour survivre aux sauts de page
(en-têtes / pieds de page entre EAN et colonnes prix).
"""

import re
from typing import Dict, List, Optional, Tuple

PRODUCT_START_RE = re.compile(r"^\[([^\]]+)\]\s*(.*)$")
EAN_LINE_RE = re.compile(r"^EAN\s*:\s*(\d{13})\b", re.IGNORECASE)
PACK_LINE_RE = re.compile(r"^(\d+)\s+Per\s+(\d+)\s*$", re.IGNORECASE)
QTY_STUK_RE = re.compile(r"^(\d+)\s+Stuk(?:\(s\)|s)?\s*$", re.IGNORECASE)
DECIMAL_RE = re.compile(r"^\d+,\d+$")
VAT_RE = re.compile(r"^\d+%$")
SUBTOTAL_RE = re.compile(r"^[€\u20ac]?\s*([\d.,]+)\s*$")
DISCOUNT_RE = re.compile(r"^(\d+,\d{2})$")

FOOTER_MARKERS = (
    "sadu abrasives bv",
    "maatschappelijke zetel",
    "entrepotstraat",
    "info@sadu.be",
    "www.sadu.com",
    "pagina ",
    "rpr gent",
    "bnp ",
    "kbc ",
    "ing ",
    "be0427796724",
    "be97 ",
    "be83 ",
    "be38 ",
)

TABLE_HEADER_LINES = {
    "omschrijving",
    "aantal",
    "prijs",
    "kort.%",
    "btw",
    "subtotaal",
    "_",
}


def _to_float(value: Optional[str]) -> Optional[float]:
    if value is None:
        return None
    try:
        return float(str(value).replace(",", ".").strip())
    except ValueError:
        return None


def _format_discount(raw: str) -> str:
    t = (raw or "").strip()
    if not t:
        return ""
    if t.endswith("%"):
        return t
    if DISCOUNT_RE.match(t):
        v = _to_float(t)
        if v is not None:
            if abs(v - round(v)) < 0.001:
                return f"{int(round(v))}%"
            return f"{v:g}%".replace(".", ",")
    return t


def _is_footer_line(line: str) -> bool:
    low = line.lower().strip()
    return any(m in low for m in FOOTER_MARKERS)


def _is_table_header(line: str) -> bool:
    return line.lower().strip() in TABLE_HEADER_LINES


def _is_skippable_line(line: str) -> bool:
    return _is_footer_line(line) or _is_table_header(line)


def _find_ean_indices(lines: List[str]) -> List[int]:
    return [i for i, line in enumerate(lines) if EAN_LINE_RE.match(line)]


def _scan_backward_sku_description(
    lines: List[str], ean_idx: int, min_idx: int
) -> Tuple[Optional[str], List[str]]:
    """Remonte depuis EAN pour retrouver [SKU] + description (ignore pieds/en-têtes page)."""
    sku: Optional[str] = None
    sku_line_idx: Optional[int] = None

    for j in range(ean_idx - 1, min_idx - 1, -1):
        line = lines[j]
        if _is_skippable_line(line):
            continue
        if EAN_LINE_RE.match(line):
            break
        m = PRODUCT_START_RE.match(line)
        if m:
            sku = m.group(1).strip()
            sku_line_idx = j
            break

    if sku is None or sku_line_idx is None:
        # Description sans [SKU] explicite — lignes texte avant EAN
        desc_parts: List[str] = []
        for j in range(ean_idx - 1, min_idx - 1, -1):
            line = lines[j]
            if _is_skippable_line(line) or EAN_LINE_RE.match(line):
                continue
            if PRODUCT_START_RE.match(line):
                break
            desc_parts.insert(0, line)
        return None, desc_parts

    desc_parts: List[str] = []
    m = PRODUCT_START_RE.match(lines[sku_line_idx])
    if m and (m.group(2) or "").strip():
        desc_parts.append(m.group(2).strip())

    for k in range(sku_line_idx + 1, ean_idx):
        line = lines[k]
        if _is_skippable_line(line):
            continue
        desc_parts.append(line)

    return sku, desc_parts


def _scan_forward_fields(
    lines: List[str], start_idx: int
) -> Tuple[
    Optional[float],
    Optional[float],
    Optional[int],
    Optional[float],
    Optional[str],
    Optional[float],
    int,
]:
    """
    Après la ligne EAN : pack, qty, prix, kort.%, btw, subtotaal.
    Ignore les en-têtes/pieds de page intercalés.
    Retourne aussi l'index de fin.
    """
    pack_qty: Optional[float] = None
    pack_size: Optional[int] = None
    qty: Optional[float] = None
    unit_price: Optional[float] = None
    discount: Optional[str] = None
    line_total: Optional[float] = None
    i = start_idx

    while i < len(lines):
        line = lines[i]
        if PRODUCT_START_RE.match(line) or EAN_LINE_RE.match(line):
            break
        if _is_skippable_line(line):
            i += 1
            continue

        if pack_qty is None and PACK_LINE_RE.match(line):
            pm = PACK_LINE_RE.match(line)
            pack_qty = _to_float(pm.group(1))
            pack_size = int(pm.group(2))
            i += 1
            continue

        if qty is None and QTY_STUK_RE.match(line):
            qty = _to_float(QTY_STUK_RE.match(line).group(1))
            i += 1
            continue

        if unit_price is None and DECIMAL_RE.match(line):
            unit_price = _to_float(line)
            i += 1
            continue

        if discount is None and DISCOUNT_RE.match(line):
            discount = _format_discount(line)
            i += 1
            continue

        if VAT_RE.match(line):
            i += 1
            if line_total is not None:
                break
            continue

        sub_m = SUBTOTAL_RE.match(line)
        if sub_m:
            line_total = _to_float(sub_m.group(1))
            i += 1
            break

        if line_total is None and discount is not None and DECIMAL_RE.match(line):
            line_total = _to_float(line)
            i += 1
            break

        i += 1
        if qty is not None and unit_price is not None and discount and line_total is not None:
            break
        if i - start_idx > 40:
            break

    return pack_qty, pack_size, qty, unit_price, discount, line_total, i


def _parse_from_ean(lines: List[str], ean_idx: int, min_idx: int) -> Optional[Dict]:
    ean_m = EAN_LINE_RE.match(lines[ean_idx])
    if not ean_m:
        return None
    ean = ean_m.group(1)

    sku, desc_parts = _scan_backward_sku_description(lines, ean_idx, min_idx)
    pack_qty, pack_size, qty, unit_price, discount, line_total, _ = _scan_forward_fields(
        lines, ean_idx + 1
    )

    if qty is None or unit_price is None or line_total is None:
        return None

    description = re.sub(r"\s+", " ", " ".join(desc_parts)).strip()
    if not sku:
        m = PRODUCT_START_RE.search(description)
        if m:
            sku = m.group(1).strip()
            description = re.sub(r"^\[[^\]]+\]\s*", "", description).strip()
    if not sku:
        sku = ean

    if pack_qty is not None and pack_size is not None:
        description = f"{description} ({int(pack_qty)} Per {pack_size})".strip()

    return {
        "sku": sku,
        "ean": ean,
        "description": description or sku,
        "qty": qty,
        "unit": "ST",
        "unit_price": unit_price,
        "discount": discount or "",
        "line_total": line_total,
        "pack_qty": pack_qty,
        "pack_size": pack_size,
    }


def parse_items(text: str) -> List[Dict]:
    lines = [ln.strip() for ln in (text or "").splitlines() if ln and ln.strip()]
    ean_indices = _find_ean_indices(lines)
    items: List[Dict] = []
    seen_eans = set()

    for idx, ean_idx in enumerate(ean_indices):
        min_idx = ean_indices[idx - 1] + 1 if idx > 0 else 0
        parsed = _parse_from_ean(lines, ean_idx, min_idx)
        if not parsed:
            continue
        ean = parsed["ean"]
        if ean in seen_eans:
            continue
        seen_eans.add(ean)
        items.append(parsed)

    return items


def extract_number(text: str) -> Optional[str]:
    patterns = [
        r"Factuur\s+(VK/\d{4}/\d{2}/\d+)",
        r"factuurnr\.?\s*:?\s*([A-Z0-9/._-]+)",
        r"factuur\s*nr\.?\s*:?\s*([A-Z0-9/._-]+)",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            return m.group(1).strip()
    return None


def extract_date(text: str) -> Optional[str]:
    m = re.search(
        r"Factuurdatum\s*\n\s*(\d{2}/\d{2}/\d{4})",
        text,
        re.IGNORECASE,
    )
    if m:
        return m.group(1)
    m = re.search(r"\b(\d{2}/\d{2}/\d{4})\b", text)
    return m.group(1) if m else None


def extract_client(text: str) -> Optional[str]:
    m = re.search(r"(EURO[- ]?BRICO[^\n]*)", text, re.IGNORECASE)
    return m.group(1).strip() if m else None


def extract_delivery_ref(text: str) -> Optional[str]:
    m = re.search(r"\b(SAD/OUT/\d+)\b", text, re.IGNORECASE)
    return m.group(1).upper() if m else None


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
        "supplier": "Sadu",
        "supplier_email": "info@sadu.be",
        "supplier_address": "Entrepotstraat 12, 9100 Sint-Niklaas",
        "count": len(items),
        "method": "sadu_v3",
    }

    return {"items": items, "metadata": metadata}
