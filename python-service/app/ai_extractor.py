"""
Extracteur basé sur l'IA pour extraire les données des documents PDF.
Utilise OpenAI GPT-4 ou Claude pour une extraction précise et flexible.
"""
import os
import json
import logging
from typing import List, Dict, Optional
from openai import OpenAI
from .utils.catalog_prompt_builder import add_catalog_to_prompt
from .utils.product_catalog import enrich_products_with_catalog

# Configuration du logging
logger = logging.getLogger(__name__)

# Configuration - utiliser des variables d'environnement
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
USE_OPENAI = os.getenv("USE_OPENAI", "true").lower() == "true"
MODEL = os.getenv("OPENAI_MODEL", "gpt-4o")  # ou "gpt-4-turbo-preview", "gpt-4"

# Configuration pour autres providers
AI_PROVIDER = os.getenv("AI_PROVIDER", "openai").lower()  # "openai", "ollama", "gemini"

def extract_text_from_pdf(path: str) -> str:
    """Extrait tout le texte d'un PDF avec PyMuPDF (plus rapide)"""
    from .utils.pdf_extractor import extract_text_from_pdf as extract_text
    return extract_text(path)

def extract_products_with_ai(path: str) -> List[Dict]:
    """
    Extrait les produits d'une facture en utilisant l'IA.
    Retourne une liste de dictionnaires avec les produits extraits.
    """
    if not OPENAI_API_KEY:
        logger.warning("OPENAI_API_KEY n'est pas défini - fallback sur méthode classique")
        raise ValueError("OPENAI_API_KEY n'est pas défini dans les variables d'environnement")
    
    logger.info(f"Extraction IA des produits depuis {path}")
    
    # Extraire le texte du PDF
    pdf_text = extract_text_from_pdf(path)
    
    if not pdf_text or len(pdf_text.strip()) < 50:
        logger.warning("PDF vide ou trop court - impossible d'extraire")
        return []
    
    logger.info(f"Texte extrait: {len(pdf_text)} caractères")
    
    # Préparer le prompt pour l'IA avec le catalogue
    system_prompt = """Tu es un expert en extraction de données de factures. 
Tu dois extraire tous les produits d'une FACTURE et les retourner sous forme de JSON.

⚠️ IMPORTANT: 
- Si un CATALOGUE PRODUIT est fourni, c'est un RÉFÉRENTIEL de référence, PAS une facture à parser
- Parse UNIQUEMENT le texte de la FACTURE (qui sera fourni séparément après le catalogue)
- Utilise le catalogue UNIQUEMENT pour matcher et enrichir les produits extraits de la facture

Pour chaque produit de la FACTURE, extrais :
- quantity: la quantité (nombre entier)
- unit: l'unité (PAC, PC, KG, etc.)
- product_code: le code article (souvent après "Artikel:")
- description: la description complète du produit (sans le numéro de position au début)
- ean: le code EAN si présent
- unit_price: le prix unitaire net
- total_value: la valeur totale nette

RÈGLES D'EXTRACTION:
- Si la description commence par un numéro (ex: "90 Flex voegmortel"), enlève le numéro (c'est le numéro de position)
- La description doit être complète, pas tronquée
- Les prix doivent être des nombres décimaux (utiliser le point comme séparateur)
- Retourne UNIQUEMENT un JSON valide, sans texte avant ou après
- Si un champ n'est pas trouvé, utilise null

MATCHING AVEC LE CATALOGUE (si fourni):
- APRÈS avoir extrait les produits de la FACTURE, cherche chaque produit dans le catalogue
- Utilise les identifiants (SKU, EAN, Barcode, GTIN) en priorité
- Si un match est trouvé, ajoute les champs suivants au produit:
  - catalog_id: ID du produit dans le catalogue
  - catalog_sku: SKU du catalogue
  - catalog_name_nl/fr/en: Noms du produit du catalogue
  - catalog_description_nl/fr/en: Descriptions du produit du catalogue
  - catalog_selling_price: Prix de vente du catalogue
  - catalog_matched: true si trouvé, false sinon
"""
    
    # Ajouter le catalogue au prompt si disponible (AVANT le texte de la facture)
    system_prompt = add_catalog_to_prompt(system_prompt, use_full_catalog=False)

    # Limiter le texte pour éviter les limites de tokens (GPT-4o supporte jusqu'à 128k tokens)
    # On prend les 20000 premiers caractères qui contiennent généralement tous les produits
    text_limit = 20000
    truncated_text = pdf_text[:text_limit]
    if len(pdf_text) > text_limit:
        logger.warning(f"Texte tronqué de {len(pdf_text)} à {text_limit} caractères")
    
    user_prompt = f"""═══════════════════════════════════════════════════════════════════════════════
TEXTE DE LA FACTURE À PARSER (ci-dessous)
═══════════════════════════════════════════════════════════════════════════════

Extrais tous les produits de cette FACTURE et retourne-les sous forme de JSON.

Format attendu:
{{
  "products": [
    {{
      "quantity": 10,
      "unit": "PAC",
      "product_code": "00184088",
      "description": "Spijkerplug 50 x 6 mm (20 st / Blister)",
      "ean": "5413503555628",
      "unit_price": 3.17,
      "total_value": 31.70
    }},
    ...
  ]
}}

RÈGLES IMPORTANTES:
- Si une description commence par un numéro (ex: "90 Flex voegmortel"), ce numéro est le numéro de position - ENLÈVE-LE
- La description doit être COMPLÈTE, pas tronquée (ex: "Flex voegmortel antraciet 5kg (164)" et non "Flex voeg...")
- Extrais TOUS les produits, même ceux avec des descriptions longues
- Les unités doivent être en majuscules (PAC, PC, KG)

Texte de la facture:
{truncated_text}
"""

    try:
        client = OpenAI(api_key=OPENAI_API_KEY)
        
        logger.info(f"Appel API OpenAI avec modèle {MODEL}")
        response = client.chat.completions.create(
            model=MODEL,
            messages=[
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt}
            ],
            response_format={"type": "json_object"},  # Force le format JSON
            temperature=0.1,  # Basse température pour plus de précision
            max_tokens=6000  # Augmenté pour supporter plus de produits
        )
        
        # Parser la réponse JSON
        response_content = response.choices[0].message.content
        logger.info(f"Réponse reçue: {len(response_content)} caractères")
        
        result = json.loads(response_content)
        products = result.get("products", [])
        
        logger.info(f"{len(products)} produits extraits par l'IA")
        
        # Normaliser les produits pour correspondre au format attendu
        normalized_products = []
        for product in products:
            try:
                normalized = {
                    "raw": product.get("description", ""),
                    "normalized": product.get("description", "").lower().strip(),
                    "quantity": int(product.get("quantity", 0)),
                    "product_code": product.get("product_code"),
                    "ean": product.get("ean"),
                    "unit": product.get("unit", "ST").upper(),
                    "unit_price": float(product.get("unit_price", 0)) if product.get("unit_price") else None,
                    "total_value": float(product.get("total_value", 0)) if product.get("total_value") else None
                }
                normalized_products.append(normalized)
            except Exception as e:
                logger.warning(f"Erreur lors de la normalisation d'un produit: {e}")
                continue
        
        logger.info(f"{len(normalized_products)} produits normalisés avec succès")
        
        # Enrichir les produits avec le catalogue (si disponible)
        try:
            normalized_products = enrich_products_with_catalog(normalized_products)
            matched_count = sum(1 for p in normalized_products if p.get('catalog_matched', False))
            if matched_count > 0:
                logger.info(f"{matched_count} produits matchés avec le catalogue")
        except Exception as e:
            logger.warning(f"Erreur lors de l'enrichissement avec le catalogue: {e}")
        
        return normalized_products
        
    except json.JSONDecodeError as e:
        logger.error(f"Erreur de parsing JSON: {e}")
        try:
            response_content = response.choices[0].message.content if 'response' in locals() else "N/A"
            logger.error(f"Réponse de l'IA: {response_content[:500]}")
        except:
            pass
        raise  # Relancer pour que le fallback fonctionne
    except Exception as e:
        error_msg = str(e)
        # Détecter les erreurs de quota ou d'authentification
        if "quota" in error_msg.lower() or "insufficient_quota" in error_msg.lower() or "429" in error_msg:
            # Ne pas logger en warning si c'est juste un problème de quota (normal si on utilise Ollama en priorité)
            logger.debug(f"OpenAI quota dépassé (normal si Ollama est utilisé): {error_msg}")
        elif "401" in error_msg or "unauthorized" in error_msg.lower():
            logger.warning(f"Clé API OpenAI invalide - fallback sur méthode classique: {error_msg}")
        else:
            logger.error(f"Erreur lors de l'extraction avec l'IA: {e}", exc_info=True)
        raise  # Relancer pour que le fallback fonctionne

def extract_metadata_with_ai(path: str) -> Dict:
    """
    Extrait les métadonnées d'un document en utilisant l'IA.
    """
    if not OPENAI_API_KEY:
        logger.warning("OPENAI_API_KEY n'est pas défini - fallback sur méthode classique")
        raise ValueError("OPENAI_API_KEY n'est pas défini dans les variables d'environnement")
    
    logger.info(f"Extraction IA des métadonnées depuis {path}")
    
    # Extraire le texte du PDF (première page suffit généralement pour les métadonnées)
    # Extraire le texte du PDF (2 premières pages) avec PyMuPDF
    from .utils.pdf_extractor import extract_pdf_raw
    pdf_raw = extract_pdf_raw(path, max_pages=2)
    texts = [p['text'] for p in pdf_raw['pages'] if p.get('text')]
    pdf_text = "\n\n".join(texts)
    
    if not pdf_text:
        logger.warning("PDF vide - impossible d'extraire les métadonnées")
        return {
            "doc_type": None,
            "number": None,
            "client": None,
            "supplier": None
        }
    
    logger.info(f"Texte extrait pour métadonnées: {len(pdf_text)} caractères")
    
    system_prompt = """Tu es un expert en extraction de métadonnées de documents PDF.
Analyse le document et extrais les informations suivantes dans un format JSON.

Règles:
- doc_type: "invoice" pour facture/factuur, "delivery" pour bon de livraison/leveringsbon
- number: le numéro de document (UNIQUEMENT des chiffres, cherche "Faktuur nr.", "Factuur nr.", "Nummer")
- client: le nom de l'entreprise cliente uniquement (sans adresse, sans "Afleveradres", sans numéro de client)
- supplier: le nom du fournisseur/émetteur
- date: la date du document (format "YYYY-MM-DD" ou "DD/MM/YYYY")
- supplier_code: le code fournisseur si présent (généralement un code court)
- supplier_address: l'adresse complète du fournisseur (rue, numéro, code postal, ville, pays)
- supplier_phone: le numéro de téléphone du fournisseur
- supplier_email: l'adresse email du fournisseur
- supplier_contact: le nom du contact commercial si présent
- supplier_payment_terms: les conditions de paiement (ex: "30 jours", "Net 30", "Paiement à réception")

Retourne UNIQUEMENT un JSON valide.
"""

    user_prompt = f"""Analyse ce document PDF et extrais les informations suivantes dans un format JSON:

{pdf_text[:10000]}

Format attendu:
{{
  "doc_type": "invoice",
  "number": "713861483",
  "client": "10170600 Euro Brico sprl",
  "date": "2025-09-04",
  "supplier": "Knauf",
  "supplier_code": "KNAUF001",
  "supplier_address": "Rue du Parc Industriel 1, 1000 Bruxelles, Belgique",
  "supplier_phone": "+32 2 123 45 67",
  "supplier_email": "contact@knauf.be",
  "supplier_contact": "Jean Dupont",
  "supplier_payment_terms": "30 jours net"
}}

IMPORTANT: Si une information n'est pas trouvée dans le document, utilise null pour ce champ.
"""

    try:
        client = OpenAI(api_key=OPENAI_API_KEY)
        
        logger.info(f"Appel API OpenAI pour métadonnées avec modèle {MODEL}")
        response = client.chat.completions.create(
            model=MODEL,
            messages=[
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt}
            ],
            response_format={"type": "json_object"},
            temperature=0.1,
            max_tokens=500
        )
        
        result = json.loads(response.choices[0].message.content)
        logger.info(f"Métadonnées extraites: {result}")
        
        # Normaliser le format pour correspondre au backend C#
        # Le backend attend: doc_type, number, client, date, supplier
        # + informations supplémentaires du fournisseur
        normalized_result = {
            "doc_type": result.get("doc_type") or result.get("typeDocument"),
            "number": result.get("number") or result.get("numero"),
            "client": result.get("client"),
            "date": result.get("date") or result.get("dateDocument"),
            "supplier": result.get("supplier"),
            "supplier_code": result.get("supplier_code"),
            "supplier_address": result.get("supplier_address"),
            "supplier_phone": result.get("supplier_phone"),
            "supplier_email": result.get("supplier_email"),
            "supplier_contact": result.get("supplier_contact"),
            "supplier_payment_terms": result.get("supplier_payment_terms")
        }
        
        logger.info(f"Métadonnées normalisées: {normalized_result}")
        return normalized_result
        
    except Exception as e:
        error_msg = str(e)
        # Détecter les erreurs de quota ou d'authentification
        if "quota" in error_msg.lower() or "insufficient_quota" in error_msg.lower() or "429" in error_msg:
            # Ne pas logger en warning si c'est juste un problème de quota (normal si on utilise Ollama en priorité)
            logger.debug(f"OpenAI quota dépassé (normal si Ollama est utilisé): {error_msg}")
        elif "401" in error_msg or "unauthorized" in error_msg.lower():
            logger.warning(f"Clé API OpenAI invalide - fallback sur méthode classique: {error_msg}")
        else:
            logger.error(f"Erreur lors de l'extraction des métadonnées avec l'IA: {e}", exc_info=True)
        raise  # Relancer pour que le fallback fonctionne

