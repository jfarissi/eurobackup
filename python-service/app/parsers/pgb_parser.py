import re
import os
import hashlib
from typing import Dict, List, Optional

import fitz  # PyMuPDF

from .base_parser import BaseParser
from ..utils.pdf_extractor import extract_pdf_raw, extract_ocr_if_needed


class PGBParser(BaseParser):
    """Parser deterministic pour documents PGB Europe (factures + BL)."""

    SKU_LINE_RE = re.compile(r"^\s*([A-Z]{2,}[A-Z0-9]{6,})\b", re.IGNORECASE)
    QTY_UNIT_RE = re.compile(r"(\d+(?:[.,]\d+)?)\s*(ST|PCS|PC|PCE|KG|M|LTR|L|DOOS|BOX|BOITE|CARTON)\b", re.IGNORECASE)
    LOT_QTY_LINE_RE = re.compile(
        r"^\s*[A-Z0-9-]{6,}\s*/\s*(\d+(?:[.,]\d+)?)\s*(ST|PCS|PC|PCE|KG|M|LTR|L|DOOS|BOX|BOITE|CARTON)\b",
        re.IGNORECASE,
    )
    # Monetary amounts only (avoid matching VAT/IBAN-like numeric chains such as 0425.888.396).
    AMOUNT_RE = re.compile(
        r"(?<![\d.,])(\d{1,4}(?:[.,]\d{2}))(?![\d.,])\s*€?",
        re.IGNORECASE,
    )
    MAIN_SKU_RE = re.compile(r"^[A-Z]{1,6}[A-Z0-9-]{5,}$")
    LOT_LINE_RE = re.compile(
        r"^\s*([A-Z0-9-]{6,})\s*/\s*(\d+(?:[.,]\d+)?)\s*(ST|PCS|PC|PCE|KG|M|LTR|L|DOOS|BOX|BOITE|CARTON)\b",
        re.IGNORECASE,
    )
    BL_SKU_LINE_RE = re.compile(
        r"^\s*([A-Z0-9]{8,})(?:\s*/\s*batch\s+[A-Z0-9-]+(?:\s*-\s*([A-Z0-9-]{6,}))?)?(?:\s+(\d+(?:[.,]\d+)?)\s*(ST|PCS|PC|PCE|KG|M|LTR|L|DOOS|BOX|BOITE|CARTON)\b)?",
        re.IGNORECASE,
    )
    BL_SKU_BATCH_LINE_RE = re.compile(
        r"^\s*([A-Z0-9][A-Z0-9]{7,})\s*/\s*batch\s+[A-Z0-9-]+(?:\s*-\s*([A-Z0-9-]{6,}))?",
        re.IGNORECASE,
    )
    BL_SKU_TOKEN_RE = re.compile(r"\b([A-Z0-9][A-Z0-9]{7,})\b")
    EMBEDDED_POS_LINE_RE = re.compile(r"^(\d{2,4})\s+(?=[A-Za-zÀ-ÿ])")
    INVOICE_DESC_START_RE = re.compile(
        r"(?i)^(Vis\b|PFS\b|Cheville|Spaander|Draad|SMART|Boulon|Rondelle|Chev\.|Ecrou|Vis à|"
        r"Regenpijp|Nylon|ISO\b|Zelfb|Nagelplug|Raamplug|Kozijn|Lange\b|Plug\b|Snelbouw)",
    )
    TRAILING_BOX_QTY_RE = re.compile(
        r"(\d+(?:[.,]\d+)?)\s*(BOITE|CARTON|DOOS|BOX)\s*$",
        re.IGNORECASE,
    )
    BOITE_DE_RE = re.compile(r"(\d+(?:[.,]\d+)?)\s*BOITE\s*DE\b", re.IGNORECASE)
    STRICT_QTY_UNIT_LINE_RE = re.compile(
        r"^\s*(\d+(?:[.,]\d+)?)\s*(ST|PCS|PC|PCE|KG|M|LTR|L|DOOS|BOX|BOITE|CARTON)\s*$",
        re.IGNORECASE,
    )
    BOX_QTY_LINE_RE = re.compile(
        r"^\s*(\d+(?:[.,]\d+)?)\s*(BOITE|CARTON|DOOS|BOX)\b",
        re.IGNORECASE,
    )
    WEIGHT_LINE_RE = re.compile(
        r"^\s*(\d+(?:[.,]\d+)?)\s*KG\s*$",
        re.IGNORECASE,
    )
    PRICE_PER_DIVISOR_RE = re.compile(
        r"^\s*/\s*([\d.,]+)\s*(ST|PCS|PC|PCE|DOOS|BOX|BOITE|CARTON)\s*$",
        re.IGNORECASE,
    )
    PRICE_INLINE_DIVISOR_RE = re.compile(
        r"([\d.,]+)\s*€\s*/\s*([\d.,]+)\s*(ST|PCS|PC|PCE|DOOS|BOX|BOITE|CARTON)",
        re.IGNORECASE,
    )
    MULTIPLY_QTY_RE = re.compile(
        r"^\s*(\d+)\s*x\s*(\d+(?:[.,]\d+)?)\s*(ST|PCS|PC|PCE)\s*$",
        re.IGNORECASE,
    )
    PIECE_QTY_LINE_RE = re.compile(
        r"^\s*([\d.,]+)\s*(ST|PCS|PC|PCE)\s*$",
        re.IGNORECASE,
    )
    PACKAGE_TOTAL_PIECES_RE = re.compile(
        r"(?:suremb|verpakking|emballage|sachet)\b.*?\bde\s+([\d.,]+)",
        re.IGNORECASE,
    )
    PAGE_FOOTER_RE = re.compile(r"^\s*\d+\s*/\s*\d+\s*$")
    DOC_REF_RE = re.compile(r"^FR\d{4,}$", re.IGNORECASE)
    OCR_CACHE_DIR = os.path.join(os.getenv("TEMP", os.getenv("TMP", "/tmp")), "pgb_ocr_cache")
    VALID_SKU_PREFIXES = ("GG", "GZ", "BZ", "GP", "SM", "PB", "PFW", "PFE", "PG0", "PGW", "TRE", "OZB")

    def __init__(self, pdf_path: str):
        # PGB a généralement une couche texte exploitable ; on évite l'OCR et
        # l'extraction "blocks/words" (coûteuse) pour réduire la latence BL/facture.
        self.pdf_path = pdf_path
        pages = []
        full_text_chunks: List[str] = []
        with fitz.open(pdf_path) as doc:
            for idx, page in enumerate(doc):
                text = page.get_text() or ""
                pages.append({"page_number": idx + 1, "text": text})
                full_text_chunks.append(text)

        self.pdf_raw = {
            "page_count": len(pages),
            "pages": pages,
            "full_text": "\n".join(full_text_chunks),
        }
        self.full_text = self.pdf_raw["full_text"] or ""
        # Some BL PDFs are scanned and have no text layer.
        # Fallback to OCR only when direct text extraction is effectively empty.
        if len(self.full_text.strip()) < 80:
            cached = self._load_cached_ocr_text(pdf_path)
            if cached:
                self.full_text = cached
            else:
                ocr_raw = extract_pdf_raw(pdf_path)
                self.full_text = extract_ocr_if_needed(pdf_path, ocr_raw) or self.full_text
                self.pdf_raw = ocr_raw
                self.pdf_raw["full_text"] = self.full_text
                self._save_cached_ocr_text(pdf_path, self.full_text)
        self.text_lower = self.full_text.lower()

    @classmethod
    def _build_ocr_cache_path(cls, pdf_path: str) -> str:
        stat = os.stat(pdf_path)
        with open(pdf_path, "rb") as f:
            head = f.read(1024 * 1024)
            if stat.st_size > 2 * 1024 * 1024:
                f.seek(max(0, stat.st_size - 1024 * 1024))
            tail = f.read(1024 * 1024)
        payload = head + b"|" + tail + b"|" + str(stat.st_size).encode("utf-8")
        key = hashlib.sha256(payload).hexdigest()
        os.makedirs(cls.OCR_CACHE_DIR, exist_ok=True)
        return os.path.join(cls.OCR_CACHE_DIR, f"{key}.txt")

    @classmethod
    def _load_cached_ocr_text(cls, pdf_path: str) -> Optional[str]:
        try:
            cache_path = cls._build_ocr_cache_path(pdf_path)
            if not os.path.exists(cache_path):
                return None
            with open(cache_path, "r", encoding="utf-8") as f:
                text = f.read()
            return text if text and text.strip() else None
        except Exception:
            return None

    @classmethod
    def _save_cached_ocr_text(cls, pdf_path: str, text: str) -> None:
        try:
            if not text or not text.strip():
                return
            cache_path = cls._build_ocr_cache_path(pdf_path)
            with open(cache_path, "w", encoding="utf-8") as f:
                f.write(text)
        except Exception:
            pass

    def extract_products(self) -> List[Dict]:
        lines = [ln.strip() for ln in self.full_text.splitlines() if ln and ln.strip()]
        # PAKLIJST/BL layout: no POS table, each product starts with
        # "<SKU> / batch <batch> <qty>ST" then description on next line.
        is_invoice = any(x in self.text_lower for x in ("factuur", "invoice", "facture"))
        is_delivery_layout = (
            ("paklijst" in self.text_lower and "omschrijving ean code hoeveelheid" in self.text_lower)
            or "leveringsnummer" in self.text_lower
            or "leveringsbon" in self.text_lower
            or "bordereau de livraison" in self.text_lower
            or "bon de livraison" in self.text_lower
            or "numéro de livraison" in self.text_lower
            or (not is_invoice and "/ batch" in self.text_lower and "zendnota" in self.text_lower)
        )
        if is_delivery_layout:
            return self._extract_products_delivery(lines)

        if is_invoice:
            return self._extract_products_invoice(lines)

        return []

    @classmethod
    def _normalize_ocr_sku(cls, sku: str) -> str:
        s = (sku or "").strip().upper().replace(" ", "")
        if re.match(r"^0[A-Z]", s):
            s = "O" + s[1:]
        return s

    @classmethod
    def _is_valid_invoice_pos(cls, pos: int) -> bool:
        return 10 <= pos <= 3000 and pos % 10 == 0

    @classmethod
    def _peek_meaningful_line(cls, lines: List[str], idx: int) -> str:
        for j in range(idx + 1, min(idx + 4, len(lines))):
            if lines[j].strip():
                return lines[j].strip()
        return ""

    @classmethod
    def _is_invoice_pos_line(cls, line: str, next_line: str) -> bool:
        if not re.fullmatch(r"\d{2,4}", line or ""):
            return False
        pos = int(line)
        if not cls._is_valid_invoice_pos(pos):
            return False
        nxt = (next_line or "").lower()
        return nxt.startswith(("uw ref.", "votre réf.", "votre ref."))

    @classmethod
    def _extract_sku_from_line(cls, line: str) -> Optional[str]:
        s = (line or "").strip()
        if not s:
            return None
        s = re.sub(
            r"\s+\d+(?:[.,]\d+)?\s*(ST|PCS|PC|PCE|KG|M|LTR|L|DOOS|BOX|BOITE|CARTON)\s*$",
            "",
            s,
            flags=re.IGNORECASE,
        )
        for token in s.split():
            candidate = cls._normalize_ocr_sku(token.upper())
            if cls._is_invoice_sku(candidate):
                return candidate
        compact = re.sub(r"\s+", "", s.upper())
        m = re.match(r"^([A-Z0-9][A-Z0-9-]{7,})", compact)
        if not m:
            return None
        token = cls._normalize_ocr_sku(m.group(1))
        return token if cls._is_invoice_sku(token) else None

    @classmethod
    def _collect_invoice_pos_anchors(cls, lines: List[str]) -> List[tuple[int, int, str]]:
        anchors: List[tuple[int, int, str]] = []
        for i, raw in enumerate(lines):
            line = raw.strip()
            m = re.match(r"^(\d{2,4})\b\s*(.*)$", line)
            if not m:
                continue
            pos = int(m.group(1))
            if not cls._is_valid_invoice_pos(pos):
                continue
            rest = (m.group(2) or "").strip()
            rest_low = rest.lower()

            if cls.INVOICE_DESC_START_RE.match(rest):
                anchors.append((i, pos, "embedded"))
                continue

            if rest_low.startswith(("uw ref.", "votre réf.", "votre ref.")):
                anchors.append((i, pos, "standalone"))
                continue

            if (
                not rest
                or rest.upper().startswith(("EENH", "BEDRAG", "HOEVEEL", "PRIJS", "NETTO"))
                or re.fullmatch(r"(ST|PCS|PC|PCE|BOX|BOITE|DOOS)", rest, re.IGNORECASE)
            ):
                nxt = cls._peek_meaningful_line(lines, i).lower()
                if nxt.startswith(("uw ref.", "votre réf.", "votre ref.")):
                    anchors.append((i, pos, "standalone"))
        anchors.sort(key=lambda x: x[0])
        return anchors

    @classmethod
    def _is_invoice_sku(cls, token: str) -> bool:
        token = cls._normalize_ocr_sku(token)
        if not token or cls.DOC_REF_RE.match(token):
            return False
        if cls._is_valid_delivery_sku(token):
            return True
        return bool(
            cls.MAIN_SKU_RE.match(token)
            and any(c.isdigit() for c in token)
            and any(c.isalpha() for c in token)
        )

    @classmethod
    def _invoice_summary_start(cls, lines: List[str]) -> int:
        for i, line in enumerate(lines):
            low = line.lower().strip()
            if re.match(r"^subtotaal\b", low) or re.match(r"^totaal\s+excl", low):
                return i
        return len(lines)

    def _parse_invoice_block(
        self, lines: List[str], start: int, end: int, kind: str = "standalone"
    ) -> Optional[Dict]:
        sku = None
        sku_idx = None
        description = ""

        if kind == "embedded":
            description = re.sub(r"^\d{2,4}\s+", "", lines[start].strip())
            for j in range(start - 1, max(start - 10, -1), -1):
                token = self._extract_sku_from_line(lines[j])
                if token:
                    sku = token
                    sku_idx = j
                    break
        else:
            for j in range(start + 1, min(start + 20, end)):
                token = self._extract_sku_from_line(lines[j])
                if token:
                    sku = token
                    sku_idx = j
                    break

        if not sku or sku_idx is None:
            return None

        qty = 0.0
        unit = "ST"
        ean = None
        lot_qty_sum = 0.0
        lot_unit = None
        piece_qty = 0.0
        box_qty = 0.0
        scan_from = sku_idx + 1
        if kind == "embedded":
            scan_from = max(scan_from, start + 1)

        for j in range(scan_from, end):
            ln = lines[j]
            low = ln.lower()
            if self.PAGE_FOOTER_RE.match(ln) or self.DOC_REF_RE.match(ln):
                continue
            if ln.upper() == "POS.":
                break
            emb = self.EMBEDDED_POS_LINE_RE.match(ln.strip())
            if emb and j > start:
                rest = ln.strip()[len(emb.group(1)) :].strip()
                if self.INVOICE_DESC_START_RE.match(rest):
                    break
            if re.fullmatch(r"\d{2,4}", ln) and self._is_valid_invoice_pos(int(ln)):
                nxt = self._peek_meaningful_line(lines, j).lower()
                if nxt.startswith(("uw ref.", "votre réf.", "votre ref.")):
                    break
            if "factuur" in low and "duplicaat" in low:
                continue
            if low.startswith(("documentdatum", "btw-nummer", "peppol id")):
                continue

            if not description and re.search(r"[A-Za-zÀ-ÿ]", ln) and not low.startswith(
                ("uw ref.", "zendnota", "lot / hoeveelheid", "lot / hoeveelheid")
            ):
                m_desc_qty = re.match(
                    r"^(.*?)(?:\s+(\d+(?:[.,]\d+)?)\s*(ST|PCS|PC|KG|M|LTR|L|DOOS|BOX))?$",
                    ln,
                    re.IGNORECASE,
                )
                if m_desc_qty:
                    description = (m_desc_qty.group(1) or "").strip()
                    if m_desc_qty.group(2):
                        qty = float(m_desc_qty.group(2).replace(",", "."))
                        unit = m_desc_qty.group(3).upper()

            lot = self.LOT_LINE_RE.match(ln)
            if lot:
                if not ean:
                    ean = lot.group(1).upper()
                lot_val = float(lot.group(2).replace(",", "."))
                lot_qty_sum += lot_val
                lot_u = lot.group(3).upper()
                if not lot_unit:
                    lot_unit = lot_u
                if lot_u in ("BOX", "BOITE", "DOOS", "CARTON"):
                    box_qty = max(box_qty, lot_val)

            m_mult = self.MULTIPLY_QTY_RE.match(ln)
            if m_mult:
                piece_qty = max(
                    piece_qty,
                    int(m_mult.group(1)) * float(m_mult.group(2).replace(",", ".")),
                )

            m_piece = self.PIECE_QTY_LINE_RE.match(ln)
            if m_piece:
                piece_qty = max(piece_qty, self._parse_number_token(m_piece.group(1)))

            q = self.QTY_UNIT_RE.search(ln)
            if q and q.group(2).upper() in ("BOX", "BOITE", "DOOS", "CARTON"):
                box_qty = max(box_qty, float(q.group(1).replace(",", ".")))

        gross_price, net_price, price_divisor, price_unit_basis = self._extract_invoice_prices(
            lines, sku_idx + 1, end
        )

        if price_unit_basis in ("ST", "PCS", "PC", "PCE") and piece_qty > 0:
            qty = piece_qty
            unit = "ST"
        elif price_unit_basis in ("BOX", "BOITE", "DOOS", "CARTON") and box_qty > 0:
            qty = box_qty
            unit = price_unit_basis if price_unit_basis != "BOITE" else "BOX"
        elif lot_qty_sum > 0:
            qty = lot_qty_sum
            if lot_unit:
                unit = lot_unit

        unit_price, line_total = self._resolve_invoice_amounts(
            qty, net_price, gross_price, price_divisor, price_unit_basis
        )

        return {
            "qty": qty if qty > 0 else 0,
            "unit": unit,
            "sku": sku,
            "description": description,
            "ean": ean,
            "unit_price": unit_price,
            "line_total": line_total,
        }

    def _extract_products_invoice(self, lines: List[str]) -> List[Dict]:
        """Extract invoice lines anchored on POS numbers (10, 20, … 1230)."""
        summary_start = self._invoice_summary_start(lines)
        anchors = self._collect_invoice_pos_anchors(lines[:summary_start])

        items: List[Dict] = []
        for idx, (pos_i, _pos_num, kind) in enumerate(anchors):
            end_i = anchors[idx + 1][0] if idx + 1 < len(anchors) else summary_start
            parsed = self._parse_invoice_block(lines, pos_i, end_i, kind)
            if parsed:
                items.append(parsed)

        return [
            x
            for x in items
            if x.get("sku") and (x.get("description") or x.get("ean") or (x.get("qty") or 0) > 0)
        ]

    @classmethod
    def _parse_number_token(cls, token: str) -> float:
        s = (token or "").strip()
        if re.fullmatch(r"\d+\.\d{3}", s):
            return float(s.replace(".", ""))
        if re.fullmatch(r"\d+,\d{3}", s):
            return float(s.replace(",", ""))
        return float(s.replace(",", "."))

    @classmethod
    def _extract_invoice_prices(
        cls, lines: List[str], start_idx: int, end_idx: int
    ) -> tuple[Optional[float], Optional[float], float, str]:
        """Parse PGB invoice prices: '7,78 €' then '/ 1.000 ST' then net '6,22 €'."""
        amounts: List[float] = []
        divisor = 1.0
        price_unit = "ST"

        for j in range(start_idx, end_idx):
            ln = lines[j].strip()
            if not ln:
                continue

            if cls.LOT_LINE_RE.match(ln):
                continue

            div_m = cls.PRICE_PER_DIVISOR_RE.match(ln)
            if div_m:
                divisor = cls._parse_number_token(div_m.group(1))
                price_unit = div_m.group(2).upper()
                if divisor <= 0:
                    divisor = 1.0
                continue

            inline = cls.PRICE_INLINE_DIVISOR_RE.search(ln)
            if inline:
                amounts.append(cls._parse_number_token(inline.group(1)))
                divisor = cls._parse_number_token(inline.group(2))
                price_unit = inline.group(3).upper()
                if divisor <= 0:
                    divisor = 1.0
                continue

            for raw in cls.AMOUNT_RE.findall(ln):
                amounts.append(cls._parse_number_token(raw))

        gross = amounts[0] if amounts else None
        net = amounts[-1] if amounts else None
        if len(amounts) >= 2:
            gross = amounts[0]
            net = amounts[-1]
        return gross, net, divisor, price_unit

    @classmethod
    def _resolve_invoice_amounts(
        cls,
        qty: float,
        net_price: Optional[float],
        gross_price: Optional[float],
        divisor: float,
        price_unit: str,
    ) -> tuple[Optional[float], Optional[float]]:
        if net_price is None and gross_price is None:
            return None, None
        if gross_price is None:
            gross_price = net_price
        if net_price is None:
            net_price = gross_price
        if divisor <= 0:
            divisor = 1.0

        # Prix catalogue (ex. 7,78 € / 1.000 ST → 0,00778 €/ST ; total = 0,00778 × 800 = 6,22 €).
        unit_price = round(gross_price / divisor, 6)
        line_total = None
        if qty > 0:
            line_total = round(qty * unit_price, 2)
        return unit_price, line_total

    @classmethod
    def _resolve_delivery_qty(
        cls,
        lines: List[str],
        start_idx: int,
        end_idx: int,
        *,
        skip_art_client_pack: bool = False,
    ) -> tuple[float, str]:
        """Resolve delivered quantity in pieces (ST), aligned with invoice parsing.

        BORDEREAU: Art. client 200 + 4 BOITE → 800 ST.
        Zendnota: 12 ST or Art. klant 12 + 1 DOOS → 12 ST.
        Weight lines (0,42 KG) are not delivered qty.
        """
        box_qty: Optional[float] = None
        piece_qty: Optional[float] = None
        pieces_per_box: Optional[float] = None
        weight_kg: Optional[float] = None
        expect_art_client_qty = False
        after_art_client_pack = False

        for j in range(start_idx, end_idx):
            art_line = lines[j].strip()
            if not art_line:
                continue
            art_low = art_line.lower()

            if re.search(r"art\.?\s*(?:klant|client)\s*:?\s*$", art_low):
                expect_art_client_qty = True
                after_art_client_pack = True
                continue

            if after_art_client_pack and cls.STRICT_QTY_UNIT_LINE_RE.match(art_line):
                after_art_client_pack = False
                # Admin zendnota: "200 ST" après Art. klant = pièces/doos (qté déjà sur en-tête/description).
                if skip_art_client_pack:
                    continue

            if expect_art_client_qty and re.fullmatch(r"[\d.,]+", art_line):
                pieces_per_box = cls._parse_number_token(art_line)
                expect_art_client_qty = False
                continue
            expect_art_client_qty = False

            pkg_total = cls.PACKAGE_TOTAL_PIECES_RE.search(art_line)
            if pkg_total:
                piece_qty = max(piece_qty or 0.0, cls._parse_number_token(pkg_total.group(1)))
                continue

            m_mult = cls.MULTIPLY_QTY_RE.match(art_line)
            if m_mult:
                piece_qty = max(
                    piece_qty or 0.0,
                    int(m_mult.group(1)) * float(m_mult.group(2).replace(",", ".")),
                )
                continue

            m_piece = cls.PIECE_QTY_LINE_RE.match(art_line)
            if m_piece:
                piece_qty = max(piece_qty or 0.0, cls._parse_number_token(m_piece.group(1)))
                continue

            m_trail = cls.TRAILING_BOX_QTY_RE.search(art_line)
            if m_trail:
                box_qty = float(m_trail.group(1).replace(",", "."))
            m_de = cls.BOITE_DE_RE.search(art_line)
            if m_de:
                box_qty = float(m_de.group(1).replace(",", "."))

            m_box = cls.BOX_QTY_LINE_RE.match(art_line)
            if m_box:
                box_qty = float(m_box.group(1).replace(",", "."))
                continue

            strict_q = cls.STRICT_QTY_UNIT_LINE_RE.match(art_line)
            if strict_q:
                unit = strict_q.group(2).upper()
                value = float(strict_q.group(1).replace(",", "."))
                if unit == "KG":
                    weight_kg = value
                elif unit in ("ST", "PCS", "PC", "PCE"):
                    piece_qty = max(piece_qty or 0.0, value)
                elif unit in ("BOITE", "CARTON", "DOOS", "BOX"):
                    box_qty = value
                continue

            if re.fullmatch(r"\d{1,5}", art_line):
                maybe_num = float(art_line)
                if j + 1 < end_idx:
                    next_q = cls.STRICT_QTY_UNIT_LINE_RE.match(lines[j + 1].strip())
                    if next_q and abs(float(next_q.group(1).replace(",", ".")) - maybe_num) < 0.001:
                        unit = next_q.group(2).upper()
                        if unit == "KG":
                            weight_kg = maybe_num
                        elif unit in ("ST", "PCS", "PC", "PCE"):
                            piece_qty = max(piece_qty or 0.0, maybe_num)
                        elif unit in ("BOITE", "CARTON", "DOOS", "BOX"):
                            box_qty = maybe_num
                if j + 1 < end_idx:
                    unit_line = lines[j + 1].strip().upper()
                    if unit_line in ("BOITE", "CARTON", "DOOS", "BOX"):
                        box_qty = maybe_num

        if piece_qty and piece_qty > 0:
            return piece_qty, "ST"
        if box_qty and pieces_per_box and box_qty > 0 and pieces_per_box > 0:
            return box_qty * pieces_per_box, "ST"
        if box_qty and box_qty > 0:
            return box_qty, "ST"
        return 0.0, "ST"

    @classmethod
    def _is_weight_only_line(cls, line: str) -> bool:
        s = (line or "").strip()
        if not s:
            return False
        if cls.WEIGHT_LINE_RE.match(s):
            return True
        return bool(re.fullmatch(r"[\d.,]+\s*KG", s, re.IGNORECASE))

    @classmethod
    def _extract_trailing_qty_unit(cls, line: str) -> tuple[float, str]:
        """Zendnota admin: 'SKU / batch …    14 ST' — qty en fin de ligne."""
        m = re.search(
            r"(\d+(?:[.,]\d+)?)\s+(ST|PCS|PC|PCE|KG|M|LTR|L|DOOS|BOX|BOITE|CARTON)\s*$",
            (line or "").strip(),
            re.IGNORECASE,
        )
        if not m:
            return 0.0, "ST"
        return float(m.group(1).replace(",", ".")), m.group(2).upper()

    @classmethod
    def _line_starts_delivery_batch(cls, line: str) -> bool:
        return "/ batch" in (line or "").lower()

    def _extract_batch_line_sku(self, line: str) -> Optional[str]:
        m = self.BL_SKU_BATCH_LINE_RE.match((line or "").strip())
        if m:
            return self._normalize_ocr_sku(m.group(1))
        pre = (line or "").split("/")[0]
        tok = re.search(r"([A-Z0-9][A-Z0-9]{7,})\s*$", pre.strip(), re.IGNORECASE)
        if tok:
            return self._normalize_ocr_sku(tok.group(1))
        return None

    def _delivery_batch_block_end(self, lines: List[str], start_after_header: int) -> int:
        for j in range(start_after_header, len(lines)):
            ln = lines[j]
            if not self._line_starts_delivery_batch(ln):
                continue
            sku = self._extract_batch_line_sku(ln)
            if sku and self._is_valid_delivery_sku(sku):
                return j
        return len(lines)

    def _find_delivery_batch_starts(self, lines: List[str]) -> List[tuple[int, str, Optional[str]]]:
        starts: List[tuple[int, str, Optional[str]]] = []
        for i, line in enumerate(lines):
            if "/ batch" not in line.lower():
                continue
            sku = None
            ean = None
            m = self.BL_SKU_BATCH_LINE_RE.match(line.strip())
            if m:
                sku = self._normalize_ocr_sku(m.group(1))
                if m.group(2):
                    ean = m.group(2).upper()
            else:
                pre = line.split("/")[0]
                tok = re.search(r"([A-Z0-9][A-Z0-9]{7,})\s*$", pre.strip(), re.IGNORECASE)
                if tok:
                    sku = self._normalize_ocr_sku(tok.group(1))
            if sku and self._is_valid_delivery_sku(sku):
                starts.append((i, sku, ean))
        return starts

    def _parse_delivery_block(
        self, lines: List[str], start: int, end: int, sku: str, ean_hint: Optional[str]
    ) -> Dict:
        ean = ean_hint
        header_qty, header_unit = self._extract_trailing_qty_unit(lines[start])
        description = ""
        desc_qty = 0.0
        desc_unit = "ST"
        for j in range(start + 1, min(start + 8, end)):
            ln = lines[j].strip()
            low = ln.lower()
            if not ln or self._is_weight_only_line(ln):
                continue
            if low.startswith(
                ("art.", "ref.", "votre", "code sh", "origine", "carton:", "réf. pgb", "hs-code", "herkomst", "doos:")
            ):
                continue
            if "/ batch" in low:
                continue
            trail = re.search(
                r"(\d+(?:[.,]\d+)?)\s+(ST|PCS|PC|PCE)\s*$",
                ln,
                re.IGNORECASE,
            )
            if trail:
                desc_qty = float(trail.group(1).replace(",", "."))
                desc_unit = trail.group(2).upper()
                description = ln[: trail.start()].strip()
            else:
                description = self.TRAILING_BOX_QTY_RE.sub("", ln).strip()
            if description:
                break

        block_qty, block_unit = self._resolve_delivery_qty(
            lines,
            start + 1,
            end,
            skip_art_client_pack=(header_qty > 0 or desc_qty > 0),
        )
        if header_qty > 0:
            qty, unit = header_qty, header_unit
        elif desc_qty > 0:
            qty, unit = desc_qty, desc_unit
        else:
            qty, unit = block_qty, block_unit
        for j in range(start + 1, end):
            art_match = re.search(
                r"art\.?\s*(?:klant|client)\s*:\s*([A-Z0-9-]{8,})", lines[j], re.IGNORECASE
            )
            if art_match:
                ean = art_match.group(1).upper()
            elif ean is None:
                standalone_ean = re.fullmatch(r"\s*(\d{13})\s*", lines[j].strip())
                if standalone_ean:
                    ean = standalone_ean.group(1)

        return {
            "qty": qty,
            "unit": unit,
            "sku": sku,
            "description": description,
            "ean": ean,
            "unit_price": None,
            "line_total": None,
        }

    @classmethod
    def _collapse_delivery_by_sku(cls, items: List[Dict]) -> List[Dict]:
        """Un batch / ligne par SKU — plusieurs lots du même article = 1 produit."""
        merged: List[Dict] = []
        index_by_sku: Dict[str, int] = {}
        for item in items:
            sku = item.get("sku")
            if not sku:
                continue
            if sku in index_by_sku:
                existing = merged[index_by_sku[sku]]
                existing["qty"] = (existing.get("qty") or 0) + (item.get("qty") or 0)
            else:
                index_by_sku[sku] = len(merged)
                merged.append(dict(item))
        return merged

    def _extract_products_delivery_batch(self, lines: List[str]) -> List[Dict]:
        starts = self._find_delivery_batch_starts(lines)
        items: List[Dict] = []
        for idx, (start_i, sku, ean_hint) in enumerate(starts):
            end_i = self._delivery_batch_block_end(lines, start_i + 1)
            items.append(self._parse_delivery_block(lines, start_i, end_i, sku, ean_hint))
        items = [
            x
            for x in items
            if x.get("sku") and ((x.get("qty") or 0) > 0 or x.get("description") or x.get("ean"))
        ]
        return self._collapse_delivery_by_sku(items)

    def _extract_products_delivery(self, lines: List[str]) -> List[Dict]:
        if any("/ batch" in ln.lower() for ln in lines):
            return self._extract_products_delivery_batch(lines)

        items: List[Dict] = []
        i = 0
        seen_skus = set()
        # Footer legal text repeats mid-document on multi-page BORDEREAU — skip, do not stop.
        footer_skip_contains = (
            "algemene voorwaarden",
            "algemene en verkoopsvoorwaarden",
            "conditions générales",
            "general conditions",
            "terms-and-conditions",
            "pgb-europe.com",
        )
        skip_prefixes = (
            "art. klant", "art klant", "art. client", "art client", "ref. pgb", "uw ref.", "votre réf.", "doos:",
            "omschrijving", "description", "lot / hoeveelheid", "zendnota / order / levering",
            "paklijst", "documentdatum", "date du document", "klantnummer", "numéro de client", "e-mail", "tel.", "tél.",
            "totaal gewicht", "poids total", "factuur -", "bordereau de livraison", "bon de livraison",
            "nombre de cartons", "numéro de livraison", "n° de livraison", "code sh", "origine",
        )
        while i < len(lines):
            line = lines[i]
            low = line.lower()
            if any(m in low for m in footer_skip_contains):
                i += 1
                continue
            if low.startswith(skip_prefixes):
                i += 1
                continue
            if self.PAGE_FOOTER_RE.match(line) or self.DOC_REF_RE.match(line):
                i += 1
                continue
            if self.LOT_LINE_RE.match(line):
                i += 1
                continue

            sku = None
            qty = None
            unit = None
            ean = None

            m = self.BL_SKU_LINE_RE.match(line)
            if m and m.group(1):
                sku = m.group(1).upper()
                if m.group(2):
                    ean = m.group(2).upper()
                if m.group(3) and m.group(4):
                    qty = float(m.group(3).replace(",", "."))
                    unit = m.group(4).upper()
            else:
                m_batch = self.BL_SKU_BATCH_LINE_RE.match(line)
                if m_batch:
                    sku = m_batch.group(1).upper()
                    if m_batch.group(2):
                        ean = m_batch.group(2).upper()
                else:
                    tok = self.BL_SKU_TOKEN_RE.search(line)
                    if tok:
                        sku = tok.group(1).upper()

            if not sku:
                i += 1
                continue
            if not self._is_valid_delivery_sku(sku):
                i += 1
                continue
            # Skip OCR lines marked as duplicated with '*'
            if "*" in line and sku in seen_skus:
                i += 1
                continue
            # Ignore lot-like codes as primary SKU.
            if sku.startswith(("VP", "P2", "0000")) and "/" in line:
                i += 1
                continue
            if "/ batch" in low:
                batch_m = re.search(r"/\s*batch\s+([A-Z0-9-]+)", line, re.IGNORECASE)
                if batch_m and len(batch_m.group(1)) < 6:
                    i += 1
                    continue

            description = ""
            lot_qty_sum = 0.0
            lot_unit = None

            # Never infer BL quantity from SKU/description line (e.g. ".../36st" packaging).
            # Quantity must come from dedicated quantity lines or lot rows.

            if i + 1 < len(lines):
                next_line = lines[i + 1].strip()
                next_low = next_line.lower()
                if next_line and not next_low.startswith(("art. klant", "art klant", "art. client", "art client", "ref. pgb", "uw ref.", "votre réf.", "doos:", "omschrijving")):
                    description = next_line

            # BL contains "Art. klant : <ean-like code>" right after description
            j = i + 1
            while j < min(i + 24, len(lines)):
                art_line = lines[j]
                art_low = art_line.lower()
                maybe_next = (art_line or "").replace(" ", "").upper()
                if self.BL_SKU_TOKEN_RE.search(art_line) and maybe_next != sku and not art_low.startswith(("art. klant", "art klant", "art. client", "art client", "ref. pgb", "uw ref.", "votre réf.")):
                    break

                art_match = re.search(r"art\.?\s*(?:klant|client)\s*:\s*([A-Z0-9-]{8,})", art_line, re.IGNORECASE)
                if art_match:
                    ean = art_match.group(1).upper()
                elif ean is None:
                    # BL admin format often places EAN-like customer code on a dedicated line.
                    standalone_ean = re.fullmatch(r"\s*(\d{13})\s*", art_line)
                    if standalone_ean:
                        ean = standalone_ean.group(1)

                lot_match = self.LOT_LINE_RE.match(art_line)
                if lot_match:
                    lot_qty_sum += float(lot_match.group(2).replace(",", "."))
                    if not lot_unit:
                        lot_unit = lot_match.group(3).upper()
                j += 1

            block_qty, block_unit = self._resolve_delivery_qty(lines, i + 1, j)
            if lot_qty_sum > 0:
                qty = lot_qty_sum
                if lot_unit:
                    unit = lot_unit
            elif block_qty > 0:
                qty = block_qty
                unit = block_unit
            elif qty is not None and (unit or "").upper() == "KG":
                qty = 0.0
                unit = "ST"
            elif qty is None:
                qty = 0.0
                unit = "ST"
            elif not unit:
                unit = "ST"

            items.append(
                {
                    "qty": qty,
                    "unit": unit,
                    "sku": sku,
                    "description": description,
                    "ean": ean,
                    "unit_price": None,
                    "line_total": None,
                }
            )
            seen_skus.add(sku)
            i = j

        # Garder une ligne par occurrence (aligné facture POS) — pas de fusion par SKU.
        return [
            x for x in items
            if x.get("sku") and ((x.get("qty") or 0) > 0 or x.get("description") or x.get("ean"))
        ]

    @classmethod
    def _is_valid_delivery_sku(cls, sku: str) -> bool:
        s = cls._normalize_ocr_sku(sku)
        if len(s) < 8:
            return False
        if not any(ch.isdigit() for ch in s):
            return False
        if not any(ch.isalpha() for ch in s):
            return False
        if s.isdigit():
            return False
        # Drop obvious headers/noise.
        banned = (
            "HOEVEELHEID",
            "ZENDNOTA",
            "DOCUMENTDATUM",
            "PAKLIJST",
            "INSTALLATION",
            "SPAANDERPLAATSCHR",
            "SHARPWARE",
            "SHARPWAREZ",
        )
        if any(b in s for b in banned):
            return False
        return s.startswith(cls.VALID_SKU_PREFIXES)

    def extract_metadata(self) -> Dict:
        text = self.full_text
        text_lower = self.text_lower

        doc_type = "delivery"
        if "factuur" in text_lower or "invoice" in text_lower or "facture" in text_lower:
            doc_type = "invoice"
        elif "paklijst" in text_lower or "leveringsbon" in text_lower or "delivery note" in text_lower or "bon de livraison" in text_lower or "bordereau de livraison" in text_lower:
            doc_type = "delivery"

        number = self._extract_first(
            text,
            [
                r"factuur\s*\([^)]*\)\s*-\s*\r?\n\s*([\d]{2,}/[\d]+)",
                r"factuur[^\n]*-\s*\r?\n\s*([\d]{2,}/[\d]+)",
                r"facture\s*\([^)]*\)\s*-\s*\r?\n\s*([\d]{2,}/[\d]+)",
                r"invoice[-\s:]*([A-Za-z0-9/.-]+)",
                r"paklijst[-\s:]*([A-Za-z0-9/.-]+)",
                r"num[ée]ro\s*de\s*livraison\s*:?\s*([A-Za-z0-9/.-]+)",
                r"bordereau\s*de\s*livraison\s*-?\s*([A-Za-z0-9/.-]+)",
                r"bordereau\s*de\s*livraison\s*-?\s*\r?\n\s*([0-9]{4,})",
                r"factuur[-\s:]*([A-Za-z0-9/.-]+)",
                r"zendnota[-\s:]*([A-Za-z0-9/.-]+)",
            ],
        )
        if number and number.lower() in ("datum", "order", "levering", "duplicaat"):
            number = self._extract_first(
                text,
                [
                    r"factuur\s*\([^)]*\)\s*-\s*\r?\n\s*([\d]{2,}/[\d]+)",
                    r"factuur[^\n]*-\s*\r?\n\s*([\d]{2,}/[\d]+)",
                ],
            )

        date = self._extract_first(
            text,
            [
                r"documentdatum\s*([0-3]?\d[-/][01]?\d[-/]\d{4})",
                r"date\s*du\s*document\s*([0-3]?\d[-/][01]?\d[-/]\d{4})",
                r"factuur[-\s:]*[A-Za-z0-9/.-]+\s+([0-3]?\d[-/][01]?\d[-/]\d{4})",
                r"\b([0-3]?\d[-/][01]?\d[-/]\d{4})\b",
                r"documentdatum\s*([0-3]?\d[-/][01]?\d[-/]\d{2})",
                r"date\s*du\s*document\s*([0-3]?\d[-/][01]?\d[-/]\d{2})",
            ],
        )

        client = self._extract_first(
            text,
            [
                r"klantnaam\s+(.+)",
                r"(?:nom du client|client name)\s*[:\s]+(.+)",
                r"euro\s*brico",
                r"oces\s+group\s+srl",
            ],
            flags=re.IGNORECASE | re.MULTILINE,
        )

        if client:
            client = re.sub(r"\s+", " ", client).strip()

        supplier_email = self._extract_first(
            text,
            [r"([a-zA-Z0-9._%+-]+@pgb-europe\.com)"],
            flags=re.IGNORECASE,
        )
        supplier_phone = self._extract_first(
            text,
            [r"(?:tel|tél)\.?\s*([+0-9() /\.-]{8,})"],
            flags=re.IGNORECASE,
        )
        payment_terms = self._extract_first(
            text,
            [
                r"betalingsvoorwaarden\s*([\wÀ-ÿ0-9 .-]{5,80})",
                r"conditions?\s*de\s*paiement\s*[:\s]+(.{5,80})",
            ],
            flags=re.IGNORECASE,
        )
        if payment_terms:
            payment_terms = payment_terms.strip(" .:-")

        supplier_address = self._extract_supplier_address(text)

        return {
            "doc_type": doc_type,
            "number": number,
            "client": client if client else None,
            "supplier": "PGB Europe",
            "date": date,
            "supplier_code": None,
            "supplier_address": supplier_address,
            "supplier_phone": supplier_phone.strip() if supplier_phone else None,
            "supplier_email": supplier_email.lower().strip() if supplier_email else None,
            "supplier_contact": None,
            "supplier_payment_terms": payment_terms,
        }

    @staticmethod
    def _extract_first(text: str, patterns: List[str], flags: int = re.IGNORECASE) -> Optional[str]:
        for pattern in patterns:
            m = re.search(pattern, text, flags)
            if not m:
                continue
            value = (m.group(1) if m.lastindex else m.group(0)).strip()
            if value:
                return value
        return None

    @staticmethod
    def _extract_supplier_address(text: str) -> Optional[str]:
        lines = [ln.strip() for ln in text.splitlines() if ln and ln.strip()]
        # Preferred explicit PGB address (most stable across docs).
        for i, line in enumerate(lines):
            if "gontrode heirweg" in line.lower():
                line2 = lines[i + 1] if i + 1 < len(lines) else ""
                if line2 and re.search(r"\b\d{4}\b", line2):
                    return f"{line}, {line2}"
                return line

        def is_address_like(s: str) -> bool:
            low = s.lower()
            if any(x in low for x in ("tel", "fax", "email", "@", "http", "www.", "kg", "totaal gewicht", "poids total", "peppol", "iban", "bic", "carton", "cartons")):
                return False
            if s in {":", "-", "Admin"}:
                return False
            if re.fullmatch(r"\d{1,3}", s):
                return False
            return bool(re.search(r"\d", s))

        for i, line in enumerate(lines):
            low = line.lower()
            if "pgb-europe" not in low and "pgb europe" not in low:
                continue
            # Take first 1-2 address-like lines after supplier name.
            candidates: List[str] = []
            for j in range(i + 1, min(i + 8, len(lines))):
                cand = lines[j]
                if is_address_like(cand):
                    candidates.append(cand)
                if len(candidates) >= 2:
                    break
            if candidates:
                return ", ".join(candidates[:2])
        return None
