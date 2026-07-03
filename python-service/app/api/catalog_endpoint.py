"""
Endpoint séparé pour le parsing de catalogues produits.
"""
from fastapi import APIRouter, UploadFile, File, HTTPException, Query
from fastapi.responses import JSONResponse
from typing import Optional
import os
import uuid

from ..catalog_parser import is_catalog, parse_catalog
from ..utils.extractor import extract_pdf_raw

router = APIRouter(prefix="/catalog", tags=["catalog"])


@router.post('/parse')
async def parse_catalog_pdf(
    file: UploadFile = File(...),
    use_ai: Optional[bool] = Query(True, description="Utiliser l'IA pour l'extraction"),
    ai_provider: Optional[str] = Query("openai", description="Fournisseur IA: 'openai' ou 'gemini'")
):
    """
    Parse un catalogue produit PDF.
    
    Args:
        file: Fichier PDF du catalogue
        use_ai: Si True, utilise l'IA pour l'extraction
        ai_provider: Fournisseur IA: 'openai' ou 'gemini'
    
    Returns:
        JSON avec la structure des tables SQL:
        {
            "products": [...],   # Table Products
            "variants": [...],   # Table ProductVariants
            "images": [...],     # Table ProductImages
            "attributes": [...] # Table ProductAttributeValues
        }
    """
    if not file.filename.lower().endswith('.pdf'):
        raise HTTPException(status_code=400, detail='Only PDF supported')
    
    # Sauvegarder temporairement
    tmp_dir = os.getenv('TEMP', os.getenv('TMP', '/tmp'))
    os.makedirs(tmp_dir, exist_ok=True)
    tmp = os.path.join(tmp_dir, f"{uuid.uuid4().hex}_{file.filename}")
    
    content = await file.read()
    with open(tmp, 'wb') as f:
        f.write(content)
    
    try:
        # Extraire le texte brut pour vérifier que c'est bien un catalogue
        pdf_raw = extract_pdf_raw(tmp)
        raw_text = pdf_raw.get('full_text', '')
        
        # Vérifier que c'est un catalogue
        if not is_catalog(raw_text):
            raise HTTPException(
                status_code=400, 
                detail='Ce document ne semble pas être un catalogue produit. Utilisez /parse pour les factures.'
            )
        
        # Parser le catalogue
        catalog_result = parse_catalog(tmp, use_ai=use_ai, ai_provider=ai_provider or "openai")
        
        return JSONResponse({
            "type": "catalog",
            "products": catalog_result.get("products", []),
            "variants": catalog_result.get("variants", []),
            "images": catalog_result.get("images", []),
            "attributes": catalog_result.get("attributes", []),
            "count": len(catalog_result.get("products", [])),
            "method": f"catalog_parser_{ai_provider}" if use_ai else "catalog_parser_basic"
        })
    
    except HTTPException:
        raise
    except Exception as e:
        print(f"Erreur lors du parsing du catalogue: {e}")
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=str(e))
    
    finally:
        # Nettoyer
        try:
            os.remove(tmp)
        except Exception:
            pass
