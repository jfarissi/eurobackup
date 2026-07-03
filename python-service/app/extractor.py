"""
Extracteur principal - Wrapper pour compatibilité avec l'API existante.
Utilise la nouvelle structure modulaire auto_invoice_parser en arrière-plan.
"""
import logging
from typing import List, Dict
from .utils.extractor import extract_pdf_raw
from importlib import import_module

logger = logging.getLogger(__name__)

# Mapping des fournisseurs vers leurs parsers
# Utiliser le chemin complet depuis app
PARSERS = {
    'knauf': 'app.parsers.knauf',
    'ffgroup': 'app.parsers.ffgroup',
    'stg': 'app.parsers.stg',
    'generic': 'app.parsers.generic'
}


def detect_fournisseur(text: str) -> str:
    """Détecte le fournisseur à partir du texte"""
    text_lower = text.lower()
    if 'ff group' in text_lower or 'ffgroup' in text_lower or 'ff-group' in text_lower:
        return 'ffgroup'
    if 'knauf' in text_lower or 'rue du parc industriel' in text_lower:
        return 'knauf'
    elif 'pgb-europe' in text_lower or 'pgb europe' in text_lower or 'gontrode heirweg' in text_lower:
        return 'pgb'
    elif 'schrauwen' in text_lower or 'stg-group.be' in text_lower or 'stg' in text_lower:
        return 'stg'
    return 'generic'


def extract_from_pdf(path: str, use_ai: bool = True) -> List[Dict]:
    """
    Extrait les produits d'un PDF (compatibilité avec l'API existante).
    
    Args:
        path: Chemin vers le fichier PDF
        use_ai: Ignoré pour l'instant (garde la compatibilité API)
    
    Returns:
        Liste de produits au format:
        [
            {
                "raw": str,
                "normalized": str,
                "quantity": int,
                "product_code": str,
                "ean": str,
                "unit": str,
                "unit_price": float,
                "total_value": float
            }
        ]
    """
    try:
        # Extraire le contenu brut du PDF
        pdf_raw = extract_pdf_raw(path)
        text = pdf_raw['full_text']
        
        # Détecter le fournisseur
        fournisseur = detect_fournisseur(text)
        
        # Charger et utiliser le parser approprié
        module_path = PARSERS.get(fournisseur, 'app.parsers.generic')
        mod = import_module(module_path)
        result = mod.parse(pdf_raw)
        
        # Convertir au format attendu par l'API existante
        products = []
        for item in result.get('items', []):
            # Construire normalized à partir de description ou sku
            normalized = item.get('description') or item.get('sku') or ''
            
            products.append({
                "raw": item.get('description', ''),
                "normalized": normalized,
                "quantity": int(item.get('qty', 0)) if item.get('qty') else 0,
                "product_code": item.get('sku'),
                "ean": item.get('ean'),
                "unit": item.get('unit'),
                "unit_price": item.get('unit_price'),
                "total_value": item.get('line_total')
            })
        
        return products
    
    except Exception as e:
        logger.error(f"Erreur lors de l'extraction: {e}", exc_info=True)
        return []


def extract_metadata_from_pdf(path: str, use_ai: bool = True) -> Dict:
    """
    Extrait les métadonnées d'un PDF (compatibilité avec l'API existante).
    
    Args:
        path: Chemin vers le fichier PDF
        use_ai: Ignoré pour l'instant (garde la compatibilité API)
    
    Returns:
        Dictionnaire avec:
        {
            "doc_type": str,      # "invoice" | "delivery"
            "number": str,
            "client": str,
            "date": str,
            "supplier": str
        }
    """
    try:
        # Extraire le contenu brut du PDF
        pdf_raw = extract_pdf_raw(path)
        text = pdf_raw['full_text']
        
        # Détecter le fournisseur
        fournisseur = detect_fournisseur(text)
        
        # Charger et utiliser le parser approprié
        module_path = PARSERS.get(fournisseur, 'app.parsers.generic')
        mod = import_module(module_path)
        result = mod.parse(pdf_raw)
        
        # Retourner les métadonnées au format attendu
        metadata = result.get('metadata', {})
        
        # Normaliser doc_type pour compatibilité
        # Les parsers peuvent retourner 'type' ou 'doc_type'
        doc_type = metadata.get('type') or metadata.get('doc_type') or 'invoice'
        doc_type_lower = doc_type.lower() if doc_type else ''
        
        # Reconnaître tous les types de bons de livraison
        if doc_type_lower in ['delivery', 'delivery_note', 'bl', 'bon de livraison', 
                              'verzendnota', 'leveringsbon', 'leveringsbevestiging']:
            doc_type = 'delivery_note'
        # Reconnaître tous les types de factures
        elif doc_type_lower in ['invoice', 'facture', 'factuur']:
            doc_type = 'invoice'
        # Par défaut, si le type n'est pas reconnu, essayer de deviner depuis les items
        else:
            # Si pas de prix dans les items, probablement un BL
            items = result.get('items', [])
            has_prices = any(item.get('unit_price') for item in items if item.get('unit_price'))
            if not has_prices:
                doc_type = 'delivery_note'
            else:
                doc_type = 'invoice'
        
        return {
            "doc_type": doc_type,
            "number": metadata.get('number'),
            "client": metadata.get('client'),
            "date": metadata.get('date'),
            "supplier": metadata.get('supplier')
        }
    
    except Exception as e:
        logger.error(f"Erreur lors de l'extraction des métadonnées: {e}", exc_info=True)
        return {
            "doc_type": None,
            "number": None,
            "client": None,
            "date": None,
            "supplier": None
        }
