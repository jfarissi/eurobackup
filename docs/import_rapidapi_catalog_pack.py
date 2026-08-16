#!/usr/bin/env python3
"""
Import pack CSV catalogue RapidApi chez un client (0 appel RapidAPI).

Accepte un dossier pack ou un .zip produit par export_rapidapi_catalog_pack.py.

Usage :
  cd d:\\GitHub\\Backup.Web.Api\\docs
  $env:PACK = "d:\\GitHub\\Backup.Web.Api\\docs\\catalog_packs\\rapidapi_....zip"
  $env:DB_HOST = "localhost"
  $env:DB_NAME = "backupcontent"
  python import_rapidapi_catalog_pack.py

Mode (défaut insert_only) :
  - ErpProductId déjà en base → ignoré (pas d'UPDATE, pas d'enfants)
  - ErpProductId absent → INSERT produit + véhicules/images/OEM

  $env:IMPORT_MODE = "upsert"   # ancien comportement (UPDATE si existe)
"""

from __future__ import annotations

import csv
import json
import os
import shutil
import tempfile
import zipfile
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

import mysql.connector

DB = {
    "host": os.getenv("DB_HOST", "localhost"),
    "port": int(os.getenv("DB_PORT", "3306")),
    "user": os.getenv("DB_USER", "root"),
    "password": os.getenv("DB_PASSWORD", "tata"),
    "database": os.getenv("DB_NAME", "backupcontent"),
    "charset": "utf8mb4",
}

PACK = os.getenv("PACK", "").strip()
DATA_SOURCE = os.getenv("DATA_SOURCE", "RapidApi")
# insert_only (défaut) : ErpProductId déjà en base → ignoré (pas d'UPDATE)
# upsert : comportement historique (UPDATE si existe)
IMPORT_MODE = os.getenv("IMPORT_MODE", "insert_only").strip().lower()
INSERT_ONLY = IMPORT_MODE in ("insert_only", "skip_existing", "new_only", "insert")


def open_pack(pack_path: Path) -> Tuple[Path, Optional[Path]]:
    """Retourne (dossier_pack, tmp_dir_à_nettoyer|None)."""
    if pack_path.is_dir():
        return pack_path, None
    if pack_path.suffix.lower() == ".zip":
        tmp = Path(tempfile.mkdtemp(prefix="rapidapi_pack_"))
        with zipfile.ZipFile(pack_path, "r") as zf:
            zf.extractall(tmp)
        return tmp, tmp
    raise SystemExit(f"PACK invalide (dossier ou .zip attendu): {pack_path}")


def read_csv(path: Path) -> Tuple[List[str], List[Dict[str, str]]]:
    if not path.exists():
        return [], []
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        r = csv.DictReader(f)
        cols = list(r.fieldnames or [])
        rows = [{k: (v if v != "" else None) for k, v in row.items()} for row in r]
    return cols, rows


def table_columns(cur, table: str) -> List[str]:
    cur.execute(f"SHOW COLUMNS FROM `{table}`")
    return [r[0] for r in cur.fetchall()]


def parse_value(col: str, raw: Optional[str], col_type_hint: str = "") -> Any:
    if raw is None or raw == "":
        return None
    # Identifiants / refs toujours string (évite OemNumber="1058458" → int)
    if col.lower() in (
        "oemnumber", "reference", "ean", "erpproductid", "erpexternalid",
        "vin", "url", "alttext", "brand", "manufacturer", "slug", "id",
    ):
        return raw
    # bools TINYINT
    if raw in ("0", "1") and col.lower() in (
        "isactive", "fromexcel", "archived", "vatincluded", "promoactive", "ismain", "isoriginal"
    ):
        return int(raw)
    # dates
    if "date" in col.lower() or col.lower().endswith("at"):
        try:
            return datetime.fromisoformat(raw.replace("Z", ""))
        except ValueError:
            return raw
    # ints
    if raw.isdigit() or (raw.startswith("-") and raw[1:].isdigit()):
        try:
            return int(raw)
        except ValueError:
            return raw
    try:
        if "." in raw:
            return float(raw)
    except ValueError:
        pass
    return raw


def upsert_brand(cur, row: Dict[str, str], now: datetime) -> Optional[int]:
    name = (row.get("Name") or "").strip()
    if not name:
        return None
    cur.execute("SELECT Id FROM ErpBrands WHERE Name=%s LIMIT 1", (name,))
    existing = cur.fetchone()
    if existing:
        return existing[0]
    slug = (row.get("Slug") or name.lower().replace(" ", "-"))[:120]
    cur.execute(
        """INSERT INTO ErpBrands (Name, Slug, Description, IsActive, CreatedAt, UpdatedAt)
           VALUES (%s,%s,%s,%s,%s,%s)""",
        (
            name,
            slug,
            row.get("Description") or "Import pack RapidApi",
            int(row.get("IsActive") or 1),
            now,
            now,
        ),
    )
    return cur.lastrowid


def upsert_category(cur, row: Dict[str, str], now: datetime) -> Optional[int]:
    name = (row.get("NameFr") or row.get("NameEn") or row.get("NameNl") or "").strip()
    level = (row.get("Level") or "Type").strip()
    if not name:
        return None
    cur.execute(
        "SELECT Id FROM ErpCategories WHERE Level=%s AND NameFr=%s LIMIT 1",
        (level, name),
    )
    existing = cur.fetchone()
    if existing:
        return existing[0]
    slug = (row.get("SlugFr") or name.lower().replace(" ", "-"))[:80]
    ext = row.get("ErpExternalId") or f"PACK_{slug[:30]}_{cur.lastrowid or 0}"
    cur.execute(
        """INSERT INTO ErpCategories
           (ErpExternalId, Level, NameNl, NameFr, NameEn, SlugNl, SlugFr, SlugEn,
            SortOrder, IsActive, CreatedAt)
           VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)""",
        (
            ext,
            level,
            row.get("NameNl") or name,
            name,
            row.get("NameEn") or name,
            row.get("SlugNl") or slug,
            slug,
            row.get("SlugEn") or slug,
            int(row.get("SortOrder") or 0),
            int(row.get("IsActive") or 1),
            now,
        ),
    )
    return cur.lastrowid


def import_product(
    cur,
    row: Dict[str, str],
    brand_id: Optional[int],
    category_id: Optional[int],
    db_cols: List[str],
    now: datetime,
    insert_only: bool = True,
) -> Tuple[Optional[int], str]:
    """
    Importe un produit du pack.
    Retourne (product_id, action) avec action = inserted | skipped | updated.
    """
    erp_id = (row.get("ErpProductId") or "").strip()
    if not erp_id:
        return None, "skipped"

    # Colonnes à écrire (intersection CSV ∩ table, hors Id auto + aides)
    skip = {
        "Id", "_BrandName", "_CategoryNameFr", "_CategoryLevel",
        "BrandId", "CategoryId",
    }
    cur.execute("SELECT Id FROM ErpProducts WHERE ErpProductId=%s LIMIT 1", (erp_id,))
    existing = cur.fetchone()

    if existing and insert_only:
        return existing[0], "skipped"

    data: Dict[str, Any] = {}
    for k, v in row.items():
        if k in skip or k not in db_cols:
            continue
        data[k] = parse_value(k, v)

    data["BrandId"] = brand_id
    data["CategoryId"] = category_id
    data["DataSource"] = data.get("DataSource") or DATA_SOURCE
    data["UpdatedAt"] = now
    data["LastSyncAt"] = now
    if "FromExcel" in db_cols:
        data["FromExcel"] = 0

    if category_id and "TypeID" in db_cols:
        cur.execute(
            "SELECT ErpExternalId, NameFr, Level FROM ErpCategories WHERE Id=%s LIMIT 1",
            (category_id,),
        )
        crow = cur.fetchone()
        if crow:
            # tuple or dict depending on cursor
            if isinstance(crow, dict):
                ext, name_fr, level = crow.get("ErpExternalId"), crow.get("NameFr"), crow.get("Level")
            else:
                ext, name_fr, level = crow[0], crow[1], crow[2]
            if level == "Type" and ext:
                data["TypeID"] = ext
                if name_fr:
                    data["TypeName"] = name_fr
                    data["MainTypeName"] = name_fr
            elif level == "MainType" and ext:
                data["MainTypeID"] = ext
                if name_fr:
                    data["MainTypeName"] = name_fr

    if existing:
        pid = existing[0]
        sets = ", ".join(f"`{k}`=%s" for k in data.keys())
        cur.execute(
            f"UPDATE ErpProducts SET {sets} WHERE Id=%s",
            list(data.values()) + [pid],
        )
        return pid, "updated"

    data["CreatedAt"] = data.get("CreatedAt") or now
    # Id auto-increment : ne pas forcer l'Id exporté
    cols = list(data.keys())
    placeholders = ", ".join(["%s"] * len(cols))
    col_sql = ", ".join(f"`{c}`" for c in cols)
    cur.execute(
        f"INSERT INTO ErpProducts ({col_sql}) VALUES ({placeholders})",
        [data[c] for c in cols],
    )
    return cur.lastrowid, "inserted"


def replace_children(
    cur,
    table: str,
    product_id: int,
    rows_for_product: List[Dict[str, str]],
    db_cols: List[str],
    now: datetime,
) -> int:
    cur.execute(f"DELETE FROM `{table}` WHERE ProductId=%s", (product_id,))
    n = 0
    skip = {"ErpProductId"}
    # Colonnes NOT NULL sans DEFAULT utilisable si on envoie explicitement NULL
    not_null_empty = {
        "ErpProductImages": {"AltText", "Url"},
        "ErpOemCrossReferences": {"OemNumber"},
    }.get(table, set())
    for row in rows_for_product:
        data: Dict[str, Any] = {"ProductId": product_id}
        for k, v in row.items():
            if k in skip or k == "ProductId" or k not in db_cols:
                continue
            if k == "Id" and not v:
                continue
            data[k] = parse_value(k, v)
        for col in not_null_empty:
            if col in db_cols and data.get(col) is None:
                data[col] = ""
        if table == "ErpProductImages":
            url = data.get("Url")
            if url is None or not str(url).strip():
                continue
            data["Url"] = str(url)
            data["AltText"] = "" if data.get("AltText") is None else str(data.get("AltText"))
        if table == "ErpOemCrossReferences":
            oem = data.get("OemNumber")
            if oem is None or not str(oem).strip():
                continue
            data["OemNumber"] = str(oem)[:128]
            if data.get("Brand") is not None:
                data["Brand"] = str(data["Brand"])[:128]
        if "Id" in db_cols and not data.get("Id"):
            import uuid
            data["Id"] = str(uuid.uuid4())
        if "CreatedAt" in db_cols and not data.get("CreatedAt"):
            data["CreatedAt"] = now
        cols = [c for c in data.keys() if c in db_cols]
        placeholders = ", ".join(["%s"] * len(cols))
        col_sql = ", ".join(f"`{c}`" for c in cols)
        cur.execute(
            f"INSERT INTO `{table}` ({col_sql}) VALUES ({placeholders})",
            [data[c] for c in cols],
        )
        n += 1
    return n


def import_vin_cache(
    cur, rows: List[Dict[str, str]], db_cols: List[str], now: datetime, insert_only: bool = True
) -> Tuple[int, int]:
    """Retourne (traités, insérés). En insert_only, VIN existant → ignoré."""
    if not rows or not db_cols:
        return 0, 0
    n = 0
    inserted = 0
    import uuid
    for row in rows:
        vin = (row.get("Vin") or "").strip().upper()
        if len(vin) != 17:
            continue
        cur.execute("SELECT Id FROM ErpVinVehicles WHERE Vin=%s LIMIT 1", (vin,))
        existing = cur.fetchone()
        if existing and insert_only:
            continue
        data = {}
        for k, v in row.items():
            if k in ("Id",) or k not in db_cols:
                continue
            data[k] = parse_value(k, v)
        data["Vin"] = vin
        data["UpdatedAt"] = now
        if existing:
            sets = ", ".join(f"`{k}`=%s" for k in data.keys())
            cur.execute(
                f"UPDATE ErpVinVehicles SET {sets} WHERE Id=%s",
                list(data.values()) + [existing[0]],
            )
        else:
            data["Id"] = str(uuid.uuid4())
            data["CreatedAt"] = now
            cols = [c for c in data.keys() if c in db_cols]
            cur.execute(
                f"INSERT INTO ErpVinVehicles ({', '.join(f'`{c}`' for c in cols)}) "
                f"VALUES ({', '.join(['%s']*len(cols))})",
                [data[c] for c in cols],
            )
            inserted += 1
        n += 1
    return n, inserted


def main() -> int:
    if not PACK:
        raise SystemExit("Définir PACK=chemin/vers/pack.zip|dossier")

    pack_path = Path(PACK).resolve()
    root, tmp = open_pack(pack_path)
    try:
        manifest_path = root / "manifest.json"
        if manifest_path.exists():
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            print("Pack:", manifest.get("format"), "v" + str(manifest.get("version")),
                  "exporté", manifest.get("exportedAt"))
            print("Counts pack:", manifest.get("counts"))

        now = datetime.now()
        conn = mysql.connector.connect(**DB)
        cur = conn.cursor()
        print(f"DB cible {DB['database']}@{DB['host']}:{DB['port']}")
        print(f"IMPORT_MODE={IMPORT_MODE} (insert_only={INSERT_ONLY})")

        # Brands
        _, brand_rows = read_csv(root / "ErpBrands.csv")
        brand_by_name: Dict[str, int] = {}
        for row in brand_rows:
            bid = upsert_brand(cur, row, now)
            if bid and row.get("Name"):
                brand_by_name[row["Name"].strip()] = bid
        print(f"ErpBrands: {len(brand_by_name)}")

        # Categories
        _, cat_rows = read_csv(root / "ErpCategories.csv")
        cat_by_key: Dict[Tuple[str, str], int] = {}
        for row in cat_rows:
            cid = upsert_category(cur, row, now)
            name = (row.get("NameFr") or "").strip()
            level = (row.get("Level") or "Type").strip()
            if cid and name:
                cat_by_key[(level, name)] = cid
        print(f"ErpCategories: {len(cat_by_key)}")

        product_cols = table_columns(cur, "ErpProducts")
        _, product_rows = read_csv(root / "ErpProducts.csv")
        erp_to_pid: Dict[str, int] = {}
        new_erps: set[str] = set()
        stats = {"inserted": 0, "skipped": 0, "updated": 0}
        for row in product_rows:
            brand_name = (row.get("_BrandName") or row.get("Brand") or "").strip()
            cat_name = (row.get("_CategoryNameFr") or "").strip()
            cat_level = (row.get("_CategoryLevel") or "Type").strip()
            brand_id = brand_by_name.get(brand_name) if brand_name else None
            category_id = cat_by_key.get((cat_level, cat_name)) if cat_name else None
            pid, action = import_product(
                cur, row, brand_id, category_id, product_cols, now, insert_only=INSERT_ONLY)
            stats[action] = stats.get(action, 0) + 1
            erp = (row.get("ErpProductId") or "").strip()
            if pid and erp:
                erp_to_pid[erp] = pid
                if action == "inserted":
                    new_erps.add(erp)
        print(
            f"ErpProducts: {len(erp_to_pid)} mappés "
            f"(+{stats.get('inserted', 0)} nouveaux, "
            f"={stats.get('skipped', 0)} ignorés, "
            f"~{stats.get('updated', 0)} mis à jour)"
        )

        # Enfants : uniquement pour les produits nouvellement insérés (insert_only)
        child_erps = new_erps if INSERT_ONLY else set(erp_to_pid.keys())

        # Children grouped by ErpProductId
        for table in ("ErpProductVehicles", "ErpProductImages", "ErpOemCrossReferences"):
            path = root / f"{table}.csv"
            if not path.exists():
                print(f"{table}: skip")
                continue
            try:
                db_cols = table_columns(cur, table)
            except Exception:
                print(f"{table}: table absente — skip")
                continue
            _, rows = read_csv(path)
            by_erp: Dict[str, List[Dict[str, str]]] = {}
            for row in rows:
                erp = (row.get("ErpProductId") or "").strip()
                if not erp:
                    continue
                by_erp.setdefault(erp, []).append(row)
            total = 0
            for erp, group in by_erp.items():
                if erp not in child_erps:
                    continue
                pid = erp_to_pid.get(erp)
                if not pid:
                    continue
                total += replace_children(cur, table, pid, group, db_cols, now)
            print(f"{table}: {total}")

        vin_path = root / "ErpVinVehicles.csv"
        if vin_path.exists():
            try:
                vin_cols = table_columns(cur, "ErpVinVehicles")
                _, vin_rows = read_csv(vin_path)
                n, vin_ins = import_vin_cache(cur, vin_rows, vin_cols, now, insert_only=INSERT_ONLY)
                print(f"ErpVinVehicles: {n} traités, {vin_ins} nouveaux")
            except Exception as ex:
                print(f"ErpVinVehicles: skip ({ex})")

        conn.commit()
        cur.execute("SELECT COUNT(*) FROM ErpProducts WHERE DataSource=%s", (DATA_SOURCE,))
        print(f"Done. Produits {DATA_SOURCE} en base: {cur.fetchone()[0]}")
        cur.close()
        conn.close()
        return 0
    finally:
        if tmp and tmp.exists():
            shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
