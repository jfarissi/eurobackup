import logging
import re
from typing import List, Dict, Any
from .base_parser import BaseParser
from . import knauf

logger = logging.getLogger(__name__)

class KnaufParser(BaseParser):
    """
    Parser pour les documents Knauf.
    Wrapper autour de l'implémentation existante (app.parsers.knauf) qui est très robuste.
    """
    
    def __init__(self, pdf_path: str):
        super().__init__(pdf_path)
        # Parse dès l'initialisation via le module legacy pour avoir les résultats
        # Note: knauf.parse prend le dictionnaire raw du PDFExtractor
        try:
            self.parsed_result = knauf.parse(self.pdf_raw)
        except Exception as e:
            logger.error(f"Erreur lors du parsing Knauf legacy: {e}")
            self.parsed_result = {"items": [], "metadata": {}}

    def extract_products(self) -> List[Dict]:
        """Extrait les produits en utilisant le module legacy."""
        items = self.parsed_result.get("items", [])
        
        # Normalisation si nécessaire (convertir les clés si elles diffèrent)
        # Le format legacy retourne déjà une structure compatible avec:
        # {
        #   "quantity": int,
        #   "unit": str,
        #   "product_code": str,
        #   "description": str,
        #   "ean": str,
        #   "unit_price": float,
        #   "total_value": float
        # }
        return items

    def extract_metadata(self) -> Dict:
        """Extrait les métadonnées en utilisant le module legacy."""
        meta = self.parsed_result.get("metadata", {})
        
        # Normalisation: le factory attend certains champs spécifiques
        # doc_type, number, client, supplier, date
        
        # Le module legacy retourne 'type' au lieu de 'doc_type' ?
        if "type" in meta and "doc_type" not in meta:
            meta["doc_type"] = meta["type"]
            
        # S'assurer que le supplier est bien défini
        if not meta.get("supplier"):
            meta["supplier"] = "Knauf"
            
        return meta

