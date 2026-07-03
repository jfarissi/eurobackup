import fitz
import io
from typing import Dict, Any

def extract_pdf_raw(path: str) -> Dict[str, Any]:
    doc = fitz.open(path)
    pages = []
    full_text = []

    for i, page in enumerate(doc):
        text = page.get_text()
        blocks = page.get_text("blocks")
        words = page.get_text("words")

        pages.append({
            "page_number": i+1,
            "text": text,
            "blocks": blocks,
            "words": words,
        })
        full_text.append(text)

    return {
        "page_count": len(doc),
        "pages": pages,
        "full_text": "\n".join(full_text)
    }

def is_scanned(pdf_raw: Dict[str, Any]) -> bool:
    texts = [p['text'].strip() for p in pdf_raw['pages']]
    empty = sum(1 for t in texts if len(t) < 50)
    return empty / max(1, len(texts)) > 0.5
