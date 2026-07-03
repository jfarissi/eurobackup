"""
API FastAPI principale - Structure alignée avec auto_invoice_parser.
Endpoint /parse pour parser les documents PDF.
"""
import os
from pathlib import Path
from dotenv import load_dotenv
from fastapi import FastAPI, UploadFile, File, HTTPException, Query
from fastapi.responses import JSONResponse
from importlib import import_module
from typing import Optional
from ..utils.extractor import extract_pdf_raw
from ..utils.product_catalog import load_catalog
from .catalog_endpoint import router as catalog_router
from ..parsers.document_classifier import classify_supplier as classify_supplier_v2

# Charger les variables d'environnement depuis le fichier .env
# IMPORTANT: Doit être fait AVANT l'import de catalog_parser qui lit les variables
env_path = Path(__file__).parent.parent.parent / '.env'
if env_path.exists():
    load_dotenv(env_path, override=True)
    print(f"[API] ✅ Fichier .env chargé depuis: {env_path}")
    # Vérifier que les clés API sont bien chargées
    gemini_key = os.getenv("GEMINI_API_KEY")
    openai_key = os.getenv("OPENAI_API_KEY")
    if gemini_key:
        print(f"[API] ✅ GEMINI_API_KEY trouvée (longueur: {len(gemini_key)} caractères)")
    if openai_key:
        print(f"[API] ✅ OPENAI_API_KEY trouvée (longueur: {len(openai_key)} caractères)")
    if not gemini_key and not openai_key:
        print("[API] ⚠️  Aucune clé API IA trouvée dans le fichier .env")
else:
    # Essayer aussi dans le dossier courant
    load_dotenv(override=True)
    print("[API] ℹ️  Aucun fichier .env trouvé, utilisation des variables d'environnement système")

app = FastAPI(title='AutoInvoiceParser')

# Inclure le router des catalogues
app.include_router(catalog_router)

# Charger le catalogue produit au démarrage
@app.on_event("startup")
async def startup_event():
    """Charge le catalogue produit au démarrage de l'API"""
    print("\n\n" + "="*50)
    print("🚀 PYTHON SERVICE STARTED - MODIFIED VERSION 2.0 (API/MAIN) 🚀")
    print("If you see this, the code changes are ACTIVE.")
    print("="*50 + "\n\n")
    catalog_path = os.getenv('PRODUCT_CATALOG_PATH')
    if not catalog_path:
        # Chercher le fichier par défaut
        default_path = Path(__file__).parent.parent / 'data' / 'product_catalog.json'
        if default_path.exists():
            catalog_path = str(default_path)
    
    if catalog_path:
        try:
            load_catalog(json_path=catalog_path)
            print(f"[API] Catalogue produit chargé depuis: {catalog_path}")
        except Exception as e:
            print(f"[API] Erreur lors du chargement du catalogue: {e}")
            print("[API] L'API fonctionnera sans catalogue")
    else:
        print("[API] Aucun catalogue produit trouvé - l'API fonctionnera sans catalogue")

    try:
        from ..ai_ollama_config import describe_config_for_startup
        print(f"[API] {describe_config_for_startup()}")
    except Exception as ex:
        print(f"[API] Config Ollama (startup): {ex}")

# Mapping des fournisseurs vers leurs parsers
# Utiliser le chemin complet depuis app
PARSERS = {
    'knauf': 'app.parsers.knauf',
    'ffgroup': 'app.parsers.ffgroup',
    'stg': 'app.parsers.stg',
    'aya': 'app.parsers.aya',
    'generic': 'app.parsers.generic'
}

MAX_RAW_TEXT_RESPONSE_CHARS = 5000


def _limit_raw_text_for_response(raw_text: Optional[str]) -> str:
    text = raw_text or ""
    if len(text) <= MAX_RAW_TEXT_RESPONSE_CHARS:
        return text
    return text[:MAX_RAW_TEXT_RESPONSE_CHARS]


def _clean_metadata_value(v: Optional[str]) -> Optional[str]:
    if v is None:
        return None
    s = str(v).strip()
    if not s:
        return None
    s = s.lstrip("-:;,. ").strip()
    return s or None


def _looks_like_header_label(value: Optional[str]) -> bool:
    s = _clean_metadata_value(value)
    if not s:
        return True
    s_lower = s.lower()
    bad_terms = (
        "tva",
        "btw",
        "vat",
        "nummer",
        "number",
        "numéro",
        "nr",
        "klant",
        "client",
        "customer",
    )
    if any(term in s_lower for term in bad_terms):
        return True
    # Trop court / pas assez informatif.
    if len(s) < 3:
        return True
    return False


def _sanitize_factory_metadata(metadata: dict) -> dict:
    """
    Evite les faux positifs d'entête (ex: numero='TVA', client='- BTW Nummer Klant').
    """
    if not isinstance(metadata, dict):
        return metadata

    number = _clean_metadata_value(metadata.get("number"))
    client = _clean_metadata_value(metadata.get("client"))
    supplier = _clean_metadata_value(metadata.get("supplier"))

    if _looks_like_header_label(number):
        metadata["number"] = None
    else:
        metadata["number"] = number

    if _looks_like_header_label(client):
        metadata["client"] = None
    else:
        metadata["client"] = client

    # Uniformiser Unknown -> None pour laisser le front/back décider.
    if supplier and supplier.lower() in ("unknown", "unk", "n/a", "na"):
        metadata["supplier"] = None
    else:
        metadata["supplier"] = supplier

    return metadata


def _semantic_extract_after_label(full_text: str, labels: list[str], max_words: int = 4) -> Optional[str]:
    lines = [ln.strip() for ln in (full_text or "").splitlines() if ln.strip()]
    labels_l = [l.lower() for l in labels]
    for i, line in enumerate(lines):
        ll = line.lower()
        hit = None
        for lbl in labels_l:
            if lbl in ll:
                hit = lbl
                break
        if not hit:
            continue

        # 1) valeur à droite du label sur la même ligne
        right = ll.split(hit, 1)[1].strip(" :.-\t")
        if right:
            parts = right.split()
            return " ".join(parts[:max_words]).strip()

        # 2) sinon première ligne utile en dessous
        if i + 1 < len(lines):
            nxt = lines[i + 1].strip(" :.-\t")
            if nxt:
                parts = nxt.split()
                return " ".join(parts[:max_words]).strip()
    return None


def _semantic_enrich_classifier_metadata(metadata: dict, full_text: str) -> dict:
    """
    Enrichissement léger inspiré du log (mots-clés + proximité ligne suivante).
    Evite les regex lourdes, garde le pipeline déterministe.
    """
    if not isinstance(metadata, dict):
        return metadata

    # Number
    if not metadata.get("number"):
        cand_number = _semantic_extract_after_label(
            full_text,
            ["invoice no", "invoice number", "facture", "factuur", "faktuur nr", "numéro", "number", "nr"],
            max_words=3,
        )
        cand_number = _clean_metadata_value(cand_number)
        if cand_number and not _looks_like_header_label(cand_number) and any(ch.isdigit() for ch in cand_number):
            metadata["number"] = cand_number

    # Client
    if not metadata.get("client"):
        cand_client = _semantic_extract_after_label(
            full_text,
            ["client", "customer", "klant", "bill to", "ship to", "delivered to", "destinataire"],
            max_words=8,
        )
        cand_client = _clean_metadata_value(cand_client)
        if cand_client and not _looks_like_header_label(cand_client):
            metadata["client"] = cand_client

    # Supplier (si Unknown/None)
    if not metadata.get("supplier"):
        supplier_name, conf, _scores = classify_supplier_v2((full_text or "").lower())
        if supplier_name and supplier_name != "Unknown" and conf >= 0.4:
            metadata["supplier"] = supplier_name

    return metadata


def detect_fournisseur(text: str) -> str:
    """
    Détecte le fournisseur à partir du texte du document.
    
    Returns:
        'knauf', 'ffgroup', 'stg', 'aya', 'pgb', ou 'generic'
    """
    text_lower = text.lower()
    
    # FF Group avant Knauf : les docs FF listent souvent des produits « Knauf » dans le corps.
    if 'ff group' in text_lower or 'ffgroup' in text_lower or 'ff-group' in text_lower:
        return 'ffgroup'
    if 'knauf' in text_lower or 'rue du parc industriel' in text_lower:
        return 'knauf'
    elif 'schrauwen' in text_lower or 'stg-group.be' in text_lower or 'stg' in text_lower:
        return 'stg'
    elif 'aya market' in text_lower or 'bara-' in text_lower:
        return 'aya'
    elif 'pgb-europe' in text_lower or 'pgb europe' in text_lower or 'gontrode heirweg' in text_lower:
        return 'pgb'
    else:
        return 'generic'


@app.post('/parse')
async def parse_pdf(
    file: UploadFile = File(...),
    engine: Optional[str] = Query("auto", description="auto | ollama | factory | classifier (séparation explicite des pipelines)"),
    use_ai: Optional[bool] = Query(None, description="IA extraction; si absent → DOCUMENT_AI_USE_AI (.env)"),
    ai_provider: Optional[str] = Query(None, description="openai | gemini | ollama; si absent → DOCUMENT_AI_DEFAULT_PROVIDER (défaut ollama)"),
    ollama_model: Optional[str] = Query(None, description="Modèle Ollama explicite ex. gemma4-rapide:latest"),
    ollama_profile: Optional[str] = Query(None, description="Profil configuré ex. Primary, Quality"),
    ollama_host: Optional[str] = Query(None, description="Surcharge URL Ollama ex. http://localhost:11434"),
    force_catalog: Optional[bool] = Query(False, description="Forcer le parsing en tant que catalogue (ignore la détection automatique)"),
    return_sql: Optional[bool] = Query(False, description="Pour les catalogues: retourner un script SQL au lieu du JSON")
):
    # DEBUG: Vérifier les paramètres reçus AVANT toute normalisation
    import sys
    print(f"[DEBUG] Paramètres bruts reçus par FastAPI:")
    print(f"  - force_catalog (raw): {force_catalog} (type: {type(force_catalog)})")
    print(f"  - return_sql (raw): {return_sql} (type: {type(return_sql)})")
    """
    Parse un document PDF (facture, bon de livraison, ou catalogue).
    Détecte automatiquement le type de document.
    
    Args:
        file: Fichier PDF à parser
        use_ai: Si True, utilise l'IA pour l'extraction
        ai_provider: Fournisseur IA: openai, gemini ou ollama (défaut env / ollama)
        force_catalog: Si True, force le parsing en tant que catalogue (ignore la détection automatique)
        return_sql: Si True et que c'est un catalogue, retourne un script SQL au lieu du JSON
    
    Returns:
        JSON avec 'items' (liste de produits) et 'metadata' (métadonnées)
        OU pour les catalogues: 'products', 'variants', 'images', 'attributes'
        OU si return_sql=True: script SQL d'insertion
    """
    if not file.filename.lower().endswith('.pdf'):
        raise HTTPException(status_code=400, detail='Only PDF supported')

    # Sauvegarder temporairement
    import tempfile
    import uuid
    tmp_dir = os.getenv('TEMP', os.getenv('TMP', '/tmp'))
    os.makedirs(tmp_dir, exist_ok=True)
    tmp = os.path.join(tmp_dir, f"{uuid.uuid4().hex}_{file.filename}")
    
    content = await file.read()
    with open(tmp, 'wb') as f:
        f.write(content)

    try:
        # LOG: Afficher les paramètres reçus
        print("=" * 80)
        print(f"[API] Paramètres reçus:")
        print(f"  - engine: {engine} (type: {type(engine)})")
        print(f"  - use_ai: {use_ai} (type: {type(use_ai)})")
        print(f"  - ai_provider: {ai_provider} (type: {type(ai_provider)})")
        print(f"  - force_catalog: {force_catalog} (type: {type(force_catalog)})")
        print(f"  - return_sql: {return_sql} (type: {type(return_sql)})")
        print("=" * 80)
        
        # Normaliser les booléens (au cas où ils arrivent comme strings)
        # FastAPI devrait convertir automatiquement, mais on s'assure que c'est bien un bool
        if isinstance(force_catalog, str):
            force_catalog = force_catalog.lower() in ('true', '1', 'yes', 'on')
        elif force_catalog is None:
            force_catalog = False
        else:
            force_catalog = bool(force_catalog)
        
        if isinstance(return_sql, str):
            return_sql = return_sql.lower() in ('true', '1', 'yes', 'on')
        elif return_sql is None:
            return_sql = False
        else:
            return_sql = bool(return_sql)
        
        print(f"[API] Paramètres normalisés:")
        print(f"  - force_catalog: {force_catalog} (bool: {isinstance(force_catalog, bool)})")
        print(f"  - return_sql: {return_sql} (bool: {isinstance(return_sql, bool)})")
        print("=" * 80)

        # IA documentaire:
        # - /parse (engine=auto): déterministe par défaut pour éviter les timeouts sur gros PDF.
        # - IA uniquement si explicitement demandée (use_ai=true) ou engine=ollama.
        if use_ai is None:
            use_ai = False
        else:
            use_ai = bool(use_ai)
        if ai_provider is None or (isinstance(ai_provider, str) and not str(ai_provider).strip()):
            # Ollama par défaut (local, sans clé) ; surcharger avec DOCUMENT_AI_DEFAULT_PROVIDER si besoin.
            ai_provider = (os.getenv("DOCUMENT_AI_DEFAULT_PROVIDER", "ollama") or "ollama").strip().lower()
        else:
            ai_provider = str(ai_provider).strip().lower()
        engine_norm = (engine or "auto").strip().lower()
        if engine_norm not in ("auto", "ollama", "factory", "classifier"):
            raise HTTPException(status_code=400, detail=f"engine invalide: {engine}. Valeurs: auto | ollama | factory | classifier")

        strict_ollama = False
        if engine_norm == "ollama":
            # Pipeline strictement Ollama, sans fallback factory.
            use_ai = True
            ai_provider = "ollama"
            strict_ollama = True
        elif engine_norm == "factory":
            # Pipeline strictement déterministe.
            use_ai = False
        elif engine_norm == "classifier":
            # Pipeline déterministe avec classifieur V2 pour sélectionner le parser.
            use_ai = False

        print(f"[API] Pipeline effectif: engine={engine_norm}, use_ai={use_ai}, ai_provider={ai_provider}, strict_ollama={strict_ollama}")
        
        # Vérification critique : si force_catalog=True, on DOIT parser en catalogue
        if force_catalog:
            print("[API] ⚠️ FORCE_CATALOG=TRUE - Le document SERA traité comme catalogue")
        
        # Extraire le texte brut du PDF (pour toutes les méthodes)
        pdf_raw = extract_pdf_raw(tmp)
        raw_text = pdf_raw.get('full_text', '')
        
        # Détecter si c'est un catalogue (ou forcer si demandé)
        catalog_parsed = False  # Flag pour savoir si on a parsé un catalogue
        try:
            from ..catalog_parser import is_catalog, parse_catalog
            
            # Si force_catalog=True, on force le parsing en tant que catalogue
            if force_catalog:
                print("[API] ========================================")
                print("[API] Parsing forcé en tant que CATALOGUE PRODUIT (force_catalog=True)")
                print("[API] ========================================")
                is_catalog_doc = True
            else:
                is_catalog_doc = is_catalog(raw_text)
                print(f"[API] Détection catalogue: {is_catalog_doc}")
            
            if is_catalog_doc:
                print("[API] ========================================")
                print("[API] ⚠️ ENTRÉE DANS LE BLOC CATALOGUE ⚠️")
                print("[API] Document détecté comme CATALOGUE PRODUIT")
                print(f"[API] force_catalog={force_catalog}, return_sql={return_sql}")
                print("[API] ========================================")
                # Parser le catalogue
                # On peut utiliser l'IA ou le parsing basique selon use_ai
                catalog_use_ai = use_ai if use_ai else False
                if catalog_use_ai:
                    print("[API] INFO: Parsing de catalogue avec IA")
                else:
                    print("[API] INFO: Parsing de catalogue sans IA (méthode basique)")
                
                try:
                    print("[API] Appel de parse_catalog...")
                    catalog_result = parse_catalog(tmp, use_ai=catalog_use_ai, ai_provider=ai_provider)
                    print(f"[API] parse_catalog retourné: {type(catalog_result)}")
                    product_count = len(catalog_result.get("products", []))
                    print(f"[API] Catalogue parsé: {product_count} produits extraits")
                    
                    if product_count == 0:
                        print("[API] ATTENTION: Aucun produit extrait du catalogue")
                    
                    # Si return_sql=True, générer et retourner le script SQL
                    if return_sql:
                        from ..catalog_parser import generate_sql_insert_script
                        sql_script = generate_sql_insert_script(catalog_result)
                        print(f"[API] Script SQL généré: {len(sql_script)} caractères")
                        catalog_parsed = True  # Marquer qu'on a parsé un catalogue
                        from fastapi.responses import PlainTextResponse
                        return PlainTextResponse(
                            sql_script,
                            media_type="text/plain",
                            headers={
                                "Content-Disposition": f'attachment; filename="catalog_insert.sql"'
                            }
                        )
                    
                    # Sinon, retourner le JSON
                    catalog_parsed = True  # Marquer qu'on a parsé un catalogue
                    return JSONResponse({
                        "type": "catalog",
                        "products": catalog_result.get("products", []),
                        "variants": catalog_result.get("variants", []),
                        "images": catalog_result.get("images", []),
                        "attributes": catalog_result.get("attributes", []),
                        "count": product_count,
                        "raw_text": raw_text[:5000]  # Limiter le texte brut pour les catalogues
                    })
                except Exception as parse_error:
                    print(f"[API] Erreur lors du parsing du catalogue: {parse_error}")
                    import traceback
                    traceback.print_exc()
                    # Si force_catalog=True, on ne doit JAMAIS continuer avec le parsing facture
                    if force_catalog:
                        error_detail = str(parse_error)
                        # Améliorer le message d'erreur si c'est une erreur de clé API
                        if "Aucune clé API IA disponible" in error_detail or "API.*key" in error_detail.lower():
                            error_detail = (
                                f"Erreur: Le parsing de catalogue nécessite une clé API IA (OpenAI ou Gemini). "
                                f"Détails: {error_detail}. "
                                f"Veuillez configurer OPENAI_API_KEY ou GEMINI_API_KEY dans les variables d'environnement."
                            )
                        raise HTTPException(
                            status_code=500,
                            detail=f"Erreur lors du parsing du catalogue (force_catalog=True): {error_detail}"
                        )
                    # Sinon, re-raise pour que l'erreur soit visible
                    raise
        except ImportError as e:
            # Module catalog_parser non disponible
            if force_catalog:
                # Si force_catalog=True, on ne peut pas continuer sans le module
                raise HTTPException(
                    status_code=500,
                    detail=f"Module catalog_parser non disponible mais force_catalog=True: {e}"
                )
            # Sinon, continuer avec le parsing normal
            print(f"[API] Module catalog_parser non disponible: {e}")
        except HTTPException:
            # Re-raise les HTTPException (déjà gérées)
            raise
        except Exception as e:
            print(f"[API] Erreur lors de la détection/parsing du catalogue: {e}")
            import traceback
            traceback.print_exc()
            # Si force_catalog=True, on ne doit JAMAIS continuer avec le parsing facture
            if force_catalog:
                raise HTTPException(
                    status_code=500,
                    detail=f"Erreur lors du parsing du catalogue (force_catalog=True): {str(e)}"
                )
            # Sinon, continuer avec le parsing normal
        
        # Si on a déjà parsé un catalogue, ne pas continuer
        if catalog_parsed:
            print("[API] Catalogue déjà parsé, arrêt du traitement")
            raise HTTPException(
                status_code=500,
                detail="Erreur: le catalogue a été parsé mais aucune réponse n'a été retournée"
            )
        
        # Si force_catalog=True mais qu'on arrive ici, c'est une erreur
        if force_catalog:
            raise HTTPException(
                status_code=500,
                detail="Erreur: force_catalog=True mais le parsing catalogue n'a pas été exécuté"
            )
        
        # Sinon, c'est une facture ou un bon de livraison
        print("[API] Document détecté comme FACTURE/BON DE LIVRAISON")
        
        # Si use_ai=True, utiliser l'IA pour l'extraction
        if use_ai:
            try:
                ai_products = []
                ai_metadata = {}
                method_name = ""
                ai_success = False
                
                # Choisir le fournisseur IA
                if ai_provider == "gemini":
                    try:
                        from ..ai_extractor_gemini import extract_products_with_gemini, extract_metadata_with_gemini
                        
                        # Extraire les produits avec Gemini
                        ai_products, gemini_model_products = extract_products_with_gemini(tmp)
                        
                        # Extraire les métadonnées avec Gemini
                        ai_metadata, gemini_model_metadata = extract_metadata_with_gemini(tmp)
                        
                        # Utiliser le modèle des produits (généralement le même)
                        gemini_model_used = gemini_model_products or gemini_model_metadata
                        method_name = f"gemini_ai_extractor ({gemini_model_used})"
                        ai_success = True
                    except Exception as gemini_error:
                        print(f"⚠️ Erreur Gemini (fallback sur OpenAI): {gemini_error}")
                        # Fallback sur OpenAI si Gemini échoue
                        try:
                            from ..ai_extractor import extract_products_with_ai, extract_metadata_with_ai
                            ai_products = extract_products_with_ai(tmp)
                            ai_metadata = extract_metadata_with_ai(tmp)
                            method_name = "openai_ai_extractor_fallback"
                            ai_success = True
                        except Exception as openai_error:
                            print(f"⚠️ Erreur OpenAI aussi: {openai_error}")
                            raise ValueError(f"Ni Gemini ni OpenAI ne fonctionnent. Gemini: {gemini_error}, OpenAI: {openai_error}")
                elif ai_provider == "ollama":
                    try:
                        from ..ai_extractor_ollama import extract_products_with_ollama, extract_metadata_with_ollama
                        from ..ai_ollama_config import resolve_ollama_model

                        om = (ollama_model or "").strip() or None
                        op = (ollama_profile or "").strip() or None
                        oh = (ollama_host or "").strip() or None
                        ai_products = extract_products_with_ollama(tmp, model=om, host=oh, profile=op)
                        ai_metadata = extract_metadata_with_ollama(tmp, model=om, host=oh, profile=op)
                        resolved, src = resolve_ollama_model(explicit_model=om, explicit_profile=op)
                        method_name = f"ollama_ai_extractor ({resolved}, {src})"
                        ai_success = True
                    except Exception:
                        # Pas de fallback cloud ; parseur déterministe ensuite — ne pas journaliser l’erreur Ollama.
                        raise
                else:
                    # Par défaut, utiliser OpenAI
                    try:
                        from ..ai_extractor import extract_products_with_ai, extract_metadata_with_ai
                        
                        # Extraire les produits avec OpenAI
                        ai_products = extract_products_with_ai(tmp)
                        
                        # Extraire les métadonnées avec OpenAI
                        ai_metadata = extract_metadata_with_ai(tmp)
                        method_name = "openai_ai_extractor"
                        ai_success = True
                    except Exception as openai_error:
                        print(f"⚠️ Erreur OpenAI (fallback sur Gemini): {openai_error}")
                        # Fallback sur Gemini si OpenAI échoue
                        try:
                            from ..ai_extractor_gemini import extract_products_with_gemini, extract_metadata_with_gemini
                            ai_products, gemini_model_products = extract_products_with_gemini(tmp)
                            ai_metadata, gemini_model_metadata = extract_metadata_with_gemini(tmp)
                            gemini_model_used = gemini_model_products or gemini_model_metadata
                            method_name = f"gemini_ai_extractor_fallback ({gemini_model_used})"
                            ai_success = True
                        except Exception as gemini_error:
                            print(f"⚠️ Erreur Gemini aussi: {gemini_error}")
                            raise ValueError(f"Ni OpenAI ni Gemini ne fonctionnent. OpenAI: {openai_error}, Gemini: {gemini_error}")
                
                if not ai_success:
                    raise ValueError("Aucun fournisseur IA n'a fonctionné")
                
                # Convertir au format attendu
                items = []
                for product in ai_products:
                    items.append({
                        'sku': product.get('product_code'),
                        'ean': product.get('ean'),
                        'description': product.get('raw') or product.get('normalized'),
                        'qty': product.get('quantity', 0),
                        'unit': product.get('unit'),
                        'unit_price': product.get('unit_price'),
                        'line_total': product.get('total_value')
                    })
                
                # Normaliser le type de document
                doc_type = ai_metadata.get('doc_type', '')
                if doc_type:
                    doc_type_lower = doc_type.lower()
                    if 'invoice' in doc_type_lower or 'facture' in doc_type_lower or 'factuur' in doc_type_lower:
                        doc_type = 'Factuur'
                    elif 'delivery' in doc_type_lower or 'livraison' in doc_type_lower or 'leverings' in doc_type_lower:
                        doc_type = 'Leveringsbevestiging'
                    else:
                        doc_type = 'Factuur'  # Par défaut
                
                result = {
                    'items': items,
                    'metadata': {
                        'type': doc_type,
                        'number': ai_metadata.get('number'),
                        'client': ai_metadata.get('client'),
                        'supplier': ai_metadata.get('supplier'),
                        'date': ai_metadata.get('date'),
                        'count': len(items),
                        'method': method_name,
                        # Informations supplémentaires du fournisseur
                        'supplier_code': ai_metadata.get('supplier_code'),
                        'supplier_address': ai_metadata.get('supplier_address'),
                        'supplier_phone': ai_metadata.get('supplier_phone'),
                        'supplier_email': ai_metadata.get('supplier_email'),
                        'supplier_contact': ai_metadata.get('supplier_contact'),
                        'supplier_payment_terms': ai_metadata.get('supplier_payment_terms')
                    },
                    'raw_text': _limit_raw_text_for_response(raw_text)
                }
                
                return JSONResponse(result)
                
            except ValueError as e:
                msg = f"⚠️ Erreur IA ({'sans fallback' if strict_ollama else 'fallback sur parser déterministe'}): {e}"
                print(msg)
                import traceback
                traceback.print_exc()
                if strict_ollama:
                    raise HTTPException(status_code=502, detail=f"Echec pipeline ollama: {e}")
                use_ai = False
            except Exception as e:
                msg = f"⚠️ Erreur IA ({'sans fallback' if strict_ollama else 'fallback sur parser déterministe'}): {e}"
                print(msg)
                import traceback
                traceback.print_exc()
                if strict_ollama:
                    raise HTTPException(status_code=502, detail=f"Echec pipeline ollama: {e}")
                use_ai = False
        
        # Parser déterministe (par défaut ou fallback)
        if not use_ai:
            print("[API] Utilisation du Parser Factory (Nouvelle architecture)")
            try:
                # Utiliser la factory pour créer le parser approprié
                # Note: create_parser détecte automatiquement le fournisseur si non spécifié
                from app.parsers.parser_factory import create_parser
                
                detection_mode = "legacy" if engine_norm == "factory" else "classifier"
                parser = create_parser(tmp, detection_mode=detection_mode)
                print(f"[API] Factory a instancié: {parser.__class__.__name__}")
                
                # Extraire les données
                products = parser.extract_products()
                metadata = parser.extract_metadata()
                metadata = _sanitize_factory_metadata(metadata)
                
                # S'assurer que les métadonnées contiennent toutes les informations requises par l'API
                supplier_fields = [
                    'supplier_code', 'supplier_address', 'supplier_phone',
                    'supplier_email', 'supplier_contact', 'supplier_payment_terms'
                ]
                for field in supplier_fields:
                    if field not in metadata:
                        metadata[field] = None
                
                # Ajouter l'info sur la méthode utilisée
                metadata['method'] = f"factory_{parser.__class__.__name__}"

                doc_type = (metadata.get("doc_type") or "").lower()
                if hasattr(parser, "summarize_invoice_amounts") and doc_type != "delivery":
                    try:
                        amounts = parser.summarize_invoice_amounts(products)
                        metadata.update(amounts)
                        footer = amounts.get("invoice_total_excl_vat")
                        lines_total = amounts.get("lines_total_excl_vat")
                        if footer and lines_total and abs(footer - lines_total) > 0.05:
                            print(
                                f"[PARSE] Écart HT: lignes={lines_total} "
                                f"facture={footer} Δ={amounts.get('total_discrepancy')}"
                            )
                    except Exception as amount_ex:
                        print(f"[PARSE] summarize_invoice_amounts: {amount_ex}")
                
                # Construire le résultat final
                # Utiliser full_text du parser (qui inclut l'OCR si activé) ou fallback sur pdf_raw
                raw_text_content = getattr(parser, 'full_text', pdf_raw.get('full_text', ''))
                if engine_norm == "classifier":
                    metadata = _semantic_enrich_classifier_metadata(metadata, raw_text_content)
                
                result = {
                    'items': products,
                    'metadata': metadata,
                    'raw_text': _limit_raw_text_for_response(raw_text_content)
                }
                
                # Log pour debug
                print(f"[PARSE] Extraction terminée: {len(products)} produits, Fournisseur: {metadata.get('supplier')}")
                
                return JSONResponse(result)
                
            except Exception as e_factory:
                print(f"[API] Erreur Parser Factory: {e_factory}")
                import traceback
                traceback.print_exc()
                raise HTTPException(status_code=500, detail=f"Erreur factory: {str(e_factory)}")
    
    except Exception as e:
        print(f"Erreur: {e}")
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))
    
    finally:
        # Nettoyer
        try:
            os.remove(tmp)
        except Exception:
            pass


@app.post('/parse/ollama')
async def parse_pdf_ollama(
    file: UploadFile = File(...),
    ollama_model: Optional[str] = Query(None, description="Modèle Ollama explicite ex. gemma4-rapide:latest"),
    ollama_profile: Optional[str] = Query(None, description="Profil configuré ex. Primary, Quality"),
    ollama_host: Optional[str] = Query(None, description="Surcharge URL Ollama ex. http://localhost:11434"),
):
    """Pipeline Ollama strict (pas de fallback sur parser factory)."""
    return await parse_pdf(
        file=file,
        engine="ollama",
        use_ai=True,
        ai_provider="ollama",
        ollama_model=ollama_model,
        ollama_profile=ollama_profile,
        ollama_host=ollama_host,
        force_catalog=False,
        return_sql=False,
    )


@app.post('/parse/factory')
async def parse_pdf_factory(
    file: UploadFile = File(...),
):
    """Pipeline parser déterministe strict (sans IA)."""
    return await parse_pdf(
        file=file,
        engine="factory",
        use_ai=False,
        ai_provider="ollama",
        ollama_model=None,
        ollama_profile=None,
        ollama_host=None,
        force_catalog=False,
        return_sql=False,
    )


@app.post('/parse/classifier')
async def parse_pdf_classifier(
    file: UploadFile = File(...),
):
    """Pipeline parser déterministe avec classifieur V2 (sans IA)."""
    return await parse_pdf(
        file=file,
        engine="classifier",
        use_ai=False,
        ai_provider="ollama",
        ollama_model=None,
        ollama_profile=None,
        ollama_host=None,
        force_catalog=False,
        return_sql=False,
    )