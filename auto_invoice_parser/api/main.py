import os
from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.responses import JSONResponse
from utils.extractor import extract_pdf_raw
from importlib import import_module

app = FastAPI(title='AutoInvoiceParser')

PARSERS = {
    'knauf': 'parsers.knauf',
    'generic': 'parsers.generic'
}

def detect_fournisseur(text: str):
    if 'Knauf' in text or 'Rue du Parc Industriel' in text:
        return 'knauf'
    return 'generic'

@app.post('/parse')
async def parse_pdf(file: UploadFile = File(...)):
    if not file.filename.lower().endswith('.pdf'):
        raise HTTPException(status_code=400, detail='Only PDF supported')

    tmp = f"/tmp/{file.filename}"
    content = await file.read()
    with open(tmp, 'wb') as f:
        f.write(content)

    pdf_raw = extract_pdf_raw(tmp)
    text = pdf_raw['full_text']
    fournisseur = detect_fournisseur(text)

    module_path = PARSERS.get(fournisseur, 'parsers.generic')
    mod = import_module(module_path)
    result = mod.parse(pdf_raw)

    try:
        os.remove(tmp)
    except Exception:
        pass

    return JSONResponse(result)
