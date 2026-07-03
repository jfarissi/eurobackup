import re
from typing import Dict, List, Tuple, Optional

# ------------------------------------------------------------
# CONSTANTES
# ------------------------------------------------------------

BAD_WORDS = [
    "onvoorwaardelijke", "netto", "kort", "verkoopsvoorwaarden",
    "algemene", "totaal", "btw", "bank", "ean:", "artikel:",
    "%", "nummerplaat", "transporteur", "shipping point", "verzendpunt",
    "frans baetenstraat", "antwerpen", "tel.", "pos.", "artikel nr",
    "beschrijving", "geleverde hoeveelheid", "gewicht",  # on garde les trucs vraiment "admin"
    # IMPORTANT : on NE met PAS "eur" ici (pour ne pas casser 'Euroring')
]

EXCLUDED_KEYWORDS = [
    "euro-palet", "euro palet", "euro-pallet",
    "pallet", "palet", "palette", "euro pallet",
]

META_PATTERNS = {
    "type": [
        r"type\s*:\s*(.+)",
        r"document\s*:\s*(.+)",
        r"factuur",  # si la ligne est juste "Factuur"
    ],
    "number": [
        r"(?:factuurnummer|nummer|nr)\s*:\s*([A-Za-z0-9\-]+)"
    ],
    "client": [
        r"(?:klant|client)\s*:\s*(.+)"
    ],
    "supplier": [
        r"(?:leverancier|fournisseur)\s*:\s*(.+)"
    ],
    "date": [
        r"(?:datum|date)\s*:\s*(\d{2}[\/\-.]\d{2}[\/\-.]\d{4})"
    ]
}

# Regex : une ligne quantité Knauf classique
# Accepte aussi les décimales pour les tonnes (ex: "8 ST 2,4 TONNE")
QTY_LINE_RE = re.compile(r"^(\d+(?:[.,]\d+)?)\s+(PAC|PC|KG|ST|PAK)\b", re.IGNORECASE)


# ------------------------------------------------------------
# DESCRIPTION
# ------------------------------------------------------------

def is_desc_candidate(line: str) -> bool:
    """
    Heuristique pour reconnaître une vraie description produit.
    Exemple: 'Spijkerplug 50 x 6 mm (20 st / Blister)'
    """
    l = line.lower().strip()

    # au moins un mot avec des lettres
    if not re.search(r"[a-z]{3,}", l):
        return False

    # éviter les lignes trop courtes
    if len(l.split()) < 1:
        return False

    # pas de mots administratifs
    if any(b in l for b in BAD_WORDS):
        return False

    # pas de vrai prix dedans (12,34)
    if re.search(r"\d+\s*[.,]\s*\d{2}", l):
        return False

    return True


def is_excluded(desc: str) -> bool:
    """
    Exclure les Euro-palet / palettes, etc.
    """
    d = desc.lower()
    return any(k in d for k in EXCLUDED_KEYWORDS)

# ------------------------------------------------------------
# METADATA : à partir des lignes du PDF
# ------------------------------------------------------------
def extract_number(text: str) -> Optional[str]:
    # PRIORITÉ ABSOLUE: Chercher d'abord un numéro de facture
    # Format: "Faktuur nr.", "Factuur nr." ou "Nummer" suivi du numéro (peut être sur la ligne suivante)
    
    # Pattern 1: "Nummer" suivi de "Datum" puis "Blad" puis le numéro (format POS)
    # Format: "Nummer\nDatum\nBlad\n713691441" ou "Nummer Datum Blad 713691441"
    # Chercher "Nummer" puis n'importe quoi jusqu'au premier numéro de 8-12 chiffres
    m = re.search(r"nummer\s+datum\s+blad\s+(\d{8,12})", text, re.IGNORECASE | re.MULTILINE | re.DOTALL)
    if m:
        num = m.group(1)
        if len(num) >= 8 and len(num) <= 12 and not (len(num) == 13 and num[0] in ['4', '5', '8']):
            return num
    
    # Pattern 2: "Nummer" suivi de "Datum" puis le numéro (sans "Blad")
    m = re.search(r"nummer\s+datum\s+[^\d]*?(\d{8,12})", text, re.IGNORECASE | re.MULTILINE | re.DOTALL)
    if m:
        num = m.group(1)
        if len(num) >= 8 and len(num) <= 12 and not (len(num) == 13 and num[0] in ['4', '5', '8']):
            return num
    
    # Pattern 3: "Nummer" suivi directement du numéro (même ligne ou ligne suivante)
    # Utiliser [^\d]*? pour matcher n'importe quoi jusqu'au prochain chiffre
    m = re.search(r"nummer\s*:?\s*[^\d]*?(\d{8,12})", text, re.IGNORECASE | re.MULTILINE | re.DOTALL)
    if m:
        num = m.group(1)
        # Vérifier que ce n'est pas un EAN (13 chiffres) ou un numéro trop court
        # Et que ce n'est pas "Uw klantnr." (numéro de client)
        if len(num) >= 8 and len(num) <= 12 and not (len(num) == 13 and num[0] in ['4', '5', '8']):
            # Vérifier que ce n'est pas précédé de "klantnr" (numéro de client)
            match_pos = m.start()
            before_match = text[max(0, match_pos-20):match_pos].lower()
            if "klantnr" not in before_match:
                return num
    
    # Pattern 3: "Faktuur nr." suivi du numéro (même ligne ou ligne suivante)
    # Utiliser [^\d]*? pour matcher n'importe quoi jusqu'au prochain chiffre (non-greedy)
    m = re.search(r"faktuur\s+nr\.\s*:?\s*[^\d]*?(\d{8,12})", text, re.IGNORECASE | re.MULTILINE | re.DOTALL)
    if m:
        num = m.group(1)
        if len(num) >= 8 and len(num) <= 12 and not (len(num) == 13 and num[0] in ['4', '5', '8']):
            return num
    
    # Pattern 4: "Faktuur nr." suivi directement du numéro (sans caractères intermédiaires)
    m = re.search(r"faktuur\s+nr\.\s*:?\s*(\d{8,12})", text, re.IGNORECASE)
    if m:
        num = m.group(1)
        if len(num) >= 8 and len(num) <= 12 and not (len(num) == 13 and num[0] in ['4', '5', '8']):
            return num
    
    # Pattern 5: "Factuur nr." (variante) avec caractères intermédiaires
    m = re.search(r"factu(u|o)r\s+nr\.\s*:?\s*[^\d]*?(\d{8,12})", text, re.IGNORECASE | re.MULTILINE | re.DOTALL)
    if m:
        num = m.group(2)
        if len(num) >= 8 and len(num) <= 12 and not (len(num) == 13 and num[0] in ['4', '5', '8']):
            return num
    
    # Pattern 6: "Factuur nr." suivi directement du numéro
    m = re.search(r"factu(u|o)r\s+nr\.\s*:?\s*(\d{8,12})", text, re.IGNORECASE)
    if m:
        num = m.group(2)
        if len(num) >= 8 and len(num) <= 12 and not (len(num) == 13 and num[0] in ['4', '5', '8']):
            return num
    
    # Si pas de facture trouvée, vérifier si c'est vraiment un bon de livraison
    # (pas une facture qui contient aussi "Afleveringsbon")
    has_factuur = bool(re.search(r"factu(u|o)r", text, re.IGNORECASE))
    
    # Si c'est une facture, ne pas chercher de numéro de bon de livraison
    if has_factuur:
        return None
    
    # Si pas de facture, chercher un numéro de livraison (Leveringsbevestiging)
    # Format: "Levering 9209140563" ou "9209140563" après "Leveringsbevestiging"
    # IMPORTANT: Ne pas chercher "Afleveringsbon" car c'est présent dans les factures aussi
    # Chercher "Levering" suivi d'un numéro de 8-10 chiffres (mais pas "Afleveringsbon")
    m = re.search(r"(?<!afleveringsbon\s)(?:^|\s)levering\s+(\d{8,10})\b", text, re.IGNORECASE | re.MULTILINE)
    if m:
        num = m.group(1)
        # Vérifier que ce n'est pas un EAN (13 chiffres commençant par 4, 5, ou 8)
        if not (len(num) == 13 and num[0] in ['4', '5', '8']):
            return num
    
    # Chercher "Afleveringsbon" suivi d'un numéro (uniquement si ce n'est pas une facture)
    # Le numéro peut être sur la même ligne ou la ligne suivante
    m = re.search(r"afleveringsbon\s*:?\s*(\d{8,10})\b", text, re.IGNORECASE)
    if m and not has_factuur:
        num = m.group(1)
        if not (len(num) == 13 and num[0] in ['4', '5', '8']):
            return num
    
    # Chercher "Afleveringsbon" sur une ligne, numéro sur la ligne suivante
    m = re.search(r"afleveringsbon\s*:?\s*\n\s*(\d{8,10})\b", text, re.IGNORECASE | re.MULTILINE)
    if m and not has_factuur:
        num = m.group(1)
        if not (len(num) == 13 and num[0] in ['4', '5', '8']):
            return num
    
    # Chercher un numéro isolé de 8-10 chiffres après "Leveringsbevestiging" (pas "Afleveringsbon")
    m = re.search(r"leveringsbevestiging\s*:?\s*(\d{8,10})\b", text, re.IGNORECASE)
    if m and not has_factuur:
        num = m.group(1)
        if len(num) >= 8 and len(num) <= 10:
            if not (len(num) == 13 and num[0] in ['4', '5', '8']):
                return num
    
    return None


def extract_date(text: str) -> Optional[str]:
    m = re.search(r"\b(\d{2}\.\d{2}\.\d{4})\b", text)
    return m.group(1) if m else None


def extract_supplier(text: str) -> Optional[str]:
    m = re.search(
        r"([\w &\.]+KNAUF[\w &\.]+)",
        text,
        re.IGNORECASE
    )
    if m:
        return m.group(1).strip()
    return None


def extract_supplier_code_knauf(text: str) -> Optional[str]:
    """Extrait le code fournisseur Knauf si présent."""
    # Pour Knauf, il n'y a généralement pas de code fournisseur distinct
    # Le code fournisseur serait généralement dans un champ spécifique
    # Si aucun code n'est trouvé, retourner None plutôt qu'un code produit
    patterns = [
        r"(?:code\s+fournisseur|supplier\s+code|fournisseur\s+code)[\s:]+([A-Z0-9]{3,20})",
        r"KNAUF[\s-]+code[\s:]+([A-Z0-9]{3,10})",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            code = m.group(1).strip()
            # Vérifier que ce n'est pas un code produit (généralement 4-8 chiffres)
            # Les codes fournisseurs sont généralement plus courts ou alphanumériques
            if not re.match(r'^\d{4,8}$', code):
                return code
    return None


def extract_supplier_address_knauf(text: str) -> Optional[str]:
    """Extrait l'adresse complète du fournisseur Knauf."""
    # L'adresse se trouve dans le footer de chaque page
    # Format: "N et B KNAUF & Cie SComm. - Rue du Parc Industriel, 1 - B 4480 Engis Tel: ..."
    # Elle apparaît après "Bank: Deutsche Bank..." et avant "Algemene verkoopsvoorwaarden"
    
    # Chercher la ligne complète avec "Rue du Parc Industriel" dans le footer
    # Pattern: "Rue du Parc Industriel, 1 - B 4480 Engis" ou "Rue du Parc Industriel - B 4480 Engis"
    pattern = r"Rue\s+du\s+Parc\s+Industriel[\s,]*(\d+)?[\s,.-]*B[\s-]*(\d{4})\s+([A-Za-z]+)(?=\s+Tel:|\s+FAX:|\s+RPM:|\s+www\.|$)"
    m = re.search(pattern, text, re.IGNORECASE)
    if m:
        street = "Rue du Parc Industriel"
        number = m.group(1) if m.group(1) else ""
        postal_code = m.group(2)
        city = m.group(3)
        
        # Construire l'adresse complète
        if number:
            address = f"{street}, {number} - B {postal_code} {city}"
        else:
            address = f"{street} - B {postal_code} {city}"
        return address.strip()
    
    # Pattern alternatif: chercher dans la ligne complète du footer
    # "N et B KNAUF & Cie SComm. - Rue du Parc Industriel, 1 - B 4480 Engis"
    pattern2 = r"(?:N\s+et\s+B\s+KNAUF[^-]*-\s*)?Rue\s+du\s+Parc\s+Industriel[\s,]*(\d+)?[\s,.-]*B[\s-]*(\d{4})\s+([A-Za-z]+)"
    m2 = re.search(pattern2, text, re.IGNORECASE)
    if m2:
        street = "Rue du Parc Industriel"
        number = m2.group(1) if m2.group(1) else ""
        postal_code = m2.group(2)
        city = m2.group(3)
        
        if number:
            address = f"{street}, {number} - B {postal_code} {city}"
        else:
            address = f"{street} - B {postal_code} {city}"
        return address.strip()
    
    return None


def extract_supplier_phone_knauf(text: str) -> Optional[str]:
    """Extrait le numéro de téléphone du fournisseur Knauf."""
    # Le téléphone apparaît dans le footer de chaque page
    # Format: "Tel :  04/273.83.11" (peut avoir des espaces après "Tel :")
    # On cherche "Tel:" suivi d'un numéro de téléphone belge (format: 0X/XXX.XX.XX)
    
    # Chercher dans tout le texte (le téléphone peut être dans le footer)
    patterns = [
        r"Tel\s*:\s*(\d{2}[\s./]?\d{1,3}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",  # Format: Tel :  04/273.83.11
        r"Tel[:\s]+(\d{2}[\s./]?\d{1,3}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",  # Format: Tel: 04/273.83.11 (sans espace après :)
        r"T\s*:\s*(\d{2}[\s./]?\d{1,3}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",    # Format: T: 04/273.83.11
        r"(?:\+|00\s*32)\s*(\d{1,2}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2}[\s./]?\d{2})",  # Format international
    ]
    
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            phone = m.group(1).strip()
            # Vérifier que ce n'est pas une date (format DD.MM.YYYY ou DD/MM/YYYY)
            # Les dates ont généralement 4 chiffres pour l'année
            if not re.match(r'^\d{2}[./]\d{2}[./]\d{4}', phone):
                # Normaliser le format: remplacer tous les séparateurs par "/"
                phone = re.sub(r'[\s./]+', '/', phone)
                # Nettoyer les espaces
                phone = phone.replace(' ', '')
                return phone
    
    return None


def extract_supplier_email_knauf(text: str) -> Optional[str]:
    """Extrait l'email du fournisseur Knauf."""
    patterns = [
        r"([a-z0-9._%+-]+@(?:knauf|knaufgroup)[a-z0-9.-]*\.[a-z]{2,})",
        r"([a-z0-9._%+-]+@[a-z0-9.-]+\.be)",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            return m.group(1).lower().strip()
    return None


def extract_supplier_contact_knauf(text: str) -> Optional[str]:
    """Extrait le nom du contact commercial Knauf si présent."""
    # Chercher "Contactpersoon" suivi du nom (format Knauf)
    # Format: "Contactpersoon\n Jolien Matthijs"
    pattern1 = r"Contactpersoon\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)"
    m1 = re.search(pattern1, text, re.IGNORECASE | re.MULTILINE)
    if m1:
        contact = m1.group(1).strip()
        # Vérifier que ce n'est pas un nom d'entreprise
        if len(contact.split()) <= 3 and not any(word.lower() in ['nv', 'bv', 'sprl', 'sa', 'knauf', 'cie', 'scomm'] for word in contact.split()):
            return contact
    
    # Patterns alternatifs
    patterns = [
        r"(?:contact|sales|commercial)[\s:]+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",
        r"([A-Z][a-z]+\s+[A-Z][a-z]+)(?:\s+-\s*(?:sales|commercial|contact))",
    ]
    for pattern in patterns:
        m = re.search(pattern, text, re.IGNORECASE)
        if m:
            contact = m.group(1).strip()
            if len(contact.split()) <= 3 and not any(word.lower() in ['nv', 'bv', 'sprl', 'sa', 'knauf', 'cie', 'scomm'] for word in contact.split()):
                return contact
    return None


def extract_supplier_payment_terms_knauf(text: str) -> Optional[str]:
    """Extrait les conditions de paiement Knauf."""
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


def extract_client_factuurontvanger(text: str) -> Optional[str]:
    """
    Extrait le client à partir du bloc Factuurontvanger ou Opdrachtgever.
    Format attendu: "Opdrachtgever 10170600 Euro Brico sprl" ou similaire.
    Pour les bons de livraison, cherche aussi directement le nom du client.
    """
    lines = [l.strip() for l in text.splitlines() if l.strip()]

    # --- LOCALISER LE POINT D'ENTRÉE ---
    idx = None
    for i, line in enumerate(lines):
        l = line.lower()
        if "factuurontvanger" in l or "opdrachtgever" in l:
            idx = i
            break

    # Si pas trouvé, chercher pour les bons de livraison (le client peut être directement après l'adresse du fournisseur)
    if idx is None:
        # PRIORITÉ ABSOLUE : Chercher directement "Euro Brico" dans le texte AVANT tout le reste
        for i, line in enumerate(lines):
            if "euro brico" in line.lower():
                # Extraire le nom complet (peut être "Euro Brico sprl Verzendpunt Levering Pagina")
                client_name = line.strip()
                # Nettoyer : enlever tout après "sprl" ou "Euro Brico sprl"
                if "sprl" in client_name.lower():
                    # Extraire "Euro Brico sprl" (peut être suivi de "Verzendpunt", "Levering", etc.)
                    sprl_match = re.search(r'(Euro\s+Brico\s+sprl)', client_name, re.IGNORECASE)
                    if sprl_match:
                        client_name = sprl_match.group(1)
                    else:
                        client_name = re.split(r'\s+sprl\s+', client_name, flags=re.IGNORECASE)[0] + " sprl"
                else:
                    # Sinon, enlever les mots après "Euro Brico"
                    client_name = re.split(r'\s+(?:Verzendpunt|Levering|Pagina|Shipping)', client_name, flags=re.IGNORECASE)[0].strip()
                # Nettoyer aussi les adresses
                client_name = re.split(r'\s+\d{4}\s+[A-Z]', client_name)[0].strip()
                client_name = re.split(r'\s+Frans Baetenstraat', client_name)[0].strip()
                client_name = re.split(r'\s+Antwerpen', client_name)[0].strip()
                
                # Chercher un numéro de client dans les lignes suivantes (ligne avec "Uw klantnummer")
                client_num = None
                for j in range(i, min(i + 10, len(lines))):
                    if "klantnummer" in lines[j].lower() or "uw klantnummer" in lines[j].lower():
                        # La ligne suivante devrait contenir le numéro
                        if j + 1 < len(lines):
                            num_match = re.search(r'\b(10\d{6}|\d{8})\b', lines[j + 1])
                            if num_match:
                                client_num = num_match.group(1)
                                break
                        # Ou le numéro peut être dans la même ligne
                        num_match = re.search(r'\b(10\d{6}|\d{8})\b', lines[j])
                        if num_match:
                            client_num = num_match.group(1)
                            break
                
                # Si pas trouvé, chercher dans les lignes autour de "Euro Brico"
                if not client_num:
                    # Chercher dans les 5 lignes avant et après
                    for j in range(max(0, i - 5), min(i + 5, len(lines))):
                        num_match = re.search(r'\b(10\d{6}|\d{8})\b', lines[j])
                        if num_match:
                            # Vérifier que ce n'est pas un EAN (13 chiffres) ou autre numéro
                            num = num_match.group(1)
                            if len(num) == 8 and num.startswith('10'):
                                client_num = num
                                break
                
                if client_num and client_name and len(client_name) >= 3:
                    return f"{client_num} {client_name}".strip()
                elif client_name and len(client_name) >= 3:
                    return client_name.strip()
        
        # Si toujours pas trouvé, chercher le nom du client dans les premières lignes (avant "Levering")
        # Le client est généralement après l'adresse Knauf et avant "Verzendpunt"
        # MAIS on cherche d'abord "Euro Brico" explicitement
        for i, line in enumerate(lines):
            # Chercher après "Engis" (adresse Knauf)
            if "engis" in line.lower():
                # Parcourir les lignes suivantes jusqu'à "Verzendpunt" ou "Levering"
                for j in range(i + 1, min(i + 10, len(lines))):
                    candidate = lines[j].strip()
                    # Arrêter si on trouve "Verzendpunt" ou "Levering" (mais pas si c'est "Euro Brico")
                    if ("verzendpunt" in candidate.lower() or "shipping point" in candidate.lower()) and "euro brico" not in candidate.lower():
                        break
                    # Ignorer les lignes vides, adresses, etc.
                    if not candidate or re.match(r'^\d{4}\s+[A-Z]', candidate):
                        continue
                    # PRIORITÉ : chercher "Euro Brico" explicitement
                    if "euro brico" in candidate.lower():
                        client_name = re.split(r'\s+(?:Verzendpunt|Levering|Pagina|Shipping)', candidate, flags=re.IGNORECASE)[0].strip()
                        if "sprl" in client_name.lower():
                            client_name = re.split(r'\s+sprl\s+', client_name, flags=re.IGNORECASE)[0] + " sprl"
                        # Chercher un numéro de client
                        num_match = re.search(r'\b(10\d{6}|\d{8})\b', candidate)
                        if not num_match and j + 1 < len(lines):
                            num_match = re.search(r'\b(10\d{6}|\d{8})\b', lines[j + 1])
                        if num_match:
                            return f"{num_match.group(1)} {client_name}".strip()
                        else:
                            return client_name.strip()
                    # Chercher un nom d'entreprise (mais ignorer "Levering" et autres mots-clés)
                    if re.search(r'[A-Za-z]{3,}', candidate) and not any(kw in candidate.lower() for kw in [
                        "knauf", "engis", "tva", "bank", "deutsche", "rekening", "iban", "bic", "swift", "levering", "pagina", "verzendpunt", "shipping point"
                    ]) and candidate.lower().strip() != "levering":
                        # Extraire le nom
                        client_name = re.split(r'\s+\d{4}\s+[A-Z]', candidate)[0].strip()
                        client_name = re.split(r'\s+Frans Baetenstraat', client_name)[0].strip()
                        client_name = re.split(r'\s+Antwerpen', client_name)[0].strip()
                        # Ignorer "Levering" et autres mots-clés (vérifier que ce n'est pas juste "Levering")
                        if any(kw in client_name.lower() for kw in ["levering", "pagina", "verzendpunt", "shipping point"]) or client_name.lower().strip() == "levering":
                            continue
                        if client_name and len(client_name) >= 3:
                            # Chercher un numéro de client dans cette ligne ou la suivante
                            num_match = re.search(r'\b(10\d{6}|\d{8})\b', candidate)
                            if not num_match and j + 1 < len(lines):
                                num_match = re.search(r'\b(10\d{6}|\d{8})\b', lines[j + 1])
                            if num_match:
                                return f"{num_match.group(1)} {client_name}".strip()
                            else:
                                return client_name.strip()

    if idx is None:
        return None

    # --- ESSAYER D'EXTRAIRE DIRECTEMENT DE LA LIGNE ---
    line = lines[idx]
    
    # Pattern: "Opdrachtgever 10170600 Euro Brico sprl" (tout sur la même ligne)
    match = re.search(r'(?:Opdrachtgever|Factuurontvanger)\s+(\d{8})\s+(.+?)(?:\s*$|\s+\d{4}\s+[A-Z]|\s+Frans|\s+Antwerpen)', line, re.IGNORECASE)
    if match:
        client_num = match.group(1)
        client_name = match.group(2).strip()
        # Nettoyer le nom (enlever l'adresse si présente)
        client_name = re.split(r'\s+\d{4}\s+[A-Z]', client_name)[0].strip()
        client_name = re.split(r'\s+Frans Baetenstraat', client_name)[0].strip()
        client_name = re.split(r'\s+Antwerpen', client_name)[0].strip()
        if client_name:
            return f"{client_num} {client_name}".strip()
        else:
            return client_num

    # --- RÉCUPÉRER LES LIGNES SUIVANTES (jusqu'à 6 lignes) ---
    block = lines[idx:idx+7]  # Inclure la ligne actuelle et les 6 suivantes
    
    # Chercher le numéro de client (8 chiffres, souvent commence par 10)
    client_num = None
    client_name = None
    
    for l in block:
        low = l.lower()
        
        # Ignorer les lignes de métadonnées
        if any(kw in low for kw in [
            "datum", "btw", "tel", "mail", "email", "contactpersoon", "contact",
            "tva", "be -", "rpm", "rekening", "iban", "bic", "swift"
        ]):
            continue
        # Ignorer les numéros TVA
        if re.match(r'^TVA\s*:?\s*-?\s*BE\s*-?\s*\d+', l, re.IGNORECASE):
            continue
        
        # Chercher le numéro de client (8 chiffres, souvent 10xxxxxx)
        if not client_num:
            num_match = re.search(r'\b(10\d{6}|\d{8})\b', l)
            if num_match:
                client_num = num_match.group(1)
        
        # Chercher le nom de l'entreprise (doit contenir des lettres)
        if not client_name and re.search(r'[A-Za-z]{3,}', l):
            # Ignorer si c'est juste une adresse (code postal + ville)
            if re.match(r'^\d{4}\s+[A-Z]', l):
                continue
            # Ignorer si c'est juste une rue (nombre + nom de rue simple)
            if re.match(r'^\d+\s+[A-Z][a-z]+$', l):
                continue
            # Prendre la ligne qui contient le nom d'entreprise
            candidate = l.strip()
            # Enlever le préfixe "Opdrachtgever" ou "Factuurontvanger" si présent
            candidate = re.sub(r'^(?:Opdrachtgever|Factuurontvanger)\s*:?\s*', '', candidate, flags=re.IGNORECASE)
            # Enlever le numéro de client si présent au début
            candidate = re.sub(r'^\d{8}\s+', '', candidate)
            # Enlever l'adresse si présente
            candidate = re.split(r'\s+\d{4}\s+[A-Z]', candidate)[0].strip()
            candidate = re.split(r'\s+Frans Baetenstraat', candidate)[0].strip()
            candidate = re.split(r'\s+Antwerpen', candidate)[0].strip()
            
            # Vérifier que c'est un nom valide (pas juste des chiffres, pas une adresse)
            if len(candidate) >= 3 and re.search(r'[A-Za-z]{2,}', candidate):
                # Ignorer les mots-clés indésirables
                if not any(kw in candidate.lower() for kw in [
                    "faktuur", "factuur", "nummer", "klantnummer", "contactpersoon",
                    "tel:", "email:", "datum", "btw", "afleveradres"
                ]):
                    client_name = candidate
                    break

    # --- CONSTRUCTION ---
    # Ne jamais retourner "Levering" comme nom de client
    if client_name and client_name.lower().strip() == "levering":
        client_name = None
    
    if client_num and client_name:
        return f"{client_num} {client_name}".strip()
    elif client_name:
        return client_name.strip()
    elif client_num:
        return client_num
    else:
        return None











# ------------------------------------------------------------
# PRIX : à partir de la ligne quantité
# ------------------------------------------------------------

def extract_prices_from_qty_line(line: str) -> Tuple[Optional[float], Optional[float]]:
    """
    Sur une ligne du type :
      '10 PAC 10 PAC 3,41 /PAC 3,17 /PAC 31,70'
      '4 PC 20 KG 2,48 /KG 2,36 /KG 47,20'

    On prend :
      - unit_price = avant-dernier nombre (3,17 ou 2,36)
      - line_total = dernier nombre (31,70 ou 47,20)
    """
    nums = re.findall(r"(\d+[.,]\d{2})", line)
    if len(nums) >= 2:
        unit_price = float(nums[-2].replace(",", "."))
    elif len(nums) == 1:
        unit_price = float(nums[0].replace(",", "."))

        line_total = float(nums[-1].replace(",", "."))
        return unit_price, line_total
    return None, None


def refine_unit_price_with_block(block: List[str], current_unit_price: Optional[float]) -> Optional[float]:
    """
    Si on trouve 'Onvoorwaardelijke nettoprijs X,XX EUR' dans le bloc,
    on préfère ce X,XX comme prix net unitaire.
    """
    for l in block:
        m = re.search(r"onvoorwaardelijke nettoprijs\s+(\d+[.,]\d{2})\s*eur", l, re.IGNORECASE)
        if m:
            return float(m.group(1).replace(",", "."))
    return current_unit_price


# ------------------------------------------------------------
# GROUPING LINES (XY)
# ------------------------------------------------------------

def build_lines(words: List[Tuple]) -> List[List[Tuple]]:
    """
    Regroupe les mots PyMuPDF en lignes logiques à partir de Y.
    """
    temp = []
    for w in words:
        try:
            x0, y0, x1, y1, txt = w[0], w[1], w[2], w[3], w[4]
        except Exception:
            continue
        yc = (y0 + y1) / 2.0
        temp.append((x0, yc, txt))

    temp.sort(key=lambda t: (t[1], t[0]))

    lines: List[List[Tuple[float, str]]] = []
    current_line: List[Tuple[float, str]] = []
    current_y: Optional[float] = None
    tol = 2.5

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
# DÉTECTION FORMAT POS
# ------------------------------------------------------------

def is_knauf_pos(text: str) -> bool:
    """
    Détecte si le document est au format POS (Position) Knauf.
    Format avec colonnes "Pos. | Artikel | Hoeveelheid | Brutoprijs | Nettoprijs"
    
    IMPORTANT: On exige TOUJOURS la présence des en-têtes POS pour éviter les faux positifs
    avec les factures non-POS qui peuvent avoir des patterns similaires.
    """
    t = text.lower()
    
    # Critère 1: Présence des en-têtes de colonnes (très spécifique au format POS)
    has_headers = ("brutoprijs" in t and "nettoprijs" in t)
    
    # Critère 2: Présence de "Pos." et "Artikel" dans les en-têtes (indicateur fort)
    has_pos_artikel = ("pos." in t and "artikel" in t and "hoeveelheid" in t)
    
    # Critère 3: Pattern de ligne produit POS (format standard)
    # Format: "10 545753 8 ST 4,73 /1 ST ..."
    has_pos_pattern = bool(re.search(
        r"^\d{1,3}\s+\d{4,6}\s+\d+\s+(st|pac|pc|kg|pak)\s+[\d,]", 
        text, 
        re.MULTILINE
    ))
    
    # Critère 4: Pattern de ligne produit POS (format compact avec SKU seul)
    # Format: "90 136725" suivi de description + qty
    has_sku_only_pattern = bool(re.search(
        r"^\d{1,3}\s+\d{4,6}\s*$", 
        text, 
        re.MULTILINE
    ))
    
    # Critère 5: Pattern de ligne POS avec prix (plus flexible)
    # Format: "10 545753 8 ST 4,73 /1 ST 7,00-% 4,40 /1 ST 35,20"
    has_pos_price_pattern = bool(re.search(
        r"\d{1,3}\s+\d{4,6}\s+\d+\s+(st|pac|pc|kg|pak)\s+\d+[.,]\d+\s*/\s*1\s+(st|pac|pc|kg|pak)", 
        text, 
        re.IGNORECASE
    ))
    
    # DEBUG
    print(f"[KNAUF DEBUG] is_knauf_pos: has_headers={has_headers}, has_pos_artikel={has_pos_artikel}, has_pos_pattern={has_pos_pattern}, has_sku_only_pattern={has_sku_only_pattern}, has_pos_price_pattern={has_pos_price_pattern}")
    
    # Détection STRICTE: On exige TOUJOURS la présence des en-têtes POS
    # (soit brutoprijs/nettoprijs, soit pos/artikel/hoeveelheid)
    # pour éviter de confondre avec les factures non-POS
    result = (
        # Cas 1: En-têtes brutoprijs/nettoprijs + pattern produit
        (has_headers and (has_pos_pattern or has_sku_only_pattern or has_pos_price_pattern)) or
        # Cas 2: En-têtes brutoprijs/nettoprijs + pos/artikel/hoeveelheid
        (has_headers and has_pos_artikel) or
        # Cas 3: En-têtes pos/artikel/hoeveelheid + pattern produit
        (has_pos_artikel and (has_pos_pattern or has_sku_only_pattern or has_pos_price_pattern)) or
        # Cas 4: Pattern prix POS très spécifique (même sans en-têtes explicites)
        (has_pos_price_pattern and has_pos_pattern)
    )
    
    print(f"[KNAUF DEBUG] is_knauf_pos result={result}")
    return result


# ------------------------------------------------------------
# PARSE FORMAT POS (Facture avec colonnes Pos/Artikel)
# ------------------------------------------------------------

def parse_knauf_pos(pages: List[Dict]) -> List[Dict]:
    """
    Parse les factures Knauf au format POS.
    Format: Ligne "Pos SKU qty unit prix..." suivie de "Description" puis "EAN-nr.: ..."
    Exemple:
    - Ligne 1: "10 545753 8 ST 4,73 /1 ST 7,00-% 4,40 /1 ST 35,20"
    - Ligne 2: "Flex-voegmortel beige 2kg (360)"
    - Ligne 3: "EAN-nr.: 5413503590100"
    """
    items: List[Dict] = []
    
    # Construire une liste globale de toutes les lignes
    all_lines = []
    for page in pages:
        words = page.get("words", [])
        if not words:
            continue
        lines = build_lines(words)
        text_lines = [" ".join(t for _, t in line).strip() for line in lines]
        all_lines.extend(text_lines)
    
    i = 0
    while i < len(all_lines):
        line = all_lines[i]
        
        # Détection ligne produit POS: "Pos SKU qty unit prix..."
        # Format 1: "10 545753 8 ST 4,73 /1 ST 7,00-% 4,40 /1 ST 35,20"
        # Format 2: "10 545753 8 ST 240 KG ..." (st suivi de kg)
        # Format 3: "10 545753 8 ST 2,4 TONNE ..." (st suivi de tonne)
        # Format 4: "90 136725" (SKU seul, description + qty sur ligne suivante)
        m = re.match(
            r"^\d{1,3}\s+(\d{4,6})\s+(\d+(?:[.,]\d+)?)\s+(ST|PAC|PC|KG|PAK)\s+(.*)$",
            line,
            re.IGNORECASE
        )
        
        # Cas alternatif: SKU seul sur une ligne (format compact)
        if not m:
            m_sku_only = re.match(r"^\d{1,3}\s+(\d{4,6})\s*$", line)
            if m_sku_only and i + 1 < len(all_lines):
                # SKU trouvé seul, chercher description + qty sur ligne suivante
                next_line = all_lines[i + 1].strip()
                # Format: "Snelgips 2 kg (96) 18 ST 1,95 /1 ST ..."
                desc_qty_match = re.match(
                    r"^(.+?)\s+(\d+)\s+(ST|PAC|PC|KG|PAK)\s+(.*)$",
                    next_line,
                    re.IGNORECASE
                )
                if desc_qty_match:
                    sku = m_sku_only.group(1).lstrip("0") or m_sku_only.group(1)
                    desc = desc_qty_match.group(1).strip()
                    qty_st = float(desc_qty_match.group(2))
                    unit_st = desc_qty_match.group(3).upper()
                    rest = desc_qty_match.group(4)  # Le reste avec les prix
                    
                    # Gestion des différentes unités (même logique que format standard)
                    qty = qty_st
                    unit = unit_st
                    
                    # Vérifier si on a une quantité en "KG" juste après "ST" dans rest
                    # Format: "4 ST 20,000 KG ..." → dans rest on a "20,000 KG ..."
                    
                    # NOUVEAU: Vérifier l'unité de PRIX pour décider si on utilise KG
                    price_unit_match = re.search(r"/\s*1\s+(ST|PAC|PC|KG|PAK|TO|TONNE)\b", rest, re.IGNORECASE)
                    price_unit = price_unit_match.group(1).upper() if price_unit_match else None
                    
                    kg_match = re.search(r"^(\d+(?:[.,]\d+)?)\s+KG\b", rest, re.IGNORECASE)
                    if kg_match and price_unit == "KG":
                        qty_kg_str = kg_match.group(1).replace(",", ".")
                        qty = float(qty_kg_str)
                        unit = "KG"
                    else:
                        # Vérifier si on a une quantité en "TO" ou "TONNE" juste après "ST" dans rest
                        # Format: "12 ST 0,120 TO ..." → dans rest on a "0,120 TO ..."
                        tonne_match = re.search(r"^(\d+[.,]\d+)\s+(TO|TONNE)\b", rest, re.IGNORECASE)
                        if tonne_match:
                            qty_tonne_str = tonne_match.group(1).replace(",", ".")
                            qty = float(qty_tonne_str)
                            unit = "TONNE"  # Normaliser en "TONNE" même si c'était "TO"
                    
                    # Nettoyage description
                    desc = re.sub(r"^\d{1,4}\s+", "", desc).strip()
                    desc = " ".join(desc.split())
                    desc = desc.replace("|", "").strip()
                    
                    # Extraction des prix selon l'unité détectée
                    # Si unité = TONNE, chercher aussi "TO" (abréviation)
                    if unit == "TONNE":
                        unit_price_matches = list(re.finditer(
                            rf"(\d+[.,]\d{{2}})\s*/\s*1\s+(TO|TONNE)\b",
                            rest,
                            re.IGNORECASE
                        ))
                    else:
                        unit_price_matches = list(re.finditer(
                            rf"(\d+[.,]\d{{2}})\s*/\s*1\s+{re.escape(unit)}\b",
                            rest,
                            re.IGNORECASE
                        ))
                    
                    if unit_price_matches:
                        unit_price = float(unit_price_matches[-1].group(1).replace(",", "."))
                    else:
                        all_unit_price_matches = list(re.finditer(r"(\d+[.,]\d{2})\s*/\s*1\s+(ST|PAC|PC|KG|PAK|TONNE|TO)", rest, re.IGNORECASE))
                        if all_unit_price_matches:
                            unit_price = float(all_unit_price_matches[-1].group(1).replace(",", "."))
                        else:
                            nums = re.findall(r"(\d+[.,]\d{2})", rest)
                            if len(nums) >= 2:
                                unit_price = float(nums[-2].replace(",", "."))
                    
                    # Calcul du total selon l'unité détectée
                    nums = re.findall(r"(\d+[.,]\d{2})", rest)
                    line_total = None
                    if nums:
                        line_total = float(nums[-1].replace(",", "."))
                    
                    # Recalculer si nécessaire
                    if unit_price is not None and qty is not None:
                        calculated_total = round(unit_price * qty, 2)
                        if line_total is None or abs(line_total - calculated_total) > 0.01:
                            line_total = calculated_total
                    
                    # Chercher EAN dans les lignes suivantes
                    ean = None
                    for j in range(i + 2, min(i + 5, len(all_lines))):
                        ean_match = re.search(r"ean[- ]?nr\.?\s*:\s*(\d{8,14})", all_lines[j], re.IGNORECASE)
                        if ean_match:
                            ean = ean_match.group(1)
                            break
                    
                    # Ajouter l'item
                    if desc and "palet" not in desc.lower() and is_desc_candidate(desc) and not is_excluded(desc):
                        if not any(kw in desc.lower() for kw in [
                            "frans baetenstraat", "antwerpen", "tel.", "shipping point",
                            "verzendpunt", "nummerplaat", "transporteur", "pos.", "artikel nr",
                            "beschrijving", "geleverde hoeveelheid", "gewicht", "deutsche bank",
                            "temp.conditie", "bank", "rekening", "iban", "bic", "swift", "totaal"
                        ]):
                            items.append({
                                "sku": sku,
                                "ean": ean,
                                "description": desc,
                                "qty": qty,
                                "unit": unit,
                                "unit_price": unit_price,
                                "line_total": line_total
                            })
                    i += 3  # SKU + description+qty+prix + EAN
                    continue
        
        if not m:
            i += 1
            continue
        
        sku = m.group(1).lstrip("0") or m.group(1)
        qty_st = float(m.group(2).replace(",", "."))
        unit_st = m.group(3).upper()
        rest = m.group(4)  # Le reste de la ligne avec les prix
        
        # Gestion des différentes unités selon les spécifications :
        # Format exemple: "120 67941 4 ST 20,000 KG 2,44 /1 KG 5,00-% 2,32 /1 KG 46,40"
        # - Si "ST" seul → unité = ST, qty = qty en ST
        # - Si "ST" suivi de "KG" → unité = KG, qty = qty en KG (ex: "4 ST 20,000 KG")
        # - Si "ST" suivi de quantité en tonne → unité = TONNE, qty = qty en tonne
        
        qty = qty_st
        unit = unit_st
        
        # Vérifier si on a une quantité en "KG" juste après "ST" dans le reste de la ligne
        # Format: "4 ST 20,000 KG ..." → dans rest on a "20,000 KG ..."
        # Pattern: nombre(s) KG (peut être au début de rest)
        
        # NOUVEAU: Vérifier l'unité de PRIX pour décider si on utilise KG
        price_unit_match = re.search(r"/\s*1\s+(ST|PAC|PC|KG|PAK|TO|TONNE)\b", rest, re.IGNORECASE)
        price_unit = price_unit_match.group(1).upper() if price_unit_match else None
        
        kg_match = re.search(r"^(\d+(?:[.,]\d+)?)\s+KG\b", rest, re.IGNORECASE)
        if kg_match and price_unit == "KG":
            # Cas KG : on trouve une quantité en KG juste après ST ET le prix est au KG
            qty_kg_str = kg_match.group(1).replace(",", ".")
            qty = float(qty_kg_str)
            unit = "KG"
        else:
            # Vérifier si on a une quantité en "TO" ou "TONNE" juste après "ST" dans le reste de la ligne
            # Format: "12 ST 0,120 TO ..." → dans rest on a "0,120 TO ..."
            # Pattern: nombre(s) TO ou TONNE (peut être au début de rest)
            tonne_match = re.search(r"^(\d+[.,]\d+)\s+(TO|TONNE)\b", rest, re.IGNORECASE)
            if tonne_match:
                # Cas TONNE : on trouve une quantité en TO/TONNE juste après ST
                qty_tonne_str = tonne_match.group(1).replace(",", ".")
                qty = float(qty_tonne_str)
                unit = "TONNE"  # Normaliser en "TONNE" même si c'était "TO"
            # Sinon, cas ST seul : on garde qty_st et unit_st
        
        # Extraction des prix selon l'unité détectée
        # Format: "4,73 /1 ST 7,00-% 4,40 /1 ST 35,20" ou "4,73 /1 KG ..." ou "4,73 /1 TONNE ..."
        # Structure: prix_brut /1 unité remise prix_net /1 unité total
        # On veut: unit_price = 4.40 (prix NET après remise), line_total = 35.20
        
        # Chercher TOUS les prix unitaires correspondant à l'unité de quantité détectée
        # Pattern: "X,XX /1 ST" ou "X,XX /1 KG" ou "X,XX /1 TONNE" ou "X,XX /1 TO" etc.
        # Si unité = TONNE, chercher aussi "TO" (abréviation)
        if unit == "TONNE":
            # Chercher "TO" ou "TONNE"
            unit_price_matches = list(re.finditer(
                rf"(\d+[.,]\d{{2}})\s*/\s*1\s+(TO|TONNE)\b",
                rest,
                re.IGNORECASE
            ))
        else:
            unit_price_matches = list(re.finditer(
                rf"(\d+[.,]\d{{2}})\s*/\s*1\s+{re.escape(unit)}\b",
                rest,
                re.IGNORECASE
            ))
        
        if unit_price_matches:
            # Prendre le DERNIER prix unitaire (prix net après remise)
            unit_price = float(unit_price_matches[-1].group(1).replace(",", "."))
        else:
            # Si pas trouvé avec l'unité exacte, chercher tous les prix unitaires
            all_unit_price_matches = list(re.finditer(r"(\d+[.,]\d{2})\s*/\s*1\s+(ST|PAC|PC|KG|PAK|TONNE|TO)", rest, re.IGNORECASE))
            if all_unit_price_matches:
                # Prendre le dernier prix unitaire (prix net)
                unit_price = float(all_unit_price_matches[-1].group(1).replace(",", "."))
            else:
                # Fallback: prendre l'avant-dernier nombre (généralement le prix unitaire net)
                nums = re.findall(r"(\d+[.,]\d{2})", rest)
                if len(nums) >= 2:
                    unit_price = float(nums[-2].replace(",", "."))
        
        # Calcul du total selon l'unité détectée
        # Le total peut être le dernier nombre dans la ligne, OU calculé = PU * Qty
        nums = re.findall(r"(\d+[.,]\d{2})", rest)
        line_total = None
        if nums:
            line_total = float(nums[-1].replace(",", "."))
        
        # Si on a un prix unitaire et une quantité, vérifier que le total correspond
        # Sinon, recalculer selon l'unité détectée
        if unit_price is not None and qty is not None:
            calculated_total = round(unit_price * qty, 2)
            # Si le total extrait ne correspond pas au calcul, utiliser le calcul
            if line_total is None or abs(line_total - calculated_total) > 0.01:
                line_total = calculated_total
        
        # Chercher la description
        desc = None
        
        # Cas 1: Description sur la ligne suivante (format standard ou compact)
        if i + 1 < len(all_lines):
            next_line = all_lines[i + 1].strip()
            # Vérifier si c'est une description (pas une ligne EAN, pas une ligne produit)
            if not re.match(r"^\d{1,3}\s+\d{4,6}\s+\d+\s+(ST|PAC|PC|KG|PAK)", next_line, re.IGNORECASE) and \
               not re.search(r"ean[- ]?nr\.?\s*:", next_line, re.IGNORECASE) and \
               not re.match(r"^\d{1,3}\s*$", next_line):  # Pas juste un numéro de position
                # Vérifier si la ligne suivante contient aussi une quantité suivie de prix (format compact)
                # Format: "Snelgips 2 kg (96) 18 ST 1,95 /1 ST ..."
                qty_price_match = re.search(r"\s+(\d+)\s+(ST|PAC|PC|KG|PAK)\s+[\d,]", next_line, re.IGNORECASE)
                if qty_price_match:
                    # Format compact: description et quantité sur la même ligne, puis prix
                    # Extraire la description (tout avant la quantité)
                    desc = next_line[:qty_price_match.start()].strip()
                    # Optionnel: mettre à jour qty et unit depuis la ligne suivante si on veut
                    # Mais on garde ceux de la ligne principale pour cohérence
                else:
                    # Format standard: description seule (pas de quantité ni prix)
                    desc = next_line
        
        # Cas 2: Description peut être sur la même ligne après le SKU (format très compact)
        # Format: "90 136725 Snelgips 2 kg (96) 18 ST ..."
        if not desc:
            # Chercher une description après le SKU dans la ligne actuelle
            desc_match = re.search(r"^\d{1,3}\s+\d{4,6}\s+([A-Za-z].*?)\s+\d+\s+(ST|PAC|PC|KG|PAK)", line, re.IGNORECASE)
            if desc_match:
                desc = desc_match.group(1).strip()
        
        # Nettoyage de la description
        if desc:
            # Enlever le numéro de position au début si présent
            desc = re.sub(r"^\d{1,4}\s+", "", desc).strip()
            # Enlever les espaces multiples
            desc = " ".join(desc.split())
            # Enlever les caractères de séparation de tableau (|)
            desc = desc.replace("|", "").strip()
        
        # Chercher EAN dans les lignes suivantes (peut être ligne i+1, i+2 ou i+3)
        ean = None
        for j in range(i + 1, min(i + 4, len(all_lines))):
            ean_match = re.search(r"ean[- ]?nr\.?\s*:\s*(\d{8,14})", all_lines[j], re.IGNORECASE)
            if ean_match:
                ean = ean_match.group(1)
                break
        
        # Exclure les palettes et valider la description
        if desc and "palet" not in desc.lower() and is_desc_candidate(desc) and not is_excluded(desc):
            if not any(kw in desc.lower() for kw in [
                "frans baetenstraat", "antwerpen", "tel.", "shipping point",
                "verzendpunt", "nummerplaat", "transporteur", "pos.", "artikel nr",
                "beschrijving", "geleverde hoeveelheid", "gewicht", "deutsche bank",
                "temp.conditie", "bank", "rekening", "iban", "bic", "swift", "totaal"
            ]):
                items.append({
                    "sku": sku,
                    "ean": ean,
                    "description": desc,
                    "qty": qty,
                    "unit": unit,
                    "unit_price": unit_price,
                    "line_total": line_total
                })
        
        # Avancer: si description trouvée sur ligne suivante, sauter 2-3 lignes
        # Sinon, avancer d'une seule ligne
        if desc and i + 1 < len(all_lines) and desc in all_lines[i + 1]:
            # Description trouvée ligne suivante, sauter description + EAN
            i += 3
        else:
            i += 1
    
    return items


# ------------------------------------------------------------
# PARSE BON DE LIVRAISON (Leveringsbevestiging)
# ------------------------------------------------------------

def parse_bon_livraison(pages: List[Dict]) -> List[Dict]:
    """
    Parse les bons de livraison Knauf.
    Format: Ligne "Pos SKU EAN" suivie de "Description quantité unit" sur la ligne suivante (peut être sur page suivante).
    Accepte aussi "Pos SKU" sans EAN, ou étalé sur plusieurs lignes.
    """
    items: List[Dict] = []
    
    # Construire une liste globale de toutes les lignes avec leur index de page
    global_lines = []  # Liste de tuples (ligne, page_idx)
    for page_idx, page in enumerate(pages):
        words = page.get("words", [])
        if not words:
            continue
        lines = build_lines(words)
        text_lines = [" ".join(t for _, t in line).strip() for line in lines]
        for line in text_lines:
            global_lines.append((line, page_idx))
    
    # Regex pour Pos/SKU/EAN sur une seule ligne (EAN optionnel)
    # Pos: 1-3 digits
    # SKU: 3-10 digits (élargi de 4-6)
    # EAN: 8-14 digits (optionnel)
    POS_RE = re.compile(r"^(\d{1,3})\s+(\d{3,10})(?:\s+(\d{8,14}))?\s*$")
    
    # Regex pour Pos seul (format multi-lignes)
    POS_ONLY_RE = re.compile(r"^(\d{1,3})\s*$")
    
    # Regex pour SKU seul (format multi-lignes) - élargi à 3-15 chars, accepte points/espaces
    SKU_ONLY_RE = re.compile(r"^([\d\s.]{3,15})\s*$")
    
    # Regex pour SKU + EAN (format multi-lignes sans Pos)
    SKU_EAN_RE = re.compile(r"^([\d\s.]{3,15})\s+(\d{8,14})\s*$")
    
    # Regex pour EAN seul (format multi-lignes)
    EAN_ONLY_RE = re.compile(r"^(\d{8,14})\s*$")
    
    # Regex pour quantité en fin de ligne (accepter aussi les décimales)
    QTY_END_RE = re.compile(r"\s+(\d+(?:[.,]\d+)?)\s+(ST|PAC|PC|KG|PAK)\s*$", re.IGNORECASE)
    
    # Traiter toutes les lignes ensemble
    i = 0
    while i < len(global_lines):
        line, _ = global_lines[i]
        
        # DEBUG LOGGING
        print(f"[KNAUF BL DEBUG] Line {i}: '{line.strip()}'")
        
        sku = None
        ean = None
        start_idx = i
        
        # Cas 1: "Pos SKU [EAN]" sur une ligne
        pos_match = POS_RE.match(line)
        
        # Cas 2: Multi-lignes
        is_multi_line = False
        multi_line_offset = 1  # Par défaut, on commence à la ligne suivante
        
        if pos_match:
            # Format sur une seule ligne: "Pos SKU [EAN]"
            sku_raw = pos_match.group(2)
            sku = sku_raw.replace(" ", "").replace(".", "").lstrip("0") or sku_raw
            ean = pos_match.group(3) # Peut être None
            print(f"[KNAUF BL DEBUG] -> Match POS_RE: SKU={sku}, EAN={ean}")
            
            # Si EAN est manquant, vérifier si la ligne suivante est un EAN
            if not ean and i + 1 < len(global_lines):
                next_line, _ = global_lines[i + 1]
                ean_match = EAN_ONLY_RE.match(next_line)
                if ean_match:
                    ean = ean_match.group(1)
                    is_multi_line = True
                    multi_line_offset = 2 # Sauter Pos+SKU (ligne i) et EAN (ligne i+1) -> start search at i+2
            
            # Vérifier que ce n'est pas un faux positif (Pos trop grand ?)
            # Rien à faire de spécial ici
        
        elif POS_ONLY_RE.match(line) and i + 1 < len(global_lines):
            # Format sur plusieurs lignes: "Pos" puis "SKU..."
            pos_num = POS_ONLY_RE.match(line).group(1)
            next_line, _ = global_lines[i + 1]
            print(f"[KNAUF BL DEBUG] -> Match POS_ONLY_RE (Pos={pos_num}). Next line: '{next_line.strip()}'")
            
            # Sous-cas 2a: Ligne suivante = "SKU EAN"
            sku_ean_match = SKU_EAN_RE.match(next_line)
            if sku_ean_match:
                sku_raw = sku_ean_match.group(1)
                sku = sku_raw.replace(" ", "").replace(".", "").lstrip("0") or sku_raw
                ean = sku_ean_match.group(2)
                print(f"[KNAUF BL DEBUG] -> Next line matches SKU_EAN_RE: SKU={sku}, EAN={ean}")
                # Avancer pour sauter la ligne SKU
                # Note: 'i' sera avancé plus loin, ici on note juste qu'on a consommé une ligne de plus pour le header
                # Mais attention, la boucle de recherche de description démarre à start_idx + delta
                # Ici start_idx = i (ligne Pos), donc la description commence après la ligne SKU (i+2)
                is_multi_line = True
                multi_line_offset = 2 # Sauter Pos (0) et SKU+EAN (1) -> start search at i+2
            
            # Sous-cas 2b: Ligne suivante = "SKU" (EAN peut être ligne suivante ou absent)
            elif SKU_ONLY_RE.match(next_line):
                sku_raw = SKU_ONLY_RE.match(next_line).group(1)
                sku = sku_raw.replace(" ", "").replace(".", "").lstrip("0") or sku_raw
                print(f"[KNAUF BL DEBUG] -> Next line matches SKU_ONLY_RE: SKU={sku}")
                multi_line_offset = 2 # Sauter Pos et SKU -> start search at i+2
                
                # Vérifier si EAN sur ligne i+2
                if i + 2 < len(global_lines):
                    next_next_line, _ = global_lines[i + 2]
                    if EAN_ONLY_RE.match(next_next_line):
                        ean = EAN_ONLY_RE.match(next_next_line).group(1)
                        multi_line_offset = 3 # Sauter Pos, SKU, EAN -> start search at i+3
                
                is_multi_line = True
            
        if not sku:
            i += 1
            continue
        
        # Chercher la description + quantité dans les lignes suivantes
        desc = None
        qty = None
        unit = None
        product_found = False  # Flag pour indiquer qu'on a trouvé un produit
        
        # Déterminer où commencer la recherche de description
        if is_multi_line:
            j = i + multi_line_offset
        else:
            j = i + 1
        
        while j < len(global_lines):
            next_line, _ = global_lines[j]
            
            # STOP si prochaine ligne produit démarre
            # C'est un nouveau produit si ça matche POS_RE ou POS_ONLY_RE
            # Attention: next_line pourrait être une description qui ressemble à un POS ?
            # On demande au moins Pos + SKU pour arrêter
            if POS_RE.match(next_line):
                break
            
            # Si Pos seul, vérifier si SKU juste après pour confirmer que c'est un nouveau produit
            if POS_ONLY_RE.match(next_line) and j + 1 < len(global_lines):
                next_check_line, _ = global_lines[j + 1]
                if SKU_ONLY_RE.match(next_check_line) or SKU_EAN_RE.match(next_check_line):
                    break
            
            # Chercher la quantité (même logique que précédemment)
            # D'abord, vérifier si la ligne actuelle est une quantité seule (format: "8 ST" ou "16 KG" ou "0,120 TO")
            qty_line_match = re.match(r"^\s*(\d+(?:[.,]\d+)?)\s+(ST|PAC|PC|PAK|KG|TO|TONNE)\s*$", next_line, re.IGNORECASE)
            if qty_line_match and not product_found:
                # La ligne actuelle est une quantité, donc la description est sur la ligne précédente
                start_search = i + multi_line_offset if is_multi_line else i + 1
                if j > start_search:
                    prev_line, _ = global_lines[j - 1]
                    desc = prev_line.strip()
                    qty_st = float(qty_line_match.group(1).replace(",", "."))
                    unit_st = qty_line_match.group(2).upper()
                    
                    # Normaliser TO en TONNE
                    if unit_st == "TO":
                        unit_st = "TONNE"
                    
                    # IMPORTANT: Dans les BL, si on trouve KG seul sur une ligne, on l'ignore
                    # car c'est juste informatif (poids). On cherche d'abord ST sur la ligne précédente
                    if unit_st == "KG":
                        # Vérifier si la ligne précédente contient ST
                        st_in_prev = re.search(r"\b(\d+)\s+ST\b", prev_line, re.IGNORECASE)
                        if st_in_prev:
                            # Si ST trouvé dans la ligne précédente, on prend ST au lieu de KG
                            qty_st = float(st_in_prev.group(1))
                            unit_st = "ST"
                            # Vérifier la ligne suivante pour TO/TONNE
                            if j + 1 < len(global_lines):
                                next_qty_line, _ = global_lines[j + 1]
                                tonne_match = re.match(r"^\s*(\d+(?:[.,]\d+)?)\s+(TO|TONNE)\s*$", next_qty_line, re.IGNORECASE)
                                if tonne_match:
                                    qty_st = float(tonne_match.group(1).replace(",", "."))
                                    unit_st = "TONNE"
                        # Sinon, si pas de ST dans la ligne précédente, on garde KG (cas rare)
                    
                    qty = qty_st
                    unit = unit_st
                    
                    # Si on a trouvé ST, vérifier les lignes suivantes pour TO/TONNE uniquement
                    # Dans les BL, KG après ST est juste informatif (poids), on garde ST comme unité principale
                    # Seul TO/TONNE change l'unité principale
                    if unit_st == "ST" and j + 1 < len(global_lines):
                        # Vérifier la ligne suivante pour TO/TONNE uniquement
                        next_qty_line, _ = global_lines[j + 1]
                        tonne_match = re.match(r"^\s*(\d+(?:[.,]\d+)?)\s+(TO|TONNE)\s*$", next_qty_line, re.IGNORECASE)
                        
                        if tonne_match:
                            # Si TO/TONNE trouvé, on prend TONNE comme unité principale
                            qty = float(tonne_match.group(1).replace(",", "."))
                            unit = "TONNE"
                        # Sinon, on garde ST (même si KG est présent sur la ligne suivante)
                    
                    # Nettoyage et validation
                    desc = re.sub(r"^\d{1,4}\s+", "", desc)
                    desc = " ".join(desc.split())
                    
                    if desc and is_desc_candidate(desc) and not is_excluded(desc):
                        if not any(kw in desc.lower() for kw in [
                            "frans baetenstraat", "antwerpen", "tel.", "shipping point",
                            "verzendpunt", "nummerplaat", "transporteur", "pos.", "artikel nr",
                            "beschrijving", "geleverde hoeveelheid", "gewicht", "deutsche bank",
                            "temp.conditie", "bank", "rekening", "iban", "bic", "swift"
                        ]) and qty and unit:
                            items.append({
                                "sku": sku,
                                "ean": ean,
                                "description": desc,
                                "qty": qty,
                                "unit": unit,
                                "unit_price": None,
                                "line_total": None
                            })
                            product_found = True
                    # Ne pas break ici, continuer jusqu'au prochain produit pour que j soit à la bonne position
                    # Le break se fera naturellement quand on trouvera le prochain produit
            
            # Sinon, chercher les quantités dans la ligne actuelle (format: "Description 8 ST 16 KG")
            # Ajout de TO et TONNE dans la regex
            all_qty_matches = list(re.finditer(r"\s+(\d+(?:[.,]\d+)?)\s+(TO|TONNE|ST|PAC|PC|PAK|KG)\b", next_line, re.IGNORECASE))
            
            # Si pas trouvé dans la ligne actuelle, vérifier la ligne suivante
            if not all_qty_matches and j + 1 < len(global_lines):
                next_next_line, _ = global_lines[j + 1]
                qty_line_match = re.match(r"^\s*(\d+(?:[.,]\d+)?)\s+(TO|TONNE|ST|PAC|PC|PAK|KG)\s*$", next_next_line, re.IGNORECASE)
                if qty_line_match and not product_found:
                    desc = next_line.strip()
                    qty_st = float(qty_line_match.group(1).replace(",", "."))
                    unit_st = qty_line_match.group(2).upper()
                    
                    # Normaliser TO en TONNE
                    if unit_st == "TO":
                        unit_st = "TONNE"
                    
                    qty = qty_st
                    unit = unit_st
                    
                    # Si on a trouvé ST, vérifier la ligne suivante pour TO/TONNE uniquement
                    # Dans les BL, KG après ST est juste informatif (poids), on garde ST comme unité principale
                    # Seul TO/TONNE change l'unité principale
                    if unit_st == "ST" and j + 2 < len(global_lines):
                        next_next_next_line, _ = global_lines[j + 2]
                        tonne_match = re.match(r"^\s*(\d+(?:[.,]\d+)?)\s+(TO|TONNE)\s*$", next_next_next_line, re.IGNORECASE)
                        
                        if tonne_match:
                            # Si TO/TONNE trouvé, on prend TONNE comme unité principale
                            qty = float(tonne_match.group(1).replace(",", "."))
                            unit = "TONNE"
                        # Sinon, on garde ST (même si KG est présent sur la ligne suivante)
                    
                    # Nettoyage et validation
                    desc = re.sub(r"^\d{1,4}\s+", "", desc)
                    desc = " ".join(desc.split())
                    
                    if desc and is_desc_candidate(desc) and not is_excluded(desc):
                        if not any(kw in desc.lower() for kw in [
                            "frans baetenstraat", "antwerpen", "tel.", "shipping point",
                            "verzendpunt", "nummerplaat", "transporteur", "pos.", "artikel nr",
                            "beschrijving", "geleverde hoeveelheid", "gewicht", "deutsche bank",
                            "temp.conditie", "bank", "rekening", "iban", "bic", "swift"
                        ]) and qty and unit:
                            items.append({
                                "sku": sku,
                                "ean": ean,
                                "description": desc,
                                "qty": qty,
                                "unit": unit,
                                "unit_price": None,
                                "line_total": None
                            })
                            product_found = True
                    break
            
            if all_qty_matches and not product_found:
                # Regrouper les matches par unité pour faciliter la recherche
                matches_by_unit = {}
                matches_list = [] # (start_index, unit, match_obj)
                
                for m in all_qty_matches:
                    u = m.group(2).upper()
                    # On stocke tous les matches
                    if u not in matches_by_unit:
                        matches_by_unit[u] = []
                    matches_by_unit[u].append(m)
                    matches_list.append((m.start(), u, m))
                
                # Stratégie de sélection:
                # 1. Chercher si on a une unité "pièce" (ST, PAC, PC, PAK)
                st_match = None
                st_prio = ["ST", "PAC", "PC", "PAK"]
                for p in st_prio:
                    if p in matches_by_unit:
                        # Prendre le premier match ST
                        st_match = matches_by_unit[p][0]
                        break
                
                selected_match = None
                
                # Chercher match TO/TONNE
                tonne_matches = matches_by_unit.get("TONNE") or matches_by_unit.get("TO")
                tonne_match = tonne_matches[-1] if tonne_matches else None # Prendre le dernier
                
                # Chercher match KG - prendre UNIQUEMENT celui qui suit directement ST sur la même ligne
                # Dans les BL, on ne prend KG que si ST et KG sont sur la MÊME ligne de texte
                kg_matches = matches_by_unit.get("KG")
                kg_match = None
                if kg_matches and st_match:
                    # Trouver le premier KG qui suit ST sur la même ligne
                    # Important: on ne prend que KG qui suit ST directement (format: "4 ST 20,000 KG")
                    # On ignore KG qui est dans la description avant ST (ex: "5 kg" dans "F2F Filler to finish 5 kg (60)")
                    # CRUCIAL: Trier les matches KG par position pour prendre le premier qui suit ST
                    kg_matches_sorted = sorted([kg_m for kg_m in kg_matches if kg_m.start() > st_match.start()], key=lambda m: m.start())
                    
                    for kg_m in kg_matches_sorted:
                        # Vérifier que KG suit ST (format: "X ST Y KG")
                        # Distance raisonnable: "4 ST" + espace + "20,000 KG" = environ 15-20 caractères max
                        # Mais on accepte jusqu'à 150 caractères pour gérer les cas avec description longue
                        distance = kg_m.start() - st_match.end()
                        if 0 <= distance < 150:  # KG doit suivre ST (entre 0 et 150 caractères, 0 = directement après ST)
                            # Vérifier qu'il n'y a pas d'autres unités entre ST et KG
                            # Format attendu: "4 ST 20,000 KG" (pas "4 ST quelque chose 20,000 KG")
                            text_between = next_line[st_match.end():kg_m.start()].strip()
                            # Si le texte entre ST et KG ne contient que des chiffres, espaces, virgules/points, c'est OK
                            # Accepter aussi les espaces multiples et texte vide
                            # Ignorer si text_between contient des lettres (c'est probablement dans la description)
                            # Simplification: accepter si text_between est vide ou ne contient que des chiffres/espaces/virgules/points
                            # Vérifier aussi qu'il n'y a pas d'autres unités (ST, PAC, PC, PAK, TO, TONNE) entre ST et KG
                            has_other_units = False
                            if text_between:
                                # Vérifier s'il y a d'autres unités dans text_between
                                other_units_pattern = r"\b(ST|PAC|PC|PAK|TO|TONNE)\b"
                                if re.search(other_units_pattern, text_between, re.IGNORECASE):
                                    has_other_units = True
                            
                            # Extraire la regex dans une variable pour éviter l'erreur f-string
                            text_between_pattern = r"^[\d\s,\.]+$"
                            
                            # IMPORTANT: Dans les BL, on ne prend KG que si la quantité en KG est décimale
                            # (ex: "20,000 KG", "60,000 KG") car cela indique que KG est l'unité principale
                            # Sinon, ST reste l'unité principale et KG est juste informatif (poids)
                            kg_qty_str = kg_m.group(1)
                            has_decimal = ',' in kg_qty_str or '.' in kg_qty_str
                            
                            if not text_between or (re.match(text_between_pattern, text_between) and not has_other_units):
                                # Prendre KG seulement si la quantité est décimale (ex: "20,000 KG")
                                if has_decimal:
                                    kg_match = kg_m
                                    break
                
                if st_match:
                    # Cas principal: on a "ST". 
                    # Dans les BL, on garde ST comme unité principale SAUF si:
                    # 1. TO/TONNE suit ST → on prend TONNE
                    # 2. KG suit directement ST sur la même ligne (format: "4 ST 20,000 KG") → on prend KG
                    # Sinon, on garde ST (même si KG est présent ailleurs)
                    if tonne_match and tonne_match.start() > st_match.start():
                        selected_match = tonne_match
                    elif kg_match:
                        # ST et KG sur la même ligne ET KG suit directement ST → on prend KG
                        selected_match = kg_match
                    else:
                        # ST seul ou KG sur ligne séparée ou KG avant ST → on garde ST
                        selected_match = st_match
                else:
                    # Pas de ST. 
                    # Dans les BL, on ne prend KG que s'il est associé à ST
                    # Si pas de ST, on prend TO/TONNE si présent, sinon on prend la première unité trouvée (ST, PAC, PC, PAK)
                    if tonne_match:
                        selected_match = tonne_match
                    else:
                        # Chercher une unité "pièce" (ST, PAC, PC, PAK) si disponible
                        for p in ["ST", "PAC", "PC", "PAK"]:
                            if p in matches_by_unit:
                                selected_match = matches_by_unit[p][0]
                                break
                        # Fallback: prendre la première quantité trouvée
                        if not selected_match and all_qty_matches:
                            selected_match = all_qty_matches[0]
                
                if selected_match:
                    qty = float(selected_match.group(1).replace(",", "."))
                    unit = selected_match.group(2).upper()
                    # Normaliser TO en TONNE
                    if unit == "TO":
                        unit = "TONNE"
                    
                    # Description = tout avant la quantité sélectionnée
                    # Si on a pris KG parce qu'il était après ST, la description est avant ST !
                    # Ex: "Desc... 4 ST 20 KG" -> Desc est avant "4 ST"
                    
                    # Trouver le "tôt" match qui fait partie du groupe logique
                    # Si on a sélectionné un override (KG après ST), la rupture de description est le ST
                    start_limit = selected_match.start()
                    
                    if st_match and selected_match != st_match and selected_match.start() > st_match.start():
                        # On a override ST par KG/TO qui est après. La description s'arrête avant ST.
                        start_limit = st_match.start()
                    
                    desc = next_line[:start_limit].strip()
                    desc = re.sub(r"^\d{1,4}\s+", "", desc)
                    desc = " ".join(desc.split())
                    
                    if desc and is_desc_candidate(desc) and not is_excluded(desc):
                        if not any(kw in desc.lower() for kw in [
                            "frans baetenstraat", "antwerpen", "tel.", "shipping point",
                            "verzendpunt", "nummerplaat", "transporteur", "pos.", "artikel nr",
                            "beschrijving", "geleverde hoeveelheid", "gewicht", "deutsche bank",
                            "temp.conditie", "bank", "rekening", "iban", "bic", "swift"
                        ]) and qty and unit:
                            items.append({
                                "sku": sku,
                                "ean": ean,
                                "description": desc,
                                "qty": qty,
                                "unit": unit,
                                "unit_price": None,
                                "line_total": None
                            })
                            product_found = True
                    break
            
            j += 1
        
        # Avancer l'index principal à la fin du traitement de l'item
        # Si on a trouvé un produit (j a été modifié), on avance à j pour continuer
        # Sinon, on avance juste d'une ligne
        # Mais attention : si on a trouvé un produit, on doit avancer jusqu'au prochain "Pos" ou "Pos SKU EAN"
        # Le break dans la boucle while j s'est arrêté à la ligne du prochain produit (ou à la fin)
        # Donc on peut simplement mettre i = j pour continuer au prochain produit
        i = j
    
    return items


# ------------------------------------------------------------
# PARSE FACTURE
# ------------------------------------------------------------

def parse_factuur(pages: List[Dict]) -> List[Dict]:
    """
    Parse les factures Knauf.
    Format: Ligne quantité suivie d'un bloc avec SKU, EAN, description et prix.
    """
    items: List[Dict] = []
    
    for page_idx, page in enumerate(pages):
        words = page.get("words", [])
        if not words:
            continue

        lines = build_lines(words)
        text_lines = [" ".join(t for _, t in line).strip() for line in lines]
        
        i = 0
        while i < len(text_lines):
            line = text_lines[i]

            # Détection stricte d'une ligne quantité
            if not QTY_LINE_RE.match(line):
                i += 1
                continue

            # ---- Début bloc produit : de cette ligne jusqu'à la prochaine ligne quantité ----
            j = i + 1
            while j < len(text_lines):
                if QTY_LINE_RE.match(text_lines[j]):
                    break
                j += 1

            block = text_lines[i:j]
            qty_line = line

            # ---- Quantité / unité ----
            qty = None
            unit = None

            # 1) cas spécial '4 PC 20 KG ...' => 20 KG
            m_kg = re.match(r"^(\d+)\s+PC\s+(\d+)\s*KG\b", qty_line, re.IGNORECASE)
            if m_kg:
                qty = float(m_kg.group(2))
                unit = "KG"
            else:
                # 2) cas normal '10 PAC 10 PAC ...' ou '8 ST ...'
                m = QTY_LINE_RE.match(qty_line)
                if m:
                    qty_st = float(m.group(1))
                    unit_st = m.group(2).upper()
                    
                    # Gestion des différentes unités selon les spécifications :
                    # - Si "ST" seul → unité = ST, qty = qty en ST
                    # - Si "ST" suivi de "KG" → unité = KG, qty = qty en KG
                    # - Si "ST" suivi de quantité en tonne → unité = TONNE, qty = qty en tonne
                    
                    qty = qty_st
                    unit = unit_st
                    
                    # Vérifier si on a une quantité en "KG" juste après "ST" dans la ligne
                    # Format: "4 ST 20,000 KG ..." → chercher "20,000 KG" après "ST"
                    # On cherche dans la ligne complète car on a besoin de voir "ST" suivi de quantité KG
                    
                    # NOUVEAU: Vérifier l'unité de PRIX pour décider si on utilise KG
                    price_unit_match = re.search(r"/\s*1\s+(ST|PAC|PC|KG|PAK|TO|TONNE)\b", qty_line, re.IGNORECASE)
                    price_unit = price_unit_match.group(1).upper() if price_unit_match else None
                    
                    kg_match = re.search(rf"\b{re.escape(unit_st)}\s+(\d+(?:[.,]\d+)?)\s+KG\b", qty_line, re.IGNORECASE)
                    if kg_match and price_unit == "KG":
                        # Cas KG : on trouve "ST" suivi de "KG" avec une quantité ET le prix est au KG
                        qty_kg_str = kg_match.group(1).replace(",", ".")
                        qty = float(qty_kg_str)
                        unit = "KG"
                    else:
                        # Vérifier si on a "ST" suivi d'une quantité en tonne
                        # Format: "12 ST 0,120 TO ..." ou "8 ST 2,4 TONNE ..."
                        # Accepter "TO" (abréviation) ou "TONNE" (complet)
                        tonne_match = re.search(rf"\b{re.escape(unit_st)}\s+(\d+[.,]\d+)\s+(TO|TONNE)\b", qty_line, re.IGNORECASE)
                        if tonne_match:
                            # Cas TONNE : on trouve "ST" suivi de la quantité en tonne
                            qty_tonne_str = tonne_match.group(1).replace(",", ".")
                            qty = float(qty_tonne_str)
                            unit = "TONNE"  # Normaliser en "TONNE" même si c'était "TO"
                        # Sinon, cas ST seul : on garde qty_st et unit_st

            # ---- SKU / EAN / Description ----
            sku = None
            ean = None
            desc = None

            for l in block:
                # SKU
                if not sku and "artikel:" in l.lower():
                    p = l.split(":", 1)[1].strip()
                    sku = p.lstrip("0") or p

                # EAN
                if not ean and "ean:" in l.lower():
                    p = l.split(":", 1)[1].strip()
                    if len(p) >= 8:
                        ean = p

                # Description
                if not desc and is_desc_candidate(l):
                    desc = l

            # Nettoyage description (supprimer '120 ' devant par ex.)
            if desc:
                desc = re.sub(r"^\s*\d{1,4}\s+", "", desc).strip()

            # ---- Prix ----
            unit_price, line_total = extract_prices_from_qty_line(qty_line)
            unit_price = refine_unit_price_with_block(block, unit_price)
            
            # Chercher le prix unitaire selon l'unité détectée dans la ligne quantité
            # Format: "8 ST 240 KG 2,48 /1 KG 2,36 /1 KG 47,20" ou "12 ST 0,120 TO 573,45 /1 TO ..."
            if unit and qty_line:
                # Chercher les prix unitaires correspondant à l'unité détectée
                # Si unité = TONNE, chercher aussi "TO" (abréviation)
                search_unit = unit
                if unit == "TONNE":
                    # Chercher "TO" ou "TONNE"
                    unit_price_matches = list(re.finditer(
                        rf"(\d+[.,]\d{{2}})\s*/\s*1\s+(TO|TONNE)\b",
                        qty_line,
                        re.IGNORECASE
                    ))
                else:
                    unit_price_matches = list(re.finditer(
                        rf"(\d+[.,]\d{{2}})\s*/\s*1\s+{re.escape(unit)}\b",
                        qty_line,
                        re.IGNORECASE
                    ))
                
                if unit_price_matches:
                    # Prendre le DERNIER prix unitaire (prix net après remise)
                    unit_price = float(unit_price_matches[-1].group(1).replace(",", "."))

            # Calcul du total selon l'unité détectée
            # Si pas de total mais prix unitaire + qty => calcul
            if line_total is None and unit_price is not None and qty:
                line_total = round(unit_price * qty, 2)
            elif unit_price is not None and qty:
                # Vérifier que le total correspond au calcul, sinon recalculer
                calculated_total = round(unit_price * qty, 2)
                if line_total is None or abs(line_total - calculated_total) > 0.01:
                    line_total = calculated_total

            # ---- Ajout si description valide et non palette ----
            if desc and not is_excluded(desc):
                items.append({
                    "sku": sku,
                    "ean": ean,
                    "description": desc,
                    "qty": qty,
                    "unit": unit,
                    "unit_price": unit_price,
                    "line_total": line_total
                })

            i = j  # passer au produit suivant
    
    return items


# ------------------------------------------------------------
# PARSE PRINCIPAL
# ------------------------------------------------------------

def parse(pdf_raw: Dict) -> Dict:
    pages = pdf_raw.get("pages", [])
    text = pdf_raw.get("full_text", "")  # Texte complet de toutes les pages
    text_lower = text.lower()
    
    # Définir les variables de détection BL/facture AVANT le if is_pos
    # pour qu'elles soient toujours disponibles (utilisées plus tard pour le type de document)
    has_levering_keyword = bool(re.search(r"levering\s+\d+\b", text, re.IGNORECASE))
    has_bl_keywords = ("leveringsbevestiging" in text_lower or "delivery confirmation" in text_lower or
                       "afleveringsbon" in text_lower or has_levering_keyword)
    has_weak_bl_keyword = ("leveringsbon" in text_lower or "delivery note" in text_lower)
    has_factuur_keyword = ("factuur" in text_lower or "faktuur" in text_lower or "invoice" in text_lower)
    
    # --------- AUTO DETECTION FORMAT POS ----------
    # Détecter d'abord le format POS (priorité absolue car plus spécifique)
    # Même si le document contient "Afleveringsbon" ou "Leveringsbon", 
    # si c'est au format POS, on utilise le parser POS
    is_pos = is_knauf_pos(text)
    print(f"[KNAUF DEBUG] is_knauf_pos={is_pos}")
    
    if is_pos:
        items = parse_knauf_pos(pages)
        method = "knauf_pos_v1"
        print(f"[KNAUF DEBUG] Parser POS a trouvé {len(items)} items")
        # Fallback: Si le parser POS ne trouve aucun item
        if len(items) == 0:
            # NOUVEAU: Si c'est un BL (mots-clés présents), essayer le parser BL avant la facture
            if has_bl_keywords:
                print(f"[KNAUF DEBUG] Parser POS vide et BL détecté, essai avec parser BL")
                items = parse_bon_livraison(pages)
                if len(items) > 0:
                    method = "knauf_bl_v1"
                    print(f"[KNAUF DEBUG] Parser BL a trouvé {len(items)} items")
            
            # Si toujours vide, essayer le parser facture
            if len(items) == 0:
                print(f"[KNAUF DEBUG] Parser POS (et BL) vide, essai avec parser facture")
                items = parse_factuur(pages)
                if len(items) > 0:
                    method = "knauf_factuur_v17"
                    print(f"[KNAUF DEBUG] Parser facture a trouvé {len(items)} items")
    else:
        # Détecter ensuite les bons de livraison (format spécifique avec "Pos SKU EAN")
        # Les variables de détection sont déjà définies avant le if is_pos
        # DEBUG
        print(f"[KNAUF DEBUG] Détection parser: has_bl_keywords={has_bl_keywords}, has_weak_bl_keyword={has_weak_bl_keyword}, has_factuur_keyword={has_factuur_keyword}, has_levering_keyword={has_levering_keyword}")
        
        if has_bl_keywords:
            # Mots-clés forts de BL présents
            items = parse_bon_livraison(pages)
            method = "knauf_bl_v1"
            # Fallback : Si le parser BL ne trouve aucun item, essayer le parser facture
            if len(items) == 0:
                print(f"[KNAUF DEBUG] Parser BL n'a trouvé aucun item, essai avec parser facture")
                items = parse_factuur(pages)
                if len(items) > 0:
                    method = "knauf_factuur_v17"
                    print(f"[KNAUF DEBUG] Parser facture a trouvé {len(items)} items")
        elif has_weak_bl_keyword and not has_factuur_keyword:
            # Mots-clés faibles de BL présents mais pas de mots-clés de facture
            items = parse_bon_livraison(pages)
            method = "knauf_bl_v1"
            # Fallback : Si le parser BL ne trouve aucun item, essayer le parser facture
            if len(items) == 0:
                print(f"[KNAUF DEBUG] Parser BL n'a trouvé aucun item, essai avec parser facture")
                items = parse_factuur(pages)
                if len(items) > 0:
                    method = "knauf_factuur_v17"
                    print(f"[KNAUF DEBUG] Parser facture a trouvé {len(items)} items")
        # Sinon, format facture classique
        else:
            items = parse_factuur(pages)
            method = "knauf_factuur_v17"
    
    # Fallback final : Si aucun item n'a été extrait, essayer tous les autres parsers
    if len(items) == 0:
        print(f"[KNAUF DEBUG] AUCUN ITEM EXTRAIT - Essai avec tous les parsers")
        if method == "knauf_pos_v1":
            # Parser POS a échoué, essayer d'abord le parser BL (car peut-être un BL mal détecté comme POS)
            print(f"[KNAUF DEBUG] Parser POS a échoué, essai avec parser BL")
            items = parse_bon_livraison(pages)
            if len(items) > 0:
                method = "knauf_bl_v1"
                print(f"[KNAUF DEBUG] Parser BL a trouvé {len(items)} items")
            else:
                # Si BL échoue aussi, essayer le parser facture
                print(f"[KNAUF DEBUG] Parser BL a échoué, essai avec parser facture")
                items = parse_factuur(pages)
                if len(items) > 0:
                    method = "knauf_factuur_v17"
                    print(f"[KNAUF DEBUG] Parser facture a trouvé {len(items)} items")
        elif method == "knauf_bl_v1":
            # Essayer d'abord le parser POS (car peut-être mal détecté)
            print(f"[KNAUF DEBUG] Parser BL a échoué, essai avec parser POS")
            items = parse_knauf_pos(pages)
            if len(items) > 0:
                method = "knauf_pos_v1"
                print(f"[KNAUF DEBUG] Parser POS a trouvé {len(items)} items")
            else:
                # Si POS échoue aussi, essayer le parser facture
                print(f"[KNAUF DEBUG] Parser POS a échoué, essai avec parser facture")
                items = parse_factuur(pages)
                if len(items) > 0:
                    method = "knauf_factuur_v17"
                    print(f"[KNAUF DEBUG] Parser facture a trouvé {len(items)} items")
        elif method == "knauf_factuur_v17":
            # Essayer d'abord le parser POS (car peut-être mal détecté)
            print(f"[KNAUF DEBUG] Parser facture a échoué, essai avec parser POS")
            items = parse_knauf_pos(pages)
            if len(items) > 0:
                method = "knauf_pos_v1"
                print(f"[KNAUF DEBUG] Parser POS a trouvé {len(items)} items")
            else:
                # Si POS échoue aussi, essayer le parser BL
                print(f"[KNAUF DEBUG] Parser POS a échoué, essai avec parser BL")
                items = parse_bon_livraison(pages)
                if len(items) > 0:
                    method = "knauf_bl_v1"
                    print(f"[KNAUF DEBUG] Parser BL a trouvé {len(items)} items")
    
    # Détecter le type de document pour les métadonnées
    doc_type = "Factuur"  # Par défaut
    
    # Vérifier la présence de prix dans les items (indicateur fiable)
    # Vérifier à la fois unit_price ET line_total pour être sûr
    has_prices = any(
        (item.get("unit_price") is not None and item.get("unit_price", 0) > 0) or
        (item.get("line_total") is not None and item.get("line_total", 0) > 0)
        for item in items
    )
    
    # --- Détection type document Knauf (corrigé) ---
    # Logique : combiner prix + méthode de parsing + mots-clés + nombre d'items
    
    # DEBUG: Afficher les indicateurs pour diagnostiquer
    print(f"[KNAUF DEBUG] method={method}, has_prices={has_prices}, items_count={len(items)}")
    if items:
        first_item = items[0]
        print(f"[KNAUF DEBUG] first_item keys: {list(first_item.keys())}")
        print(f"[KNAUF DEBUG] first_item unit_price: {first_item.get('unit_price')}, line_total: {first_item.get('line_total')}")
        if len(items) > 1:
            print(f"[KNAUF DEBUG] second_item unit_price: {items[1].get('unit_price')}, line_total: {items[1].get('line_total')}")
    
    # Vérifier les mots-clés dans le texte
    has_factuur = "factuur" in text_lower or "faktuur" in text_lower or "invoice" in text_lower
    has_bl = ("afleveringsbon" in text_lower or "leveringsbon" in text_lower or 
              "delivery note" in text_lower or has_levering_keyword)
    has_leveringbevestiging = "leveringsbevestiging" in text_lower or "delivery confirmation" in text_lower
    print(f"[KNAUF DEBUG] has_factuur={has_factuur}, has_bl={has_bl}, has_leveringbevestiging={has_leveringbevestiging}")
    
    # CAS SPÉCIAL : Si aucun item n'a été extrait (items_count=0)
    # C'est probablement une erreur de détection du parser
    if len(items) == 0:
        print(f"[KNAUF DEBUG] AUCUN ITEM EXTRAIT - Correction nécessaire")
        # Si le document contient "factuur", c'est probablement une facture
        if has_factuur:
            doc_type = "Factuur"
            print(f"[KNAUF DEBUG] Pas d'items mais has_factuur=True -> Factuur (correction)")
        # Sinon, utiliser les mots-clés BL
        elif has_bl or has_leveringbevestiging:
            if has_leveringbevestiging:
                doc_type = "Leveringsbevestiging"
            else:
                doc_type = "Leveringsbon"
            print(f"[KNAUF DEBUG] Pas d'items mais mots-clés BL -> {doc_type}")
        else:
            # Par défaut, facture si pas d'indication
            doc_type = "Factuur"
            print(f"[KNAUF DEBUG] Pas d'items, pas d'indication -> Factuur (défaut)")
    
    # Priorité 1 : Si méthode POS, utiliser les prix
    elif method == "knauf_pos_v1":
        doc_type = "Factuur" if has_prices else "Leveringsbon"
        print(f"[KNAUF DEBUG] POS -> doc_type={doc_type}")
    
    # Priorité 2 : Si méthode BL, vérifier les prix et les mots-clés
    elif method == "knauf_bl_v1":
        if has_prices:
            # Prix présents mais méthode BL = facture mal détectée
            doc_type = "Factuur"
            print(f"[KNAUF DEBUG] BL avec prix -> Factuur (correction)")
        elif has_factuur:
            # Mots-clés facture présents = facture mal détectée (priorité sur BL)
            doc_type = "Factuur"
            print(f"[KNAUF DEBUG] BL mais has_factuur=True -> Factuur (correction)")
        else:
            # Pas de prix + méthode BL + pas de mots-clés facture = BL
            if has_leveringbevestiging:
                doc_type = "Leveringsbevestiging"
            else:
                doc_type = "Leveringsbon"
            print(f"[KNAUF DEBUG] BL sans prix -> {doc_type}")
    
    # Priorité 3 : Si méthode facture, vérifier les prix et les mots-clés
    elif method == "knauf_factuur_v17":
        # Si le document contient "Leveringsbevestiging" et pas "factuur", c'est un BL
        if has_leveringbevestiging and not has_factuur:
            doc_type = "Leveringsbevestiging"
            print(f"[KNAUF DEBUG] Factuur mais has_leveringbevestiging=True et has_factuur=False -> Leveringsbevestiging (correction)")
        elif has_bl and not has_factuur:
            # Mots-clés BL présents mais pas de mots-clés facture = BL mal détecté
            if has_leveringbevestiging:
                doc_type = "Leveringsbevestiging"
            else:
                doc_type = "Leveringsbon"
            print(f"[KNAUF DEBUG] Factuur mais has_bl=True et has_factuur=False -> {doc_type} (correction)")
        elif has_prices:
            # Prix présents + méthode facture = facture
            doc_type = "Factuur"
            print(f"[KNAUF DEBUG] Factuur avec prix -> Factuur")
        else:
            # Par défaut, facture
            doc_type = "Factuur"
            print(f"[KNAUF DEBUG] Factuur -> Factuur (défaut)")
    
    # Fallback : utiliser uniquement les prix
    else:
        doc_type = "Factuur" if has_prices else "Leveringsbon"
        print(f"[KNAUF DEBUG] Fallback -> {doc_type}")
    
    print(f"[KNAUF DEBUG] FINAL doc_type={doc_type}")


    
    metadata = {
        "type": doc_type,
        "number": extract_number(text),
        "client": extract_client_factuurontvanger(text),
        "supplier": extract_supplier(text),
        "date": extract_date(text),
        "count": len(items),
        "method": method,
        "supplier_code": extract_supplier_code_knauf(text),
        "supplier_address": extract_supplier_address_knauf(text),
        "supplier_phone": extract_supplier_phone_knauf(text),
        "supplier_email": extract_supplier_email_knauf(text),
        "supplier_contact": extract_supplier_contact_knauf(text),
        "supplier_payment_terms": extract_supplier_payment_terms_knauf(text)
    }
    return {
        "items": items,
        "metadata": metadata
    }
