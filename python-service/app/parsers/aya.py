"""
Parser AYA MARKET SPRL
Compatible :
 - Factuur (Facture)

Fonction exposee :
    parse(pdf_raw: Dict) -> Dict
"""

import re
from typing import Dict, List, Optional


# -----------------------------------------------------------------------------
# REGEX PRODUITS
# -----------------------------------------------------------------------------

# Structure de la table AYA:
# Référence | Description | Nbr. | Prix Vente | Montant | TVA
# Exemple: BDAJAA601 | PP/Alu coude 45° Ø 80/125 | 4 | 48,04 | 65% -10% | 60,53
# La table peut avoir des séparateurs variables (espaces, tabulations, pipes)
PRODUCT_RE = re.compile(
    r"""
    ^\s*
    ([A-Z0-9,/-]+?)\s*[|\s]+\s*     # Référence (SKU) - peut contenir virgules, slashes, suivi de | ou espaces
    (.+?)\s*[|\s]+\s*                # Description (jusqu'au prochain séparateur)
    (\d+)\s*[|\s]+\s*                # Nbr. (quantité)
    (\d+[.,]\d+)\s*[|\s]+\s*         # Prix Vente (prix unitaire)
    (?:.*?[-%]+\s*[|\s]*)?           # Montant (réductions en %) - optionnel, on ignore
    (\d+[.,]\d+)\s*                  # TVA (montant total de la ligne après réductions)
    """,
    re.IGNORECASE | re.VERBOSE
)

# Version plus flexible pour les lignes avec format variable (sans pipes obligatoires)
PRODUCT_RE_FLEX = re.compile(
    r"""
    ^\s*
    ([A-Z0-9][A-Z0-9,/-]{3,})\s+    # Référence (SKU) - au moins 4 caractères
    (.+?)\s+                         # Description
    (\d+)\s+                         # Nbr. (quantité)
    (\d+[.,]\d+)\s+                  # Prix Vente (prix unitaire)
    (\d+[.,]\d+)\s*                  # TVA (montant total) - peut être le dernier nombre
    """,
    re.IGNORECASE | re.VERBOSE
)

# Version très simple : SKU + Description + Qty + Prix + Total (sans séparateurs stricts)
PRODUCT_RE_SIMPLE = re.compile(
    r"""
    ^\s*
    ([A-Z0-9][A-Z0-9,/-]{3,})\s+    # Référence (SKU)
    (.+?)\s+                         # Description
    (\d+)\s+                         # Quantité
    (\d+[.,]\d+)\s+                  # Prix unitaire
    (\d+[.,]\d+)                     # Total
    """,
    re.IGNORECASE | re.VERBOSE
)

# Version alternative sans la colonne TVA (si elle n'est pas présente)
PRODUCT_RE_NO_TVA = re.compile(
    r"""
    ^\s*
    ([A-Z0-9,/-]+?)\s+              # Référence (SKU)
    (.+?)\s+                        # Description
    (\d+)\s+                        # Nbr. (quantité)
    (\d+[.,]\d+)\s*                 # Prix Vente (prix unitaire)
    """,
    re.IGNORECASE | re.VERBOSE
)


# -----------------------------------------------------------------------------
# UTILS
# -----------------------------------------------------------------------------

def clean_desc(text: str) -> str:
    """Nettoie la description."""
    return re.sub(r"\s+", " ", text).strip()


def parse_price(price_str: str) -> float:
    """Convertit un prix avec virgule en float."""
    return float(price_str.replace(',', '.'))


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

def extract_type_aya(text: str) -> str:
    """Extrait le type de document."""
    text_lower = text.lower()
    if 'facture' in text_lower or 'factuur' in text_lower:
        return 'Factuur'
    elif 'bon de livraison' in text_lower or 'leveringsbon' in text_lower:
        return 'Leveringsbon'
    else:
        return 'Factuur'  # Par défaut


def extract_number_aya(text: str) -> Optional[str]:
    """Extrait le numéro de facture."""
    # Format: BARA-202517723
    # Chercher d'abord le pattern complet BARA- suivi de chiffres
    patterns = [
        r'BARA-(\d{6,})',  # Format: BARA-202517723 (au moins 6 chiffres)
        r'(?:numéro|nummer|facture|factuur)[\s:]*BARA-(\d{6,})',  # Avec label
        r'BARA[\s-]+(\d{6,})',  # Avec espace ou tiret
        r'(?:facture|factuur|nummer|numéro)[\s:]*([A-Z]{4,}-\d{6,})',  # Format générique avec préfixe
        r'INV[_-]?([A-Z0-9_-]+)',  # Format INV_xxx
    ]
    
    for pattern in patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match:
            num = match.group(1) if match.lastindex >= 1 else match.group(0)
            if num:
                # Si on a trouvé juste le numéro, ajouter le préfixe BARA- si nécessaire
                if num.isdigit() and len(num) >= 6 and 'BARA' in text.upper():
                    return f"BARA-{num}"
                elif num.startswith('BARA-'):
                    return num
                elif 'BARA' in text.upper() and not num.startswith('BARA') and num.isdigit() and len(num) >= 6:
                    return f"BARA-{num}"
                elif '-' in num and len(num) > 5:  # Format déjà complet
                    return num
                return num
    
    return None


def extract_date_aya(text: str) -> Optional[str]:
    """Extrait la date de facture."""
    # Format: 17/11/2025
    patterns = [
        r'(?:date|datum)[\s:]*(\d{1,2})[/-](\d{1,2})[/-](\d{2,4})',
        r'(\d{1,2})[/-](\d{1,2})[/-](\d{2,4})',
    ]
    
    for pattern in patterns:
        match = re.search(pattern, text)
        if match:
            day, month, year = match.groups()
            if len(year) == 2:
                year = '20' + year
            return f"{day}/{month}/{year}"
    
    return None


def extract_client_aya(text: str) -> Optional[str]:
    """Extrait le nom du client."""
    # Le client apparaît juste avant "AYA MARKET SPRL"
    # Structure dans le PDF:
    # - Adresse (ex: "RUE BARA 115")
    # - Nom du client (ex: "SENKO BV", "COMPANY NAME BV", etc.)
    # - "AYA MARKET SPRL" (fournisseur)
    
    # Chercher "AYA MARKET" pour trouver la position
    aya_pos = text.find('AYA MARKET')
    if aya_pos > 0:
        # Chercher dans les 300 caractères avant "AYA MARKET"
        text_before_aya = text[max(0, aya_pos - 300):aya_pos]
        
        # Diviser en lignes pour analyser
        lines = text_before_aya.split('\n')
        
        # Parcourir les lignes en sens inverse pour trouver le nom du client
        # (il est généralement sur la dernière ligne avant "AYA MARKET")
        for i in range(len(lines) - 1, -1, -1):
            line = lines[i].strip()
            if not line:
                continue
            
            # Ignorer les lignes qui sont clairement des adresses ou autres infos
            # (codes postaux, numéros de téléphone, emails, etc.)
            if re.search(r'\d{4}\s+[A-Z]', line):  # Code postal (ex: "2550 Kontich")
                continue
            if re.search(r'(?:Tel|Fax|Email|@)', line, re.IGNORECASE):  # Téléphone, fax, email
                continue
            if re.search(r'BE\d{2}\s+\d{4}', line):  # Numéro de compte bancaire
                continue
            if re.search(r'RUE|STREET|AVENUE|ROAD', line, re.IGNORECASE):  # Adresse
                continue
            if re.search(r'^\d+', line) and len(line) < 20:  # Numéro seul
                continue
            
            # Chercher un nom de société (majuscules, peut contenir des espaces, se termine par BV/SPRL/etc.)
            # Pattern pour trouver des lignes comme "SENKO BV", "COMPANY NAME SPRL", etc.
            company_pattern = r'^([A-Z][A-Z\s&.,-]{2,}(?:BV|SPRL|SA|NV|LTD|INC|SRL|GMBH|AS|AB))\s*$'
            match = re.match(company_pattern, line)
            if match:
                client_name = match.group(1).strip()
                # Nettoyer les espaces multiples
                client_name = re.sub(r'\s+', ' ', client_name)
                # Ignorer si c'est "AYA MARKET" ou contient des mots-clés du fournisseur
                if 'AYA' not in client_name.upper() and 'MARKET' not in client_name.upper():
                    return client_name
            
            # Si la ligne contient des majuscules et fait entre 3 et 50 caractères, c'est peut-être le client
            if line.isupper() and 3 <= len(line) <= 50 and not re.search(r'\d{4}', line):
                # Vérifier que ce n'est pas une adresse ou autre
                if not re.search(r'(?:RUE|STREET|AVENUE|ROAD|KONTICH|ANDERLECHT)', line, re.IGNORECASE):
                    return line
    
    return None


def extract_supplier_aya(text: str) -> str:
    """Extrait le fournisseur."""
    if 'aya market' in text.lower():
        return 'AYA MARKET SPRL'
    return 'AYA MARKET SPRL'  # Par défaut


def extract_supplier_code_aya(text: str) -> Optional[str]:
    """Extrait le code fournisseur AYA si présent."""
    patterns = [
        r"(?:code|supplier\s+code|fournisseur)[\s:]+([A-Z0-9]{3,20})",
        r"AYA[\s-]+([A-Z0-9]{3,10})",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            return m.group(1).strip()
    return None


def extract_supplier_address_aya(text: str) -> Optional[str]:
    """Extrait l'adresse complète du fournisseur AYA."""
    # Chercher l'adresse après "AYA MARKET SPRL"
    patterns = [
        r"AYA\s+MARKET\s+SPRL[^|\n]*(?:\n|\|)\s*([A-Za-z0-9\s,.-]+(?:B-)?\d{4}\s+[A-Za-z\s]+)",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            address = m.group(1).strip()
            address = re.sub(r"\s+", " ", address)
            return address
    return None


def extract_supplier_phone_aya(text: str) -> Optional[str]:
    """Extrait le numéro de téléphone du fournisseur AYA."""
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


def extract_supplier_email_aya(text: str) -> Optional[str]:
    """Extrait l'email du fournisseur AYA."""
    patterns = [
        r"([a-z0-9._%+-]+@(?:aya|ayamarket)[a-z0-9.-]*\.[a-z]{2,})",
        r"([a-z0-9._%+-]+@[a-z0-9.-]+\.be)",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            return m.group(1).lower().strip()
    return None


def extract_supplier_contact_aya(text: str) -> Optional[str]:
    """Extrait le nom du contact commercial AYA si présent."""
    patterns = [
        r"(?:contact|sales|commercial)[\s:]+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",
        r"([A-Z][a-z]+\s+[A-Z][a-z]+)(?:\s+-\s*(?:sales|commercial|contact))",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            contact = m.group(1).strip()
            if len(contact.split()) <= 3 and not any(word.lower() in ['nv', 'bv', 'sprl', 'sa', 'aya', 'market'] for word in contact.split()):
                return contact
    return None


def extract_supplier_payment_terms_aya(text: str) -> Optional[str]:
    """Extrait les conditions de paiement AYA."""
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


# -----------------------------------------------------------------------------
# EXTRACT PRODUCTS
# -----------------------------------------------------------------------------

def extract_products_aya(pdf_raw: Dict) -> List[Dict]:
    """Extrait les produits de la facture AYA - structure verticale."""
    items = []
    
    # Utiliser le texte brut directement
    full_text_raw = pdf_raw.get('full_text', '')
    text_lines = [line.strip() for line in full_text_raw.split('\n')]
    
    print("[AYA PARSER] Extraction des produits...")
    print(f"[AYA PARSER] Nombre de lignes: {len(text_lines)}")
    
    # Chercher la section des produits (après "Référence" ou "Referentie")
    table_start = -1
    for i, line in enumerate(text_lines):
        if re.search(r'référence|referentie', line, re.IGNORECASE):
            table_start = i
            print(f"[AYA PARSER] En-tête de table trouvé à la ligne {i}: {line}")
            break
    
    if table_start == -1:
        print("[AYA PARSER] Aucun en-tête de table trouvé")
        return items
    
    # La structure est VERTICALE : chaque colonne est sur une ligne différente
    # Pattern pour un produit :
    #   - SKU (code produit) : ligne avec code alphanumérique (ex: BDAJAA601)
    #   - Description : ligne avec texte (ex: PP/Alu coude 45° Ø 80/125)
    #   - Quantité : ligne avec nombre entier seul (ex: 4)
    #   - Prix unitaire : ligne avec prix décimal (ex: 48,04)
    #   - Montant total : ligne avec prix décimal (ex: 60,53)
    #   - TVA : ligne avec prix décimal (ex: 12,71)
    #   - Réductions : ligne avec pourcentages (ex: 65% -10%)
    
    # Stratégie : chercher les SKU valides, puis remonter pour trouver les autres éléments
    # Un SKU valide doit :
    #   - Contenir au moins 4 caractères
    #   - Commencer par une lettre ou chiffre
    #   - Ne pas être un nombre seul de 1-3 chiffres
    #   - Ne pas être un mot commun (Référence, Referentie, etc.)
    
    i = table_start + 1
    processed_skus = set()
    
    while i < len(text_lines):
        line = text_lines[i]
        if not line:
            i += 1
            continue
        
        # Arrêter si on trouve un total
        if re.search(r'total\s+(?:htva|tva|btw|totaal)', line, re.IGNORECASE):
            print(f"[AYA PARSER] Ligne de total trouvée, arrêt: {line[:50]}")
            break
        
        # Ignorer les lignes d'en-tête
        if re.search(r'page|blz|note|contestation|commande|bestelling|référence|referentie|description|beschrijving|nbr|prix|vente|tva', line, re.IGNORECASE):
            i += 1
            continue
        
        # Chercher un SKU valide
        # Format: code alphanumérique, au moins 4 caractères, commence par lettre/chiffre
        # Exemples valides: BDAJAA601, ADAHAA601, 001A002, IT-3005B
        # Exemples invalides: 4, 14, 70 (nombres seuls)
        sku_match = re.match(r'^([A-Z0-9][A-Z0-9,/-]{3,})$', line)
        if sku_match:
            sku = sku_match.group(1).strip()
            
            # Nettoyer le SKU
            if ',' in sku:
                sku = sku.split(',')[0].strip()
            
            # Ignorer si c'est un nombre seul (pas un vrai SKU)
            if sku.isdigit() and len(sku) <= 3:
                i += 1
                continue
            
            # Ignorer les mots communs
            if sku.upper() in ['REFERENCE', 'REFERENTIE', 'DESCRIPTION', 'BESCHRIJVING', 'NBR', 'PRIX', 'VENTE', 'TVA']:
                i += 1
                continue
            
            # Ignorer si déjà traité
            if sku in processed_skus:
                i += 1
                continue
            
            # Remonter dans les lignes précédentes (max 10 lignes) pour trouver les éléments
            unit_price = None
            qty = None
            description = None
            total_value = None
            
            # Parcourir les lignes précédentes de manière ordonnée
            for j in range(max(0, i - 10), i):
                prev_line = text_lines[j]
                if not prev_line:
                    continue
                
                # Chercher une quantité (nombre entier seul, 1-3 chiffres)
                if not qty and re.match(r'^\d{1,3}$', prev_line):
                    potential_qty = int(prev_line)
                    if 1 <= potential_qty <= 1000:
                        qty = potential_qty
                        continue
                
                # Chercher un prix unitaire (format: XX,XX ou XX.XX)
                # Le prix unitaire est généralement le premier prix qu'on trouve en remontant
                if not unit_price and re.match(r'^\d+[.,]\d{2}$', prev_line):
                    potential_price = parse_price(prev_line)
                    if 0.01 <= potential_price <= 10000:
                        unit_price = potential_price
                        continue
                
                # Chercher le montant total (dernier prix avant le SKU)
                if re.match(r'^\d+[.,]\d{2}$', prev_line):
                    potential_total = parse_price(prev_line)
                    if 0.01 <= potential_total <= 100000:
                        total_value = potential_total
                
                # Chercher une description (texte avec lettres, pas seulement des chiffres)
                # Ignorer les lignes qui sont des prix, quantités, ou réductions
                if not description and re.search(r'[A-Za-z]', prev_line) and len(prev_line) > 5:
                    if (not re.match(r'^\d+[.,]?\d+$', prev_line) and 
                        not re.search(r'%', prev_line) and
                        not re.match(r'^\d{1,3}$', prev_line)):
                        # C'est probablement une description
                        description = prev_line
                        continue
            
            # Si on a trouvé les éléments essentiels (SKU, prix, quantité), créer le produit
            if sku and unit_price and qty:
                if not total_value:
                    total_value = unit_price * qty
                
                if not description:
                    description = ""
                
                print(f"[AYA PARSER] ✅ Produit trouvé: SKU={sku}, Qty={qty}, Prix={unit_price}, Total={total_value}, Desc={description[:50]}")
                
                items.append({
                    'sku': sku,
                    'ean': None,
                    'description': clean_desc(description) if description else '',
                    'qty': qty,
                    'unit': 'ST',
                    'unit_price': unit_price,
                    'line_total': total_value
                })
                
                processed_skus.add(sku)
        
        i += 1
    
    print(f"[AYA PARSER] Total produits extraits: {len(items)}")
    return items


# -----------------------------------------------------------------------------
# MAIN PARSE FUNCTION
# -----------------------------------------------------------------------------

def parse(pdf_raw: Dict) -> Dict:
    """
    Parse un document AYA.
    
    Args:
        pdf_raw: Dictionnaire avec 'words', 'full_text', etc.
    
    Returns:
        Dict avec 'items' (liste de produits) et 'metadata'
    """
    text = pdf_raw.get('full_text', '')
    
    # LOG: Afficher le texte extrait pour déboguer
    print("=" * 80)
    print("[AYA PARSER] TEXTE EXTRAIT DU PDF (premiers 2000 caractères):")
    print("=" * 80)
    print(text[:2000])
    print("=" * 80)
    
    # Chercher spécifiquement le numéro TVA client dans le texte
    print("[AYA PARSER] RECHERCHE DU NUMERO TVA CLIENT:")
    print("=" * 80)
    
    # Chercher toutes les occurrences de "BE" suivies de chiffres
    be_patterns = list(re.finditer(r'BE\s*(\d{3}[.,]\d{3}[.,]\d{3})', text, re.IGNORECASE))
    print(f"[AYA PARSER] Nombre d'occurrences 'BE xxx.xxx.xxx' trouvées: {len(be_patterns)}")
    for match in be_patterns:
        print(f"[AYA PARSER] Trouvé: '{match.group(0)}' à la position {match.start()}")
        # Afficher le contexte (100 caractères avant et après)
        start = max(0, match.start() - 100)
        end = min(len(text), match.end() + 100)
        context = text[start:end]
        print(f"[AYA PARSER] Contexte: ...{context}...")
    
    # Chercher "BTW Nummer Klant" et le contexte autour
    btw_patterns = list(re.finditer(r'BTW\s+Nummer\s+Klant', text, re.IGNORECASE))
    print(f"[AYA PARSER] Nombre d'occurrences 'BTW Nummer Klant' trouvées: {len(btw_patterns)}")
    for match in btw_patterns:
        print(f"[AYA PARSER] Trouvé 'BTW Nummer Klant' à la position {match.start()}")
        # Afficher 300 caractères après
        start = match.start()
        end = min(len(text), match.end() + 300)
        context = text[start:end]
        print(f"[AYA PARSER] Contexte après 'BTW Nummer Klant': {context}")
    
    # Chercher aussi "Numéro TVA Client"
    tva_patterns = list(re.finditer(r'Numéro\s+TVA\s+Client', text, re.IGNORECASE))
    print(f"[AYA PARSER] Nombre d'occurrences 'Numéro TVA Client' trouvées: {len(tva_patterns)}")
    for match in tva_patterns:
        print(f"[AYA PARSER] Trouvé 'Numéro TVA Client' à la position {match.start()}")
        # Afficher 300 caractères après
        start = match.start()
        end = min(len(text), match.end() + 300)
        context = text[start:end]
        print(f"[AYA PARSER] Contexte après 'Numéro TVA Client': {context}")
    
    print("=" * 80)
    
    # Extraire les métadonnées
    doc_type = extract_type_aya(text)
    number = extract_number_aya(text)
    date = extract_date_aya(text)
    client = extract_client_aya(text)
    supplier = extract_supplier_aya(text)
    
    print(f"[AYA PARSER] Résultat extraction:")
    print(f"  - Type: {doc_type}")
    print(f"  - Numéro: {number}")
    print(f"  - Date: {date}")
    print(f"  - Client: {client}")
    print(f"  - Supplier: {supplier}")
    print("=" * 80)
    
    # Extraire les produits
    items = extract_products_aya(pdf_raw)
    
    return {
        'items': items,
        'metadata': {
            'type': doc_type,
            'number': number,
            'client': client,
            'supplier': supplier,
            'date': date,
            'count': len(items),
            'method': 'aya_v1',
            'supplier_code': extract_supplier_code_aya(text),
            'supplier_address': extract_supplier_address_aya(text),
            'supplier_phone': extract_supplier_phone_aya(text),
            'supplier_email': extract_supplier_email_aya(text),
            'supplier_contact': extract_supplier_contact_aya(text),
            'supplier_payment_terms': extract_supplier_payment_terms_aya(text)
        }
    }

