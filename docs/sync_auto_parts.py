#!/usr/bin/env python3
"""
Sync Auto Parts Catalog → ERP Database v2
Gestion complète des dimensions + unités de mesure
Compatible avec la structure erpproducts / erpproductimages / erpbrands / erpcategories
"""

import os
import hashlib
import uuid
import re
from datetime import datetime
from typing import List, Dict, Optional, Tuple
import requests
import mysql.connector
from mysql.connector import Error

# ──────────────────────────────────────────────
# CONFIGURATION
# ──────────────────────────────────────────────

DB_CONFIG = {
    "host": os.getenv("DB_HOST", "localhost"),
    "port": int(os.getenv("DB_PORT", "3306")),
    "database": os.getenv("DB_NAME", "backupcontent"),
    "user": os.getenv("DB_USER", "root"),
    "password": os.getenv("DB_PASSWORD", "tata"),
    "charset": "utf8mb4",
    "collation": "utf8mb4_unicode_ci",
}

# API Auto Parts Catalog (RapidAPI)
# Endpoints réels: path params (voir catamc90/auto-parts-catalog CatalogApi.php)
# LangId 4 = EN, 6 = FR ; CountryFilterId 62 = DE (ex. playground) ; TypeId 1 = passenger cars
API_CONFIG = {
    "base_url": "https://auto-parts-catalog.p.rapidapi.com",
    "headers": {
        "X-RapidAPI-Key": os.getenv("RAPIDAPI_KEY", ""),
        "X-RapidAPI-Host": "auto-parts-catalog.p.rapidapi.com",
    },
    "lang_id": int(os.getenv("RAPIDAPI_LANG_ID", "6")),
    "country_filter_id": int(os.getenv("RAPIDAPI_COUNTRY_ID", "34")),
    "type_id": int(os.getenv("RAPIDAPI_TYPE_ID", "1")),
}

# TecDoc Web Service (si tu as une licence)
TECDOC_CONFIG = {
    "base_url": os.getenv("TECDOC_URL", ""),
    "api_key": os.getenv("TECDOC_KEY", ""),
    "provider_id": int(os.getenv("TECDOC_PROVIDER", "0")),
}

DATA_SOURCE = "RapidApi"  # DataSource dans ErpProducts
BATCH_SIZE = 100

# Unités cibles pour l'ERP (standardisation)
# Note: ErpProducts utilise Weight en kg ; Height/Width/Depth stockés en cm ici (comme le script).
TARGET_WEIGHT_UNIT = "kg"
TARGET_DIM_UNIT = "cm"


# ──────────────────────────────────────────────
# CONVERSION D'UNITÉS
# ──────────────────────────────────────────────

class UnitConverter:
    """Convertit les unités de mesure vers les standards ERP"""

    WEIGHT_FACTORS = {
        "kg": 1.0,
        "kilogram": 1.0,
        "g": 0.001,
        "gram": 0.001,
        "gr": 0.001,
        "lb": 0.453592,
        "lbs": 0.453592,
        "pound": 0.453592,
        "oz": 0.0283495,
        "ounce": 0.0283495,
    }

    DIM_FACTORS = {
        "cm": 1.0,
        "centimeter": 1.0,
        "centimetre": 1.0,
        "mm": 0.1,
        "millimeter": 0.1,
        "millimetre": 0.1,
        "m": 100.0,
        "meter": 100.0,
        "metre": 100.0,
        "in": 2.54,
        "inch": 2.54,
        "inches": 2.54,
        "ft": 30.48,
        "foot": 30.48,
        "feet": 30.48,
    }

    @classmethod
    def parse_value_unit(cls, raw_value, raw_unit: str = None) -> Tuple[Optional[float], Optional[str]]:
        """
        Extrait la valeur numérique et l'unité d'une chaîne brute.
        Ex: "12.5 kg" → (12.5, "kg")
            "3.9 IN" → (3.9, "in")
            15.2 → (15.2, None)
        """
        if raw_value is None:
            return None, None

        # Si c'est déjà un nombre
        if isinstance(raw_value, (int, float)):
            return float(raw_value), raw_unit

        # Si c'est une chaîne
        val_str = str(raw_value).strip()
        if not val_str:
            return None, None

        # Cherche un nombre au début (entier ou décimal)
        match = re.match(r"^([0-9]*\.?[0-9]+)\s*(.*)$", val_str.replace(",", "."))
        if match:
            value = float(match.group(1))
            unit = match.group(2).strip().lower() if match.group(2) else raw_unit
            return value, unit

        return None, None

    @classmethod
    def convert_weight(cls, value, from_unit: str = None) -> Optional[float]:
        """Convertit un poids en kg"""
        val, unit = cls.parse_value_unit(value, from_unit)
        if val is None:
            return None
        unit = (unit or "kg").lower().strip()
        factor = cls.WEIGHT_FACTORS.get(unit, 1.0)
        return round(val * factor, 4)

    @classmethod
    def convert_dimension(cls, value, from_unit: str = None) -> Optional[float]:
        """Convertit une dimension en cm"""
        val, unit = cls.parse_value_unit(value, from_unit)
        if val is None:
            return None
        unit = (unit or "cm").lower().strip()
        factor = cls.DIM_FACTORS.get(unit, 1.0)
        return round(val * factor, 4)

    @classmethod
    def extract_dimensions_from_specs(cls, specs: List[Dict]) -> Dict:
        """
        Extrait les dimensions depuis une liste de spécifications techniques.
        Format attendu: [{"name": "Height", "value": "12.5", "unit": "cm"}, ...]
        """
        result = {"weight": None, "height": None, "width": None, "depth": None, "length": None}

        if not specs:
            return result

        # Mapping des noms de champs possibles
        field_map = {
            "weight": ["weight", "poids", "gewicht", "masse", "mass"],
            "height": ["height", "hauteur", "hohe", "höhe"],
            "width": ["width", "largeur", "breite", "weite"],
            "depth": ["depth", "profondeur", "tiefe"],
            "length": ["length", "longueur", "länge", "lange", "laenge"],
        }

        for spec in specs:
            name = str(spec.get("name", "")).lower().strip()
            value = spec.get("value")
            unit = spec.get("unit", "")

            for target_field, possible_names in field_map.items():
                if any(pn in name for pn in possible_names):
                    if target_field == "weight":
                        result["weight"] = cls.convert_weight(value, unit)
                    else:
                        result[target_field] = cls.convert_dimension(value, unit)
                    break

        # Si on a length mais pas depth, on considère length = depth
        if result["depth"] is None and result["length"] is not None:
            result["depth"] = result["length"]

        return result


# ──────────────────────────────────────────────
# BASE DE DONNÉES
# ──────────────────────────────────────────────

class Database:
    def __init__(self):
        self.conn = None

    def connect(self):
        try:
            self.conn = mysql.connector.connect(**DB_CONFIG)
            print(f"✅ Connecté à {DB_CONFIG['database']}")
        except Error as e:
            print(f"❌ Erreur connexion DB: {e}")
            raise

    def close(self):
        if self.conn and self.conn.is_connected():
            self.conn.close()

    def execute(self, query: str, params: tuple = ()):
        cursor = self.conn.cursor(dictionary=True)
        cursor.execute(query, params)
        self.conn.commit()
        return cursor

    def fetchone(self, query: str, params: tuple = ()):
        cursor = self.conn.cursor(dictionary=True)
        cursor.execute(query, params)
        return cursor.fetchone()

    def fetchall(self, query: str, params: tuple = ()):
        cursor = self.conn.cursor(dictionary=True)
        cursor.execute(query, params)
        return cursor.fetchall()


# ──────────────────────────────────────────────
# API CLIENTS
# ──────────────────────────────────────────────

class AutoPartsCatalogAPI:
    """Client pour Auto Parts Catalog API (RapidAPI) — endpoints path-param réels."""

    def __init__(self):
        self.base_url = API_CONFIG["base_url"].rstrip("/")
        self.headers = API_CONFIG["headers"]
        self.lang_id = API_CONFIG["lang_id"]
        self.country_id = API_CONFIG["country_filter_id"]
        self.type_id = API_CONFIG["type_id"]

    def _get(self, endpoint: str, params: dict = None):
        path = endpoint if endpoint.startswith("/") else f"/{endpoint}"
        url = f"{self.base_url}{path}"
        resp = requests.get(url, headers=self.headers, params=params, timeout=45)
        resp.raise_for_status()
        return resp.json()

    def get_language(self, lang_id: int = None):
        """Note: /languages/list n'existe PAS sur l'API live (doc RapidAPI obsolète)."""
        lid = lang_id or self.lang_id
        return self._get(f"/languages/get-language/lang-id/{lid}")

    def list_countries(self, lang_id: int = None):
        lid = lang_id or self.lang_id
        return self._get(f"/countries/list-countries-by-lang-id/{lid}")

    def list_vehicle_type_catalog(self):
        return self._get("/types/list-vehicles-type")

    def get_manufacturers(self, type_id: int = None) -> List[Dict]:
        """Marques véhicules (BMW, Audi…) — path live vérifié."""
        tid = type_id or self.type_id
        data = self._get(f"/manufacturers/list/type-id/{tid}")
        if isinstance(data, list):
            return data
        return data.get("manufacturers") or data.get("data") or []

    def get_models(self, manufacturer_id: int) -> List[Dict]:
        data = self._get(
            f"/models/list/type-id/{self.type_id}/manufacturer-id/{manufacturer_id}/"
            f"lang-id/{self.lang_id}/country-filter-id/{self.country_id}"
        )
        if isinstance(data, list):
            return data
        return data.get("models") or data.get("data") or []

    def get_vehicle_types(self, model_id: int, manufacturer_id: int) -> List[Dict]:
        data = self._get(
            f"/types/list-vehicles-types/{model_id}/manufacturer-id/{manufacturer_id}/"
            f"lang-id/{self.lang_id}/country-filter-id/{self.country_id}/type-id/{self.type_id}"
        )
        if isinstance(data, list):
            return data
        return data.get("data") or []

    def get_categories(self, vehicle_id: int, manufacturer_id: int) -> List[Dict]:
        data = self._get(
            f"/category/category-products-groups-variant-1/{vehicle_id}/"
            f"manufacturer-id/{manufacturer_id}/lang-id/{self.lang_id}/"
            f"country-filter-id/{self.country_id}/type-id/{self.type_id}"
        )
        if isinstance(data, list):
            return data
        return data.get("data") or []

    def get_articles_by_vehicle(
        self, vehicle_id: int, product_group_id: int, manufacturer_id: int, page: int = 1
    ) -> List[Dict]:
        data = self._get(
            f"/articles/list/vehicle-id/{vehicle_id}/product-group-id/{product_group_id}/"
            f"manufacturer-id/{manufacturer_id}/lang-id/{self.lang_id}/"
            f"country-filter-id/{self.country_id}/type-id/{self.type_id}"
        )
        if isinstance(data, list):
            return data
        return data.get("articles") or data.get("data") or []

    def get_article_details(self, article_id) -> Dict:
        """Détails + specs (Length/Width/Height mm). Endpoint live vérifié."""
        return self._get(f"/articles/details/article-id/{article_id}/lang-id/{self.lang_id}")

    def get_article_specifications(self, article_id) -> List[Dict]:
        """Specs techniques (poids, dimensions…)."""
        try:
            data = self._get(
                f"/articles/selection-of-all-specifications-criterias-for-the-article/"
                f"article-id/{article_id}/lang-id/{self.lang_id}/country-filter-id/{self.country_id}"
            )
            if isinstance(data, list):
                return data
            details = self.get_article_details(article_id)
            return details.get("articleAllSpecifications") or []
        except requests.HTTPError as e:
            if e.response is not None and e.response.status_code == 404:
                return []
            raise

    def search_by_oem(self, oem_number: str) -> List[Dict]:
        """Recherche OEM — chemin à valider selon playground OEM Identifier."""
        data = self._get(
            f"/artlookup/search-for-analogue-of-spare-parts-by-oem-number/"
            f"article-oem-no/{oem_number}"
        )
        if isinstance(data, list):
            return data
        return data.get("data") or data.get("articles") or []


class TecDocAPI:
    """Client pour TecDoc Web Service (licence requise)"""

    def __init__(self):
        self.base_url = TECDOC_CONFIG["base_url"]
        self.api_key = TECDOC_CONFIG["api_key"]
        self.provider_id = TECDOC_CONFIG["provider_id"]

    def _post(self, endpoint: str, payload: dict):
        url = f"{self.base_url}{endpoint}"
        headers = {"Content-Type": "application/json", "X-Api-Key": self.api_key}
        resp = requests.post(url, json=payload, headers=headers, timeout=30)
        resp.raise_for_status()
        return resp.json()

    def get_articles(self, search_term: str) -> List[Dict]:
        payload = {
            "providerId": self.provider_id,
            "lang": "fr",
            "searchType": 0,
            "searchQuery": search_term,
            "perPage": BATCH_SIZE,
            "page": 1,
            "includeImages": True,
            "includeOEMNumbers": True,
            "includeGenericArticles": True,
            "includeTechData": True,  # ← IMPORTANT: inclure les données techniques
        }
        return self._post("/articles", payload)


# ──────────────────────────────────────────────
# SYNCHRONISATION
# ──────────────────────────────────────────────

class ProductSync:
    def __init__(self, db: Database, api):
        self.db = db
        self.api = api
        self.now = datetime.now()
        self.sync_stats = {
            "created": 0, "updated": 0, "images": 0,
            "vehicles": 0, "errors": 0, "skipped": 0
        }

    # ── Brands ──
    def get_or_create_brand(self, brand_name: str) -> Optional[int]:
        """Récupère ou crée une marque dans erpbrands"""
        if not brand_name:
            return None

        slug = re.sub(r"[^a-z0-9-]", "-", brand_name.lower()).strip("-")
        existing = self.db.fetchone(
            "SELECT Id FROM ErpBrands WHERE Name = %s", (brand_name,)
        )
        if existing:
            return existing["Id"]

        brand_id = self.db.execute(
            """INSERT INTO ErpBrands (Name, Slug, IsActive, CreatedAt)
               VALUES (%s, %s, 1, %s)""",
            (brand_name, slug, self.now)
        ).lastrowid
        self.db.conn.commit()
        return brand_id

    # ── Categories ──
    def get_or_create_category(self, category_name: str, level: str = "Type") -> Optional[int]:
        """Récupère ou crée une catégorie dans erpcategories"""
        if not category_name:
            return None

        slug = re.sub(r"[^a-z0-9-]", "-", category_name.lower()).strip("-")
        existing = self.db.fetchone(
            "SELECT Id FROM ErpCategories WHERE Level = %s AND NameFr = %s",
            (level, category_name)
        )
        if existing:
            return existing["Id"]

        ext_id = f"AUTO_{level.upper()}_{slug[:20]}_{uuid.uuid4().hex[:6]}"
        cat_id = self.db.execute(
            """INSERT INTO ErpCategories 
               (ErpExternalId, Level, NameNl, NameFr, NameEn, SlugNl, SlugFr, SlugEn, SortOrder, IsActive, CreatedAt)
               VALUES (%s, %s, %s, %s, %s, %s, %s, %s, 0, 1, %s)""",
            (ext_id, level, category_name, category_name, category_name,
             slug, slug, slug, self.now)
        ).lastrowid
        self.db.conn.commit()
        return cat_id

    # ── Dimensions extraction ──
    def extract_dimensions(self, api_data: Dict) -> Dict:
        """
        Extrait et normalise les dimensions depuis les données API.
        Priorité:
        1. Spécifications techniques détaillées (/specifications)
        2. Champs directs dans l'objet article
        3. Valeurs par défaut (None)
        """
        result = {"weight": None, "height": None, "width": None, "depth": None}

        # 1. Essayer les specs techniques
        specs = api_data.get("specifications", api_data.get("technicalData", []))
        if specs:
            spec_dims = UnitConverter.extract_dimensions_from_specs(specs)
            result.update({k: v for k, v in spec_dims.items() if v is not None})

        # 2. Essayer les champs directs (fallback)
        direct_fields = {
            "weight": ["weight", "Weight", "gewicht", "poids", "mass"],
            "height": ["height", "Height", "hauteur", "hohe", "höhe"],
            "width": ["width", "Width", "largeur", "breite", "weite"],
            "depth": ["depth", "Depth", "profondeur", "tiefe", "length", "Length", "longueur"],
        }

        for target, possible_keys in direct_fields.items():
            if result[target] is None:
                for key in possible_keys:
                    if key in api_data and api_data[key] is not None:
                        # Détecte l'unité si présente dans un champ séparé
                        unit_key = f"{key}Unit"
                        unit = api_data.get(unit_key, api_data.get("unit", ""))
                        if target == "weight":
                            result[target] = UnitConverter.convert_weight(api_data[key], unit)
                        else:
                            result[target] = UnitConverter.convert_dimension(api_data[key], unit)
                        break

        # 3. Essayer l'objet Dimensions imbriqué (style CatalogRack)
        dims_obj = api_data.get("Dimensions", api_data.get("dimensions", {}))
        if isinstance(dims_obj, dict):
            dim_unit = dims_obj.get("DimUOM", dims_obj.get("dimUOM", ""))
            weight_unit = dims_obj.get("Weightuom", dims_obj.get("weightUOM", ""))

            if result["height"] is None and "Height" in dims_obj:
                result["height"] = UnitConverter.convert_dimension(dims_obj["Height"], dim_unit)
            if result["width"] is None and "Width" in dims_obj:
                result["width"] = UnitConverter.convert_dimension(dims_obj["Width"], dim_unit)
            if result["depth"] is None and "Depth" in dims_obj:
                result["depth"] = UnitConverter.convert_dimension(dims_obj["Depth"], dim_unit)
            if result["weight"] is None and "Weight" in dims_obj:
                result["weight"] = UnitConverter.convert_weight(dims_obj["Weight"], weight_unit)

        return result

    # ── Product mapping ──
    def map_api_to_product(self, api_data: Dict) -> Dict:
        """Mappe les données API vers la structure erpproducts avec dimensions normalisées"""

        # Extraction des dimensions
        dims = self.extract_dimensions(api_data)

        return {
            "ErpProductId": str(api_data.get("articleId", api_data.get("id", ""))),
            "Name": api_data.get("articleName", api_data.get("name", "")),
            "Name2": api_data.get("articleName2", ""),
            "Reference": api_data.get("articleNumber", api_data.get("reference", "")),
            "Ean": api_data.get("eanNumber", api_data.get("ean", "")),
            "Brand": api_data.get("brandName", ""),
            "Manufacturer": api_data.get("manufacturerName", api_data.get("brandName", "")),
            "Model": "",
            "Comment": str(api_data.get("description", ""))[:2048],
            "Link": api_data.get("productUrl", ""),
            "PicName": None,
            "PriceHT": api_data.get("price", 0),
            "UnitPrice": api_data.get("unitPrice", 0),
            "CPrice": api_data.get("costPrice", 0),
            "RPrice": api_data.get("retailPrice", 0),
            "VatIncluded": 0,
            "TypeVatPerc": 20.0,
            "DiscountPerc": 0,
            "DiscountPrice": None,
            "ProductDiscountPerc": 0,
            "TypeDiscountPerc": 0,
            "PromoActive": 0,
            "PromoPrice": None,
            "PromoStartDate": None,
            "PromoEndDate": None,
            "StockQuantity": api_data.get("stock", 0),
            "StockDate": self.now,
            "Quantity": 1,
            "PerUnit": api_data.get("unit", "piece"),
            "PieceID": None,
            "Weight": dims["weight"],
            "Height": dims["height"],
            "Width": dims["width"],
            "Depth": dims["depth"],
            "MainTypeID": str(api_data.get("genericArticleId", "")),
            "MainTypeName": api_data.get("genericArticleName", ""),
            "MainSubTypeID": None,
            "MainSubTypeName": None,
            "TypeID": str(api_data.get("assemblyGroupNodeId", "")),
            "TypeName": api_data.get("assemblyGroupName", ""),
            "SubTypeID": None,
            "SubTypeName": None,
            "SubProductID": None,
            "Label": api_data.get("label", ""),
            "ColorCode": None,
            "Archived": 0,
            "CreatedAt": self.now,
            "UpdatedAt": self.now,
            "LastSyncAt": self.now,
            "DataSource": DATA_SOURCE,
            "FromExcel": 0,
            "SourceFile": None,
        }

    def upsert_product(self, product_data: Dict, brand_id: int = None, category_id: int = None) -> int:
        """Crée ou met à jour un produit"""
        erp_id = product_data["ErpProductId"]

        if not erp_id:
            print("⚠️ ErpProductId vide, article ignoré")
            self.sync_stats["skipped"] += 1
            return None

        existing = self.db.fetchone(
            "SELECT Id FROM ErpProducts WHERE ErpProductId = %s", (erp_id,)
        )

        if existing:
            product_id = existing["Id"]
            self.db.execute(
                """UPDATE ErpProducts SET
                    Name = %s, Name2 = %s, Reference = %s, Ean = %s,
                    Brand = %s, Manufacturer = %s, Comment = %s, Link = %s,
                    PriceHT = %s, UnitPrice = %s, CPrice = %s, RPrice = %s,
                    StockQuantity = %s, StockDate = %s, Weight = %s,
                    Height = %s, Width = %s, Depth = %s,
                    MainTypeID = %s, MainTypeName = %s, TypeID = %s, TypeName = %s,
                    Label = %s, UpdatedAt = %s, LastSyncAt = %s,
                    BrandId = %s, CategoryId = %s
                   WHERE Id = %s""",
                (
                    product_data["Name"], product_data["Name2"], product_data["Reference"],
                    product_data["Ean"], product_data["Brand"], product_data["Manufacturer"],
                    product_data["Comment"], product_data["Link"],
                    product_data["PriceHT"], product_data["UnitPrice"],
                    product_data["CPrice"], product_data["RPrice"],
                    product_data["StockQuantity"], product_data["StockDate"],
                    product_data["Weight"], product_data["Height"],
                    product_data["Width"], product_data["Depth"],
                    product_data["MainTypeID"], product_data["MainTypeName"],
                    product_data["TypeID"], product_data["TypeName"],
                    product_data["Label"], self.now, self.now,
                    brand_id, category_id, product_id
                )
            )
            self.sync_stats["updated"] += 1
        else:
            product_id = self.db.execute(
                """INSERT INTO ErpProducts (
                    ErpProductId, Name, Name2, Reference, Ean, Brand, Manufacturer,
                    Model, Comment, Link, PicName, PriceHT, UnitPrice, CPrice, RPrice,
                    VatIncluded, TypeVatPerc, DiscountPerc, DiscountPrice,
                    ProductDiscountPerc, TypeDiscountPerc, PromoActive, PromoPrice,
                    PromoStartDate, PromoEndDate, StockQuantity, StockDate, Quantity,
                    PerUnit, PieceID, Weight, Height, Width, Depth,
                    MainTypeID, MainTypeName, MainSubTypeID, MainSubTypeName,
                    TypeID, TypeName, SubTypeID, SubTypeName, SubProductID,
                    Label, ColorCode, Archived, CreatedAt, UpdatedAt, LastSyncAt,
                    DataSource, FromExcel, SourceFile, BrandId, CategoryId
                ) VALUES (
                    %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                    %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                    %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                    %s, %s, %s, %s, %s, %s, %s, %s, %s
                )""",
                (
                    product_data["ErpProductId"], product_data["Name"], product_data["Name2"],
                    product_data["Reference"], product_data["Ean"], product_data["Brand"],
                    product_data["Manufacturer"], product_data["Model"], product_data["Comment"],
                    product_data["Link"], product_data["PicName"], product_data["PriceHT"],
                    product_data["UnitPrice"], product_data["CPrice"], product_data["RPrice"],
                    product_data["VatIncluded"], product_data["TypeVatPerc"],
                    product_data["DiscountPerc"], product_data["DiscountPrice"],
                    product_data["ProductDiscountPerc"], product_data["TypeDiscountPerc"],
                    product_data["PromoActive"], product_data["PromoPrice"],
                    product_data["PromoStartDate"], product_data["PromoEndDate"],
                    product_data["StockQuantity"], product_data["StockDate"],
                    product_data["Quantity"], product_data["PerUnit"], product_data["PieceID"],
                    product_data["Weight"], product_data["Height"], product_data["Width"],
                    product_data["Depth"], product_data["MainTypeID"], product_data["MainTypeName"],
                    product_data["MainSubTypeID"], product_data["MainSubTypeName"],
                    product_data["TypeID"], product_data["TypeName"], product_data["SubTypeID"],
                    product_data["SubTypeName"], product_data["SubProductID"],
                    product_data["Label"], product_data["ColorCode"], product_data["Archived"],
                    product_data["CreatedAt"], product_data["UpdatedAt"], product_data["LastSyncAt"],
                    product_data["DataSource"], product_data["FromExcel"],
                    product_data["SourceFile"], brand_id, category_id
                )
            ).lastrowid
            self.sync_stats["created"] += 1

        return product_id

    # ── Images ──
    def sync_images(self, product_id: int, images: List[Dict]):
        """Synchronise les images d'un produit"""
        if not images:
            return

        self.db.execute(
            "DELETE FROM ErpProductImages WHERE ProductId = %s",
            (product_id,)
        )

        for idx, img in enumerate(images):
            img_url = img.get("url", img.get("imageUrl", img.get("imageUrl100", "")))
            if not img_url:
                continue

            self.db.execute(
                """INSERT INTO ErpProductImages
                   (Id, ProductId, Url, AltText, IsMain, SortOrder, CreatedAt)
                   VALUES (%s, %s, %s, %s, %s, %s, %s)""",
                (
                    str(uuid.uuid4()), product_id, img_url,
                    img.get("altText", ""), 1 if idx == 0 else 0,
                    idx, self.now
                )
            )
            self.sync_stats["images"] += 1

    # ── Vehicle Compatibility ──
    def sync_vehicle_compatibility(self, product_id: int, vehicles: List[Dict]):
        """Synchronise la compatibilité véhicule"""
        if not vehicles:
            return

        self.db.execute(
            "DELETE FROM ErpProductVehicles WHERE ProductId = %s",
            (product_id,)
        )

        for v in vehicles:
            self.db.execute(
                """INSERT INTO ErpProductVehicles
                   (Id, ProductId, Make, Model, YearFrom, YearTo, EngineCode, KType, BodyType, FuelType, CreatedAt)
                   VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)""",
                (
                    str(uuid.uuid4()), product_id,
                    v.get("make", ""), v.get("model", ""),
                    v.get("yearFrom"), v.get("yearTo"),
                    v.get("engineCode", ""), v.get("kType", ""),
                    v.get("bodyType", ""), v.get("fuelType", ""),
                    self.now
                )
            )
            self.sync_stats["vehicles"] += 1

    # ── OEM Cross-references ──
    def sync_oem_crossrefs(self, product_id: int, oem_numbers: List[Dict]):
        """Synchronise les cross-références OEM"""
        if not oem_numbers:
            return

        self.db.execute(
            "DELETE FROM ErpOemCrossReferences WHERE ProductId = %s",
            (product_id,)
        )

        for oem in oem_numbers:
            number = oem.get("oemNumber", oem.get("number", ""))
            if not number:
                continue

            self.db.execute(
                """INSERT INTO ErpOemCrossReferences
                   (Id, ProductId, OemNumber, Brand, IsOriginal, CreatedAt)
                   VALUES (%s, %s, %s, %s, %s, %s)""",
                (
                    str(uuid.uuid4()), product_id, number,
                    oem.get("brand", ""), 1 if oem.get("isOriginal", False) else 0,
                    self.now
                )
            )

    # ── Main Sync ──
    def sync_article(self, api_data: Dict):
        """Synchronise un article complet (produit + images + compatibilité + OEM)"""
        try:
            brand_id = self.get_or_create_brand(api_data.get("brandName", ""))
            category_id = self.get_or_create_category(
                api_data.get("genericArticleName", api_data.get("category", "")),
                level="Type"
            )

            product_data = self.map_api_to_product(api_data)
            product_id = self.upsert_product(product_data, brand_id, category_id)

            if not product_id:
                return None

            # Images
            images = api_data.get("images", api_data.get("articleImages", []))
            self.sync_images(product_id, images)

            # Compatibilité véhicule
            vehicles = api_data.get("vehicleModels", api_data.get("linkages", []))
            self.sync_vehicle_compatibility(product_id, vehicles)

            # Cross-références OEM
            oem_numbers = api_data.get("oemNumbers", api_data.get("oeNumbers", []))
            self.sync_oem_crossrefs(product_id, oem_numbers)

            # Log des dimensions trouvées
            dims = self.extract_dimensions(api_data)
            if any(v is not None for v in dims.values()):
                print(f"   📏 {product_data['Reference']}: W={dims['weight']}kg, "
                      f"H={dims['height']}cm, W={dims['width']}cm, D={dims['depth']}cm")

            return product_id

        except Exception as e:
            print(f"❌ Erreur sync article {api_data.get('articleId', '?')}: {e}")
            self.sync_stats["errors"] += 1
            return None

    def print_stats(self):
        print("\n📊 STATISTIQUES DE SYNCHRONISATION")
        print(f"   Produits créés:      {self.sync_stats['created']}")
        print(f"   Produits mis à jour: {self.sync_stats['updated']}")
        print(f"   Ignorés:             {self.sync_stats['skipped']}")
        print(f"   Images ajoutées:     {self.sync_stats['images']}")
        print(f"   Compatibilités:      {self.sync_stats['vehicles']}")
        print(f"   Erreurs:             {self.sync_stats['errors']}")


# ──────────────────────────────────────────────
# WORKFLOWS
# ──────────────────────────────────────────────

def sync_by_oem(db: Database, oem_numbers: List[str]):
    """Synchronise des pièces par numéro OEM"""
    api = AutoPartsCatalogAPI()
    sync = ProductSync(db, api)

    for oem in oem_numbers:
        print(f"🔍 Recherche OEM: {oem}")
        results = api.search_by_oem(oem)
        for article in results:
            details = api.get_article_details(article["articleId"])
            # Récupère aussi les specs techniques pour les dimensions
            try:
                specs = api.get_article_specifications(article["articleId"])
                details["specifications"] = specs
            except Exception:
                pass
            sync.sync_article(details)

    sync.print_stats()


def sync_by_vehicle(db: Database, car_id: int, max_pages: int = 5):
    """Synchronise toutes les pièces compatibles avec un véhicule"""
    api = AutoPartsCatalogAPI()
    sync = ProductSync(db, api)

    for page in range(1, max_pages + 1):
        print(f"📄 Page {page}/{max_pages}")
        articles = api.get_articles_by_vehicle(car_id, page)
        if not articles:
            break
        for article in articles:
            # Enrichit avec les specs si l'API le permet
            try:
                specs = api.get_article_specifications(article["articleId"])
                article["specifications"] = specs
            except Exception:
                pass
            sync.sync_article(article)

    sync.print_stats()


def full_catalog_sync(db: Database, max_items: int = 10000):
    """Synchronisation complète du catalogue (à adapter selon l'API)"""
    api = AutoPartsCatalogAPI()
    sync = ProductSync(db, api)

    manufacturers = api.get_manufacturers()
    print(f"🏭 {len(manufacturers)} marques trouvées")

    for mfg in manufacturers[:10]:
        print(f"📦 Sync marque: {mfg.get('name', '?')}")
        # Adapter selon les endpoints disponibles

    sync.print_stats()


# ──────────────────────────────────────────────
# MAIN
# ──────────────────────────────────────────────

if __name__ == "__main__":
    db = Database()
    db.connect()

    try:
        # Exemple 1: Sync par numéro OEM
        # sync_by_oem(db, ["0281002937", "0986280411"])

        # Exemple 2: Sync par véhicule
        # sync_by_vehicle(db, car_id=12345, max_pages=3)

        # Exemple 3: Sync complète (limitée)
        full_catalog_sync(db, max_items=1000)

    finally:
        db.close()
        print("🔒 Connexion fermée")

