import logging
import re
from typing import List, Dict
from .base_parser import BaseParser
from . import coek

logger = logging.getLogger(__name__)

class CoekParser(BaseParser):
    """
    Parser pour les documents COEK.
    Wrapper autour de l'implémentation (app.parsers.coek).
    """
    
    def __init__(self, pdf_path: str):
        super().__init__(pdf_path)
        try:
            self.parsed_result = coek.parse(self.pdf_raw)
        except Exception as e:
            logger.error(f"Erreur lors du parsing Coek: {e}")
            import traceback
            import os
            try:
                temp_dir = os.getenv('TEMP', os.getenv('TMP', '/tmp'))
                with open(os.path.join(temp_dir, "coek_parser_error.log"), "a") as f:
                    f.write(f"ERROR: {e}\n")
                    traceback.print_exc(file=f)
            except: pass
            self.parsed_result = {"items": [], "metadata": {}}

    def extract_products(self) -> List[Dict]:
        """Extrait les produits"""
        items = self.parsed_result.get("items", [])
        
        normalized_items = []
        # Regex pour retirer les suffixes de stats (poids/unité/palette) collés à la fin
        stat_suffix_re = re.compile(r"\s+\d+[.,]\d{2}\s+(zak-sac|sac|pcs?|pc|st|pallet|palette)\b.*$", re.IGNORECASE)
        def strip_stats(txt: str) -> str:
            if not txt:
                return txt
            return stat_suffix_re.sub("", txt).strip()
        
        for item in items:
            # Enrichir la description avec dimensions et poids si présents
            raw_desc = item.get("description", "") or ""
            dims = item.get("dimensions")
            weight = item.get("weight")
            
            # Parser le poids (ex: 25,00)
            if weight and isinstance(weight, str):
                try:
                    weight = float(weight.replace(',', '.').replace('kg', '').strip())
                except:
                    pass

            # On ne rajoute plus les infos dans la description car elles sont maintenant dans les colonnes dédiées
            # extras = []
            # if dims:
            #     extras.append(f"Dims: {dims}")
            # if weight:
            #     extras.append(f"Weight: {weight}kg")
            #     
            # if extras:
            #     desc += " | " + " | ".join(extras)

            # Nettoyer les descriptions courtes des stats et privilégier NL/FR explicites
            desc_nl_raw = item.get("description_nl") or ""
            desc_fr_raw = item.get("description_fr") or ""
            clean_desc_nl = strip_stats(desc_nl_raw) or None
            clean_desc_fr = strip_stats(desc_fr_raw) or None
            clean_desc = strip_stats(raw_desc)

            base_desc = clean_desc_fr or clean_desc_nl or clean_desc

            # Parser les dimensions (ex: 29,0x9,0x14,0)
            length, width, height = None, None, None
            if dims:
                parts = dims.replace(',', '.').lower().split('x')
                if len(parts) >= 1:
                    try: length = float(parts[0])
                    except: pass
                if len(parts) >= 2:
                    try: width = float(parts[1])
                    except: pass
                if len(parts) >= 3:
                    try: height = float(parts[2])
                    except: pass

            normalized_item = {
                "qty": item.get("qty", 0),
                "unit": item.get("unit", ""),
                "sku": item.get("sku", ""),
                "sku": item.get("sku", ""),
                "description": base_desc,
                "description_nl": clean_desc_nl or item.get("long_description_nl") or base_desc,
                "description_fr": clean_desc_fr or item.get("long_description_fr") or base_desc,
                # Fournir aussi une description EN : FR prioritaire, sinon EN déjà fournie, sinon rien (pas de NL par défaut)
                "description_en": item.get("description_en") or clean_desc_fr or item.get("long_description_fr"),
                "technical_code": item.get("technical_code"),
                "long_description_nl": item.get("long_description_nl"),
                "long_description_fr": item.get("long_description_fr"),
                "long_description_en": item.get("long_description_fr") or item.get("long_description_nl"),
                "ean": item.get("ean"),
                "unit_price": item.get("unit_price"),
                "line_total": item.get("line_total"),
                "weight": weight,
                "length": length,
                "width": width,
                "height": height,
                "dimensions": dims, # On garde aussi le brut au cas où
                "pallet_quantity": item.get("pallet_quantity"),
                "pallet_type": item.get("pallet_type"), 
                "min_qty": item.get("qty") # qty dans coek.py correspond au Bestelh (MOQ)
            }
            normalized_items.append(normalized_item)
            
        return normalized_items
    
    def extract_metadata(self) -> Dict:
        """Extrait les métadonnées"""
        meta = self.parsed_result.get("metadata", {})
        
        if "type" in meta and "doc_type" not in meta:
            meta["doc_type"] = meta["type"]
            
        if not meta.get("supplier"):
            meta["supplier"] = "COEK"
            
        return meta
