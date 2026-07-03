"""
Parser de base pour tous les parsers de fournisseurs.
Tous les parsers doivent implémenter les méthodes extract_products et extract_metadata.
"""
from abc import ABC, abstractmethod
from typing import List, Dict, Any
from ..utils.pdf_extractor import extract_pdf_raw, extract_ocr_if_needed


class BaseParser(ABC):
    """Classe de base pour tous les parsers de fournisseurs"""
    
    def __init__(self, pdf_path: str):
        self.pdf_path = pdf_path
        self.pdf_raw = extract_pdf_raw(pdf_path)
        # Use OCR if needed
        self.full_text = extract_ocr_if_needed(pdf_path, self.pdf_raw)
        self.text_lower = self.full_text.lower()
    
    @abstractmethod
    def extract_products(self) -> List[Dict]:
        """
        Extrait les produits du document.
        Retourne une liste de dictionnaires avec les champs normalisés.
        """
        pass
    
    @abstractmethod
    def extract_metadata(self) -> Dict:
        """
        Extrait les métadonnées du document.
        Retourne un dictionnaire avec: doc_type, number, client, supplier, date
        """
        pass
    
    def detect_supplier(self) -> str:
        """Détecte le fournisseur à partir du texte"""
        # Debug: check text content for detection
        import logging
        logging.getLogger(__name__).warning(f"[DETECT SUPPLIER] Text preview: {self.text_lower[:1000]}")

        # Check for Kolor System (Name, NIP, or WAPRO Mag) - PRIORITIZED
        if "kolor system" in self.text_lower or "kolorsystem" in self.text_lower:
            return "Kolor System"
        if "9661867953" in self.text_lower or "966-186-79-53" in self.text_lower:
            return "Kolor System"
        if "wapro mag" in self.text_lower:
            return "Kolor System"
            
        # Custom checks - Prioritized
        if "coeck" in self.text_lower or "coek" in self.text_lower:
            return "Coek"
            
        if "ff group" in self.text_lower or "ffgroup" in self.text_lower or "ff-group" in self.text_lower:
            return "FF Group"
        if "knauf" in self.text_lower:
            return "Knauf"
        elif "pgb-europe" in self.text_lower or "pgb europe" in self.text_lower:
            return "PGB"
        elif "gontrode heirweg" in self.text_lower:
            return "PGB"
        elif "pardaen nv" in self.text_lower or "pardaen" in self.text_lower:
            return "Pardaen"
        elif "haachtsesteenweg 672" in self.text_lower:
            return "Pardaen"
        elif "schrauwen" in self.text_lower:
            return "STG"
        elif "stg" in self.text_lower and ("tool" in self.text_lower or "group" in self.text_lower):
            return "STG"
            
        return "Unknown"
    
    def detect_doc_type(self) -> str:
        """Détecte le type de document"""
        if "factuur" in self.text_lower or "invoice" in self.text_lower:
            return "invoice"
        elif "leveringsbon" in self.text_lower or "delivery note" in self.text_lower:
            return "delivery"
        return "invoice"  # Par défaut

