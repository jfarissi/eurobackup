import logging
from typing import Dict, List

from .base_parser import BaseParser
from . import bobruche

logger = logging.getLogger(__name__)


class BobrucheParser(BaseParser):
    """Parser pour les factures Bobrush / Bobruche."""

    def __init__(self, pdf_path: str):
        super().__init__(pdf_path)
        try:
            self.parsed_result = bobruche.parse(self.pdf_raw)
        except Exception as e:
            logger.error("Erreur lors du parsing Bobruche: %s", e)
            self.parsed_result = {"items": [], "metadata": {}}

    def extract_products(self) -> List[Dict]:
        items = self.parsed_result.get("items", [])
        return [
            {
                "qty": item.get("qty", 0),
                "unit": item.get("unit", "ST"),
                "sku": item.get("sku", ""),
                "description": item.get("description", ""),
                "ean": item.get("ean"),
                "unit_price": item.get("unit_price"),
                "line_total": item.get("line_total"),
                "discount": item.get("discount"),
            }
            for item in items
        ]

    def extract_metadata(self) -> Dict:
        meta = dict(self.parsed_result.get("metadata", {}))
        if "type" in meta and "doc_type" not in meta:
            meta["doc_type"] = meta["type"]
        if not meta.get("supplier"):
            meta["supplier"] = "Bobrush"
        return meta
