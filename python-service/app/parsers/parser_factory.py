import os
from typing import Optional
from .base_parser import BaseParser
from .knauf_parser import KnaufParser
from .ffgroup_parser import FFGroupParser
from .generic_parser import GenericParser
from .stg_parser import STGParser
from .kolor_system import KolorSystemParser
from .coek_parser import CoekParser
from .pgb_parser import PGBParser
from .pardaen_parser import PardaenParser
from .bobruche_parser import BobrucheParser
from .sadu_parser import SaduParser
from .xenex_parser import XenexParser
from .rectavit_parser import RectavitParser
from .document_classifier import classify_supplier, classify_doc_type

def log_debug(msg):
    try:
        with open("d:\\GitHub\\Backup.Web.Api\\python-service\\debug_trace.log", "a", encoding="utf-8") as f:
            f.write(msg + "\n")
    except:
        pass


def create_parser(
    pdf_path: str,
    supplier: Optional[str] = None,
    detection_mode: str = "classifier",
) -> BaseParser:
    """
    Crée le parser approprié selon le fournisseur.
    
    Args:
        pdf_path: Chemin vers le PDF
        supplier: Fournisseur détecté (optionnel, sera détecté automatiquement si None)
    
    Returns:
        Instance du parser approprié
    """
    # Si le fournisseur n'est pas spécifié, créer un parser temporaire pour le détecter
    if supplier is None:
        temp_parser = GenericParser(pdf_path)
        mode = (detection_mode or "classifier").strip().lower()
        if mode == "legacy":
            supplier = temp_parser.detect_supplier()
            log_debug(f"Factory LEGACY detected supplier: {supplier}")
        else:
            # V2: classifier sans regex strictes pour guider le choix du parser.
            classified_supplier, supplier_conf, supplier_scores = classify_supplier(temp_parser.text_lower)
            doc_type, doc_conf, doc_scores = classify_doc_type(temp_parser.text_lower)
            if mode == "classifier_strict":
                supplier = classified_supplier
                log_debug(
                    f"Factory CLASSIFIER_STRICT supplier={classified_supplier} conf={supplier_conf} scores={supplier_scores}; "
                    f"doc_type={doc_type} conf={doc_conf} scores={doc_scores}; selected_supplier={supplier}"
                )
            else:
                fallback_supplier = temp_parser.detect_supplier()
                supplier = classified_supplier if classified_supplier != "Unknown" else fallback_supplier
                log_debug(
                    f"Factory CLASSIFIER supplier={classified_supplier} conf={supplier_conf} scores={supplier_scores}; "
                    f"doc_type={doc_type} conf={doc_conf} scores={doc_scores}; fallback_supplier={fallback_supplier}; "
                    f"selected_supplier={supplier}"
                )
    
    # Créer le parser approprié
    supplier_lower = supplier.lower() if supplier else "unknown"
    
    if "ff group" in supplier_lower or "ffgroup" in supplier_lower or "ff-group" in supplier_lower:
        return FFGroupParser(pdf_path)
    if "knauf" in supplier_lower:
        return KnaufParser(pdf_path)
    elif "stg" in supplier_lower or "schrauwen" in supplier_lower:
        return STGParser(pdf_path)
    elif "kolor system" in supplier_lower or "kolorsystem" in supplier_lower:
        return KolorSystemParser(pdf_path)
    elif "coek" in supplier_lower:
        return CoekParser(pdf_path)
    elif "pgb" in supplier_lower:
        return PGBParser(pdf_path)
    elif "pardaen" in supplier_lower:
        return PardaenParser(pdf_path)
    elif "bobrush" in supplier_lower or "bobruche" in supplier_lower:
        return BobrucheParser(pdf_path)
    elif "sadu" in supplier_lower:
        return SaduParser(pdf_path)
    elif "xenex" in supplier_lower:
        return XenexParser(pdf_path)
    elif "rectavit" in supplier_lower:
        return RectavitParser(pdf_path)
    else:
        return GenericParser(pdf_path)

