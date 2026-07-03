import logging
from typing import List, Dict
from .base_parser import BaseParser
from . import stg

logger = logging.getLogger(__name__)

class STGParser(BaseParser):
    """
    Parser pour les documents STG (Schrauwen).
    Wrapper autour de l'implémentation existante (app.parsers.stg).
    """
    
    def __init__(self, pdf_path: str):
        super().__init__(pdf_path)
        # Parse dès l'initialisation via le module legacy
        try:
            self.parsed_result = stg.parse(self.pdf_raw)
        except Exception as e:
            logger.error(f"Erreur lors du parsing STG legacy: {e}")
            self.parsed_result = {"items": [], "metadata": {}}

    def extract_products(self) -> List[Dict]:
        """Extrait les produits d'un document STG"""
        items = self.parsed_result.get("items", [])
        
        # Mapping des clés pour correspondre au format attendu par l'API
        # Format legacy STG:
        # {
        #   "sku": str,
        #   "supplier_sku": str,
        #   "ean": str,
        #   "description": str,
        #   "qty": float,
        #   "unit": str,
        #   "unit_price": float,
        #   "line_total": float
        # }
        
        normalized_items = []
        for item in items:
            normalized_item = {
                "qty": item.get("qty", 0),
                "unit": item.get("unit", ""),
                "sku": item.get("sku", "") or item.get("supplier_sku", ""),
                "description": item.get("description", ""),
                "ean": item.get("ean"),
                "unit_price": item.get("unit_price"),
                "line_total": item.get("line_total")
            }
            normalized_items.append(normalized_item)
            
        return normalized_items
    
    def extract_metadata(self) -> Dict:
        """Extrait les métadonnées d'un document STG"""
        meta = self.parsed_result.get("metadata", {})
        
        # Normalisation
        if "type" in meta and "doc_type" not in meta:
            meta["doc_type"] = meta["type"]
            
        if not meta.get("supplier"):
            meta["supplier"] = "STG"
            
        return meta
