import re
from typing import Dict, List, Optional

from .base_parser import BaseParser


class PardaenParser(BaseParser):
    """Parser deterministic for PARDAEN invoices and delivery notes."""

    # Chaque segment après le préfixe chiffres doit contenir au moins un chiffre (1-TRA706T, 1-1399).
    # Rejette les fragments description type 8-DELIGE (lettres seules).
    _SKU_PART = r"[A-Za-z0-9]*\d+[A-Za-z0-9]*"
    _INVOICE_SKU = rf"(?:\d+(?:-{_SKU_PART})+|S-[A-Za-z0-9-]+)"
    _STRICT_SKU_PREFIX_RE = re.compile(
        rf"^\s*{_INVOICE_SKU}\b",
        re.IGNORECASE,
    )
    _INVOICE_MONEY = r"\d+[.,]\d{1,3}"
    _QTY_UNIT_CAP = r"(?<![a-zA-Z])(?:Stk|Blister|Doos(?:\s*\([^)]+\))?|Paar)(?=\s)"
    _QTY_UNIT_WORD = rf"({_QTY_UNIT_CAP})"
    INVOICE_LINE_RE = re.compile(
        rf"^\s*({_INVOICE_SKU})\s+(.+?)\s+(\d+(?:[.,]\d+)?)\s+"
        rf"{_QTY_UNIT_WORD}\s+"
        rf"({_INVOICE_MONEY})\s+([0-9+]+)\s+({_INVOICE_MONEY})\s+({_INVOICE_MONEY})\s+\d{{1,2}}\s*$",
        re.IGNORECASE,
    )
    # Gratuit / 100% korting: seulement korting % + netto 0,00 + Btw (ex. 50+10 0,00 21).
    INVOICE_LINE_PROMO_RE = re.compile(
        rf"^\s*({_INVOICE_SKU})\s+(.+?)\s+(\d+(?:[.,]\d+)?)\s+"
        rf"{_QTY_UNIT_WORD}\s+"
        rf"([0-9+]+)\s+({_INVOICE_MONEY})\s+\d{{1,2}}\s*$",
        re.IGNORECASE,
    )
    INVOICE_LINE_FIND_RE = re.compile(
        rf"^\s*({_INVOICE_SKU})\s+(.+?)\s+(\d+(?:[.,]\d+)?)\s+"
        rf"{_QTY_UNIT_WORD}\s+"
        rf"({_INVOICE_MONEY})\s+([0-9+]+)\s+({_INVOICE_MONEY})\s+({_INVOICE_MONEY})\s+\d{{1,2}}",
        re.IGNORECASE,
    )
    DELIVERY_QTY_RE = re.compile(
        r"^\s*(\d+(?:[.,]\d+)?)\s+"
        r"(Stk|Blister|Doos(?:\s*\([^)]+\))?|Paar(?:\s*\(\d+\s*ST\))?)\s+(\d+[.,]\d{2})\s*$",
        re.IGNORECASE,
    )
    # BL: "M10 2 Doos (100 ST)" — qty is the number after the metric label, not the digits in M10/M12.
    METRIC_QTY_UNIT_RE = re.compile(
        r"\bM(\d+)\s+(\d+(?:[.,]\d+)?)\s+(Stk|Blister|Doos(?:\s*\([^)]+\))?)",
        re.IGNORECASE,
    )
    UNIT_ONLY_RE = re.compile(
        r"^(stk|blister|doos(?:\s*\([^)]+\))?|paar(?:\s*\(\d+\s*st\))?)$",
        re.IGNORECASE,
    )
    _VALID_UNIT_PREFIX_RE = re.compile(r"^(?:stk|blister|doos|paar)\b", re.IGNORECASE)
    _INVOICE_TABLE_TAIL_RE = re.compile(
        r"\s+\d+(?:[.,]\d+)?\s+(?:Stk|Blister|Doos(?:\s*\([^)]+\))?)\s+.*$",
        re.IGNORECASE,
    )
    MONEY_RE = re.compile(r"\d+[.,]\d{2}")
    INVOICE_MONEY_RE = re.compile(_INVOICE_MONEY)
    SKU_RE = re.compile(r"^\s*([A-Z0-9-]{3,})\s*$")
    # Dense table rows: SKU followed by Pardaen's custom barcode token ("X(..(" or "u(..(" etc.)
    # Use lookahead so "(X|" stays in the remainder for "_split_dense_line_tail".
    DENSE_ROW_SKU_RE = re.compile(
        r"^\s*([A-Za-z0-9][A-Za-z0-9-]{2,})\s+(?=[XxuU]\()",
        re.IGNORECASE,
    )
    EAN_RE = re.compile(r"\b\d{8,14}\b")
    DATE_RE = re.compile(r"Documentdatum\s*:\s*([^\n\r]+)", re.IGNORECASE)
    INVOICE_NUMBER_RE = re.compile(r"Factuurnr\.\s*:\s*([A-Z0-9-]+)", re.IGNORECASE)
    DELIVERY_NUMBER_RE = re.compile(r"Verzendnr\.\s*:\s*([A-Z0-9-]+)", re.IGNORECASE)
    PAYMENT_TERMS_RE = re.compile(r"Betalingsvoorwaarden\s*:\s*([^\n\r]+)", re.IGNORECASE)
    BARCODE_TOKEN_RE = re.compile(r"^[A-Z]\(([A-Z0-9*]+)\($", re.IGNORECASE)
    BARCODE_TOKEN_SPLIT_RE = re.compile(r"^([A-Za-z])\(([A-Za-z0-9]{6})\*([A-Za-z0-9]{6})\($")
    # Pardaen token uses position-specific alphabets:
    # - left block (before "*"):  0-9 + A..J
    # - right block (after "*"):  K..T
    LEAD_CHAR_TO_DIGIT = {
        "u": "5",
        "U": "0",
        "x": "8",
        "X": "3",
    }
    LEFT_CHAR_TO_DIGIT = {
        "0": "0",
        "1": "1",
        "2": "2",
        "3": "3",
        "4": "4",
        "5": "5",
        "6": "6",
        "7": "7",
        "8": "8",
        "9": "9",
        "A": "0",
        "B": "1",
        "C": "2",
        "D": "3",
        "E": "4",
        "F": "5",
        "G": "6",
        "H": "7",
        "I": "8",
        "J": "9",
    }
    RIGHT_CHAR_TO_DIGIT = {
        "K": "0",
        "L": "1",
        "M": "2",
        "N": "3",
        "O": "4",
        "P": "5",
        "Q": "6",
        "R": "7",
        "S": "8",
        "T": "9",
    }

    def _to_float(self, value: Optional[str]) -> Optional[float]:
        if not value:
            return None
        s = str(value).strip().replace(" ", "")
        if not s:
            return None
        try:
            if "," in s and "." in s:
                # ex. 1.234,56
                s = s.replace(".", "").replace(",", ".")
            elif "," in s:
                s = s.replace(",", ".")
            return float(s)
        except Exception:
            return None

    def _normalize_barcode_token(self, token: Optional[str]) -> Optional[str]:
        if not token:
            return None
        t = token.strip().upper()
        m = self.BARCODE_TOKEN_RE.match(t)
        if m:
            return m.group(1)
        return t

    def _is_valid_ean13(self, ean: str) -> bool:
        if not ean or len(ean) != 13 or not ean.isdigit():
            return False
        digits = [int(ch) for ch in ean]
        body = digits[:12]
        check = digits[12]
        odd_sum = sum(body[::2])
        even_sum = sum(body[1::2])
        expected = (10 - ((odd_sum + 3 * even_sum) % 10)) % 10
        return check == expected

    def _decode_barcode_to_ean(self, raw_token: Optional[str]) -> Optional[str]:
        if not raw_token:
            return None
        t = raw_token.strip()
        m = self.BARCODE_TOKEN_SPLIT_RE.match(t)
        if not m:
            return None

        decoded_digits: List[str] = []
        lead_digit = self.LEAD_CHAR_TO_DIGIT.get(m.group(1))
        if lead_digit is None:
            return None
        decoded_digits.append(lead_digit)

        for symbol in m.group(2):
            digit = self.LEFT_CHAR_TO_DIGIT.get(symbol)
            if digit is None:
                return None
            decoded_digits.append(digit)

        for symbol in m.group(3):
            digit = self.RIGHT_CHAR_TO_DIGIT.get(symbol)
            if digit is None:
                return None
            decoded_digits.append(digit)

        candidate = "".join(decoded_digits)
        if self._is_valid_ean13(candidate):
            return candidate
        return None

    def _is_delivery(self) -> bool:
        text = self.text_lower
        # Invoice can contain embedded delivery references; prioritize explicit invoice header.
        if "factuurnr." in text or "\nfactuur" in text:
            return False
        return "verzendnr." in text or "verzending" in text

    def _is_noise_line(self, line: str, is_delivery: bool = False) -> bool:
        low = line.lower().strip()
        if not low:
            return True
        # Page / block markers that must not bleed into article descriptions
        if low in {"belgië", "belgie"}:
            return True
        if low == "eenheids" or low.endswith(" eenheids"):
            return True
        if re.fullmatch(r"vle\d{5,}", low):
            return True
        if "omschrijving" in low and "barcode" in low and "nr." in low:
            return True
        # Table headings sometimes appear as separate tokens / merged lines.
        if "nr." in low and "barcode" in low:
            return True
        # BL only: standalone page index (1–4). On invoices the same tokens are often qty (3, 4…).
        if is_delivery and re.fullmatch(r"[1-4]", low):
            return True
        if is_delivery and re.fullmatch(r"vle\d{5,}", low):
            return True
        return (
            low.startswith("sensitivity classification")
            or low.startswith("-- ")
            or "pardaen nv" in low
            or "haachtsesteenweg" in low
            or "info@pardaen.be" in low
            or "www.pardaen.be" in low
            or low.startswith("verzending")
            or low.startswith("factuur")
            or low.startswith("nr. barcode")
            or low.startswith("documentdatum")
            or low.startswith("btw %")
            or low.startswith("subtotaal")
            or low.startswith("totaal")
        )

    def _merge_numeric_sku_prefix_fragments(self, lines: List[str]) -> List[str]:
        """
        PDF layout sometimes splits a reference like \"0-28-510\" into a line ending with \"0-\"
        and the next line starting with \"28-510  X(...)\". Rejoin those fragments so SKU stays complete.
        """
        if not lines:
            return lines
        out: List[str] = []
        i = 0
        frag = re.compile(r"^[0-9]+-$")
        sku_barcode_follows = re.compile(r"^[0-9A-Za-z]{1,}-[0-9A-Za-z][0-9A-Za-z.-]*\s+[XxuU]\(", re.IGNORECASE)
        while i < len(lines):
            s = lines[i].strip()
            nxt_avail = i + 1 < len(lines)
            if (
                nxt_avail
                and frag.fullmatch(s)
                and sku_barcode_follows.match(lines[i + 1].lstrip())
            ):
                combined = f"{s}{lines[i + 1].lstrip()}"
                out.append(combined)
                i += 2
                continue
            out.append(lines[i])
            i += 1
        return out

    def _is_valid_unit(self, unit: str) -> bool:
        u = (unit or "").strip()
        if not u:
            return False
        if self.UNIT_ONLY_RE.match(u):
            return True
        return bool(self._VALID_UNIT_PREFIX_RE.match(u))

    def _sku_trailing_number(self, sku: str) -> Optional[float]:
        """Last segment of refs like 1-0074-12-10 (often 10 or 12) — not the delivered qty."""
        if not sku or "-" not in sku:
            return None
        tail = sku.rsplit("-", 1)[-1].strip()
        if re.fullmatch(r"\d+(?:[.,]\d+)?", tail):
            return self._to_float(tail)
        return None

    _DIMENSION_QTY_RE = re.compile(r"\d+\s*x\s*\d", re.IGNORECASE)
    _BL_RESERVE_QTY_RE = re.compile(
        r"\b(\d+(?:[.,]\d+)?)\s+RESERVEMESJES?\b",
        re.IGNORECASE,
    )
    _PAAR_QTY_UNIT_RE = re.compile(
        r"\b(\d+(?:[.,]\d+)?)\s+Paar(?:\s*\(\d+\s*ST\))?",
        re.IGNORECASE,
    )
    _PAAR_QTY_PRICE_RE = re.compile(
        r"\b(\d+(?:[.,]\d+)?)\s+Paar(?:\s*\(\d+\s*ST\))?\s+(\d+[.,]\d{1,3})\b",
        re.IGNORECASE,
    )
    _PAAR_DESC_TAIL_RE = re.compile(
        r"\s+\d+(?:[.,]\d+)?\s+Paar(?:\s*\(\d+\s*ST\))?(?:\s+\d+[.,]\d{1,3})?\s*$",
        re.IGNORECASE,
    )

    def _qty_match_is_voor_sku_noise(self, text: str, m: re.Match) -> bool:
        """Ignore 0 in 'VOOR 0-28-500' (suffixe SKU, pas la quantité livrée)."""
        qv_str = (m.group(1) or "").strip()
        if not qv_str:
            return False
        start = m.start(1)
        before = text[max(0, start - 10) : start + len(qv_str)]
        after = text[start + len(qv_str) : start + len(qv_str) + 8]
        if re.search(r"VOOR\s+0\s*$", before, re.IGNORECASE):
            return True
        if qv_str in ("0", "0.0") and re.match(r"\s*-\d", after):
            return True
        return False

    def _extract_bl_implicit_qty_unit(self, joined: str) -> Optional[tuple[float, str]]:
        """
        BL Stanley/Pardaen: '10 RESERVEMESJES VOOR 0-28-500' — qté avant RESERVEMESJES,
        pas toujours suivi de Stk/Blister sur la même ligne OCR.
        """
        m = self._BL_RESERVE_QTY_RE.search(joined)
        if not m:
            return None
        qv = self._to_float(m.group(1)) or 0.0
        if qv <= 0:
            return None
        unit = "Stk"
        if re.search(r"\bBlister\b", joined, re.IGNORECASE):
            unit = "Blister"
        elif re.search(r"\bDoos\b", joined, re.IGNORECASE):
            unit = "Doos"
        return qv, unit

    def _qty_match_is_dimension_noise(self, text: str, m: re.Match) -> bool:
        """Ignore 10 in '10x100mm' mistaken for qty (OCR BL/facture descriptions)."""
        start = m.start(1)
        if self._DIMENSION_QTY_RE.match(text[start : start + 20]):
            return True
        qv_str = (m.group(1) or "").strip()
        if not qv_str:
            return False
        # Same number already used in NxM before this qty (…10x100mm…10 Blister…).
        before = text[: start + len(qv_str)]
        if re.search(rf"{re.escape(qv_str)}\s*x\s*\d", before, re.IGNORECASE):
            return True
        return False

    def _extract_dimension_table_qty(
        self, joined: str
    ) -> Optional[tuple[float, str, Optional[float]]]:
        """
        After '10x100mm', table qty is 5 Blister — not the 10 used in the dimension prefix.
        """
        dm = re.search(r"\d+\s*x\s*\d+\s*mm", joined, re.IGNORECASE)
        if not dm:
            return None
        dim_lead_m = re.match(r"(\d+(?:[.,]\d+)?)\s*x", joined[dm.start() : dm.end()], re.IGNORECASE)
        dim_lead = self._to_float(dim_lead_m.group(1)) if dim_lead_m else None
        tail = joined[dm.end() :]
        row_pat = re.compile(
            r"(\d+(?:[.,]\d+)?)\s+(Stk|Blister|Doos(?:\s*\([^)]+\))?)\s+(\d+[.,]\d{2})",
            re.IGNORECASE,
        )
        last: Optional[re.Match] = None
        for m in row_pat.finditer(tail):
            qv = self._to_float(m.group(1)) or 0.0
            if dim_lead is not None and abs(qv - dim_lead) < 0.001:
                continue
            last = m
        if not last:
            return None
        qty = self._to_float(last.group(1)) or 0.0
        if qty <= 0:
            return None
        unit = (last.group(2) or "Stk").strip()
        unit_price = self._to_float(last.group(3))
        return qty, unit, unit_price

    def _only_num_pair_is_dimension_noise(
        self, block_lines: List[str], idx: int, line: str
    ) -> bool:
        num = line.strip()
        if not re.fullmatch(r"\d+(?:[.,]\d+)?", num):
            return False
        prev = block_lines[idx - 1].strip() if idx > 0 else ""
        if prev and re.search(rf"{re.escape(num)}\s*x\s*\d", prev, re.IGNORECASE):
            return True
        if idx + 2 < len(block_lines) and re.match(
            r"^x\s*\d", block_lines[idx + 2].strip(), re.IGNORECASE
        ):
            return True
        return False

    def _pick_best_qty_unit_matches(
        self,
        matches: List[re.Match],
        sku: str,
        prefer_first: bool = False,
    ) -> Optional[tuple[float, str]]:
        if not matches:
            return None
        if prefer_first:
            m = matches[0]
            qv = self._to_float(m.group(1)) or 0.0
            if qv > 0:
                return qv, m.group(2)
            return None

        sku_tail = self._sku_trailing_number(sku)
        unit_rank = {"doos": 3, "blister": 2, "stk": 1}
        has_alt_qty = False
        if sku_tail is not None and len(matches) > 1:
            for m in matches:
                qv = self._to_float(m.group(1)) or 0.0
                if abs(qv - sku_tail) >= 0.001:
                    has_alt_qty = True
                    break

        def rank(m: re.Match) -> tuple:
            qv = self._to_float(m.group(1)) or 0.0
            unit_low = (m.group(2) or "").lower()
            ur = 0
            if unit_low.startswith("doos"):
                ur = unit_rank["doos"]
            elif unit_low.startswith("blister"):
                ur = unit_rank["blister"]
            elif unit_low.startswith("stk"):
                ur = unit_rank["stk"]
            suffix_penalty = 0
            if (
                has_alt_qty
                and sku_tail is not None
                and abs(qv - sku_tail) < 0.001
            ):
                suffix_penalty = 1
            # Prefer last match in line (table qty), not largest number (10 vs 5).
            return (suffix_penalty, -ur, -m.start())

        best = min(matches, key=rank)
        qv = self._to_float(best.group(1)) or 0.0
        if qv > 0:
            return qv, best.group(2)
        return None

    def _extract_qty_unit_price(
        self,
        block_lines: List[str],
        sku: str = "",
        prefer_first: bool = False,
        prefer_last: bool = False,
    ) -> tuple[float, str, Optional[float]]:
        qty = 0.0
        unit = "Stk"
        unit_price = None
        joined = " ".join(block_lines)
        sku_tail = self._sku_trailing_number(sku)

        dim_qty = self._extract_dimension_table_qty(joined)
        if dim_qty:
            return dim_qty

        paar_qty = self._extract_paar_qty_unit_price(joined, block_lines)
        if paar_qty:
            return paar_qty

        qty_unit_pair = re.compile(
            rf"(\d+(?:[.,]\d+)?)\s+{self._QTY_UNIT_WORD}",
            re.IGNORECASE,
        )
        all_matches: List[re.Match] = []
        for m in qty_unit_pair.finditer(joined):
            qv = self._to_float(m.group(1)) or 0.0
            if qv <= 0:
                continue
            if self._qty_match_is_dimension_noise(joined, m):
                continue
            if self._qty_match_is_voor_sku_noise(joined, m):
                continue
            all_matches.append(m)

        metric_last: Optional[re.Match] = None
        for line in block_lines:
            for m in self.METRIC_QTY_UNIT_RE.finditer(line):
                metric_last = m
        if metric_last:
            qty = self._to_float(metric_last.group(2)) or 0.0
            unit = metric_last.group(3)
            unit_low = (unit or "").lower()
            doos_matches = [m for m in all_matches if (m.group(2) or "").lower().startswith("doos")]
            doos_pick = self._pick_best_qty_unit_matches(doos_matches, sku)
            if (
                doos_pick
                and unit_low.startswith("blister")
                and abs(doos_pick[0] - qty) > 0.001
            ):
                qty, unit = doos_pick[0], doos_pick[1]
            price_m = re.search(r"(\d+[.,]\d{2})\s*$", joined)
            if price_m:
                unit_price = self._to_float(price_m.group(1))
            return qty, unit, unit_price

        same_line_pat = re.compile(
            rf"(\d+(?:[.,]\d+)?)\s+{self._QTY_UNIT_WORD}\s+(\d+[.,]\d{{2}})",
            re.IGNORECASE,
        )
        same_line_last: Optional[re.Match] = None
        for same_line in same_line_pat.finditer(joined):
            if not self._qty_match_is_dimension_noise(joined, same_line):
                same_line_last = same_line
        if same_line_last:
            qty = self._to_float(same_line_last.group(1)) or 0.0
            unit = (same_line_last.group(2) or "Stk").strip()
            unit_price = self._to_float(same_line_last.group(3))
            return qty, unit, unit_price

        last_only_num: Optional[tuple[float, str, Optional[float]]] = None
        for idx, line in enumerate(block_lines):
            q = self.DELIVERY_QTY_RE.match(line)
            if q:
                qty = self._to_float(q.group(1)) or 0.0
                unit = q.group(2)
                unit_price = self._to_float(q.group(3))
                return qty, unit, unit_price

            only_num = re.match(r"^\d+(?:[.,]\d+)?$", line.strip())
            if only_num and idx + 1 < len(block_lines):
                if self._only_num_pair_is_dimension_noise(block_lines, idx, line):
                    continue
                next_ln = block_lines[idx + 1].strip()
                if self.UNIT_ONLY_RE.match(next_ln) or self._VALID_UNIT_PREFIX_RE.match(
                    next_ln
                ):
                    qv = self._to_float(line) or 0.0
                    unit = block_lines[idx + 1]
                    up: Optional[float] = None
                    if idx + 2 < len(block_lines) and self.MONEY_RE.fullmatch(
                        block_lines[idx + 2]
                    ):
                        up = self._to_float(block_lines[idx + 2])
                    last_only_num = (qv, unit, up)
        if last_only_num:
            return last_only_num

        if prefer_last and all_matches:
            m = all_matches[-1]
            qv = self._to_float(m.group(1)) or 0.0
            if qv > 0:
                return qv, (m.group(2) or "Stk").strip(), unit_price

        picked = self._pick_best_qty_unit_matches(all_matches, sku, prefer_first=prefer_first)
        if picked:
            return picked[0], picked[1], unit_price

        implicit = self._extract_bl_implicit_qty_unit(joined)
        if implicit:
            if unit_price is None:
                price_m = re.search(r"(\d+[.,]\d{2})\s*$", joined)
                if price_m:
                    unit_price = self._to_float(price_m.group(1))
            return implicit[0], implicit[1], unit_price

        if qty <= 0 and unit != "Stk":
            money_values = [
                self._to_float(x.group(0))
                for x in self.MONEY_RE.finditer(joined)
            ]
            money_values = [v for v in money_values if v is not None]
            if len(money_values) >= 2 and money_values[-2] > 0:
                inferred = round(money_values[-1] / money_values[-2])
                if inferred > 0:
                    qty = float(inferred)
                    unit_price = money_values[-2]

        return qty, unit, unit_price

    def _extract_paar_qty_unit_price(
        self, joined: str, block_lines: List[str]
    ) -> Optional[tuple[float, str, Optional[float]]]:
        """BL Pardaen: '6 Paar (2 ST) 9,53' — unité Paar, pas Stk/Blister."""
        m = self._PAAR_QTY_PRICE_RE.search(joined)
        if m:
            qty = self._to_float(m.group(1)) or 0.0
            if qty > 0:
                return qty, "Paar", self._to_float(m.group(2))

        m = self._PAAR_QTY_UNIT_RE.search(joined)
        if m:
            qty = self._to_float(m.group(1)) or 0.0
            if qty > 0:
                unit_price = None
                price_m = re.search(r"(\d+[.,]\d{1,3})\s*$", joined)
                if price_m:
                    unit_price = self._to_float(price_m.group(1))
                return qty, "Paar", unit_price

        for idx, line in enumerate(block_lines):
            if not re.fullmatch(r"\d+(?:[.,]\d+)?", line.strip()):
                continue
            if idx + 1 >= len(block_lines):
                continue
            next_ln = block_lines[idx + 1].strip()
            if not re.match(r"^paar\b", next_ln, re.IGNORECASE):
                continue
            qty = self._to_float(line) or 0.0
            if qty <= 0:
                continue
            unit_price = None
            if idx + 2 < len(block_lines) and self.MONEY_RE.fullmatch(
                block_lines[idx + 2].strip()
            ):
                unit_price = self._to_float(block_lines[idx + 2])
            return qty, "Paar", unit_price
        return None

    def _standalone_sku_followed_by_barcode_line(self, lines: List[str], idx: int) -> bool:
        """
        When PDF/OCR emits one fragment per line, a real article starts with SKU + next line barcode token X( / U(...
        Lines like \"28-500\" (continuation after \"VOOR 0-\\n\") match SKU_RE but must not start a row.
        """
        if idx + 1 >= len(lines):
            return False
        nl = lines[idx + 1].lstrip()
        if re.match(r"^[XxuU]\(", nl):
            return True
        if re.fullmatch(r"\d{8,14}", nl):
            return True
        return False

    def _line_starts_with_product_sku(self, line: str) -> bool:
        """Real Pardaen article ref at line start (not description fragments like 8-DELIGE)."""
        return bool(self._STRICT_SKU_PREFIX_RE.match(line))

    def _is_bl_continuation_line(self, line: str) -> bool:
        """Description/qty fragments and orphan barcode rows under a prior SKU."""
        s = line.strip()
        if not s:
            return True
        if self._line_starts_with_product_sku(line):
            return False
        if re.match(r"^[XxuU]\([A-Za-z0-9*]+\(", s, re.IGNORECASE):
            return True
        low = s.lower()
        if low in {"st)", "st"}:
            return True
        if low.startswith(("koetswerk", "verzinkt", "plat ", "metaalbouten", "verbind")):
            return True
        if re.search(r"\d+\s+(?:doos|blister|stk)\b", low, re.IGNORECASE):
            return True
        return bool(line.startswith((" ", "\t")))

    def _starts_new_product_row(self, lines: List[str], j: int, is_delivery: bool) -> bool:
        if j >= len(lines):
            return False
        line = lines[j]
        if is_delivery and not self._line_starts_with_product_sku(line):
            return False
        m = self.DENSE_ROW_SKU_RE.match(line)
        if m:
            return "-" in m.group(1)
        m = self.SKU_RE.match(line)
        if not m or "-" not in m.group(1):
            return False
        # Facture (OCR vertical): SKU seul sur une ligne = nouvelle ligne article.
        if not is_delivery:
            return True
        # BL: ignorer les morceaux type \"28-500\" (suite de \"VOOR 0-\\n\") sans jeton barcode.
        return self._standalone_sku_followed_by_barcode_line(lines, j)

    _INVOICE_SKU_LINE_RE = re.compile(
        rf"^\s*({_INVOICE_SKU})\s*(.*)$",
        re.IGNORECASE,
    )

    def _try_invoice_loose_row(self, lines: List[str], idx: int) -> Optional[Dict]:
        """
        OCR facture page 1: SKU + description on one line, qty sometimes on indented lines below.
        """
        m = self._INVOICE_SKU_LINE_RE.match(lines[idx])
        if not m:
            return None
        sku = m.group(1).strip()
        if not re.fullmatch(self._INVOICE_SKU, sku, re.IGNORECASE):
            return None
        tail = (m.group(2) or "").strip()
        block: List[str] = [tail] if tail else []
        j = idx + 1
        while j < len(lines) and j < idx + 10:
            nxt = lines[j]
            if self._INVOICE_SKU_LINE_RE.match(nxt):
                break
            if self._is_noise_line(nxt, False):
                j += 1
                continue
            if self._STRICT_SKU_PREFIX_RE.match(nxt):
                break
            block.append(nxt)
            j += 1
        joined = " ".join(block)
        if "verzendnr." in joined.lower() or "uw referentie" in joined.lower():
            joined = re.sub(
                r"(?i)verzendnr\.\s*vle\d+:?|uw referentie:\s*[^\d]+",
                " ",
                joined,
            ).strip()
        qty, unit, _ = self._extract_qty_unit_price(block, sku, prefer_last=True)
        unit_price, line_total, discount = self._extract_invoice_prices_from_tail(block, qty)
        unit_price, line_total = self._finalize_invoice_line_amounts(
            unit_price, line_total, qty
        )
        if qty <= 0:
            if unit_price and line_total and unit_price > 0:
                inferred = round(line_total / unit_price)
                if inferred > 0:
                    qty = float(inferred)
            if qty <= 0:
                return None
        desc = re.sub(r"\s+", " ", joined).strip()
        desc = self._QTY_PRICE_TAIL_RE.sub("", desc).strip()
        desc = self._QTY_BLISTER_TAIL_NOPRICE_RE.sub("", desc).strip()
        if not desc or desc.lower() == sku.lower():
            desc = sku
        return {
            "item": {
                "qty": qty,
                "unit": unit,
                "sku": sku,
                "description": desc,
                "ean": None,
                "barcode_raw": None,
                "barcode_normalized": None,
                "discount": discount,
                "unit_price": unit_price,
                "line_total": line_total,
            },
            "next_i": j,
        }

    def _infer_net_qty_from_bruto_and_line_total(
        self, bruto: float, line_total: float, max_qty: int = 48
    ) -> tuple[Optional[float], Optional[float]]:
        """Quand OCR ne garde que bruto + regelbedrag (qty inconnue)."""
        if bruto <= 0 or line_total <= 0 or line_total <= bruto * 0.5:
            return None, None

        best_q: Optional[int] = None
        best_net: Optional[float] = None
        best_score: Optional[float] = None
        # Pardaen facture : korting 41 % → netto ≈ 59 % du bruto
        target_net = bruto * 0.59
        for q in range(1, max_qty + 1):
            net = round(line_total / q, 4)
            if net >= bruto * 0.98 or net <= bruto * 0.05:
                continue
            if abs(round(net * q, 2) - line_total) > 0.06:
                continue
            score = abs(net - target_net)
            if best_score is None or score < best_score:
                best_q, best_net, best_score = q, net, score
        if best_q is None or best_net is None:
            return None, None
        return best_net, line_total

    def _is_invoice_korting_column_value(
        self, value: float, bruto: float, net: float
    ) -> bool:
        """Korting % (souvent 41 chez Pardaen) — pas un prix unitaire."""
        rounded = round(value)
        if abs(value - rounded) > 0.05:
            return False
        if rounded < 10 or rounded > 99:
            return False
        if abs(value - bruto) <= 0.05 or abs(value - net) <= 0.05:
            return False
        return value > net * 1.5

    def _sanitize_invoice_money_values(self, money_values: List[float]) -> List[float]:
        """Retire korting % (41, 41,00) et faux décimaux type '1.5 mm' avant bruto."""
        if not money_values:
            return []

        values = list(money_values)
        if len(values) >= 4 and values[0] < min(values[1], values[-1]) * 0.35:
            values = values[1:]

        if len(values) < 3:
            return values

        bruto = values[0]
        net = values[-2]
        total = values[-1]
        middle = values[1:-2]
        cleaned_middle = [
            v
            for v in middle
            if not self._is_invoice_korting_column_value(v, bruto, net)
        ]
        if not cleaned_middle:
            return [bruto, net, total]
        return [bruto, *cleaned_middle, net, total]

    def _resolve_invoice_net_price(
        self,
        bruto: Optional[float],
        net: Optional[float],
        line_total: Optional[float],
        qty: Optional[float],
    ) -> tuple[Optional[float], Optional[float]]:
        """
        Keep netto read from the invoice column (5,8 / 0,068 / 0,742).
        Use regelbedrag / qty only when net is missing or clearly wrong (brut taken as net).
        """
        q = float(qty or 0)
        if q <= 0 or line_total is None:
            if (
                line_total is not None
                and bruto is not None
                and net is not None
                and abs(net - bruto) < 0.02
                and line_total > bruto * 1.2
            ):
                inferred = self._infer_net_qty_from_bruto_and_line_total(bruto, line_total)
                if inferred[0] is not None:
                    return inferred
            return net, line_total

        derived = round(line_total / q, 4)
        if net is None:
            return derived, line_total

        line_total_r = round(line_total, 2)
        line_from_net = round(net * q, 2)
        # Slack for regelbedrag rounding (0,068×200, 5,8×16…); +epsilon avoids float edge cases.
        slack = max(0.07, min(0.35, 0.002 * q))
        if abs(line_from_net - line_total_r) <= slack + 1e-6:
            return net, line_total

        line_from_derived = round(derived * q, 2)
        if abs(line_from_derived - line_total_r) > 0.03:
            return net, line_total

        if (
            bruto is not None
            and abs(net - bruto) < 0.02
            and abs(derived - bruto) > 0.02
        ):
            return derived, line_total

        ref = max(abs(derived), 0.01)
        if abs(net - derived) >= max(0.05, 0.08 * ref):
            return derived, line_total

        return net, line_total

    def _pick_invoice_net_unit_price(
        self,
        money_values: List[float],
        qty: Optional[float] = None,
    ) -> tuple[Optional[float], Optional[float]]:
        """
        Pardaen invoice columns: Bruto eenheidsprijs, [Korting %], Netto eenheidsprijs, Regelbedrag.
        Korting (e.g. 50+10) is not matched by MONEY_RE — only decimal amounts appear.
        """
        if not money_values:
            return None, None

        money_values = self._sanitize_invoice_money_values(money_values)
        bruto = money_values[0] if money_values else None
        net: Optional[float]
        line_total: Optional[float]
        q = float(qty or 0)

        if len(money_values) >= 3:
            net, line_total = money_values[-2], money_values[-1]
        elif len(money_values) == 2:
            gross, second = money_values[0], money_values[1]
            # [bruto, net] sans regelbedrag
            if second < gross and 0.05 <= (second / gross) <= 0.99:
                net, line_total = second, None
            # [bruto, regelbedrag] — net = regelbedrag / qty (ou qty inférée)
            elif second > gross:
                if q > 0:
                    net, line_total = round(second / q, 4), second
                else:
                    inferred = self._infer_net_qty_from_bruto_and_line_total(
                        gross, second
                    )
                    if inferred[0] is not None:
                        net, line_total = inferred
                    else:
                        net, line_total = gross, second
            else:
                net, line_total = gross, second
        else:
            net, line_total = money_values[0], None

        return self._resolve_invoice_net_price(bruto, net, line_total, qty)

    @staticmethod
    def _format_discount_value(raw: Optional[str]) -> Optional[str]:
        if not raw:
            return None
        t = str(raw).strip()
        if not t:
            return None
        if t.endswith("%"):
            return t
        if re.fullmatch(r"\d+\+\d+", t):
            return t
        return f"{t}%"

    def _extract_invoice_discount_from_tail(self, tail_lines: List[str]) -> Optional[str]:
        """Colonne Korting % (header « % ») dans le bloc prix après qté/unité."""
        for line in tail_lines:
            s = line.strip()
            if self._is_invoice_korting_line(s):
                return self._format_discount_value(s)
        return None

    def _append_invoice_row_match(self, items: List[Dict], match: re.Match) -> None:
        qty = self._to_float(match.group(3)) or 0.0
        unit = match.group(4).strip()
        bruto = self._to_float(match.group(5))
        discount = self._format_discount_value(match.group(6))
        net = self._to_float(match.group(7))
        line_total = self._to_float(match.group(8))
        unit_price, line_total = self._resolve_invoice_net_price(bruto, net, line_total, qty)
        unit_price, line_total = self._finalize_invoice_line_amounts(
            unit_price, line_total, qty
        )
        items.append(
            {
                "qty": qty,
                "unit": unit,
                "sku": match.group(1).strip(),
                "description": match.group(2).strip(),
                "ean": None,
                "barcode_raw": None,
                "barcode_normalized": None,
                "discount": discount,
                "unit_price": unit_price,
                "line_total": line_total,
            }
        )

    def _append_invoice_promo_row_match(self, items: List[Dict], match: re.Match) -> None:
        """Ligne facture 100% korting (netto 0,00) — qty/unit en groupes 3-4."""
        qty = self._to_float(match.group(3)) or 0.0
        unit = match.group(4).strip()
        discount = self._format_discount_value(match.group(5))
        net = self._to_float(match.group(6)) or 0.0
        items.append(
            {
                "qty": qty,
                "unit": unit,
                "sku": match.group(1).strip(),
                "description": match.group(2).strip(),
                "ean": None,
                "barcode_raw": None,
                "barcode_normalized": None,
                "discount": discount,
                "unit_price": net,
                "line_total": round(net * qty, 2) if qty > 0 else net,
            }
        )

    def _extract_invoice_prices_from_tail(
        self, tail_lines: List[str], qty: Optional[float] = None
    ) -> tuple[Optional[float], Optional[float], Optional[str]]:
        """Collect price lines after qty+unit; skip Korting % and Btw % rows."""
        discount = self._extract_invoice_discount_from_tail(tail_lines)
        money_after_unit: List[float] = []
        seen_unit = False
        for line in tail_lines:
            s = line.strip()
            if self._is_invoice_korting_line(s):
                continue
            if seen_unit and re.fullmatch(r"\d{1,2}", s):
                # Btw % (souvent 21) — pas confondre avec korting % (41, 50…).
                if money_after_unit and s in ("21", "6", "12"):
                    break
                continue
            if self.UNIT_ONLY_RE.match(s) or self._VALID_UNIT_PREFIX_RE.match(s):
                seen_unit = True
                continue
            compact = s.replace(" ", "")
            if seen_unit and self.INVOICE_MONEY_RE.fullmatch(compact):
                v = self._to_float(s)
                if v is not None:
                    money_after_unit.append(v)
        if money_after_unit:
            unit_price, line_total = self._pick_invoice_net_unit_price(money_after_unit, qty)
            return unit_price, line_total, discount
        joined = " ".join(tail_lines)
        money_values = [
            self._to_float(x.group(0))
            for x in self.INVOICE_MONEY_RE.finditer(joined)
        ]
        money_values = [v for v in money_values if v is not None]
        unit_price, line_total = self._pick_invoice_net_unit_price(money_values, qty)
        return unit_price, line_total, discount

    def _clean_invoice_description(self, desc: str) -> str:
        out = (desc or "").strip()
        out = self._INVOICE_TABLE_TAIL_RE.sub("", out).strip()
        out = self._MID_QTY_UNIT_PRICE_RE.sub(" ", out).strip()
        out = self._QTY_PRICE_TAIL_RE.sub("", out).strip()
        out = self._QTY_BLISTER_TAIL_NOPRICE_RE.sub("", out).strip()
        return out

    @staticmethod
    def _normalize_unit_key(unit: Optional[str]) -> str:
        return (unit or "").strip().upper()

    def _item_descriptions_similar(self, existing: Dict, incoming: Dict) -> bool:
        da = re.sub(r"\s+", " ", (existing.get("description") or "").strip().lower())
        db = re.sub(r"\s+", " ", (incoming.get("description") or "").strip().lower())
        if not da or not db:
            return da == db
        if da in db or db in da:
            return True
        ta, tb = set(da.split()), set(db.split())
        if not ta or not tb:
            return False
        return len(ta & tb) / min(len(ta), len(tb)) >= 0.55

    def _delivery_rows_are_page_repeat(self, existing: Dict, incoming: Dict) -> bool:
        """Multi-page BL: same article reprinted — drop, do not sum or max qty."""
        if not self._item_descriptions_similar(existing, incoming):
            return False
        qa = float(existing.get("qty") or 0)
        qb = float(incoming.get("qty") or 0)
        if abs(qa - qb) < 0.001:
            return True
        dim_pat = re.compile(r"\d+(?:[.,]\d+)?\s*x\s*\d+", re.IGNORECASE)
        da = (existing.get("description") or "").lower()
        db = (incoming.get("description") or "").lower()
        ma, mb = dim_pat.search(da), dim_pat.search(db)
        return bool(ma and mb and ma.group(0) == mb.group(0))

    def _is_invoice_korting_line(self, text: str) -> bool:
        """Colonne Korting % (41, 50+10…) — pas un montant."""
        t = text.strip()
        if re.fullmatch(r"\d+\+\d+", t):
            return True
        if re.fullmatch(r"\d{1,3}", t):
            try:
                v = int(t)
                return 10 <= v <= 99
            except ValueError:
                return False
        return False

    def _advance_invoice_price_tail_end(self, lines: List[str], j: int) -> int:
        """
        Facture verticale : après Bruto, lire Korting %, Netto eenheidsprijs, Regelbedrag, Btw %.
        """
        while j < len(lines):
            t = lines[j].strip()
            if self._line_starts_with_product_sku(lines[j]):
                break
            if re.fullmatch(r"VLE\d{5,}", t, re.IGNORECASE):
                break
            if self._is_invoice_korting_line(t):
                j += 1
                continue
            if self.INVOICE_MONEY_RE.fullmatch(t.replace(" ", "")):
                j += 1
                continue
            if re.fullmatch(r"\d{1,2}", t) and t in ("21", "6", "12"):
                j += 1
                break
            if self._bl_vertical_looks_like_order_qty_line(lines, j, ""):
                break
            break
        return j

    def _bl_vertical_tail_end_index(
        self, lines: List[str], qty_start: int, *, invoice: bool = False
    ) -> int:
        money_re = self.INVOICE_MONEY_RE if invoice else self.MONEY_RE
        j = qty_start
        while j < len(lines):
            s = lines[j].strip()
            if self._line_starts_with_product_sku(lines[j]):
                break
            if self._BARCODE_ONLY_LINE_RE.match(s):
                break
            if re.fullmatch(r"VLE\d{5,}", s, re.IGNORECASE):
                break
            if money_re.fullmatch(s.replace(" ", "")):
                j += 1
                if invoice:
                    j = self._advance_invoice_price_tail_end(lines, j)
                break
            if self._bl_vertical_looks_like_order_qty_line(lines, j, ""):
                j += 1
                while j < len(lines):
                    t = lines[j].strip().lower()
                    if self._line_starts_with_product_sku(lines[j]):
                        break
                    if money_re.fullmatch(lines[j].strip().replace(" ", "")):
                        j += 1
                        if invoice:
                            j = self._advance_invoice_price_tail_end(lines, j)
                        break
                    if t.startswith(("doos", "blister", "stk")) or t == "st)":
                        j += 1
                        continue
                    if re.fullmatch(r"\d+(?:[.,]\d+)?", lines[j].strip()):
                        break
                    j += 1
                break
            j += 1
        return j

    def _merge_duplicate_item_row(self, existing: Dict, incoming: Dict) -> None:
        """Same SKU on multiple document lines (lots / variants) → sum qty when unit matches."""
        ex_unit = self._normalize_unit_key(existing.get("unit"))
        it_unit = self._normalize_unit_key(incoming.get("unit"))
        if not ex_unit and it_unit:
            existing["unit"] = incoming.get("unit")
            ex_unit = it_unit
        ex_q = float(existing.get("qty") or 0)
        it_q = float(incoming.get("qty") or 0)
        ex_up = float(existing.get("unit_price") or 0)
        it_up = float(incoming.get("unit_price") or 0)
        ex_lt = float(existing.get("line_total") or 0)
        it_lt = float(incoming.get("line_total") or 0)
        duplicate_zero_promo = (
            ex_up == 0
            and it_up == 0
            and ex_lt == 0
            and it_lt == 0
            and abs(ex_q - it_q) < 0.001
        )
        if not it_unit or ex_unit == it_unit:
            if duplicate_zero_promo:
                existing["qty"] = max(ex_q, it_q)
            else:
                existing["qty"] = ex_q + it_q

        ex_lt = existing.get("line_total")
        it_lt = incoming.get("line_total")
        if ex_lt is not None and it_lt is not None:
            existing["line_total"] = float(ex_lt) + float(it_lt)
        elif it_lt is not None and ex_lt is None:
            existing["line_total"] = it_lt

        if not existing.get("ean") and incoming.get("ean"):
            existing["ean"] = incoming.get("ean")
        if not existing.get("barcode_raw") and incoming.get("barcode_raw"):
            existing["barcode_raw"] = incoming.get("barcode_raw")
        if not existing.get("barcode_normalized") and incoming.get("barcode_normalized"):
            existing["barcode_normalized"] = incoming.get("barcode_normalized")
        if incoming.get("unit_price") is not None and existing.get("unit_price") is None:
            existing["unit_price"] = incoming.get("unit_price")

        ex_desc = (existing.get("description") or "").strip()
        it_desc = (incoming.get("description") or "").strip()
        if not ex_desc:
            existing["description"] = it_desc
        elif it_desc and len(it_desc) > len(ex_desc):
            existing["description"] = it_desc

    def _consolidate_items_by_sku(self, items: List[Dict]) -> List[Dict]:
        """One row per SKU; sum quantities for repeated references (e.g. split lots on BL)."""
        by_sku: Dict[str, Dict] = {}
        ordered: List[Dict] = []
        for it in items:
            sku = (it.get("sku") or "").strip()
            if not sku:
                continue
            existing = by_sku.get(sku)
            if existing is None:
                by_sku[sku] = it
                ordered.append(it)
                continue
            self._merge_duplicate_item_row(existing, it)
        return ordered

    def _dedupe_delivery_page_repeats(self, items: List[Dict]) -> List[Dict]:
        out: List[Dict] = []
        index_by_key: Dict[tuple, int] = {}
        for it in items:
            sku = (it.get("sku") or "").strip()
            unit = self._normalize_unit_key(it.get("unit"))
            up = round(float(it.get("unit_price") or 0), 2)
            key = (sku, unit, up)
            if key not in index_by_key:
                index_by_key[key] = len(out)
                out.append(it)
                continue
            if self._delivery_rows_are_page_repeat(out[index_by_key[key]], it):
                continue
            out.append(it)
        return out

    def _dedupe_identical_items(self, items: List[Dict]) -> List[Dict]:
        """OCR répète parfois le même bloc article — ne pas additionner les qté ensuite."""
        seen: set[tuple] = set()
        out: List[Dict] = []
        for it in items:
            sku = (it.get("sku") or "").strip()
            desc = re.sub(r"\s+", " ", (it.get("description") or "")).strip().lower()[:120]
            qty = round(float(it.get("qty") or 0), 4)
            unit = self._normalize_unit_key(it.get("unit"))
            up = round(float(it.get("unit_price") or 0), 4)
            lt = it.get("line_total")
            lt_key = round(float(lt), 4) if lt is not None else None
            key = (sku, qty, unit, up, lt_key, desc)
            if key in seen:
                continue
            seen.add(key)
            out.append(it)
        return out

    @staticmethod
    def _sum_items_excl_vat(items: List[Dict]) -> float:
        """Total HT = somme des Regelbedrag (line_total), sinon netto × qté."""
        total = 0.0
        for it in items:
            lt = it.get("line_total")
            up = float(it.get("unit_price") or 0)
            qty = float(it.get("qty") or 0)
            if lt is not None and float(lt) >= 0:
                total += float(lt)
            elif up > 0 and qty > 0:
                total += round(up * qty, 2)
        return round(total, 2)

    def _extract_invoice_total_excl_vat(self) -> Optional[float]:
        """Subtotaal / totaal HT en pied de facture Pardaen (ex. 3.253,86)."""
        if not self.full_text:
            return None

        candidates: List[float] = []
        patterns = [
            r"(?i)subtotaal[^\d]{0,60}(\d{1,3}(?:\.\d{3})*,\d{2})",
            r"(?i)totaal\s+excl\.?[^\d]{0,40}(\d{1,3}(?:\.\d{3})*,\d{2})",
            r"(?i)netto\s+bedrag\s+excl\.?[^\d]{0,40}(\d{1,3}(?:\.\d{3})*,\d{2})",
            r"(?i)subtotaal[^\d]{0,60}(\d+,\d{2})",
        ]
        for pat in patterns:
            for m in re.finditer(pat, self.full_text):
                v = self._to_float(m.group(1))
                if v is not None and v > 100:
                    candidates.append(v)

        if not candidates:
            return None
        # Dernier subtotaal avant bloc TVA (souvent le total articles)
        return candidates[-1]

    def summarize_invoice_amounts(self, items: List[Dict]) -> Dict[str, Optional[float]]:
        lines_total = self._sum_items_excl_vat(items)
        footer = self._extract_invoice_total_excl_vat()
        diff = round(footer - lines_total, 2) if footer is not None else None
        return {
            "lines_total_excl_vat": lines_total,
            "invoice_total_excl_vat": footer,
            "total_discrepancy": diff,
        }

    def _backfill_regelbedrag_from_embedded_rows(self, items: List[Dict]) -> None:
        """Lignes tableau complètes (Bruto/Netto/Regelbedrag) → regelbedrag fiable pour la somme HT."""
        if not self.full_text:
            return
        by_sku: Dict[str, tuple[float, float, float, Optional[str]]] = {}
        for line in self.full_text.splitlines():
            m = self.INVOICE_LINE_FIND_RE.search(line.strip())
            if not m:
                continue
            sku = m.group(1).strip().upper()
            ref_qty = self._to_float(m.group(3)) or 0.0
            ref_discount = self._format_discount_value(m.group(6))
            ref_net = self._to_float(m.group(7)) or 0.0
            regel = self._to_float(m.group(8)) or 0.0
            if regel > 0:
                by_sku[sku] = (ref_qty, ref_net, regel, ref_discount)

        for it in items:
            sku = (it.get("sku") or "").strip().upper()
            if sku not in by_sku:
                continue
            ref_qty, ref_net, regel, ref_discount = by_sku[sku]
            qty = float(it.get("qty") or 0)
            if ref_qty > 0 and qty > 0 and abs(qty - ref_qty) / max(ref_qty, qty) > 0.05:
                continue
            it["line_total"] = regel
            if ref_net > 0:
                it["unit_price"] = ref_net
            if ref_discount and not it.get("discount"):
                it["discount"] = ref_discount

    def _finalize_invoice_items(self, items: List[Dict]) -> List[Dict]:
        """Drop OCR garbage, then one row per SKU with summed quantities."""
        cleaned: List[Dict] = []
        for it in items:
            sku = (it.get("sku") or "").strip()
            desc = self._clean_invoice_description((it.get("description") or "").strip())
            it = {**it, "description": desc}
            if not re.fullmatch(self._INVOICE_SKU, sku, re.IGNORECASE):
                continue
            if "nr." in desc.lower() and "omschrijving" in desc.lower():
                continue
            if len(desc) > 120 and re.search(r"\b\d+-\d+-\d+\b", desc[40:]):
                continue
            cleaned.append(it)
        cleaned = self._dedupe_identical_items(cleaned)
        consolidated = self._consolidate_items_by_sku(cleaned)
        self._backfill_regelbedrag_from_embedded_rows(consolidated)
        for it in consolidated:
            lt = it.get("line_total")
            if lt is not None:
                it["line_total"] = round(float(lt), 2)
                continue
            up = float(it.get("unit_price") or 0)
            qty = float(it.get("qty") or 0)
            if up > 0 and qty > 0:
                it["line_total"] = round(up * qty, 2)
        return consolidated

    def _is_weak_delivery_item(self, sku: str, description: str, block_lines: List[str]) -> bool:
        if re.fullmatch(r"M\d+", description.strip(), re.IGNORECASE):
            if self._recover_description_via_mm_scan(block_lines, sku):
                return False
            return True
        if re.search(r"^[XxuU]\(", description):
            return True
        if re.search(r"\bS-\d", description) and description.index("S-") > 20:
            return True
        return False

    def _split_dense_line_tail(self, tail: str) -> List[str]:
        """After the SKU, one line can contain 'X(..(' + description + qty; split for downstream heuristics."""
        tail = tail.strip()
        if not tail:
            return []
        bm = re.match(r"^([XxuU]\([A-Za-z0-9*]+\()\s*(.*)$", tail, re.IGNORECASE)
        if not bm:
            return [tail]
        first, rest = bm.group(1).strip(), bm.group(2).strip()
        if rest:
            return [first, rest]
        return [first]

    def _is_bundle_or_table_header_fragment(self, text: str) -> bool:
        """Stop building description — bundled blade ref / OCR table headings."""
        low = text.lower().strip()
        if not low:
            return True
        if re.search(r"\bvoor\s+0-", low):
            return True
        # Loose blade ref line (Stanley VOOR 10-…) — not the main FATMAX titre.
        if "reserve" in low and "mesje" in low:
            if re.search(r"\bvoor\s+0-", low) or re.match(r"^\d+\s+", low):
                return True
        if "omschrijving" in low and "barcode" in low:
            return True
        if "omschrijving" in low and "2" in low and len(low) <= 140:
            return True
        if "aantal" in low and "eenheid" in low and "bruto" in low:
            return True
        if "prijs" in low and "excl" in low and ("btw" in low or "eur" in low):
            return True
        if "+32" in low and "251" in low:
            return True
        return False

    _QTY_PRICE_TAIL_RE = re.compile(
        r"\s+\d+(?:[.,]\d+)?\s+(?:Stk|Blister|Doos(?:\s*\([^)]+\))?)\s+\d+[.,]\d{2}\s*$",
        re.IGNORECASE,
    )
    _QTY_BLISTER_TAIL_NOPRICE_RE = re.compile(
        r"\s+\d+(?:[.,]\d+)?\s+(?:Stk|Blister|Doos(?:\s*\([^)]+\))?)\s+prijs\s+excl\.\s*$",
        re.IGNORECASE,
    )
    # BL dense/OCR: qty+unit+price embedded mid-description before a colour line (e.g. FLUOGROEN).
    _MID_QTY_UNIT_PRICE_RE = re.compile(
        r"\s+\d+(?:[.,]\d+)?\s+(?:Stk|Blister|Doos(?:\s*\([^)]+\))?)\s+\d+[.,]\d{1,3}\b",
        re.IGNORECASE,
    )
    _DIM_MM_TITLE_RE = re.compile(
        r"(?P<title>[A-Za-z][A-Za-z0-9,\.\-\'/&\s]{5,}?)\s+(?P<dim>\d{1,4}(?:[.,]\d+)?)\s*mm\b",
        re.IGNORECASE,
    )
    # Last resort when horizontal titre is nowhere in OCR after this SKU's block (matches barcode_normalized).
    _FALLBACK_DESC_BY_BARCODE_INNER = {
        "25DFG0*MSPLKO": "WATERPAS FATMAX MLH 800 mm",
    }

    def _title_from_dimension_match(self, m: re.Match) -> Optional[str]:
        title = re.sub(r"\s+", " ", m.group("title").strip())
        dim = m.group("dim").replace(",", ".").strip()
        low = title.lower()
        if len(title) < 6:
            return None
        letters = sum(c.isalpha() for c in title)
        if letters < 8:
            return None
        if self._is_bundle_or_table_header_fragment(title):
            return None
        if "reserve" in low and "mesje" in low:
            return None
        if "nr." in low and "barcode" in low:
            return None
        return f"{title} {dim} mm"

    def _recover_description_via_mm_scan(self, block_lines: List[str], sku: str) -> Optional[str]:
        """When OCR loses the titre in desc_parts (vertical layout), reuse '… NN mm' from block or repeated BL blocks."""
        sku_s = sku.strip()

        def scan_blob(blob: str) -> Optional[str]:
            low_blob = blob.lower()
            cut = len(blob)
            for key in ("reservermesjes", "reserve mesjes", "voor 0-"):
                i = low_blob.find(key)
                if i >= 80 and i < cut:
                    cut = i
            trimmed = blob[:cut]
            flat2 = re.sub(r"\s+", " ", trimmed)
            best: Optional[str] = None
            for m in self._DIM_MM_TITLE_RE.finditer(flat2):
                cand = self._title_from_dimension_match(m)
                if cand and (best is None or len(cand) > len(best)):
                    best = cand
            return best

        hit = scan_blob(" ".join(block_lines))
        if hit:
            return hit

        for sku_m in re.finditer(re.escape(sku_s), self.full_text):
            chunk = self.full_text[sku_m.start() : sku_m.start() + 5500]
            hit = scan_blob(chunk)
            if hit:
                return hit
        return None

    def _finalize_item_description(self, parts: List[str], sku: str, block_lines: List[str]) -> str:
        kept: List[str] = []
        _bundle_tail = re.compile(r"\s+\d+\s+RESERVE\w*MESJES.*$", re.IGNORECASE)
        for raw in parts:
            s = raw.strip()
            if not s:
                continue
            s = _bundle_tail.sub("", s).strip()
            if not s:
                continue
            low = s.lower()
            if " voor 0-" in low:
                prefix = re.split(r"\s+voor\s+0-", s, maxsplit=1, flags=re.IGNORECASE)[0].strip()
                # Bundle-only line → skip remaining segments.
                if (
                    prefix
                    and not re.search(r"reserver?mesjes|reserve.*mesje", prefix, re.IGNORECASE)
                    and sum(c.isalpha() for c in prefix) >= 3
                ):
                    kept.append(prefix)
                break
            if self._is_bundle_or_table_header_fragment(s):
                break
            kept.append(s)
        out = " ".join(kept).strip()
        out = self._MID_QTY_UNIT_PRICE_RE.sub(" ", out).strip()
        out = self._PAAR_DESC_TAIL_RE.sub("", out).strip()
        out = self._QTY_PRICE_TAIL_RE.sub("", out).strip()
        out = self._QTY_BLISTER_TAIL_NOPRICE_RE.sub("", out).strip()
        sku_s = sku.strip()
        if not out or out == sku_s:
            rec = self._recover_description_via_mm_scan(block_lines, sku_s)
            if rec:
                out = rec
        return out or sku_s

    def _fallback_description_known_barcode(self, barcode_normalized: Optional[str]) -> Optional[str]:
        if not barcode_normalized:
            return None
        return self._FALLBACK_DESC_BY_BARCODE_INNER.get(barcode_normalized.strip().upper())

    _BARCODE_ONLY_LINE_RE = re.compile(r"^[XxuUyY]\([A-Za-z0-9*]+\($", re.IGNORECASE)
    _BL_SKU_ONLY_LINE_RE = re.compile(
        rf"^\s*({_INVOICE_SKU})\s*$",
        re.IGNORECASE,
    )

    def _bl_vertical_looks_like_order_qty_line(
        self, lines: List[str], j: int, sku: str
    ) -> bool:
        """True when a lone number line is order qty (next tokens are Blister/Doos/Stk)."""
        if j >= len(lines):
            return False
        s = lines[j].strip()
        if not re.fullmatch(r"\d+(?:[.,]\d+)?$", s):
            return False
        if j + 1 >= len(lines):
            return False
        t = lines[j + 1].strip()
        low = t.lower()
        return bool(
            self.UNIT_ONLY_RE.match(t)
            or low.startswith(("doos", "blister", "stk", "paar"))
            or low == "st)"
        )

    def _repair_bl_vertical_item(
        self,
        item: Dict,
        desc_parts: List[str],
        sku: str,
        tail_lines: List[str],
    ) -> Dict:
        desc = (item.get("description") or "").strip()
        unit = (item.get("unit") or "").strip()
        sku_s = sku.strip()

        if desc_parts:
            rebuilt = self._clean_bl_description(" ".join(desc_parts))
            if rebuilt and rebuilt != sku_s:
                item["description"] = rebuilt
                desc = rebuilt

        if (desc == sku_s or re.fullmatch(r"(?:\d+-|S-)[A-Za-z0-9-]+", desc)) and not self._is_valid_unit(
            unit
        ):
            if unit:
                item["description"] = self._clean_bl_description(unit)
            item["unit"] = "Stk"

        if not self._is_valid_unit(item.get("unit") or ""):
            q, u, p = self._extract_qty_unit_price(tail_lines, sku, prefer_last=True)
            if q > 0:
                item["qty"] = q
                item["unit"] = u
            if p is not None:
                item["unit_price"] = p

        sku_tail = self._sku_trailing_number(sku)
        cur_q = float(item.get("qty") or 0)
        if sku_tail is not None and abs(cur_q - sku_tail) < 0.001 and tail_lines:
            q, u, p = self._extract_qty_unit_price(tail_lines, sku, prefer_last=True)
            if q > 0 and abs(q - sku_tail) >= 0.001:
                item["qty"] = q
                item["unit"] = u
                if p is not None:
                    item["unit_price"] = p

        return item

    def _invoice_use_vertical_layout(self, lines: List[str]) -> bool:
        """PDF/OCR facture: SKU seul, puis description (pas de jeton barcode)."""
        hits = 0
        for i in range(min(len(lines) - 1, 250)):
            if not self._BL_SKU_ONLY_LINE_RE.match(lines[i]):
                continue
            if self._BARCODE_ONLY_LINE_RE.match(lines[i + 1].strip()):
                continue
            hits += 1
        return hits >= 4

    def _bl_use_vertical_layout(self, lines: List[str]) -> bool:
        """PDF/OCR verzendbon: SKU seul puis barcode sur la ligne suivante."""
        hits = 0
        for i in range(min(len(lines) - 1, 150)):
            if not self._line_starts_with_product_sku(lines[i]):
                continue
            if self._BARCODE_ONLY_LINE_RE.match(lines[i + 1].strip()):
                hits += 1
        return hits >= 4

    def _clean_bl_description(self, text: str) -> str:
        out = re.sub(r"\s+", " ", text).strip()
        out = self._MID_QTY_UNIT_PRICE_RE.sub(" ", out).strip()
        out = self._PAAR_DESC_TAIL_RE.sub("", out).strip()
        out = re.sub(
            r"(?i)\b(nr\.?\s*barcode\s*omschrijving.*|prijs\s+excl\.?|eenheids\s*prijs.*)$",
            "",
            out,
        ).strip()
        return out

    def _parse_bl_vertical_article(self, lines: List[str], idx: int) -> Optional[Dict]:
        sku_m = self._BL_SKU_ONLY_LINE_RE.match(lines[idx])
        if not sku_m:
            loose = self._INVOICE_SKU_LINE_RE.match(lines[idx])
            if not loose or (loose.group(2) or "").strip():
                return None
            sku = loose.group(1).strip()
        else:
            sku = sku_m.group(1).strip()
        if not re.fullmatch(self._INVOICE_SKU, sku, re.IGNORECASE):
            return None

        j = idx + 1
        if j >= len(lines) or not self._BARCODE_ONLY_LINE_RE.match(lines[j].strip()):
            return None
        barcode_token = lines[j].strip()
        j += 1

        desc_parts: List[str] = []
        qty_start = j
        while j < len(lines):
            s = lines[j].strip()
            if self._line_starts_with_product_sku(lines[j]):
                break
            if re.fullmatch(r"VLE\d{5,}", s, re.IGNORECASE):
                break
            if self._bl_vertical_looks_like_order_qty_line(lines, j, sku):
                qty_start = j
                break
            if self._BARCODE_ONLY_LINE_RE.match(s):
                break
            if self._is_noise_line(lines[j], True):
                j += 1
                continue
            desc_parts.append(s)
            j += 1

        article_end = self._bl_vertical_tail_end_index(lines, qty_start)
        tail_lines = lines[qty_start:article_end]
        qty, unit, unit_price = self._extract_qty_unit_price(
            tail_lines, sku, prefer_last=True
        )
        j = article_end

        description = self._clean_bl_description(" ".join(desc_parts))
        if not description:
            description = sku

        barcode_normalized = self._normalize_barcode_token(barcode_token)
        ean = self._decode_barcode_to_ean(barcode_token)
        if self._is_weak_delivery_item(sku, description, desc_parts):
            return None

        item = {
            "qty": qty,
            "unit": unit,
            "sku": sku,
            "description": description,
            "ean": ean,
            "barcode_raw": barcode_token,
            "barcode_normalized": barcode_normalized,
            "unit_price": unit_price,
            "line_total": None,
        }
        item = self._repair_bl_vertical_item(item, desc_parts, sku, tail_lines)

        return {
            "item": item,
            "next_i": j,
        }

    def _parse_invoice_vertical_article(self, lines: List[str], idx: int) -> Optional[Dict]:
        sku_m = self._BL_SKU_ONLY_LINE_RE.match(lines[idx])
        if not sku_m:
            return None
        sku = sku_m.group(1).strip()
        if not re.fullmatch(self._INVOICE_SKU, sku, re.IGNORECASE):
            return None

        j = idx + 1
        desc_parts: List[str] = []
        qty_start = j
        while j < len(lines):
            s = lines[j].strip()
            if self._line_starts_with_product_sku(lines[j]):
                break
            if re.fullmatch(r"VLE\d{5,}", s, re.IGNORECASE):
                break
            if self._bl_vertical_looks_like_order_qty_line(lines, j, sku):
                qty_start = j
                break
            if self._BARCODE_ONLY_LINE_RE.match(s):
                break
            if self._is_noise_line(lines[j], False):
                j += 1
                continue
            desc_parts.append(s)
            j += 1

        article_end = self._bl_vertical_tail_end_index(lines, qty_start, invoice=True)
        tail_lines = lines[qty_start:article_end]
        qty, unit, _ = self._extract_qty_unit_price(
            tail_lines, sku, prefer_last=True
        )
        unit_price, line_total, discount = self._extract_invoice_prices_from_tail(tail_lines, qty)
        unit_price, line_total = self._finalize_invoice_line_amounts(
            unit_price, line_total, qty
        )
        j = article_end

        description = self._clean_invoice_description(" ".join(desc_parts))
        if not description or description.lower() == sku.lower():
            description = sku

        if qty <= 0:
            return None

        return {
            "item": {
                "qty": qty,
                "unit": unit,
                "sku": sku,
                "description": description,
                "ean": None,
                "barcode_raw": None,
                "barcode_normalized": None,
                "discount": discount,
                "unit_price": unit_price,
                "line_total": line_total,
            },
            "next_i": j,
        }

    @staticmethod
    def _finalize_invoice_line_amounts(
        unit_price: Optional[float],
        line_total: Optional[float],
        qty: float,
    ) -> tuple[Optional[float], Optional[float]]:
        """Conserve le regelbedrag PDF ; ne calcule qté×PU qu'en dernier recours."""
        if line_total is not None and line_total >= 0:
            return unit_price, round(float(line_total), 2)
        if unit_price is None or qty <= 0:
            return unit_price, line_total
        return unit_price, round(unit_price * qty, 2)

    def _extract_products_invoice_vertical(self, lines: List[str]) -> List[Dict]:
        items: List[Dict] = []
        stop_words = ("subtotaal", "totaal €", "totaal eur", "gelieve te betalen")
        i = 0
        while i < len(lines):
            line = lines[i]
            if any(sw in line.lower() for sw in stop_words):
                break
            if self._is_noise_line(line, False):
                i += 1
                continue
            if not self._BL_SKU_ONLY_LINE_RE.match(line):
                i += 1
                continue
            parsed = self._parse_invoice_vertical_article(lines, i)
            if parsed:
                items.append(parsed["item"])
                i = parsed["next_i"]
            else:
                i += 1
        return self._finalize_invoice_items(items)

    def _finalize_delivery_items(self, items: List[Dict]) -> List[Dict]:
        cleaned = self._dedupe_identical_items(items)
        cleaned = self._dedupe_delivery_page_repeats(cleaned)
        return self._consolidate_items_by_sku(cleaned)

    def _extract_products_bl_vertical(self, lines: List[str]) -> List[Dict]:
        items: List[Dict] = []
        stop_words = ("subtotaal", "totaal €", "totaal eur", "gelieve te betalen")
        i = 0
        while i < len(lines):
            line = lines[i]
            if any(sw in line.lower() for sw in stop_words):
                break
            if self._is_noise_line(line, True):
                i += 1
                continue
            if not self._line_starts_with_product_sku(line):
                i += 1
                continue
            parsed = self._parse_bl_vertical_article(lines, i)
            if parsed:
                items.append(parsed["item"])
                i = parsed["next_i"]
            else:
                i += 1
        return self._finalize_delivery_items(items)

    def extract_products(self) -> List[Dict]:
        lines = [ln.strip() for ln in self.full_text.splitlines() if ln and ln.strip()]
        lines = self._merge_numeric_sku_prefix_fragments(lines)
        items: List[Dict] = []
        is_delivery = self._is_delivery()
        stop_words = ("subtotaal", "totaal €", "totaal eur", "gelieve te betalen")

        if is_delivery and self._bl_use_vertical_layout(lines):
            return self._extract_products_bl_vertical(lines)

        use_invoice_vertical = (
            not is_delivery and self._invoice_use_vertical_layout(lines)
        )

        i = 0
        while i < len(lines):
            line = lines[i]
            low = line.lower()
            if any(sw in low for sw in stop_words):
                break

            if use_invoice_vertical:
                parsed = self._parse_invoice_vertical_article(lines, i)
                if parsed:
                    items.append(parsed["item"])
                    i = parsed["next_i"]
                    continue

            # Facture OCR: en-tête + tableau sur une seule ligne (raw_text sans retours ligne).
            if not is_delivery:
                embedded = self.INVOICE_LINE_FIND_RE.match(line)
                if embedded:
                    self._append_invoice_row_match(items, embedded)
                    i += 1
                    continue

            if self._is_noise_line(line, is_delivery):
                i += 1
                continue

            # Facture: lignes tableau complètes ou lignes SKU+lignes suite (pas de blocs SKU orphelins).
            if not is_delivery:
                inv = self.INVOICE_LINE_RE.match(line)
                if inv:
                    self._append_invoice_row_match(items, inv)
                    i += 1
                    continue
                promo = self.INVOICE_LINE_PROMO_RE.match(line)
                if promo:
                    self._append_invoice_promo_row_match(items, promo)
                    i += 1
                    continue
                loose = self._try_invoice_loose_row(lines, i)
                if loose:
                    items.append(loose["item"])
                    i = loose["next_i"]
                    continue
                i += 1
                continue

            sku_match = self.SKU_RE.match(line)
            dense_hdr = None if sku_match else self.DENSE_ROW_SKU_RE.match(line)

            if (
                sku_match
                and not dense_hdr
                and not self._standalone_sku_followed_by_barcode_line(lines, i)
            ):
                i += 1
                continue

            if not sku_match and not dense_hdr:
                i += 1
                continue

            sku = (sku_match or dense_hdr).group(1).strip()
            # Avoid matching pure words like DOEKJES; SKU must contain "-" segmenting.
            if "-" not in sku:
                i += 1
                continue

            j = i + 1
            block_lines: List[str] = []
            # Verzendbon: article + barcode token sometimes share the same line.
            if dense_hdr:
                tail = line[dense_hdr.end() :].strip()
                if tail:
                    block_lines.extend(self._split_dense_line_tail(tail))

            while j < len(lines):
                nxt = lines[j]
                if self._starts_new_product_row(lines, j, is_delivery):
                    break
                nxt_low = nxt.lower()
                # Stop continuation before page footer / next table header repeats
                # (otherwise desc picks up totals, BELGI�, BL number, Nr./Barcode headings).
                if self._is_noise_line(nxt.strip(), is_delivery):
                    j += 1
                    continue
                if any(sw in nxt_low for sw in stop_words):
                    break
                block_lines.append(nxt)
                if len(block_lines) >= 28:
                    break
                j += 1

            # BL: lignes KOETSWERK / qty sous le bloc sans nouveau SKU en début de ligne.
            while j < len(lines) and len(block_lines) < 28:
                nxt = lines[j]
                if self._line_starts_with_product_sku(nxt):
                    break
                if any(sw in nxt.lower() for sw in stop_words):
                    break
                if re.fullmatch(r"VLE\d{5,}", nxt.strip(), re.IGNORECASE):
                    break
                if not self._is_bl_continuation_line(nxt) and self._is_noise_line(nxt, True):
                    j += 1
                    continue
                if not self._is_bl_continuation_line(nxt):
                    break
                block_lines.append(nxt)
                j += 1

            qty_source_lines = list(block_lines)
            if dense_hdr:
                row_tail = line[dense_hdr.end() :].strip()
                if row_tail:
                    qty_source_lines = [row_tail] + [
                        b for b in block_lines if b.strip() != row_tail
                    ]
            qty, unit, unit_price = self._extract_qty_unit_price(
                qty_source_lines, sku, prefer_last=True
            )

            line_total = None
            discount = None
            if not is_delivery:
                unit_price, line_total, discount = self._extract_invoice_prices_from_tail(
                    block_lines, qty
                )
            elif unit_price is None:
                money_values = [
                    self._to_float(x.group(0))
                    for x in self.MONEY_RE.finditer(" ".join(block_lines))
                ]
                money_values = [m for m in money_values if m is not None]
                if money_values:
                    unit_price = money_values[-1]

            desc_parts: List[str] = []
            ean_candidates: List[str] = []
            barcode_token: Optional[str] = None
            for part in block_lines:
                if self.DELIVERY_QTY_RE.match(part):
                    continue
                if self.UNIT_ONLY_RE.match(part):
                    continue
                if re.match(r"^\d+(?:[.,]\d+)?$", part):
                    continue
                if re.fullmatch(r"\d+\+\d+", part):
                    continue
                if re.match(r"^[A-Z]\([A-Z0-9*]+\($", part, re.IGNORECASE):
                    if barcode_token is None:
                        barcode_token = part.strip()
                    continue
                if self._is_noise_line(part, is_delivery):
                    continue
                for match in self.EAN_RE.findall(part):
                    ean_candidates.append(match)
                desc_parts.append(part.strip())

            ean = None
            if ean_candidates:
                preferred = [c for c in ean_candidates if len(c) == 13]
                if preferred:
                    ean = preferred[0]
                else:
                    ean = sorted(ean_candidates, key=len, reverse=True)[0]
            barcode_raw = None
            barcode_normalized = None
            translated_ean = None
            if not ean and barcode_token:
                # Some Pardaen BL render barcode value in custom font token form.
                # Keep it separately for traceability without polluting numeric EAN.
                barcode_raw = barcode_token
                barcode_normalized = self._normalize_barcode_token(barcode_token)
                translated_ean = self._decode_barcode_to_ean(barcode_token)
                if translated_ean:
                    ean = translated_ean

            description = self._finalize_item_description(desc_parts, sku, block_lines)
            if description.strip() == sku.strip():
                fb = self._fallback_description_known_barcode(barcode_normalized)
                if fb:
                    description = fb
            description = re.sub(r"\s+", " ", description).strip()
            if qty > 0 and not self._is_weak_delivery_item(sku, description, block_lines):
                if not is_delivery:
                    unit_price, line_total = self._finalize_invoice_line_amounts(
                        unit_price, line_total, qty
                    )
                items.append(
                    {
                        "qty": qty,
                        "unit": unit,
                        "sku": sku,
                        "description": description,
                        "ean": ean,
                        "barcode_raw": barcode_raw,
                        "barcode_normalized": barcode_normalized,
                        "discount": discount,
                        "unit_price": unit_price,
                        "line_total": line_total,
                    }
                )

            i = j

        if not is_delivery:
            return self._finalize_invoice_items(items)
        return self._finalize_delivery_items(items)

    def extract_metadata(self) -> Dict:
        doc_type = "delivery" if self._is_delivery() else "invoice"
        invoice_number = None
        delivery_number = None

        inv = self.INVOICE_NUMBER_RE.search(self.full_text)
        if inv:
            invoice_number = inv.group(1).strip()

        dn = self.DELIVERY_NUMBER_RE.search(self.full_text)
        if dn:
            delivery_number = dn.group(1).strip()

        date = None
        dm = self.DATE_RE.search(self.full_text)
        if dm:
            date = dm.group(1).strip()
        payment_terms = None
        pt = self.PAYMENT_TERMS_RE.search(self.full_text)
        if pt:
            payment_terms = pt.group(1).strip()

        client = None
        # Lightweight heuristic: EURO BRICO appears on both invoice and BL blocks.
        if "euro brico" in self.text_lower:
            client = "EURO BRICO"

        supplier_address = None
        if "haachtsesteenweg" in self.text_lower:
            supplier_address = "Haachtsesteenweg 672 bus 1, 1910 Kampenhout"

        return {
            "doc_type": doc_type,
            "number": delivery_number if doc_type == "delivery" else invoice_number,
            "client": client,
            "supplier": "Pardaen",
            "date": date,
            "supplier_code": None,
            "supplier_address": supplier_address,
            "supplier_phone": "+32 (0)2 251 13 85",
            "supplier_email": "info@pardaen.be",
            "supplier_contact": None,
            "supplier_payment_terms": payment_terms,
        }
