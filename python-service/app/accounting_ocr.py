"""OCR comptable : factures marocaines (ICE/IF/RC) et relevés bancaires (texte/PDF)."""
from __future__ import annotations

import os
import re
import tempfile
from datetime import datetime
from typing import Any, Optional

from fastapi import APIRouter, File, Form, UploadFile
from pydantic import BaseModel

from .parsers.document_classifier import classify_doc_type

router = APIRouter(prefix="/ocr", tags=["accounting-ocr"])

ICE_RE = re.compile(r"(?:ICE|I\.C\.E\.?)[\s:.-]*([0-9]{15})", re.I)
IF_RE = re.compile(r"(?:IF|I\.F\.|Identifiant\s+fiscal)[\s:.-]*([0-9]{7,10})", re.I)
RC_RE = re.compile(r"(?:R\.?C\.?|Registre\s+(?:de\s+)?commerce)[\s:.-]*([A-Z0-9\-/]{4,})", re.I)
NUM_RE = re.compile(r"(?:Facture|FACTURE|N[°o]|Invoice)[\s:.-]*([A-Z]{0,4}[-/]?\d{2,}[-/]?\d*)", re.I)
DATE_RE = re.compile(r"\b(\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4})\b")
HT_RE = re.compile(r"(?:Total\s*HT|Montant\s*HT|H\.?T\.?)[\s:]*(-?\d{1,3}(?:[ \u00A0]?\d{3})*[.,]\d{2}|\d+[.,]\d{2})", re.I)
TTC_RE = re.compile(r"(?:Total\s*TTC|Montant\s*TTC|T\.?T\.?C\.?)[\s:]*(-?\d{1,3}(?:[ \u00A0]?\d{3})*[.,]\d{2}|\d+[.,]\d{2})", re.I)
TVA_RE = re.compile(r"(?:TVA|VAT)[\s:]*(-?\d{1,3}(?:[ \u00A0]?\d{3})*[.,]\d{2}|\d+[.,]\d{2})", re.I)
RATE_RE = re.compile(r"TVA[^\d]{0,12}(20|14|10|7)\s*%", re.I)
PARTY_RE = re.compile(r"(?:Client|Fournisseur|Ste|Société)[\s:]+([A-Za-zÀ-ÿ0-9 .,'-]{3,80})", re.I)
AMOUNT_RE = re.compile(r"-?\d{1,3}(?:[ \u00A0]?\d{3})*[.,]\d{2}")


def _amount(raw: Optional[str]) -> Optional[float]:
    if not raw:
        return None
    clean = raw.replace("\u00A0", "").replace(" ", "")
    if "," in clean and "." in clean:
        if clean.rfind(",") > clean.rfind("."):
            clean = clean.replace(".", "").replace(",", ".")
        else:
            clean = clean.replace(",", "")
    elif "," in clean:
        clean = clean.replace(",", ".")
    try:
        return float(clean)
    except ValueError:
        return None


def _date(raw: str) -> Optional[str]:
    for fmt in ("%d/%m/%Y", "%d-%m-%Y", "%d/%m/%y"):
        try:
            return datetime.strptime(raw, fmt).date().isoformat()
        except ValueError:
            continue
    return None


def parse_moroccan_invoice(text: str) -> dict[str, Any]:
    ice = ICE_RE.search(text)
    tax = IF_RE.search(text)
    rc = RC_RE.search(text)
    number = NUM_RE.search(text)
    date_m = DATE_RE.search(text)
    ht = _amount(HT_RE.search(text).group(1) if HT_RE.search(text) else None)
    ttc = _amount(TTC_RE.search(text).group(1) if TTC_RE.search(text) else None)
    vat = _amount(TVA_RE.search(text).group(1) if TVA_RE.search(text) else None)
    rate_m = RATE_RE.search(text)
    party = PARTY_RE.search(text)
    if ht is None and ttc is not None and vat is not None:
        ht = round(ttc - vat, 2)
    if ttc is None and ht is not None and vat is not None:
        ttc = round(ht + vat, 2)
    hits = sum(1 for x in (ice, tax, rc, number, date_m) if x) + (1 if (ttc or ht) else 0)
    return {
        "type_document": "facture",
        "ice": ice.group(1) if ice else None,
        "tax_id": tax.group(1) if tax else None,
        "trade_register": rc.group(1) if rc else None,
        "numero_facture": number.group(1) if number else None,
        "date": _date(date_m.group(1)) if date_m else None,
        "tiers_nom": party.group(1).strip() if party else None,
        "montant_ht": ht,
        "tva": vat,
        "montant_ttc": ttc,
        "taux_tva": float(rate_m.group(1)) if rate_m else None,
        "lignes": None,
        "confiance": min(1.0, hits / 6.0),
    }


def parse_bank_statement_text(text: str) -> list[dict[str, Any]]:
    lines: list[dict[str, Any]] = []
    for raw in text.replace("\r\n", "\n").split("\n"):
        trimmed = raw.strip()
        date_m = DATE_RE.search(trimmed)
        if not date_m:
            continue
        iso = _date(date_m.group(1))
        if not iso:
            continue
        amounts = AMOUNT_RE.findall(trimmed)
        if not amounts:
            continue
        last = _amount(amounts[-1]) or 0
        if last == 0:
            continue
        credit = bool(re.search(r"\b(C|CR|CREDIT|VIR)\b", trimmed, re.I))
        debit = bool(re.search(r"\b(D|DB|DEBIT|CHQ|PREL)\b", trimmed, re.I))
        d = abs(last) if (debit and not credit) or last < 0 else 0
        c = abs(last) if (credit and not debit) or (last > 0 and not debit) else 0
        label = AMOUNT_RE.sub("", trimmed.replace(date_m.group(0), "")).strip()
        lines.append({
            "date": iso,
            "libelle": label or trimmed,
            "debit": d,
            "credit": c,
        })
    return lines


def _extract_text(path: str, filename: str) -> str:
    lower = (filename or "").lower()
    ext = os.path.splitext(lower)[1]
    if ext in {".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".bmp", ".gif"}:
        return _ocr_image(path)
    if lower.endswith(".pdf"):
        text = ""
        try:
            from .utils.pdf_extractor import extract_pdf_raw, extract_text_with_ocr, is_scanned
            raw = extract_pdf_raw(path)
            text = raw.get("full_text") or ""
            if is_scanned(raw) or len(text.strip()) < 40:
                ocr = extract_text_with_ocr(path, lang="fra+ara")
                if ocr and not ocr.startswith("[OCR"):
                    return ocr
        except Exception:
            pass
        return text
    with open(path, "r", encoding="utf-8", errors="ignore") as handle:
        return handle.read()


def _ocr_image(path: str) -> str:
    """Tesseract fra+ara, puis EasyOCR si disponible."""
    try:
        from PIL import Image
        image = Image.open(path)
        if image.mode not in ("RGB", "L"):
            image = image.convert("RGB")
    except Exception:
        return ""

    try:
        import pytesseract
        tesseract_path = r"C:\Program Files\Tesseract-OCR\tesseract.exe"
        if os.path.exists(tesseract_path):
            pytesseract.pytesseract.tesseract_cmd = tesseract_path
        for lang in ("fra+ara", "fra", "eng"):
            try:
                text = pytesseract.image_to_string(image, lang=lang)
                if text and text.strip():
                    return text
            except Exception:
                continue
    except Exception:
        pass

    try:
        import easyocr
        reader = easyocr.Reader(["fr", "ar", "en"], gpu=False)
        rows = reader.readtext(path, detail=0)
        return "\n".join(str(r) for r in rows)
    except Exception:
        return ""


async def _save_upload(file: UploadFile) -> str:
    suffix = os.path.splitext(file.filename or "")[1] or ".txt"
    fd, path = tempfile.mkstemp(suffix=suffix)
    os.close(fd)
    data = await file.read()
    with open(path, "wb") as handle:
        handle.write(data)
    return path


_TYPE_MAP = {
    "invoice": "facture",
    "delivery": "bon_livraison",
    "bank_statement": "releve_bancaire",
}


class TextExtractBody(BaseModel):
    text: str
    file_name: str | None = None
    hint: str | None = None


def _filename_forces_bank(filename: str) -> bool:
    name = (filename or "").lower()
    return name.endswith((".ofx", ".qfx", ".csv"))


def classify_accounting_document(text: str, filename: str = "", hint: str | None = None) -> tuple[str, float, dict]:
    hint_n = (hint or "").strip().lower()
    if hint_n in {"bank", "releve", "relevé", "releve_bancaire"}:
        return "releve_bancaire", 1.0, {"hint": 1}
    if hint_n in {"invoice", "facture"}:
        return "facture", 1.0, {"hint": 1}
    if hint_n in {"delivery", "bl", "bon_livraison"}:
        return "bon_livraison", 1.0, {"hint": 1}
    if _filename_forces_bank(filename):
        return "releve_bancaire", 0.95, {"filename": 1}
    doc_type, conf, raw = classify_doc_type(text)
    return _TYPE_MAP.get(doc_type, "facture"), conf, raw


def _try_purchase_lines(path: str | None, filename: str) -> list[dict[str, Any]]:
    if not path or not (filename or "").lower().endswith(".pdf"):
        return []
    try:
        from .utils.pdf_extractor import extract_pdf_raw
        preview = (extract_pdf_raw(path).get("full_text") or "")[:8000]
        doc_type, conf, _ = classify_doc_type(preview)
        if doc_type == "bank_statement" and conf >= 0.4:
            return []
    except Exception:
        pass
    try:
        from .parsers.parser_factory import create_parser
        parser = create_parser(path)
        products = parser.extract_products() or []
        lines: list[dict[str, Any]] = []
        for item in products:
            desc = item.get("description") or item.get("sku") or ""
            qty = item.get("qty") or item.get("quantity") or 0
            price = item.get("unit_price") or 0
            if not desc and not qty and not price:
                continue
            lines.append({
                "product": desc,
                "quantity": qty,
                "unitPrice": price,
            })
        return lines[:50]
    except Exception:
        return []


def classify_and_parse(
    text: str,
    path: str | None = None,
    filename: str = "",
    hint: str | None = None,
) -> dict[str, Any]:
    doc_type, type_conf, scores = classify_accounting_document(text, filename, hint)
    payload: dict[str, Any] = {
        "type_document": doc_type,
        "type_confidence": type_conf,
        "type_scores": scores,
        "source": "python",
        "lignes_articles": [],
        "lignes_releve": [],
    }
    if doc_type == "releve_bancaire":
        payload["lignes_releve"] = parse_bank_statement_text(text)
        payload["confiance"] = min(1.0, type_conf)
        return payload

    invoice = parse_moroccan_invoice(text)
    invoice["type_document"] = doc_type
    articles = _try_purchase_lines(path, filename)
    invoice["lignes_articles"] = articles
    invoice["lignes"] = articles or invoice.get("lignes")
    invoice["type_confidence"] = type_conf
    invoice["type_scores"] = scores
    invoice["source"] = "python"
    invoice["lignes_releve"] = []
    return invoice


@router.post("/extract")
async def extract_document(
    file: UploadFile = File(...),
    hint: str = Form(""),
):
    path = await _save_upload(file)
    try:
        text = _extract_text(path, file.filename or "")
        return classify_and_parse(text, path, file.filename or "", hint)
    finally:
        try:
            os.remove(path)
        except OSError:
            pass


@router.post("/extract-text")
async def extract_from_text(body: TextExtractBody):
    return classify_and_parse(body.text, None, body.file_name or "", body.hint)


@router.post("/releve-bancaire")
async def parse_releve_bancaire(banque: str = Form("CIH"), file: UploadFile = File(...)):
    path = await _save_upload(file)
    try:
        text = _extract_text(path, file.filename or "")
        return {"banque": banque, "lignes": parse_bank_statement_text(text)}
    finally:
        try:
            os.remove(path)
        except OSError:
            pass
