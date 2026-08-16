#!/usr/bin/env python3
"""
POC RapidAPI Auto Parts Catalog → backupcontent (ErpProducts + vehicles + dims).
Chaîne live vérifiée:
  manufacturers/list/type-id/{typeId}
  models/list/type-id/.../manufacturer-id/.../lang-id/.../country-filter-id/...
  types/type-id/.../list-vehicles-types/{modelId}/lang-id/.../country-filter-id/...
  category/type-id/.../products-groups-variant-1/{vehicleId}/lang-id/...
  articles/list/type-id/.../vehicle-id/.../category-id/.../lang-id/...
  articles/details/article-id/{id}/lang-id/...
"""

from __future__ import annotations

import json
import os
import re
import sys
import time
import uuid
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional, Set, Tuple

import mysql.connector
import requests

ROOT = Path(__file__).resolve().parents[1]


def _appsettings_path() -> Path:
    override = os.getenv("APPSETTINGS_PATH", "").strip()
    if override:
        return Path(override)
    candidates = [
        ROOT / "Backup.Web.Api.Server" / "appsettings.json",
        Path("/app/Backup.Web.Api.Server/appsettings.json"),
        Path("/app/appsettings.json"),
    ]
    for p in candidates:
        if p.is_file():
            return p
    return candidates[0]


def _read_appsettings() -> dict:
    path = _appsettings_path()
    if not path.is_file():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))

DATA_SOURCE = "RapidApi"
TYPE_ID = int(os.getenv("RAPIDAPI_TYPE_ID", "1"))
LANG_ID = int(os.getenv("RAPIDAPI_LANG_ID", "6"))  # 6=FR si dispo, sinon fallback 4
COUNTRY_ID = int(os.getenv("RAPIDAPI_COUNTRY_ID", "34"))  # BE approx; fallback 62
MAX_PRODUCTS = int(os.getenv("RAPIDAPI_MAX_PRODUCTS", "80"))
MAX_MANUFACTURERS = int(os.getenv("RAPIDAPI_MAX_MFG", "3"))
MAX_MODELS_PER_MFG = int(os.getenv("RAPIDAPI_MAX_MODELS", "2"))
MAX_VEHICLES_PER_MODEL = int(os.getenv("RAPIDAPI_MAX_VEHICLES", "2"))
MAX_CATEGORIES_PER_VEHICLE = int(os.getenv("RAPIDAPI_MAX_CATS", "4"))
REQUEST_PAUSE_S = float(os.getenv("RAPIDAPI_PAUSE", "0.35"))
# Si false (défaut) : skip article_details/media quand RAPID-{id} existe déjà (économise Ultra).
# Si true : re-télécharge détails + images.
REFRESH_EXISTING = os.getenv("RAPIDAPI_REFRESH", "").strip() in ("1", "true", "True", "yes", "YES")

# Familles de catégories (ordre = priorité). On diversifie : au moins 1 cat / famille si dispo.
# Les essuie-glaces / ampoules passent en bas pour ne plus saturer le quota.
CAT_FAMILIES: Tuple[Tuple[str, Tuple[str, ...]], ...] = (
    ("bearing", ("bearing", "roulement", "kugellager", "moyeu", "wheel hub", "radlager", "hub")),
    ("brake", ("brake", "frein", "plaquette", "mâchoire", "machoire", "étrier", "etrier", "disque de frein")),
    ("filter", ("filter", "filtre")),
    ("suspension", ("shock", "amortisseur", "ressort", "triangle", "rotule")),
    ("drivetrain", ("clutch", "embrayage", "cardan", "soufflet")),
    ("engine", ("timing", "courroie", "bougie", "spark", "huile", "oil", "distribution")),
    ("cooling", ("radiator", "radiateur", "thermostat", "pompe à eau", "pompe a eau")),
    ("lighting", ("lamp", "phare", "ampoule", "feu")),
    ("wiper", ("wiper", "essuie")),
)

# Focus optionnel : n'importer que les cats matchant ces mots (ex. RAPIDAPI_CAT_FOCUS=roulement,bearing)
_CAT_FOCUS_RAW = os.getenv("RAPIDAPI_CAT_FOCUS", "").strip().lower()
CAT_FOCUS: Tuple[str, ...] = tuple(
    p.strip() for p in _CAT_FOCUS_RAW.replace(";", ",").split(",") if p.strip()
)

FAMILY_LABELS_FR: Dict[str, str] = {
    "bearing": "Roulements / moyeux",
    "brake": "Freinage",
    "filter": "Filtres",
    "suspension": "Suspension / direction",
    "drivetrain": "Transmission / embrayage",
    "engine": "Moteur",
    "cooling": "Refroidissement",
    "lighting": "Éclairage",
    "wiper": "Essuie-glaces",
    "other": "Autres",
}


def category_family(name: str) -> Tuple[str, int]:
    low = (name or "").lower()
    for i, (fname, keys) in enumerate(CAT_FAMILIES):
        if any(k in low for k in keys):
            return fname, i
    return "other", len(CAT_FAMILIES)


def flatten_all_leaf_categories(rows: List[Dict]) -> List[Dict]:
    """Toutes les feuilles RapidAPI, dédupliquées, triées par famille puis nom."""
    best: Dict[int, Dict] = {}
    for row in rows:
        leaf_id = None
        leaf_name = ""
        parent = ""
        for lvl in (4, 3, 2, 1):
            id_key = f"categoryId{lvl}"
            name_key = f"categoryName{lvl}"
            if row.get(id_key):
                leaf_id = int(row[id_key])
                leaf_name = str(row.get(name_key) or "")
                if lvl > 1:
                    parent = str(row.get(f"categoryName{lvl - 1}") or "")
                break
        if not leaf_id or not leaf_name:
            continue
        fam, rank = category_family(leaf_name)
        prev = best.get(leaf_id)
        if prev is None or rank < prev["familyRank"]:
            best[leaf_id] = {
                "id": leaf_id,
                "name": leaf_name,
                "parent": parent or None,
                "family": fam,
                "familyLabel": FAMILY_LABELS_FR.get(fam, fam),
                "familyRank": rank,
            }
    return sorted(best.values(), key=lambda x: (x["familyRank"], (x["name"] or "").lower()))


def pick_leaf_categories(rows: List[Dict], limit: int) -> List[Tuple[int, str]]:
    """
    Feuilles catégorie, diversifiées par famille (roulement avant essuie-glace).
    Si CAT_FOCUS est défini, ne garde que les noms matchant ces mots-clés.
    """
    candidates: List[Tuple[int, str, str]] = []  # (family_rank, cid, name)
    for row in rows:
        cid = None
        name = ""
        for lvl in (4, 3, 2, 1):
            id_key = f"categoryId{lvl}"
            name_key = f"categoryName{lvl}"
            if row.get(id_key):
                cid = int(row[id_key])
                name = str(row.get(name_key) or "")
                break
        if not cid or not name:
            continue
        low = name.lower()
        if CAT_FOCUS and not any(k in low for k in CAT_FOCUS):
            continue
        family_rank = len(CAT_FAMILIES)  # hors famille = bas
        for i, (_fname, keys) in enumerate(CAT_FAMILIES):
            if any(k in low for k in keys):
                family_rank = i
                break
        candidates.append((family_rank, cid, name))

    # Unique by cid (meilleur = plus petit family_rank)
    best: Dict[int, Tuple[int, str]] = {}
    for rank, cid, name in candidates:
        prev = best.get(cid)
        if prev is None or rank < prev[0]:
            best[cid] = (rank, name)

    # Round-robin par famille pour remplir `limit`
    by_family: Dict[int, List[Tuple[int, str]]] = {}
    for cid, (rank, name) in best.items():
        by_family.setdefault(rank, []).append((cid, name))
    for rank in by_family:
        by_family[rank].sort(key=lambda x: x[1].lower())

    out: List[Tuple[int, str]] = []
    seen: Set[int] = set()
    # 1) une (ou plus) pass(es) sur les familles prioritaires
    max_passes = max(1, limit)
    for _ in range(max_passes):
        progressed = False
        for rank in sorted(by_family.keys()):
            if len(out) >= limit:
                break
            bucket = by_family[rank]
            while bucket:
                cid, name = bucket.pop(0)
                if cid in seen:
                    continue
                seen.add(cid)
                out.append((cid, name))
                progressed = True
                break
        if len(out) >= limit or not progressed:
            break
    return out


def load_api_key() -> Tuple[str, str]:
    key = os.getenv("RAPIDAPI_KEY", "").strip()
    host = (os.getenv("RAPIDAPI_HOST") or "auto-parts-catalog.p.rapidapi.com").strip()
    if key:
        return key, host
    cfg = _read_appsettings()
    rap = cfg.get("RapidApi") or {}
    key = (rap.get("ApiKey") or "").strip()
    host = (rap.get("Host") or host).strip()
    if not key:
        raise SystemExit("RapidApi:ApiKey manquante (RAPIDAPI_KEY ou appsettings.json)")
    return key, host


def load_db_config() -> Dict[str, Any]:
    """Préfère les variables d'env (Docker) puis ConnectionStrings appsettings."""
    env_host = os.getenv("DB_HOST", "").strip()
    env_db = os.getenv("DB_NAME", "").strip()
    env_user = os.getenv("DB_USER", "").strip()
    if env_host and env_db and env_user:
        return {
            "host": env_host,
            "port": int(os.getenv("DB_PORT") or "3306"),
            "database": env_db,
            "user": env_user,
            "password": os.getenv("DB_PASSWORD") or "",
            "charset": "utf8mb4",
        }

    cfg = _read_appsettings()
    cs = cfg.get("ConnectionStrings", {}).get("DefaultConnection", "")
    parts = {}
    for chunk in cs.split(";"):
        if "=" in chunk:
            k, v = chunk.split("=", 1)
            parts[k.strip().lower()] = v.strip()

    host = os.getenv("DB_HOST") or parts.get("server") or "localhost"
    database = os.getenv("DB_NAME") or parts.get("database") or "backupcontent"
    user = os.getenv("DB_USER") or parts.get("user") or "root"
    password = os.getenv("DB_PASSWORD") or parts.get("password") or "tata"
    port = int(os.getenv("DB_PORT") or parts.get("port") or "3306")

    return {
        "host": host,
        "port": port,
        "database": database,
        "user": user,
        "password": password,
        "charset": "utf8mb4",
    }


class RapidClient:
    def __init__(self, api_key: str, host: str):
        self.base = f"https://{host}"
        self.headers = {
            "X-RapidAPI-Key": api_key,
            "X-RapidAPI-Host": host,
            "Content-Type": "application/json",
        }
        self.lang_id = LANG_ID
        self.country_id = COUNTRY_ID
        self.session = requests.Session()

    def get(self, path: str) -> Any:
        url = f"{self.base}{path}"
        time.sleep(REQUEST_PAUSE_S)
        r = self.session.get(url, headers=self.headers, timeout=60)
        if r.status_code == 404 and self.lang_id != 4:
            # fallback EN / DE playground defaults
            alt = path.replace(f"/lang-id/{self.lang_id}", "/lang-id/4")
            if "country-filter-id" in alt:
                alt = alt.replace(f"/country-filter-id/{self.country_id}", "/country-filter-id/62")
            time.sleep(REQUEST_PAUSE_S)
            r2 = self.session.get(f"{self.base}{alt}", headers=self.headers, timeout=60)
            if r2.ok:
                self.lang_id = 4
                self.country_id = 62
                return r2.json()
        if not r.ok:
            raise requests.HTTPError(f"{r.status_code} {path}: {r.text[:200]}", response=r)
        return r.json()

    def manufacturers(self) -> List[Dict]:
        data = self.get(f"/manufacturers/list/type-id/{TYPE_ID}")
        if isinstance(data, dict):
            return data.get("manufacturers") or []
        return data if isinstance(data, list) else []

    def models(self, manufacturer_id: int) -> List[Dict]:
        data = self.get(
            f"/models/list/type-id/{TYPE_ID}/manufacturer-id/{manufacturer_id}/"
            f"lang-id/{self.lang_id}/country-filter-id/{self.country_id}"
        )
        if isinstance(data, dict):
            return data.get("models") or []
        return data if isinstance(data, list) else []

    def vehicles(self, model_id: int) -> List[Dict]:
        data = self.get(
            f"/types/type-id/{TYPE_ID}/list-vehicles-types/{model_id}/"
            f"lang-id/{self.lang_id}/country-filter-id/{self.country_id}"
        )
        if isinstance(data, dict):
            return data.get("modelTypes") or data.get("vehicles") or []
        return data if isinstance(data, list) else []

    def categories(self, vehicle_id: int) -> List[Dict]:
        data = self.get(
            f"/category/type-id/{TYPE_ID}/products-groups-variant-1/{vehicle_id}/"
            f"lang-id/{self.lang_id}"
        )
        if isinstance(data, dict):
            return data.get("categories") or []
        return data if isinstance(data, list) else []

    def articles(self, vehicle_id: int, category_id: int) -> List[Dict]:
        data = self.get(
            f"/articles/list/type-id/{TYPE_ID}/vehicle-id/{vehicle_id}/"
            f"category-id/{category_id}/lang-id/{self.lang_id}"
        )
        if isinstance(data, list):
            return data
        if isinstance(data, dict):
            return data.get("articles") or data.get("data") or []
        return []

    def article_details(self, article_id: int) -> Dict:
        return self.get(f"/articles/details/article-id/{article_id}/lang-id/{self.lang_id}")

    def article_media(self, article_id: int) -> List[Dict]:
        time.sleep(REQUEST_PAUSE_S)
        url = f"{self.base}/articles/article-all-media-info"
        r = self.session.get(
            url,
            headers=self.headers,
            params={"articleId": article_id, "langId": self.lang_id},
            timeout=60,
        )
        if not r.ok and self.lang_id != 4:
            time.sleep(REQUEST_PAUSE_S)
            r = self.session.get(
                url,
                headers=self.headers,
                params={"articleId": article_id, "langId": 4},
                timeout=60,
            )
        if not r.ok:
            return []
        data = r.json()
        if isinstance(data, list):
            return data
        return data.get("data") or data.get("articleMedia") or []

    def article_oem_crossrefs(self, article_no: str, supplier_name: str) -> List[Dict]:
        """OEM via articleNo + fournisseur (1 req supplémentaire si details sans OEM)."""
        if not article_no or not supplier_name:
            return []
        path = (
            "/artlookup/search-for-cross-references-through-oem-numbers/"
            f"article-no/{requests.utils.quote(str(article_no), safe='')}/"
            f"supplierName/{requests.utils.quote(str(supplier_name), safe='')}"
        )
        try:
            data = self.get(path)
        except Exception:
            return []
        if isinstance(data, list):
            return data
        if isinstance(data, dict):
            return (
                data.get("oemNumbers")
                or data.get("oeNumbers")
                or data.get("data")
                or data.get("articles")
                or []
            )
        return []


def parse_spec_unit(name: str) -> Tuple[str, Optional[str]]:
    m = re.search(r"^(.*?)\s*\[([^\]]+)\]\s*$", name.strip())
    if m:
        return m.group(1).strip().lower(), m.group(2).strip().lower()
    return name.strip().lower(), None


def extract_dims_from_specs(specs: List[Dict]) -> Dict[str, Optional[float]]:
    result = {"weight": None, "height": None, "width": None, "depth": None}
    field_map = {
        "weight": ["weight", "poids", "masse", "mass"],
        "height": ["height", "hauteur"],
        "width": ["width", "largeur"],
        "depth": ["depth", "profondeur", "length", "longueur"],
    }
    dim_factors = {"mm": 0.1, "cm": 1.0, "m": 100.0, "in": 2.54}
    weight_factors = {"kg": 1.0, "g": 0.001, "lb": 0.453592}

    for spec in specs or []:
        raw_name = str(spec.get("criteriaName") or spec.get("name") or "")
        value = spec.get("criteriaValue", spec.get("value"))
        name, unit_in_name = parse_spec_unit(raw_name)
        unit = (spec.get("unit") or unit_in_name or "").lower()
        try:
            num = float(str(value).replace(",", "."))
        except (TypeError, ValueError):
            continue
        for field, keys in field_map.items():
            if any(k in name for k in keys):
                if field == "weight":
                    result[field] = round(num * weight_factors.get(unit, 1.0), 4)
                else:
                    result[field] = round(num * dim_factors.get(unit, 1.0), 4)
                break
    return result


def flatten_article(details: Dict, category_name: str = "", list_row: Dict | None = None) -> Dict:
    article = details.get("article") or details
    specs = details.get("articleAllSpecifications") or details.get("specifications") or []
    dims = extract_dims_from_specs(specs)
    images = []
    for media in details.get("articleMedia") or details.get("images") or []:
        url = media.get("s3image") or media.get("url") or media.get("imageUrl")
        if url:
            images.append({"url": url})
    if list_row:
        u = list_row.get("s3image") or list_row.get("imageUrl")
        if u and not any(i["url"] == u for i in images):
            images.insert(0, {"url": u})

    return {
        "articleId": article.get("articleId") or details.get("articleId"),
        "articleNo": article.get("articleNo") or article.get("articleNumber"),
        "name": article.get("articleProductName") or article.get("articleName") or category_name,
        "supplierName": article.get("supplierName") or article.get("brandName") or "",
        "ean": article.get("eanNumber") or article.get("ean") or "",
        "category": category_name or article.get("articleProductName") or "Pièce auto",
        "specs": specs,
        "dims": dims,
        "images": images,
        "oems": extract_oem_list(details, article),
        "raw": details,
    }


def extract_oem_list(details: Dict, article: Dict | None = None) -> List[Dict[str, Any]]:
    """Extrait les numéros OEM depuis un payload article details (clés variables TecDoc/RapidAPI)."""
    article = article or details.get("article") or details
    candidates: List[Any] = []
    for src in (details, article):
        if not isinstance(src, dict):
            continue
        for key in (
            # RapidAPI live: articleOemNo = [{oemBrand, oemDisplayNo, ...}]
            "articleOemNo", "articleOEM", "articleOems",
            "oemNumbers", "oeNumbers", "oenNumbers",
            "oem", "OEMs", "oems", "originalNumbers", "replacesNumbers",
        ):
            val = src.get(key)
            if val:
                candidates.append(val)

    out: List[Dict[str, Any]] = []
    seen: Set[str] = set()

    def add(number: Any, brand: Any = None, is_original: Any = None) -> None:
        num = str(number or "").strip()
        if not num or num.lower() in ("none", "null", "-"):
            return
        key = num.upper()
        if key in seen:
            return
        seen.add(key)
        out.append({
            "oemNumber": num[:128],
            "brand": (str(brand).strip()[:128] if brand else "") or "",
            "isOriginal": bool(is_original) if is_original is not None else True,
        })

    def number_from_item(item: Dict) -> Any:
        return (
            item.get("oemDisplayNo") or item.get("oemNumber") or item.get("oeNumber")
            or item.get("oenNumber") or item.get("displayNo") or item.get("number")
            or item.get("articleNo") or item.get("oem")
        )

    def brand_from_item(item: Dict) -> Any:
        return (
            item.get("oemBrand") or item.get("brand") or item.get("manufacturerName")
            or item.get("supplierName") or item.get("mfrName")
        )

    for block in candidates:
        if isinstance(block, list):
            for item in block:
                if isinstance(item, str):
                    add(item)
                elif isinstance(item, dict):
                    add(
                        number_from_item(item),
                        brand_from_item(item),
                        item.get("isOriginal") if "isOriginal" in item else item.get("original"),
                    )
        elif isinstance(block, dict):
            # parfois { oemNumbers: [...] } ou map brand→number
            nested = (
                block.get("oemNumbers") or block.get("oeNumbers")
                or block.get("articleOemNo") or block.get("data")
            )
            if isinstance(nested, list):
                for item in nested:
                    if isinstance(item, str):
                        add(item)
                    elif isinstance(item, dict):
                        add(number_from_item(item), brand_from_item(item))
            else:
                for k, v in block.items():
                    if isinstance(v, (str, int)):
                        add(v, k)
                    elif isinstance(v, list):
                        for item in v:
                            if isinstance(item, str):
                                add(item, k)
                            elif isinstance(item, dict):
                                add(number_from_item(item), brand_from_item(item) or k)
        elif isinstance(block, str):
            add(block)

    return out


def _clip(value: Optional[str], n: int) -> Optional[str]:
    if value is None:
        return None
    s = str(value).strip()
    return s[:n] if s else None


def _first_str(obj: Dict, *keys: str) -> Optional[str]:
    for k in keys:
        if k not in obj or obj[k] is None:
            continue
        s = str(obj[k]).strip()
        if s and s.lower() not in ("none", "null", "-"):
            return s
    return None


def _first_int(obj: Dict, *keys: str) -> Optional[int]:
    for k in keys:
        if k not in obj or obj[k] is None:
            continue
        raw = obj[k]
        try:
            if isinstance(raw, bool):
                continue
            if isinstance(raw, (int, float)):
                return int(raw)
            s = str(raw).strip().replace(",", ".")
            # "90 kW" / "1390 ccm"
            m = re.match(r"^(\d+(?:\.\d+)?)", s)
            if m:
                return int(float(m.group(1)))
        except (TypeError, ValueError):
            continue
    return None


def _year_part(raw) -> Optional[int]:
    if raw is None:
        return None
    try:
        return int(str(raw)[:4])
    except ValueError:
        return None


def extract_vehicle_row(vehicle: Dict, make: str, model_name: str) -> Dict[str, Any]:
    """
    Mappe un payload list-vehicles-types vers ErpProductVehicles.
    Aliases TecDoc / RapidAPI couverts ; RawJson garde le JSON complet.
    """
    year_from = _year_part(
        vehicle.get("constructionIntervalStart")
        or vehicle.get("modelYearFrom")
        or vehicle.get("yearOfConstrFrom")
        or vehicle.get("yearFrom")
    )
    year_to = _year_part(
        vehicle.get("constructionIntervalEnd")
        or vehicle.get("modelYearTo")
        or vehicle.get("yearOfConstrTo")
        or vehicle.get("yearTo")
    )

    power_kw = _first_int(
        vehicle,
        "powerKw", "powerKW", "PowerKW", "powerKwFrom", "powerKwTo",
        "impulsionPower", "motorPower",
    )
    power_hp = _first_int(
        vehicle,
        "powerHp", "powerHP", "PowerHP", "powerHpFrom", "powerHpTo",
        "horsePower", "ps", "PS",
    )
    if power_hp is None and power_kw is not None:
        power_hp = int(round(power_kw * 1.35962))
    if power_kw is None and power_hp is not None:
        power_kw = int(round(power_hp / 1.35962))

    ccm = _first_int(
        vehicle,
        "capacityCC", "capacityCc", "ccm", "Ccm", "cylinderCapacity",
        "engineCapacity", "capacityTech",
    )

    # Codes moteur : string ou liste
    engine = _first_str(
        vehicle,
        "typeEngineName", "engineCode", "EngineCode", "motorCode",
        "engines", "engineCodes",
    )
    if engine is None and isinstance(vehicle.get("engineCodes"), list):
        codes = [str(x).strip() for x in vehicle["engineCodes"] if x]
        engine = ", ".join(codes) if codes else None

    raw_json = None
    try:
        raw_json = json.dumps(vehicle, ensure_ascii=False, default=str)
    except (TypeError, ValueError):
        raw_json = str(vehicle)

    return {
        "make": (_first_str(vehicle, "manufacturerName", "manuName", "make") or make or "")[:128],
        # model_name = nom catalogue du sync (ex. AMAROK (T1A, T1B)) — prioritaire sur vehicle.modelName (souvent type/moteur)
        "model": (model_name or _first_str(vehicle, "modelName", "MakeModelName", "model") or "")[:128],
        "type_name": _first_str(
            vehicle, "typeName", "vehicleTypeName", "type", "fullName", "description"
        ),
        "year_from": year_from,
        "year_to": year_to,
        "engine_code": engine,
        "ktype": _first_str(vehicle, "vehicleId", "VehicleId", "ktype", "KType", "carId") or "",
        "ext_manu_id": _first_str(vehicle, "manufacturerId", "manuId", "makeId"),
        "ext_model_id": _first_str(vehicle, "modelId", "modId"),
        "body_type": _first_str(vehicle, "bodyType", "BodyType", "constructionType", "body"),
        "fuel_type": _first_str(
            vehicle, "fuelType", "FuelType", "fuelTypeProcess", "fuel", "motorType"
        ),
        "drive_type": _first_str(
            vehicle, "driveType", "DriveType", "drive", "absDriveType", "impulsionType"
        ),
        "transmission": _first_str(
            vehicle, "transmission", "Transmission", "gearbox", "gearBoxType", "salesDescription"
        ),
        "power_kw": power_kw,
        "power_hp": power_hp,
        "ccm": ccm,
        "cylinders": _first_int(
            vehicle, "cylinders", "Cylinders", "numberOfCylinders", "cylinder"
        ),
        "valves": _first_int(
            vehicle, "valves", "Valves", "numberOfValves", "valvesTotal"
        ),
        "raw_json": raw_json,
    }


class DbWriter:
    def __init__(self, cfg: Dict[str, Any]):
        self.conn = mysql.connector.connect(**cfg)
        self.cur = self.conn.cursor(dictionary=True)
        self.now = datetime.now()
        self.stats = {
            "created": 0,
            "updated": 0,
            "vehicles": 0,
            "images": 0,
            "oems": 0,
            "errors": 0,
            "skipped": 0,
        }
        self._existing_article_ids: Optional[Set[int]] = None

    def close(self):
        self.cur.close()
        self.conn.close()

    def execute(self, sql: str, params=None):
        self.cur.execute(sql, params or ())
        self.conn.commit()
        return self.cur

    def fetchone(self, sql: str, params=None):
        self.cur.execute(sql, params or ())
        return self.cur.fetchone()

    def load_existing_article_ids(self) -> Set[int]:
        """Ids numériques déjà en base (ErpProductId = RAPID-{id})."""
        if self._existing_article_ids is not None:
            return self._existing_article_ids
        self.cur.execute(
            "SELECT ErpProductId FROM ErpProducts WHERE ErpProductId LIKE 'RAPID-%'"
        )
        out: Set[int] = set()
        for row in self.cur.fetchall() or []:
            raw = row.get("ErpProductId") if isinstance(row, dict) else row[0]
            if not raw:
                continue
            try:
                out.add(int(str(raw).split("-", 1)[1]))
            except (ValueError, IndexError):
                continue
        self._existing_article_ids = out
        return out

    def get_product_id_by_article(self, article_id: int) -> Optional[int]:
        row = self.fetchone(
            "SELECT Id FROM ErpProducts WHERE ErpProductId=%s LIMIT 1",
            (f"RAPID-{article_id}",),
        )
        return int(row["Id"]) if row else None

    def mark_article_known(self, article_id: int) -> None:
        ids = self.load_existing_article_ids()
        ids.add(article_id)

    def get_or_create_brand(self, name: str) -> Optional[int]:
        if not name:
            return None
        row = self.fetchone("SELECT Id FROM ErpBrands WHERE Name = %s LIMIT 1", (name,))
        if row:
            return row["Id"]
        slug = re.sub(r"[^a-z0-9-]+", "-", name.lower()).strip("-")[:120] or "brand"
        self.execute(
            """INSERT INTO ErpBrands (Name, Slug, Description, IsActive, CreatedAt, UpdatedAt)
               VALUES (%s, %s, %s, 1, %s, %s)""",
            (name, slug, "Fournisseur RapidApi", self.now, self.now),
        )
        return self.cur.lastrowid

    def get_or_create_category(self, name: str) -> Optional[int]:
        if not name:
            name = "Pièces auto RapidApi"
        row = self.fetchone(
            "SELECT Id FROM ErpCategories WHERE Level = %s AND NameFr = %s LIMIT 1",
            ("Type", name),
        )
        if row:
            return row["Id"]
        slug = re.sub(r"[^a-z0-9-]+", "-", name.lower()).strip("-")[:80] or "piece"
        ext = f"RAPID_{slug[:30]}_{uuid.uuid4().hex[:6]}"
        self.execute(
            """INSERT INTO ErpCategories
               (ErpExternalId, Level, NameNl, NameFr, NameEn, SlugNl, SlugFr, SlugEn,
                SortOrder, IsActive, CreatedAt)
               VALUES (%s,'Type',%s,%s,%s,%s,%s,%s,0,1,%s)""",
            (ext, name, name, name, slug, slug, slug, self.now),
        )
        return self.cur.lastrowid

    def upsert_product(self, flat: Dict, brand_id: Optional[int], category_id: Optional[int]) -> Optional[int]:
        erp_id = f"RAPID-{flat['articleId']}"
        dims = flat["dims"]
        name = (flat["name"] or flat["articleNo"] or erp_id)[:255]
        ref = (flat["articleNo"] or "")[:128]
        brand = (flat["supplierName"] or "")[:128]
        pic = None
        if flat.get("images"):
            pic = (flat["images"][0].get("url") or "")[:500] or None
        type_ext = None
        type_name = flat.get("category")
        if category_id:
            crow = self.fetchone(
                "SELECT ErpExternalId, NameFr, Level FROM ErpCategories WHERE Id=%s",
                (category_id,),
            )
            if crow:
                type_ext = crow.get("ErpExternalId")
                type_name = crow.get("NameFr") or type_name
        existing = self.fetchone("SELECT Id FROM ErpProducts WHERE ErpProductId = %s", (erp_id,))
        if existing:
            pid = existing["Id"]
            self.execute(
                """UPDATE ErpProducts SET
                    Name=%s, Reference=%s, Ean=%s, Brand=%s, Manufacturer=%s, PicName=COALESCE(%s, PicName),
                    Weight=%s, Height=%s, Width=%s, Depth=%s,
                    BrandId=%s, CategoryId=%s, TypeID=%s, TypeName=%s, MainTypeName=%s,
                    UpdatedAt=%s, LastSyncAt=%s, DataSource=%s
                   WHERE Id=%s""",
                (
                    name, ref, flat.get("ean") or None, brand, brand, pic,
                    dims.get("weight"), dims.get("height"), dims.get("width"), dims.get("depth"),
                    brand_id, category_id, type_ext, type_name, type_name,
                    self.now, self.now, DATA_SOURCE, pid,
                ),
            )
            self.stats["updated"] += 1
            return pid

        self.execute(
            """INSERT INTO ErpProducts (
                ErpProductId, Name, Reference, Ean, Brand, Manufacturer, PicName,
                PriceHT, UnitPrice, CPrice, RPrice, VatIncluded, TypeVatPerc,
                DiscountPerc, ProductDiscountPerc, TypeDiscountPerc, PromoActive,
                StockQuantity, StockDate, Quantity, PerUnit,
                Weight, Height, Width, Depth,
                MainTypeName, TypeName, TypeID, Archived, CreatedAt, UpdatedAt, LastSyncAt,
                DataSource, FromExcel, BrandId, CategoryId
            ) VALUES (
                %s,%s,%s,%s,%s,%s,%s,
                0,0,0,0,0,21,
                0,0,0,0,
                0,%s,1,'piece',
                %s,%s,%s,%s,
                %s,%s,%s,0,%s,%s,%s,
                %s,0,%s,%s
            )""",
            (
                erp_id, name, ref, flat.get("ean") or None, brand, brand, pic,
                self.now,
                dims.get("weight"), dims.get("height"), dims.get("width"), dims.get("depth"),
                type_name, type_name, type_ext,
                self.now, self.now, self.now,
                DATA_SOURCE, brand_id, category_id,
            ),
        )
        self.stats["created"] += 1
        return self.cur.lastrowid

    def sync_vehicle(self, product_id: int, vehicle: Dict, make: str, model_name: str):
        fields = extract_vehicle_row(vehicle, make, model_name)
        exists = self.fetchone(
            """SELECT Id FROM ErpProductVehicles
               WHERE ProductId=%s AND Make=%s AND Model=%s AND IFNULL(KType,'')=%s LIMIT 1""",
            (product_id, fields["make"], fields["model"], fields["ktype"] or ""),
        )
        if exists:
            # Enrichir une ligne existante (re-sync) avec les champs manquants + RawJson
            self.execute(
                """UPDATE ErpProductVehicles SET
                    TypeName=COALESCE(%s, TypeName),
                    YearFrom=COALESCE(%s, YearFrom),
                    YearTo=COALESCE(%s, YearTo),
                    EngineCode=COALESCE(%s, EngineCode),
                    ExternalManufacturerId=COALESCE(%s, ExternalManufacturerId),
                    ExternalModelId=COALESCE(%s, ExternalModelId),
                    BodyType=COALESCE(%s, BodyType),
                    FuelType=COALESCE(%s, FuelType),
                    DriveType=COALESCE(%s, DriveType),
                    Transmission=COALESCE(%s, Transmission),
                    PowerKW=COALESCE(%s, PowerKW),
                    PowerHP=COALESCE(%s, PowerHP),
                    Ccm=COALESCE(%s, Ccm),
                    Cylinders=COALESCE(%s, Cylinders),
                    Valves=COALESCE(%s, Valves),
                    RawJson=%s
                   WHERE Id=%s""",
                (
                    fields["type_name"], fields["year_from"], fields["year_to"],
                    fields["engine_code"], fields["ext_manu_id"], fields["ext_model_id"],
                    fields["body_type"], fields["fuel_type"], fields["drive_type"],
                    fields["transmission"], fields["power_kw"], fields["power_hp"],
                    fields["ccm"], fields["cylinders"], fields["valves"],
                    fields["raw_json"], exists["Id"],
                ),
            )
            return
        self.execute(
            """INSERT INTO ErpProductVehicles
               (Id, ProductId, Make, Model, TypeName, YearFrom, YearTo, EngineCode, KType,
                ExternalManufacturerId, ExternalModelId, BodyType, FuelType, DriveType, Transmission,
                PowerKW, PowerHP, Ccm, Cylinders, Valves, RawJson, CreatedAt)
               VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)""",
            (
                str(uuid.uuid4()), product_id,
                fields["make"][:128], fields["model"][:128],
                _clip(fields["type_name"], 256),
                fields["year_from"], fields["year_to"],
                _clip(fields["engine_code"], 64), _clip(fields["ktype"], 64),
                _clip(fields["ext_manu_id"], 64), _clip(fields["ext_model_id"], 64),
                _clip(fields["body_type"], 64), _clip(fields["fuel_type"], 64),
                _clip(fields["drive_type"], 64), _clip(fields["transmission"], 64),
                fields["power_kw"], fields["power_hp"], fields["ccm"],
                fields["cylinders"], fields["valves"],
                fields["raw_json"], self.now,
            ),
        )
        self.stats["vehicles"] += 1

    def sync_images(self, product_id: int, images: List[Dict]):
        if not images:
            return
        self.execute("DELETE FROM ErpProductImages WHERE ProductId=%s", (product_id,))
        for idx, img in enumerate(images[:8]):
            url = img.get("url")
            if not url:
                continue
            self.execute(
                """INSERT INTO ErpProductImages (Id, ProductId, Url, AltText, IsMain, SortOrder, CreatedAt)
                   VALUES (%s,%s,%s,%s,%s,%s,%s)""",
                (str(uuid.uuid4()), product_id, url, "", 1 if idx == 0 else 0, idx, self.now),
            )
            self.stats["images"] += 1

    def sync_oem(self, product_id: int, oems: List[Dict[str, Any]]) -> int:
        """Remplace les OEM d'un produit. Retourne le nombre inséré."""
        if not oems:
            return 0
        self.execute("DELETE FROM ErpOemCrossReferences WHERE ProductId=%s", (product_id,))
        n = 0
        seen: Set[str] = set()
        for oem in oems:
            number = str(oem.get("oemNumber") or oem.get("number") or "").strip()
            if not number:
                continue
            key = number.upper()
            if key in seen:
                continue
            seen.add(key)
            brand = str(oem.get("brand") or "")[:128]
            is_orig = 1 if oem.get("isOriginal", True) else 0
            try:
                self.execute(
                    """INSERT INTO ErpOemCrossReferences
                       (Id, ProductId, OemNumber, Brand, IsOriginal, CreatedAt)
                       VALUES (%s,%s,%s,%s,%s,%s)""",
                    (str(uuid.uuid4()), product_id, number[:128], brand or None, is_orig, self.now),
                )
                n += 1
            except Exception:
                # Unique (ProductId, OemNumber) — ignore doublon
                continue
        self.stats["oems"] += n
        return n


def preferred_manufacturers(all_mfg: List[Dict]) -> List[Dict]:
    want = {"RENAULT", "PEUGEOT", "CITROEN", "CITROËN", "VW", "VOLKSWAGEN", "BMW", "AUDI", "FORD", "OPEL"}
    picked = [m for m in all_mfg if str(m.get("manufacturerName", "")).upper() in want]
    if not picked:
        return all_mfg[:MAX_MANUFACTURERS]
    return picked[:MAX_MANUFACTURERS]


def process_article(
    db: "DbWriter",
    client: "RapidClient",
    art: Dict,
    vehicle: Dict,
    make: str,
    model_name: str,
    cat_name: str,
    fetched_ids: Set[int],
    refresh: bool = REFRESH_EXISTING,
) -> bool:
    """
    Traite un article liste. Retourne True si détails API ont été tirés (compte pour MAX_PRODUCTS).
    Skip détail si RAPID-{id} existe déjà (sauf refresh) — lie quand même le véhicule.
    """
    aid = art.get("articleId") or (art.get("article") or {}).get("articleId")
    if not aid:
        return False
    aid = int(aid)

    existing_ids = db.load_existing_article_ids()
    pid = db.get_product_id_by_article(aid) if aid in existing_ids else None

    # Déjà traité dans ce run → lien véhicule seulement
    if aid in fetched_ids:
        if pid:
            db.sync_vehicle(pid, vehicle, make, model_name)
        return False

    # Existe en base et pas de refresh → skip API détail/media
    if pid and not refresh:
        db.sync_vehicle(pid, vehicle, make, model_name)
        db.stats["skipped"] += 1
        return False

    was_existing = pid is not None
    details = client.article_details(aid)
    media = client.article_media(aid)
    if media:
        details["articleMedia"] = media
    flat = flatten_article(details, cat_name, art)
    # Si details sans OEM → endpoint cross-ref (articleNo + supplier)
    if not flat.get("oems"):
        extra = client.article_oem_crossrefs(
            str(flat.get("articleNo") or ""),
            str(flat.get("supplierName") or ""),
        )
        if extra:
            flat["oems"] = extract_oem_list({"oemNumbers": extra}, {})
    brand_id = db.get_or_create_brand(flat["supplierName"])
    cat_db = db.get_or_create_category(flat["category"])
    pid = db.upsert_product(flat, brand_id, cat_db)
    if not pid:
        return False
    db.sync_vehicle(pid, vehicle, make, model_name)
    db.sync_images(pid, flat.get("images") or [])
    oem_n = db.sync_oem(pid, flat.get("oems") or [])
    db.mark_article_known(aid)
    fetched_ids.add(aid)
    d = flat["dims"]
    action = "↻" if was_existing else "+"
    print(
        f"        {action} {flat.get('articleNo')} {(flat.get('name') or '')[:40]} "
        f"H={d.get('height')} W={d.get('width')} D={d.get('depth')} oem={oem_n}"
    )
    return True


def run():
    api_key, host = load_api_key()
    db_cfg = load_db_config()
    client = RapidClient(api_key, host)
    db = DbWriter(db_cfg)

    print(f"Host={host} lang={LANG_ID} country={COUNTRY_ID} max={MAX_PRODUCTS} refresh={REFRESH_EXISTING}")
    print(f"DB={db_cfg['database']}@{db_cfg['host']}")
    known = db.load_existing_article_ids()
    print(f"Articles déjà en base (skip détail): {len(known)}")

    try:
        mfgs = preferred_manufacturers(client.manufacturers())
        print(f"{len(mfgs)} constructeurs cibles")
        fetched_ids: Set[int] = set()

        for mfg in mfgs:
            if len(fetched_ids) >= MAX_PRODUCTS:
                break
            mid = int(mfg["manufacturerId"])
            mname = mfg.get("manufacturerName", "?")
            print(f"\n=== {mname} (id={mid}) ===")
            try:
                models = client.models(mid)[:MAX_MODELS_PER_MFG]
            except Exception as e:
                print(f"  models ERR: {e}")
                continue

            for model in models:
                if len(fetched_ids) >= MAX_PRODUCTS:
                    break
                model_id = int(model["modelId"])
                model_name = model.get("modelName", "")
                print(f"  modèle: {model_name}")
                try:
                    vehicles = client.vehicles(model_id)[:MAX_VEHICLES_PER_MODEL]
                except Exception as e:
                    print(f"  vehicles ERR: {e}")
                    continue

                for vehicle in vehicles:
                    if len(fetched_ids) >= MAX_PRODUCTS:
                        break
                    vid = int(vehicle["vehicleId"])
                    eng = vehicle.get("typeEngineName", "")
                    print(f"    véhicule {vid} {eng}")
                    try:
                        cats = pick_leaf_categories(client.categories(vid), MAX_CATEGORIES_PER_VEHICLE)
                    except Exception as e:
                        print(f"    categories ERR: {e}")
                        continue

                    for cat_id, cat_name in cats:
                        if len(fetched_ids) >= MAX_PRODUCTS:
                            break
                        try:
                            arts = client.articles(vid, cat_id)
                        except Exception as e:
                            print(f"    articles {cat_name} ERR: {e}")
                            continue
                        print(f"      cat {cat_name} ({cat_id}): {len(arts)} articles")

                        for art in arts:
                            if len(fetched_ids) >= MAX_PRODUCTS:
                                break
                            try:
                                process_article(
                                    db, client, art, vehicle, mname, model_name, cat_name, fetched_ids
                                )
                            except Exception as e:
                                db.stats["errors"] += 1
                                print(f"        ERR article: {e}")

        print("\nSTATS", db.stats)
        print(f"   Nouveaux/rafraîchis (API détail): {len(fetched_ids)}")
    finally:
        db.close()


if __name__ == "__main__":
    run()
