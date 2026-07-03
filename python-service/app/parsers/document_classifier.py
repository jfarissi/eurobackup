"""
Classification documentaire lightweight (sans regex strictes).
But: décider fournisseur + type de document avant sélection du parser.
"""
from typing import Dict, List, Tuple


def _contains_any(text: str, terms: List[str]) -> int:
    score = 0
    for t in terms:
        if t and t in text:
            score += 1
    return score


def classify_supplier(text: str) -> Tuple[str, float, Dict[str, int]]:
    t = (text or "").lower()
    # Pondération simple par présence d'indices métier stables.
    signals = {
        "FF Group": [
            "ff group",
            "ffgroup",
            "ff-group",
            "ff group tool industries",
            "aspropyrgos",
        ],
        "Knauf": [
            "knauf",
            "knauf insulation",
            "rue du parc industriel",
            "faktuur nr",
        ],
        "STG": [
            "schrauwen",
            "stg-group",
            "stg ",
            "stg tool",
        ],
        "Kolor System": [
            "kolor system",
            "kolorsystem",
            "wapro mag",
            "9661867953",
        ],
        "Coek": [
            "coeck",
            "coek",
        ],
        "PGB": [
            "pgb-europe",
            "pgb europe",
            "gontrode heirweg",
            "pb-fasteners",
        ],
        "Pardaen": [
            "pardaen nv",
            "pardaen",
            "haachtsesteenweg 672",
            "kampenhout",
        ],
    }

    raw_scores: Dict[str, int] = {k: _contains_any(t, v) for k, v in signals.items()}
    best = max(raw_scores, key=raw_scores.get) if raw_scores else "Unknown"
    best_score = raw_scores.get(best, 0)
    if best_score <= 0:
        return "Unknown", 0.0, raw_scores
    total = sum(raw_scores.values()) or 1
    confidence = round(best_score / total, 3)
    return best, confidence, raw_scores


def classify_doc_type(text: str) -> Tuple[str, float, Dict[str, int]]:
    t = (text or "").lower()
    invoice_terms = [
        "invoice",
        "facture",
        "factuur",
        "faktuur",
        "invoice number",
        "faktuur nr",
    ]
    delivery_terms = [
        "delivery note",
        "bon de livraison",
        "leveringsbon",
        "leveringsbevestiging",
        "verzendbon",
        "pakbon",
    ]
    s_invoice = _contains_any(t, invoice_terms)
    s_delivery = _contains_any(t, delivery_terms)
    raw = {"invoice": s_invoice, "delivery": s_delivery}
    if s_delivery > s_invoice:
        denom = max(1, s_delivery + s_invoice)
        return "delivery", round(s_delivery / denom, 3), raw
    denom = max(1, s_delivery + s_invoice)
    return "invoice", round(s_invoice / denom, 3), raw

