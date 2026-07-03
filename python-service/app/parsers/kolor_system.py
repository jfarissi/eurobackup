import re
import logging
from typing import Dict, List, Tuple, Optional
from .base_parser import BaseParser

class KolorSystemParser(BaseParser):
    """
    Parser pour le fournisseur Kolor System (WAPRO Mag).
    Gère les factures (Faktura) pour l'instant.
    """

    def extract_metadata(self) -> Dict:
        text = self.full_text
        lower_text = text.lower()
        
        # Doc Type
        doc_type = "invoice" # Default
        if "faktura" in lower_text:
            doc_type = "invoice"
        elif "wydanie" in lower_text or "wz" in lower_text:
            doc_type = "delivery_note"

        # Supplier
        supplier = "Kolor System"

        # Number
        # Ex: "Faktura WE nr FE/000415/25"
        number = None
        m_num = re.search(r"Faktura.*?nr\s+([A-Za-z0-9/]+)", text, re.IGNORECASE)
        if m_num:
            number = m_num.group(1)

        # Date
        # Ex: "Data wystawienia 18.12.2025"
        date = None
        m_date = re.search(r"Data\s+wystawienia[\s\n]+(\d{2}\.\d{2}\.\d{4})", text, re.IGNORECASE)
        if m_date:
            date = m_date.group(1)
            
        # Client
        client = None
        if "nabywca" in lower_text:
            if "senko" in lower_text:
                client = "SENKO BV"
            else:
                client = "Unknown Client"

        # Supplier Details extraction
        supplier_address = "ul. K. Ciołkowskiego 171, 15-516 Białystok" # Hardcoded for now as it's standard for this supplier
        
        # NIP as Supplier Code
        supplier_code = None
        m_nip = re.search(r"NIP:\s*(?:PL)?\s*(\d+)", text, re.IGNORECASE)
        if m_nip:
            supplier_code = m_nip.group(1)
            
        # Payment Terms
        payment_terms = None
        m_terms = re.search(r"Termin:\s*(\d+\s*dni)", text, re.IGNORECASE)
        if m_terms:
            payment_terms = m_terms.group(1)

        return {
            "type": doc_type, # Fixed key name
            "number": number,
            "supplier": supplier,
            "client": client,
            "date": date,
            "supplier_address": supplier_address,
            "supplier_code": supplier_code,
            "supplier_payment_terms": payment_terms,
            "supplier_phone": None,
            "supplier_email": None,
            "supplier_contact": None
        }

    def extract_products(self) -> List[Dict]:
        products = []
        
        # 1. Get words and build lines
        all_lines = []
        import fitz
        from ..utils.pdf_extractor import extract_ocr_words
        
        doc = fitz.open(self.pdf_path)
        
        has_digital_text = False
        for page in doc:
            words = page.get_text("words")
            if words:
                has_digital_text = True
                lines = self._build_lines(words)
                all_lines.extend(lines)
        
        # FALLBACK: If no digital text found (scanned), use OCR words to rebuild lines correctly
        if not has_digital_text or not all_lines:
            logging.getLogger(__name__).warning("[KolorSystem] No digital text found. Using OCR with coordinate-based line reconstruction.")
            ocr_pages = extract_ocr_words(self.pdf_path)
            for words in ocr_pages:
                if words:
                    lines = self._build_lines(words)
                    all_lines.extend(lines)
            
        # 2. Parse lines
        current_product = None
        
        # Regex for main line:
        # Index | Desc | (CodeCN?) | Qty | Unit | Price
        # Ex: "17Płyta GKB-WODA 68091100 66 szt. 7,55 ..."
        # Note: Index might be attached to Desc keyword like "17Płyta" if space is small
        # Regex to catch: Start with digits, then text... then digits + unit + price
        
        # Unit regex: szt\.|op|kg|m|kpl\.|st|pac|pc|pak|szt|sz\.
        # Removed szt/pal from here to avoid matching it as the main unit
        unit_re_str = r"(szt\.|op|kg|m|kpl\.|st|pac|pc|pak|pes|pcs|sz\.|szt)"
        
        # Regex for Index at start (Allow optional space: 21Profil -> 21, Profil...)
        index_re = re.compile(r"^(\d+)\s*")
        
        # Regex for Qty + Unit (allow attached, allow spaces)
        # Group 1: Qty
        # Group 2: Unit
        qty_unit_re = re.compile(r"(\d+(?:[.,]\d+)?)\s*" + unit_re_str, re.IGNORECASE)
        
        # Regex for Price (at end of line usually, or after Unit)
        # PRIOR FIX: Anchored to end ($) which captured Total Value instead of Unit Price.
        # NEW FIX: Capture the FIRST number found after the unit.
        price_re = re.compile(r"(\d[\d\s]*[.,]\d+)")

        for line_data in all_lines:
            line_text = line_data["text"]
            logging.getLogger(__name__).warning(f"[DEBUG LINE] {line_text}")
            
            # 1. Match Index
            m_idx = index_re.match(line_text)
            new_product_found = False
            
            if m_idx:
                idx = m_idx.group(1)
                remainder = line_text[m_idx.end():]
                
                # 2. Find all Qty-Unit candidates in remainder
                matches = list(qty_unit_re.finditer(remainder))
                
                valid_match = None
                if matches:
                    valid_match = matches[-1]
                
                if valid_match:
                    new_product_found = True
                    # If we had a previous product, save it
                    if current_product:
                        products.append(current_product)
                    
                    qty_str = valid_match.group(1)
                    unit_str = valid_match.group(2)
                    
                    # Everything before valid_match is Description
                    # Everything after is potentially Price
                    match_start = valid_match.start()
                    match_end = valid_match.end()
                    
                    raw_desc = remainder[:match_start].strip()
                    after_part = remainder[match_end:].strip()
                    
                    # Check Price in after_part
                    unit_price = 0.0
                    m_price = price_re.search(after_part)
                    if m_price:
                        price_str = m_price.group(1)
                        clean_price = price_str.replace(" ", "").replace(",", ".")
                        try:
                            unit_price = float(clean_price)
                        except:
                            unit_price = 0.0
                    
                    clean_qty = qty_str.replace(" ", "").replace(",", ".")
                    
                    current_product = {
                        "sku": f"IDX-{idx}", 
                        "description": raw_desc,
                        "qty": float(clean_qty),
                        "unit_price": unit_price,
                        "unit": unit_str,
                        "ean": None,
                        "line_total": float(clean_qty) * unit_price
                    }

            if not new_product_found and current_product:
                # Append to description or find Code
                # Ignore trivial lines
                # Append to description or find Code
                # Ignore trivial lines
                line_lower = line_text.lower()
                
                if "wydrukowano z programu" in line_lower or "strona" in line_lower:
                    continue
                if "razem" in line_lower or "ogółem" in line_lower:
                    continue
                    
                # Stop words for footer
                footer_words = [
                    "specyfikacja", "data obowiązku", "do zapłaty", 
                    "wartość", "kwota", "eur", "vat", "kursu", "podatku",
                    "słownie", "podpis", "odbioru", "wystawił"
                ]
                
                if any(word in line_lower for word in footer_words):
                    continue
                    
                # Check for Product Code (Symbol)
                # Usually a short code on its own line or at end
                # Ex: "003837" (6 digits), "KER000280"
                if re.match(r"^[A-Z0-9]{3,12}$", line_text.strip()):
                    current_product["sku"] = line_text.strip() # Update SKU with real code
                    current_product["product_code"] = line_text.strip() # Keep for legacy
                else:
                    # Append to description if not too long/garbage
                    if len(line_text) < 200:
                         current_product["description"] += " " + line_text

        # Add last product
        if current_product:
            products.append(current_product)
            
        return products

    def _build_lines(self, words) -> List[Dict]:
        """
        Group words into lines based on Y coordinate.
        Words format: (x0, y0, x1, y1, text, block_no, line_no, word_no)
        """
        # Sort by Y then X
        words = sorted(words, key=lambda w: (w[1], w[0]))
        lines = []
        current_line_words = []
        current_y = -1
        
        TOLERANCE_Y = 10 # Increased to 10 to aggressively merge columns (drift)
        
        for w in words:
            text = w[4]
            y = w[1]
            
            if current_y == -1:
                current_y = y
                current_line_words.append(w)
                continue
                
            if abs(y - current_y) < TOLERANCE_Y:
                current_line_words.append(w)
            else:
                # Flush line
                line_text = " ".join([word[4] for word in sorted(current_line_words, key=lambda x: x[0])])
                lines.append({"text": line_text, "y": current_y})
                
                current_line_words = [w]
                current_y = y
                
        if current_line_words:
            line_text = " ".join([word[4] for word in sorted(current_line_words, key=lambda x: x[0])])
            lines.append({"text": line_text, "y": current_y})
            
        return lines
