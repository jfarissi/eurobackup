import logging
import re
from typing import List, Dict
from .base_parser import BaseParser
from . import ffgroup

logger = logging.getLogger(__name__)

class FFGroupParser(BaseParser):
    """
    Parser pour les documents FF Group.
    Wrapper autour de l'implémentation existante (app.parsers.ffgroup).
    """
    
    def __init__(self, pdf_path: str):
        super().__init__(pdf_path)
        # Parse dès l'initialisation via le module legacy
        try:
            self.parsed_result = ffgroup.parse(self.pdf_raw)
        except Exception as e:
            logger.error(f"Erreur lors du parsing FFGroup legacy: {e}")
            self.parsed_result = {"items": [], "metadata": {}}

    def extract_products(self) -> List[Dict]:
        """Extrait les produits d'un document FF Group"""
        items = self.parsed_result.get("items", [])
        
        # Normalisation si nécessaire
        # Le format legacy retourne déjà une structure compatible avec:
        # {
        #   "sku": str,           -> product_code
        #   "description": str,
        #   "qty": float,         -> quantity
        #   "unit": str,
        #   "unit_price": float,
        #   "line_total": float   -> total_value
        # }
        
        # Mapping des clés si nécessaire pour correspondre au format attendu par l'API
        normalized_items = []
        for item in items:
            normalized_item = {
                "qty": item.get("qty", 0),
                "unit": item.get("unit", ""),
                "sku": item.get("sku", ""),
                "description": item.get("description", ""),
                "ean": item.get("ean"),
                "unit_price": item.get("unit_price"),
                "line_total": item.get("line_total")
            }
            normalized_items.append(normalized_item)
            
        return normalized_items
    
    def extract_metadata(self) -> Dict:
        """Extrait les métadonnées d'un document FF Group"""
        meta = self.parsed_result.get("metadata", {})
        
        # Normalisation
        if "type" in meta and "doc_type" not in meta:
            meta["doc_type"] = meta["type"]
            
        if not meta.get("supplier"):
            meta["supplier"] = "FF Group"
            
        return meta

