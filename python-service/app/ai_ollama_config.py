"""
Résolution de la configuration Ollama (hôte, modèle, profils) pour l'extraction documentaire.
Variables d'environnement (synchronisées depuis appsettings via sync-openai-key.ps1) :
- OLLAMA_HOST : URL de base (défaut http://localhost:11434)
- OLLAMA_MODEL : modèle par défaut si aucun profil ne s'applique
- OLLAMA_ACTIVE_PROFILE : nom du profil actif (ex: Primary, Quality)
- OLLAMA_PROFILE_<NOM> : modèle pour le profil NOM en majuscules (ex: OLLAMA_PROFILE_PRIMARY=gemma4-rapide:latest)
- OLLAMA_FALLBACK_PROFILES : liste séparée par virgules (usage futur / logs)
- OLLAMA_CHAT_READ_TIMEOUT : secondes max d'attente de la réponse /api/chat (défaut 120 ; fallback déterministe rapide)
- OLLAMA_CHAT_CONNECT_TIMEOUT : timeout de connexion TCP (défaut 15)
- OLLAMA_GENERATE_TIMEOUT : alias de OLLAMA_CHAT_READ_TIMEOUT (compat.)
- OLLAMA_NUM_PREDICT_PRODUCTS : tokens de sortie max pour /api/chat produits (défaut 32768 ; évite done_reason=length à 512)
- OLLAMA_NUM_PREDICT_METADATA : idem métadonnées (défaut 4096)
- OLLAMA_NUM_PREDICT : surcharge des deux si OLLAMA_NUM_PREDICT_PRODUCTS absent
- OLLAMA_CHAT_THINK_EXTRACTION : true | false | omit — false par défaut (think désactivé = budget JSON)
"""
import os
import re
import logging
from typing import Optional, Tuple, Any, Dict

logger = logging.getLogger(__name__)

_DEFAULT_HOST = "http://localhost:11434"


def get_ollama_host() -> str:
    h = (os.getenv("OLLAMA_HOST") or "").strip()
    return h if h else _DEFAULT_HOST


def get_ollama_chat_request_timeout() -> tuple[float, float]:
    """
    Timeouts ``(connect_sec, read_sec)`` pour ``requests.post(..., /api/chat)``.
    Sur machine locale, l'inférence peut dépasser plusieurs minutes (gros BL, modèle lourd).
    """
    connect = 15.0
    raw_c = (os.getenv("OLLAMA_CHAT_CONNECT_TIMEOUT") or "").strip()
    if raw_c:
        try:
            connect = float(max(5.0, min(float(raw_c), 120.0)))
        except ValueError:
            pass

    read = 120.0
    raw_r = (
        os.getenv("OLLAMA_CHAT_READ_TIMEOUT")
        or os.getenv("OLLAMA_GENERATE_TIMEOUT")
        or ""
    ).strip()
    if raw_r:
        try:
            read = float(max(60.0, min(float(raw_r), 7200.0)))
        except ValueError:
            pass

    return (connect, read)


def _clamp_int(value: str, fallback: int, *, min_v: int, max_v: int) -> int:
    try:
        v = int(float(value.strip()))
        return max(min_v, min(v, max_v))
    except (ValueError, TypeError, AttributeError):
        return fallback


def get_ollama_num_predict_products() -> int:
    """Tokens de sortie pour une liste de produits (JSON volumineux)."""
    raw = (os.getenv("OLLAMA_NUM_PREDICT_PRODUCTS") or os.getenv("OLLAMA_NUM_PREDICT") or "").strip()
    return _clamp_int(raw, 32768, min_v=1024, max_v=131072) if raw else 32768


def get_ollama_num_predict_metadata() -> int:
    raw = (os.getenv("OLLAMA_NUM_PREDICT_METADATA") or "").strip()
    return _clamp_int(raw, 4096, min_v=256, max_v=65536) if raw else 4096


def get_ollama_chat_options_products() -> Dict[str, Any]:
    """Options Modelfile passées dans le corps /api/chat pour l'extraction produits."""
    return {
        "temperature": 0.1,
        "num_predict": get_ollama_num_predict_products(),
    }


def get_ollama_chat_options_metadata() -> Dict[str, Any]:
    return {
        "temperature": 0.1,
        "num_predict": get_ollama_num_predict_metadata(),
    }


def get_ollama_chat_think_for_payload() -> Any:
    """
    Valeur du champ ``think`` pour /api/chat (extraction documentaire).
    - false (défaut) : pas de canal thinking → le budget ``num_predict`` sert au JSON
    - true : laisser le modèle raisonner (réduit l'espace pour le JSON)
    - omit / vide avec env absent : ne pas envoyer la clé ``think``
    """
    raw = (os.getenv("OLLAMA_CHAT_THINK_EXTRACTION") or "false").strip().lower()
    if raw in ("omit", "default", "model", "none"):
        return None
    if raw in ("1", "true", "yes", "on"):
        return True
    return False


def _profile_env_key(profile_name: str) -> str:
    safe = re.sub(r"[^a-zA-Z0-9]+", "_", profile_name.strip()).upper().strip("_")
    return f"OLLAMA_PROFILE_{safe}"


def resolve_ollama_model(
    explicit_model: Optional[str] = None,
    explicit_profile: Optional[str] = None,
) -> Tuple[str, str]:
    """
    Retourne (model_id, source_label) pour les logs.
    explicit_model > profil explicite > OLLAMA_ACTIVE_PROFILE > OLLAMA_MODEL
    """
    if explicit_model and explicit_model.strip():
        m = explicit_model.strip()
        logger.info("Ollama: modèle explicite (requête): %s", m)
        return m, "explicit_model"

    profile = (explicit_profile or os.getenv("OLLAMA_ACTIVE_PROFILE") or "").strip()
    if profile:
        env_key = _profile_env_key(profile)
        mapped = (os.getenv(env_key) or "").strip()
        if mapped:
            logger.info("Ollama: profil '%s' (%s) -> %s", profile, env_key, mapped)
            return mapped, f"profile:{profile}"
        # Profil sans entrée dédiée : même nom que tag modèle ollama
        logger.info("Ollama: profil '%s' sans %s, utilisation du nom comme modèle", profile, env_key)
        return profile, f"profile_as_model:{profile}"

    legacy = (os.getenv("OLLAMA_MODEL") or "qwen2.5:7b-instruct").strip()
    logger.info("Ollama: modèle défaut OLLAMA_MODEL=%s", legacy)
    return legacy, "OLLAMA_MODEL"


def describe_config_for_startup() -> str:
    host = get_ollama_host()
    model, src = resolve_ollama_model()
    prof = os.getenv("OLLAMA_ACTIVE_PROFILE") or "(non défini)"
    tc, tr = get_ollama_chat_request_timeout()
    np_p = get_ollama_num_predict_products()
    np_m = get_ollama_num_predict_metadata()
    th = get_ollama_chat_think_for_payload()
    th_s = "omit" if th is None else str(th).lower()
    return (
        f"Ollama host={host}, modèle résolu={model} (source={src}), "
        f"profil actif env={prof}, timeout /api/chat connect={tc}s read={tr}s, "
        f"num_predict products={np_p} metadata={np_m}, think={th_s}"
    )
