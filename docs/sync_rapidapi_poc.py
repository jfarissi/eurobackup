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
from typing import Any, Dict, List, Optional, Tuple

import mysql.connector
import requests

ROOT = Path(__file__).resolve().parents[1]
APPSETTINGS = ROOT / "Backup.Web.Api.Server" / "appsettings.json"

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

# Catégories utiles (filtres / freinage / éclairage) — match case-insensitive sur le nom
PREFERRED_CAT_KEYWORDS = (
    "filter", "filtre", "brake", "frein", "oil", "huile",
    "spark", "bougie", "lamp", "phare", "wiper", "essuie",
    "timing", "courroie", "pad", "plaque",
)


def load_api_key() -> Tuple[str, str]:
    key = os.getenv("RAPIDAPI_KEY", "").strip()
    host = "auto-parts-catalog.p.rapidapi.com"
    if key:
        return key, host
    cfg = json.loads(APPSETTINGS.read_text(encoding="utf-8"))
    rap = cfg.get("RapidApi") or {}
    key = (rap.get("ApiKey") or "").strip()
    host = (rap.get("Host") or host).strip()
    if not key:
        raise SystemExit("RapidApi:ApiKey manquante (appsettings.json ou RAPIDAPI_KEY)")
    return key, host


def load_db_config() -> Dict[str, Any]:
    """Préfère les variables d'env (Docker démo) puis ConnectionStrings appsettings."""
    cfg = json.loads(APPSETTINGS.read_text(encoding="utf-8"))
    cs = cfg.get("ConnectionStrings", {}).get("DefaultConnection", "")
    # Server=localhost;Database=backupcontent;User=root;Password=tata;
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


def pick_leaf_categories(rows: List[Dict], limit: int) -> List[Tuple[int, str]]:
    """Retourne (categoryId, name) feuilles, priorisant les mots-clés utiles."""
    scored: List[Tuple[int, int, str]] = []
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
        if not cid:
            continue
        low = name.lower()
        score = 0 if any(k in low for k in PREFERRED_CAT_KEYWORDS) else 1
        scored.append((score, cid, name))
    # unique by id, preferred first
    seen = set()
    out: List[Tuple[int, str]] = []
    for score, cid, name in sorted(scored, key=lambda x: (x[0], x[2])):
        if cid in seen:
            continue
        seen.add(cid)
        out.append((cid, name))
        if len(out) >= limit:
            break
    return out


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
        "raw": details,
    }


class DbWriter:
    def __init__(self, cfg: Dict[str, Any]):
        self.conn = mysql.connector.connect(**cfg)
        self.cur = self.conn.cursor(dictionary=True)
        self.now = datetime.now()
        self.stats = {"created": 0, "updated": 0, "vehicles": 0, "images": 0, "errors": 0}

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
        existing = self.fetchone("SELECT Id FROM ErpProducts WHERE ErpProductId = %s", (erp_id,))
        if existing:
            pid = existing["Id"]
            self.execute(
                """UPDATE ErpProducts SET
                    Name=%s, Reference=%s, Ean=%s, Brand=%s, Manufacturer=%s, PicName=COALESCE(%s, PicName),
                    Weight=%s, Height=%s, Width=%s, Depth=%s,
                    BrandId=%s, CategoryId=%s, UpdatedAt=%s, LastSyncAt=%s, DataSource=%s
                   WHERE Id=%s""",
                (
                    name, ref, flat.get("ean") or None, brand, brand, pic,
                    dims.get("weight"), dims.get("height"), dims.get("width"), dims.get("depth"),
                    brand_id, category_id, self.now, self.now, DATA_SOURCE, pid,
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
                MainTypeName, TypeName, Archived, CreatedAt, UpdatedAt, LastSyncAt,
                DataSource, FromExcel, BrandId, CategoryId
            ) VALUES (
                %s,%s,%s,%s,%s,%s,%s,
                0,0,0,0,0,21,
                0,0,0,0,
                0,%s,1,'piece',
                %s,%s,%s,%s,
                %s,%s,0,%s,%s,%s,
                %s,0,%s,%s
            )""",
            (
                erp_id, name, ref, flat.get("ean") or None, brand, brand, pic,
                self.now,
                dims.get("weight"), dims.get("height"), dims.get("width"), dims.get("depth"),
                flat.get("category"), flat.get("category"),
                self.now, self.now, self.now,
                DATA_SOURCE, brand_id, category_id,
            ),
        )
        self.stats["created"] += 1
        return self.cur.lastrowid

    def sync_vehicle(self, product_id: int, vehicle: Dict, make: str, model_name: str):
        year_from = year_to = None
        start = vehicle.get("constructionIntervalStart") or vehicle.get("modelYearFrom")
        end = vehicle.get("constructionIntervalEnd") or vehicle.get("modelYearTo")
        if start:
            try:
                year_from = int(str(start)[:4])
            except ValueError:
                pass
        if end:
            try:
                year_to = int(str(end)[:4])
            except ValueError:
                pass
        make_v = vehicle.get("manufacturerName") or make
        model_v = vehicle.get("modelName") or model_name
        engine = vehicle.get("typeEngineName") or vehicle.get("engineCode") or ""
        ktype = str(vehicle.get("vehicleId") or "")
        # avoid duplicates
        exists = self.fetchone(
            """SELECT Id FROM ErpProductVehicles
               WHERE ProductId=%s AND Make=%s AND Model=%s AND IFNULL(KType,'')=%s LIMIT 1""",
            (product_id, make_v, model_v, ktype),
        )
        if exists:
            return
        self.execute(
            """INSERT INTO ErpProductVehicles
               (Id, ProductId, Make, Model, YearFrom, YearTo, EngineCode, KType, BodyType, FuelType, CreatedAt)
               VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)""",
            (
                str(uuid.uuid4()), product_id, make_v[:128], model_v[:128],
                year_from, year_to, (engine or "")[:64], ktype[:64],
                (vehicle.get("bodyType") or "")[:64],
                (vehicle.get("fuelType") or "")[:64],
                self.now,
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


def preferred_manufacturers(all_mfg: List[Dict]) -> List[Dict]:
    want = {"RENAULT", "PEUGEOT", "CITROEN", "CITROËN", "VW", "VOLKSWAGEN", "BMW", "AUDI", "FORD", "OPEL"}
    picked = [m for m in all_mfg if str(m.get("manufacturerName", "")).upper() in want]
    if not picked:
        return all_mfg[:MAX_MANUFACTURERS]
    return picked[:MAX_MANUFACTURERS]


def run():
    api_key, host = load_api_key()
    db_cfg = load_db_config()
    client = RapidClient(api_key, host)
    db = DbWriter(db_cfg)

    print(f"Host={host} lang={LANG_ID} country={COUNTRY_ID} max={MAX_PRODUCTS}")
    print(f"DB={db_cfg['database']}@{db_cfg['host']}")

    try:
        mfgs = preferred_manufacturers(client.manufacturers())
        print(f"{len(mfgs)} constructeurs cibles")
        imported_ids = set()

        for mfg in mfgs:
            if len(imported_ids) >= MAX_PRODUCTS:
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
                if len(imported_ids) >= MAX_PRODUCTS:
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
                    if len(imported_ids) >= MAX_PRODUCTS:
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
                        if len(imported_ids) >= MAX_PRODUCTS:
                            break
                        try:
                            arts = client.articles(vid, cat_id)
                        except Exception as e:
                            print(f"    articles {cat_name} ERR: {e}")
                            continue
                        print(f"      cat {cat_name} ({cat_id}): {len(arts)} articles")

                        for art in arts:
                            if len(imported_ids) >= MAX_PRODUCTS:
                                break
                            aid = art.get("articleId") or (art.get("article") or {}).get("articleId")
                            if not aid:
                                continue
                            aid = int(aid)
                            if aid in imported_ids:
                                # still add vehicle link if product exists
                                row = db.fetchone(
                                    "SELECT Id FROM ErpProducts WHERE ErpProductId=%s",
                                    (f"RAPID-{aid}",),
                                )
                                if row:
                                    db.sync_vehicle(row["Id"], vehicle, mname, model_name)
                                continue
                            try:
                                details = client.article_details(aid)
                                media = client.article_media(aid)
                                if media:
                                    details["articleMedia"] = media
                                flat = flatten_article(details, cat_name, art)
                                brand_id = db.get_or_create_brand(flat["supplierName"])
                                cat_db = db.get_or_create_category(flat["category"])
                                pid = db.upsert_product(flat, brand_id, cat_db)
                                if not pid:
                                    continue
                                db.sync_vehicle(pid, vehicle, mname, model_name)
                                db.sync_images(pid, flat.get("images") or [])
                                imported_ids.add(aid)
                                d = flat["dims"]
                                print(
                                    f"        + {flat['articleNo']} {flat['name'][:40]} "
                                    f"H={d.get('height')} W={d.get('width')} D={d.get('depth')} "
                                    f"imgs={len(flat.get('images') or [])}"
                                )
                            except Exception as e:
                                db.stats["errors"] += 1
                                print(f"        ERR article {aid}: {e}")

        print("\nSTATS", db.stats)
        print(f"   Articles uniques: {len(imported_ids)}")
    finally:
        db.close()


if __name__ == "__main__":
    run()
