from fastapi import FastAPI, UploadFile, File, Query
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import List, Optional
import shutil
import os
from pathlib import Path
from dotenv import load_dotenv
from .extractor import extract_from_pdf, extract_metadata_from_pdf
from .api.main import parse_pdf, parse_pdf_factory, parse_pdf_ollama, parse_pdf_classifier

print("\n\n" + "="*50)
print("🚀 PYTHON SERVICE IMPORTING APP/MAIN - MODIFIED VERSION 2.1 🚀")
print("If you see this, the code is definitively loading from disk.")
print("="*50 + "\n\n", flush=True)

# Charger les variables d'environnement depuis le fichier .env
# Chercher le fichier .env dans le dossier python-service/
env_path = Path(__file__).parent.parent.parent / '.env'
if env_path.exists():
    load_dotenv(env_path)
    print(f"✅ Fichier .env chargé depuis: {env_path}")
else:
    # Essayer aussi dans le dossier courant
    load_dotenv()
    print("ℹ️  Aucun fichier .env trouvé, utilisation des variables d'environnement système")

app = FastAPI(title="Doc Extractor")

# Configuration CORS pour permettre les appels depuis Angular
# IMPORTANT: Si allow_credentials=True, on ne peut pas utiliser "*" pour allow_origins
# Il faut spécifier explicitement les origines
app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "http://localhost:4200",  # Angular dev server (HTTP)
        "https://localhost:4200",  # Angular dev server (HTTPS)
        "https://localhost:7157",  # Backend C# (HTTPS)
        "http://localhost:7157",   # Backend C# (HTTP)
        "http://127.0.0.1:4200",   # Alternative localhost
        "https://127.0.0.1:4200",  # Alternative localhost HTTPS
        "https://localhost:57096",
        "http://localhost:57096"
    ],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/health")
async def health():
    """Health check endpoint"""
    print("\n\n" + "="*50)
    print("🚀 PYTHON SERVICE STARTED - MODIFIED VERSION 2.1 🚀")
    print("="*50 + "\n\n", flush=True)
    import logging
    logger = logging.getLogger(__name__)
    logger.info("Health check appelé")
    print("🔍 [main.py] Health check appelé")
    return {"status": "healthy", "service": "python-extractor"}

@app.get("/test_debug")
async def test_debug():
    print("🔍 [main.py] TEST DEBUG endpoint called", flush=True)
    try:
        from .parsers.coek import log_debug
        log_debug("TEST DEBUG called via HTTP endpoint")
        return {"status": "ok", "message": "Log attempt made"}
    except Exception as e:
        return {"status": "error", "error": str(e)}

class ProductLine(BaseModel):
    raw: str
    normalized: str
    quantity: int
    product_code: str | None = None
    ean: str | None = None
    unit: str | None = None
    unit_price: float | None = None
    total_value: float | None = None

class DocumentMetadata(BaseModel):
    doc_type: str | None = None       # "invoice" | "delivery_note"
    number: str | None = None
    client: str | None = None
    date: str | None = None           # "YYYY-MM-DD"
    supplier: str | None = None

@app.post("/extract", response_model=List[ProductLine])
async def extract(file: UploadFile = File(...), use_ai: Optional[bool] = Query(True, description="Utiliser l'IA pour l'extraction (nécessite OPENAI_API_KEY). Par défaut: True")):
    temp_path = os.path.join("/tmp", file.filename)
    os.makedirs("/tmp", exist_ok=True)
    with open(temp_path, "wb") as f:
        shutil.copyfileobj(file.file, f)
    try:
        items = extract_from_pdf(temp_path, use_ai=use_ai)
        # Deduplicate by (normalized) keeping max quantity for simplicity
        by_norm = {}
        for it in items:
            k = it["normalized"]
            if k not in by_norm or it["quantity"] > by_norm[k]["quantity"]:
                by_norm[k] = it
        return list(by_norm.values())
    finally:
        try:
            os.remove(temp_path)
        except Exception:
            pass

@app.post("/inspect", response_model=DocumentMetadata)
async def inspect(file: UploadFile = File(...), use_ai: Optional[bool] = Query(True, description="Utiliser l'IA pour l'extraction (nécessite OPENAI_API_KEY). Par défaut: True")):
    temp_path = os.path.join("/tmp", file.filename)
    os.makedirs("/tmp", exist_ok=True)
    with open(temp_path, "wb") as f:
        shutil.copyfileobj(file.file, f)
    try:
        print(f"🔍 [main.py] AVANT appel extract_metadata_from_pdf (use_ai={use_ai})")
        meta = extract_metadata_from_pdf(temp_path, use_ai=use_ai)
        print(f"🔍 [main.py] APRÈS appel extract_metadata_from_pdf - client: '{meta.get('client')}'")
        print(f"🔍 [main.py] AVANT RETOUR JSON - client: '{meta.get('client')}'")
        print(f"🔍 [main.py] MÉTADONNÉES COMPLÈTES: {meta}")
        print(f"🔍 [main.py] Type de meta: {type(meta)}")
        print(f"🔍 [main.py] meta['client'] direct: {meta['client'] if 'client' in meta else 'KEY NOT FOUND'}")
        return meta
    finally:
        try:
            os.remove(temp_path)
        except Exception:
            pass

@app.get("/ollama/models")
async def ollama_models(ollama_host: Optional[str] = Query(None, description="URL Ollama ex. http://localhost:11434")):
    """Liste les modèles installés sur Ollama (via le serveur Python, pas depuis le navigateur)."""
    from .ai_extractor_ollama import check_ollama_available, get_available_models

    host = (ollama_host or "").strip() or None
    try:
        if not check_ollama_available(host):
            return {
                "models": [],
                "error": "Ollama ne répond pas. Vérifiez l’URL (hôte) et que `ollama serve` tourne.",
            }
        models = get_available_models(host)
        models = sorted(set(models), key=lambda x: x.lower())
        return {"models": models, "error": None}
    except Exception as e:
        return {"models": [], "error": str(e)}


# Ajouter l'endpoint /parse de la nouvelle structure auto_invoice_parser
app.post("/parse")(parse_pdf)
app.post("/parse/ollama")(parse_pdf_ollama)
app.post("/parse/factory")(parse_pdf_factory)
app.post("/parse/classifier")(parse_pdf_classifier)

