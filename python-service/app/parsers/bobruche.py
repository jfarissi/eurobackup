"""
Parser pour les factures Bobrush (Bobruche).

Layout PDF typique (extraction texte par colonnes) :
  - Paires description + "Barcode: <ean>"
  - Colonne "N STUKS"
  - Colonne Eprijs (prix unitaire)
  - Colonne remise (%)
  - Colonne Prijs (total ligne)
  - Colonne Artikel (référence fournisseur)
"""

import re
from typing import Dict, List, Optional, Tuple

BARCODE_RE = re.compile(r"^Barcode:\s*(\d{8,14})\s*$", re.IGNORECASE)
QTY_RE = re.compile(r"^(\d+)\s+STUKS$", re.IGNORECASE)
MONEY_RE = re.compile(r"^\d+,\d{2}$")
DISCOUNT_RE = re.compile(r"^\d+%$")
PAGE_MARKER_RE = re.compile(r"^(\d+)/(\d+)$")
SKU_RE = re.compile(r"^[\w./,-]{2,20}$")

FOOTER_MARKERS = (
    "bobrush bv",
    "algemene verkoopsvoorwaarden",
    "fortis ",
    "iban ",
    "bic ",
    "kbc ",
    "btw-tva",
)


def _to_float(value: Optional[str]) -> Optional[float]:
    if value is None:
        return None
    try:
        return float(str(value).replace(",", ".").strip())
    except ValueError:
        return None


def _is_page_marker(line: str) -> bool:
    m = PAGE_MARKER_RE.match(line.strip())
    if not m:
        return False
    page, total = int(m.group(1)), int(m.group(2))
    return page <= total and total <= 30 and page < 20


def _is_footer_line(line: str) -> bool:
    low = line.lower().strip()
    return any(marker in low for marker in FOOTER_MARKERS)


def _is_sku_line(line: str) -> bool:
    s = line.strip()
    if not s or _is_footer_line(s) or _is_page_marker(s):
        return False
    if BARCODE_RE.match(s) or QTY_RE.match(s) or MONEY_RE.match(s) or DISCOUNT_RE.match(s):
        return False
    if re.search(r"@|www\.|http|tel:|t:|e:", s, re.IGNORECASE):
        return False
    return bool(SKU_RE.match(s))


def _split_pages(lines: List[str]) -> List[List[str]]:
    """Découpe le document en blocs produits (entre en-têtes répétés / pieds de page)."""
    pages: List[List[str]] = []
    current: List[str] = []
    seen_products = False

    for line in lines:
        if _is_footer_line(line) and current:
            if any(BARCODE_RE.match(l) for l in current):
                pages.append(current)
            current = []
            seen_products = False
            continue

        if line.strip() == "Factuur" and seen_products and current:
            if any(BARCODE_RE.match(l) for l in current):
                pages.append(current)
            current = [line]
            continue

        current.append(line)
        if BARCODE_RE.match(line):
            seen_products = True

    if current and any(BARCODE_RE.match(l) for l in current):
        pages.append(current)

    if pages:
        return pages

    # Fallback : tout le document comme une seule page.
    return [lines]


def _parse_column_block(col_lines: List[str]) -> Tuple[List[int], List[float], List[str], List[float], List[str]]:
    qtys: List[int] = []
    unit_prices: List[float] = []
    discounts: List[str] = []
    totals: List[float] = []
    skus: List[str] = []

    i = 0
    while i < len(col_lines) and QTY_RE.match(col_lines[i]):
        qtys.append(int(QTY_RE.match(col_lines[i]).group(1)))
        i += 1

    while i < len(col_lines) and MONEY_RE.match(col_lines[i]):
        unit_prices.append(_to_float(col_lines[i]) or 0.0)
        i += 1

    while i < len(col_lines) and DISCOUNT_RE.match(col_lines[i]):
        discounts.append(col_lines[i])
        i += 1

    while i < len(col_lines) and MONEY_RE.match(col_lines[i]):
        totals.append(_to_float(col_lines[i]) or 0.0)
        i += 1

    while i < len(col_lines):
        line = col_lines[i].strip()
        if _is_footer_line(line) or _is_page_marker(line):
            break
        if _is_sku_line(line):
            skus.append(line)
        i += 1

    return qtys, unit_prices, discounts, totals, skus


def _parse_page(lines: List[str]) -> List[Dict]:
    products: List[Dict] = []
    barcode_rows: List[Tuple[str, str]] = []

    for idx, line in enumerate(lines):
        m = BARCODE_RE.match(line)
        if not m:
            continue
        desc = lines[idx - 1].strip() if idx > 0 else ""
        if desc.lower().startswith("barcode:"):
            desc = ""
        barcode_rows.append((desc, m.group(1)))

    if not barcode_rows:
        return products

    try:
        stuks_idx = next(i for i, l in enumerate(lines) if QTY_RE.match(l))
    except StopIteration:
        return products

    qtys, unit_prices, discounts, totals, skus = _parse_column_block(lines[stuks_idx:])
    n = len(barcode_rows)

    for i in range(n):
        desc, ean = barcode_rows[i]
        qty = qtys[i] if i < len(qtys) else 0
        unit_price = unit_prices[i] if i < len(unit_prices) else None
        line_total = totals[i] if i < len(totals) else None
        sku = skus[i] if i < len(skus) else None

        products.append(
            {
                "sku": sku or "",
                "ean": ean,
                "description": desc,
                "qty": float(qty),
                "unit": "ST",
                "unit_price": unit_price,
                "line_total": line_total,
                "discount": discounts[i] if i < len(discounts) else None,
            }
        )

    return products


def parse_items(text: str) -> List[Dict]:
    lines = [ln.strip() for ln in (text or "").splitlines() if ln and ln.strip()]
    items: List[Dict] = []
    for page in _split_pages(lines):
        items.extend(_parse_page(page))
    return items


def extract_number(text: str) -> Optional[str]:
    m = re.search(r"\b(VK\d{5,})\b", text, re.IGNORECASE)
    if m:
        return m.group(1).upper()
    m = re.search(r"factuur[^\n]{0,40}\n\s*([A-Z]{2}\d{5,})", text, re.IGNORECASE)
    return m.group(1).upper() if m else None


def extract_date(text: str) -> Optional[str]:
    m = re.search(r"Datum\s*\n\s*(\d{2}/\d{2}/\d{2,4})", text, re.IGNORECASE)
    if m:
        return m.group(1)
    m = re.search(r"\b(\d{2}/\d{2}/\d{2,4})\b", text)
    return m.group(1) if m else None


def extract_client(text: str) -> Optional[str]:
    m = re.search(r"Klant\s*\n\s*([A-Z0-9]+)\s*\n\s*(.+)", text, re.IGNORECASE)
    if m:
        return f"{m.group(1)} {m.group(2).strip()}".strip()
    m = re.search(r"(EURO\s*BRICO[^\n]*)", text, re.IGNORECASE)
    return m.group(1).strip() if m else None


def parse(pdf_raw: Dict) -> Dict:
    text = pdf_raw.get("full_text", "") or pdf_raw.get("text", "") or ""
    items = parse_items(text)

    metadata = {
        "type": "Invoice",
        "doc_type": "invoice",
        "number": extract_number(text),
        "date": extract_date(text),
        "client": extract_client(text),
        "supplier": "Bobrush",
        "supplier_email": "info@bobrush.be",
        "count": len(items),
        "method": "bobruche_v1",
    }

    return {"items": items, "metadata": metadata}
