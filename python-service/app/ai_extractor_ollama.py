"""
Extracteur basé sur Ollama (gratuit, local, sans quota).
Utilise Ollama pour une extraction précise sans limitation.
"""
import os
import json
import logging
import re
import pdfplumber
import requests
from typing import List, Dict, Optional, Tuple

# Configuration du logging
logger = logging.getLogger(__name__)

# Rétrocompatibilité (préférer get_ollama_host / resolve_ollama_model)
OLLAMA_HOST = os.getenv("OLLAMA_HOST", "http://localhost:11434")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "qwen2.5:7b-instruct")

from .ai_ollama_config import (
    get_ollama_host,
    resolve_ollama_model,
    get_ollama_chat_request_timeout,
    get_ollama_chat_options_products,
    get_ollama_chat_options_metadata,
    get_ollama_chat_think_for_payload,
)

def check_ollama_available(host: Optional[str] = None) -> bool:
    """Vérifie si Ollama est disponible et accessible"""
    base = (host or get_ollama_host()).rstrip("/")
    try:
        response = requests.get(f"{base}/api/tags", timeout=2)
        return response.status_code == 200
    except:
        return False

def get_available_models(host: Optional[str] = None) -> List[str]:
    """Récupère la liste des modèles disponibles dans Ollama"""
    base = (host or get_ollama_host()).rstrip("/")
    try:
        response = requests.get(f"{base}/api/tags", timeout=5)
        if response.status_code == 200:
            data = response.json()
            models = [model.get("name", "") for model in data.get("models", [])]
            return models
    except:
        pass
    return []


def _strip_json_fences(raw: str) -> str:
    s = raw.strip()
    m = re.match(r"^```(?:json)?\s*([\s\S]*?)\s*```$", s, re.IGNORECASE)
    if m:
        return m.group(1).strip()
    return s


def _balanced_object_from(s: str, start: int) -> Optional[str]:
    """Extrait un objet JSON `{...}` équilibré depuis `start` (quotes et `\"`)."""
    if start < 0 or start >= len(s) or s[start] != "{":
        return None
    depth = 0
    in_str = False
    esc = False
    quote = ""
    for j in range(start, len(s)):
        c = s[j]
        if in_str:
            if esc:
                esc = False
            elif c == "\\":
                esc = True
            elif c == quote:
                in_str = False
            continue
        if c in ('"', "'"):
            in_str = True
            quote = c
            continue
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return s[start : j + 1]
    return None


def _find_json_object_in_text(blob: str, anchors: Tuple[str, ...]) -> Optional[str]:
    """Repère un objet JSON dans du texte (thinking, markdown, prose)."""
    blob = blob.strip()
    if not blob:
        return None
    blob = _strip_json_fences(blob)
    if blob.startswith("{") and blob.endswith("}"):
        return blob
    for m in re.finditer(r"```(?:json)?\s*(\{)", blob, re.IGNORECASE):
        st = m.start(1)
        got = _balanced_object_from(blob, st)
        if got:
            return got
    for anchor in anchors:
        pos = blob.rfind(anchor)
        if pos == -1:
            continue
        st = blob.rfind("{", 0, pos)
        if st == -1:
            continue
        got = _balanced_object_from(blob, st)
        if got:
            return got
    st = blob.rfind("{")
    if st != -1:
        got = _balanced_object_from(blob, st)
        if got:
            return got
    st = blob.find("{")
    if st != -1:
        return _balanced_object_from(blob, st)
    return None


def ollama_chat_assistant_json_text(
    result_data: dict,
    *,
    json_anchors: Tuple[str, ...],
    log_label: str = "ollama",
) -> str:
    """
    Texte JSON assistant depuis la réponse POST /api/chat (non stream).
    Gère content vide, champs ``thinking``, blocs ```json```, et ``error`` racine.
    """
    if not isinstance(result_data, dict):
        raise ValueError(f"{log_label}: réponse Ollama invalide (type={type(result_data).__name__})")
    err = result_data.get("error")
    if err:
        raise ValueError(f"{log_label}: erreur Ollama — {err}")
    msg = result_data.get("message")
    if msg is None:
        msg = {}
    if not isinstance(msg, dict):
        msg = {}

    def _norm_field(v) -> str:
        if v is None:
            return ""
        if isinstance(v, (dict, list)):
            return json.dumps(v, ensure_ascii=False)
        return str(v).strip()

    content_s = _norm_field(msg.get("content"))
    thinking_s = _norm_field(msg.get("thinking"))

    for label, blob in (("content", content_s), ("thinking", thinking_s)):
        if not blob:
            continue
        extracted = _find_json_object_in_text(blob, json_anchors)
        if extracted:
            if label == "thinking" and not content_s:
                logger.info("%s: JSON extrait depuis message.thinking (content vide)", log_label)
            return extracted
        stripped = _strip_json_fences(blob)
        if stripped.startswith("{"):
            return stripped

    tool_calls = msg.get("tool_calls")
    dr = result_data.get("done_reason")
    detail = (
        f"{log_label}: réponse sans JSON exploitable "
        f"(done_reason={dr!r}, "
        f"eval_count={result_data.get('eval_count')!r}, "
        f"model={result_data.get('model')!r}, "
        f"tool_calls={bool(tool_calls)}, "
        f"content_len={len(content_s)}, thinking_len={len(thinking_s)})"
    )
    if dr == "length":
        detail += (
            " — sortie tronquée par num_predict. "
            "Augmenter OLLAMA_NUM_PREDICT_PRODUCTS (défaut 32768) ou mettre "
            "OLLAMA_CHAT_THINK_EXTRACTION=false (défaut) pour libérer le budget JSON."
        )
    logger.warning(detail)
    raise ValueError(detail)


def _extract_metadata_with_regex(pdf_text: str) -> Dict:
    """
    Extrait les métadonnées avec regex (méthode fiable).
    Utilisée pour valider/corriger les résultats d'Ollama.
    """
    metadata = {
        "doc_type": None,
        "number": None,
        "client": None,
        "supplier": None,
        "date": None
    }
    
    text_lower = pdf_text.lower()
    
    # Type de document
    if "factuur" in text_lower or "invoice" in text_lower:
        metadata["doc_type"] = "invoice"
    elif "leveringsbon" in text_lower or "delivery note" in text_lower:
        metadata["doc_type"] = "delivery"
    
    # Fournisseur — FF Group avant Knauf (produits Knauf cités sur factures FF)
    if "ff group" in text_lower or "ffgroup" in text_lower or "ff-group" in text_lower:
        metadata["supplier"] = "FF Group"
    elif "knauf" in text_lower:
        metadata["supplier"] = "Knauf"
    elif "stg" in text_lower and "tool" in text_lower:
        metadata["supplier"] = "STG"
    
    is_delivery = metadata.get("doc_type") == "delivery" or any(
        k in text_lower
        for k in ("leveringsbon", "delivery note", "bon de livraison", "verzendbon", "pakbon", "leveringsbevestiging")
    )

    # Numéro de bon de livraison (priorité si le texte ressemble à un BL)
    delivery_number_patterns = [
        # FF Group : tableau ISSUE TIME puis numéro puis DELIVERY NOTE (lignes séparées)
        r'ISSUE\s+TIME\s*[\r\n]+\s*(\d{4,8})\s*[\r\n]+\s*DELIVERY\s+NOTE\b',
        r'(?:^|[\r\n])\s*(\d{4,8})\s*[\r\n]+\s*DELIVERY\s+NOTE\b',
        r'Leveringsbon\s*(?:nr\.?|nummer|n[°o]?)\s*:?\s*([A-Z0-9][A-Z0-9\-/\.]*)',
        r'Leveringsbon\s+(\d{4,})',
        r'Delivery\s+note\s*(?:n[°o]?|nr\.?|#|no\.?)?\s*:?\s*([A-Z0-9][A-Z0-9\-/\.]*)',
        r'Bon\s+de\s+livraison\s*(?:n[°o]?|nr\.?)?\s*:?\s*([A-Z0-9][A-Z0-9\-/\.]*)',
        r'(?:^|\n)\s*(?:BL|B/L)\s*(?:nr\.?|n[°o]?)?\s*:?\s*([A-Z0-9][A-Z0-9\-/\.]*)',
        r'Verzendbon\s*(?:nr\.?)?\s*:?\s*([A-Z0-9][A-Z0-9\-/\.]*)',
        r'Pakbon\s*(?:nr\.?)?\s*:?\s*([A-Z0-9][A-Z0-9\-/\.]*)',
        r'(?:NUMBER|NUMERO|NR)\s*[:.]?\s*(\d{4,8})\b',  # FF Group style près du bandeau
    ]

    invoice_number_patterns = [
        r'Faktuur\s*nr\.?\s*:?\s*(\d+)',
        r'Factuur\s*nr\.?\s*:?\s*(\d+)',
        r'Faktuurnr\.?\s*:?\s*(\d+)',
        r'Invoice\s*number\s*:?\s*(\d+)',
    ]

    def _try_number_patterns(patterns: list) -> bool:
        for pattern in patterns:
            match = re.search(pattern, pdf_text, flags=re.IGNORECASE | re.MULTILINE)
            if match:
                cand = (match.group(1) or "").strip()
                if cand and any(c.isdigit() for c in cand):
                    metadata["number"] = cand
                    return True
        return False

    if is_delivery:
        if not _try_number_patterns(delivery_number_patterns):
            _try_number_patterns(invoice_number_patterns)
    else:
        if not _try_number_patterns(invoice_number_patterns):
            _try_number_patterns(delivery_number_patterns)
    
    # Client - patterns améliorés pour tous les fournisseurs
    client_patterns = [
        r'Opdrachtgever\s*:?\s*(.+?)(?=\n\n|\nFaktuur|\nContactpersoon|\nTel:|\nEmail:|$)',
        r'Goederenontvanger\s*:?\s*(.+?)(?=\n\n|\nContactpersoon|\nTel:|\nEmail:|$)',
        r'Client\s*:?\s*(.+?)(?=\n\n|\nContact|\nTel:|\nEmail:|$)',
        r'Klant\s*:?\s*(.+?)(?=\n\n|\nContact|\nTel:|\nEmail:|$)'
    ]
    for pattern in client_patterns:
        match = re.search(pattern, pdf_text, flags=re.IGNORECASE | re.MULTILINE | re.DOTALL)
        if match:
            client_text = match.group(1).strip()
            lines = [line.strip() for line in client_text.split('\n') if line.strip()]
            valid_lines = []
            for line in lines:
                # Filtrer les lignes invalides (en-têtes, numéros, contacts, etc.)
                if not (line.lower().startswith('klantnummer') or 
                        line.lower().startswith('nummer') or
                        line.lower().startswith('contact') or
                        line.lower().startswith('afleveradres') or
                        line.lower().startswith('order') or
                        line.lower().startswith('datum') or
                        line.lower().startswith('balance') or
                        'eur' in line.lower() or
                        'euro' in line.lower() or
                        (any(char.isdigit() for char in line) and len(line) < 20) or
                        re.match(r'^[A-Z][a-z]+\s+[A-Z][a-z]+$', line) or  # Noms de personnes
                        len(line) < 5):  # Trop court
                    valid_lines.append(line)
            if valid_lines:
                # Prendre la première ligne qui ressemble à un nom d'entreprise
                for line in valid_lines:
                    if re.search(r'[A-Z]{2,}|sprl|sa|ltd|bv|nv|\d{4,}', line, flags=re.IGNORECASE):
                        metadata["client"] = line.strip()
                        break
                # Si pas trouvé, prendre la première ligne valide
                if not metadata.get("client") and valid_lines:
                    metadata["client"] = valid_lines[0].strip()
                if metadata.get("client"):
                    break
    
    return metadata

def _is_invalid_value(value: str, field_type: str) -> bool:
    """Détecte si une valeur est invalide (en-tête, numéro de client, etc.)"""
    if not value or not value.strip():
        return True
    
    value_lower = value.lower().strip()
    
    # Détection FORCÉE pour "΄S BALANCE (EUR)" et variantes
    if field_type == "client":
        # Si contient "balance" ET "(eur)" ou "(euro)", c'est FORCÉMENT un en-tête
        if "balance" in value_lower and ("(eur)" in value_lower or "(euro)" in value_lower):
            logger.warning(f"🚨 Détection FORCÉE: '{value}' est un en-tête (contient balance + eur/euro)")
            return True
    
    # Mots-clés d'en-têtes de colonnes à ignorer
    invalid_keywords = [
        "order", "datum", "offerte", "balance", "eur", "euro", "total",
        "nummer", "number", "client", "klant", "customer", "date",
        "factuur", "invoice", "leveringsbon", "delivery"
    ]
    
    # Vérifier si c'est un en-tête de colonne
    if any(keyword in value_lower for keyword in invalid_keywords):
        # Si c'est court OU contient des parenthèses avec EUR/EURO/BALANCE, c'est un en-tête
        if len(value.split()) <= 5 or re.search(r'\(.*(eur|euro|balance).*\)', value_lower):
            return True
    
    if field_type == "number":
        v = value.strip()
        if len(v) <= 2:
            return True
        # Fragment d’en-tête / hallucination (ex. « TES » depuis un tableau) : pas de chiffre
        if not any(ch.isdigit() for ch in v):
            return True
        # Trop court et uniquement des lettres (ex. REF, N_A)
        if len(v) <= 4 and v.isalpha():
            return True
    
    # Client qui contient "nummer" ou est un numéro
    if field_type == "client":
        if "nummer" in value_lower or value.strip().isdigit():
            return True
        if len(value.strip()) < 5:  # Trop court
            return True
        # Détecter les en-têtes avec parenthèses contenant EUR/EURO/BALANCE
        if re.search(r'\(.*(eur|euro|balance).*\)', value_lower, re.IGNORECASE):
            return True
        # Détecter les valeurs contenant "balance" (en-tête de colonne) - FORCER la détection
        if "balance" in value_lower:
            # Si contient "balance" ET (EUR) ou est court, c'est un en-tête
            if "(eur)" in value_lower or "(euro)" in value_lower or len(value.split()) <= 5:
                return True
        # Détecter les valeurs qui commencent par un caractère spécial suivi de "S BALANCE"
        if re.search(r'^[\'΄\'\u0384\u2019].*balance', value_lower, re.IGNORECASE):
            return True
        # Détecter "S BALANCE" ou "BALANCE" seul (avec ou sans caractère spécial au début)
        if re.match(r'^[\'΄\'\u0384\u2019]?\s*s\s*balance', value_lower, re.IGNORECASE) or value_lower.strip() == "balance":
            return True
        # Détecter spécifiquement "΄S BALANCE (EUR)" ou variantes
        if re.search(r'[\'΄\'\u0384\u2019]\s*s\s*balance.*\(.*eur', value_lower, re.IGNORECASE):
            return True
        # Détecter les en-têtes avec parenthèses contenant EUR/EURO/BALANCE
        if re.search(r'\(.*(eur|euro|balance).*\)', value_lower):
            return True
        # Détecter les valeurs qui commencent par un caractère spécial suivi de "S BALANCE"
        if re.search(r'^[\'΄].*balance', value_lower):
            return True
    
    return False

def _validate_and_correct_metadata(metadata: Dict, pdf_text: str) -> Dict:
    """
    Valide et corrige les métadonnées extraites par Ollama en utilisant regex.
    REMPLACE SYSTÉMATIQUEMENT les valeurs incorrectes par celles de regex.
    """
    print(f"🔍 [validate] Validation des métadonnées: {metadata}")
    logger.info(f"🔍 Validation des métadonnées: {metadata}")
    
    # Extraire avec regex pour avoir les valeurs correctes
    regex_metadata = _extract_metadata_with_regex(pdf_text)
    print(f"🔍 [validate] Métadonnées regex extraites: {regex_metadata}")
    logger.info(f"🔍 Métadonnées regex extraites: {regex_metadata}")
    
    text_lower = pdf_text.lower()
    
    # Numéro : regex prioritaire ; si IA renvoie du texte sans chiffres (ex. « TES »), corriger ou effacer
    current_number = metadata.get("number", "")
    if regex_metadata.get("number"):
        if _is_invalid_value(str(current_number or ""), "number") or str(current_number or "").strip() != str(
            regex_metadata["number"]
        ).strip():
            metadata["number"] = regex_metadata["number"]
            logger.info("Numéro corrigé avec regex: %s → %s", current_number, regex_metadata["number"])
    elif not current_number and regex_metadata.get("number"):
        metadata["number"] = regex_metadata["number"]
        logger.info("Numéro ajouté avec regex: %s", regex_metadata["number"])
    elif _is_invalid_value(str(current_number or ""), "number"):
        metadata["number"] = None
        logger.info("Numéro IA invalide supprimé (aucune regex): %s", current_number)
    
    # Client : TOUJOURS utiliser regex si disponible et que la valeur actuelle est suspecte
    current_client = metadata.get("client", "")
    
    # Détection FORCÉE pour "΄S BALANCE (EUR)" - vérification directe AVANT _is_invalid_value
    current_client_lower = current_client.lower() if current_client else ""
    is_balance_header = "balance" in current_client_lower and ("(eur)" in current_client_lower or "(euro)" in current_client_lower or "eur)" in current_client_lower)
    
    is_invalid = _is_invalid_value(current_client, "client") or is_balance_header
    print(f"🔍 [validate] Validation client: '{current_client}' -> invalide={is_invalid} (balance_header={is_balance_header}), regex_client={regex_metadata.get('client')}")
    logger.info(f"Validation client: '{current_client}' -> invalide={is_invalid} (balance_header={is_balance_header}), regex_client={regex_metadata.get('client')}")
    
    if is_invalid:
        # Si le client actuel est invalide, utiliser regex si disponible
        if regex_metadata.get("client"):
            metadata["client"] = regex_metadata["client"]
            logger.info(f"✅ Client corrigé avec regex: '{current_client}' → '{regex_metadata['client']}'")
        else:
            # Si regex n'a rien trouvé mais que la valeur est invalide, mettre None
            logger.warning(f"⚠️ Client invalide détecté mais regex n'a pas trouvé de remplacement: '{current_client}'")
            metadata["client"] = None  # Mieux vaut None qu'une valeur incorrecte
    elif regex_metadata.get("client") and current_client != regex_metadata["client"]:
        # Si regex a trouvé quelque chose de différent, l'utiliser
        metadata["client"] = regex_metadata["client"]
        logger.info(f"Client remplacé avec regex: '{current_client}' → '{regex_metadata['client']}'")
    elif not current_client and regex_metadata.get("client"):
        metadata["client"] = regex_metadata["client"]
        logger.info(f"Client ajouté avec regex: '{regex_metadata['client']}'")
    
    # Fournisseur : TOUJOURS utiliser regex si disponible
    if regex_metadata.get("supplier"):
        if not metadata.get("supplier") or metadata.get("supplier") != regex_metadata["supplier"]:
            metadata["supplier"] = regex_metadata["supplier"]
            logger.info(f"Fournisseur corrigé avec regex: {metadata.get('supplier')} → {regex_metadata['supplier']}")
    elif not metadata.get("supplier"):
        # Fallback sur détection simple
        if "ff group" in text_lower or "ffgroup" in text_lower or "ff-group" in text_lower:
            metadata["supplier"] = "FF Group"
        elif "knauf" in text_lower:
            metadata["supplier"] = "Knauf"
        elif "stg" in text_lower and "tool" in text_lower:
            metadata["supplier"] = "STG"
    
    # Type de document : utiliser regex si manquant
    if not metadata.get("doc_type") and regex_metadata.get("doc_type"):
        metadata["doc_type"] = regex_metadata["doc_type"]
    
    return metadata

def extract_text_from_pdf(path: str) -> str:
    """Extrait tout le texte d'un PDF avec PyMuPDF (plus rapide)"""
    from .utils.pdf_extractor import extract_text_from_pdf as extract_text
    return extract_text(path)

def extract_products_with_ollama(
    path: str,
    model: Optional[str] = None,
    host: Optional[str] = None,
    profile: Optional[str] = None,
) -> List[Dict]:
    """
    Extrait les produits d'une facture en utilisant Ollama (gratuit, local).
    model: surcharge directe du tag Ollama (ex: gemma4-rapide:latest)
    profile: nom de profil (Primary, Quality, …) — mappé via OLLAMA_PROFILE_* ou nom=modèle
    host: surcharge de l'URL de base Ollama
    """
    base_host = (host or get_ollama_host()).rstrip("/")
    # Vérifier que Ollama est disponible
    if not check_ollama_available(base_host):
        logger.debug("Ollama non accessible (%s)", base_host)
        raise ConnectionError("Ollama n'est pas accessible")
    
    # Vérifier que le modèle est disponible
    available_models = get_available_models(base_host)
    if not available_models:
        logger.debug("Aucun modèle Ollama disponible sur %s", base_host)
        raise ValueError("Aucun modèle Ollama disponible")
    
    resolved, src = resolve_ollama_model(explicit_model=model, explicit_profile=profile)
    model_to_use = resolved
    if model_to_use not in available_models:
        logger.warning("Modèle %s (%s) non dans la liste locale, tentative correspondance partielle", model_to_use, src)
        prefix = model_to_use.split(":")[0] if ":" in model_to_use else model_to_use
        match = next((m for m in available_models if m == model_to_use or m.startswith(prefix + ":")), None)
        if match:
            model_to_use = match
        else:
            logger.warning("Modèle %s non disponible, utilisation de %s", model_to_use, available_models[0])
            model_to_use = available_models[0]
    
    logger.info("Extraction Ollama des produits depuis %s avec modèle %s (config=%s)", path, model_to_use, src)
    
    # Extraire le texte du PDF
    pdf_text = extract_text_from_pdf(path)
    
    if not pdf_text or len(pdf_text.strip()) < 50:
        logger.warning("PDF vide ou trop court - impossible d'extraire")
        return []
    
    logger.info(f"Texte extrait: {len(pdf_text)} caractères")
    
    # Limiter le texte pour Ollama
    text_limit = 15000
    truncated_text = pdf_text[:text_limit]
    if len(pdf_text) > text_limit:
        logger.warning(f"Texte tronqué de {len(pdf_text)} à {text_limit} caractères")
    
    system_prompt = """Tu es un expert en extraction de données de factures. 
Extrais tous les produits d'une facture et retourne-les sous forme de JSON.

Pour chaque produit, extrais :
- quantity: la quantité (nombre entier)
- unit: l'unité (PAC, PC, KG, etc.)
- product_code: le code article (souvent après "Artikel:")
- description: la description complète du produit (sans le numéro de position au début)
- ean: le code EAN si présent
- unit_price: le prix unitaire net
- total_value: la valeur totale nette

IMPORTANT:
- Si la description commence par un numéro (ex: "90 Flex voegmortel"), enlève le numéro (c'est le numéro de position)
- La description doit être complète, pas tronquée
- Les prix doivent être des nombres décimaux (utiliser le point comme séparateur)
- Retourne UNIQUEMENT un JSON valide, sans texte avant ou après
- Si un champ n'est pas trouvé, utilise null
"""

    user_prompt = f"""Extrais tous les produits de cette facture et retourne-les sous forme de JSON.

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

Texte de la facture:
{truncated_text}
"""

    try:
        # Appel à Ollama
        url = f"{base_host}/api/chat"
        payload = {
            "model": model_to_use,
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt}
            ],
            "stream": False,
            "format": "json",
            "options": get_ollama_chat_options_products(),
        }
        think_flag = get_ollama_chat_think_for_payload()
        if think_flag is not None:
            payload["think"] = think_flag

        chat_timeout = get_ollama_chat_request_timeout()
        logger.info(
            "Appel Ollama avec modèle %s (%s) timeout=%s options=%s think=%s",
            model_to_use,
            url,
            chat_timeout,
            payload.get("options"),
            think_flag,
        )
        response = requests.post(url, json=payload, timeout=chat_timeout)
        response.raise_for_status()
        
        result_data = response.json()
        response_content = ollama_chat_assistant_json_text(
            result_data,
            json_anchors=('"products"', "'products'", '"items"'),
            log_label="Ollama produits",
        )
        logger.info("Réponse JSON assistant (produits): %s caractères", len(response_content))
        result = json.loads(response_content)
        products = result.get("products", [])
        
        logger.info(f"{len(products)} produits extraits par Ollama")
        
        # Normaliser les produits
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
        return normalized_products
        
    except requests.exceptions.RequestException:
        logger.debug("Requête Ollama échouée (produits)", exc_info=True)
        raise
    except json.JSONDecodeError:
        logger.debug("JSON Ollama invalide (produits)", exc_info=True)
        raise
    except Exception:
        logger.debug("Extraction produits Ollama échouée", exc_info=True)
        raise

def extract_metadata_with_ollama(
    path: str,
    model: Optional[str] = None,
    host: Optional[str] = None,
    profile: Optional[str] = None,
) -> Dict:
    """
    Extrait les métadonnées d'un document en utilisant Ollama (gratuit, local).
    Voir extract_products_with_ollama pour model / host / profile.
    """
    base_host = (host or get_ollama_host()).rstrip("/")
    # Vérifier que Ollama est disponible
    if not check_ollama_available(base_host):
        logger.debug("Ollama non accessible (%s)", base_host)
        raise ConnectionError("Ollama n'est pas accessible")
    
    # Vérifier que le modèle est disponible
    available_models = get_available_models(base_host)
    if not available_models:
        logger.debug("Aucun modèle Ollama disponible sur %s", base_host)
        raise ValueError("Aucun modèle Ollama disponible")
    
    resolved, src = resolve_ollama_model(explicit_model=model, explicit_profile=profile)
    model_to_use = resolved
    if model_to_use not in available_models:
        prefix = model_to_use.split(":")[0] if ":" in model_to_use else model_to_use
        match = next((m for m in available_models if m == model_to_use or m.startswith(prefix + ":")), None)
        if match:
            model_to_use = match
        else:
            logger.warning("Modèle %s non disponible, utilisation de %s", model_to_use, available_models[0])
            model_to_use = available_models[0]
    
    logger.info(f"Extraction Ollama des métadonnées depuis {path} avec modèle {model_to_use}")
    
    # Extraire le texte du PDF (PREMIÈRE PAGE SEULEMENT pour les métadonnées - beaucoup plus rapide)
    from .utils.pdf_extractor import extract_pdf_raw
    pdf_raw = extract_pdf_raw(path, max_pages=1)
    pdf_text = pdf_raw['pages'][0]['text'] if pdf_raw['pages'] else ""
    
    if not pdf_text:
        logger.warning("PDF vide - impossible d'extraire les métadonnées")
        return {
            "doc_type": None,
            "number": None,
            "client": None,
            "supplier": None
        }
    
    # Limiter drastiquement le texte pour accélérer (métadonnées sont en haut de la première page)
    # Prendre seulement les 3000 premiers caractères (suffisant pour les métadonnées)
    if len(pdf_text) > 3000:
        pdf_text = pdf_text[:3000]
        logger.info(f"Texte tronqué à 3000 caractères pour accélérer l'extraction")
    
    logger.info(f"Texte extrait pour métadonnées: {len(pdf_text)} caractères")
    
    user_prompt = f"""Tu dois extraire les métadonnées de ce document PDF (facture ou bon de livraison) et retourner un JSON.

Format JSON attendu:
{{
  "typeDocument": "",
  "numero": "",
  "client": "",
  "dateDocument": "",
  "supplier": ""
}}

RÈGLES D'EXTRACTION (valables pour TOUS les documents et fournisseurs):

1. typeDocument:
   - "Facture" si tu vois: "factuur", "invoice", "facture", "faktura"
   - "Bon de livraison" si tu vois: "leveringsbon", "delivery note", "bon de livraison", "livraison"
   - Si aucun des deux, utilise "Facture" par défaut

2. numero: Le numéro du document (facture OU bon de livraison). Il DOIT contenir au moins un chiffre (souvent 4 à 12 chiffres, ou alphanumérique type AB-123456).
   Pour les FACTURES, cherche:
   - "Faktuur nr." ou "Factuur nr." ou "Faktuurnr." suivi du numéro
   - "Invoice number" ou "Numéro facture" suivi du numéro
   
   Pour les BONS DE LIVRAISON, cherche en priorité:
   - "Leveringsbon nr." / "Leveringsbon nummer" suivi du numéro
   - "Delivery note" + numéro sur la même zone d'en-tête
   - "Verzendbon", "Pakbon", "BL nr." si présents
   
   ⚠️ IGNORE (ne JAMAIS mettre dans "numero"):
   - Mots d'en-tête de colonne du tableau (ex. fragments "POS", "ART", "TES", "CODE")
   - "Klantnummer" / "Customer number" (numéro de client)
   - Toute valeur sans chiffre (pas un numéro de document crédible)

3. client: Le nom de l'entreprise cliente
   Cherche ces mots-clés suivis du nom de l'entreprise:
   - "Opdrachtgever", "Goederenontvanger", "Client", "Klant", "Customer"
   - "Afleveradres" (mais prends seulement le nom, pas l'adresse complète)
   - "Livré à", "Delivered to"
   
   ⚠️ IGNORE:
   - Les adresses complètes (rues, villes, codes postaux)
   - Les numéros de client ("Klantnummer", "Customer number")
   - Les textes comme "nummer", "contactpersoon", "Contactpersoon"
   - Les noms de personnes (ce sont des contacts, pas le client)

4. supplier: Le nom du fournisseur qui émet le document
   Cherche en haut du document (premières lignes):
   - "Knauf", "N et B KNAUF" → "Knauf"
   - "FF Group", "FFGroup" → "FF Group"
   - "STG", "STG Tool" → "STG"
   - Ou tout autre nom d'entreprise en haut du document

5. dateDocument: La date du document
   - Format: "YYYY-MM-DD" (ex: "2025-11-17")
   - Cherche "Datum", "Date", "Factuurdatum", etc.

EXEMPLES:

❌ FAUX:
{{
  "numero": "1077086",  // ❌ C'est "Klantnummer" (numéro de client), pas de document
  "client": "nummer 1077086"  // ❌ Ce n'est pas le nom du client
}}

✅ CORRECT:
{{
  "typeDocument": "Facture",
  "numero": "713861483",  // ✅ Numéro après "Faktuur nr."
  "client": "10170600 Euro Brico sprl",  // ✅ Nom après "Goederenontvanger"
  "dateDocument": "2025-11-17",
  "supplier": "Knauf"
}}

Texte du document:
{pdf_text[:10000]}"""

    try:
        # Appel à Ollama
        url = f"{base_host}/api/chat"
        payload = {
            "model": model_to_use,
            "messages": [
                {"role": "user", "content": user_prompt}
            ],
            "stream": False,
            "format": "json",
            "options": get_ollama_chat_options_metadata(),
        }
        think_flag = get_ollama_chat_think_for_payload()
        if think_flag is not None:
            payload["think"] = think_flag

        chat_timeout = get_ollama_chat_request_timeout()
        logger.info(
            "Appel Ollama pour métadonnées avec modèle %s timeout=%s options=%s think=%s",
            model_to_use,
            chat_timeout,
            payload.get("options"),
            think_flag,
        )
        response = requests.post(url, json=payload, timeout=chat_timeout)
        response.raise_for_status()
        
        result_data = response.json()
        response_content = ollama_chat_assistant_json_text(
            result_data,
            json_anchors=(
                '"typeDocument"',
                '"doc_type"',
                '"numero"',
                '"number"',
                '"dateDocument"',
            ),
            log_label="Ollama métadonnées",
        )
        result = json.loads(response_content)
        logger.info(f"Métadonnées extraites par Ollama: {result}")
        
        # Normaliser le format
        normalized_result = {
            "doc_type": result.get("doc_type") or result.get("typeDocument"),
            "number": result.get("number") or result.get("numero"),
            "client": result.get("client"),
            "date": result.get("date") or result.get("dateDocument"),
            "supplier": result.get("supplier")
        }
        
        # Validation et correction avec regex si nécessaire
        logger.debug("Métadonnées Ollama avant validation: %s", normalized_result)
        normalized_result = _validate_and_correct_metadata(normalized_result, pdf_text)
        logger.debug("Métadonnées Ollama après validation: %s", normalized_result)
        
        # FORCER le remplacement si le client contient "balance" et "(eur)" - sécurité supplémentaire
        if normalized_result.get("client"):
            client_val = normalized_result["client"]
            client_lower = client_val.lower() if client_val else ""
            if "balance" in client_lower and ("(eur)" in client_lower or "(euro)" in client_lower or "eur)" in client_lower):
                logger.debug("Client balance+eur ignoré: %s", client_val)
                normalized_result["client"] = None
        
        logger.info(f"Métadonnées normalisées et validées: {normalized_result}")
        return normalized_result
        
    except requests.exceptions.RequestException:
        logger.debug("Requête Ollama échouée (métadonnées)", exc_info=True)
        raise
    except Exception:
        logger.debug("Extraction métadonnées Ollama échouée", exc_info=True)
        raise

