#!/usr/bin/env python3
"""Retry image backfill for RapidApi products missing PicName."""

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
    key = cfg["RapidApi"]["ApiKey"]
    host = cfg["RapidApi"]["Host"]
    db = mysql.connector.connect(
        host="localhost", user="root", password="tata", database="backupcontent"
    )
    cur = db.cursor(dictionary=True)
    cur.execute(
        """SELECT Id, ErpProductId, Name, Reference FROM ErpProducts
           WHERE DataSource=%s AND (PicName IS NULL OR PicName='')""",
        ("RapidApi",),
    )
    rows = cur.fetchall()
    print(f"remaining={len(rows)}")
    headers = {"X-RapidAPI-Key": key, "X-RapidAPI-Host": host}
    session = requests.Session()
    now = datetime.now()

    for p in rows:
        m = re.match(r"^RAPID-(\d+)$", p["ErpProductId"] or "")
        if not m:
            continue
        article_id = int(m.group(1))
        ok = False
        for attempt in range(5):
            time.sleep(2.5 * (attempt + 1))
            r = session.get(
                f"https://{host}/articles/article-all-media-info",
                headers=headers,
                params={"articleId": article_id, "langId": 4},
                timeout=45,
            )
            if r.status_code == 429:
                print(f"429 {p['Reference']} attempt={attempt}")
                continue
            r.raise_for_status()
            media = r.json() if isinstance(r.json(), list) else []
            urls = [i.get("s3image") for i in media if i.get("s3image")]
            if not urls:
                print(f"noimg {p['Reference']}")
                ok = True
                break
            cur.execute(
                "UPDATE ErpProducts SET PicName=%s, UpdatedAt=%s WHERE Id=%s",
                (urls[0][:500], now, p["Id"]),
            )
            cur.execute("DELETE FROM ErpProductImages WHERE ProductId=%s", (p["Id"],))
            for i, u in enumerate(urls[:8]):
                cur.execute(
                    """INSERT INTO ErpProductImages
                       (Id, ProductId, Url, AltText, IsMain, SortOrder, CreatedAt)
                       VALUES (%s,%s,%s,%s,%s,%s,%s)""",
                    (str(uuid.uuid4()), p["Id"], u, p["Name"] or "", 1 if i == 0 else 0, i, now),
                )
            db.commit()
            print(f"ok {p['Reference']} imgs={len(urls)}")
            ok = True
            break
        if not ok:
            print(f"FAIL {p['Reference']}")

    cur.close()
    db.close()


if __name__ == "__main__":
    main()
