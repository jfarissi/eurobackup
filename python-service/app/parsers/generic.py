"""
Parser générique pour les documents non reconnus ou formats inconnus.
Structure alignée avec auto_invoice_parser : fonction parse(pdf_raw).
"""
import re
from typing import Dict, List, Optional

EAN_REGEX = re.compile(r"\b\d{13}\b")
PRICE_REGEX = re.compile(r"\d+[\.,]\d{2}")


def normalize_number(s: str) -> str:
    """Normalise un nombre (remplace virgule par point)"""
    return s.replace(',', '.').replace('\u00A0', '').strip()


def find_ean(text: str):
    """Trouve un EAN dans le texte"""
    m = EAN_REGEX.search(text)
    return m.group(0) if m else None


def simple_line_parser(page_text: str) -> List[Dict]:
    """Parse les lignes d'une page pour extraire les items"""
    items = []
    lines = [l for l in page_text.splitlines() if l.strip()]

    for line in lines:
        ean = find_ean(line)
        # Heuristique : ligne qui commence par un index numérique ou contient un EAN
        if re.match(r"^\s*\d+\s", line) or ean:
            prices = PRICE_REGEX.findall(line)
            item = {
                "sku": None,
                "ean": ean,
                "description": None,
                "qty": None,
                "unit": None,
                "unit_price": None,
                "line_total": None
            }
            
            parts = line.split()
            # SKU candidate = deuxième token si la ligne commence par pos
            if re.match(r"^\s*\d+\s", line) and len(parts) > 1:
                item['sku'] = parts[1]
            
            # Quantité heuristique
            qty_search = re.search(r"(\d+[\,\.]?\d*)\s*(ST|PC|KG|PAC|PAX)?", line)
            if qty_search:
                item['qty'] = float(normalize_number(qty_search.group(1)))
                item['unit'] = qty_search.group(2) if qty_search.group(2) else None
            
            # Prix heuristique : dernier prix trouvé
            if prices:
                item['unit_price'] = float(normalize_number(prices[-1]))
            
            items.append(item)

    return items


def extract_supplier_code_generic(text: str) -> Optional[str]:
    """Extrait le code fournisseur générique si présent."""
    patterns = [
        r"(?:code|supplier\s+code|fournisseur)[\s:]+([A-Z0-9]{3,20})",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            return m.group(1).strip()
    return None


def extract_supplier_address_generic(text: str) -> Optional[str]:
    """Extrait l'adresse complète du fournisseur générique."""
    # Chercher des patterns d'adresse typiques
    patterns = [
        r"([A-Za-z0-9\s,.-]+(?:B-)?\d{4}\s+[A-Za-z\s]+(?:Belgium|Belgique|België)?)",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            address = m.group(1).strip()
            address = re.sub(r"\s+", " ", address)
            if len(address) > 10:  # Filtrer les faux positifs
                return address
    return None


def extract_supplier_phone_generic(text: str) -> Optional[str]:
    """Extrait le numéro de téléphone du fournisseur générique."""
    patterns = [
        r"(?:\+|00\s*32)\s*(\d{1,2}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",
        r"T\s*[:\s]*(\d{2,3}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",
        r"Tel[:\s]*(\d{2,3}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            phone = m.group(1).strip()
            phone = re.sub(r"[\s./]", " ", phone)
            return phone
    return None


def extract_supplier_email_generic(text: str) -> Optional[str]:
    """Extrait l'email du fournisseur générique."""
    patterns = [
        r"([a-z0-9._%+-]+@[a-z0-9.-]+\.(?:be|com|eu|net|org))",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            return m.group(1).lower().strip()
    return None


def extract_supplier_contact_generic(text: str) -> Optional[str]:
    """Extrait le nom du contact commercial générique si présent."""
    patterns = [
        r"(?:contact|sales|commercial)[\s:]+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            contact = m.group(1).strip()
            if len(contact.split()) <= 3 and not any(word.lower() in ['nv', 'bv', 'sprl', 'sa', 'ltd', 'inc'] for word in contact.split()):
                return contact
    return None


def extract_supplier_payment_terms_generic(text: str) -> Optional[str]:
    """Extrait les conditions de paiement génériques."""
    patterns = [
        r"(?:betalingsvoorwaarden|payment\s+terms|conditions\s+de\s+paiement)[\s:]+([^\n]{5,100})",
        r"(?:net|paiement)[\s:]+(\d+\s*(?:jours|days|dagen|net))",
        r"(\d+\s*(?:jours|days|dagen)\s*(?:net|à\s+réception))",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            terms = m.group(1).strip()
            terms = re.sub(r"\s+", " ", terms)
            if len(terms) > 3:
                return terms[:100]
    return None


def parse(pdf_raw: Dict) -> Dict:
    """
    Parse un document générique et retourne les items extraits.
    
    Args:
        pdf_raw: Résultat de extract_pdf_raw()
    
    Returns:
        {
            "items": [...],
            "metadata": {...}
        }
    """
    items = []
    for p in pdf_raw['pages']:
        items += simple_line_parser(p['text'])
    
    # Métadonnées basiques
    full_text = pdf_raw['full_text']
    text_lower = full_text.lower()
    
    metadata = {
        "type": "Factuur",
        "number": None,
        "client": None,
        "supplier": None,
        "date": None,
        "count": len(items),
        "method": "generic_v1",
        "supplier_code": None,
        "supplier_address": None,
        "supplier_phone": None,
        "supplier_email": None,
        "supplier_contact": None,
        "supplier_payment_terms": None
    }
    
    # Détecter le type de document
    if "factuur" in text_lower or "invoice" in text_lower or "facture" in text_lower:
        metadata["type"] = "Factuur"
    elif "leveringsbon" in text_lower or "delivery note" in text_lower or "bon de livraison" in text_lower:
        metadata["type"] = "Leveringsbevestiging"
    
    # Détecter le fournisseur
    if "ff group" in text_lower or "ffgroup" in text_lower or "ff-group" in text_lower:
        metadata["supplier"] = "FF Group"
    elif "knauf" in text_lower:
        metadata["supplier"] = "Knauf"
    elif "stg" in text_lower and "tool" in text_lower:
        metadata["supplier"] = "STG"
    
    # Chercher numéro de document
    number_patterns = [
        r'(?:Invoice|Facture|Faktura|Factuur)\s*(?:number|numéro|nr\.?|no\.?)\s*:?\s*(\d+)',
        r'Number\s*:?\s*(\d+)',
        r'Numéro\s*:?\s*(\d+)'
    ]
    for pattern in number_patterns:
        match = re.search(pattern, full_text, flags=re.IGNORECASE)
        if match:
            metadata["number"] = match.group(1)
            break
    
    # Chercher client
    client_patterns = [
        r'(?:Client|Customer|Klant)\s*:?\s*(.+?)(?=\n\n|\nContact|\nTel:|\nEmail:|$)',
        r'(?:Bill\s+to|Ship\s+to|Delivered\s+to)\s*:?\s*(.+?)(?=\n\n|\nContact|\nTel:|\nEmail:|$)'
    ]
    for pattern in client_patterns:
        match = re.search(pattern, full_text, flags=re.IGNORECASE | re.DOTALL)
        if match:
            client = match.group(1).strip()
            client_lines = [l.strip() for l in client.split('\n') if l.strip()]
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
        match = re.search(pattern, full_text, flags=re.IGNORECASE)
        if match:
            metadata["date"] = match.group(1)
            break
    
    # Extraire les informations supplémentaires du fournisseur
    metadata["supplier_code"] = extract_supplier_code_generic(full_text)
    metadata["supplier_address"] = extract_supplier_address_generic(full_text)
    metadata["supplier_phone"] = extract_supplier_phone_generic(full_text)
    metadata["supplier_email"] = extract_supplier_email_generic(full_text)
    metadata["supplier_contact"] = extract_supplier_contact_generic(full_text)
    metadata["supplier_payment_terms"] = extract_supplier_payment_terms_generic(full_text)
    
    return {
        "items": items,
        "metadata": metadata
    }

