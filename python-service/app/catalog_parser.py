"""
Parser pour les catalogues produits PDF.
Détecte et parse les catalogues produits pour extraire les produits selon la structure SQL.
"""
import os
import json
import re
import uuid
import logging
from typing import Dict, List, Optional
from .utils.pdf_extractor import extract_pdf_raw, extract_text_from_pdf

logger = logging.getLogger(__name__)

# Configuration IA
import datetime

def log_debug(msg):
    try:
        with open("d:\\GitHub\\Backup.Web.Api\\python-service\\debug_trace.log", "a", encoding="utf-8") as f:
            timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
            f.write(f"[{timestamp}] {msg}\n")
    except Exception as e:
        print(f"Failed to log: {e}")


def _looks_like_code(value: Optional[str]) -> bool:
    """Détecte une courte valeur de type code technique (ex: 102)."""
    if not value:
        return False
    return re.fullmatch(r"[\d\w./-]{1,6}", str(value).strip() or "") is not None


def _looks_like_table_noise(value: Optional[str]) -> bool:
    """
    Détecte un bloc qui ressemble à un en-tête/ligne de tableau (beaucoup de chiffres,
    plusieurs lignes courtes, mots clés comme Art. Nr., Omschrijving, etc.).
    """
    if not value:
        return False
    txt = str(value).strip()
    # Lignes purement numériques ou suffixes de stats (ex: "25,00 zak-sac 48 48 94")
    if re.match(r"^\d+[.,]\d{2}\s+(zak-sac|sac|pcs?|pc|st|pallet|palette)\b.*$", txt, re.IGNORECASE):
        return True
    if re.fullmatch(r"[0-9\s.,/-]+", txt):
        return True
    # Petites lignes avec mots-clés de tableau
    if len(txt) < 25 and re.search(r"(art\.?\s*nr|omschrijving|description|poids|unit[e]?|eenheid|pallet|bestelh)", txt, re.IGNORECASE):
        return True
    lines = [l.strip() for l in txt.splitlines() if l.strip()]
    if len(lines) >= 3:
        digit_lines = sum(1 for l in lines if re.match(r"^\d{2,}$", l))
        header_hits = sum(
            1 for l in lines
            if re.search(r"(art\.?\s*nr|omschrijving|gewicht|eenheid|pallet)", l, re.IGNORECASE)
        )
        if digit_lines >= 2 or header_hits >= 1:
            return True
    # Lignes courtes contenant explicitement des mots d'en-tête
    if re.search(r"\b(Art\.|Nr\.|Omschrijving|Bestelh\.|pallet|Description|Poids|Unit)\b", txt, re.IGNORECASE):
        return True
    return False


def _short_title(text: Optional[str], ref: str, fallback: Optional[str] = None, allow_ref: bool = True) -> Optional[str]:
    """
    Retourne un titre court et nettoyé (1 ligne, sans stats de tableau).
    """
    if not text:
        if fallback is not None:
            return fallback
        return ref if allow_ref else None

    def _strip_stats(line: str) -> str:
        # Si la ligne contient des stats de tableau (25,00 zak-sac 48 48 94), couper avant
        m = re.match(r"^(.*?)(\s+\d+[.,]\d{2}\s+(zak-sac|sac|pcs?|pc|st|pallet|palette)\b.*)$", line, flags=re.IGNORECASE)
        if m:
            return m.group(1).strip()
        # motif chiffres + chiffres (colonnes qty) sans 'kg' pour ne pas couper "25 kg"
        m2 = re.match(r"^(.*?)(\s+\d+[.,]\d{2}\s+\d+\s+\d+)", line)
        if m2 and not re.search(r"\bkg\b", line, re.IGNORECASE):
            return m2.group(1).strip()
        return line

    # Prendre la première ligne non vide
    for line in str(text).splitlines():
        line = line.strip()
        if not line:
            continue
        # Nettoyer les stats/tablo en bout de ligne
        line = _strip_stats(line)
        # Couper ce qui ressemble à des stats (poids/unité/qty/pallet)
        line = re.sub(r"\s+\d+[.,]\d{2}.*$", "", line).strip()
        # Couper les colonnes répétées
        line = re.sub(r"(zak-sac|stuk-pièce|pcs|pc|st|pallet|palette)\b.*$", "", line, flags=re.IGNORECASE).strip()
        # Ignorer si c'est un code ou trop court
        if _looks_like_code(line) or len(line) < 4:
            continue
        # Ignorer si cela ressemble encore à une ligne de tableau ou trop long
        if _looks_like_table_noise(line):
            continue
        if len(line) > 80:
            # Essayer de couper à la première phrase si possible
            dot_pos = line.find(". ")
            if 0 < dot_pos < 80:
                line = line[:dot_pos]
            else:
                continue
        if line:
            return line[:120]
    if fallback is not None:
        return fallback
    return ref if allow_ref else None

# Fonction pour obtenir les clés API (lecture dynamique au moment de l'appel)
def get_openai_api_key():
    """Récupère la clé API OpenAI depuis les variables d'environnement"""
    return os.getenv("OPENAI_API_KEY")

def get_gemini_api_key():
    """Récupère la clé API Gemini depuis les variables d'environnement"""
    return os.getenv("GEMINI_API_KEY")

# Variables pour compatibilité (mais on utilisera les fonctions)
OPENAI_API_KEY = None  # Sera lu dynamiquement
GEMINI_API_KEY = None  # Sera lu dynamiquement
USE_AI = os.getenv("USE_AI_CATALOG", "true").lower() == "true"


def is_catalog(text: str) -> bool:
    """
    Détecte si un document PDF est un catalogue produit.
    
    Args:
        text: Texte brut du PDF
    
    Returns:
        True si c'est un catalogue, False sinon
    """
    text_lower = text.lower()
    
    # Mots-clés indicateurs de catalogue
    catalog_keywords = [
        "catalogue",
        "catalog",
        "product catalog",
        "produktkatalog",
        "productenlijst",
        "prijslijst",
        "price list",
        "prijsnota",
        "product list",
        "assortiment",
        "product range",
        "complete range",
        "product range presentation"
    ]
    
    # Mots-clés qui indiquent que ce n'est PAS un catalogue
    not_catalog_keywords = [
        "factuur",
        "invoice",
        "faktuur",
        "facture",
        "leveringsbon",
        "delivery note",
        "bon de livraison",
        "verzendnota",
        "afleveringsbon"
    ]
    
    # Vérifier les mots-clés de catalogue
    has_catalog_keyword = any(keyword in text_lower for keyword in catalog_keywords)
    
    # Vérifier qu'il n'y a pas de mots-clés de facture/BL
    has_invoice_keyword = any(keyword in text_lower for keyword in not_catalog_keywords)
    
    # Si on trouve "catalogue" et pas de mots-clés de facture, c'est probablement un catalogue
    if has_catalog_keyword and not has_invoice_keyword:
        return True
    
    # Heuristique supplémentaire : détection par patterns de catalogue
    # Vérifier les métadonnées de facture (si présentes, ce n'est probablement pas un catalogue)
    has_invoice_metadata = bool(
        re.search(r"(?:factuur|invoice|faktuur)\s*(?:nr|number|nummer)\s*:?\s*\d{8,12}", text_lower) or
        re.search(r"(?:datum|date)\s*:?\s*\d{2}[./-]\d{2}[./-]\d{4}", text_lower) or
        re.search(r"(?:client|klant|customer|factuurontvanger)\s*:?\s*[A-Z0-9]", text_lower)
    )
    
    # Si pas de métadonnées de facture et présence de "catalogue", c'est probablement un catalogue
    if has_catalog_keyword and not has_invoice_metadata:
        return True
    
    # Heuristique avancée : détecter les catalogues sans le mot "catalogue"
    # Patterns typiques d'un catalogue :
    # 1. Beaucoup de références produits (REF., REF, référence, etc.)
    ref_patterns = [
        r"\bREF\.?\s*\d{4,8}",
        r"\bREF\s+\d{4,8}",
        r"référence\s*:?\s*\d{4,8}",
        r"reference\s*:?\s*\d{4,8}"
    ]
    ref_count = sum(len(re.findall(pattern, text, re.IGNORECASE)) for pattern in ref_patterns)
    
    # 2. Présence de "PRODUCT RANGE" ou sections de produits
    has_product_sections = bool(
        re.search(r"product\s+range", text_lower) or
        re.search(r"complete\s+range", text_lower) or
        re.search(r"hand\s+tools|power\s+tools|fasteners|furniture", text_lower)
    )
    
    # 3. Présence de spécifications produits (dimensions, poids, etc.)
    has_product_specs = bool(
        re.search(r"(?:length|width|height|diameter|weight|box|pack)\s*:?\s*\d+", text_lower, re.IGNORECASE) or
        re.search(r"\d+\s*(?:mm|cm|kg|g|m)\b", text_lower)
    )
    
    # 4. Pas de métadonnées de facture
    # 5. Document assez long (les catalogues sont généralement longs)
    is_long_document = len(text) > 10000
    
    # Si on a beaucoup de références produits + sections produits + spécifications
    # et pas de métadonnées de facture, c'est probablement un catalogue
    if (ref_count >= 10 and has_product_sections and has_product_specs and 
        not has_invoice_metadata and not has_invoice_keyword and is_long_document):
        logger.info(f"Catalog détecté par heuristique: {ref_count} références, sections produits, spécifications")
        return True
    
    return False


def parse_catalog_with_ai(path: str, ai_provider: str = "openai", max_pages: Optional[int] = None) -> Dict:
    """
    Parse un catalogue produit en utilisant l'IA.
    Traite le catalogue par parties si nécessaire pour extraire tous les produits.
    
    Args:
        path: Chemin vers le fichier PDF du catalogue
        ai_provider: "openai" ou "gemini"
        max_pages: Nombre maximum de pages à traiter (None = toutes les pages, mais limité à 200 par sécurité)
    
    Returns:
        Dictionnaire avec la structure des tables SQL:
        {
            "products": [...],  # Table Products
            "variants": [...],   # Table ProductVariants
            "images": [...],     # Table ProductImages
            "attributes": [...]  # Table ProductAttributeValues
        }
    """
    logger.info(f"Parsing catalogue avec IA ({ai_provider}) depuis {path}")
    
    # Limiter le nombre de pages par sécurité (éviter les plantages mémoire)
    # Par défaut, limiter à 200 pages pour éviter les problèmes de mémoire
    safe_max_pages = max_pages if max_pages is not None else 200
    logger.info(f"Limite de pages: {safe_max_pages} (pour éviter les problèmes de mémoire)")
    
    # Extraire le texte du PDF avec limite de pages
    try:
        pdf_text = extract_text_from_pdf(path, max_pages=safe_max_pages)
    except Exception as e:
        logger.error(f"Erreur lors de l'extraction du PDF: {e}")
        raise
    
    if not pdf_text or len(pdf_text.strip()) < 100:
        logger.warning("PDF vide ou trop court")
        return {
            "products": [],
            "variants": [],
            "images": [],
            "attributes": []
        }
    
    # Vérifier la taille du texte pour éviter les problèmes de mémoire
    text_size_mb = len(pdf_text.encode('utf-8')) / (1024 * 1024)
    logger.info(f"Taille du texte extrait: {text_size_mb:.2f} MB")
    
    # Si le texte est trop gros (> 50 MB), limiter encore plus
    if text_size_mb > 50:
        logger.warning(f"Texte très volumineux ({text_size_mb:.2f} MB). Réduction de la limite de pages à 100.")
        safe_max_pages = min(safe_max_pages, 100)
        pdf_text = extract_text_from_pdf(path, max_pages=safe_max_pages)
        text_size_mb = len(pdf_text.encode('utf-8')) / (1024 * 1024)
        logger.info(f"Nouvelle taille après limitation: {text_size_mb:.2f} MB")
    
    # Déterminer si on doit traiter par parties
    # Réduire la taille des chunks pour éviter les problèmes de mémoire (40000 au lieu de 80000)
    chunk_size = 40000  # Taille réduite pour éviter les problèmes de mémoire
    overlap = 3000  # Chevauchement réduit entre chunks
    
    if len(pdf_text) > chunk_size:
        logger.info(f"Catalogue très long ({len(pdf_text)} caractères), traitement par parties...")
        return _parse_catalog_in_chunks(pdf_text, ai_provider, chunk_size, overlap)
    else:
        # Traitement normal pour les petits catalogues
        return _parse_single_catalog_chunk(pdf_text, ai_provider, chunk_index=0, total_chunks=1)


def _parse_catalog_in_chunks(pdf_text: str, ai_provider: str, chunk_size: int, overlap: int) -> Dict:
    """
    Parse un catalogue en le divisant en plusieurs parties.
    
    Args:
        pdf_text: Texte complet du PDF
        ai_provider: "openai" ou "gemini"
        chunk_size: Taille de chaque chunk
        overlap: Chevauchement entre chunks
    
    Returns:
        Dictionnaire avec tous les produits fusionnés
    """
    # Diviser le texte en chunks avec chevauchement
    chunks = []
    start = 0
    chunk_index = 0
    
    while start < len(pdf_text):
        end = min(start + chunk_size, len(pdf_text))
        chunk_text = pdf_text[start:end]
        chunks.append((chunk_index, chunk_text, start, end))
        start = end - overlap  # Chevauchement pour ne pas perdre de produits
        chunk_index += 1
    
    total_chunks = len(chunks)
    logger.info(f"Catalogue divisé en {total_chunks} parties pour traitement")
    
    # Traiter chaque chunk
    all_products = []
    all_variants = []
    all_images = []
    all_attributes = []
    processed_refs = set()  # Pour éviter les doublons
    
    for chunk_idx, chunk_text, start_pos, end_pos in chunks:
        logger.info(f"Traitement de la partie {chunk_idx + 1}/{total_chunks} (positions {start_pos}-{end_pos})")
        
        try:
            result = _parse_single_catalog_chunk(chunk_text, ai_provider, chunk_idx, total_chunks)
            
            # Fusionner les résultats en évitant les doublons
            for product in result.get("products", []):
                ref = product.get("Reference") or product.get("Sku")
                if ref and ref not in processed_refs:
                    all_products.append(product)
                    processed_refs.add(ref)
            
            # Ajouter les variantes, images et attributs (ils seront associés par ProductId lors de la normalisation)
            all_variants.extend(result.get("variants", []))
            all_images.extend(result.get("images", []))
            all_attributes.extend(result.get("attributes", []))
            
            logger.info(f"Partie {chunk_idx + 1} traitée: {len(result.get('products', []))} nouveaux produits")
            
            # Libérer la mémoire : supprimer les références aux données du chunk
            del result
            del chunk_text
            
            # Petit délai entre les chunks pour éviter de surcharger le système
            if chunk_idx < total_chunks - 1:  # Pas de délai après le dernier chunk
                import time
                time.sleep(0.5)  # Délai de 500ms entre les chunks
            
        except Exception as e:
            logger.error(f"Erreur lors du traitement de la partie {chunk_idx + 1}: {e}")
            # Continuer avec le chunk suivant même en cas d'erreur
            # Continuer avec les autres parties même si une échoue
            continue
    
    logger.info(f"Traitement terminé: {len(all_products)} produits au total extraits de {total_chunks} parties")
    
    return {
        "products": all_products,
        "variants": all_variants,
        "images": all_images,
        "attributes": all_attributes
    }


def _parse_single_catalog_chunk(chunk_text: str, ai_provider: str, chunk_index: int = 0, total_chunks: int = 1) -> Dict:
    """
    Parse un seul chunk du catalogue.
    
    Args:
        chunk_text: Texte du chunk à parser
        ai_provider: "openai" ou "gemini"
        chunk_index: Index du chunk (0-based)
        total_chunks: Nombre total de chunks
    
    Returns:
        Dictionnaire avec les produits extraits
    """
    # Limiter le texte pour éviter les limites de tokens
    text_limit = 40000  # Limite réduite pour éviter les problèmes de mémoire
    truncated_text = chunk_text[:text_limit]
    if len(chunk_text) > text_limit:
        logger.warning(f"Texte tronqué de {len(chunk_text)} à {text_limit} caractères")
    
    # Construire le prompt pour l'IA
    system_prompt = """Tu es un expert en extraction de données de catalogues produits.
Tu dois extraire TOUS les produits d'un CATALOGUE PRODUIT et les retourner sous forme de JSON.

⚠️ IMPORTANT: Ceci est un CATALOGUE PRODUIT, pas une facture.
Un catalogue contient une liste de produits avec leurs caractéristiques, prix, descriptions, etc.
Il n'y a PAS de quantités, pas de client, pas de numéro de facture.

Structure attendue (selon les tables SQL):
{
  "products": [
    {
      "Reference": "REF001",
      "Sku": "SKU001",
      "Barcode": "5413503555628",
      "Gtin": "5413503555628",
      "Name_EN": "Product Name English",
      "Name_FR": "Nom du produit français",
      "Name_NL": "Productnaam Nederlands",
      "Description_EN": "Full description in English",
      "Description_FR": "Description complète en français",
      "Description_NL": "Volledige beschrijving in het Nederlands",
      "ShortDescription_EN": "Short description EN",
      "ShortDescription_FR": "Courte description FR",
      "ShortDescription_NL": "Korte beschrijving NL",
      "SellingPrice": 10.50,
      "CostPrice": 8.00,
      "WeightKg": 0.5,
      "LengthCm": 10.0,
      "WidthCm": 5.0,
      "HeightCm": 3.0,
      "MinOrderQuantity": 1,
      "IsActive": true,
      "variants": [
        {
          "Sku": "SKU001-V1",
          "Barcode": "5413503555629",
          "PriceOverride": null,
          "Weight": 0.6,
          "Length": 11.0,
          "Width": 5.0,
          "Height": 3.0,
          "IsActive": true
        }
      ],
      "images": [
        {
          "Url": "https://example.com/image.jpg",
          "AltText": "Product image",
          "IsMain": true,
          "SortOrder": 1
        }
      ],
      "attributes": [
        {
          "AttributeId": 1,
          "Value": "Red"
        }
      ]
    }
  ]
}

RÈGLES D'EXTRACTION:
- Extrais TOUS les produits du catalogue
- Pour chaque produit, essaie d'extraire tous les champs disponibles
- Si un champ n'est pas trouvé, utilise null
- Les prix doivent être des nombres décimaux (point comme séparateur)
- Les dimensions en cm, poids en kg (convertir mm en cm, g en kg si nécessaire)
- Les variantes sont des variations du même produit (couleurs, tailles, etc.)
- Les images sont les URLs ou chemins des images du produit
- Les attributs sont les caractéristiques du produit (couleur, matériau, etc.)
- Si le catalogue est multilingue, extrais les noms/descriptions dans toutes les langues disponibles

FORMAT TYPIQUE DES CATALOGUES:
- Les produits sont souvent listés avec "REF." ou "REF" suivi d'un code produit
- Les dimensions peuvent être en mm, cm, ou m (convertir en cm)
- Les poids peuvent être en g, kg (convertir en kg)
- Les produits peuvent avoir des variantes (tailles, couleurs) listées dans le même bloc
- Les catégories/sections (ex: "HAND TOOLS", "POWER TOOLS") peuvent être utilisées comme attributs

EXEMPLE D'EXTRACTION:
Si tu vois:
"REF. LENGTH BOX
27717 170mm 6*"

Extrais:
- Reference: "27717"
- Sku: "27717" (ou générer un SKU basé sur la référence)
- LengthCm: 17.0 (convertir 170mm en cm)
- Description: basée sur le contexte (nom du produit dans la section)
- MinOrderQuantity: 6 (si "BOX" indique la quantité minimale)
"""
    
    # Construire le prompt utilisateur selon si c'est un chunk ou le catalogue complet
    if total_chunks > 1:
        chunk_info = f"\n⚠️ ATTENTION: Ceci est la PARTIE {chunk_index + 1} sur {total_chunks} du catalogue.\n"
        chunk_info += "Extrais TOUS les produits de cette partie. Ne t'arrête pas après quelques produits.\n"
        chunk_info += "Si tu vois des références produits (REF., REF, codes numériques), extrais-les TOUTES.\n"
    else:
        chunk_info = ""
    
    user_prompt = f"""Extrais TOUS les produits de ce CATALOGUE PRODUIT et retourne-les sous forme de JSON.
{chunk_info}
Texte du catalogue:
{truncated_text}

IMPORTANT: 
- Retourne UNIQUEMENT le JSON valide et complet, sans texte avant ou après, sans markdown, sans code blocks
- Extrais TOUS les produits que tu peux identifier (priorité aux produits avec REF. ou code référence)
- Ne te limite PAS à 30-50 produits : extrais TOUS les produits visibles dans ce texte
- Pour chaque produit avec "REF." ou code référence, crée une entrée dans le tableau products
- Utilise la référence comme "Reference" et "Sku"
- Extrais les dimensions (LENGTH, WIDTH, HEIGHT, DIAMETER) et convertis-les en cm
- Extrais les poids et convertis-les en kg
- Structure les données selon le format JSON fourni ci-dessus
- Si plusieurs variantes d'un même produit (différentes tailles), crée un produit principal et des variantes
- ASSURE-TOI que le JSON est COMPLET et VALIDE (tous les objets fermés, toutes les virgules correctes)
- Si le JSON risque d'être tronqué, réduis le nombre de détails par produit mais garde TOUS les produits
"""
    
    try:
        # Lire les clés API dynamiquement au moment de l'appel
        gemini_key = get_gemini_api_key()
        openai_key = get_openai_api_key()
        
        if ai_provider.lower() == "gemini" and gemini_key:
            return _parse_with_gemini(system_prompt, user_prompt)
        elif openai_key:
            return _parse_with_openai(system_prompt, user_prompt)
        else:
            # Message d'erreur plus détaillé
            missing_keys = []
            if not openai_key:
                missing_keys.append("OPENAI_API_KEY")
            if ai_provider.lower() == "gemini" and not gemini_key:
                missing_keys.append("GEMINI_API_KEY")
            
            error_msg = (
                f"Aucune clé API IA disponible pour le provider '{ai_provider}'. "
                f"Clés manquantes: {', '.join(missing_keys)}. "
                f"Veuillez configurer les variables d'environnement OPENAI_API_KEY ou GEMINI_API_KEY."
            )
            raise ValueError(error_msg)
    except Exception as e:
        logger.error(f"Erreur lors du parsing du catalogue avec IA: {e}", exc_info=True)
        raise


def _parse_with_openai(system_prompt: str, user_prompt: str) -> Dict:
    """Parse le catalogue avec OpenAI"""
    from openai import OpenAI
    
    openai_key = get_openai_api_key()
    if not openai_key:
        raise ValueError("OPENAI_API_KEY n'est pas définie")
    client = OpenAI(api_key=openai_key)
    model = os.getenv("OPENAI_MODEL", "gpt-4o")
    
    # Limiter la taille du prompt pour éviter les problèmes de mémoire
    prompt_size_mb = (len(system_prompt) + len(user_prompt)) / (1024 * 1024)
    logger.info(f"Appel OpenAI avec modèle {model} (taille prompt: {prompt_size_mb:.2f} MB)")
    
    # Si le prompt est trop gros, tronquer le user_prompt
    if len(user_prompt) > 100000:  # ~100 KB de texte
        logger.warning(f"Prompt très long ({len(user_prompt)} caractères), troncature à 100000 caractères")
        user_prompt = user_prompt[:100000] + "\n\n[... texte tronqué pour éviter les problèmes de mémoire ...]"
    
    response = client.chat.completions.create(
        model=model,
        messages=[
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": user_prompt}
        ],
        response_format={"type": "json_object"},
        temperature=0.1,
        max_tokens=16000  # Plus de tokens pour les catalogues
    )
    
    result = json.loads(response.choices[0].message.content)
    
    # Normaliser la structure
    return _normalize_catalog_structure(result)


def _parse_with_gemini(system_prompt: str, user_prompt: str) -> Dict:
    """Parse le catalogue avec Gemini"""
    import google.generativeai as genai
    
    gemini_key = get_gemini_api_key()
    if not gemini_key:
        raise ValueError("GEMINI_API_KEY n'est pas définie")
    genai.configure(api_key=gemini_key)
    
    # Utiliser la même approche que ai_extractor_gemini.py : lister les modèles disponibles
    # et trouver celui qui fonctionne
    model = None
    model_name_used = None
    
    try:
        # Lister tous les modèles disponibles
        all_models = list(genai.list_models())
        logger.info(f"Total modèles Gemini listés: {len(all_models)}")
        
        # Filtrer les modèles qui supportent generateContent
        available_models = [m for m in all_models if 'generateContent' in m.supported_generation_methods]
        logger.info(f"Modèles avec generateContent: {len(available_models)}")
        
        if not available_models:
            raise ValueError("Aucun modèle Gemini avec generateContent trouvé")
        
        # Prioriser Gemini 1.5 Pro ou 2.0 Flash (plus de capacité) puis les autres
        # Trier les modèles : Pro/Flash en premier
        def model_priority(model_info):
            name = model_info.name.lower()
            if 'pro' in name or '2.0' in name:
                return 0  # Priorité haute
            elif 'flash' in name:
                return 1  # Priorité moyenne
            else:
                return 2  # Priorité basse
        
        available_models_sorted = sorted(available_models, key=model_priority)
        
        # Essayer chaque modèle disponible (en commençant par les plus puissants)
        for model_info in available_models_sorted:
            model_full_name = model_info.name  # Format: "models/gemini-1.5-flash" ou similaire
            model_short_name = model_full_name.split('/')[-1] if '/' in model_full_name else model_full_name
            
            try:
                logger.info(f"🔍 Test du modèle: {model_full_name} (court: {model_short_name})")
                
                # Essayer d'abord avec le nom complet
                try:
                    test_model = genai.GenerativeModel(model_full_name)
                    # Tester avec un petit prompt pour voir si ça fonctionne
                    test_response = test_model.generate_content(
                        "test",
                        generation_config={"temperature": 0.1, "max_output_tokens": 10}
                    )
                    # Si on arrive ici, le modèle fonctionne
                    model = test_model
                    model_name_used = model_short_name
                    logger.info(f"✅ Modèle {model_full_name} testé et fonctionnel - UTILISÉ")
                    break
                except Exception as e1:
                    error_msg = str(e1)
                    logger.debug(f"  Nom complet échoué: {error_msg[:150]}")
                    
                    # Essayer avec le nom court
                    try:
                        test_model = genai.GenerativeModel(model_short_name)
                        test_response = test_model.generate_content(
                            "test",
                            generation_config={"temperature": 0.1, "max_output_tokens": 10}
                        )
                        model = test_model
                        model_name_used = model_short_name
                        logger.info(f"✅ Modèle {model_short_name} testé et fonctionnel - UTILISÉ")
                        break
                    except Exception as e2:
                        error_msg = str(e2)
                        logger.warning(f"  ❌ Nom court aussi échoué: {error_msg[:150]}")
                        continue
                        
            except Exception as e:
                error_msg = str(e)
                logger.debug(f"❌ Erreur lors de la création du modèle {model_full_name}: {error_msg[:150]}")
                continue
        
        if model is None:
            logger.error(f"❌ Aucun modèle Gemini fonctionnel trouvé parmi {len(available_models)} modèles testés")
            raise ValueError(f"Aucun modèle Gemini disponible. Modèles testés: {len(available_models)}")
            
    except Exception as e:
        logger.error(f"Erreur lors de la recherche d'un modèle Gemini: {e}")
        raise
    
    logger.info(f"Appel Gemini avec modèle {model_name_used}")
    
    # Combiner system et user prompt pour Gemini
    full_prompt = f"{system_prompt}\n\n{user_prompt}"
    
    try:
        # Limite de tokens pour les grands catalogues
        # Gemini 1.5 Flash peut gérer jusqu'à 8192 tokens de sortie
        # Gemini 1.5 Pro peut gérer jusqu'à 8192 tokens de sortie (mais plus de contexte)
        # Gemini 2.0 Flash peut gérer jusqu'à 8192 tokens de sortie
        # On utilise 8192 pour garantir une réponse complète
        # Si c'est un modèle Pro, on peut utiliser plus de tokens
        if 'pro' in model_name_used.lower() or '2.0' in model_name_used.lower():
            max_tokens = 8192  # Maximum pour Pro/2.0
        else:
            max_tokens = 8192  # Maximum standard
        
        logger.info(f"Génération avec max_output_tokens={max_tokens} (modèle: {model_name_used})")
        
        response = model.generate_content(
            full_prompt,
            generation_config={
                "temperature": 0.1,
                "max_output_tokens": max_tokens,
                "response_mime_type": "application/json"
            }
        )
    except Exception as e:
        logger.error(f"Erreur lors de l'appel à Gemini: {e}")
        raise ValueError(f"Erreur lors de l'appel à l'API Gemini: {str(e)}")
    
    # Extraire le texte de la réponse
    response_text = response.text if hasattr(response, 'text') else str(response)
    
    # Logger la réponse pour debug (tronquée à 500 caractères)
    logger.info(f"Réponse Gemini (premiers 500 caractères): {response_text[:500]}")
    logger.info(f"Longueur totale de la réponse: {len(response_text)} caractères")
    
    # Essayer de parser le JSON
    try:
        result = json.loads(response_text)
    except json.JSONDecodeError as e:
        # Si le JSON est invalide, essayer de le nettoyer
        logger.warning(f"Erreur de parsing JSON: {e}")
        logger.warning(f"Position de l'erreur: ligne {e.lineno}, colonne {e.colno}")
        
        # Afficher le contexte autour de l'erreur
        lines = response_text.split('\n')
        error_line_idx = e.lineno - 1
        context_start = max(0, error_line_idx - 2)
        context_end = min(len(lines), error_line_idx + 3)
        context = '\n'.join(lines[context_start:context_end])
        logger.warning(f"Contexte autour de l'erreur:\n{context}")
        
        # Essayer de nettoyer le JSON (enlever les markdown code blocks si présents)
        cleaned_text = response_text.strip()
        if cleaned_text.startswith('```json'):
            cleaned_text = cleaned_text[7:]  # Enlever ```json
        if cleaned_text.startswith('```'):
            cleaned_text = cleaned_text[3:]  # Enlever ```
        if cleaned_text.endswith('```'):
            cleaned_text = cleaned_text[:-3]  # Enlever ```
        cleaned_text = cleaned_text.strip()
        
        # Essayer de parser à nouveau
        try:
            result = json.loads(cleaned_text)
            logger.info("JSON nettoyé avec succès")
        except json.JSONDecodeError as e2:
            # Si ça échoue encore, essayer de réparer le JSON tronqué
            logger.warning(f"JSON toujours invalide après nettoyage: {e2}")
            logger.info("Tentative de réparation du JSON tronqué...")
            
            # Essayer de réparer le JSON en fermant les structures ouvertes
            try:
                repaired_json = _repair_truncated_json(cleaned_text)
                result = json.loads(repaired_json)
                logger.info("JSON réparé avec succès")
            except Exception as repair_error:
                logger.error(f"Échec de la réparation du JSON: {repair_error}")
                # Dernière tentative : extraire le JSON valide jusqu'à l'erreur
                try:
                    # Trouver la position de l'erreur et extraire le JSON valide jusqu'à ce point
                    error_pos = e.pos if hasattr(e, 'pos') else len(cleaned_text)
                    # Chercher le dernier objet/variant complet avant l'erreur
                    # Chercher la dernière occurrence de "}," ou "]" avant l'erreur
                    last_complete_pos = max(
                        cleaned_text.rfind('},', 0, error_pos),
                        cleaned_text.rfind(']', 0, error_pos),
                        cleaned_text.rfind('}', 0, error_pos)
                    )
                    
                    if last_complete_pos > 0:
                        # Extraire jusqu'au dernier objet complet + fermer les structures
                        partial_json = cleaned_text[:last_complete_pos + 1]
                        # Fermer les structures ouvertes
                        partial_json = _repair_truncated_json(partial_json)
                        result = json.loads(partial_json)
                        logger.warning(f"JSON partiel extrait (tronqué à la position {last_complete_pos})")
                    else:
                        raise ValueError(f"Impossible d'extraire un JSON valide. Erreur à la ligne {e.lineno}, colonne {e.colno}")
                except Exception as extract_error:
                    logger.error(f"Échec de l'extraction partielle: {extract_error}")
                    # Dernière tentative : chercher un bloc JSON valide dans la réponse
                    import re
                    json_match = re.search(r'\{.*\}', cleaned_text, re.DOTALL)
                    if json_match:
                        try:
                            result = json.loads(json_match.group(0))
                            logger.info("JSON extrait avec regex avec succès")
                        except json.JSONDecodeError:
                            raise ValueError(f"Impossible de parser la réponse JSON de Gemini. Erreur à la ligne {e.lineno}, colonne {e.colno}. Réponse (tronquée): {response_text[:1000]}")
                    else:
                        raise ValueError(f"Impossible de parser la réponse JSON de Gemini. Erreur à la ligne {e.lineno}, colonne {e.colno}. Réponse (tronquée): {response_text[:1000]}")
    
    # Normaliser la structure
    return _normalize_catalog_structure(result)


def _repair_truncated_json(json_text: str) -> str:
    """
    Tente de réparer un JSON tronqué en fermant les structures ouvertes et les chaînes non terminées.
    """
    repaired = json_text.rstrip()
    
    # Étape 1: Gérer les chaînes non terminées
    # Chercher les guillemets non fermés à la fin
    in_string = False
    escape_next = False
    quote_count = 0
    
    # Compter les guillemets pour détecter les chaînes non fermées
    for i, char in enumerate(repaired):
        if escape_next:
            escape_next = False
            continue
        if char == '\\':
            escape_next = True
            continue
        if char == '"':
            quote_count += 1
            in_string = not in_string
    
    # Si on est dans une chaîne à la fin, la fermer
    if in_string:
        # Trouver la position du dernier guillemet ouvrant
        last_quote_pos = repaired.rfind('"')
        if last_quote_pos >= 0:
            # Vérifier si c'est vraiment une chaîne ouverte (pas échappée)
            # Si le caractère avant n'est pas un backslash, c'est une chaîne ouverte
            if last_quote_pos == 0 or repaired[last_quote_pos - 1] != '\\':
                # Fermer la chaîne
                repaired = repaired[:last_quote_pos + 1] + '"'
                logger.info("Chaîne non terminée détectée et fermée")
    
    # Étape 2: Nettoyer les virgules en trop à la fin
    repaired = repaired.rstrip()
    while repaired.endswith(','):
        repaired = repaired[:-1].rstrip()
    
    # Étape 3: Fermer les structures JSON ouvertes
    open_braces = repaired.count('{')
    close_braces = repaired.count('}')
    open_brackets = repaired.count('[')
    close_brackets = repaired.count(']')
    
    # Fermer les tableaux ouverts
    for _ in range(open_brackets - close_brackets):
        repaired = repaired.rstrip()
        # Enlever la virgule finale si présente
        if repaired.endswith(','):
            repaired = repaired[:-1].rstrip()
        repaired += ']'
    
    # Fermer les objets ouverts
    for _ in range(open_braces - close_braces):
        repaired = repaired.rstrip()
        # Enlever la virgule finale si présente
        if repaired.endswith(','):
            repaired = repaired[:-1].rstrip()
        repaired += '}'
    
    # Étape 4: Nettoyer les virgules en trop avant les fermetures
    repaired = repaired.rstrip()
    while repaired.endswith(',}'):
        repaired = repaired[:-2].rstrip() + '}'
    while repaired.endswith(',]'):
        repaired = repaired[:-2].rstrip() + ']'
    
    return repaired


def _normalize_product_name_for_grouping(name: Optional[str]) -> str:
    """
    Normalise un nom de produit pour le regroupement (ignore casse, ponctuation, etc.)
    
    Args:
        name: Nom du produit
    
    Returns:
        Nom normalisé pour le regroupement
    """
    if not name:
        return ""
    # Normaliser: minuscules, supprimer ponctuation, espaces multiples
    normalized = name.lower().strip()
    # Supprimer les virgules et autres ponctuations
    normalized = re.sub(r'[,;:\.!?]+', '', normalized)
    # Remplacer les espaces multiples par un seul espace
    normalized = re.sub(r'\s+', ' ', normalized)
    return normalized.strip()


def _group_products_by_name(products: list, variants: list, is_ffgroup: bool = False) -> tuple:
    """
    Regroupe les produits par nom normalisé et crée un seul produit avec plusieurs variantes.
    
    Args:
        products: Liste des produits à regrouper
        variants: Liste des variantes existantes
    
    Returns:
        Tuple (grouped_products, grouped_variants) où les produits sont regroupés
    """
    # Créer un dictionnaire pour regrouper par nom normalisé
    product_groups = {}
    
    # Créer un mapping SKU -> variant pour retrouver rapidement les variantes
    variant_by_sku = {v.get("Sku") or v.get("_product_ref"): v for v in variants if v.get("Sku") or v.get("_product_ref")}
    
    # Grouper les produits par nom normalisé
    for product in products:
        # Essayer d'obtenir le nom dans toutes les langues
        name_en = product.get("Name_EN") or ""
        name_fr = product.get("Name_FR") or ""
        name_nl = product.get("Name_NL") or ""
        
        # Utiliser le premier nom disponible
        product_name = name_en or name_fr or name_nl or f"Product {product.get('Reference', '')}"
        
        # Normaliser le nom pour le regroupement
        normalized_name = _normalize_product_name_for_grouping(product_name)
        
        # Ignorer les noms trop génériques ou vides
        if not normalized_name or len(normalized_name) < 3:
            normalized_name = f"product_{product.get('Reference', 'unknown')}"
        
        # Si ce groupe n'existe pas encore, le créer
        if normalized_name not in product_groups:
            product_groups[normalized_name] = {
                "products": [],
                "name": product_name,  # Conserver le nom original
                "name_en": name_en,
                "name_fr": name_fr,
                "name_nl": name_nl
            }
        
        product_groups[normalized_name]["products"].append(product)
    
    # Créer les produits regroupés et leurs variantes
    grouped_products = []
    grouped_variants = []
    processed_variant_skus = set()  # Pour suivre les variantes déjà traitées
    
    for normalized_name, group in product_groups.items():
        group_products = group["products"]
        
        # Si un seul produit dans le groupe, pas besoin de regrouper
        if len(group_products) == 1:
            product = group_products[0].copy()
            # Pour FFGroup, ne pas mettre de SKU sur le produit (seulement sur les variantes)
            if is_ffgroup:
                product["Sku"] = None
            grouped_products.append(product)
            # Ajouter la variante correspondante si elle existe
            ref = product.get("Reference") or product.get("Sku")
            if ref:
                processed_variant_skus.add(ref)
                if ref in variant_by_sku:
                    variant = variant_by_sku[ref].copy()
                    # S'assurer que la variante a un ProductId
                    product_id = group_products[0].get("Id") or str(uuid.uuid4())
                    variant["ProductId"] = product_id
                    variant["Id"] = variant.get("Id") or str(uuid.uuid4())
                    variant.pop("_product_ref", None)
                    grouped_variants.append(variant)
                else:
                    # Créer une variante par défaut si elle n'existe pas
                    product_id = group_products[0].get("Id") or str(uuid.uuid4())
                    variant = {
                        "Id": str(uuid.uuid4()),
                        "ProductId": product_id,
                        "Sku": ref,
                        "Barcode": group_products[0].get("Barcode"),
                        "PriceOverride": None,
                        "Weight": group_products[0].get("WeightKg"),
                        "Length": group_products[0].get("LengthCm"),
                        "Width": group_products[0].get("WidthCm"),
                        "Height": group_products[0].get("HeightCm"),
                        "IsActive": True
                    }
                    grouped_variants.append(variant)
            continue
        
        # Plusieurs produits avec le même nom -> regrouper en un seul produit avec plusieurs variantes
        # Utiliser le premier produit comme base
        main_product = group_products[0]
        main_ref = main_product.get("Reference") or main_product.get("Sku")
        
        # Créer le produit principal (utiliser le nom du groupe)
        # Pour FFGroup et autres catalogues où les produits n'ont pas de SKU,
        # ne pas mettre de SKU sur le produit (seulement sur les variantes)
        grouped_product = {
            "Id": main_product.get("Id") or str(uuid.uuid4()),
            "Reference": main_ref,  # Utiliser la première référence comme référence principale
            "Sku": None,  # Les produits regroupés n'ont pas de SKU, seulement les variantes
            "Barcode": main_product.get("Barcode"),
            "Gtin": main_product.get("Gtin"),
            "Name_EN": group["name_en"] or group["name"],
            "Name_FR": group["name_fr"] or group["name"],
            "Name_NL": group["name_nl"] or group["name"],
            "Description_EN": main_product.get("Description_EN"),
            "Description_FR": main_product.get("Description_FR"),
            "Description_NL": main_product.get("Description_NL"),
            "ShortDescription_EN": main_product.get("ShortDescription_EN"),
            "ShortDescription_FR": main_product.get("ShortDescription_FR"),
            "ShortDescription_NL": main_product.get("ShortDescription_NL"),
            "SellingPrice": main_product.get("SellingPrice"),
            "CostPrice": main_product.get("CostPrice"),
            "WeightKg": main_product.get("WeightKg"),
            "LengthCm": main_product.get("LengthCm"),
            "WidthCm": main_product.get("WidthCm"),
            "HeightCm": main_product.get("HeightCm"),
            "MinOrderQuantity": main_product.get("MinOrderQuantity"),
            "IsActive": main_product.get("IsActive", True)
        }
        
        grouped_products.append(grouped_product)
        product_id = grouped_product["Id"]
        
        # Créer une variante pour chaque produit du groupe
        for product in group_products:
            ref = product.get("Reference") or product.get("Sku")
            
            # Chercher la variante existante ou en créer une nouvelle
            if ref in variant_by_sku:
                variant = variant_by_sku[ref].copy()
            else:
                variant = {
                    "Sku": ref,
                    "Barcode": None,
                    "PriceOverride": None,
                    "Weight": None,
                    "Length": None,
                    "Width": None,
                    "Height": None,
                    "IsActive": True
                }
            
            # Mettre à jour la variante avec les données du produit
            variant["ProductId"] = product_id
            variant["Id"] = variant.get("Id") or str(uuid.uuid4())
            variant["Sku"] = ref
            variant["Barcode"] = product.get("Barcode") or variant.get("Barcode")
            variant["Length"] = product.get("LengthCm") or variant.get("Length")
            variant["Width"] = product.get("WidthCm") or variant.get("Width")
            variant["Height"] = product.get("HeightCm") or variant.get("Height")
            variant["Weight"] = product.get("WeightKg") or variant.get("Weight")
            variant["PriceOverride"] = product.get("SellingPrice") or variant.get("PriceOverride")
            variant["IsActive"] = product.get("IsActive", True)
            
            grouped_variants.append(variant)
            processed_variant_skus.add(ref)
    
    # Ajouter les variantes qui n'ont pas été regroupées (pas de produit correspondant)
    for variant_sku, variant in variant_by_sku.items():
        if variant_sku not in processed_variant_skus:
            # Cette variante n'a pas de produit correspondant, on la garde quand même
            grouped_variants.append(variant.copy())
    
    return grouped_products, grouped_variants


def _normalize_catalog_structure(result: Dict) -> Dict:
    """
    Normalise la structure du résultat pour correspondre aux tables SQL.
    
    Args:
        result: Résultat brut (peut avoir variants/images/attributes dans products ou comme listes séparées)
    
    Returns:
        Structure normalisée avec products, variants, images, attributes
    """
    products = result.get("products", [])
    
    # Récupérer les variantes, images et attributs (peuvent être dans result ou dans chaque product)
    all_variants = result.get("variants", [])
    all_images = result.get("images", [])
    all_attributes = result.get("attributes", [])
    
    # Détecter si c'est un catalogue FFGroup (vérifier dans les métadonnées ou le texte)
    is_ffgroup = result.get("_is_ffgroup", False)
    # Vérifier aussi dans les produits si le fournisseur est FFGroup
    if not is_ffgroup:
        # Chercher des indices FFGroup dans les produits (par exemple, si beaucoup de produits ont le même pattern)
        # Pour l'instant, on détecte FFGroup si on a des produits regroupés sans SKU explicite
        # ou si le texte contient "ff group" ou "ffgroup"
        pass  # On laisse la détection au niveau supérieur
    
    # Regrouper les produits par nom avant la normalisation
    products, all_variants = _group_products_by_name(products, all_variants, is_ffgroup=is_ffgroup)
    
    normalized_products = []
    
    for i, product in enumerate(products):
        # Utiliser un GUID pour l'Id produit (ou conserver s'il existe déjà)
        product_id = product.get("Id") or str(uuid.uuid4())
        product_ref = product.get("Reference") or product.get("Sku")
        
        # Extraire les variantes depuis le produit (si présentes)
        variants = product.pop("variants", [])
        for variant in variants:
            variant["ProductId"] = product_id
            # Générer un GUID pour l'Id de la variante (ou conserver s'il existe déjà)
            if "Id" not in variant or not variant.get("Id"):
                variant["Id"] = str(uuid.uuid4())
            all_variants.append(variant)
        
        # Extraire les images depuis le produit (si présentes)
        images = product.pop("images", [])
        for img_idx, image in enumerate(images):
            image["ProductId"] = product_id
            # Générer un GUID pour l'Id de l'image (ou conserver s'il existe déjà)
            if "Id" not in image or not image.get("Id"):
                image["Id"] = str(uuid.uuid4())
            image["SortOrder"] = image.get("SortOrder", img_idx + 1)
            all_images.append(image)
        
        # Extraire les attributs depuis le produit (si présents)
        attributes = product.pop("attributes", [])
        for attr in attributes:
            attr["ProductId"] = product_id
            # Générer un GUID pour l'Id de l'attribut (ou conserver s'il existe déjà)
            if "Id" not in attr or not attr.get("Id"):
                attr["Id"] = str(uuid.uuid4())
            all_attributes.append(attr)
        
        # Associer les variantes et attributs de la liste séparée à ce produit
        # (par SKU/Reference)
        for variant in all_variants:
            if variant.get("ProductId") is None:
                variant_sku = variant.get("Sku") or variant.get("_product_ref")
                if variant_sku == product_ref:
                    variant["ProductId"] = product_id
                    # Générer un GUID pour l'Id de la variante si elle n'en a pas
                    if "Id" not in variant or not variant.get("Id"):
                        variant["Id"] = str(uuid.uuid4())
                    # Nettoyer la référence temporaire
                    variant.pop("_product_ref", None)
        
        for attr in all_attributes:
            if attr.get("ProductId") is None or attr.get("ProductId") == product_id:
                # Les attributs peuvent être associés par ProductId ou par position
                if attr.get("ProductId") == product_id or (attr.get("ProductId") is None and len([a for a in all_attributes if a.get("ProductId") == product_id]) == 0):
                    attr["ProductId"] = product_id
                    # Générer un GUID pour l'Id de l'attribut si il n'en a pas
                    if "Id" not in attr or not attr.get("Id"):
                        attr["Id"] = str(uuid.uuid4())
        
        # Ajouter l'ID au produit
        product["Id"] = product_id
        normalized_products.append(product)
    
    # S'assurer que tous les variants et attributes ont un ProductId
    # Utiliser la référence du produit pour associer
    for variant in all_variants:
        if variant.get("ProductId") is None:
            variant_sku = variant.get("Sku") or variant.get("_product_ref")
            if variant_sku:
                # Trouver le produit correspondant par référence
                for product in normalized_products:
                    if product.get("Reference") == variant_sku or product.get("Sku") == variant_sku:
                        variant["ProductId"] = product.get("Id")
                        # Générer un GUID pour l'Id de la variante si elle n'en a pas
                        if "Id" not in variant or not variant.get("Id"):
                            variant["Id"] = str(uuid.uuid4())
                        # Nettoyer la référence temporaire
                        variant.pop("_product_ref", None)
                        break
                # Si pas trouvé par référence, chercher dans les variantes déjà regroupées
                if variant.get("ProductId") is None:
                    # Chercher dans les variantes déjà associées pour trouver le ProductId
                    for other_variant in all_variants:
                        if other_variant.get("Sku") == variant_sku and other_variant.get("ProductId"):
                            variant["ProductId"] = other_variant.get("ProductId")
                            if "Id" not in variant or not variant.get("Id"):
                                variant["Id"] = str(uuid.uuid4())
                            variant.pop("_product_ref", None)
                            break
        # S'assurer que la variante a un Id (GUID)
        if "Id" not in variant or not variant.get("Id"):
            variant["Id"] = str(uuid.uuid4())
    
    # S'assurer que toutes les images ont un Id (GUID)
    for image in all_images:
        if "Id" not in image or not image.get("Id"):
            image["Id"] = str(uuid.uuid4())
    
    for attr in all_attributes:
        if attr.get("ProductId") is None:
            product_ref = attr.get("_product_ref")
            if product_ref:
                # Trouver le produit correspondant par référence
                for product in normalized_products:
                    if product.get("Reference") == product_ref or product.get("Sku") == product_ref:
                        attr["ProductId"] = product.get("Id")
                        # Générer un GUID pour l'Id de l'attribut si il n'en a pas
                        if "Id" not in attr or not attr.get("Id"):
                            attr["Id"] = str(uuid.uuid4())
                        attr.pop("_product_ref", None)
                        break
            else:
                # Si pas de référence, utiliser l'index (pour les attributs créés par position)
                attr_idx = all_attributes.index(attr)
                if attr_idx < len(normalized_products):
                    attr["ProductId"] = normalized_products[attr_idx].get("Id")
                    # Générer un GUID pour l'Id de l'attribut si il n'en a pas
                    if "Id" not in attr or not attr.get("Id"):
                        attr["Id"] = str(uuid.uuid4())
        # S'assurer que l'attribut a un Id (GUID)
        if "Id" not in attr or not attr.get("Id"):
            attr["Id"] = str(uuid.uuid4())
    
    return {
        "products": normalized_products,
        "variants": all_variants,
        "images": all_images,
        "attributes": all_attributes
    }


def parse_catalog_basic(path: str) -> Dict:
    """
    Parse un catalogue produit sans IA, en utilisant des règles de parsing basiques.
    
    Args:
        path: Chemin vers le fichier PDF du catalogue
    
    Returns:
        Dictionnaire avec la structure des tables SQL
    """
    logger.info("Parsing de catalogue sans IA (méthode basique)")
    
    # Extraire le texte brut du PDF
    pdf_raw = extract_pdf_raw(path)
    text = pdf_raw.get('full_text', '')
    
    # IMPRIMER LE TEXTE BRUT POUR AMÉLIORER LE PARSING
    print("=" * 100)
    print("[PARSING NON-IA] TEXTE BRUT DU PDF")
    print("=" * 100)
    print(f"Longueur totale du texte: {len(text)} caractères")
    print(f"Nombre de lignes: {text.count(chr(10)) + text.count(chr(13))}")
    print("\n" + "-" * 100)
    print("PREMIERS 2000 CARACTÈRES:")
    print("-" * 100)
    print(text[:2000])
    print("\n" + "-" * 100)
    print("DERNIERS 2000 CARACTÈRES:")
    print("-" * 100)
    print(text[-2000:] if len(text) > 2000 else text)
    print("\n" + "-" * 100)
    print("ÉCHANTILLON AU MILIEU (positions 25%-75%):")
    print("-" * 100)
    if len(text) > 4000:
        mid_start = len(text) // 4
        mid_end = mid_start + 2000
        print(text[mid_start:mid_end])
    print("=" * 100)
    
    # Sauvegarder aussi le texte complet dans un fichier pour analyse
    try:
        import os
        from pathlib import Path
        # Créer un dossier pour les textes extraits
        output_dir = Path(path).parent / "extracted_texts"
        output_dir.mkdir(exist_ok=True)
        # Nom du fichier basé sur le PDF original
        pdf_name = Path(path).stem
        text_file = output_dir / f"{pdf_name}_extracted_text.txt"
        with open(text_file, 'w', encoding='utf-8') as f:
            f.write(text)
        print(f"[PARSING NON-IA] Texte brut sauvegardé dans: {text_file}")
        logger.info(f"Texte brut sauvegardé dans: {text_file}")
    except Exception as e:
        logger.warning(f"Impossible de sauvegarder le texte brut: {e}")
    
    # Initialiser toutes les listes de résultats
    products = []
    variants = []
    images = []  # Liste des images (vide pour le parsing basique)
    attributes = []
    
    # Mots-clés à exclure des noms de produits
    excluded_keywords = [
        "MULTIPLE ORDER QUANTITY", "ORDER QUANTITY", "QUANTITY",
        "REF.", "REF", "REFERENCE", "LENGTH", "WIDTH", "HEIGHT",
        "BOX", "PCS", "PIECES", "PRODUCT RANGE", "COMPLETE RANGE",
        "HAND TOOLS", "POWER TOOLS", "FASTENERS", "FURNITURE FITTINGS",
        "INCLUDES", "INCLUDE",  # Mots non pertinents pour les noms de produits
        "SWEDISH", "HEAT", "FORGED", "WITH FAST ADJUSTMENT",  # Attributs, pas des noms complets
        "CUTTING POWER", "DIAMETER", "CAPACITY"
    ]
    
    # Pattern amélioré pour extraire les lignes de produits
    # Format observé dans le PDF:
    # 1. Format avec REF. en-tête:
    #    REF.
    #    LENGTH
    #    BOX
    #    27717
    #    170mm
    #    6*
    #
    # 2. Format avec REF. et références sur la même ligne:
    #    REF.
    #    LENGTH
    #    BOX
    #    27710
    #    160mm
    #    6
    #    27711
    #    180mm
    #    6
    #
    # 3. Format avec références et dimensions sur la même ligne:
    #    27420 160mm
    #    6*
    #
    # 4. Format avec références seules:
    #    REF.
    #    45092
    #    REF.
    #    45094
    
    # Pattern pour capturer les blocs REF./LENGTH/BOX suivis de références
    # Ce pattern cherche les références qui suivent un bloc "REF.\nLENGTH\nBOX"
    ref_block_pattern = re.compile(
        r'REF\.\s*\n\s*LENGTH\s*\n\s*BOX\s*\n'
        r'((?:\d{4,8}\s*\n\s*(?:\d+(?:\.\d+)?\s*(?:mm|cm|m|"|inch)\s*\n\s*)?(?:\d+\*?\s*\n\s*)?)+)',
        re.IGNORECASE | re.MULTILINE
    )
    
    # Pattern pour les références avec dimensions sur la même ligne (format: "27420 160mm")
    product_line_with_dim_pattern = re.compile(
        r'^(\d{4,8})\s+(\d+(?:\.\d+)?)\s*(mm|cm|m|"|inch)\s*$',
        re.IGNORECASE | re.MULTILINE
    )
    
    # Pattern pour les références seules après "REF." (format: "REF.\n45092")
    ref_only_pattern = re.compile(
        r'REF\.\s*\n\s*(\d{4,8})\s*$',
        re.IGNORECASE | re.MULTILINE
    )
    
    # Pattern amélioré pour extraire les lignes de produits (format classique)
    product_line_pattern = re.compile(
        r'(?:REF\.?|REFERENCE)\s+(\d{4,8})\s+'
        r'(?:(\d+(?:\.\d+)?)\s*(?:mm|cm|m|"|inch)?\s*)?'  # LENGTH (optionnel)
        r'(?:(\d+(?:\.\d+)?)\s*(?:mm|cm|m|"|inch)?\s*)?'  # WIDTH (optionnel)
        r'(?:(\d+(?:\.\d+)?)\s*(?:mm|cm|m|"|inch)?\s*)?'  # HEIGHT (optionnel)
        r'(?:BOX\s*:?\s*(\d+)\*?)?',  # BOX quantity (optionnel, peut avoir *)
        re.IGNORECASE | re.MULTILINE
    )
    
    # Pattern pour les références sans "REF." (format: "27717 170mm 6*")
    # Format: nombre (4-8 chiffres) suivi de dimensions
    product_line_no_ref_pattern = re.compile(
        r'^(\d{4,8})\s+'
        r'(?:(\d+(?:\.\d+)?)\s*(mm|cm|m|"|inch)\s*)?'  # LENGTH avec unité
        r'(?:\s*(\d+\*?)\s*)?$',  # BOX quantity ou nombre avec *
        re.IGNORECASE | re.MULTILINE
    )
    
    # Pattern encore plus simple: juste un nombre de 4-8 chiffres suivi d'un espace et d'autres caractères
    # (pour capturer les formats comme "27717\n170mm\n6*" sur plusieurs lignes)
    simple_product_pattern = re.compile(
        r'\b(\d{4,8})\b(?=\s*(?:\d|mm|cm|m|"|inch|BOX|\*))',  # Référence suivie de dimensions/quantités
        re.IGNORECASE | re.MULTILINE
    )
    
    # Pattern simple pour les références seules
    ref_pattern = re.compile(
        r'\b(?:REF\.?|REFERENCE)\s+(\d{4,8})\b',
        re.IGNORECASE
    )
    
    # Pattern encore plus simple: juste les nombres de 4-8 chiffres qui ressemblent à des références
    # (pas des années, pas des dimensions)
    simple_ref_pattern = re.compile(
        r'\b(\d{4,8})\b(?!\s*(?:mm|cm|m|"|inch|kg|g|°|%))',  # Pas suivi d'une unité
        re.IGNORECASE
    )
    
    # Initialiser processed_refs et section_names AVANT de les utiliser dans le parser multi-lignes
    processed_refs = set()
    
    # Dictionnaire pour stocker les noms de produits par section
    section_names = {}
    current_section = None
    
    # Chercher les sections de produits (titres en majuscules) AVANT le parser multi-lignes
    section_pattern = re.compile(r'^([A-Z][A-Z\s\-&,()]+?)(?:\n|$)', re.MULTILINE)
    sections = section_pattern.finditer(text)
    for section_match in sections:
        section_name = section_match.group(1).strip()
        # Exclure les mots-clés non pertinents
        if not any(excluded in section_name.upper() for excluded in excluded_keywords):
            # Filtrer les mots isolés qui sont des attributs, pas des noms de produits
            words = section_name.split()
            # Accepter seulement si c'est un nom composé (2+ mots) ou un mot long (8+ caractères)
            if (len(section_name) >= 5 and len(section_name) < 50 and 
                (len(words) >= 2 or (len(words) == 1 and len(section_name) >= 8))):
                # Exclure les mots isolés courts qui sont des attributs
                if section_name.upper() not in ['SWEDISH', 'HEAT', 'FORGED', 'INCLUDES', 'INCLUDE']:
                    section_pos = section_match.end()
                    section_names[section_pos] = section_name
    
    # PARSER LE FORMAT MULTI-LIGNES OBSERVÉ DANS LE PDF
    # Format: REF.\nLENGTH\nBOX\n27717\n170mm\n6*\n27718\n200mm\n6*\nBENT NOSE PLIERS
    # On va parser ce format spécifique en cherchant les blocs REF./LENGTH/BOX
    print("[PARSING NON-IA] Parsing du format multi-lignes REF./LENGTH/BOX...")
    
    # Pattern pour trouver les blocs "REF.\nLENGTH\nBOX" suivis de références
    ref_block_header = re.compile(
        r'REF\.\s*\n\s*LENGTH\s*\n\s*BOX\s*\n',
        re.IGNORECASE | re.MULTILINE
    )
    
    # Trouver tous les blocs REF./LENGTH/BOX
    block_matches = list(ref_block_header.finditer(text))
    print(f"[PARSING NON-IA] Blocs REF./LENGTH/BOX trouvés: {len(block_matches)}")
    
    # Parser chaque bloc pour extraire les produits
    for block_idx, block_match in enumerate(block_matches):
        if block_idx % 10 == 0:  # Afficher la progression tous les 10 blocs
            print(f"[PARSING NON-IA] Traitement du bloc {block_idx + 1}/{len(block_matches)}...")
        
        block_start = block_match.end()
        # Trouver la fin du bloc (prochain "REF." ou section de produit)
        block_end = len(text)
        next_ref = ref_block_header.search(text, block_start + 50)
        if next_ref:
            block_end = next_ref.start()
        
        block_text = text[block_start:block_end]
        
        # Parser les lignes du bloc : format alterné ref/dimension/quantité
        lines = [line.strip() for line in block_text.split('\n') if line.strip()]
        
        i = 0
        current_ref = None
        current_dim = None
        current_qty = None
        block_product_name = None  # Nom du produit pour tout le bloc (ex: "BENT NOSE PLIERS")
        
        # Chercher le nom du produit à la fin du bloc (après toutes les références)
        # Format: "27715\n170mm\n6*\n27716\n200mm\n6*\nBENT NOSE PLIERS"
        # On cherche dans les dernières lignes, en excluant les lignes qui sont des références ou dimensions
        for j in range(len(lines) - 1, max(0, len(lines) - 15), -1):  # Chercher dans les 15 dernières lignes
            candidate = lines[j].strip()
            # Ignorer les lignes vides, les références (nombres), les dimensions (avec mm/cm), les quantités
            if (not candidate or 
                re.match(r'^\d{4,8}$', candidate) or  # Référence
                re.match(r'^\d+(?:\.\d+)?\s*(mm|cm|m|"|inch)$', candidate, re.IGNORECASE) or  # Dimension
                re.match(r'^\d+\*?$', candidate) or  # Quantité
                candidate.upper() in ['REF', 'LENGTH', 'BOX', 'DIAMETER', 'CAPACITY', 'INCLUDES', 'INCLUDE']):
                continue
            
            # Vérifier si c'est un nom de produit (majuscules, plusieurs mots généralement)
            if (re.match(r'^[A-Z][A-Z\s\-&,()]+$', candidate) and 
                len(candidate) >= 5 and len(candidate) <= 50 and  # Au moins 5 caractères pour éviter les mots isolés
                not re.match(r'^\d+$', candidate) and
                # Exclure les mots isolés qui sont des attributs
                candidate.upper() not in ['SWEDISH', 'HEAT', 'FORGED', 'INCLUDES', 'INCLUDE', 'WITH FAST ADJUSTMENT'] and
                not any(excluded in candidate.upper() for excluded in excluded_keywords)):
                # Vérifier que ce n'est pas juste un mot isolé (attribut) mais un vrai nom de produit
                words = candidate.split()
                if len(words) >= 2 or (len(words) == 1 and len(candidate) >= 8):  # Au moins 2 mots ou 1 mot de 8+ caractères
                    block_product_name = candidate
                    break
        
        while i < len(lines):
            line = lines[i]
            
            # Si c'est une référence (4-8 chiffres)
            if re.match(r'^\d{4,8}$', line):
                # Si on a déjà une référence, créer le produit précédent
                if current_ref:
                    if current_ref not in processed_refs:
                        # Utiliser le nom du bloc si disponible
                        block_pos_with_name = block_start + block_match.start()
                        if block_product_name:
                            # Stocker temporairement le nom du produit dans section_names pour ce bloc
                            section_names[block_pos_with_name] = block_product_name
                        _create_product_from_block(
                            current_ref, current_dim, current_qty,
                            block_pos_with_name, text, section_names,
                            excluded_keywords, products, variants, images, attributes, processed_refs
                        )
                
                current_ref = line
                current_dim = None
                current_qty = None
                i += 1
                
                # Regarder les lignes suivantes pour dimension et quantité
                if i < len(lines):
                    next_line = lines[i]
                    
                    # Ignorer si c'est le nom du produit (majuscules, plusieurs mots généralement)
                    if (re.match(r'^[A-Z][A-Z\s\-&,()]+$', next_line) and 
                        len(next_line) >= 5 and  # Au moins 5 caractères
                        next_line.upper() not in ['REF', 'LENGTH', 'BOX', 'DIAMETER', 'CAPACITY', 'INCLUDES', 'INCLUDE']):
                        words = next_line.split()
                        # Si c'est un nom composé (2+ mots) ou un mot long, c'est probablement le nom du produit
                        if len(words) >= 2 or (len(words) == 1 and len(next_line) >= 8):
                            i += 1
                            continue  # Continuer la boucle while
                    
                    # Dimension (format: 170mm, 20cm, etc.)
                    dim_match = re.match(r'^(\d+(?:\.\d+)?)\s*(mm|cm|m|"|inch)$', next_line, re.IGNORECASE)
                    if dim_match:
                        current_dim = next_line
                        i += 1
                        
                        # Quantité (format: 6, 6*, 12, etc.)
                        if i < len(lines):
                            # Ignorer si c'est le nom du produit
                            if (re.match(r'^[A-Z][A-Z\s\-&,()]+$', lines[i]) and 
                                len(lines[i]) >= 5 and
                                lines[i].upper() not in ['REF', 'LENGTH', 'BOX', 'DIAMETER', 'CAPACITY', 'INCLUDES', 'INCLUDE']):
                                words = lines[i].split()
                                if len(words) >= 2 or (len(words) == 1 and len(lines[i]) >= 8):
                                    i += 1
                                    continue  # Continuer la boucle while
                            qty_match = re.match(r'^(\d+)\*?$', lines[i])
                            if qty_match:
                                current_qty = lines[i]
                                i += 1
                    # Quantité seule (sans dimension)
                    elif re.match(r'^\d+\*?$', next_line):
                        current_qty = next_line
                        i += 1
                    else:
                        # Si ce n'est ni un nom, ni une dimension, ni une quantité, on passe à la ligne suivante
                        i += 1
            else:
                i += 1
        
        # Créer le dernier produit du bloc
        if current_ref and current_ref not in processed_refs:
            block_pos_with_name = block_start + block_match.start()
            if block_product_name:
                section_names[block_pos_with_name] = block_product_name
            _create_product_from_block(
                current_ref, current_dim, current_qty,
                block_pos_with_name, text, section_names,
                excluded_keywords, products, variants, images, attributes, processed_refs
            )
    
    # Extraire toutes les références avec "REF." (pour les formats simples)
    ref_matches = ref_pattern.findall(text)
    logger.info(f"Trouvé {len(ref_matches)} références avec 'REF.'")
    print(f"[PARSING NON-IA] Références avec 'REF.' trouvées: {len(ref_matches)}")
    if ref_matches:
        print(f"[PARSING NON-IA] Exemples de références: {ref_matches[:10]}")
    
    # Extraire les lignes de produits avec leurs informations (avec REF.) - format classique
    product_lines = list(product_line_pattern.finditer(text))
    print(f"[PARSING NON-IA] Lignes de produits avec pattern 'REF.' (format classique): {len(product_lines)}")
    logger.info(f"Trouvé {len(product_lines)} lignes de produits avec dimensions (format REF.)")
    
    # Extraire aussi les lignes sans "REF." (format: "27717 170mm 6*")
    product_lines_no_ref = list(product_line_no_ref_pattern.finditer(text))
    logger.info(f"Trouvé {len(product_lines_no_ref)} lignes de produits avec dimensions (sans REF.)")
    print(f"[PARSING NON-IA] Lignes de produits sans 'REF.' explicite: {len(product_lines_no_ref)}")
    if product_lines_no_ref:
        print(f"[PARSING NON-IA] Exemple de ligne sans REF.: {product_lines_no_ref[0].group(0)[:100]}")
    
    # Extraire aussi les références simples (format très basique)
    simple_products = list(simple_product_pattern.finditer(text))
    logger.info(f"Trouvé {len(simple_products)} références simples potentielles")
    
    # Traiter aussi les formats classiques (si pas déjà traité par le parser multi-lignes)
    # Combiner toutes les listes (en évitant les doublons)
    all_product_lines = product_lines + product_lines_no_ref
    # Ajouter les simples seulement si on n'a pas assez de produits
    if len(products) < 50 and len(all_product_lines) < 20:
        all_product_lines.extend(simple_products[:100])  # Limiter à 100 supplémentaires
    
    # (section_names et processed_refs sont déjà définis plus haut, avant le parser multi-lignes)
    
    # Traiter les lignes de produits avec dimensions
    
    for match in all_product_lines:
        ref = match.group(1)
        # Les groupes peuvent être None si le pattern simple est utilisé
        try:
            length_str = match.group(2)
        except (IndexError, AttributeError):
            length_str = None
        try:
            width_str = match.group(3)
        except (IndexError, AttributeError):
            width_str = None
        try:
            height_str = match.group(4)
        except (IndexError, AttributeError):
            height_str = None
        try:
            box_qty = match.group(5)
        except (IndexError, AttributeError):
            box_qty = None
        
        if ref in processed_refs:
            continue
        
        processed_refs.add(ref)
        
        # Convertir les dimensions en cm
        length_cm = None
        width_cm = None
        height_cm = None
        
        if length_str:
            # Extraire la valeur numérique et l'unité
            dim_match = re.match(r'^(\d+(?:\.\d+)?)\s*(mm|cm|m|"|inch)?$', str(length_str), re.IGNORECASE)
            if dim_match:
                length_val = float(dim_match.group(1))
                unit = dim_match.group(2).lower() if dim_match.group(2) else None
                if unit == 'mm':
                    length_cm = length_val / 10.0
                elif unit == 'cm' or unit is None:
                    length_cm = length_val
                elif unit == 'm':
                    length_cm = length_val * 100
                elif unit in ('"', 'inch'):
                    length_cm = length_val * 2.54
                else:
                    # Si pas d'unité, supposer mm si valeur > 10, sinon cm
                    length_cm = length_val / 10.0 if length_val > 10 else length_val
            else:
                # Essayer de convertir directement si c'est juste un nombre
                try:
                    length_val = float(length_str)
                    length_cm = length_val / 10.0 if length_val > 10 else length_val
                except ValueError:
                    pass
        
        if width_str:
            # Extraire la valeur numérique et l'unité
            dim_match = re.match(r'^(\d+(?:\.\d+)?)\s*(mm|cm|m|"|inch)?$', str(width_str), re.IGNORECASE)
            if dim_match:
                width_val = float(dim_match.group(1))
                unit = dim_match.group(2).lower() if dim_match.group(2) else None
                if unit == 'mm':
                    width_cm = width_val / 10.0
                elif unit == 'cm' or unit is None:
                    width_cm = width_val
                elif unit == 'm':
                    width_cm = width_val * 100
                elif unit in ('"', 'inch'):
                    width_cm = width_val * 2.54
                else:
                    width_cm = width_val / 10.0 if width_val > 10 else width_val
            else:
                try:
                    width_val = float(width_str)
                    width_cm = width_val / 10.0 if width_val > 10 else width_val
                except ValueError:
                    pass
        
        if height_str:
            # Extraire la valeur numérique et l'unité
            dim_match = re.match(r'^(\d+(?:\.\d+)?)\s*(mm|cm|m|"|inch)?$', str(height_str), re.IGNORECASE)
            if dim_match:
                height_val = float(dim_match.group(1))
                unit = dim_match.group(2).lower() if dim_match.group(2) else None
                if unit == 'mm':
                    height_cm = height_val / 10.0
                elif unit == 'cm' or unit is None:
                    height_cm = height_val
                elif unit == 'm':
                    height_cm = height_val * 100
                elif unit in ('"', 'inch'):
                    height_cm = height_val * 2.54
                else:
                    height_cm = height_val / 10.0 if height_val > 10 else height_val
            else:
                try:
                    height_val = float(height_str)
                    height_cm = height_val / 10.0 if height_val > 10 else height_val
                except ValueError:
                    pass
        
        # Trouver le nom du produit depuis la section la plus proche
        match_start = match.start()
        product_name = None
        
        # Chercher la section la plus proche avant cette référence
        for section_pos, section_name in sorted(section_names.items()):
            if section_pos < match_start and match_start - section_pos < 500:
                product_name = section_name
                break
        
        # Si pas de section trouvée, chercher dans le contexte immédiat
        if not product_name:
            context_start = max(0, match_start - 150)
            context = text[context_start:match_start]
            # Chercher un nom de produit (ligne en majuscules avant la référence)
            name_match = re.search(r'^([A-Z][A-Z\s\-&]+?)(?:\n|REF\.?|REFERENCE)', context, re.MULTILINE)
            if name_match:
                candidate_name = name_match.group(1).strip()
                # Vérifier que ce n'est pas un mot-clé exclu
                if not any(excluded in candidate_name.upper() for excluded in excluded_keywords):
                    if 3 < len(candidate_name) < 50:
                        product_name = candidate_name
        
        # Utiliser un nom par défaut si rien trouvé
        if not product_name:
            product_name = f"Product {ref}"
        
        # Créer le produit
        product = {
            "Reference": ref,
            "Sku": ref,
            "Barcode": None,
            "Gtin": None,
            "Name_EN": product_name,
            "Name_FR": None,
            "Name_NL": None,
            "Description_EN": None,
            "Description_FR": None,
            "Description_NL": None,
            "ShortDescription_EN": None,
            "ShortDescription_FR": None,
            "ShortDescription_NL": None,
            "SellingPrice": None,
            "CostPrice": None,
            "WeightKg": None,
            "LengthCm": length_cm,
            "WidthCm": width_cm,
            "HeightCm": height_cm,
            "MinOrderQuantity": int(box_qty) if box_qty else None,
            "IsActive": True
        }
        products.append(product)
        
        # Créer une variante pour chaque produit (même sans dimensions)
        # Le ProductId sera assigné lors de la normalisation
        variant = {
            "Sku": ref,
            "Barcode": None,
            "PriceOverride": None,
            "Weight": None,
            "Length": length_cm,
            "Width": width_cm,
            "Height": height_cm,
            "IsActive": True,
            "_product_ref": ref  # Référence pour l'association lors de la normalisation
        }
        variants.append(variant)
        
        # Chercher les attributs (CrV, S2 STEEL, etc.) dans un contexte plus large
        context_start = max(0, match_start - 300)
        context_end = min(len(text), match_start + 300)
        context = text[context_start:context_end]
        
        # Patterns pour les attributs (plus complets)
        attribute_patterns = [
            r'\b(CrV|CrMo|Cr[VM])\b',  # Chrome Vanadium, Chrome Molybdenum
            r'\b(S2\s+STEEL|S2\s+Alloy\s+Steel|S2)\b',
            r'\b(FORGED|DROP\s+FORGED|DROP\s+CHROME\s+VANADIUM)\b',
            r'\b(CHROME\s+VANADIUM|VANADIUM|CHROME)\b',
            r'\b(STEEL|STAINLESS\s+STEEL|CARBON\s+STEEL)\b',
            r'\b(HEAT\s+TREATMENT|57-60HRC|HRC)\b',
        ]
        
        found_attributes = set()  # Pour éviter les doublons
        
        for pattern in attribute_patterns:
            attr_matches = re.finditer(pattern, context, re.IGNORECASE)
            for attr_match in attr_matches:
                attr_value = attr_match.group(1).strip()
                # Normaliser la valeur
                attr_value_upper = attr_value.upper()
                # Éviter les doublons et les valeurs trop courtes
                if attr_value_upper not in found_attributes and len(attr_value) >= 2:
                    found_attributes.add(attr_value_upper)
                    attributes.append({
                        "AttributeId": None,
                        "Value": attr_value,
                        "_product_ref": ref  # Référence pour l'association lors de la normalisation
                    })
    
    # Si on n'a pas assez de produits, chercher aussi les références simples
    if len(products) < 50:
        logger.info(f"Seulement {len(products)} produits trouvés, recherche des références simples")
        # Chercher les références simples (nombres de 4-8 chiffres)
        simple_refs = simple_ref_pattern.findall(text)
        # Filtrer pour éviter les doublons et les nombres qui ne sont pas des références
        unique_simple_refs = []
        seen_refs = set(ref_matches + [p.get('Reference') for p in products])
        for ref in simple_refs:
            if ref not in seen_refs and len(ref) >= 4:
                # Vérifier que ce n'est pas une année (1900-2100)
                if not (1900 <= int(ref) <= 2100):
                    unique_simple_refs.append(ref)
                    seen_refs.add(ref)
                    if len(unique_simple_refs) >= 200:  # Limiter à 200
                        break
        
        logger.info(f"Trouvé {len(unique_simple_refs)} références simples supplémentaires")
        
        # Ajouter les références restantes (avec REF. d'abord)
        for ref in ref_matches:
            if ref not in processed_refs:
                # Trouver le nom depuis la section la plus proche
                ref_pos = text.find(f"REF. {ref}")
                if ref_pos == -1:
                    ref_pos = text.find(f"REF {ref}")
                
                product_name = None
                if ref_pos > 0:
                    for section_pos, section_name in sorted(section_names.items()):
                        if section_pos < ref_pos and ref_pos - section_pos < 500:
                            product_name = section_name
                            break
                
                if not product_name:
                    product_name = f"Product {ref}"
                
                product = {
                    "Reference": ref,
                    "Sku": ref,
                    "Barcode": None,
                    "Gtin": None,
                    "Name_EN": product_name,
                    "Name_FR": None,
                    "Name_NL": None,
                    "Description_EN": None,
                    "Description_FR": None,
                    "Description_NL": None,
                    "ShortDescription_EN": None,
                    "ShortDescription_FR": None,
                    "ShortDescription_NL": None,
                    "SellingPrice": None,
                    "CostPrice": None,
                    "WeightKg": None,
                    "LengthCm": None,
                    "WidthCm": None,
                    "HeightCm": None,
                    "MinOrderQuantity": None,
                    "IsActive": True
                }
                products.append(product)
                processed_refs.add(ref)
                
                # Créer aussi une variante pour ce produit (même sans dimensions)
                variant = {
                    "Sku": ref,
                    "Barcode": None,
                    "PriceOverride": None,
                    "Weight": None,
                    "Length": None,
                    "Width": None,
                    "Height": None,
                    "IsActive": True,
                    "_product_ref": ref  # Référence pour l'association
                }
                variants.append(variant)
                
                # Chercher les attributs dans le contexte
                ref_pos = text.find(f"REF. {ref}")
                if ref_pos == -1:
                    ref_pos = text.find(f"REF {ref}")
                if ref_pos == -1:
                    ref_pos = text.find(ref)
                
                if ref_pos > 0:
                    context_start = max(0, ref_pos - 300)
                    context_end = min(len(text), ref_pos + 300)
                    context = text[context_start:context_end]
                    
                    attribute_patterns = [
                        r'\b(CrV|CrMo|Cr[VM])\b',
                        r'\b(S2\s+STEEL|S2\s+Alloy\s+Steel|S2)\b',
                        r'\b(FORGED|DROP\s+FORGED|DROP\s+CHROME\s+VANADIUM)\b',
                        r'\b(CHROME\s+VANADIUM|VANADIUM|CHROME)\b',
                        r'\b(STEEL|STAINLESS\s+STEEL|CARBON\s+STEEL)\b',
                    ]
                    
                    found_attrs = set()
                    for pattern in attribute_patterns:
                        attr_matches = re.finditer(pattern, context, re.IGNORECASE)
                        for attr_match in attr_matches:
                            attr_value = attr_match.group(1).strip()
                            attr_value_upper = attr_value.upper()
                            if attr_value_upper not in found_attrs and len(attr_value) >= 2:
                                found_attrs.add(attr_value_upper)
                                attributes.append({
                                    "AttributeId": None,
                                    "Value": attr_value,
                                    "_product_ref": ref  # Référence pour l'association
                                })
                
                # Limiter à 200 produits maximum
                if len(products) >= 200:
                    break
        
        # Ajouter aussi les références simples trouvées
        for ref in unique_simple_refs:
            if ref not in processed_refs:
                # Trouver le nom depuis la section la plus proche
                ref_pos = text.find(ref)
                
                product_name = None
                if ref_pos > 0:
                    for section_pos, section_name in sorted(section_names.items()):
                        if section_pos < ref_pos and ref_pos - section_pos < 500:
                            product_name = section_name
                            break
                
                if not product_name:
                    product_name = f"Product {ref}"
                
                product = {
                    "Reference": ref,
                    "Sku": ref,
                    "Barcode": None,
                    "Gtin": None,
                    "Name_EN": product_name,
                    "Name_FR": None,
                    "Name_NL": None,
                    "Description_EN": None,
                    "Description_FR": None,
                    "Description_NL": None,
                    "ShortDescription_EN": None,
                    "ShortDescription_FR": None,
                    "ShortDescription_NL": None,
                    "SellingPrice": None,
                    "CostPrice": None,
                    "WeightKg": None,
                    "LengthCm": None,
                    "WidthCm": None,
                    "HeightCm": None,
                    "MinOrderQuantity": None,
                    "IsActive": True
                }
                products.append(product)
                processed_refs.add(ref)
                
                # Créer aussi une variante pour ce produit (même sans dimensions)
                variant = {
                    "Sku": ref,
                    "Barcode": None,
                    "PriceOverride": None,
                    "Weight": None,
                    "Length": None,
                    "Width": None,
                    "Height": None,
                    "IsActive": True,
                    "_product_ref": ref  # Référence pour l'association
                }
                variants.append(variant)
                
                # Chercher les attributs dans le contexte
                ref_pos = text.find(ref)
                if ref_pos > 0:
                    context_start = max(0, ref_pos - 300)
                    context_end = min(len(text), ref_pos + 300)
                    context = text[context_start:context_end]
                    
                    attribute_patterns = [
                        r'\b(CrV|CrMo|Cr[VM])\b',
                        r'\b(S2\s+STEEL|S2\s+Alloy\s+Steel|S2)\b',
                        r'\b(FORGED|DROP\s+FORGED|DROP\s+CHROME\s+VANADIUM)\b',
                        r'\b(CHROME\s+VANADIUM|VANADIUM|CHROME)\b',
                        r'\b(STEEL|STAINLESS\s+STEEL|CARBON\s+STEEL)\b',
                    ]
                    
                    found_attrs = set()
                    for pattern in attribute_patterns:
                        attr_matches = re.finditer(pattern, context, re.IGNORECASE)
                        for attr_match in attr_matches:
                            attr_value = attr_match.group(1).strip()
                            attr_value_upper = attr_value.upper()
                            if attr_value_upper not in found_attrs and len(attr_value) >= 2:
                                found_attrs.add(attr_value_upper)
                                attributes.append({
                                    "AttributeId": None,
                                    "Value": attr_value,
                                    "_product_ref": ref  # Référence pour l'association
                                })
                
                # Limiter à 200 produits maximum
                if len(products) >= 200:
                    break
    
    # S'assurer que toutes les variables sont définies (sécurité)
    if 'images' not in locals():
        images = []
    if 'variants' not in locals():
        variants = []
    if 'attributes' not in locals():
        attributes = []
    if 'products' not in locals():
        products = []
    
    logger.info(f"Parsing basique terminé: {len(products)} produits, {len(variants)} variantes, {len(attributes)} attributs")
    print("=" * 100)
    print(f"[PARSING NON-IA] RÉSULTAT FINAL:")
    print(f"  - Produits extraits: {len(products)}")
    print(f"  - Variantes extraites: {len(variants)}")
    print(f"  - Attributs extraits: {len(attributes)}")
    print(f"  - Images extraites: {len(images)}")
    if products:
        print(f"[PARSING NON-IA] Exemples de produits extraits:")
        for i, p in enumerate(products[:5]):
            print(f"  {i+1}. Ref: {p.get('Reference')}, Name: {p.get('Name_EN', 'N/A')[:50]}")
    print("=" * 100)
    
    # Normaliser la structure avant de retourner
    result = {
        "products": products,
        "variants": variants,
        "images": images,
        "attributes": attributes
    }
    return _normalize_catalog_structure(result)


def _create_product_from_block(ref: str, dim: Optional[str], qty: Optional[str],
                                block_pos: int, text: str, section_names: dict,
                                excluded_keywords: list, products: list, variants: list,
                                images: list, attributes: list, processed_refs: set):
    """
    Crée un produit à partir d'un bloc REF./LENGTH/BOX.
    
    Args:
        ref: Référence du produit
        dim: Dimension (ex: "170mm")
        qty: Quantité (ex: "6*")
        block_pos: Position du bloc dans le texte
        text: Texte complet
        section_names: Dictionnaire des noms de sections
        excluded_keywords: Mots-clés à exclure
        products: Liste des produits
        variants: Liste des variantes
        images: Liste des images
        attributes: Liste des attributs
        processed_refs: Set des références déjà traitées
    """
    if ref in processed_refs:
        return
    
    processed_refs.add(ref)
    
    # Parser la dimension
    length_cm = None
    if dim:
        dim_match = re.match(r'^(\d+(?:\.\d+)?)\s*(mm|cm|m|"|inch)$', dim, re.IGNORECASE)
        if dim_match:
            value = float(dim_match.group(1))
            unit = dim_match.group(2).lower()
            if unit == 'mm':
                length_cm = value / 10.0
            elif unit == 'cm':
                length_cm = value
            elif unit == 'm':
                length_cm = value * 100
            elif unit in ('"', 'inch'):
                length_cm = value * 2.54
    
    # Parser la quantité
    box_qty = None
    if qty:
        qty_match = re.match(r'^(\d+)\*?$', qty)
        if qty_match:
            box_qty = int(qty_match.group(1))
    
    # Trouver le nom du produit
    product_name = None
    
    # 1. Chercher dans les sections AVANT le bloc (méthode originale)
    for section_pos, section_name in sorted(section_names.items()):
        if section_pos < block_pos and block_pos - section_pos < 500:
            product_name = section_name
            break
    
    # 2. Chercher APRÈS le bloc (pour FFGroup: "REF.\nLENGTH\nBOX\n27717\n170mm\n6*\nBENT NOSE PLIERS")
    if not product_name:
        context_start = block_pos
        context_end = min(len(text), block_pos + 1000)  # Augmenter la fenêtre
        context = text[context_start:context_end]
        
        # Chercher un nom de produit (ligne en majuscules) APRÈS le bloc
        # Pattern pour détecter les noms de produits (majuscules, 3-50 caractères, pas juste des mots-clés)
        name_patterns = [
            r'^([A-Z][A-Z\s\-&,()]+?)(?:\n|REF\.?|$)',  # Ligne en majuscules
            r'\n([A-Z][A-Z\s\-&,()]{3,50})\n(?!REF\.|LENGTH|BOX)',  # Ligne en majuscules entre deux sauts de ligne
        ]
        
        for pattern in name_patterns:
            name_match = re.search(pattern, context, re.MULTILINE)
            if name_match:
                candidate_name = name_match.group(1).strip()
                # Filtrer les mots-clés non pertinents
                excluded_for_names = excluded_keywords + ['REF', 'LENGTH', 'BOX', 'DIAMETER', 'CAPACITY', 'INCLUDES']
                if not any(excluded in candidate_name.upper() for excluded in excluded_for_names):
                    if 3 < len(candidate_name) < 50:
                        # Vérifier que ce n'est pas juste un nombre ou un code
                        if not re.match(r'^\d+$', candidate_name):
                            product_name = candidate_name
                            break
        
        # 3. Si toujours pas trouvé, chercher dans les sections APRÈS le bloc
        if not product_name:
            for section_pos, section_name in sorted(section_names.items()):
                if section_pos > block_pos and section_pos - block_pos < 500:
                    product_name = section_name
                    break
    
    if not product_name:
        product_name = f"Product {ref}"
    
    # Créer le produit
    product = {
        "Reference": ref,
        "Sku": ref,
        "Barcode": None,
        "Gtin": None,
        "Name_EN": product_name,
        "Name_FR": None,
        "Name_NL": None,
        "Description_EN": None,
        "Description_FR": None,
        "Description_NL": None,
        "ShortDescription_EN": None,
        "ShortDescription_FR": None,
        "ShortDescription_NL": None,
        "SellingPrice": None,
        "CostPrice": None,
        "WeightKg": None,
        "LengthCm": length_cm,
        "WidthCm": None,
        "HeightCm": None,
        "MinOrderQuantity": box_qty,
        "IsActive": True
    }
    products.append(product)
    
    # Créer la variante
    variant = {
        "Sku": ref,
        "Barcode": None,
        "PriceOverride": None,
        "Weight": None,
        "Length": length_cm,
        "Width": None,
        "Height": None,
        "IsActive": True,
        "_product_ref": ref
    }
    variants.append(variant)
    
    # Chercher les attributs dans le contexte
    context_start = max(0, block_pos - 300)
    context_end = min(len(text), block_pos + 300)
    context = text[context_start:context_end]
    
    attribute_patterns = [
        r'\b(CrV|CrMo|Cr[VM])\b',
        r'\b(S2\s+STEEL|S2\s+Alloy\s+Steel|S2)\b',
        r'\b(FORGED|DROP\s+FORGED|DROP\s+CHROME\s+VANADIUM)\b',
        r'\b(CHROME\s+VANADIUM|VANADIUM|CHROME)\b',
        r'\b(STEEL|STAINLESS\s+STEEL|CARBON\s+STEEL)\b',
    ]
    
    found_attributes = set()
    for pattern in attribute_patterns:
        attr_matches = re.finditer(pattern, context, re.IGNORECASE)
        for attr_match in attr_matches:
            attr_value = attr_match.group(1).strip()
            attr_value_upper = attr_value.upper()
            if attr_value_upper not in found_attributes and len(attr_value) >= 2:
                found_attributes.add(attr_value_upper)
                attributes.append({
                    "AttributeId": None,
                    "Value": attr_value,
                    "_product_ref": ref
                })
    
    # Cette fonction modifie les listes passées en paramètre, elle ne retourne rien
    # Le résultat sera normalisé dans parse_catalog_basic


def parse_catalog(path: str, use_ai: bool = True, ai_provider: str = "openai", max_pages: Optional[int] = None) -> Dict:
    """
    Parse un catalogue produit.
    
    Args:
        path: Chemin vers le fichier PDF
        use_ai: Si True, utilise l'IA pour l'extraction. Si False, utilise le parsing basique.
        ai_provider: "openai" ou "gemini" (utilisé seulement si use_ai=True)
        max_pages: Nombre maximum de pages à traiter (None = toutes les pages, mais limité à 200 par sécurité pour l'IA)
    
    Returns:
        Dictionnaire avec la structure des tables SQL
    """
    # D'abord, essayer de détecter un fournisseur spécifique via la factory
    # car les parsers spécifiques (comme CoekParser) sont plus précis que le parser générique
    is_ffgroup = False
    try:
        log_debug(f"START parsing: {path}")
        log_debug("[DEBUG] Tentative d'import de parser_factory...")
        from .parsers.parser_factory import create_parser
        from .parsers.generic_parser import GenericParser
        
        # Créer le parser via la factory (qui détectera le fournisseur)
        log_debug(f"[DEBUG] Création du parser pour {path}...")
        parser = create_parser(path)
        log_debug(f"[DEBUG] Parser créé: {type(parser)}")
        
        # Si ce n'est pas le parser générique, c'est qu'un fournisseur spécifique a été détecté
        if not isinstance(parser, GenericParser):
            supplier_name = parser.__class__.__name__.replace("Parser", "")
            log_debug(f"[DEBUG] Fournisseur spécifique détecté: {supplier_name}. Utilisation du parser dédié.")
            
            # Détecter si c'est FFGroup
            is_ffgroup = "FFGroup" in supplier_name or "FF Group" in supplier_name or "FFGROUP" in supplier_name.upper()
            
            # Parser le document
            # Certains parsers (comme CoekParser) ont une méthode parse()
            # D'autres (héritant de BaseParser) utilisent extract_products()
            if hasattr(parser, "parse"):
                log_debug("Calling parser.parse()")
                items = parser.parse()
            else:
                log_debug("Calling parser.extract_products()")
                items = parser.extract_products()
                
            log_debug(f"[DEBUG] Parser spécifique a retourné {len(items)} items")
            
            if items:
                # Convertir la liste d'items en format catalogue
                products = []
                variants = []
                
                for i, item in enumerate(items):
                    # Générer un ID produit GUID
                    product_id = str(uuid.uuid4())
                    
                    # Récupérer les données
                    ref = item.get("reference") or item.get("sku") or f"UNKNOWN-{i}"
                    desc = item.get("description", f"Product {ref}")
                    
                    # Normalisation des descriptions avant mapping
                    desc_fr_raw = item.get("description_fr")
                    desc_fr_clean = desc_fr_raw if not _looks_like_code(desc_fr_raw) else None
                    desc_nl_clean = item.get("description_nl") or desc
                    desc_en_clean = item.get("description_en")

                    # Filtrer le bruit (en-têtes de tableaux)
                    if _looks_like_table_noise(desc_nl_clean):
                        desc_nl_clean = None
                    if _looks_like_table_noise(desc_fr_clean):
                        desc_fr_clean = None
                    if _looks_like_table_noise(desc_en_clean):
                        desc_en_clean = None
                    
                    # Noms (Court)
                    base_short = desc or ref
                    name_nl = _short_title(desc_nl_clean or item.get("description") or item.get("long_description_nl"), ref, fallback=base_short, allow_ref=True)
                    
                    fr_source = desc_fr_clean or item.get("long_description_fr") or item.get("description_fr")
                    # Si la source FR est très longue, préférer le NL pour le nom court
                    if fr_source and len(str(fr_source)) > 80:
                        fr_source = None
                    # Ne pas forcer un fallback NL sur le nom FR si absent
                    name_fr = _short_title(fr_source, ref, fallback=None, allow_ref=False)
                    
                    # EN: privilégier une source dédiée, sinon FR (plutôt que NL)
                    long_desc_en = item.get("long_description_en")
                    en_source = desc_en_clean or long_desc_en or fr_source or item.get("long_description_fr")
                    if en_source and len(str(en_source)) > 80:
                        en_source = None
                    # Ne pas propager le NL en dernier recours sur le nom EN
                    name_en = _short_title(en_source, ref, fallback=None, allow_ref=False)
                    
                    # Descriptions (Longues si dispo, sinon Nom)
                    long_desc_nl = item.get("long_description_nl") or desc_nl_clean or name_nl
                    long_desc_fr = item.get("long_description_fr") or desc_fr_clean or name_fr
                    # EN longue: privilégier EN, sinon FR; pas de fallback NL pour éviter le copier-coller NL
                    if not long_desc_en:
                        long_desc_en = item.get("long_description_en") or long_desc_fr
                    
                    # Nettoyage: si la valeur FR ressemble à un code (ex: "102"), on préfère la longue description
                    if _looks_like_code(name_fr):
                        name_fr = long_desc_fr or name_nl
                    # Ne pas injecter le NL si FR/EN manquent
                    
                    desc_nl_for_product = long_desc_nl or desc_nl_clean or name_nl
                    # FR: pas de fallback NL pour éviter la duplication
                    desc_fr_for_product = long_desc_fr or desc_fr_clean
                    # EN: pas de fallback NL; si rien en EN, prendre FR comme dernier recours
                    desc_en_for_product = long_desc_en or desc_en_clean or long_desc_fr or desc_fr_clean
                    
                    if _looks_like_code(desc_fr_for_product):
                        desc_fr_for_product = long_desc_fr
                    
                    # Créer le produit
                    product = {
                        "Id": product_id,
                        "Reference": ref,
                        "Sku": ref,
                        "Barcode": item.get("ean"),
                        "Name_EN": name_en,
                        "Name_FR": name_fr,
                        "Name_NL": name_nl,
                        "Description_FR": desc_fr_for_product,
                        "Description_NL": desc_nl_for_product,
                        "Description_EN": desc_en_for_product,
                        "SellingPrice": item.get("price"),
                        "MinOrderQuantity": item.get("min_qty") or item.get("quantity"),
                        "WeightKg": item.get("weight"),
                        "LengthCm": item.get("length"), 
                        "WidthCm": item.get("width"),
                        "HeightCm": item.get("height"),
                        "IsActive": True
                    }
                    
                    products.append(product)
                    
                    # Construction des attributs additionnels pour la variante
                    variant_attributes = {}
                    if item.get("unit"):
                        variant_attributes["Unit"] = item.get("unit")
                    if item.get("pallet_quantity"):
                        variant_attributes["PalletQuantity"] = item.get("pallet_quantity")
                    if item.get("pallet_type"):
                        variant_attributes["PalletType"] = item.get("pallet_type")
                    if item.get("min_qty"):
                         variant_attributes["MinOrderQuantity"] = item.get("min_qty")
                    if item.get("technical_code"):
                         variant_attributes["TechnicalCode"] = item.get("technical_code")

                    # Créer une variante par défaut
                    variant = {
                        "Id": str(uuid.uuid4()),  # GUID pour l'Id de la variante
                        "ProductId": product_id,
                        "Sku": ref,
                        "Length": item.get("length"),
                        "Width": item.get("width"),
                        "Height": item.get("height"), 
                        "Weight": item.get("weight"),
                        "AttributesJson": json.dumps(variant_attributes) if variant_attributes else None,
                        "IsActive": True
                    }
                    variants.append(variant)
                
                log_debug(f"[DEBUG] Conversion terminée: {len(products)} produits catalogues depuis le parser spécifique")
                
                result = {
                    "products": products,
                    "variants": variants,
                    "images": [],
                    "attributes": []
                }
                # Ajouter le flag FFGroup si détecté
                if is_ffgroup:
                    result["_is_ffgroup"] = True
                return result
            
    except Exception as e:
        log_debug(f"[ERROR] Erreur lors de la tentative d'utilisation du parser spécifique: {e}")
        import traceback
        try:
             with open("d:\\GitHub\\Backup.Web.Api\\python-service\\debug_trace.log", "a", encoding="utf-8") as f:
                 traceback.print_exc(file=f)
        except: pass
        # En cas d'erreur, on continue avec la méthode standard
        pass

    if use_ai and USE_AI:
        logger.info("Parsing de catalogue avec IA")
        return parse_catalog_with_ai(path, ai_provider, max_pages=max_pages)
    else:
        logger.info("Parsing de catalogue sans IA (méthode basique)")
        return parse_catalog_basic(path)


def generate_sql_insert_script(catalog_result: Dict) -> str:
    """
    Génère un script SQL d'insertion pour les produits du catalogue.
    
    Args:
        catalog_result: Résultat du parsing du catalogue (dict avec products, variants, images, attributes)
    
    Returns:
        Script SQL complet avec les INSERT statements
    """
    import re
    from datetime import datetime
    
    products = catalog_result.get("products", [])
    variants = catalog_result.get("variants", [])
    images = catalog_result.get("images", [])
    attributes = catalog_result.get("attributes", [])
    
    # Constantes
    COMPANY_ID = "0B470A4F-F073-4B12-B54E-A4C1DC234F67"
    DEFAULT_USER_ID = "C3631737-A81C-47F3-8499-A52154A24A01"  # CreatedBy/UpdatedBy
    TODAY = datetime.now()
    SQL_DATE_FORMAT = TODAY.strftime("%Y-%m-%d %H:%M:%S")
    # Format datetimeoffset pour SQL Server (sans timezone pour simplifier)
    SQL_DATETIMEOFFSET_FORMAT = TODAY.strftime("%Y-%m-%d %H:%M:%S +00:00")
    
    sql_lines = []
    sql_lines.append("-- Script SQL généré automatiquement depuis le catalogue")
    sql_lines.append("-- Tables: ErpProducts, ErpProductVariants")
    sql_lines.append("")
    sql_lines.append("BEGIN TRANSACTION;")
    sql_lines.append("")
    
    # Fonction helper pour échapper les chaînes SQL
    def escape_sql(value):
        if value is None:
            return "NULL"
        if isinstance(value, bool):
            return "1" if value else "0"
        if isinstance(value, (int, float)):
            return str(value)
        # Échapper les apostrophes et guillemets
        escaped = str(value).replace("'", "''")
        return f"'{escaped}'"
    
    # Fonction pour générer un slug depuis un texte
    def generate_slug(text: str) -> str:
        if not text:
            return ""
        # Convertir en minuscules
        slug = text.lower()
        # Remplacer les caractères accentués
        replacements = {
            'à': 'a', 'á': 'a', 'â': 'a', 'ã': 'a', 'ä': 'a', 'å': 'a',
            'è': 'e', 'é': 'e', 'ê': 'e', 'ë': 'e',
            'ì': 'i', 'í': 'i', 'î': 'i', 'ï': 'i',
            'ò': 'o', 'ó': 'o', 'ô': 'o', 'õ': 'o', 'ö': 'o',
            'ù': 'u', 'ú': 'u', 'û': 'u', 'ü': 'u',
            'ç': 'c', 'ñ': 'n',
            'ß': 'ss'
        }
        for old, new in replacements.items():
            slug = slug.replace(old, new)
        # Remplacer les espaces et caractères spéciaux par des tirets
        slug = re.sub(r'[^a-z0-9]+', '-', slug)
        # Enlever les tirets en début/fin
        slug = slug.strip('-')
        # Limiter la longueur
        if len(slug) > 200:
            slug = slug[:200]
        return slug if slug else "product"
    
    # INSERT ErpProducts
    if products:
        sql_lines.append("-- ========================================")
        sql_lines.append("-- INSERT INTO ErpProducts")
        sql_lines.append("-- ========================================")
        sql_lines.append("")
        
        for product in products:
            # Construire la liste des colonnes (tous les champs NOT NULL)
            columns = []
            values = []
            
            # Id (GUID) - REQUIRED
            product_id = product.get("Id") or str(uuid.uuid4())
            columns.append("[Id]")
            values.append(escape_sql(product_id))
            
            # CompanyId - REQUIRED
            columns.append("[CompanyId]")
            values.append(escape_sql(COMPANY_ID))
            
            # Reference - REQUIRED
            ref = product.get("Reference") or product.get("Sku") or ""
            columns.append("[Reference]")
            values.append(escape_sql(ref))
            
            # Noms multilingues - REQUIRED (avec fallback)
            for lang in ["NL", "FR", "EN"]:
                name_key = f"Name_{lang}"
                name_value = product.get(name_key) or product.get("Name_NL") or product.get("Name_FR") or product.get("Name_EN") or f"Product {ref}"
                columns.append(f"[Name_{lang}]")
                values.append(escape_sql(name_value))
            
            # Slugs - REQUIRED (générés depuis les noms)
            for lang in ["NL", "FR", "EN"]:
                name_key = f"Name_{lang}"
                name_value = product.get(name_key) or product.get("Name_NL") or product.get("Name_FR") or product.get("Name_EN") or f"Product {ref}"
                slug = generate_slug(name_value)
                columns.append(f"[Slug_{lang}]")
                values.append(escape_sql(slug))
            
            # ShortDescription - REQUIRED (utiliser Description ou Name)
            for lang in ["NL", "FR", "EN"]:
                short_desc = product.get(f"ShortDescription_{lang}") or product.get(f"Description_{lang}") or product.get(f"Name_{lang}") or ""
                columns.append(f"[ShortDescription_{lang}]")
                values.append(escape_sql(short_desc))
            
            # Description - REQUIRED (utiliser Description ou Name)
            for lang in ["NL", "FR", "EN"]:
                desc = product.get(f"Description_{lang}") or product.get(f"ShortDescription_{lang}") or product.get(f"Name_{lang}") or ""
                columns.append(f"[Description_{lang}]")
                values.append(escape_sql(desc))
            
            # MetaTitle - REQUIRED (utiliser Name)
            for lang in ["NL", "FR", "EN"]:
                meta_title = product.get(f"Name_{lang}") or product.get("Name_NL") or product.get("Name_FR") or product.get("Name_EN") or f"Product {ref}"
                columns.append(f"[MetaTitle_{lang}]")
                values.append(escape_sql(meta_title))
            
            # MetaDescription - REQUIRED (utiliser ShortDescription)
            for lang in ["NL", "FR", "EN"]:
                meta_desc = product.get(f"ShortDescription_{lang}") or product.get(f"Description_{lang}") or product.get(f"Name_{lang}") or ""
                columns.append(f"[MetaDescription_{lang}]")
                values.append(escape_sql(meta_desc))
            
            # CostPrice - REQUIRED (prix estimé, utiliser 0 si non disponible)
            cost_price = product.get("CostPrice")
            if cost_price is None:
                # Estimer un prix basé sur le poids ou utiliser 0
                weight = product.get("WeightKg") or 0
                cost_price = max(0, weight * 0.5) if weight > 0 else 0.0
            columns.append("[CostPrice]")
            values.append(escape_sql(cost_price))
            
            # SellingPrice - REQUIRED (prix estimé, utiliser CostPrice * 1.2 si non disponible)
            selling_price = product.get("SellingPrice")
            if selling_price is None:
                selling_price = cost_price * 1.2 if cost_price > 0 else 0.0
            columns.append("[SellingPrice]")
            values.append(escape_sql(selling_price))
            
            # StockQuantity - REQUIRED (0 par défaut)
            stock_qty = product.get("StockQuantity", 0)
            columns.append("[StockQuantity]")
            values.append(escape_sql(stock_qty))
            
            # MinOrderQuantity - REQUIRED (1 par défaut)
            min_order_qty = product.get("MinOrderQuantity", 1)
            columns.append("[MinOrderQuantity]")
            values.append(escape_sql(min_order_qty))
            
            # BrandId - NULLABLE
            if "BrandId" in product and product["BrandId"]:
                columns.append("[BrandId]")
                values.append(escape_sql(product["BrandId"]))
            
            # CategoryId - NULLABLE
            if "CategoryId" in product and product["CategoryId"]:
                columns.append("[CategoryId]")
                values.append(escape_sql(product["CategoryId"]))
            
            # UnitId - NULLABLE
            if "UnitId" in product and product["UnitId"]:
                columns.append("[UnitId]")
                values.append(escape_sql(product["UnitId"]))
            
            # Pour FFGroup, les produits n'ont pas de Barcode, Sku, ni Gtin (seulement les variantes)
            # Détecter si c'est FFGroup en vérifiant si le SKU est None (indicateur FFGroup)
            is_ffgroup_product = product.get("Sku") is None
            
            # Barcode - '0' pour FFGroup (champ non-null), sinon utiliser SKU ou ref
            if is_ffgroup_product:
                columns.append("[Barcode]")
                values.append("'0'")
            else:
                barcode = product.get("Barcode")
                if barcode is None:
                    # Si pas de barcode, utiliser SKU seulement s'il existe, sinon utiliser ref
                    barcode = product.get("Sku") or ref
                columns.append("[Barcode]")
                values.append(escape_sql(barcode))
            
            # Sku - '0' pour FFGroup (champ non-null), sinon utiliser SKU ou ref
            if is_ffgroup_product:
                columns.append("[Sku]")
                values.append("'0'")
            else:
                sku = product.get("Sku") or ref
                columns.append("[Sku]")
                values.append(escape_sql(sku))
            
            # Gtin - '0' pour FFGroup (champ non-null), sinon utiliser SKU ou ref
            if is_ffgroup_product:
                columns.append("[Gtin]")
                values.append("'0'")
            else:
                gtin = product.get("Gtin")
                if gtin is None:
                    # Si pas de gtin, utiliser SKU seulement s'il existe, sinon utiliser ref
                    gtin = product.get("Sku") or ref
                columns.append("[Gtin]")
                values.append(escape_sql(gtin))
            
            # Dimensions - NULLABLE
            if "HeightCm" in product and product["HeightCm"] is not None:
                columns.append("[HeightCm]")
                values.append(escape_sql(product["HeightCm"]))
            
            if "LengthCm" in product and product["LengthCm"] is not None:
                columns.append("[LengthCm]")
                values.append(escape_sql(product["LengthCm"]))
            
            if "WidthCm" in product and product["WidthCm"] is not None:
                columns.append("[WidthCm]")
                values.append(escape_sql(product["WidthCm"]))
            
            if "WeightKg" in product and product["WeightKg"] is not None:
                columns.append("[WeightKg]")
                values.append(escape_sql(product["WeightKg"]))
            
            # SpecificationsJson - REQUIRED (JSON vide si non disponible)
            specs_json = product.get("SpecificationsJson") or "{}"
            columns.append("[SpecificationsJson]")
            values.append(escape_sql(specs_json))
            
            # Availability - REQUIRED
            availability = product.get("Availability") or "InStock"
            columns.append("[Availability]")
            values.append(escape_sql(availability))
            
            # Visibility - REQUIRED
            visibility = product.get("Visibility") or "Visible"
            columns.append("[Visibility]")
            values.append(escape_sql(visibility))
            
            # IsActive - REQUIRED
            is_active = product.get("IsActive", True)
            columns.append("[IsActive]")
            values.append(escape_sql(is_active))
            
            # IsPublished - REQUIRED
            is_published = product.get("IsPublished", True)
            columns.append("[IsPublished]")
            values.append(escape_sql(is_published))
            
            # CreatedBy - REQUIRED
            columns.append("[CreatedBy]")
            values.append(escape_sql(DEFAULT_USER_ID))
            
            # UpdatedBy - REQUIRED
            columns.append("[UpdatedBy]")
            values.append(escape_sql(DEFAULT_USER_ID))
            
            # CreatedAt - REQUIRED
            columns.append("[CreatedAt]")
            values.append(f"'{SQL_DATETIMEOFFSET_FORMAT}'")
            
            # UpdatedAt - NULLABLE
            columns.append("[UpdatedAt]")
            values.append("NULL")
            
            if columns:
                columns_str = ", ".join(columns)
                values_str = ", ".join(values)
                sql_lines.append(f"INSERT INTO [dbo].[ErpProducts] ({columns_str})")
                sql_lines.append(f"VALUES ({values_str});")
                sql_lines.append("")
    
    # INSERT ErpProductVariants
    if variants:
        sql_lines.append("-- ========================================")
        sql_lines.append("-- INSERT INTO ErpProductVariants")
        sql_lines.append("-- ========================================")
        sql_lines.append("")
        
        for variant in variants:
            columns = []
            values = []
            
            # Id - REQUIRED
            variant_id = variant.get("Id") or str(uuid.uuid4())
            columns.append("[Id]")
            values.append(escape_sql(variant_id))
            
            # ProductId - REQUIRED
            product_id = variant.get("ProductId")
            if not product_id:
                # Essayer de trouver le ProductId depuis le SKU
                variant_sku = variant.get("Sku")
                for p in products:
                    if p.get("Sku") == variant_sku or p.get("Reference") == variant_sku:
                        product_id = p.get("Id")
                        break
            if not product_id:
                continue  # Skip si pas de ProductId
            
            columns.append("[ProductId]")
            values.append(escape_sql(product_id))
            
            # Sku - REQUIRED
            variant_sku = variant.get("Sku") or ""
            columns.append("[Sku]")
            values.append(escape_sql(variant_sku))
            
            # Barcode - REQUIRED (utiliser SKU si non disponible)
            barcode = variant.get("Barcode") or variant_sku
            columns.append("[Barcode]")
            values.append(escape_sql(barcode))
            
            # PriceOverride - NULLABLE
            if "PriceOverride" in variant and variant["PriceOverride"] is not None:
                columns.append("[PriceOverride]")
                values.append(escape_sql(variant["PriceOverride"]))
            
            # StockQuantity - REQUIRED (0 par défaut)
            stock_qty = variant.get("StockQuantity", 0)
            columns.append("[StockQuantity]")
            values.append(escape_sql(stock_qty))
            
            # AttributesJson - REQUIRED (JSON vide si non disponible)
            attrs_json = variant.get("AttributesJson") or "{}"
            columns.append("[AttributesJson]")
            values.append(escape_sql(attrs_json))
            
            # Dimensions - NULLABLE
            if "Weight" in variant and variant["Weight"] is not None:
                columns.append("[Weight]")
                values.append(escape_sql(variant["Weight"]))
            
            if "Length" in variant and variant["Length"] is not None:
                columns.append("[Length]")
                values.append(escape_sql(variant["Length"]))
            
            if "Width" in variant and variant["Width"] is not None:
                columns.append("[Width]")
                values.append(escape_sql(variant["Width"]))
            
            if "Height" in variant and variant["Height"] is not None:
                columns.append("[Height]")
                values.append(escape_sql(variant["Height"]))
            
            # IsActive - REQUIRED
            is_active = variant.get("IsActive", True)
            columns.append("[IsActive]")
            values.append(escape_sql(is_active))
            
            # CreatedBy - REQUIRED
            columns.append("[CreatedBy]")
            values.append(escape_sql(DEFAULT_USER_ID))
            
            # UpdatedBy - REQUIRED
            columns.append("[UpdatedBy]")
            values.append(escape_sql(DEFAULT_USER_ID))
            
            # CreatedAt - REQUIRED
            columns.append("[CreatedAt]")
            values.append(f"'{SQL_DATETIMEOFFSET_FORMAT}'")
            
            # UpdatedAt - REQUIRED
            columns.append("[UpdatedAt]")
            values.append(f"'{SQL_DATETIMEOFFSET_FORMAT}'")
            
            if columns:
                columns_str = ", ".join(columns)
                values_str = ", ".join(values)
                sql_lines.append(f"INSERT INTO [dbo].[ErpProductVariants] ({columns_str})")
                sql_lines.append(f"VALUES ({values_str});")
                sql_lines.append("")
    
    sql_lines.append("COMMIT TRANSACTION;")
    sql_lines.append("")
    sql_lines.append(f"-- Total: {len(products)} produits, {len(variants)} variantes")
    
    # Si aucune variante, ajouter un message informatif
    if len(variants) == 0 and len(products) > 0:
        sql_lines.append("-- NOTE: Aucune variante extraite (peut être normal si les produits n'ont pas de variantes)")
    
    # Si aucun produit, ajouter un message d'avertissement
    if len(products) == 0:
        sql_lines.insert(3, "-- ⚠️ ATTENTION: Aucun produit extrait du catalogue!")
        sql_lines.insert(4, "-- Vérifiez que le document est bien un catalogue produit.")
        sql_lines.insert(5, "")
    
    return "\n".join(sql_lines)
