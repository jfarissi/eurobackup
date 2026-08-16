#!/usr/bin/env python3
"""
Sync catalogue RapidAPI pour UN seul K-Type (vehicleId TecDoc).
Appelé à la demande depuis le flux VIN / plaque quand le K-Type est connu
mais absent d'ErpProductVehicles.

Usage:
  python sync_ktype_vehicle.py --ktype 5377 --make DACIA --model "DUSTER (HS_)"
  python sync_ktype_vehicle.py --ktype 5377 --make DACIA --model DUSTER --max-products 40 --year 2019
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path
from typing import Optional

DOCS = Path(__file__).resolve().parent
if str(DOCS) not in sys.path:
    sys.path.insert(0, str(DOCS))

from sync_rapidapi_poc import (  # noqa: E402
    DbWriter,
    RapidClient,
    flatten_all_leaf_categories,
    load_api_key,
    load_db_config,
    pick_leaf_categories,
    process_article,
)


def emit_progress(phase: str, current: int, total: int, message: str = "") -> None:
    safe_total = max(total, 1)
    payload = {
        "phase": phase,
        "current": current,
        "total": safe_total,
        "percent": min(100, round(100 * current / safe_total)),
        "message": message,
    }
    print("PROGRESS_JSON=" + json.dumps(payload, ensure_ascii=False), flush=True)


def list_categories(ktype: int) -> dict:
    api_key, host = load_api_key()
    client = RapidClient(api_key, host)
    cats = flatten_all_leaf_categories(client.categories(ktype))
    return {"ktype": ktype, "categories": cats}


def _parse_category_ids(raw: Optional[str]) -> list[int]:
    if not raw:
        return []
    ids: list[int] = []
    for part in str(raw).replace(";", ",").split(","):
        part = part.strip()
        if part.isdigit():
            ids.append(int(part))
    return ids


def sync_ktype(
    ktype: int,
    make: str,
    model: str,
    max_products: int,
    max_categories: int,
    skip_details: bool = False,
    refresh: bool = False,
    year: Optional[int] = None,
    category_ids: Optional[list[int]] = None,
    fuel: Optional[str] = None,
) -> dict:
    api_key, host = load_api_key()
    db_cfg = load_db_config()
    client = RapidClient(api_key, host)
    db = DbWriter(db_cfg)

    vehicle = {
        "vehicleId": ktype,
        "manufacturerName": make,
        "modelName": model,
    }
    if year and 1900 < year < 2100:
        vehicle["yearOfConstrFrom"] = year
        vehicle["yearOfConstrTo"] = year
        vehicle["year"] = year
    if fuel and str(fuel).strip():
        vehicle["fuelType"] = str(fuel).strip()

    known = db.load_existing_article_ids()
    fetched_ids: set[int] = set()

    emit_progress("start", 0, max_products, f"Import {make} {model}".strip())

    try:
        raw_cats = client.categories(ktype)
        selected = [cid for cid in (category_ids or []) if cid > 0]
        if selected:
            names = {int(c["id"]): str(c.get("name") or c["id"]) for c in flatten_all_leaf_categories(raw_cats)}
            cats = [(cid, names.get(cid, str(cid))) for cid in selected]
        else:
            cats = pick_leaf_categories(raw_cats, max_categories)
        if not cats:
            emit_progress("error", 0, max_products, "Aucune catégorie RapidAPI")
            return {"ktype": ktype, "products": 0, "categories": 0, "error": "no_categories"}

        emit_progress("categories", 0, len(cats), f"{len(cats)} catégorie(s)")

        for cat_index, (cat_id, cat_name) in enumerate(cats):
            if len(fetched_ids) >= max_products:
                break
            emit_progress("categories", cat_index + 1, len(cats), cat_name)
            try:
                arts = client.articles(ktype, cat_id)
            except Exception as ex:
                db.stats["errors"] += 1
                print(f"articles ERR cat={cat_id}: {ex}", file=sys.stderr)
                continue

            for art in arts:
                if len(fetched_ids) >= max_products:
                    break
                if skip_details:
                    aid = art.get("articleId")
                    if not aid:
                        continue
                    flat = {
                        "articleId": aid,
                        "articleNo": art.get("articleNo") or art.get("articleNumber"),
                        "name": art.get("articleProductName") or art.get("articleName") or cat_name,
                        "supplierName": art.get("supplierName") or art.get("brandName") or "",
                        "ean": art.get("eanNumber") or art.get("ean") or "",
                        "category": cat_name,
                        "dims": {"weight": None, "height": None, "width": None, "depth": None},
                        "images": [],
                        "oems": [],
                        "raw": art,
                    }
                    if art.get("s3image") or art.get("imageUrl"):
                        flat["images"] = [{"url": art.get("s3image") or art.get("imageUrl")}]
                    brand_id = db.get_or_create_brand(flat["supplierName"])
                    cat_db = db.get_or_create_category(flat["category"])
                    pid = db.upsert_product(flat, brand_id, cat_db)
                    if pid:
                        db.sync_vehicle(pid, vehicle, make, model)
                        db.sync_images(pid, flat.get("images") or [])
                        fetched_ids.add(int(aid))
                        emit_progress(
                            "articles",
                            len(fetched_ids),
                            max_products,
                            flat.get("articleNo") or flat.get("name") or cat_name,
                        )
                    continue

                try:
                    before = len(fetched_ids)
                    process_article(
                        db, client, art, vehicle, make, model, cat_name, fetched_ids, refresh=refresh
                    )
                    if len(fetched_ids) > before:
                        emit_progress(
                            "articles",
                            len(fetched_ids),
                            max_products,
                            art.get("articleNo") or art.get("articleProductName") or cat_name,
                        )
                except Exception as ex:
                    db.stats["errors"] += 1
                    print(f"process_article ERR: {ex}", file=sys.stderr)

        emit_progress("done", len(fetched_ids), max_products, "Import terminé")
        return {
            "ktype": ktype,
            "make": make,
            "model": model,
            "products": len(fetched_ids),
            "categories": len(cats),
            "stats": db.stats,
        }
    finally:
        db.close()


def main() -> int:
    parser = argparse.ArgumentParser(description="Sync RapidAPI pour un K-Type")
    parser.add_argument("--ktype", type=int, required=True, help="vehicleId TecDoc")
    parser.add_argument("--make", default="", help="Marque véhicule")
    parser.add_argument("--model", default="", help="Modèle véhicule")
    parser.add_argument("--year", type=int, default=None, help="Année véhicule (YearFrom/YearTo)")
    parser.add_argument("--max-products", type=int, default=int(os.getenv("RAPIDAPI_KTYPE_MAX_PRODUCTS", "40")))
    parser.add_argument("--max-categories", type=int, default=int(os.getenv("RAPIDAPI_KTYPE_MAX_CATS", "6")))
    parser.add_argument("--fast", action="store_true", help="Sans article_details (moins de quota API)")
    parser.add_argument("--refresh", action="store_true", help="Re-télécharge détails/OEM même si produit existe")
    parser.add_argument("--list-categories", action="store_true", help="Liste les catégories RapidAPI (sans import)")
    parser.add_argument("--category-ids", default="", help="CSV d'ids catégorie RapidAPI à importer")
    parser.add_argument("--fuel", default="", help="Carburant (Essence/Diesel) à écrire sur ErpProductVehicles")
    args = parser.parse_args()

    if args.list_categories:
        listed = list_categories(args.ktype)
        print("CATEGORIES_JSON=" + json.dumps(listed, ensure_ascii=False))
        print("RESULT_JSON=" + json.dumps({"ktype": args.ktype, "categories": len(listed.get("categories") or [])}, ensure_ascii=False))
        return 0 if listed.get("categories") else 1

    if not (args.make or "").strip() or not (args.model or "").strip():
        parser.error("--make et --model sont requis sauf avec --list-categories")

    result = sync_ktype(
        args.ktype,
        args.make.strip(),
        args.model.strip(),
        max(1, args.max_products),
        max(1, args.max_categories),
        skip_details=args.fast,
        refresh=args.refresh,
        year=args.year,
        category_ids=_parse_category_ids(args.category_ids),
        fuel=(args.fuel or "").strip() or None,
    )
    print("RESULT_JSON=" + json.dumps(result, ensure_ascii=False))
    return 0 if result.get("products", 0) > 0 or not result.get("error") else 1


if __name__ == "__main__":
    raise SystemExit(main())
