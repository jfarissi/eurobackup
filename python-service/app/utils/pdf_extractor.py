"""
Extracteur PDF universel basé sur PyMuPDF (fitz).
Remplace pdfplumber pour de meilleures performances et une extraction plus précise.
"""
import fitz  # PyMuPDF
from typing import Dict, Any, List, Optional, Tuple
import logging

logger = logging.getLogger(__name__)


def extract_pdf_raw(path: str, max_pages: Optional[int] = None) -> Dict[str, Any]:
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
    try:
        doc = fitz.open(path)
        pages = []
        full_text = []
        
        # Stocker le nombre de pages AVANT de fermer le document
        page_count = len(doc)
        page_limit = min(max_pages or page_count, page_count)
        
        for i in range(page_limit):
            page = doc[i]
            
            # Extraction du texte brut
            text = page.get_text()
            
            # Extraction des blocs (utile pour la structure)
            blocks = page.get_text("blocks")
            
            # Extraction des mots avec coordonnées (utile pour reconstruire les tableaux)
            words = page.get_text("words")
            
            pages.append({
                "page_number": i + 1,
                "text": text,
                "blocks": blocks,
                "words": words,
            })
            full_text.append(text)
        
        # Fermer le document après avoir extrait toutes les données
        doc.close()
        
        return {
            "page_count": page_count,
            "pages": pages,
            "full_text": "\n".join(full_text)
        }
    except Exception as e:
        logger.error(f"Erreur lors de l'extraction PDF avec PyMuPDF: {e}", exc_info=True)
        raise


def extract_text_from_pdf(path: str, max_pages: Optional[int] = None) -> str:
    """
    Extrait uniquement le texte brut d'un PDF (compatible avec l'ancienne API pdfplumber).
    Utilisé par les extracteurs AI.
    """
    pdf_raw = extract_pdf_raw(path, max_pages)
    print(pdf_raw["full_text"])
    return pdf_raw["full_text"]


def is_scanned(pdf_raw: Dict[str, Any]) -> bool:
    """
    Détecte si un PDF est scanné (image) plutôt que texte.
    Heuristique : si la plupart des pages ont peu ou pas de texte.
    """
    texts = [p['text'].strip() for p in pdf_raw['pages']]
    empty = sum(1 for t in texts if len(t) < 50)
    return empty / max(1, len(texts)) > 0.5


def extract_text_lines(path: str, max_pages: Optional[int] = None) -> List[str]:
    """
    Extrait le texte ligne par ligne (compatible avec l'ancienne approche).
    """
    pdf_raw = extract_pdf_raw(path, max_pages)
    all_lines = []
    for page in pdf_raw['pages']:
        page_lines = [l.strip() for l in page['text'].split('\n') if l.strip()]
        all_lines.extend(page_lines)
    return all_lines


# OCR Support
OCR_ERROR_MSG = None
try:
    import pytesseract
    from pytesseract import Output
    from pdf2image import convert_from_path
    OCR_AVAILABLE = True
except ImportError as e:
    logger.warning(f"OCR dependencies (pytesseract, pdf2image) not found: {e}. OCR disabled.")
    OCR_ERROR_MSG = str(e)
    OCR_AVAILABLE = False
except Exception as e:
    logger.warning(f"Unexpected error importing OCR dependencies: {e}. OCR disabled.")
    OCR_ERROR_MSG = str(e)
    OCR_AVAILABLE = False
else:
    # Configure Tesseract path if found in default location
    import os
    tesseract_path = r"C:\Program Files\Tesseract-OCR\tesseract.exe"
    if os.path.exists(tesseract_path):
        pytesseract.pytesseract.tesseract_cmd = tesseract_path


def extract_text_with_ocr(path: str, max_pages: Optional[int] = None, lang: str = "eng+pol") -> str:
    """
    Extrait le texte d'un PDF via OCR (Tesseract).
    Utilisé pour les documents scannés.
    """
    if not OCR_AVAILABLE:
        logger.warning(f"OCR requested but dependencies are missing. Error: {OCR_ERROR_MSG}")
        return f"[OCR UNAVAILABLE: {OCR_ERROR_MSG}]"

    # Path provided by user
    poppler_bin = r"D:\poppler-25.12.0\Library\bin"

    try:
        pages = convert_from_path(path, poppler_path=poppler_bin)
        full_text = []

        # Limit pages if requested
        if max_pages:
            pages = pages[:max_pages]

        for i, page_img in enumerate(pages):
            logger.info(f"Processing page {i+1} with OCR...")
            # --psm 1: Automatic page segmentation with OSD (Orientation and Script Detection)
            try:
                text = pytesseract.image_to_string(page_img, lang=lang, config='--psm 1')
            except Exception:
                # Fallback to default if OSD fails
                text = pytesseract.image_to_string(page_img, lang=lang)
                
            full_text.append(text)
            
        return "\n".join(full_text)
    
    except Exception as e:
        logger.error(f"OCR Failed for {path}: {e}")
        # Detect Tesseract missing
        if "tesseract is not installed" in str(e).lower() or "not found" in str(e).lower():
            logger.error("PLEASE INSTALL TESSERACT-OCR and add to PATH.")
        return f"[OCR ERROR: {str(e)}]"


def extract_ocr_if_needed(path: str, pdf_raw: Dict[str, Any]) -> str:
    """
    Checks if PDF is scanned and performs OCR if necessary.
    Returns the original text if sufficient, or OCR text if scanned.
    """
    if is_scanned(pdf_raw):
        logger.info(f"Document {path} detected as SCANNED. Attempting OCR.")
        ocr_text = extract_text_with_ocr(path)
        if ocr_text.strip():
            return ocr_text
        else:
            logger.warning("OCR returned empty text.")
    
    return pdf_raw["full_text"]


def extract_ocr_words(path: str, max_pages: Optional[int] = None, lang: str = "eng+pol") -> List[List[Tuple]]:
    """
    Extrait les mots avec coordonnées via OCR.
    Retourne une liste de pages, où chaque page contient une liste de tuples mots.
    Tuple format compatible fitz: (x0, y0, x1, y1, text, block, line, word)
    """
    if not OCR_AVAILABLE:
        return []

    poppler_bin = r"D:\poppler-25.12.0\Library\bin"
    pages_words = []

    try:
        images = convert_from_path(path, poppler_path=poppler_bin)
        if max_pages:
            images = images[:max_pages]

        for i, img in enumerate(images):
            # Try to get data with coordinates
            try:
                # Use PSM 1 (OSD) or 3 (Auto) logic. 
                # Since PSM 1 works for full text via image_to_string, let's use it here too if possible.
                # If image_to_data supports config, we pass it.
                data = pytesseract.image_to_data(img, lang=lang, output_type=Output.DICT, config='--psm 1')
            except:
                data = pytesseract.image_to_data(img, lang=lang, output_type=Output.DICT)

            page_words = []
            n_boxes = len(data['text'])
            for j in range(n_boxes):
                text = data['text'][j]
                if not text or not text.strip():
                    continue
                
                # Coordinates
                x = data['left'][j]
                y = data['top'][j]
                w = data['width'][j]
                h = data['height'][j]
                
                # Create tuple compatible with fitz logic
                # (x0, y0, x1, y1, text, block_no, line_no, word_no)
                word_tuple = (x, y, x + w, y + h, text, 0, 0, 0)
                page_words.append(word_tuple)
            
            pages_words.append(page_words)

        return pages_words
    except Exception as e:
        logger.error(f"OCR Words Extraction Failed: {e}")
        return []
