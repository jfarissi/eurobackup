#!/usr/bin/env python3
"""Copy RapidApi catalog rows from local MySQL → Docker demo MySQL (no RapidAPI calls)."""

from __future__ import annotations

import os
import sys
from typing import Any, Dict, List, Sequence, Tuple

import mysql.connector

SRC = {
    "host": os.getenv("SRC_DB_HOST", "localhost"),
    "port": int(os.getenv("SRC_DB_PORT", "3306")),
    "user": os.getenv("SRC_DB_USER", "root"),
    "password": os.getenv("SRC_DB_PASSWORD", "tata"),
    "database": os.getenv("SRC_DB_NAME", "backupcontent"),
    "charset": "utf8mb4",
}

DST = {
    "host": os.getenv("DST_DB_HOST", "localhost"),
    "port": int(os.getenv("DST_DB_PORT", "3307")),
    "user": os.getenv("DST_DB_USER", "backup"),
    "password": os.getenv("DST_DB_PASSWORD", "backup_pieces_auto"),
    "database": os.getenv("DST_DB_NAME", "backupcontent"),
    "charset": "utf8mb4",
}

DATA_SOURCE = os.getenv("DATA_SOURCE", "RapidApi")


def fetchall(cur, sql: str, params: Sequence[Any] = ()) -> Tuple[List[str], List[Tuple]]:
    cur.execute(sql, params)
    cols = [d[0] for d in cur.description] if cur.description else []
    rows = cur.fetchall()
    return cols, list(rows)


def upsert_rows(dst_cur, table: str, cols: List[str], rows: List[Tuple], update_cols: List[str] | None = None) -> int:
    if not rows:
        return 0
    placeholders = ", ".join(["%s"] * len(cols))
    col_list = ", ".join(f"`{c}`" for c in cols)
    if update_cols is None:
        update_cols = [c for c in cols if c.lower() != "id"]
    updates = ", ".join(f"`{c}`=VALUES(`{c}`)" for c in update_cols)
    sql = f"INSERT INTO `{table}` ({col_list}) VALUES ({placeholders}) ON DUPLICATE KEY UPDATE {updates}"
    dst_cur.executemany(sql, rows)
    return len(rows)


def main() -> int:
    print(f"SRC {SRC['host']}:{SRC['port']}/{SRC['database']} → DST {DST['host']}:{DST['port']}/{DST['database']}")
    src = mysql.connector.connect(**SRC)
    dst = mysql.connector.connect(**DST)
    src_cur = src.cursor()
    dst_cur = dst.cursor()

    src_cur.execute(
        "SELECT Id FROM ErpProducts WHERE DataSource=%s ORDER BY Id",
        (DATA_SOURCE,),
    )
    product_ids = [r[0] for r in src_cur.fetchall()]
    print(f"RapidApi products in source: {len(product_ids)}")
    if not product_ids:
        print("Nothing to copy.")
        return 0

    # Brands / categories referenced by those products
    ph = ",".join(["%s"] * len(product_ids))
    src_cur.execute(
        f"SELECT DISTINCT BrandId FROM ErpProducts WHERE Id IN ({ph}) AND BrandId IS NOT NULL",
        product_ids,
    )
    brand_ids = [r[0] for r in src_cur.fetchall()]
    src_cur.execute(
        f"SELECT DISTINCT CategoryId FROM ErpProducts WHERE Id IN ({ph}) AND CategoryId IS NOT NULL",
        product_ids,
    )
    category_ids = [r[0] for r in src_cur.fetchall()]

    if brand_ids:
        bph = ",".join(["%s"] * len(brand_ids))
        cols, rows = fetchall(src_cur, f"SELECT * FROM ErpBrands WHERE Id IN ({bph})", brand_ids)
        n = upsert_rows(dst_cur, "ErpBrands", cols, rows)
        print(f"  ErpBrands: {n}")

    if category_ids:
        cph = ",".join(["%s"] * len(category_ids))
        cols, rows = fetchall(src_cur, f"SELECT * FROM ErpCategories WHERE Id IN ({cph})", category_ids)
        n = upsert_rows(dst_cur, "ErpCategories", cols, rows)
        print(f"  ErpCategories: {n}")

    cols, rows = fetchall(
        src_cur,
        f"SELECT * FROM ErpProducts WHERE Id IN ({ph})",
        product_ids,
    )
    n = upsert_rows(dst_cur, "ErpProducts", cols, rows)
    print(f"  ErpProducts: {n}")

    # Child tables keyed by ProductId
    for table in (
        "ErpProductImages",
        "ErpProductVehicles",
        "ErpOemCrossReferences",
        "ErpProductAttributeValues",
        "ErpProductVariants",
    ):
        src_cur.execute(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=%s AND table_name=%s",
            (SRC["database"], table),
        )
        if src_cur.fetchone()[0] == 0:
            print(f"  {table}: skip (absent en source)")
            continue
        dst_cur.execute(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=%s AND table_name=%s",
            (DST["database"], table),
        )
        if dst_cur.fetchone()[0] == 0:
            print(f"  {table}: skip (absent en destination)")
            continue
        cols, rows = fetchall(src_cur, f"SELECT * FROM `{table}` WHERE ProductId IN ({ph})", product_ids)
        # Prefer replace by primary key when present
        n = upsert_rows(dst_cur, table, cols, rows)
        print(f"  {table}: {n}")

    # Copy vehicle_compat attribute definition + values already handled above
    src_cur.execute(
        "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=%s AND table_name='ErpProductAttributeDefinitions'",
        (SRC["database"],),
    )
    if src_cur.fetchone()[0]:
        cols, rows = fetchall(
            src_cur,
            "SELECT * FROM ErpProductAttributeDefinitions WHERE Code=%s",
            ("vehicle_compat",),
        )
        if rows:
            # Remap CompanyId to destination default company if needed
            dst_cur.execute("SELECT Id FROM Companies ORDER BY CreatedAt LIMIT 1")
            company_row = dst_cur.fetchone()
            company_id = company_row[0] if company_row else None
            if company_id and "CompanyId" in cols:
                idx = cols.index("CompanyId")
                rows = [tuple(company_id if i == idx else v for i, v in enumerate(r)) for r in rows]
            n = upsert_rows(dst_cur, "ErpProductAttributeDefinitions", cols, rows)
            print(f"  ErpProductAttributeDefinitions(vehicle_compat): {n}")

    dst.commit()
    dst_cur.execute("SELECT COUNT(*) FROM ErpProducts WHERE DataSource=%s", (DATA_SOURCE,))
    print(f"Done. Destination RapidApi products: {dst_cur.fetchone()[0]}")
    src_cur.close()
    dst_cur.close()
    src.close()
    dst.close()
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as ex:
        print(f"ERROR: {ex}", file=sys.stderr)
        raise
