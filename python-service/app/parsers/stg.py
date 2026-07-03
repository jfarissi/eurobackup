"""
Parser STG (Schrauwen Sanitair & Verwarming NV)
Compatible :
 - Verzendnota (BL)
 - Factuur (Facture)

Fonction exposee :
    parse(pdf_raw: Dict) -> Dict
"""

import re
from typing import Dict, List, Optional


# -----------------------------------------------------------------------------
# REGEX PRODUITS
# -----------------------------------------------------------------------------

# BL standard : 2 quantités
PRODUCT_RE = re.compile(
    r"^\s*(\d{8})\s+(\d{9})\s+(.+?)\s+(\d+[.,]?\d*)\s+(\d+[.,]?\d*)\s+(ST|KG|PC|PAC|PAK)\s*$",
    re.IGNORECASE
)

# FACTURE : 1 quantité + prix brut + prix net + total
# Structure typique: SKU SupplierSKU Description Qty Unit PrixBrut PrixNet Total
PRODUCT_INVOICE_PRICE_RE = re.compile(
    r"""
    ^\s*(\d{8})\s+              # SKU
    (\d{9})\s+                  # Supplier SKU
    (.+?)\s+                    # Description
    (\d+[.,]?\d*)\s+            # Quantity
    (ST|KG|PC|PAC|PAK)\s+       # Unit
    (\d+[.,]\d+)\s*€?\s+        # Prix brut (on l'ignore)
    (\d+[.,]\d+)\s*€?\s+        # Prix net (on prend celui-ci)
    (\d+[.,]\d+)\s*€?           # Line total
    """,
    re.IGNORECASE | re.VERBOSE
)

# FACTURE flexible : 1 quantité seulement
PRODUCT_INVOICE_RE = re.compile(
    r"^\s*(\d{8})\s+(\d{9})\s+(.+?)\s+(\d+[.,]?\d*)\s+(ST|KG|PC|PAC|PAK)",
    re.IGNORECASE
)


# -----------------------------------------------------------------------------
# UTILS
# -----------------------------------------------------------------------------

def clean_desc(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip()


def build_lines(words: List) -> List[List]:
    """Reconstruction lignes PyMuPDF."""
    if not words:
        return []

    temp = []
    for w in words:
        try:
            x0, y0, x1, y1, txt = w[0], w[1], w[2], w[3], w[4]
        except:
            continue
        yc = (y0 + y1) / 2
        temp.append((x0, yc, txt))

    temp.sort(key=lambda t: (t[1], t[0]))

    lines, cur, cur_y = [], [], None
    tol = 2.5

    for x, y, txt in temp:
        if cur_y is None:
            cur_y = y
            cur = [(x, txt)]
            continue

        if abs(y - cur_y) <= tol:
            cur.append((x, txt))
        else:
            cur.sort(key=lambda k: k[0])
            lines.append(cur)
            cur = [(x, txt)]
            cur_y = y

    if cur:
        cur.sort(key=lambda k: k[0])
        lines.append(cur)

    return lines


# -----------------------------------------------------------------------------
# EXTRACT METADATA
# -----------------------------------------------------------------------------

def extract_client_stg(text: str) -> Optional[str]:
    """
    Extraction ultra robuste du client STG :
    Gère :
      ✓ 'Klant : 99998 - Euro Brico'
      ✓ 'Klantnummer : 99998' + ligne suivante
      ✓ 'Klant 99998 Euro Brico'
      ✓ fallback : premier nom entreprise hors adresse fournisseur
    """

    # -------------------------------------------------------------
    # 1) Format classique : "Klant : 99998 - Euro Brico"
    # -------------------------------------------------------------
    m = re.search(r"Klant\s*:\s*(\d+)\s*-\s*([^\n(]+)", text, re.IGNORECASE)
    if m:
        return f"{m.group(1).strip()} {m.group(2).strip()}"

    # -------------------------------------------------------------
    # 2) Format compact : "Klant 99998 Euro Brico"
    # -------------------------------------------------------------
    m = re.search(r"Klant\s+(\d{5,8})\s+([A-Za-z].+)", text, re.IGNORECASE)
    if m:
        num = m.group(1).strip()
        name = m.group(2).split("\n")[0].strip()
        name = re.split(r"Factuur|Verzendnota|Pagina", name)[0].strip()
        return f"{num} {name}"

    # -------------------------------------------------------------
    # 3) Format 2 lignes :
    #    Klantnummer : 99998
    #    Euro Brico SPRL (ou ligne suivante)
    # -------------------------------------------------------------
    lines = [l.strip() for l in text.splitlines() if l.strip()]

    for i, line in enumerate(lines):
        if "klantnummer" in line.lower() or "klant nummer" in line.lower():
            # Cherche numéro
            num = re.search(r"(\d{5,8})", line)
            if num:
                num = num.group(1)
                # ligne suivante = nom
                if i+1 < len(lines):
                    name = lines[i+1]
                    name = re.split(r"Factuur|Verzendnota|Pagina", name)[0].strip()
                    name = re.split(r"\d{4}\s+[A-Z]", name)[0].strip()
                    if len(name) > 2:
                        return f"{num} {name}"

    # -------------------------------------------------------------
    # 4) Fallback : chercher un nom d'entreprise connu (le plus courant)
    # -------------------------------------------------------------
    # STG = souvent "Euro Brico" / "Brico" / "Hubo" / "Bouw"
    m = re.search(r"(Euro\s+Brico[^\n]*)", text, re.IGNORECASE)
    if m:
        name = re.split(r"Factuur|Verzendnota|Pagina", m.group(1))[0].strip()
        name = re.split(r"\d{4}\s+[A-Z]", name)[0].strip()
        return name

    # -------------------------------------------------------------
    # 5) Fallback de dernier recours (mais filtrer les faux positifs)
    # -------------------------------------------------------------
    m = re.search(r"Klant\s*:\s*([^\n]+)", text, re.IGNORECASE)
    if m:
        client_candidate = m.group(1).strip()
        # Filtrer les faux positifs courants
        bad_keywords = ["nummer", "numéro", "number", "tel", "telefoon", "telephone", 
                       "email", "e-mail", "fax", "adres", "address", "behandeld", "door"]
        client_lower = client_candidate.lower()
        if any(kw in client_lower for kw in bad_keywords):
            return None
        # Vérifier que ce n'est pas juste un mot court sans sens
        if len(client_candidate) < 5 or not re.search(r'[A-Za-z]{3,}', client_candidate):
            return None
        return client_candidate

    return None



def extract_number_stg(text: str) -> Optional[str]:
    """Ex: '302950206-1' -> '302950206'"""
    m = re.search(r"(\d{9})[-\\]\d+", text)
    if m:
        return m.group(1)
    m = re.search(r"(\d{9})", text)
    return m.group(1) if m else None


def extract_date_stg(text: str) -> Optional[str]:
    m = re.search(r"(\d{2}\.\d{2}\.\d{4})", text)
    return m.group(1) if m else None


def extract_supplier_stg(text: str) -> Optional[str]:
    """
    Extrait uniquement le nom de l'entreprise STG, sans l'adresse.
    Ex: "Schrauwen Sanitair & Verwarming NV | Atealaan 34B | ..." 
        -> "Schrauwen Sanitair & Verwarming NV"
    """
    # Chercher le nom complet de l'entreprise STG
    # Patterns possibles :
    # 1. "Schrauwen Sanitair & Verwarming NV"
    # 2. "SCHRAUWEN SANITAIR & VERWARMING NV"
    # 3. "Schrauwen" suivi de texte jusqu'au premier "|" ou pattern d'adresse
    
    # Pattern 1 : Nom complet avec "Sanitair & Verwarming"
    m = re.search(r"(Schrauwen\s+Sanitair\s+&\s+Verwarming\s+NV)", text, re.IGNORECASE)
    if m:
        return m.group(1).strip()
    
    # Pattern 2 : "Schrauwen" suivi de texte jusqu'au premier "|" ou pattern d'adresse
    m = re.search(r"(Schrauwen[^|\n]+)", text, re.IGNORECASE)
    if m:
        supplier_name = m.group(0).strip()
        # Nettoyer : enlever les caractères de fin de ligne et espaces multiples
        supplier_name = re.sub(r"\s+", " ", supplier_name).strip()
        # S'assurer qu'on ne prend que le nom (avant le premier "|" si présent)
        if "|" in supplier_name:
            supplier_name = supplier_name.split("|")[0].strip()
        
        # Filtrer les adresses (codes postaux, numéros de téléphone, emails, URLs)
        # Si on trouve un pattern d'adresse, on s'arrête avant
        patterns_to_stop = [
            r"\s+B-\d{4}",  # Code postal belge
            r"\s+T\s*\d{2,3}",  # Téléphone
            r"\s+\d{2,3}\s+\d{2}\s+\d{2}\s+\d{2}",  # Format téléphone
            r"@",  # Email
            r"\.be\b",  # URL .be
            r"\.com\b",  # URL .com
            r"http",  # URL
            r"Atealaan",  # Nom de rue
            r"Herentals",  # Ville
        ]
        for pattern in patterns_to_stop:
            match = re.search(pattern, supplier_name, re.IGNORECASE)
            if match:
                supplier_name = supplier_name[:match.start()].strip()
                break
        
        # Vérifier que le nom a une longueur raisonnable (max 100 caractères)
        if len(supplier_name) > 100:
            # Prendre seulement les premiers mots jusqu'à "NV" ou "BV" ou "SPRL" etc.
            m_company = re.search(r"(.+?(?:NV|BV|SPRL|SA|SRL|LTD|INC))", supplier_name, re.IGNORECASE)
            if m_company:
                supplier_name = m_company.group(1).strip()
            else:
                # Sinon, prendre les 50 premiers caractères
                supplier_name = supplier_name[:50].strip()
        
        # Si on a juste "Schrauwen NV" ou similaire, essayer de trouver le nom complet
        if supplier_name and len(supplier_name) < 20:
            # Chercher dans le texte le nom complet
            m_full = re.search(r"(Schrauwen\s+[^|\n]*?(?:Sanitair|Verwarming)[^|\n]*?NV)", text, re.IGNORECASE)
            if m_full:
                full_name = m_full.group(1).strip()
                # Nettoyer
                for pattern in patterns_to_stop:
                    match = re.search(pattern, full_name, re.IGNORECASE)
                    if match:
                        full_name = full_name[:match.start()].strip()
                        break
                if len(full_name) > len(supplier_name):
                    supplier_name = full_name
        
        # Normaliser les variantes courtes vers le nom complet
        if supplier_name:
            supplier_lower = supplier_name.lower().strip()
            # Si on a juste "Schrauwen NV" ou "SCHRAUWEN NV", utiliser le nom complet
            if supplier_lower in ["schrauwen nv", "schrauwen", "schrauwen sanitair nv"]:
                return "Schrauwen Sanitair & Verwarming NV"
            # Si le nom contient "Sanitair" ou "Verwarming", c'est probablement le nom complet
            if "sanitair" in supplier_lower or "verwarming" in supplier_lower:
                # S'assurer qu'on a le nom complet
                if "sanitair" in supplier_lower and "verwarming" in supplier_lower:
                    # Normaliser la casse et les espaces
                    normalized = re.sub(r"\s+", " ", supplier_name).strip()
                    # S'assurer qu'on a "NV" à la fin
                    if not normalized.upper().endswith("NV"):
                        normalized += " NV"
                    return normalized
                else:
                    # Si on a partiellement le nom, utiliser le nom complet
                    return "Schrauwen Sanitair & Verwarming NV"
        
        if supplier_name and len(supplier_name) > 3:
            return supplier_name
    
    # Fallback : nom par défaut
    return "Schrauwen Sanitair & Verwarming NV"


def extract_supplier_code_stg(text: str) -> Optional[str]:
    """Extrait le code fournisseur STG si présent."""
    # Limiter la recherche aux 2000 premiers caractères (en-tête)
    header_text = text[:2000]
    
    # Mots-clés à exclure (faux positifs)
    exclude_keywords = ['NAME', 'CODE', 'DESCRIPTION', 'UNIT', 'QUANTITY', 'PRICE', 'TOTAL', 'AMOUNT']
    
    # Chercher des patterns comme "Code: XXX" ou "Supplier Code: XXX" près du nom STG
    patterns = [
        r"(?:supplier\s+code|code\s+fournisseur)[\s:]+([A-Z0-9]{3,20})",
        r"STG[\s-]+CODE[\s:]+([A-Z0-9]{3,10})",
    ]
    for pattern in patterns:
        m = re.search(pattern, header_text, re.IGNORECASE)
        if m:
            code = m.group(1).strip()
            # Filtrer les faux positifs
            if code.upper() not in exclude_keywords:
                return code
    return None


def extract_supplier_address_stg(text: str) -> Optional[str]:
    """Extrait l'adresse complète du fournisseur STG."""
    # Limiter la recherche aux 2000 premiers caractères (en-tête)
    header_text = text[:2000]
    
    # DEBUG: Afficher le texte recherché
    print(f"[STG ADDRESS] Recherche dans les {len(header_text)} premiers caractères")
    print(f"[STG ADDRESS] Texte recherché: {header_text[:500]}")
    
    # Pattern spécifique pour STG : "Schrauwen Sanitair & Verwarming NV |  Atealaan 34B  |  B-2200 Herentals  |  T 014 24 40 20  |  stg-group.be  |  info@stg-group.be"
    # L'adresse est entre des "|" (pipes) : deuxième et troisième segments
    # Format: "Schrauwen ... |  Atealaan 34B  |  B-2200 Herentals  |  ..."
    
    # Pattern principal : capturer "Atealaan 34B" et "B-2200 Herentals" séparément
    # Format exact du log ligne 14: "Schrauwen Sanitair & Verwarming NV |  Atealaan 34B  |  B-2200 Herentals  |  T 014 24 40 20"
    # Le problème : il y a plusieurs pipes, il faut capturer entre le 1er et 2ème pipe (Atealaan), puis entre le 2ème et 3ème (code postal)
    
    # Pattern 1 : Chercher directement "Atealaan" suivi d'un pipe, puis code postal
    # Format: "... |  Atealaan 34B  |  B-2200 Herentals  |  ..."
    # Utiliser [\dA-Za-z]+ pour capturer "34B" (avec B minuscule)
    pattern1 = r"Atealaan\s+([\dA-Za-z]+)\s+\|\s+(B-\d{4}\s+[A-Za-z]+)"
    m = re.search(pattern1, header_text, re.IGNORECASE)
    if m:
        street_num = m.group(1).strip()
        city = m.group(2).strip()
        address = f"Atealaan {street_num}, {city}"
        address = re.sub(r"\s+", " ", address)
        print(f"[STG ADDRESS] Pattern 1 trouvé: '{address}'")
        
        # Vérifier qu'il y a un code postal
        if re.search(r'(?:B-)?\d{4}', address):
            if len(address) <= 200:
                print(f"[STG ADDRESS] Adresse extraite: {address}")
                return address
    
    # Pattern 1b : Plus flexible pour le code postal (sans B-)
    pattern1b = r"Atealaan\s+([\dA-Za-z]+)\s+\|\s+(\d{4}\s+[A-Za-z]+)"
    m = re.search(pattern1b, header_text, re.IGNORECASE)
    if m:
        street_num = m.group(1).strip()
        city = m.group(2).strip()
        address = f"Atealaan {street_num}, {city}"
        address = re.sub(r"\s+", " ", address)
        print(f"[STG ADDRESS] Pattern 1b trouvé: '{address}'")
        
        if re.search(r'\d{4}', address):
            if len(address) <= 200:
                print(f"[STG ADDRESS] Adresse extraite: {address}")
                return address
    
    # Pattern 2 : Chercher "Schrauwen ... |  Atealaan ... |  B-2200 ..."
    # Plus flexible avec les espaces
    pattern2 = r"Schrauwen[^|]*\|\s*Atealaan\s+([\dA-Z]+)\s*\|\s*([B-]?\d{4}\s+[A-Za-z\s]+)"
    m = re.search(pattern2, header_text, re.IGNORECASE)
    if m:
        street_num = m.group(1).strip()
        city = m.group(2).strip()
        address = f"Atealaan {street_num}, {city}"
        address = re.sub(r"\s+", " ", address)
        print(f"[STG ADDRESS] Pattern 2 trouvé: '{address}'")
        
        if re.search(r'(?:B-)?\d{4}', address):
            if len(address) <= 200:
                print(f"[STG ADDRESS] Adresse extraite: {address}")
                return address
    
    # Pattern 3 : Plus simple, chercher juste "Atealaan" et le code postal qui suit après un pipe
    pattern3 = r"Atealaan\s*([\dA-Z]+)\s*\|\s*([B-]?\d{4}\s*[A-Za-z\s]+)"
    m = re.search(pattern3, header_text, re.IGNORECASE)
    if m:
        street_num = m.group(1).strip()
        city = m.group(2).strip()
        address = f"Atealaan {street_num}, {city}"
        address = re.sub(r"\s+", " ", address)
        print(f"[STG ADDRESS] Pattern 3 trouvé: '{address}'")
        
        if re.search(r'(?:B-)?\d{4}', address):
            if len(address) <= 200:
                print(f"[STG ADDRESS] Adresse extraite: {address}")
                return address
    
    print(f"[STG ADDRESS] Aucune adresse trouvée")
    return None


def extract_supplier_phone_stg(text: str) -> Optional[str]:
    """Extrait le numéro de téléphone du fournisseur STG."""
    # Limiter la recherche aux 2000 premiers caractères (en-tête)
    header_text = text[:2000]
    
    # Pattern spécifique pour STG : "T 014 24 40 20" dans la ligne avec les pipes
    # Format: "Schrauwen ... |  Atealaan 34B  |  B-2200 Herentals  |  T 014 24 40 20  |  ..."
    patterns = [
        r"Schrauwen[^|]*\|\s*[^|]*\|\s*[^|]*\|\s*T\s+(\d{2,3}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",
        r"T\s+(\d{2,3}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",
        r"(?:\+|00\s*32)\s*(\d{1,2}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",
        r"Tel[:\s]*(\d{2,3}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",
    ]
    for pattern in patterns:
        m = re.search(pattern, header_text, re.IGNORECASE)
        if m:
            phone = m.group(1).strip()
            # Normaliser le format
            phone = re.sub(r"[\s./]", " ", phone)
            # Vérifier que c'est un numéro valide (au moins 8 chiffres)
            digits_only = re.sub(r'\D', '', phone)
            if len(digits_only) >= 8:
                return phone
    return None


def extract_supplier_email_stg(text: str) -> Optional[str]:
    """Extrait l'email du fournisseur STG."""
    # Limiter la recherche aux 2000 premiers caractères (en-tête)
    header_text = text[:2000]
    
    # Pattern spécifique pour STG : "info@stg-group.be" ou "AR.Schrauwen@STG-group.be"
    # Format: "Schrauwen ... |  ...  |  ...  |  ...  |  ...  |  info@stg-group.be"
    patterns = [
        r"Schrauwen[^|]*\|\s*[^|]*\|\s*[^|]*\|\s*[^|]*\|\s*[^|]*\|\s*([a-z0-9._%+-]+@(?:stg-group|schrauwen)[a-z0-9.-]*\.[a-z]{2,})",
        r"([a-z0-9._%+-]+@(?:stg-group|schrauwen)[a-z0-9.-]*\.[a-z]{2,})",
        r"([a-z0-9._%+-]+@[a-z0-9.-]+\.be)",
    ]
    for pattern in patterns:
        m = re.search(pattern, header_text, re.IGNORECASE)
        if m:
            email = m.group(1).lower().strip()
            # Vérifier que c'est un email valide (contient @ et un point)
            if '@' in email and '.' in email.split('@')[1]:
                return email
    return None


def extract_supplier_contact_stg(text: str) -> Optional[str]:
    """Extrait le nom du contact commercial STG si présent."""
    # Limiter la recherche aux 2000 premiers caractères (en-tête)
    header_text = text[:2000]
    
    # Mots-clés à exclure (faux positifs)
    exclude_keywords = ['PAYMENT', 'DUE', 'DAYS', 'BANK', 'PROFORMA', 'RELATED', 'DOCUMENTS', 'CLIENT', 'DETAILS', 'DELIVERY', 'ADDRESS']
    
    # Chercher des patterns comme "Contact: Nom" ou "Sales: Nom"
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
                not any(word.lower() in ['nv', 'bv', 'sprl', 'sa', 'schrauwen', 'sanitair', 'verwarming'] for word in contact.split()) and
                not any(keyword in contact.upper() for keyword in exclude_keywords)):
                return contact
    return None


def extract_supplier_payment_terms_stg(text: str) -> Optional[str]:
    """Extrait les conditions de paiement STG."""
    # Chercher dans tout le texte, pas seulement le footer
    # "60 dagen na factuurdatum" peut être n'importe où dans le document
    # DEBUG: Afficher le footer recherché
    print(f"[STG PAYMENT] Recherche dans tout le texte ({len(text)} caractères)")
    
    # Chercher dans une zone plus large : milieu du document (où se trouve souvent "60 dagen na factuurdatum")
    # Prendre le texte entre les caractères 5000 et 10000 (zone où se trouve généralement cette info)
    mid_text = text[5000:10000] if len(text) > 10000 else text[5000:] if len(text) > 5000 else text
    footer_text = text[-5000:] if len(text) > 5000 else text  # Footer plus large
    
    print(f"[STG PAYMENT] Zone milieu (5000-10000): {mid_text[:300]}")
    print(f"[STG PAYMENT] Footer (derniers 5000): {footer_text[:300]}")
    
    # Pattern spécifique pour STG : "60 dagen na factuurdatum" (ligne 188 dans le log original)
    # Format exact : "60 dagen na factuurdatum"
    patterns = [
        r"(\d+\s+dagen\s+na\s+factuurdatum)",
        r"(\d+\s*(?:dagen|days|jours)\s*(?:na|after)\s*(?:factuurdatum|invoice|facture))",
        r"(?:betalingsvoorwaarden|payment\s+terms|conditions\s+de\s+paiement)[\s:]+([^\n]{5,100})",
        r"payment\s+due[\s:]+(\d+\s*(?:days|jours|dagen))",
        r"(\d+\s*(?:days|jours|dagen)\s*(?:net|after\s+invoice|after\s+delivery))",
    ]
    
    # Mots-clés à exclure (faux positifs)
    exclude_keywords = ['RELATED', 'DOCUMENTS', 'CLIENT', 'DETAILS', 'DELIVERY', 'ADDRESS', 'BANK', 'ACCOUNT', 'CUSTOMER', 'VRAAGEN', 'OPMERKINGEN']
    
    # Chercher dans la zone milieu d'abord
    for i, pattern in enumerate(patterns):
        m = re.search(pattern, mid_text, re.IGNORECASE)
        if m:
            terms = m.group(1).strip()
            print(f"[STG PAYMENT] Pattern {i+1} trouvé dans zone milieu: '{terms}'")
            # Filtrer les faux positifs
            if not any(keyword in terms.upper() for keyword in exclude_keywords):
                # Nettoyer
                terms = re.sub(r"\s+", " ", terms)
                # Vérifier qu'il contient des informations de paiement
                if len(terms) > 3 and any(word in terms.upper() for word in ['DAY', 'NET', 'PAYMENT', 'DUE', 'JOUR', 'RECEPTION', 'DAGEN', 'FACTUURDATUM', 'NA']):
                    print(f"[STG PAYMENT] Conditions de paiement extraites: {terms[:100]}")
                    return terms[:100]  # Limiter à 100 caractères
    
    # Chercher dans le footer si pas trouvé dans la zone milieu
    for i, pattern in enumerate(patterns):
        m = re.search(pattern, footer_text, re.IGNORECASE)
        if m:
            terms = m.group(1).strip()
            print(f"[STG PAYMENT] Pattern {i+1} trouvé dans footer: '{terms}'")
            if not any(keyword in terms.upper() for keyword in exclude_keywords):
                terms = re.sub(r"\s+", " ", terms)
                if len(terms) > 3 and any(word in terms.upper() for word in ['DAY', 'NET', 'PAYMENT', 'DUE', 'JOUR', 'RECEPTION', 'DAGEN', 'FACTUURDATUM', 'NA']):
                    print(f"[STG PAYMENT] Conditions de paiement extraites: {terms[:100]}")
                    return terms[:100]
    
    # Chercher dans tout le texte en dernier recours
    for i, pattern in enumerate(patterns):
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            terms = m.group(1).strip()
            print(f"[STG PAYMENT] Pattern {i+1} trouvé dans texte complet: '{terms}'")
            if not any(keyword in terms.upper() for keyword in exclude_keywords):
                terms = re.sub(r"\s+", " ", terms)
                if len(terms) > 3 and any(word in terms.upper() for word in ['DAY', 'NET', 'PAYMENT', 'DUE', 'JOUR', 'RECEPTION', 'DAGEN', 'FACTUURDATUM', 'NA']):
                    print(f"[STG PAYMENT] Conditions de paiement extraites: {terms[:100]}")
                    return terms[:100]
    
    print(f"[STG PAYMENT] Aucune condition de paiement trouvée")
    return None


def extract_doc_type_stg(text: str) -> str:
    """
    Détecte le type de document STG.
    Priorité : chercher explicitement "Verzendnota" ou "Delivery Note" d'abord,
    puis "Factuur" ou "Invoice".
    """
    up = text.upper()
    
    # Chercher d'abord les mots-clés de bon de livraison (priorité)
    verzendnota_keywords = [
        "VERZENDNOTA",
        "DELIVERY NOTE",
        "LEVERINGSNOTA",
        "BON DE LIVRAISON",
        "BON LIVRAISON"
    ]
    for keyword in verzendnota_keywords:
        if keyword in up:
            return "Verzendnota"
    
    # Chercher ensuite les mots-clés de facture
    factuur_keywords = [
        "FACTUUR",
        "INVOICE",
        "FACTURE"
    ]
    for keyword in factuur_keywords:
        if keyword in up:
            return "Factuur"
    
    # Par défaut, si aucun mot-clé trouvé, vérifier s'il y a des prix dans les items
    # (sera fait dans la fonction parse)
    return "Verzendnota"  # Par défaut BL


# -----------------------------------------------------------------------------
# PARSER
# -----------------------------------------------------------------------------

def parse(pdf_raw: Dict) -> Dict:
    pages = pdf_raw.get("pages", [])
    text = pdf_raw.get("full_text", "")

    # reconstruire toutes lignes
    lines: List[str] = []
    for page in pages:
        ws = page.get("words", [])
        if ws:
            lns = build_lines(ws)
            for ln in lns:
                txt = " ".join(t for _, t in ln).strip()
                if txt:
                    lines.append(txt)
        else:
            for ln in page.get("text", "").splitlines():
                ln = ln.strip()
                if ln:
                    lines.append(ln)

    items = []
    i = 0
    while i < len(lines):
        line = lines[i]

        # -------------------------
        # FACTURE AVEC PRIX
        # -------------------------
        m = PRODUCT_INVOICE_PRICE_RE.match(line)
        if m:
            sku = m.group(1)
            supplier_sku = m.group(2)
            desc = clean_desc(m.group(3))
            qty = float(m.group(4).replace(",", "."))
            unit = m.group(5).upper()
            
            # Extraire tous les prix de la ligne pour déterminer lequel est le prix net
            # Structure possible:
            # - 2 prix: prix_brut total -> on doit identifier lequel est le brut
            # - 3 prix: prix_brut prix_net total -> on prend l'avant-dernier (prix net)
            all_prices = re.findall(r"(\d+[.,]\d+)\s*€?", line)
            
            if len(all_prices) >= 3:
                # Structure: prix_brut prix_net total
                # Prendre le prix NET (avant-dernier, car le dernier est le total)
                unit_price = float(all_prices[-2].replace(",", "."))  # Prix net
                line_total = float(all_prices[-1].replace(",", "."))  # Total
            elif len(all_prices) == 2:
                # Structure: prix total
                # Vérifier lequel correspond au total (le plus grand est généralement le total)
                price1 = float(all_prices[0].replace(",", "."))
                price2 = float(all_prices[1].replace(",", "."))
                
                # Si price1 * qty ≈ price2, alors price1 est le prix unitaire et price2 le total
                # Sinon, price1 est probablement le brut et il manque le prix net
                if abs(price1 * qty - price2) < 0.01:
                    # price1 est le prix unitaire (net), price2 est le total
                    unit_price = price1
                    line_total = price2
                else:
                    # price1 est probablement le brut, price2 le total
                    # Calculer le prix net à partir du total
                    unit_price = price2 / qty if qty > 0 else price1
                    line_total = price2
            else:
                # Fallback: utiliser les groupes de la regex
                # Si la regex a capturé 3 prix (brut, net, total), prendre le net
                if m.lastindex >= 8:
                    unit_price = float(m.group(7).replace(",", "."))  # Prix net
                    line_total = float(m.group(8).replace(",", "."))  # Total
                elif m.lastindex >= 7:
                    # 2 prix capturés: vérifier lequel est le total
                    price1 = float(m.group(6).replace(",", "."))
                    price2 = float(m.group(7).replace(",", "."))
                    if abs(price1 * qty - price2) < 0.01:
                        unit_price = price1
                        line_total = price2
                    else:
                        unit_price = price2 / qty if qty > 0 else price1
                        line_total = price2
                else:
                    # Un seul prix: c'est le prix unitaire
                    unit_price = float(m.group(6).replace(",", "."))
                    line_total = unit_price * qty if qty > 0 else None

            # description suite sur 2 lignes max
            if i+1 < len(lines) and not re.match(r"^\d{8}\s+\d{9}", lines[i+1]):
                desc += " " + clean_desc(lines[i+1])
            if i+2 < len(lines) and not re.match(r"^\d{8}\s+\d{9}", lines[i+2]):
                desc += " " + clean_desc(lines[i+2])

            items.append({
                "sku": sku,
                "supplier_sku": supplier_sku,
                "ean": None,
                "description": desc,
                "qty": qty,
                "unit": unit,
                "unit_price": unit_price,
                "line_total": line_total
            })
            i += 1
            continue

        # -------------------------
        # BL STANDARD
        # -------------------------
        m = PRODUCT_RE.match(line)
        if m:
            sku = m.group(1)
            supplier_sku = m.group(2)
            desc = clean_desc(m.group(3))
            qty = float(m.group(5).replace(",", "."))  # quantité livrée
            unit = m.group(6).upper()

            if i+1 < len(lines) and not re.match(r"^\d{8}\s+\d{9}", lines[i+1]):
                desc += " " + clean_desc(lines[i+1])
            if i+2 < len(lines) and not re.match(r"^\d{8}\s+\d{9}", lines[i+2]):
                desc += " " + clean_desc(lines[i+2])

            items.append({
                "sku": sku,
                "supplier_sku": supplier_sku,
                "ean": None,
                "description": desc,
                "qty": qty,
                "unit": unit,
                "unit_price": None,
                "line_total": None
            })
            i += 1
            continue

        # -------------------------
        # FACTURE (SANS PRIX)
        # -------------------------
        m = PRODUCT_INVOICE_RE.match(line)
        if m:
            sku = m.group(1)
            supplier_sku = m.group(2)
            desc = clean_desc(m.group(3))
            qty = float(m.group(4).replace(",", "."))
            unit = m.group(5).upper()

            items.append({
                "sku": sku,
                "supplier_sku": supplier_sku,
                "ean": None,
                "description": desc,
                "qty": qty,
                "unit": unit,
                "unit_price": None,
                "line_total": None
            })
            i += 1
            continue

        i += 1

    # METADATA
    doc_type = extract_doc_type_stg(text)
    
    # Vérifier s'il y a des prix dans les items (indicateur fort d'une facture)
    has_prices = any(item.get("unit_price") is not None and item.get("unit_price", 0) > 0 for item in items)
    
    # Si on a détecté "Verzendnota" mais qu'il y a des prix, c'est probablement une facture
    if doc_type == "Verzendnota" and has_prices:
        # Vérifier à nouveau le texte pour être sûr
        up = text.upper()
        if "FACTUUR" in up or "INVOICE" in up:
            doc_type = "Factuur"
        # Sinon, garder "Verzendnota" car la détection initiale était correcte
    
    # Si on a détecté "Factuur" mais qu'il n'y a pas de prix, vérifier à nouveau
    if doc_type == "Factuur" and not has_prices:
        # Vérifier si c'est vraiment un BL
        up = text.upper()
        if "VERZENDNOTA" in up or "DELIVERY NOTE" in up:
            doc_type = "Verzendnota"
    
    meta = {
        "type": doc_type,
        "number": extract_number_stg(text),
        "client": extract_client_stg(text),
        "supplier": extract_supplier_stg(text),
        "date": extract_date_stg(text),
        "count": len(items),
        "method": "stg_v2",
        "supplier_code": extract_supplier_code_stg(text),
        "supplier_address": extract_supplier_address_stg(text),
        "supplier_phone": extract_supplier_phone_stg(text),
        "supplier_email": extract_supplier_email_stg(text),
        "supplier_contact": extract_supplier_contact_stg(text),
        "supplier_payment_terms": extract_supplier_payment_terms_stg(text)
    }

    return {"items": items, "metadata": meta}
