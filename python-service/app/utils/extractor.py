"""
Extracteur PDF universel basé sur PyMuPDF (fitz).
Structure alignée avec auto_invoice_parser.
"""
import fitz
from typing import Dict, Any


def extract_pdf_raw(path: str) -> Dict[str, Any]:
    """
    Extrait le contenu brut d'un PDF avec PyMuPDF.
    
    Retourne:
    {
        "page_count": int,
        "pages": [
            {
                "page_number": int,
                "text": str,
                "blocks": list,
                "words": list
            }
        ],
        "full_text": str
    }
    """
    doc = fitz.open(path)
    pages = []
    full_text = []
    
    # Sauvegarder le nombre de pages AVANT de fermer le document
    page_count = len(doc)

    for i in range(page_count):
        page = doc[i]
        text = page.get_text()
        blocks = page.get_text("blocks")
        words = page.get_text("words")

        pages.append({
            "page_number": i + 1,
            "text": text,
            "blocks": blocks,
            "words": words,
        })
        full_text.append(text)

    doc.close()

    return {
        "page_count": page_count,
        "pages": pages,
        "full_text": "\n".join(full_text)
    }


def is_scanned(pdf_raw: Dict[str, Any]) -> bool:
    """
    Détecte si un PDF est scanné (image) plutôt que texte.
    Heuristique : si la plupart des pages ont peu ou pas de texte.
    """
    texts = [p['text'].strip() for p in pdf_raw['pages']]
    empty = sum(1 for t in texts if len(t) < 50)
    return empty / max(1, len(texts)) > 0.5

