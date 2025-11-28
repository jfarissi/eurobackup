from fastapi import FastAPI, UploadFile, File
from pydantic import BaseModel
from typing import List
import shutil
import os
from .extractor import extract_from_pdf, extract_metadata_from_pdf

app = FastAPI(title="Doc Extractor")

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
async def extract(file: UploadFile = File(...)):
    temp_path = os.path.join("/tmp", file.filename)
    os.makedirs("/tmp", exist_ok=True)
    with open(temp_path, "wb") as f:
        shutil.copyfileobj(file.file, f)
    try:
        items = extract_from_pdf(temp_path)
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
async def inspect(file: UploadFile = File(...)):
    temp_path = os.path.join("/tmp", file.filename)
    os.makedirs("/tmp", exist_ok=True)
    with open(temp_path, "wb") as f:
        shutil.copyfileobj(file.file, f)
    try:
        print(f"🔍 [main.py] AVANT appel extract_metadata_from_pdf")
        meta = extract_metadata_from_pdf(temp_path)
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


