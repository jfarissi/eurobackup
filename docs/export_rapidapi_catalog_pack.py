#!/usr/bin/env python3
"""
Export catalogue RapidApi (local MySQL) → pack CSV pour install client.
Aucun appel RapidAPI.

Sortie :
  docs/catalog_packs/rapidapi_morocco_YYYYMMDD/
    manifest.json
    ErpBrands.csv
    ErpCategories.csv
    ErpProducts.csv
    ErpProductVehicles.csv   (+ colonne ErpProductId)
    ErpProductImages.csv     (+ ErpProductId)
    ErpOemCrossReferences.csv (+ ErpProductId)
    ErpVinVehicles.csv       (optionnel, cache VIN)
  + archive .zip à côté

Usage :
  cd d:\\GitHub\\Backup.Web.Api\\docs
  python export_rapidapi_catalog_pack.py
  $env:OUT_DIR = "F:\\packs\\ma_v1"
  python export_rapidapi_catalog_pack.py
"""

from __future__ import annotations

import csv
import json
import os
import zipfile
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence, Tuple

import mysql.connector

DB = {
    "host": os.getenv("DB_HOST", "localhost"),
    "port": int(os.getenv("DB_PORT", "3306")),
    "user": os.getenv("DB_USER", "root"),
    "password": os.getenv("DB_PASSWORD", "tata"),
    "database": os.getenv("DB_NAME", "backupcontent"),
    "charset": "utf8mb4",
}

DATA_SOURCE = os.getenv("DATA_SOURCE", "RapidApi")
INCLUDE_VIN_CACHE = os.getenv("INCLUDE_VIN_CACHE", "1").strip() not in ("0", "false", "False")


def table_exists(cur, schema: str, table: str) -> bool:
    cur.execute(
        "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=%s AND table_name=%s",
        (schema, table),
    )
    return cur.fetchone()[0] > 0


def fetchall(cur, sql: str, params: Sequence[Any] = ()) -> Tuple[List[str], List[Tuple]]:
    cur.execute(sql, params)
    cols = [d[0] for d in cur.description] if cur.description else []
    return cols, list(cur.fetchall())


def write_csv(path: Path, cols: List[str], rows: List[Tuple]) -> int:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f, quoting=csv.QUOTE_MINIMAL)
        w.writerow(cols)
        for row in rows:
            out = []
            for v in row:
                if v is None:
                    out.append("")
                elif isinstance(v, datetime):
                    out.append(v.isoformat(sep=" ", timespec="seconds"))
                elif isinstance(v, (bytes, bytearray)):
                    out.append(v.decode("utf-8", errors="replace"))
                else:
                    out.append(v)
            w.writerow(out)
    return len(rows)


def export_child_with_erp_id(
    cur,
    table: str,
    product_ids: List[int],
    out_path: Path,
) -> int:
    if not product_ids:
        return write_csv(out_path, [], [])
    ph = ",".join(["%s"] * len(product_ids))
    # Jointure pour exporter ErpProductId (clé stable à l'import)
    cols, rows = fetchall(
        cur,
        f"""SELECT c.*, p.ErpProductId AS ErpProductId
            FROM `{table}` c
            INNER JOIN ErpProducts p ON p.Id = c.ProductId
            WHERE c.ProductId IN ({ph})""",
        product_ids,
    )
    return write_csv(out_path, cols, rows)


def main() -> int:
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    default_out = Path(__file__).resolve().parent / "catalog_packs" / f"rapidapi_{stamp}"
    out_dir = Path(os.getenv("OUT_DIR", str(default_out))).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    print(f"DB={DB['database']}@{DB['host']}:{DB['port']} DataSource={DATA_SOURCE}")
    print(f"OUT={out_dir}")

    conn = mysql.connector.connect(**DB)
    cur = conn.cursor()

    cur.execute(
        "SELECT Id FROM ErpProducts WHERE DataSource=%s ORDER BY Id",
        (DATA_SOURCE,),
    )
    product_ids = [r[0] for r in cur.fetchall()]
    print(f"Produits {DATA_SOURCE}: {len(product_ids)}")
    if not product_ids:
        print("Rien à exporter.")
        return 1

    ph = ",".join(["%s"] * len(product_ids))
    counts: Dict[str, int] = {"ErpProducts": len(product_ids)}

    # Brands / categories référencés
    cur.execute(
        f"SELECT DISTINCT BrandId FROM ErpProducts WHERE Id IN ({ph}) AND BrandId IS NOT NULL",
        product_ids,
    )
    brand_ids = [r[0] for r in cur.fetchall()]
    cur.execute(
        f"SELECT DISTINCT CategoryId FROM ErpProducts WHERE Id IN ({ph}) AND CategoryId IS NOT NULL",
        product_ids,
    )
    category_ids = [r[0] for r in cur.fetchall()]

    if brand_ids:
        bph = ",".join(["%s"] * len(brand_ids))
        cols, rows = fetchall(cur, f"SELECT * FROM ErpBrands WHERE Id IN ({bph})", brand_ids)
        counts["ErpBrands"] = write_csv(out_dir / "ErpBrands.csv", cols, rows)
    else:
        counts["ErpBrands"] = write_csv(out_dir / "ErpBrands.csv", [], [])

    if category_ids:
        cph = ",".join(["%s"] * len(category_ids))
        cols, rows = fetchall(cur, f"SELECT * FROM ErpCategories WHERE Id IN ({cph})", category_ids)
        counts["ErpCategories"] = write_csv(out_dir / "ErpCategories.csv", cols, rows)
    else:
        counts["ErpCategories"] = write_csv(out_dir / "ErpCategories.csv", [], [])

    # Products + BrandName / CategoryName pour remap FK à l'import
    cols, rows = fetchall(
        cur,
        f"""SELECT p.*,
                   b.Name AS _BrandName,
                   c.NameFr AS _CategoryNameFr,
                   c.Level AS _CategoryLevel
            FROM ErpProducts p
            LEFT JOIN ErpBrands b ON b.Id = p.BrandId
            LEFT JOIN ErpCategories c ON c.Id = p.CategoryId
            WHERE p.Id IN ({ph})""",
        product_ids,
    )
    counts["ErpProducts"] = write_csv(out_dir / "ErpProducts.csv", cols, rows)

    for table in ("ErpProductVehicles", "ErpProductImages", "ErpOemCrossReferences"):
        if not table_exists(cur, DB["database"], table):
            print(f"  skip {table} (absent)")
            counts[table] = 0
            continue
        n = export_child_with_erp_id(cur, table, product_ids, out_dir / f"{table}.csv")
        counts[table] = n
        print(f"  {table}: {n}")

    if INCLUDE_VIN_CACHE and table_exists(cur, DB["database"], "ErpVinVehicles"):
        cols, rows = fetchall(cur, "SELECT * FROM ErpVinVehicles")
        counts["ErpVinVehicles"] = write_csv(out_dir / "ErpVinVehicles.csv", cols, rows)
        print(f"  ErpVinVehicles: {counts['ErpVinVehicles']}")
    else:
        counts["ErpVinVehicles"] = 0

    manifest = {
        "format": "backup.rapidapi.catalog.pack",
        "version": 1,
        "exportedAt": datetime.utcnow().isoformat() + "Z",
        "dataSource": DATA_SOURCE,
        "database": DB["database"],
        "counts": counts,
        "notes": [
            "Clé produit stable: ErpProductId (ex. RAPID-12345).",
            "À l'import, BrandId/CategoryId/ProductId sont recalculés.",
            "Colonnes _BrandName / _Category* / ErpProductId sont des aides d'import.",
        ],
    }
    (out_dir / "manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8"
    )

    zip_path = out_dir.with_suffix(".zip")
    with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        for f in out_dir.iterdir():
            if f.is_file():
                zf.write(f, arcname=f.name)

    cur.close()
    conn.close()
    print("COUNTS", counts)
    print(f"Pack dossier: {out_dir}")
    print(f"Pack zip:     {zip_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
