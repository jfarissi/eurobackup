"""
Extracteur basé sur Google Gemini (gratuit avec quota généreux).
Utilise Google Gemini API pour une extraction précise.
"""
import os
import json
import logging
import google.generativeai as genai
from typing import List, Dict

# Configuration du logging
logger = logging.getLogger(__name__)

# Configuration Gemini
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")
# Modèles disponibles: "gemini-1.5-pro", "gemini-1.5-flash", "gemini-1.0-pro", "gemini-2.0-flash-exp"
# Note: "gemini-pro" est obsolète, utiliser "gemini-1.5-pro" ou "gemini-1.5-flash"
GEMINI_MODEL = os.getenv("GEMINI_MODEL", "gemini-1.5-flash")  # Par défaut: gemini-1.5-flash (rapide et gratuit)

def extract_text_from_pdf(path: str) -> str:
    """Extrait tout le texte d'un PDF avec PyMuPDF (plus rapide)"""
    from .utils.pdf_extractor import extract_text_from_pdf as extract_text
    return extract_text(path)

def extract_products_with_gemini(path: str) -> tuple[List[Dict], str]:
    """
    Extrait les produits d'une facture en utilisant Google Gemini.
    Retourne: (liste de produits, nom du modèle utilisé)
    """
    if not GEMINI_API_KEY:
        logger.warning("GEMINI_API_KEY n'est pas défini - fallback sur méthode classique")
        raise ValueError("GEMINI_API_KEY n'est pas défini dans les variables d'environnement")
    
    logger.info(f"Extraction Gemini des produits depuis {path}")
    
    # Configurer Gemini
    genai.configure(api_key=GEMINI_API_KEY)
    
    # Trouver automatiquement un modèle disponible et fonctionnel
    model = None
    model_name_used = None
    
    try:
        # Lister TOUS les modèles disponibles et tester chacun
        all_models = list(genai.list_models())
        logger.info(f"Total modèles listés: {len(all_models)}")
        
        # Filtrer les modèles qui supportent generateContent
        available_models = [m for m in all_models if 'generateContent' in m.supported_generation_methods]
        logger.info(f"Modèles avec generateContent: {len(available_models)}")
        
        # Afficher tous les modèles disponibles pour debug
        for m in available_models:
            logger.info(f"Modèle disponible: {m.name} (display_name: {getattr(m, 'display_name', 'N/A')})")
        
        if not available_models:
            raise ValueError("Aucun modèle Gemini avec generateContent trouvé")
        
        # Essayer CHAQUE modèle disponible avec son nom complet d'abord, puis nom court
        for model_info in available_models:
            model_full_name = model_info.name  # Format: "models/gemini-1.5-flash" ou similaire
            model_short_name = model_full_name.split('/')[-1] if '/' in model_full_name else model_full_name
            
            try:
                logger.info(f"🔍 Test du modèle: {model_full_name} (court: {model_short_name})")
                
                # Essayer d'abord avec le nom complet
                try:
                    test_model = genai.GenerativeModel(model_full_name)
                    test_response = test_model.generate_content("test", generation_config={"temperature": 0.1})
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
                        test_response = test_model.generate_content("test", generation_config={"temperature": 0.1})
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
        logger.error(f"Erreur lors de la recherche de modèles Gemini: {e}", exc_info=True)
        raise ValueError(f"Impossible de trouver un modèle Gemini disponible: {e}")
    
    # Extraire le texte du PDF
    pdf_text = extract_text_from_pdf(path)
    
    if not pdf_text or len(pdf_text.strip()) < 50:
        logger.warning("PDF vide ou trop court - impossible d'extraire")
        return []
    
    logger.info(f"Texte extrait: {len(pdf_text)} caractères")
    
    # Limiter le texte
    text_limit = 20000
    truncated_text = pdf_text[:text_limit]
    if len(pdf_text) > text_limit:
        logger.warning(f"Texte tronqué de {len(pdf_text)} à {text_limit} caractères")
    
    # Ajouter le catalogue au prompt si disponible
    from .utils.catalog_prompt_builder import add_catalog_to_prompt
    
    base_prompt = """Tu es un expert en extraction de données de factures. 
Extrais tous les produits d'une FACTURE et retourne-les sous forme de JSON.

⚠️ IMPORTANT: 
- Si un CATALOGUE PRODUIT est fourni, c'est un RÉFÉRENTIEL de référence, PAS une facture à parser
- Parse UNIQUEMENT le texte de la FACTURE (qui sera fourni séparément après le catalogue)
- Utilise le catalogue UNIQUEMENT pour matcher et enrichir les produits extraits de la facture
"""
    
    # Ajouter le catalogue au prompt
    base_prompt = add_catalog_to_prompt(base_prompt, use_full_catalog=False)
    
    prompt = f"""{base_prompt}

═══════════════════════════════════════════════════════════════════════════════
TEXTE DE LA FACTURE À PARSER (ci-dessous)
═══════════════════════════════════════════════════════════════════════════════

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
    }}
  ]
}}

Règles IMPORTANTES:
- Si la description commence par un numéro (ex: "90 Flex voegmortel"), enlève le numéro
- La description doit être complète
- Les prix en nombres décimaux avec point (utilise le point comme séparateur décimal, convertis les virgules en points)

**STRUCTURE DES FACTURES - Identification des colonnes:**
- Si la facture a une table avec colonnes "Prix Vente" / "Verkoop Prijs" et "TVA" / "BTW":
  - **PRIX UNITAIRE (unit_price)**: C'est la colonne "Prix Vente" / "Verkoop Prijs" (ex: 48,04). C'est le prix d'UN SEUL article AVANT réductions.
  - **TOTAL (total_value)**: C'est la colonne "TVA" / "BTW" qui contient le montant total de la ligne APRÈS réductions (ex: 60,53). C'est le montant total de la ligne, pas le prix unitaire.
  - La colonne "Montant" / "Bedrag" contient généralement les pourcentages de réduction (ex: "65% -10%"), pas un prix.

- Si la facture n'a pas de colonnes clairement identifiées:
  - **PRIX UNITAIRE (unit_price)**: C'est le prix d'UN SEUL article, AVANT TVA et AVANT réductions. C'est généralement le premier prix numérique dans la ligne du produit.
  - **TOTAL (total_value)**: C'est le montant total de la ligne (quantité × prix unitaire après réductions), généralement AVANT TVA.
  - Si une ligne contient plusieurs prix (ex: "48,04 -65% -10% 60,53 12,71"), le prix unitaire est le premier (48,04), le total après réduction est le deuxième (60,53), et le dernier est souvent la TVA (12,71) à IGNORER.

- Ignore les pourcentages de réduction dans le calcul du prix unitaire, mais utilise le total après réduction pour total_value.

MATCHING AVEC LE CATALOGUE (si fourni):
- APRÈS avoir extrait les produits de la FACTURE, cherche chaque produit dans le catalogue
- Utilise les identifiants (SKU, EAN, Barcode, GTIN) en priorité
- Si un match est trouvé, ajoute les champs catalog_* au produit

Texte de la facture:
{truncated_text}

MATCHING AVEC LE CATALOGUE (si fourni):
- APRÈS avoir extrait les produits de la FACTURE, cherche chaque produit dans le catalogue
- Utilise les identifiants (SKU, EAN, Barcode, GTIN) en priorité
- Si un match est trouvé, ajoute les champs catalog_* au produit

IMPORTANT: Retourne UNIQUEMENT le JSON, sans texte avant ou après, sans markdown, sans code blocks, sans explications. Juste le JSON brut.
"""

    try:
        logger.info(f"Appel Gemini avec modèle {model_name_used or GEMINI_MODEL}")
        # Configuration de génération
        generation_config = {
            "temperature": 0.1,
        }
        # Essayer d'utiliser response_mime_type si le modèle le supporte (gemini-1.5+)
        # Utiliser le nom du modèle détecté
        model_name = model_name_used or GEMINI_MODEL
        if "1.5" in model_name or "2.0" in model_name:
            try:
                generation_config["response_mime_type"] = "application/json"
            except:
                pass
        
        response = model.generate_content(
            prompt,
            generation_config=generation_config
        )
        
        # Extraire la réponse
        if not hasattr(response, 'text') or response.text is None:
            logger.error("Réponse Gemini n'a pas d'attribut 'text' ou est None")
            raise ValueError("Réponse Gemini invalide: pas de texte")
        
        response_content = response.text.strip()
        logger.info(f"Réponse reçue: {len(response_content)} caractères")
        
        if not response_content:
            logger.error("Réponse vide de Gemini!")
            raise ValueError("Réponse vide de Gemini")
        
        # Debug: afficher le début de la réponse
        logger.debug(f"Premiers 500 caractères de la réponse: {response_content[:500]}")
        
        # Parser la réponse JSON (gérer les markdown code blocks)
        import re
        # Enlever les markdown code blocks si présents (```json ... ```)
        cleaned_content = response_content.strip()
        if cleaned_content.startswith('```'):
            # Extraire le contenu entre les backticks
            match = re.search(r'```(?:json)?\s*\n(.*?)\n```', cleaned_content, re.DOTALL)
            if match:
                cleaned_content = match.group(1).strip()
                logger.info("✅ Markdown code block détecté et retiré")
        
        # Parser la réponse JSON
        try:
            result = json.loads(cleaned_content)
        except json.JSONDecodeError as json_err:
            # Si la réponse n'est pas du JSON valide, essayer d'extraire le JSON du texte
            logger.warning(f"Réponse n'est pas du JSON valide, tentative d'extraction: {json_err}")
            logger.warning(f"Réponse complète (1000 premiers caractères): {response_content[:1000]}")
            # Chercher un bloc JSON dans la réponse (entre { et })
            json_match = re.search(r'\{.*\}', cleaned_content, re.DOTALL)
            if json_match:
                try:
                    result = json.loads(json_match.group(0))
                    logger.info("✅ JSON extrait avec succès du texte")
                except Exception as extract_err:
                    logger.error(f"❌ Impossible d'extraire le JSON: {extract_err}")
                    raise ValueError(f"Réponse Gemini n'est pas du JSON valide. Réponse: {response_content[:500]}")
            else:
                logger.error(f"❌ Aucun JSON trouvé dans la réponse")
                raise ValueError(f"Réponse Gemini ne contient pas de JSON. Réponse: {response_content[:500]}")
        
        products = result.get("products", [])
        
        logger.info(f"{len(products)} produits extraits par Gemini avec modèle {model_name_used}")
        
        # Normaliser les produits
        normalized_products = []
        for product in products:
            try:
                # Gérer les valeurs None/null
                unit_value = product.get("unit")
                if unit_value is None:
                    unit_value = "ST"
                else:
                    unit_value = str(unit_value).upper()
                
                description = product.get("description") or ""
                
                # Nettoyer le product_code (sku) : enlever les virgules et espaces superflus
                product_code = product.get("product_code") or product.get("sku")
                if product_code:
                    # Si le code contient une virgule, prendre seulement la première partie
                    if ',' in str(product_code):
                        product_code = str(product_code).split(',')[0].strip()
                        logger.warning(f"SKU avec virgule détecté, utilisation de la première partie: {product_code}")
                    else:
                        product_code = str(product_code).strip()
                else:
                    product_code = None
                    logger.warning(f"Produit sans SKU: {description[:50]}")
                
                normalized = {
                    "raw": description,
                    "normalized": description.lower().strip() if description else "",
                    "quantity": int(product.get("quantity", 0)) if product.get("quantity") is not None else 0,
                    "product_code": product_code,
                    "ean": product.get("ean") or None,
                    "unit": unit_value,
                    "unit_price": float(product.get("unit_price")) if product.get("unit_price") is not None else None,
                    "total_value": float(product.get("total_value")) if product.get("total_value") is not None else None
                }
                normalized_products.append(normalized)
            except Exception as e:
                logger.warning(f"Erreur lors de la normalisation d'un produit: {e}")
                logger.debug(f"Produit problématique: {product}")
                continue
        
        return normalized_products, model_name_used or GEMINI_MODEL
        
    except Exception as e:
        logger.error(f"Erreur lors de l'extraction avec Gemini: {e}", exc_info=True)
        raise

def extract_metadata_with_gemini(path: str) -> tuple[Dict, str]:
    """
    Extrait les métadonnées d'un document en utilisant Google Gemini.
    Retourne: (métadonnées, nom du modèle utilisé)
    """
    if not GEMINI_API_KEY:
        logger.warning("GEMINI_API_KEY n'est pas défini - fallback sur méthode classique")
        raise ValueError("GEMINI_API_KEY n'est pas défini dans les variables d'environnement")
    
    logger.info(f"Extraction Gemini des métadonnées depuis {path}")
    
    # Configurer Gemini
    genai.configure(api_key=GEMINI_API_KEY)
    
    # Trouver automatiquement un modèle disponible (même logique que pour les produits)
    model = None
    model_name_used = None
    
    try:
        # Lister les modèles disponibles et tester directement avec les noms complets
        all_models = list(genai.list_models())
        available_models = [m for m in all_models if 'generateContent' in m.supported_generation_methods]
        
        if not available_models:
            raise ValueError("Aucun modèle Gemini avec generateContent trouvé")
        
        # Essayer CHAQUE modèle disponible avec son nom complet d'abord, puis nom court
        for model_info in available_models:
            model_full_name = model_info.name  # Format: "models/gemini-1.5-flash" ou similaire
            model_short_name = model_full_name.split('/')[-1] if '/' in model_full_name else model_full_name
            
            try:
                logger.info(f"🔍 Test du modèle pour métadonnées: {model_full_name} (court: {model_short_name})")
                
                # Essayer d'abord avec le nom complet
                try:
                    test_model = genai.GenerativeModel(model_full_name)
                    test_response = test_model.generate_content("test", generation_config={"temperature": 0.1})
                    model = test_model
                    model_name_used = model_short_name
                    logger.info(f"✅ Modèle {model_full_name} testé et fonctionnel pour métadonnées - UTILISÉ")
                    break
                except Exception as e1:
                    error_msg = str(e1)
                    logger.debug(f"  Nom complet échoué: {error_msg[:150]}")
                    
                    # Essayer avec le nom court
                    try:
                        test_model = genai.GenerativeModel(model_short_name)
                        test_response = test_model.generate_content("test", generation_config={"temperature": 0.1})
                        model = test_model
                        model_name_used = model_short_name
                        logger.info(f"✅ Modèle {model_short_name} testé et fonctionnel pour métadonnées - UTILISÉ")
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
        logger.error(f"Erreur lors de la recherche de modèles Gemini: {e}")
        raise ValueError(f"Impossible de trouver un modèle Gemini disponible: {e}")
    
    # Extraire le texte du PDF
    # Extraire le texte du PDF (2 premières pages) avec PyMuPDF
    from .utils.pdf_extractor import extract_pdf_raw
    pdf_raw = extract_pdf_raw(path, max_pages=2)
    texts = [p['text'] for p in pdf_raw['pages'] if p.get('text')]
    pdf_text = "\n\n".join(texts)
    
    if not pdf_text:
        return {
            "doc_type": None,
            "number": None,
            "client": None,
            "supplier": None
        }
    
    prompt = f"""Analyse ce document PDF et extrais les informations suivantes dans un format JSON:

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

IMPORTANT: 
- Si une information n'est pas trouvée dans le document, utilise null pour ce champ.
- Retourne UNIQUEMENT le JSON, sans texte avant ou après, sans markdown, sans code blocks, sans explications. Juste le JSON brut.
"""

    try:
        # Configuration de génération avec support JSON
        generation_config = {
            "temperature": 0.1,
        }
        # Essayer d'utiliser response_mime_type si le modèle le supporte (gemini-1.5+)
        # Utiliser le nom du modèle détecté
        model_name = model_name_used or GEMINI_MODEL
        if "1.5" in model_name or "2.0" in model_name:
            try:
                generation_config["response_mime_type"] = "application/json"
            except:
                pass
        
        response = model.generate_content(
            prompt,
            generation_config=generation_config
        )
        
        # Extraire la réponse
        if not hasattr(response, 'text') or response.text is None:
            logger.error("Réponse Gemini métadonnées n'a pas d'attribut 'text' ou est None")
            raise ValueError("Réponse Gemini métadonnées invalide: pas de texte")
        
        response_content = response.text.strip()
        logger.info(f"Réponse métadonnées reçue: {len(response_content)} caractères")
        
        if not response_content:
            logger.error("Réponse métadonnées vide de Gemini!")
            raise ValueError("Réponse métadonnées vide de Gemini")
        
        # Debug: afficher le début de la réponse
        logger.debug(f"Premiers 500 caractères: {response_content[:500]}")
        
        # Parser la réponse JSON (gérer les markdown code blocks)
        import re
        # Enlever les markdown code blocks si présents (```json ... ```)
        cleaned_content = response_content.strip()
        if cleaned_content.startswith('```'):
            # Extraire le contenu entre les backticks
            match = re.search(r'```(?:json)?\s*\n(.*?)\n```', cleaned_content, re.DOTALL)
            if match:
                cleaned_content = match.group(1).strip()
                logger.info("✅ Markdown code block détecté et retiré pour métadonnées")
        
        # Parser la réponse JSON
        try:
            result = json.loads(cleaned_content)
        except json.JSONDecodeError as json_err:
            # Si la réponse n'est pas du JSON valide, essayer d'extraire le JSON du texte
            logger.warning(f"Réponse métadonnées n'est pas du JSON valide, tentative d'extraction: {json_err}")
            logger.warning(f"Réponse complète (1000 premiers caractères): {response_content[:1000]}")
            # Chercher un bloc JSON dans la réponse (entre { et })
            json_match = re.search(r'\{.*\}', cleaned_content, re.DOTALL)
            if json_match:
                try:
                    result = json.loads(json_match.group(0))
                    logger.info("✅ JSON métadonnées extrait avec succès du texte")
                except Exception as extract_err:
                    logger.error(f"❌ Impossible d'extraire le JSON: {extract_err}")
                    raise ValueError(f"Réponse Gemini métadonnées n'est pas du JSON valide. Réponse: {response_content[:500]}")
            else:
                logger.error(f"❌ Aucun JSON trouvé dans la réponse métadonnées")
                raise ValueError(f"Réponse Gemini métadonnées ne contient pas de JSON. Réponse: {response_content[:500]}")
        
        # Normaliser le format
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
        
        logger.info(f"Métadonnées extraites par Gemini avec modèle {model_name_used}")
        
        return normalized_result, model_name_used or GEMINI_MODEL
        
    except Exception as e:
        logger.error(f"Erreur lors de l'extraction des métadonnées avec Gemini: {e}", exc_info=True)
        raise

