#!/usr/bin/env python3
"""Restore RapidApi English names from image AltText, then apply clean French."""

from __future__ import annotations

import re
from datetime import datetime

import mysql.connector

PHRASES = [
    ("brake-pad-set-disc-brake", "jeu de plaquettes de frein à disque"),
    ("tensioner-pulley-timing-belt", "galet tendeur de courroie de distribution"),
    ("brake-pad-set", "jeu de plaquettes de frein"),
    ("brake-shoe-set", "jeu de mâchoires de frein"),
    ("timing-belt-kit", "kit de courroie de distribution"),
    ("tensioner-pulley", "galet tendeur"),
    ("fuel-filter", "filtre à carburant"),
    ("air-filter", "filtre à air"),
    ("oil-filter", "filtre à huile"),
    ("timing-belt", "courroie de distribution"),
]


def slugify(s: str) -> str:
    # keep ascii only for matching english source labels
    s = s.lower()
    s = s.encode("ascii", "ignore").decode("ascii")
    return re.sub(r"[^a-z0-9]+", "-", s).strip("-")


def looks_english(name: str) -> bool:
    low = name.lower()
    return any(
        w in low
        for w in (
            "brake",
            "filter",
            "belt",
            "pulley",
            "timing",
            "pad",
            "shoe",
            "tensioner",
            "kit",
        )
    )


def translate_english(name: str) -> str:
    n = slugify(name)
    if not n:
        return name
    consumed = [False] * len(n)
    parts: list[tuple[int, str]] = []
    for ph, fr in sorted(PHRASES, key=lambda x: -len(x[0])):
        i = 0
        while True:
            j = n.find(ph, i)
            if j < 0:
                break
            start_ok = j == 0 or n[j - 1] == "-"
            end_ok = j + len(ph) >= len(n) or n[j + len(ph)] == "-"
            if start_ok and end_ok and not any(consumed[j : j + len(ph)]):
                parts.append((j, fr))
                for k in range(j, j + len(ph)):
                    consumed[k] = True
            i = j + 1
    # leftover tokens: keep only meaningful leftovers not covered
    leftovers = []
    for tok in n.split("-"):
        if not tok:
            continue
        ts = n.find(tok)
        if any(consumed[ts : ts + len(tok)]):
            continue
        leftovers.append(tok)
    if parts:
        text = " ".join(t for _, t in sorted(parts))
        if leftovers:
            # ignore common english leftovers already implied
            skip = {"set", "disc", "brake", "timing", "belt", "pulley"}
            extra = [t for t in leftovers if t not in skip]
            if extra:
                text = text + " " + " ".join(extra)
        return text[:1].upper() + text[1:]
    return name


def main():
    db = mysql.connector.connect(
        host="localhost", user="root", password="tata", database="backupcontent"
    )
    cur = db.cursor(dictionary=True)
    cur.execute(
        """SELECT p.Id, p.Name, p.Reference,
                  (SELECT i.AltText FROM ErpProductImages i
                   WHERE i.ProductId=p.Id AND i.AltText IS NOT NULL AND i.AltText<>''
                   ORDER BY i.IsMain DESC, i.SortOrder LIMIT 1) AS AltText
           FROM ErpProducts p WHERE p.DataSource=%s""",
        ("RapidApi",),
    )
    rows = cur.fetchall()
    updated = 0
    for r in rows:
        source = r["AltText"] if r["AltText"] and looks_english(r["AltText"]) else None
        if not source and looks_english(r["Name"] or ""):
            source = r["Name"]
        if not source:
            # try recover common broken FR from reference-less patterns
            print(f"SKIP no english source: id={r['Id']} name={r['Name']!r}")
            continue
        fr = translate_english(source)
        if fr and fr != r["Name"]:
            cur.execute(
                "UPDATE ErpProducts SET Name=%s, Name2=%s, UpdatedAt=%s WHERE Id=%s",
                (fr, source[:255], datetime.now(), r["Id"]),
            )
            updated += 1
            print(f"{source} -> {fr}")
    db.commit()
    print(f"updated {updated}/{len(rows)}")
    cur.close()
    db.close()


if __name__ == "__main__":
    main()
