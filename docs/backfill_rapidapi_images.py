#!/usr/bin/env python3
"""Backfill images for RapidApi products via /articles/article-all-media-info."""

from __future__ import annotations

import json
import re
import time
import uuid
from datetime import datetime
from pathlib import Path

import mysql.connector
import requests

ROOT = Path(__file__).resolve().parents[1]
APPSETTINGS = ROOT / "Backup.Web.Api.Server" / "appsettings.json"


def main():
    cfg = json.loads(APPSETTINGS.read_text(encoding="utf-8"))
    key = (cfg.get("RapidApi") or {}).get("ApiKey") or ""
    host = (cfg.get("RapidApi") or {}).get("Host") or "auto-parts-catalog.p.rapidapi.com"
    lang_id = int((cfg.get("RapidApi") or {}).get("LangId") or 4)
    if not key:
        raise SystemExit("RapidApi ApiKey missing")

    cs = cfg.get("ConnectionStrings", {}).get("DefaultConnection", "")
    parts = {}
    for chunk in cs.split(";"):
        if "=" in chunk:
            k, v = chunk.split("=", 1)
            parts[k.strip().lower()] = v.strip()

    db = mysql.connector.connect(
        host=parts.get("server", "localhost"),
        database=parts.get("database", "backupcontent"),
        user=parts.get("user", "root"),
        password=parts.get("password", "tata"),
        charset="utf8mb4",
    )
    cur = db.cursor(dictionary=True)
    cur.execute(
        "SELECT Id, ErpProductId, Name, Reference FROM ErpProducts WHERE DataSource=%s",
        ("RapidApi",),
    )
    products = cur.fetchall()
    print(f"products={len(products)}")

    headers = {
        "X-RapidAPI-Key": key,
        "X-RapidAPI-Host": host,
        "Content-Type": "application/json",
    }
    session = requests.Session()
    now = datetime.now()
    updated = 0
    skipped = 0
    errors = 0

    for p in products:
        m = re.match(r"^RAPID-(\d+)$", p["ErpProductId"] or "")
        if not m:
            skipped += 1
            continue
        article_id = int(m.group(1))
        time.sleep(0.35)
        try:
            r = session.get(
                f"https://{host}/articles/article-all-media-info",
                headers=headers,
                params={"articleId": article_id, "langId": lang_id},
                timeout=45,
            )
            if not r.ok and lang_id != 4:
                r = session.get(
                    f"https://{host}/articles/article-all-media-info",
                    headers=headers,
                    params={"articleId": article_id, "langId": 4},
                    timeout=45,
                )
            r.raise_for_status()
            media = r.json()
            if not isinstance(media, list):
                media = media.get("data") or media.get("articleMedia") or []
            urls = []
            for item in media:
                url = (item or {}).get("s3image") or (item or {}).get("url")
                if url and str(url).startswith("http"):
                    urls.append(str(url))
            if not urls:
                skipped += 1
                print(f"  no image {p['Reference']} id={article_id}")
                continue

            main_url = urls[0]
            cur.execute(
                "UPDATE ErpProducts SET PicName=%s, UpdatedAt=%s WHERE Id=%s",
                (main_url[:500], now, p["Id"]),
            )
            cur.execute("DELETE FROM ErpProductImages WHERE ProductId=%s", (p["Id"],))
            for idx, url in enumerate(urls[:8]):
                cur.execute(
                    """INSERT INTO ErpProductImages
                       (Id, ProductId, Url, AltText, IsMain, SortOrder, CreatedAt)
                       VALUES (%s,%s,%s,%s,%s,%s,%s)""",
                    (
                        str(uuid.uuid4()),
                        p["Id"],
                        url[:2000],
                        p["Name"] or "",
                        1 if idx == 0 else 0,
                        idx,
                        now,
                    ),
                )
            db.commit()
            updated += 1
            print(f"  + {p['Reference']}: {len(urls)} image(s)")
        except Exception as ex:
            errors += 1
            db.rollback()
            print(f"  ERR {p['Reference']}: {ex}")

    print(f"DONE updated={updated} skipped={skipped} errors={errors}")
    cur.close()
    db.close()


if __name__ == "__main__":
    main()
