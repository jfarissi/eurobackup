#!/usr/bin/env python3
"""
Backfill ErpOemCrossReferences pour les produits RAPID-* déjà en base.

Quota Ultra : ~1 req/produit (details). Cross-ref secondaire seulement si
`articleOemNo` est absent du payload (pas si présent et vide).

Les produits déjà vérifiés (y compris 0 OEM) sont enregistrés dans
`ErpOemBackfillChecked` pour ne pas reconsommer le quota.

Usage :
  cd d:\\GitHub\\Backup.Web.Api\\docs
  $env:RAPIDAPI_OEM_MAX = "200"
  python backfill_rapidapi_oem.py
  # re-forcer un produit déjà checked : RAPIDAPI_OEM_FORCE=1
"""

from __future__ import annotations

import os
import sys
from datetime import datetime
from typing import Any, Dict, List, Optional, Tuple

import sync_rapidapi_poc as poc

OEM_MAX = int(os.getenv("RAPIDAPI_OEM_MAX", "200"))
OEM_FORCE = os.getenv("RAPIDAPI_OEM_FORCE", "").strip() in ("1", "true", "True", "yes", "YES")
SKIP_CROSSREF = os.getenv("RAPIDAPI_OEM_SKIP_CROSSREF", "").strip() in (
    "1",
    "true",
    "True",
    "yes",
    "YES",
)


def parse_article_id(erp_product_id: str) -> Optional[int]:
    if not erp_product_id or not str(erp_product_id).startswith("RAPID-"):
        return None
    try:
        return int(str(erp_product_id).split("-", 1)[1])
    except (ValueError, IndexError):
        return None


def ensure_checked_table(db: poc.DbWriter) -> None:
    db.execute(
        """
        CREATE TABLE IF NOT EXISTS ErpOemBackfillChecked (
            ProductId INT NOT NULL PRIMARY KEY,
            ErpProductId VARCHAR(64) NULL,
            OemCount INT NOT NULL DEFAULT 0,
            CheckedAt DATETIME NOT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """
    )


def mark_checked(db: poc.DbWriter, product_id: int, erp_id: str, oem_count: int) -> None:
    db.execute(
        """
        INSERT INTO ErpOemBackfillChecked (ProductId, ErpProductId, OemCount, CheckedAt)
        VALUES (%s, %s, %s, %s)
        ON DUPLICATE KEY UPDATE
            ErpProductId=VALUES(ErpProductId),
            OemCount=VALUES(OemCount),
            CheckedAt=VALUES(CheckedAt)
        """,
        (product_id, erp_id, oem_count, datetime.now()),
    )


def list_targets(db: poc.DbWriter) -> List[Tuple[int, str, Optional[str], Optional[str]]]:
    """
    Produits RAPID à traiter.
    Sans FORCE : pas d'OEM en base ET pas encore dans ErpOemBackfillChecked.
    """
    if OEM_FORCE:
        sql = """
            SELECT p.Id, p.ErpProductId, p.Reference, COALESCE(p.Brand, p.Manufacturer) AS BrandName
            FROM ErpProducts p
            WHERE p.ErpProductId LIKE 'RAPID-%%'
            ORDER BY p.Id
            LIMIT %s
        """
        db.cur.execute(sql, (OEM_MAX,))
    else:
        sql = """
            SELECT p.Id, p.ErpProductId, p.Reference, COALESCE(p.Brand, p.Manufacturer) AS BrandName
            FROM ErpProducts p
            LEFT JOIN ErpOemCrossReferences o ON o.ProductId = p.Id
            LEFT JOIN ErpOemBackfillChecked c ON c.ProductId = p.Id
            WHERE p.ErpProductId LIKE 'RAPID-%%'
              AND o.Id IS NULL
              AND c.ProductId IS NULL
            ORDER BY p.Id
            LIMIT %s
        """
        db.cur.execute(sql, (OEM_MAX,))
    rows = db.cur.fetchall() or []
    return [
        (
            int(row["Id"]),
            str(row["ErpProductId"]),
            row.get("Reference"),
            row.get("BrandName"),
        )
        for row in rows
    ]


def resolve_oems(
    client: poc.RapidClient,
    article_id: int,
    reference: Optional[str],
    brand_name: Optional[str],
) -> List[Dict[str, Any]]:
    details = client.article_details(article_id)
    if not isinstance(details, dict):
        details = {}
    oems = poc.extract_oem_list(details, {})
    if oems or SKIP_CROSSREF:
        return oems

    # articleOemNo présent (même null/[]) = TecDoc n'a pas d'OEM → pas de 2e req
    if "articleOemNo" in details:
        return oems

    article = details.get("article") if isinstance(details.get("article"), dict) else details
    article_no = (
        (article or {}).get("articleNo")
        or (article or {}).get("articleNumber")
        or reference
        or ""
    )
    supplier = (
        (article or {}).get("supplierName")
        or (article or {}).get("brandName")
        or (article or {}).get("mfrName")
        or brand_name
        or ""
    )
    extra = client.article_oem_crossrefs(str(article_no), str(supplier))
    if extra:
        return poc.extract_oem_list({"oemNumbers": extra}, {})
    return []


def run() -> None:
    key, host = poc.load_api_key()
    client = poc.RapidClient(key, host)
    db = poc.DbWriter(poc.load_db_config())
    try:
        ensure_checked_table(db)
        targets = list_targets(db)
        print(
            f"OEM backfill: {len(targets)} produits "
            f"(max={OEM_MAX} force={OEM_FORCE} skip_crossref={SKIP_CROSSREF})"
        )
        if not targets:
            print("Rien à faire (tous vérifiés ou déjà pourvus d'OEM).")
            return

        ok = 0
        empty = 0
        errors = 0
        oem_total = 0

        for i, (pid, erp_id, ref, brand) in enumerate(targets, 1):
            aid = parse_article_id(erp_id)
            if aid is None:
                print(f"  [{i}/{len(targets)}] skip {erp_id} (id invalide)")
                mark_checked(db, pid, erp_id, 0)
                continue
            try:
                oems = resolve_oems(client, aid, ref, brand)
                n = db.sync_oem(pid, oems)
                mark_checked(db, pid, erp_id, n)
                if n:
                    ok += 1
                    oem_total += n
                    print(f"  [{i}/{len(targets)}] {erp_id} ref={ref} → {n} OEM")
                else:
                    empty += 1
                    print(f"  [{i}/{len(targets)}] {erp_id} ref={ref} → 0 OEM (API vide)")
            except Exception as e:
                errors += 1
                db.stats["errors"] += 1
                print(f"  [{i}/{len(targets)}] ERR {erp_id}: {e}")

        print(
            "\nSTATS",
            {
                "products_ok": ok,
                "products_empty": empty,
                "errors": errors,
                "oem_rows": oem_total,
                "db": db.stats,
            },
        )
        print(
            "Relancer la même commande pour le lot suivant "
            "(les 0 OEM déjà vérifiés sont mémorisés, pas de OFFSET)."
        )
    finally:
        db.close()


if __name__ == "__main__":
    try:
        run()
    except KeyboardInterrupt:
        print("\nInterrompu.", file=sys.stderr)
        sys.exit(130)
