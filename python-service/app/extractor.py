def extract_products_from_invoice(path: str) -> List[Dict]:
    """Extrait les produits d'une facture Knauf avec une approche textuelle améliorée
    
    Structure d'un produit dans la facture :
    1. Ligne quantité : quantité + unité à gauche, prix brut, prix net, valeur nette à droite
    2. Ligne code : "Artikel: XXXXXXXX"
    3. Ligne description : libellé produit
    4. Ligne prix : "Onvoorwaardelijke nettoprijs" + prix net par unité
    5. Ligne remise : "Klant art korting" + pourcentage
    6. Ligne EAN : "EAN: XXXXXXXXXXXXX"
    """
    products: List[Dict] = []
    
    with pdfplumber.open(path) as pdf:
        for page in pdf.pages:
            text = page.extract_text()
            if not text:
                continue
                
            lines = [l.strip() for l in text.split('\n') if l.strip()]
            
            # Parcourir les lignes pour trouver les quantités (ligne 1 d'un produit)
            i = 0
            while i < len(lines):
                line = lines[i]
                line_lower = line.lower()
                
                # Ignorer les lignes d'en-tête et de footer
                if any(kw in line_lower for kw in [
                    "pos.", "hoeveelheid", "brutoprijs", "nettoprijs", "netto waarde",
                    "totaal (artikelen)", "totaal (incl. btw)", "btw", "bruto gewicht",
                    "betalingsvoorwaarde", "page", "pagina", "totaal"
                ]):
                    i += 1
                    continue
                
                # Ignorer les lignes qui sont juste des informations de produit (pas la ligne quantité)
                if re.match(r'^\s*(Artikel:|EAN:|Onvoorwaardelijke|Klant art korting)', line, flags=re.IGNORECASE):
                    i += 1
                    continue
                
                # Détecter les lignes avec quantité (ligne 1 d'un produit)
                # Format: "10 PAC 10 PAC" ou "4 PC 20 KG" ou "12 PAC" avec prix à droite
                quantity_pattern = r'(?:^|\s)(\d+)\s*(PAC|PC|KG)(?:\s+\d+\s*(?:PAC|PC|KG))?'
                qty_match = re.search(quantity_pattern, line, flags=re.IGNORECASE)
                
                if qty_match:
                    quantity = int(qty_match.group(1))
                    unit = qty_match.group(2).upper()  # Normaliser en majuscules (PAC, PC, KG)
                    
                    # Vérifier que ce n'est pas juste un prix (ex: "3,41 /PAC" ne doit pas matcher)
                    if not re.search(r'\d+\s*(PAC|PC|KG)\s+\d+\s*(PAC|PC|KG)', line, flags=re.IGNORECASE) and \
                       re.search(r'^\s*\d+[.,]\d+\s*/\s*(PAC|PC|KG)', line, flags=re.IGNORECASE):
                        i += 1
                        continue
                    
                    # Initialiser les variables pour ce produit
                    description = ""
                    product_code = ""
                    ean = None
                    unit_price = None
                    total_value = None
                    
                    # Extraire les prix de la ligne quantité
                    # Chercher prix unitaire: "X,XX /PAC" ou "X,XX /PC" ou "X,XX /KG"
                    per_unit = re.findall(r'(\d+[.,]\d+)\s*/\s*(PAC|PC|KG)', line, flags=re.IGNORECASE)
                    if per_unit:
                        try:
                            # Prendre le dernier prix unitaire trouvé (le plus à droite = prix net)
                            unit_price = float(per_unit[-1][0].replace(',', '.'))
                        except Exception:
                            pass
                    
                    # Chercher tous les décimaux pour trouver prix unitaire et total
                    all_decimals = re.findall(r'\b(\d+[.,]\d{2,})\b', line)
                    if all_decimals:
                        decimals = []
                        for d in all_decimals:
                            try:
                                decimals.append(float(d.replace(',', '.')))
                            except Exception:
                                pass
                        if decimals:
                            decimals_sorted = sorted(decimals)
                            if unit_price is None:
                                # Prix unitaire: le plus petit nombre raisonnable (mais pas trop petit)
                                small = [d for d in decimals_sorted if 0.1 < d < 1000]
                                if small:
                                    unit_price = small[0]
                            # Total: le plus grand nombre raisonnable
                            large = [d for d in decimals_sorted if d > 0]
                            if large:
                                total_value = large[-1]
                    
                    # Chercher le code "Artikel:" dans les lignes suivantes (ligne 2)
                    artikel_line_idx = None
                    for j in range(i+1, min(i+10, len(lines))):
                        next_line = lines[j]
                        # Arrêter si on trouve une nouvelle quantité (nouveau produit)
                        if re.search(r'(?:^|\s)(\d+)\s*(PAC|PC|KG)(?:\s+\d+\s*(?:PAC|PC|KG))?', next_line, flags=re.IGNORECASE):
                            break
                        
                        # Chercher "Artikel:" (ligne 2)
                        artikel_match = re.search(r'Artikel:\s*(\d+)', next_line, flags=re.IGNORECASE)
                        if artikel_match and not product_code:
                            product_code = artikel_match.group(1)
                            artikel_line_idx = j
                            # Chercher EAN dans la même ligne (peu probable mais possible)
                            ean_match = re.search(r'EAN:\s*(\d+)', next_line, flags=re.IGNORECASE)
                            if ean_match:
                                ean = ean_match.group(1)
                    
                    # Chercher la description (ligne 3) - juste après "Artikel:"
                    if artikel_line_idx is not None:
                        for j in range(artikel_line_idx + 1, min(artikel_line_idx + 5, len(lines))):
                            next_line = lines[j]
                            # Arrêter si on trouve une nouvelle quantité (nouveau produit)
                            if re.search(r'(?:^|\s)(\d+)\s*(PAC|PC|KG)(?:\s+\d+\s*(?:PAC|PC|KG))?', next_line, flags=re.IGNORECASE):
                                break
                            # Ignorer les lignes qui sont des codes, prix, ou remises
                            if re.match(r'^\s*(Artikel:|EAN:|Onvoorwaardelijke|Klant art korting)', next_line, flags=re.IGNORECASE):
                                continue
                            # Chercher une description qui commence par une lettre majuscule
                            desc_match = re.search(r'^([A-Z][A-Za-z][^0-9]*?)(?=\d|Artikel:|EAN:|Onvoorwaardelijke|Klant|$)', next_line)
                            if desc_match:
                                candidate_desc = desc_match.group(1).strip()
                                # Vérifier que ce n'est pas un en-tête
                                if len(candidate_desc) >= 3 and not any(kw in candidate_desc.lower() for kw in [
                                    "antwerpen", "contactpersoon", "leveringsvoorwaarde", "place of destination", "totaal"
                                ]):
                                    description = candidate_desc
                                    break
                    
                    # Chercher EAN (ligne 6) - après la description, dans les lignes suivantes
                    if description:
                        for j in range(i+1, min(i+10, len(lines))):
                            next_line = lines[j]
                            # Arrêter si on trouve une nouvelle quantité (nouveau produit)
                            if re.search(r'(?:^|\s)(\d+)\s*(PAC|PC|KG)(?:\s+\d+\s*(?:PAC|PC|KG))?', next_line, flags=re.IGNORECASE):
                                break
                            # Chercher EAN
                            if not ean:
                                ean_match = re.search(r'EAN:\s*(\d+)', next_line, flags=re.IGNORECASE)
                                if ean_match:
                                    ean = ean_match.group(1)
                                    break  # EAN est la dernière ligne du produit
                    
                    # Nettoyer la description
                    if description:
                        description = re.sub(r'\s+', ' ', description).strip()
                    
                    # Ajouter le produit seulement si on a une quantité valide
                    if quantity > 0:
                        # Si pas de description, utiliser le code produit comme fallback
                        if not description or len(description) < 3:
                            if product_code:
                                description = product_code
                            else:
                                # Si pas de code ni description, ignorer cette ligne
                                i += 1
                                continue
                        
                        normalized = re.sub(r'\s+', ' ', description.lower()).strip()
                        
                        # Vérifier que ce n'est pas un en-tête
                        if any(kw in normalized for kw in ['totaal', 'btw', 'bruto gewicht', 'betalingsvoorwaarde']):
                            i += 1
                            continue
                        
                        products.append({
                            "raw": line,
                            "normalized": normalized,
                            "quantity": quantity,
                            "product_code": product_code,
                            "ean": ean,
                            "unit": unit,
                            "unit_price": unit_price,
                            "total_value": total_value
                        })
                    i += 1
                    continue
                
                # Format ancien: "10 545753 8 ST"
                product_pattern_old = r'^\s*(\d+)\s+(\d+)\s+(\d+)\s*(ST|PAK)'
                match_old = re.search(product_pattern_old, line)
                if match_old:
                    product_code = match_old.group(2)
                    quantity = int(match_old.group(3))
                    unit = match_old.group(4)
                    
                    # Extraire description et prix
                    description = ""
                    ean = None
                    unit_price = None
                    total_value = None
                    
                    # Chercher description après le code
                    desc_start = match_old.end()
                    remaining = line[desc_start:].strip()
                    desc_match = re.search(r'^([A-Za-z][^0-9]*?)(?=\d|$)', remaining)
                    if desc_match:
                        description = desc_match.group(1).strip()
                    
                    # Chercher prix: "X,XX /1 ST"
                    per_unit = re.findall(r'(\d+[.,]\d+)\s*/\s*1\s*(ST|PAK)', line, flags=re.IGNORECASE)
                    if per_unit:
                        try:
                            unit_price = float(per_unit[-1][0].replace(',', '.'))
                        except Exception:
                            pass
                    
                    # Chercher EAN
                    ean_match = re.search(r'EAN-nr\.\s*:\s*(\d+)', line)
                    if ean_match:
                        ean = ean_match.group(1)
                    
                    if description and product_code:
                        normalized = re.sub(r'\s+', ' ', description.lower()).strip()
                        products.append({
                            "raw": line,
                            "normalized": normalized,
                            "quantity": quantity,
                            "product_code": product_code,
                            "ean": ean,
                            "unit": unit,
                            "unit_price": unit_price,
                            "total_value": total_value
                        })
                    i += 1
                    continue
                
                # Si aucune correspondance, passer à la ligne suivante
                i += 1
    
    # Filtrer les doublons
    seen = set()
    unique_products = []
    
    for product in products:
        key = (product["product_code"], product["quantity"], product["normalized"])
        if (key not in seen and product["quantity"] > 0 and 
            product["product_code"] and len(product["product_code"]) >= 4):
            seen.add(key)
            unique_products.append(product)
    
    return unique_products
