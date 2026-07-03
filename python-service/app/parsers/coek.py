"""
Parser spécifique pour COEK (Coek Engineering / Catalog).

Gère :
  - Catalogues / Listes de prix / Commandes

Retour :
{
  "items": [
      {
          "sku": "...",
          "ean": null,
          "description": "...",
          "qty": ...,
          "unit": "...",
          "unit_price": null,
          "line_total": null
      },
      ...
  ],
  "metadata": {
      "type": "Catalog" | "Order" | "Invoice",
      "number": "...",
      "date": "dd/mm/yyyy",
      "client": "...",
      "supplier": "COEK",
      "count": ...,
      "method": "coek_v1"
  }
}
"""

import re
from typing import Dict, List, Optional
import os

def log_debug(msg):
    try:
        temp_dir = os.getenv('TEMP', os.getenv('TMP', '/tmp'))
        log_path = os.path.join(temp_dir, "coek_debug.log")
        with open(log_path, "a", encoding="utf-8") as f:
            f.write(msg + "\n")
    except:
        pass


# ------------------------------------------------------------
# TEXT HELPERS
# ------------------------------------------------------------
ACCENT_RE = re.compile(r"[éèêëàâîïôöùûüçœÉÈÊËÀÂÎÏÔÖÙÛÜÇŒ]")

def is_code_like(value: Optional[str]) -> bool:
    """Retourne True si la chaine ressemble à un petit code technique (ex: 102)."""
    if not value:
        return False
    v = str(value).strip()
    return bool(re.fullmatch(r"[\d\w./-]{1,6}", v))


def split_lang_block(block: str) -> (Optional[str], Optional[str]):
    """
    Essaie de découper un bloc NL/FR.
    Heuristiques simples:
    - Si deux paragraphes séparés par une ligne vide -> NL puis FR
    - Sinon, on cherche le premier caractère accentué (probablement FR)
    """
    if not block:
        return None, None
    
    block = block.strip()
    # 1) Paragraphes séparés
    parts = re.split(r"\n\s*\n", block)
    if len(parts) >= 2:
        nl_part = parts[0].strip()
        fr_part = " ".join(p.strip() for p in parts[1:] if p.strip())
        return (nl_part or None, fr_part or None)
    
    # 2) Détection d'un début de phrase française via caractère accentué
    m = ACCENT_RE.search(block)
    if m:
        nl = block[:m.start()].strip()
        fr = block[m.start():].strip()
        return (nl or None, fr or None)
    
    # 3) Fallback: dupliquer
    return block or None, block or None


def extract_technical_specs(text: str) -> Dict[str, Dict[str, Optional[str]]]:
    """
    Extrait les descriptions techniques (NL/FR) indexées par code technique.
    On cible la section 'technische specificaties / spécifications techniques'.
    """
    specs: Dict[str, Dict[str, Optional[str]]] = {}
    if not text:
        return specs
    
    lines = [l.strip() for l in text.splitlines()]
    start_indices = [
        i for i, l in enumerate(lines)
        if "technische specificaties" in l.lower() or "spécifications techniques" in l.lower()
    ]
    if not start_indices:
        return specs
    
    code_re = re.compile(r"^(\d{2,5})\b")
    start_idx = min(start_indices)
    i = start_idx + 1
    
    while i < len(lines):
        line = lines[i]
        m = code_re.match(line)
        if m:
            code = m.group(1)
            block_lines = []
            j = i + 1
            while j < len(lines):
                nxt = lines[j]
                if code_re.match(nxt):
                    break
                # stop if we reach another section title
                if "technische specificaties" in nxt.lower() or "spécifications techniques" in nxt.lower():
                    break
                block_lines.append(nxt)
                j += 1
            
            block_text = "\n".join(block_lines).strip()
            nl_desc, fr_desc = split_lang_block(block_text)
            specs[code] = {"nl": nl_desc, "fr": fr_desc}
            i = j
            continue
        i += 1
    
    return specs


def fallback_long_desc_from_body(text: str, code: str) -> (Optional[str], Optional[str]):
    """
    Heuristique de secours: cherche un bloc de texte juste après une ligne contenant le code.
    On prend quelques lignes suivantes et on tente de splitter NL/FR en évitant les en-têtes de tableau.
    """
    if not text or not code:
        return None, None
    lines = text.splitlines()
    indices = [i for i, l in enumerate(lines) if code in l]
    if not indices:
        return None, None
    i = indices[0]
    start = max(0, i + 1)
    end = min(len(lines), i + 20)  # petite fenêtre après le code
    raw_block_lines = [l.rstrip() for l in lines[start:end]]

    # Arrêter au premier blanc ou bloc "zie pagina" / titres
    stop_regex = re.compile(r"(zie\s+pagina|voire\s+page|colbloc|versie\s+\d|inleiding|introduction)", re.IGNORECASE)
    header_regex = re.compile(r"(art\.?\s*nr|omschrijving|description|poids|weight|unit|eenheid|pallet|bestelh|aantal|type)", re.IGNORECASE)

    block_lines = []
    for l in raw_block_lines:
        if not l.strip():
            break
        if header_regex.search(l):
            continue
        if stop_regex.search(l):
            break
        block_lines.append(l.strip())

    if not block_lines:
        return None, None

    # Reconstituer le bloc et vérifier qu'il ressemble à une vraie description (phrases)
    block = "\n".join(block_lines)
    # Filtrer les blocs qui ressemblent encore à un tableau (mots clés)
    if re.search(r"(Description|Poids|Unit|Aantal|Type|No\.?\s*Art)", block, re.IGNORECASE):
        return None, None
    if len(block) < 40 or not re.search(r"[.!?;]", block):
        return None, None

    return split_lang_block(block)


# ------------------------------------------------------------
# HELPERS
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
    tol = 6.0 

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
    lower = text.lower()
    if "invoice" in lower or "facture" in lower or "rekening" in lower:
        return "Invoice"
    if "delivery" in lower or "levering" in lower or "bon de livraison" in lower:
        return "Delivery Note"
    if "order" in lower or "commande" in lower or "bestelling" in lower:
        return "Order"
    
    return "Catalog"


def extract_number(text: str) -> Optional[str]:
    # Cherche un numéro de document
    m = re.search(r"(?:Nr\.|No\.|Number|Numéro)\s*[:.]?\s*([0-9A-Za-z/-]{3,20})", text, re.IGNORECASE)
    if m:
        return m.group(1)
    return None


def extract_date(text: str) -> Optional[str]:
    # dd/mm/yyyy ou dd.mm.yyyy ou dd-mm-yyyy
    m = re.search(r"\b(\d{2}[./-]\d{2}[./-]\d{4})\b", text)
    return m.group(1) if m else None


# ------------------------------------------------------------
# ITEMS
# ------------------------------------------------------------

# Colonnes typiques:
# SKU | Omschrijving NL | Description FR | [Afmetingen] | Gewicht | Eenheid | Bestelh | Aantal/Pallet | Type
# Ex: 281765 Betonmortel... Béton... 25,00 zak-sac 48 48 94
# Ex: 212178 Betonblok... Bloc... 29,0x9,0x14,0 7,80 stuk-pièce 208 208 95

# 1. SKU: 6 chiffres au début de ligne
# 2. Description: Texte libre. Peut etre sur plusieurs lignes.
# 3. Dims (Optionnel): Formats comme 29,0x9,0x14,0 ou 100,0x8,0x1,0. Generalement a la fin de la description.
# 4. Poids: Nombre avec 2 décimales (ex: 25,00 ou 1,40 ou 1500,00)
# 5. Unité: zak-sac, stuk-pièce, L, set, rol - rouleau, pak - paquet
# 6. Bestelh: Entier
# 7. Qty/Pallet: Entier
# 8. Type Pallet: Entier ou '-'

# On utilise une regex pour la fin (Stats) car elle est très structurée.
# Le début (SKU) est structuré.
# Le milieu (Description) est variable.

LINE_SUFFIX_RE = re.compile(
    r"\s+"
    r"(\d+[.,]\d{2})"           # Poids (ex: 25,00)
    r"\s+"
    r"([a-zA-Z\s/-]+?)"         # Unité (ex: zak-sac)
    r"\s+"
    r"(\d+)"                    # Bestelh (ex: 48)
    r"\s+"
    r"(\d+)"                    # Aantal/Pallet (ex: 48)
    r"\s+"
    r"([0-9-]+)"                # Type Pallet (ex: 94 ou -)
    r"$",
    re.IGNORECASE
)

# Pattern pour détecter si un texte est juste des stats (pas un nom de produit)
STATS_ONLY_PATTERN = re.compile(
    r"^[\s(]*"  # Début optionnel avec parenthèses ou espaces
    r"(?:"  # Groupe optionnel
        r"\(?\d+\s*(?:st|pc|blister|zak|sac|pak|paquet|pallet|palette)\)?"  # Ex: "(2 st/blister)"
        r"|"  # OU
        r"\d+[.,]\d{2}\s*(?:blister|zak|sac|pak|paquet)"  # Ex: "0,04 blister"
        r"|"  # OU
        r"(?:paquet|pallet|palette)\s+\d+\s+\d+\s+\d+"  # Ex: "paquet 1 25 94"
    r")"
    r"(?:\s+.*)?$",  # Optionnellement suivi d'autres stats
    re.IGNORECASE
)

def is_stats_only(text: str) -> bool:
    """Vérifie si un texte est juste des stats (pas un nom de produit valide)"""
    if not text or not text.strip():
        return False
    text_clean = text.strip()
    # Si c'est très court et ressemble à des stats
    if len(text_clean) < 20:
        # Pattern pour détecter des stats courtes
        if STATS_ONLY_PATTERN.match(text_clean):
            return True
        # Si ça commence par des parenthèses et contient des chiffres/stats
        if text_clean.startswith("(") and any(c.isdigit() for c in text_clean):
            if "blister" in text_clean.lower() or "st" in text_clean.lower() or "pc" in text_clean.lower():
                return True
    # Si ça contient un pattern de stats complet (poids + unité + chiffres)
    if LINE_SUFFIX_RE.search(text_clean):
        # Vérifier si c'est juste ça (pas de texte descriptif avant)
        match = LINE_SUFFIX_RE.search(text_clean)
        if match and match.start() < 10:  # Si les stats commencent très tôt
            return True
    return False

# Pattern pour dimensions: 123,0x45,0x67,0
DIMS_RE = re.compile(r"(\d+[.,]\d+x\d+[.,]\d+(?:x\d+[.,]\d+)?)")

IGNORED_LINE_KEYWORDS = [
    "CODE", "DESCRIPTION", "UNIT", "QUANTITY", "PRICE", "TOTAL", "AMOUNT",
    "PAGE", "DATE", "CLIENT", "BTW", "TVA", "IBAN", "BIC", "BANK",
    "AFMETINGEN", "GEWICHT", "EENHEID", "BESTELH", "AANTAL", "TYPE PALLET",
    "OMSCHRIJVING", "NO. ART."
]

# En-têtes de tableau spécifiques (NL et FR)
# Note: Certains en-têtes peuvent être sur plusieurs lignes (ex: "Aantal" puis "/ pallet")
TABLE_HEADER_PATTERNS = [
    r"^Art\.?\s*Nr\.?$",  # Art. Nr.
    r"^Omschrijving$",  # Omschrijving
    r"^Gewicht$",  # Gewicht
    r"^\(kg\)$",  # (kg)
    r"^Eenheid$",  # Eenheid
    r"^Bestelh\.?$",  # Bestelh.
    r"^Aantal\s*/?\s*pallet$",  # Aantal / pallet (sur une ligne)
    r"^Aantal$",  # Aantal (sur ligne séparée)
    r"^/ pallet$",  # / pallet (suite de Aantal)
    r"^Type\s+pallet$",  # Type pallet (sur une ligne)
    r"^Type$",  # Type (sur ligne séparée, suivi de "pallet")
    r"^pallet$",  # pallet (suite de Type)
    r"^No\.?\s*Art\.?$",  # No. Art.
    r"^Description$",  # Description
    r"^Poids$",  # Poids
    r"^Unité$",  # Unité
    r"^Unit\.?\s*comm\.?$",  # Unit. comm. (sur une ligne)
    r"^Unit\.?$",  # Unit. (sur ligne séparée)
    r"^comm\.?$",  # comm. (suite de Unit.)
    r"^Quantité\s*/?\s*palette$",  # Quantité / palette (sur une ligne)
    r"^Quantité$",  # Quantité (sur ligne séparée)
    r"^/ palette$",  # / palette (suite de Quantité)
    r"^Type\s+palette$",  # Type palette (sur une ligne)
    r"^palette$",  # palette (suite de Type)
]

def is_header_or_footer(line: str) -> bool:
    u = line.upper().strip()
    if not u:
        return False
    
    # Lignes très courtes qui sont des nombres seuls
    if len(line) < 5 and re.match(r"^\d+$", line):
        return True
    
    # Version
    if "VERSIE 202" in u:
        return True
    
    # Vérifier les mots-clés génériques
    if any(k in u for k in IGNORED_LINE_KEYWORDS):
        return True
    
    # Vérifier les patterns d'en-têtes de tableau spécifiques
    for pattern in TABLE_HEADER_PATTERNS:
        if re.match(pattern, line, re.IGNORECASE):
            return True
    
    return False


def clean_repetitions(text: str, sku: Optional[str] = None) -> str:
    """Enlève les répétitions de mots et les préfixes indésirables"""
    if not text:
        return text
    text = text.strip()
    
    # Enlever le préfixe "N " (probablement "Nouveau" tronqué)
    if text.startswith("N ") and len(text) > 2:
        text = text[2:].strip()
    
    # Si un SKU est fourni, supprimer toutes les occurrences du SKU dans le texte
    # (sauf si le texte entier est juste le SKU)
    if sku:
        # Pattern pour trouver le SKU comme mot entier (pas comme partie d'un autre nombre)
        # On cherche le SKU au début, au milieu ou à la fin, entouré d'espaces ou de ponctuation
        sku_pattern = re.compile(r"(?:^|\s+)" + re.escape(sku) + r"(?:\s+|$)", re.IGNORECASE)
        # Compter combien de fois le SKU apparaît
        matches = list(sku_pattern.finditer(text))
        if len(matches) > 0:
            # Si le texte entier est juste le SKU (éventuellement avec espaces), le garder
            text_without_sku = sku_pattern.sub("", text).strip()
            if not text_without_sku:
                # Le texte était juste le SKU, on le garde tel quel
                pass
            else:
                # Supprimer toutes les occurrences du SKU
                text = sku_pattern.sub(" ", text).strip()
                # Nettoyer les espaces multiples
                text = re.sub(r"\s+", " ", text)
                # Nettoyer les espaces en début/fin
                text = text.strip()
    
    # Détecter et enlever les répétitions de mots/phrases
    # Ex: "MP 75 MP 75" -> "MP 75"
    # Ex: "Goldband Goldband" -> "Goldband"
    # Ex: "Stabilisé Stabilisé" -> "Stabilisé"
    words = text.split()
    if len(words) >= 2:
        # Vérifier si les premiers mots sont répétés
        # On cherche une répétition dans les 4 premiers mots
        for i in range(1, min(5, len(words) // 2 + 1)):
            first_part = " ".join(words[:i])
            second_part = " ".join(words[i:i*2])
            if first_part == second_part:
                # Répétition détectée, garder seulement la première partie
                text = first_part + " " + " ".join(words[i*2:])
                text = text.strip()
                break
    
    return text


def parse_items(pages: List[Dict]) -> List[Dict]:
    log_debug("--- parse_items STARTED ---")
    items: List[Dict] = []
    
    # Flatten all text into a single list of lines
    all_lines: List[str] = []
    for page in pages:
        words = page.get("words", [])
        if not words:
            continue
        lines_struct = build_lines(words)
        text_lines = [" ".join(t for _, t in line).strip() for line in lines_struct]
        all_lines.extend(text_lines)

    # -------------------------------------------------------------------------
    # PRE-PROCESSING: SPLIT MERGED LINES
    # -------------------------------------------------------------------------
    # Il arrive que plusieurs produits soient sur la même ligne (ou fusionnés car même Y).
    # On scanne chaque ligne pour voir si elle contient des SKU (6 chiffres) ailleurs qu'au début.
    # Si oui, on coupe la ligne pour que chaque SKU commence sa propre ligne.
    
    split_lines = []
    # Regex pour trouver un SKU (6 chiffres isolés)
    # On utilise positif lookbehind/ahead pour s'assurer que c'est isolé
    # mais sans consommer les espaces pour le split
    re_sku_search = re.compile(r"(?<!\d)(\d{6})(?!\d)")
    
    for line in all_lines:
        # Trouver tous les SKUs dans la ligne
        matches = list(re_sku_search.finditer(line))
        
        if not matches:
            split_lines.append(line)
            continue
            
        # Si on a des matches, on découpe
        last_idx = 0
        for m in matches:
            start = m.start()
            
            # Si le SKU n'est pas au tout début (marge de tolérance de 2 chars pour bullet/noise)
            if start > last_idx + 2:
                # Tout ce qui précède appartient à la ligne précédente (ou fin item précédent)
                segment = line[last_idx:start].strip()
                if segment:
                    split_lines.append(segment)
                last_idx = start
            # Si le SKU est quasi au début, on decale juste last_idx pour inclure ce SKU dans le prochain segment
            
        # Ajouter le reste de la ligne (qui commence maintenant par le dernier SKU trouvé)
        remaining = line[last_idx:].strip()
        if remaining:
            split_lines.append(remaining)
            
    all_lines = split_lines

    # -------------------------------------------------------------------------
    # PRE-FILTER: Remove table header blocks
    # -------------------------------------------------------------------------
    # Les en-têtes de tableau peuvent apparaître en bloc avant chaque produit.
    # Stratégie: Un produit commence toujours par un SKU (6 chiffres).
    # Tout ce qui précède un SKU et qui n'est pas un SKU est probablement un en-tête.
    filtered_lines = []
    i = 0
    RE_SKU_CHECK = re.compile(r"^(\d{6})(?:\s+.*)?$")  # Pour vérifier si une ligne commence par un SKU
    
    while i < len(all_lines):
        line = all_lines[i].strip()
        
        # Si la ligne commence par un SKU, c'est le début d'un produit
        if RE_SKU_CHECK.match(line):
            # Garder cette ligne (c'est un SKU)
            filtered_lines.append(all_lines[i])
            i += 1
            continue
        
        # Si c'est un en-tête individuel, l'ignorer
        if is_header_or_footer(line):
            i += 1
            continue
        
        # Si ce n'est pas un SKU et pas un en-tête explicite, vérifier le contexte
        # Si on est dans une séquence d'en-têtes (plusieurs lignes d'en-têtes consécutives),
        # on les ignore toutes
        header_count = 0
        j = i
        while j < len(all_lines):
            check_line = all_lines[j].strip()
            # Si on trouve un SKU, on s'arrête (c'est le début d'un produit)
            if RE_SKU_CHECK.match(check_line):
                break
            # Si c'est un en-tête, on continue à compter
            if is_header_or_footer(check_line):
                header_count += 1
                j += 1
            else:
                # Si ce n'est ni un SKU ni un en-tête, on s'arrête
                break
        
        # Si on a trouvé un bloc d'en-têtes (3+ lignes consécutives), les ignorer
        if header_count >= 3:
            log_debug(f"Bloc d'en-têtes détecté (lignes {i} à {j-1}, {header_count} lignes), ignoré")
            i = j
            continue
        
        # Si ce n'est pas un bloc d'en-têtes, garder la ligne (c'est probablement du contenu de produit)
        filtered_lines.append(all_lines[i])
        i += 1
    
    all_lines = filtered_lines

    # -------------------------------------------------------------------------
    # BUFFER STRATEGY
    # -------------------------------------------------------------------------
    # On accumule les lignes jusqu'au prochain SKU ou fin de fichier.
    # Ensuite on analyse le buffer (Item complet).
    
    RE_SKU_START = re.compile(r"^(\d{6})(?:\s+.*)?$") # Detection de ligne commencant par SKU
    RE_SKU_CAPTURE = re.compile(r"^(\d{6})(?:\s+(.*))?$", re.IGNORECASE)

    current_buffer = []
    
    def process_buffer(lines_buffer):
        if not lines_buffer:
            return

        # La premiere ligne contient le SKU
        first_line = lines_buffer[0]
        m_sku = RE_SKU_CAPTURE.match(first_line)
        if not m_sku:
            return # Should not happen if logic is correct
            
        sku = m_sku.group(1)
        sku_remainder_raw = m_sku.group(2) or ""
        
        # Nettoyer le reste: si le SKU est répété au début, l'enlever
        # Ex: "281765 281765 Nom produit" -> "Nom produit"
        sku_remainder = sku_remainder_raw.strip()
        # Pattern pour détecter un SKU répété au début
        sku_repeat_pattern = re.compile(r"^" + re.escape(sku) + r"(?:\s+|$)", re.IGNORECASE)
        if sku_repeat_pattern.match(sku_remainder):
            # Enlever le SKU répété et les espaces qui suivent
            sku_remainder = sku_repeat_pattern.sub("", sku_remainder).strip()
        
        # Nettoyer les répétitions de mots et supprimer le SKU s'il apparaît dans le texte
        sku_remainder = clean_repetitions(sku_remainder, sku)
        
        # On contruit le texte complet a analyser pour la fin (Stats)
        # On ignore le SKU pour le reste de l'analyse
        # Texte = Remainder ligne 1 + Lignes suivantes
        
        full_text_parts = []
        if sku_remainder.strip():
            full_text_parts.append(sku_remainder.strip())
        full_text_parts.extend(lines_buffer[1:])
        
        full_text = " ".join(full_text_parts)
        
        # 1. Chercher le Suffixe Stats (Weight ... PalType)
        # On cherche a la fin du full_text
        m_suffix = LINE_SUFFIX_RE.search(full_text)
        
        desc = full_text # Par defaut tout est description si pas de stats
        pallet_qty_str = None
        pallet_type_str = None
        moq_str = None
        unit_str = None
        weight_str = None
        desc_nl = None
        desc_fr = None
        
        if m_suffix:
            weight_str = m_suffix.group(1)
            unit_str = m_suffix.group(2).strip()
            moq_str = m_suffix.group(3)
            pallet_qty_str = m_suffix.group(4)
            pallet_type_str = m_suffix.group(5)
            
            # La description est tout ce qui precede
            # MAIS attention, si on a plusieurs lignes dans le buffer:
            # Ligne 1 = Nom (et code)
            # Lignes 2+ = Longue description
            
            full_desc_block = full_text[:m_suffix.start()].strip()
            
            name_part = full_desc_block
            long_desc_part = ""
            
            # Si on a plusieurs lignes d'origine, on peut tenter de séparer
            if len(lines_buffer) > 1:
                # Nom = Reste de la ligne 1 (après SKU)
                name_part = sku_remainder.strip()
                
                # Le reste des lignes = Longue Description
                raw_lines = lines_buffer 
                # Tout ce qui est après la ligne 1 est potentiellement de la description
                # On join tout sauf la ligne 1
                long_desc_part = " ".join(l.strip() for l in raw_lines[1:] if l.strip()).strip()
                
                # Attention: le suffixe stats se trouve a la fin de la DERNIERE ligne.
                # Il faut l'enlever de long_desc_part si présent.
                # Le full_desc_block contient TOUT sauf le suffixe.
                # Si full_desc_block commence par name_part, le reste est la description.
                if full_desc_block.startswith(name_part):
                     # On prend le reste, et on nettoie les espaces
                     long_desc_part = full_desc_block[len(name_part):].strip()

            # Analyse du pattern "Nom NL - Code - Nom FR" dans name_part
            desc_nl = name_part
            desc_fr = name_part
            
            if " - " in name_part:
                parts = name_part.split(" - ")
                if len(parts) >= 3:
                     # Pattern: NL - Code - FR
                     # Ex: "Betonmortel... - 102 - Béton..."
                     desc_nl = parts[0].strip()
                     tech_code = parts[1].strip()
                     desc_fr = " - ".join(parts[2:]).strip()
                elif len(parts) == 2:
                     # Pattern: NL - FR
                     desc_nl = parts[0].strip()
                     desc_fr = parts[1].strip()
            elif "  " in name_part:
                 # Double espace
                 parts = [p for p in name_part.split("  ") if p.strip()]
                 if len(parts) >= 2:
                     desc_nl = parts[0].strip()
                     desc_fr = " ".join(parts[1:]).strip()

            # Assigner long description si non vide
            if long_desc_part:
                long_desc_nl = long_desc_part
                long_desc_fr = long_desc_part
            
        tech_code = None
        long_desc_nl = None
        long_desc_fr = None

        if m_suffix:
            weight_str = m_suffix.group(1)
            unit_str = m_suffix.group(2).strip()
            moq_str = m_suffix.group(3)
            pallet_qty_str = m_suffix.group(4)
            pallet_type_str = m_suffix.group(5)
            
            # La description est tout ce qui precede
            # MAIS attention, si on a plusieurs lignes dans le buffer:
            # Ligne 1 = Nom (et code)
            # Lignes 2+ = Longue description
            
            full_desc_block = full_text[:m_suffix.start()].strip()
            
            # On essaie de séparer le Nom (Ligne 1) de la Longue Description (Lignes suivantes)
            # Pour cela, on regarde dans lines_buffer comment le full_text a été construit
            # full_text = sku_remainder + lines_buffer[1:]
            
            name_part = full_desc_block
            long_desc_part = ""
            
            # Si on a plusieurs lignes d'origine, on peut tenter de séparer
            # Si on a plusieurs lignes d'origine, on peut tenter de séparer
            found_multiline = False
            
            if len(lines_buffer) > 1:
                # Heuristique Multi-lignes (User observation: Line 1 = NL, Line 2 = FR)
                candidate_nl = sku_remainder.strip()
                candidate_fr = lines_buffer[1].strip()
                
                # Inline helper to extract suffix code like "- 102"
                nl_clean = candidate_nl
                code1 = None
                if " - " in candidate_nl:
                    parts = candidate_nl.rsplit(" - ", 1)
                    if len(parts[1].strip()) < 6:
                        nl_clean = parts[0].strip()
                        code1 = parts[1].strip()

                fr_clean = candidate_fr
                code2 = None
                if " - " in candidate_fr:
                     parts = candidate_fr.rsplit(" - ", 1)
                     if len(parts[1].strip()) < 6:
                         fr_clean = parts[0].strip()
                         code2 = parts[1].strip()
                
                desc_nl = nl_clean
                desc_fr = fr_clean
                if code1 or code2:
                    tech_code = code1 or code2
                
                found_multiline = True
                
                # Long description starts at line 3+
                if len(lines_buffer) > 2:
                     long_desc_part = " ".join(l.strip() for l in lines_buffer[2:]).strip()
                else:
                     long_desc_part = ""

            if not found_multiline:
                # Fallback standard
                name_part = sku_remainder.strip()
                
                # Le reste des lignes = Longue Description
                long_desc_lines = [l.strip() for l in lines_buffer[1:]]
                long_desc_part = " ".join(long_desc_lines).strip()
                if full_desc_block.startswith(name_part):
                     long_desc_part = full_desc_block[len(name_part):].strip()

            # Analyse du pattern standard si pas multiline
            if not found_multiline:
                desc_nl = name_part
                desc_fr = name_part
            
            # 1. Nettoyage initial
            # Parfois le nom contient le Technical Code "102"
            
            if not found_multiline and " - " in name_part:
                parts = name_part.split(" - ")
                log_debug(f"DEBUG Split name '{name_part}' -> {parts} LEN={len(parts)}")
                if len(parts) >= 3:
                     # Pattern: NL - Code - FR
                     # Ex: "Betonmortel... - 102 - Béton..."
                     desc_nl = parts[0].strip()
                     tech_code = parts[1].strip()
                     desc_fr = " - ".join(parts[2:]).strip()
                elif len(parts) == 2:
                     # Pattern: NL - FR ... OU NL - Code
                     part1 = parts[0].strip()
                     part2 = parts[1].strip()
                     
                     # HEURISTIQUE: Detection de Code (ex: "102", "C2", "201")
                     # Si part2 est court (< 6 chars) et ressemble à un code
                     is_code = False
                     p2_clean = part2.strip()
                     
                     if len(p2_clean) < 6:
                         has_letters = any(c.isalpha() for c in p2_clean)
                         # Soit regex alphanumeric strict, soit pas de lettres du tout (chiiffres purs)
                         match = re.match(r'^[\d\w./-]+$', p2_clean)
                         if match or not has_letters:
                             is_code = True
                     
                     if is_code:
                         desc_nl = part1
                         tech_code = p2_clean.replace("\n", "").strip()
                         desc_fr = part1 # On fallback sur NL car pas de FR explicite
                     else:
                         desc_nl = part1
                         desc_fr = part2
                         
            elif "  " in name_part:
                 # Double espace
                 parts = [p for p in name_part.split("  ") if p.strip()]
                 
                 if len(parts) >= 2:
                     part1 = parts[0].strip()
                     part2 = " ".join(parts[1:]).strip()
                     
                     # Meme heuristique
                     is_code = False
                     p2_clean = part2.strip()
                     
                     if len(p2_clean) < 6:
                         has_letters = any(c.isalpha() for c in p2_clean)
                         match = re.match(r'^[\d\w./-]+$', p2_clean)
                         if match or not has_letters:
                             is_code = True

                     if is_code:
                         desc_nl = part1
                         tech_code = p2_clean.replace("\n", "").strip()
                         desc_fr = part1
                     else:
                         desc_nl = part1
                         desc_fr = part2

            # Longue description: si le buffer avait plus d'une ligne, on prend le reste comme description
            # On recupere les lignes brutes (sans le SKU)
            raw_lines = lines_buffer 
            if len(raw_lines) > 1:
                # Tout ce qui est après la ligne 1 est potentiellement de la description
                # On join tout sauf la ligne 1
                potential_long_desc = " ".join(raw_lines[1:]).strip()
                
                # Attention: le suffixe stats se trouve a la fin de la DERNIERE ligne.
                # Il faut l'enlever.
                # m_suffix match sur full_text.
                # On va faire simple: on prend full_desc_block et on retire name_part
                if full_desc_block.startswith(name_part):
                    long_desc_part = full_desc_block[len(name_part):].strip()
                
                if long_desc_part:
                    # On duplique pour NL/FR par defaut
                    long_desc_nl = long_desc_part
                    long_desc_fr = long_desc_part
                    
                    # On essaie de splitter la longue description aussi?
                    # Souvent c'est un bloc de texte NL suivi d'un bloc FR.
                    # Pas de delimiteur clair sauf le changement de langue.
                    # On laisse tel quel pour l'instant ou on cherche un double saut de ligne?
                    pass
            
        # 2. Chercher les Dimensions (dans la description)
        # Souvent a la fin de la description
        dims = None
        m_dims = DIMS_RE.search(desc)
        if m_dims:
            # Si a la fin
            if desc.endswith(m_dims.group(0)) or desc.endswith(m_dims.group(0) + " "):
                dims = m_dims.group(0)
                desc = desc[:m_dims.start()].strip()
            # Sinon on extrait quand meme
            else:
                 dims = m_dims.group(0)
        
        # Conversion
        qty = 1.0
        if moq_str:
            try:
                qty = float(moq_str.replace(",", "."))
            except:
                pass
                
        # Si on n'a pas trouve de suffixe stats, c'est peut-etre un "faux positif" de SKU (ex: 2026)
        # On peut decider de filtrer ici.
        # Le log montre que les vrais items ont TOUS une structure de stats.
        if not m_suffix:
            # On ignore les buffers qui n'ont pas la structure de fin.
            # Cela elimine les faux positifs comme "202601" (Version) qui n'ont pas de poids/unite derriere.
            return

        # Nettoyage final: enlever les stats des descriptions
        def clean_stats_from_text(text: Optional[str]) -> Optional[str]:
            """Enlève les stats à la fin d'un texte"""
            if not text:
                return text
            text = text.strip()
            # Chercher et enlever le pattern de stats à la fin
            m = LINE_SUFFIX_RE.search(text)
            if m:
                # Enlever les stats de la fin
                text = text[:m.start()].strip()
            # Enlever aussi les patterns courts de stats comme "0,04 blister 1 480 94"
            stats_short = re.compile(r"\s+\d+[.,]\d{2}\s+(?:blister|zak|sac|pak|paquet)\s+\d+\s+\d+\s+\d+$", re.IGNORECASE)
            text = stats_short.sub("", text).strip()
            return text if text else None
        
        # Nettoyer les descriptions
        desc_nl = clean_stats_from_text(desc_nl)
        desc_fr = clean_stats_from_text(desc_fr)
        long_desc_nl = clean_stats_from_text(long_desc_nl)
        long_desc_fr = clean_stats_from_text(long_desc_fr)
        desc = clean_stats_from_text(desc)
        
        # Nettoyer aussi les répétitions de mots dans les descriptions et supprimer le SKU
        if desc_nl:
            desc_nl = clean_repetitions(desc_nl, sku)
        if desc_fr:
            desc_fr = clean_repetitions(desc_fr, sku)
        if desc:
            desc = clean_repetitions(desc, sku)
        if long_desc_nl:
            long_desc_nl = clean_repetitions(long_desc_nl, sku)
        if long_desc_fr:
            long_desc_fr = clean_repetitions(long_desc_fr, sku)
        
        # Si les noms sont juste des stats, les ignorer
        if desc_nl and is_stats_only(desc_nl):
            desc_nl = None
        if desc_fr and is_stats_only(desc_fr):
            desc_fr = None
        
        # Si on n'a pas de nom valide, utiliser le SKU comme fallback (mais seulement si vraiment nécessaire)
        if not desc_nl and not desc_fr:
            # Si même la description générale est des stats, on utilise le SKU
            if not desc or is_stats_only(desc):
                desc = f"Product {sku}"
            desc_nl = desc
            desc_fr = desc
        
        items.append({
            "sku": sku,
            "ean": None,
            "description": desc,
            "description_nl": desc_nl,
            "description_fr": desc_fr,
            "technical_code": tech_code,
            "long_description_nl": long_desc_nl,
            "long_description_fr": long_desc_fr,
            "qty": qty,
            "unit": unit_str,
            "unit_price": None,
            "line_total": None,
            "dimensions": dims,
            "weight": weight_str,
            "pallet_quantity": pallet_qty_str,
            "pallet_type": pallet_type_str
        })

    for line in all_lines:
        line = line.strip()
        if not line:
            continue
            
        if is_header_or_footer(line):
            continue
            
        # Detection Debut d'Item
        if RE_SKU_START.match(line):
            # Nouveau SKU detecte -> On traite le buffer precedent
            if current_buffer:
                process_buffer(current_buffer)
            # On demarre nouveau buffer
            current_buffer = [line]
        else:
            # Ligne de continuation (desc, stats, dims...)
            if current_buffer:
                current_buffer.append(line)
                
    # Traiter dernier buffer
    if current_buffer:
        process_buffer(current_buffer)

    return items

    log_debug(f"--- parse_items FINISHED. Found {len(items)} items ---")
    return items


# ------------------------------------------------------------
# PARSE PRINCIPAL
# ------------------------------------------------------------

def parse(pdf_raw: Dict) -> Dict:
    pages = pdf_raw.get("pages", [])
    text = pdf_raw.get("full_text", "") or pdf_raw.get("text", "")

    doc_type = extract_type(text)
    number = extract_number(text)
    date = extract_date(text)
    
    items = parse_items(pages)
    
    # Enrichir avec les descriptions techniques (section "technische specificaties")
    specs = extract_technical_specs(text)
    if specs:
        for item in items:
            raw_code = str(item.get("technical_code") or "").strip()
            m_code = re.match(r"(\d{2,5})", raw_code)
            code = m_code.group(1) if m_code else None
            if code:
                # Normaliser le champ technical_code pour faciliter le matching en aval
                item["technical_code"] = code
            if code and code in specs:
                spec = specs[code]
                nl_spec = spec.get("nl")
                fr_spec = spec.get("fr")
                # Longues descriptions NL/FR depuis la table technique
                if nl_spec:
                    item["long_description_nl"] = nl_spec
                    if not item.get("description_nl"):
                        item["description_nl"] = nl_spec
                if fr_spec:
                    item["long_description_fr"] = fr_spec
                # Si la description FR actuelle ressemble à un code, remplace par la version FR
                # MAIS attention à ne pas injecter une longue description ou du bruit (ex: "Unit.") comme Nom
                curr_fr = item.get("description_fr")
                if is_code_like(curr_fr) and fr_spec:
                    clean_fr = fr_spec.replace("\n", " ").strip()
                    # Heuristique: Un nom de produit fait généralement moins de 100 caracteres.
                    # Et doit faire plus de 3 caracteres ("Unit." = 5 donc on filtre aussi sur contenu si besoin)
                    if 3 < len(clean_fr) < 100:
                         item["description_fr"] = clean_fr
                # Ajouter un champ description_en si absent pour alimenter le mapping catalogue
                if not item.get("description_en"):
                    item["description_en"] = nl_spec or fr_spec
                # Si description_nl/fr est le code, remplacer par texte spec
                if is_code_like(item.get("description_nl")) and nl_spec:
                    item["description_nl"] = nl_spec
                if is_code_like(item.get("description_fr")) and fr_spec:
                    item["description_fr"] = fr_spec
            # fallback: si aucune longue description trouvée, essayer de la déduire du corps du texte
            if not item.get("long_description_nl") and not item.get("long_description_fr") and code:
                nl_fb, fr_fb = fallback_long_desc_from_body(text, code)
                if nl_fb and not is_code_like(nl_fb):
                    item["long_description_nl"] = nl_fb
                    if not item.get("description_nl") or is_code_like(item.get("description_nl")) or len((item.get("description_nl") or "")) < 4:
                        item["description_nl"] = nl_fb
                if fr_fb and not is_code_like(fr_fb):
                    item["long_description_fr"] = fr_fb
                    if not item.get("description_fr") or is_code_like(item.get("description_fr")) or len((item.get("description_fr") or "")) < 4:
                        item["description_fr"] = fr_fb
                if not item.get("description_en"):
                    item["description_en"] = nl_fb or fr_fb

    metadata = {
        "type": doc_type,
        "number": number,
        "date": date,
        "client": None,
        "supplier": "COEK",
        "count": len(items),
        "method": "coek_v1"
    }

    return {
        "items": items,
        "metadata": metadata
    }
