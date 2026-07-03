"""
Parser générique pour les documents non reconnus ou formats inconnus.
Utilise des heuristiques générales pour extraire les données.
"""
import re
from typing import List, Dict
from .base_parser import BaseParser


class GenericParser(BaseParser):
    """Parser générique pour tous les formats"""
    
    EAN_REGEX = re.compile(r"\b\d{13}\b")
    PRICE_REGEX = re.compile(r"\d+[\.,]\d{2}")
    
    def normalize_number(self, s: str) -> str:
        """Normalise un nombre (remplace virgule par point)"""
        return s.replace(',', '.').replace('\u00A0', '').strip()
    
    def find_ean(self, text: str):
        """Trouve un EAN dans le texte"""
        m = self.EAN_REGEX.search(text)
        return m.group(0) if m else None
    
    def extract_products(self) -> List[Dict]:
        """Extrait les produits avec des heuristiques génériques"""
        products = []
        
        for page in self.pdf_raw['pages']:
            lines = [l for l in page['text'].splitlines() if l.strip()]
            
            for line in lines:
                ean = self.find_ean(line)
                # Heuristique : ligne qui commence par un index numérique ou contient un EAN
                if re.match(r"^\s*\d+\s", line) or ean:
                    prices = self.PRICE_REGEX.findall(line)
                    item = {
                        "raw": line,
                        "ean": ean,
                        "product_code": None,
                        "description": None,
                        "quantity": None,
                        "unit": None,
                        "unit_price": None,
                        "total_value": None
                    }
                    
                    parts = line.split()
                    # SKU candidate = deuxième token si la ligne commence par pos
                    if re.match(r"^\s*\d+\s", line) and len(parts) > 1:
                        item['product_code'] = parts[1]
                    
                    # Quantité heuristique
                    qty_search = re.search(r"(\d+[\,\.]?\d*)\s*(ST|PC|KG|PAC|PAX)?", line)
                    if qty_search:
                        item['quantity'] = float(self.normalize_number(qty_search.group(1)))
                        item['unit'] = qty_search.group(2) if qty_search.group(2) else None
                    
                    # Prix heuristique : dernier prix trouvé
                    if prices:
                        item['unit_price'] = float(self.normalize_number(prices[-1]))
                    
                    products.append(item)
        
        return products
    
    def extract_metadata(self) -> Dict:
        """Extrait les métadonnées avec des heuristiques génériques"""
        metadata = {
            "doc_type": self.detect_doc_type(),
            "number": None,
            "client": None,
            "supplier": self.detect_supplier(),
            "date": None
        }
        
        # Chercher numéro de document
        number_patterns = [
            r'(?:Invoice|Facture|Faktura|Factuur)\s*(?:number|numéro|nr\.?|no\.?)\s*:?\s*(\d+)',
            r'Number\s*:?\s*(\d+)',
            r'Numéro\s*:?\s*(\d+)'
        ]
        for pattern in number_patterns:
            match = re.search(pattern, self.full_text, flags=re.IGNORECASE)
            if match:
                metadata["number"] = match.group(1)
                break
        
        # Chercher client
        client_patterns = [
            r'(?:Client|Customer|Klant|Client)\s*:?\s*(.+?)(?=\n\n|\nContact|\nTel:|\nEmail:|$)',
            r'(?:Bill\s+to|Ship\s+to|Delivered\s+to)\s*:?\s*(.+?)(?=\n\n|\nContact|\nTel:|\nEmail:|$)'
        ]
        for pattern in client_patterns:
            match = re.search(pattern, self.full_text, flags=re.IGNORECASE | re.DOTALL)
            if match:
                client = match.group(1).strip()
                client_lines = [l.strip() for l in client.split('\n') if l.strip()]
                # Filtrer les lignes invalides
                client_lines = [l for l in client_lines if len(l) >= 5 and not l.strip().isdigit()]
                if client_lines:
                    metadata["client"] = client_lines[0].strip()
                break
        
        # Chercher date
        date_patterns = [
            r'(?:Date|Datum)\s*:?\s*(\d{1,2}[./-]\d{1,2}[./-]\d{2,4})',
            r'(\d{1,2}[./-]\d{1,2}[./-]\d{2,4})'
        ]
        for pattern in date_patterns:
            match = re.search(pattern, self.full_text, flags=re.IGNORECASE)
            if match:
                metadata["date"] = match.group(1)
                break
        
        return metadata

