"""
Parser spécifique pour FF GROUP TOOL INDUSTRIES SA.

Gère :
  - FACTURES (INVOICE)
  - BONS DE LIVRAISON (DELIVERY NOTE)

Retour :
{
  "items": [
      {
          "sku": "...",
          "ean": null,
          "description": "...",
          "qty": ...,
          "unit": "PC|PACK|SET|PCS|ST",
          "unit_price": null,
          "line_total": null
      },
      ...
  ],
  "metadata": {
      "type": "Invoice" | "Delivery Note",
      "number": "...",
      "date": "dd/mm/yyyy",
      "client": "...",
      "supplier": "FF GROUP TOOL INDUSTRIES SA",
      "count": ...,
      "method": "ffgroup_v5"
  }
}
"""

import re
from typing import Dict, List, Optional


# ------------------------------------------------------------
# HELPERS : construction des lignes à partir de words
# ------------------------------------------------------------

def build_lines(words: List) -> List[List]:
    """
    Regroupe les words PyMuPDF en lignes logiques.
    words : [(x0, y0, x1, y1, text), ...]
    """
    if not words:
        return []

    temp = []
    for w in words:
        try:
            x0, y0, x1, y1, txt = w[0], w[1], w[2], w[3], w[4]
        except Exception:
            continue
        yc = (y0 + y1) / 2.0
        temp.append((x0, yc, txt))

    # tri par Y puis X
    temp.sort(key=lambda t: (t[1], t[0]))

    lines = []
    current_line = []
    current_y = None
    tol = 6.0  # Augmenté à 6.0 pour garantir la fusion des champs Code/Desc/Qty alignés approximativement

    for x, y, txt in temp:
        if current_y is None:
            current_y = y
            current_line = [(x, txt)]
            continue

        if abs(y - current_y) <= tol:
            current_line.append((x, txt))
        else:
            current_line.sort(key=lambda k: k[0])
            lines.append(current_line)
            current_line = [(x, txt)]
            current_y = y

    if current_line:
        current_line.sort(key=lambda k: k[0])
        lines.append(current_line)

    return lines


# ------------------------------------------------------------
# METADATA
# ------------------------------------------------------------

def extract_type(text: str) -> str:
    """
    Détecte le type de document :
      - "Invoice"
      - "Delivery Note"
    """
    lines = text.splitlines()
    upper_lines = [l.upper() for l in lines]

    # 1) Recherche autour de "TYPE OF DOCUMENT"
    for i, line in enumerate(upper_lines):
        if "TYPE OF DOCUMENT" in line:
            # Regarder environ 5 lignes AVANT et APRÈS
            start = max(0, i - 5)
            end = min(len(lines), i + 5)
            context = " ".join(upper_lines[start:end])
            
            if "INVOICE" in context:
                return "Invoice"
            if "DELIVERY NOTE" in context:
                return "Delivery Note"
            break

    # 2) Fallback: Si "INVOICE" apparait isolément dans les premières lignes (Header)
    for i in range(min(20, len(lines))):
        if "INVOICE" in upper_lines[i]:
            return "Invoice"

    # 3) Fallback global (dangereux si le mot delivery note apparait dans une ref)
    lower = text.lower()
    # On priorise Invoice si les deux sont présents (souvent "Delivery Note" est référencé dans une facture)
    if "invoice" in lower:
        return "Invoice"
    if "delivery note" in lower:
        return "Delivery Note"
    
    return "Invoice"


def extract_number(pages: List[Dict], text: str) -> Optional[str]:
    """
    Extraction fiable du numéro FF GROUP.
    Extraction PDF : souvent en-têtes (TYPE, SERIES, NUMBER, DATE, ISSUE TIME) puis valeurs
    sur lignes séparées — ex. ligne « 18495 » suivie de « DELIVERY NOTE » (pas sur la même ligne que NUMBER).
    """
    # 1) Même ligne : NUMBER : 12345 (mise en page horizontale)
    m = re.search(r"(?:NUMBER|NUMERO|NR)\s*[:.]?\s*(\d{4,8})\b", text, re.IGNORECASE)
    if m:
        return m.group(1)

    # 2) BL FF Group : ISSUE TIME → ligne chiffres → DELIVERY NOTE (cf. log / PDF FF)
    m = re.search(
        r"ISSUE\s+TIME\s*[\r\n]+\s*(\d{4,8})\s*[\r\n]+\s*DELIVERY\s+NOTE\b",
        text,
        re.IGNORECASE | re.MULTILINE,
    )
    if m:
        return m.group(1)

    # 3) Facture FF : ISSUE TIME → chiffres → INVOICE
    m = re.search(
        r"ISSUE\s+TIME\s*[\r\n]+\s*(\d{4,8})\s*[\r\n]+\s*INVOICE\b",
        text,
        re.IGNORECASE | re.MULTILINE,
    )
    if m:
        return m.group(1)

    # 4) Ligne uniquement chiffres (4–8) immédiatement au-dessus de DELIVERY NOTE
    m = re.search(r"(?:^|[\r\n])\s*(\d{4,8})\s*[\r\n]+\s*DELIVERY\s+NOTE\b", text, re.IGNORECASE | re.MULTILINE)
    if m:
        return m.group(1)

    # 5) Ancien fallback : 5 chiffres puis INVOICE
    m = re.search(r"(\d{4,8})\s*[\r\n]+\s*INVOICE\b", text, re.IGNORECASE | re.MULTILINE)
    if m:
        return m.group(1)

    return None


def extract_date(text: str) -> Optional[str]:
    """
    Première date au format dd/mm/yyyy ou dd.mm.yyyy
    """
    m = re.search(r"\b(\d{2}[./]\d{2}[./]\d{4})\b", text)
    return m.group(1) if m else None


def extract_client(text: str) -> Optional[str]:
    """
    Extraction du client (simple fallback).
    """
    # Pattern pour capturer après "SPRL" ou "BV"
    m = re.search(r"(SPRL\s+[A-Z0-9\s]+)", text, re.IGNORECASE)
    if m:
        return m.group(1).strip()
    return None


# Regex pour facture avec prix (Code Desc Unit Qty Gross [Discount...] Net Total)
# Modification: regex robuste qui cherche les 2 derniers montants (Net et Total)
# Structure: Code Desc Unit Qty Gross ... Net Total
PRODUCT_LINE_WITH_PRICES_RE = re.compile(
    r"^(\d{4,5})\s+(.+)\s+(PC|PACK|SET|PCS|ST)\s+(\d+)\s+(\d+[.,]\d+)\s+(?:.*\s+)?(\d+[.,]\d+)\s+(\d+[.,]\d+)",
    re.IGNORECASE
)

# Regex BL (sans prix)
PRODUCT_LINE_RE = re.compile(
    r"^(\d{4,5})\s+(.+)\s+(PC|PACK|SET|PCS|ST)\s+(\d+)\b",
    re.IGNORECASE
)

IGNORED_LINE_KEYWORDS = [
    "CODE DESCRIPTION UNIT QUANTITY",  # en-tête tableau
    "PRICE NET UNIT NET",              # en-tête prix
    "TOTAL AMOUNT",
    "GROSS AMOUNT",
    "NET AMOUNT",
    "DISCOUNT",
    "CLIENT DETAILS",
    "DELIVERY ADDRESS DETAILS",
    "TRANSPORTER DETAILS",
    "BANK",
    "ISSUED RECEIPT",
    "Page :",
    "Brought Forward",
    "Carried Forward",
    "Totals ",
    "TOTALS ",
    "LOAD:",
]


def is_header_or_footer(line: str) -> bool:
    u = line.upper()
    return any(k.upper() in u for k in IGNORED_LINE_KEYWORDS)


def parse_items(pages: List[Dict]) -> List[Dict]:
    """
    Parse les lignes produits FF GROUP (facture ET BL).
    """
    items: List[Dict] = []

    # Construire toutes les lignes de toutes les pages
    all_lines: List[str] = []
    for page in pages:
        words = page.get("words", [])
        if not words:
            continue
        lines = build_lines(words)
        text_lines = [" ".join(t for _, t in line).strip() for line in lines]
        all_lines.extend(text_lines)

    for line in all_lines:
        line = line.strip()
        if not line:
            continue

        # Filtrer en-têtes / pieds
        if is_header_or_footer(line):
            continue

        # DEBUG: Afficher la ligne testée si elle contient un code produit connu (pour eviter trop de bruit)
        # On peut aussi afficher tout ce qui commence par un chiffre
        if re.match(r"^\d{4,5}", line):
             print(f"[FFGROUP DEBUG] Testing Line: '{line}'")

        # Doit commencer par un code produit 4-5 chiffres
        if not re.match(r"^\d{4,5}\s+", line):
            continue

        # 1) Essayer d'abord la regex avec PRIX (Facture)
        # Ex: "77027 TWIST ... PC 15 2,70 45% 1,49 22,28"
        m_price = PRODUCT_LINE_WITH_PRICES_RE.match(line)
        if m_price:
            print(f"[FFGROUP DEBUG] MATCH WITH PRICES: {m_price.groups()}")  # DEBUG
            code = m_price.group(1)
            desc = m_price.group(2).strip()
            unit = m_price.group(3).upper()
            qty_str = m_price.group(4)
            # group 5 = gross price (ignored)
            net_price_str = m_price.group(6)
            total_str = m_price.group(7)
            # ...
# ... (rest of the file)


            try:
                qty = float(qty_str.replace(",", "."))
                unit_price = float(net_price_str.replace(",", "."))
                line_total = float(total_str.replace(",", "."))
            except ValueError:
                pass # Fallback au pattern simple si erreur de conversion

            desc = " ".join(desc.split())
            items.append({
                "sku": code,
                "ean": None,
                "description": desc,
                "qty": qty,
                "unit": unit,
                "unit_price": unit_price,
                "line_total": line_total
            })
            continue

        # 2) Fallback sur regex simple (BL ou échec prix)
        m = PRODUCT_LINE_RE.match(line)
        if m:
            code = m.group(1)
            desc = m.group(2).strip()
            unit = m.group(3).upper()
            qty_str = m.group(4)

            try:
                qty = float(qty_str.replace(",", "."))
            except ValueError:
                continue

            desc = " ".join(desc.split())
            if len(desc) < 3:
                continue

            items.append({
                "sku": code,
                "ean": None,
                "description": desc,
                "qty": qty,
                "unit": unit,
                "unit_price": None,
                "line_total": None
            })

    return items


# ------------------------------------------------------------
# PARSE PRINCIPAL
# ------------------------------------------------------------

def extract_supplier_code_ffgroup(text: str) -> Optional[str]:
    """Extrait le code fournisseur FFGroup si présent."""
    # Limiter la recherche aux 2000 premiers caractères (en-tête)
    header_text = text[:2000]
    
    # Mots-clés à exclure (faux positifs)
    exclude_keywords = ['NAME', 'CODE', 'DESCRIPTION', 'UNIT', 'QUANTITY', 'PRICE', 'TOTAL', 'AMOUNT']
    
    patterns = [
        r"(?:supplier\s+code|code\s+fournisseur)[\s:]+([A-Z0-9]{3,20})",
        r"FF\s*GROUP[\s-]+CODE[\s:]+([A-Z0-9]{3,10})",
    ]
    for pattern in patterns:
        m = re.search(pattern, header_text, re.IGNORECASE)
        if m:
            code = m.group(1).strip()
            # Filtrer les faux positifs
            if code.upper() not in exclude_keywords:
                return code
    return None


def extract_supplier_address_ffgroup(text: str) -> Optional[str]:
    """Extrait l'adresse complète du fournisseur FFGroup."""
    # L'adresse apparaît au tout début du document
    # Format: "9 km Paradromos ATTIKI ODOS (exit 4),\n19300 ASPROPYRGOS ATTICA, GREECE"
    header_text = text[:500]
    
    # Pattern pour capturer l'adresse complète sur 2 lignes
    # Ligne 1: "9 km Paradromos ATTIKI ODOS (exit 4),"
    # Ligne 2: "19300 ASPROPYRGOS ATTICA, GREECE"
    pattern = r"^([A-Za-z0-9\s,.-]+(?:km|exit\s+\d+)[^,\n]*(?:,\s*)?)\s*\n\s*(\d{4,5}\s+[A-Za-z\s,]+(?:GREECE|BELGIUM|BELGIË|FRANCE|GERMANY|NETHERLANDS|NEDERLAND))"
    m = re.search(pattern, header_text, re.IGNORECASE | re.MULTILINE)
    if m:
        line1 = m.group(1).strip()
        line2 = m.group(2).strip()
        address = f"{line1} {line2}"
        address = re.sub(r"\s+", " ", address)
        
        # Mots-clés à exclure (faux positifs)
        exclude_keywords = ['PAYMENT', 'DUE', 'DAYS', 'BANK', 'PROFORMA', 'RELATED', 'DOCUMENTS', 'CLIENT', 'DETAILS', 'DELIVERY', 'ADDRESS', 'CUSTOMER', 'VAT', 'EORI', 'REGISTRATION', 'NUMBER']
        
        # Filtrer les faux positifs
        if not any(keyword in address.upper() for keyword in exclude_keywords):
            # Vérifier qu'il y a un code postal
            if re.search(r'\d{4,5}', address):
                # Limiter la longueur
                if len(address) <= 200:
                    return address
    
    # Fallback : chercher juste les 2 premières lignes qui contiennent l'adresse
    lines = header_text.split('\n')[:3]
    if len(lines) >= 2:
        line1 = lines[0].strip()
        line2 = lines[1].strip()
        # Vérifier que la ligne 2 contient un code postal
        if re.search(r'\d{4,5}', line2) and any(country in line2.upper() for country in ['GREECE', 'BELGIUM', 'BELGIË', 'FRANCE', 'GERMANY', 'NETHERLANDS', 'NEDERLAND']):
            address = f"{line1} {line2}"
            address = re.sub(r"\s+", " ", address)
            exclude_keywords = ['PAYMENT', 'DUE', 'DAYS', 'BANK', 'PROFORMA', 'RELATED', 'DOCUMENTS', 'CLIENT', 'DETAILS', 'DELIVERY', 'ADDRESS', 'CUSTOMER', 'VAT', 'EORI', 'REGISTRATION', 'NUMBER']
            if not any(keyword in address.upper() for keyword in exclude_keywords) and len(address) <= 200:
                return address
    
    return None


def extract_supplier_phone_ffgroup(text: str) -> Optional[str]:
    """Extrait le numéro de téléphone du fournisseur FFGroup."""
    # Limiter la recherche aux 500 premiers caractères (en-tête)
    header_text = text[:500]
    
    # Pattern spécifique pour FFGroup : "Tel.:+30 211 850 9500"
    patterns = [
        r"Tel\.?\s*:\s*(\+?\d{1,3}[\s./]?\d{1,3}[\s./]?\d{1,3}[\s./]?\d{1,3}[\s./]?\d{1,4})",
        r"(?:\+|00\s*30)\s*(\d{1,3}[\s./]?\d{1,3}[\s./]?\d{1,3}[\s./]?\d{1,4})",
        r"(?:\+|00\s*32)\s*(\d{1,2}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",
        r"T\s*[:\s]*(\d{2,3}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",
    ]
    for pattern in patterns:
        m = re.search(pattern, header_text, re.IGNORECASE)
        if m:
            phone = m.group(1).strip()
            # Nettoyer et formater
            phone = re.sub(r"[\s./]", " ", phone)
            # Vérifier que c'est un numéro valide (au moins 8 chiffres)
            digits_only = re.sub(r'\D', '', phone)
            if len(digits_only) >= 8:
                return phone
    return None


def extract_supplier_email_ffgroup(text: str) -> Optional[str]:
    """Extrait l'email du fournisseur FFGroup."""
    # Limiter la recherche aux 3000 premiers caractères (en-tête)
    header_text = text[:3000]
    
    patterns = [
        r"([a-z0-9._%+-]+@(?:ffgroup|ff-group)[a-z0-9.-]*\.[a-z]{2,})",
        r"([a-z0-9._%+-]+@[a-z0-9.-]+\.(?:be|com|eu))",
    ]
    for pattern in patterns:
        m = re.search(pattern, header_text, re.IGNORECASE)
        if m:
            email = m.group(1).lower().strip()
            # Vérifier que c'est un email valide (contient @ et un point)
            if '@' in email and '.' in email.split('@')[1]:
                return email
    return None


def extract_supplier_contact_ffgroup(text: str) -> Optional[str]:
    """Extrait le nom du contact commercial FFGroup si présent."""
    # Limiter la recherche aux 2000 premiers caractères (en-tête)
    header_text = text[:2000]
    
    # Mots-clés à exclure (faux positifs)
    exclude_keywords = ['PAYMENT', 'DUE', 'DAYS', 'BANK', 'PROFORMA', 'RELATED', 'DOCUMENTS', 'CLIENT', 'DETAILS', 'DELIVERY', 'ADDRESS']
    
    patterns = [
        r"(?:contact\s+person|sales\s+contact|commercial\s+contact)[\s:]+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",
        r"([A-Z][a-z]+\s+[A-Z][a-z]+)(?:\s+-\s*(?:sales|commercial|contact))",
    ]
    for pattern in patterns:
        m = re.search(pattern, header_text, re.IGNORECASE)
        if m:
            contact = m.group(1).strip()
            # Filtrer les faux positifs
            if (len(contact.split()) <= 3 and 
                not any(word.lower() in ['nv', 'bv', 'sprl', 'sa', 'ff', 'group', 'tool', 'industries'] for word in contact.split()) and
                not any(keyword in contact.upper() for keyword in exclude_keywords)):
                return contact
    return None


def extract_supplier_payment_terms_ffgroup(text: str) -> Optional[str]:
    """Extrait les conditions de paiement FFGroup."""
    # Chercher dans l'en-tête ET le footer
    header_text = text[:1000]  # En-tête
    footer_text = text[-2000:] if len(text) > 2000 else text  # Footer
    
    # Pattern spécifique pour FFGroup : "PAYMENT DUE 60D AFTER EOM TTRANSFER"
    patterns = [
        r"PAYMENT\s+DUE\s+(\d+[Dd]\s*(?:AFTER\s+)?(?:EOM|END\s+OF\s+MONTH|INVOICE|DELIVERY)?\s*[A-Z\s]*)",
        r"PAYMENT\s+TERMS?\s*:?\s*(\d+\s*(?:days|jours|dagen|D)\s*(?:after|net|à\s+réception)?\s*[A-Z\s]*)",
        r"(?:payment\s+terms|conditions\s+de\s+paiement|betalingsvoorwaarden)[\s:]+([^\n]{5,100})",
        r"(\d+\s*(?:days|jours|dagen|D)\s*(?:net|after\s+invoice|after\s+delivery|AFTER\s+EOM))",
    ]
    
    # Mots-clés à exclure (faux positifs)
    exclude_keywords = ['RELATED', 'DOCUMENTS', 'CLIENT', 'DETAILS', 'DELIVERY', 'ADDRESS', 'BANK', 'ACCOUNT', 'CUSTOMER']
    
    # Chercher d'abord dans l'en-tête
    for pattern in patterns:
        m = re.search(pattern, header_text, re.IGNORECASE)
        if m:
            terms = m.group(1).strip()
            terms = re.sub(r"\s+", " ", terms)
            # Filtrer les faux positifs
            if len(terms) > 3 and not any(keyword in terms.upper() for keyword in exclude_keywords):
                # Vérifier qu'il contient des informations de paiement
                if any(word in terms.upper() for word in ['DAY', 'NET', 'PAYMENT', 'DUE', 'JOUR', 'RECEPTION', 'AFTER', 'EOM', 'D']):
                    return terms[:100]
    
    # Chercher dans le footer si pas trouvé dans l'en-tête
    for pattern in patterns:
        m = re.search(pattern, footer_text, re.IGNORECASE)
        if m:
            terms = m.group(1).strip()
            terms = re.sub(r"\s+", " ", terms)
            if len(terms) > 3 and not any(keyword in terms.upper() for keyword in exclude_keywords):
                if any(word in terms.upper() for word in ['DAY', 'NET', 'PAYMENT', 'DUE', 'JOUR', 'RECEPTION', 'AFTER', 'EOM', 'D']):
                    return terms[:100]
    return None


def parse(pdf_raw: Dict) -> Dict:
    pages = pdf_raw.get("pages", [])
    text = pdf_raw.get("full_text", "") or pdf_raw.get("text", "")

    # LOG: Afficher le texte brut pour debug
    print("=" * 80)
    print("[FFGROUP PARSER] TEXTE BRUT DU PDF (premiers 3000 caractères):")
    print("=" * 80)
    print(text[:3000])
    print("=" * 80)
    print("[FFGROUP PARSER] TEXTE BRUT DU PDF (derniers 2000 caractères - footer):")
    print("=" * 80)
    print(text[-2000:] if len(text) > 2000 else text)
    print("=" * 80)

    doc_type = extract_type(text)
    number = extract_number(pages, text)
    date = extract_date(text)
    client = extract_client(text)
    supplier = "FF GROUP TOOL INDUSTRIES SA"

    items = parse_items(pages)

    metadata = {
        "type": doc_type,
        "number": number,
        "date": date,
        "client": client,
        "supplier": supplier,
        "count": len(items),
        "method": "ffgroup_v5",
        "supplier_code": extract_supplier_code_ffgroup(text),
        "supplier_address": extract_supplier_address_ffgroup(text),
        "supplier_phone": extract_supplier_phone_ffgroup(text),
        "supplier_email": extract_supplier_email_ffgroup(text),
        "supplier_contact": extract_supplier_contact_ffgroup(text),
        "supplier_payment_terms": extract_supplier_payment_terms_ffgroup(text)
    }

    return {
        "items": items,
        "metadata": metadata
    }
