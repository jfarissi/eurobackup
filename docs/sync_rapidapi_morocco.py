#!/usr/bin/env python3
"""
Sync RapidAPI → ERP — parc Maroc (FR, borné pour Ultra 100k req/mois).

Réutilise le client / DbWriter de sync_rapidapi_poc.py.
1 langue : FR (lang-id=6). Skip articles déjà en base (sauf RAPIDAPI_REFRESH=1).

Usage (PowerShell) :
  cd d:\\GitHub\\Backup.Web.Api\\docs

  # Profil large (majorité marques MA) — recommandé pour enrichir la base
  $env:RAPIDAPI_PROFILE = "wide"
  Remove-Item Env:RAPIDAPI_CAT_FOCUS -ErrorAction SilentlyContinue
  python sync_rapidapi_morocco.py

  # Profil standard (défaut)
  $env:RAPIDAPI_PROFILE = "standard"
  $env:RAPIDAPI_MAX_PRODUCTS = "800"
  python sync_rapidapi_morocco.py

  # Sync ciblée roulements
  $env:RAPIDAPI_CAT_FOCUS = "roulement,bearing,moyeu,hub"
  $env:RAPIDAPI_MAX_PRODUCTS = "400"
  python sync_rapidapi_morocco.py

  # Marques manquantes seulement
  $env:RAPIDAPI_PROFILE = "wide"
  $env:RAPIDAPI_MFG_FOCUS = "VOLKSWAGEN,TOYOTA,FORD,KIA,FIAT,OPEL,NISSAN,MERCEDES-BENZ,BMW,SEAT,SKODA,SUZUKI"
  $env:RAPIDAPI_MAX_PRODUCTS = "3000"
  python sync_rapidapi_morocco.py
"""

from __future__ import annotations

import os
import sys
from typing import Dict, List, Optional, Set, Tuple

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import sync_rapidapi_poc as poc

# ── Parc Maroc (ordre = priorité). Couverture large marché MA / Maghreb. ──
MOROCCO_FLEET: List[Tuple[str, List[str]]] = [
    ("DACIA", ["SANDERO", "LOGAN", "DUSTER", "LODGY", "DOKKER", "JOGGER", "SPRING"]),
    ("RENAULT", ["CLIO", "MEGANE", "SYMBOL", "KANGOO", "CAPTUR", "TWINGO", "EXPRESS", "TRAFIC", "MASTER"]),
    ("PEUGEOT", ["208", "301", "308", "2008", "3008", "5008", "PARTNER", "RIFTER", "EXPERT", "207"]),
    ("CITROEN", ["C3", "C4", "C-ELYSEE", "ELYSEE", "BERLINGO", "C5", "JUMPY"]),
    ("CITROËN", ["C3", "C4", "C-ELYSEE", "ELYSEE", "BERLINGO", "C5", "JUMPY"]),
    ("HYUNDAI", ["I10", "I20", "ACCENT", "TUCSON", "CRETA", "I30", "SANTA FE", "H1"]),
    ("VOLKSWAGEN", ["POLO", "GOLF", "PASSAT", "TIGUAN", "CADDY", "TRANSPORTER", "AMAROK"]),
    ("VW", ["POLO", "GOLF", "PASSAT", "TIGUAN", "CADDY", "TRANSPORTER", "AMAROK"]),
    ("TOYOTA", ["COROLLA", "YARIS", "RAV4", "HILUX", "LAND CRUISER", "AURIS", "PROACE"]),
    ("FIAT", ["PUNTO", "500", "DOBLO", "TIPO", "FIORINO", "DUCATO"]),
    ("FORD", ["FIESTA", "FOCUS", "ECOSPORT", "KUGA", "RANGER", "TRANSIT", "CONNECT"]),
    ("OPEL", ["CORSA", "ASTRA", "COMBO", "MOKKA", "VIVARO"]),
    ("NISSAN", ["MICRA", "QASHQAI", "JUKE", "NAVARA", "NP300", "X-TRAIL"]),
    ("KIA", ["PICANTO", "RIO", "SPORTAGE", "CERATO", "SORENTO"]),
    ("MERCEDES", ["CLASSE A", "CLASSE C", "CLASSE E", "SPRINTER", "VITO", "CITAN"]),
    ("MERCEDES-BENZ", ["CLASSE A", "CLASSE C", "CLASSE E", "SPRINTER", "VITO", "CITAN"]),
    ("BMW", ["SERIE 1", "SERIE 3", "SERIE 5", "X1", "X3", "X5"]),
    ("AUDI", ["A3", "A4", "A6", "Q3", "Q5"]),
    ("SEAT", ["IBIZA", "LEON", "ATECA", "ARONA"]),
    ("SKODA", ["FABIA", "OCTAVIA", "RAPID", "KAROQ", "KODIAQ"]),
    ("SUZUKI", ["SWIFT", "VITARA", "JIMNY", "DZIRE", "ALTO"]),
    ("MITSUBISHI", ["L200", "ASX", "OUTLANDER", "PAJERO"]),
    ("ISUZU", ["D-MAX"]),
    ("CHERY", ["TIGGO", "ARRIZO", "QQ"]),
    ("GEELY", ["COOLRAY", "EMGRAND", "AZKARRA"]),
    ("DFSK", ["GLORY", "C SERIES"]),
    ("HAVAL", ["H6", "JOLION"]),
    ("BYD", ["ATTO", "SONG", "SEAL"]),
]

_PROFILE = os.getenv("RAPIDAPI_PROFILE", "standard").strip().lower()
_PROFILE_DEFAULTS = {
    "standard": {
        "MAX_PRODUCTS": "800",
        "MAX_MFG": "10",
        "MAX_MODELS": "5",
        "MAX_VEHICLES": "3",
        "MAX_CATS": "8",
        "YEAR_FROM_MIN": "2014",
    },
    "wide": {
        "MAX_PRODUCTS": "4000",
        "MAX_MFG": "30",
        "MAX_MODELS": "15",
        "MAX_VEHICLES": "10",
        "MAX_CATS": "15",
        "YEAR_FROM_MIN": "2009",
    },
}


def _env_int(name: str, profile_key: str, fallback: str) -> int:
    if name in os.environ and str(os.environ.get(name, "")).strip() != "":
        return int(os.environ[name])
    defaults = _PROFILE_DEFAULTS.get(_PROFILE, _PROFILE_DEFAULTS["standard"])
    return int(defaults.get(profile_key, fallback))


MAX_PRODUCTS = _env_int("RAPIDAPI_MAX_PRODUCTS", "MAX_PRODUCTS", "800")
MAX_MANUFACTURERS = _env_int("RAPIDAPI_MAX_MFG", "MAX_MFG", "10")
MAX_MODELS_PER_MFG = _env_int("RAPIDAPI_MAX_MODELS", "MAX_MODELS", "5")
MAX_VEHICLES_PER_MODEL = _env_int("RAPIDAPI_MAX_VEHICLES", "MAX_VEHICLES", "3")
MAX_CATEGORIES_PER_VEHICLE = _env_int("RAPIDAPI_MAX_CATS", "MAX_CATS", "8")
YEAR_FROM_MIN = _env_int("RAPIDAPI_YEAR_FROM_MIN", "YEAR_FROM_MIN", "2014")
DRY_RUN = os.getenv("RAPIDAPI_DRY_RUN", "").strip() in ("1", "true", "True", "yes", "YES")

# Focus constructeurs (ex. VOLKSWAGEN,TOYOTA,FORD) — ignore les autres du parc.
_MFG_FOCUS_RAW = os.getenv("RAPIDAPI_MFG_FOCUS", "").strip()
MFG_FOCUS: Tuple[str, ...] = tuple(
    p.strip().upper().replace("Ë", "E").replace("É", "E").replace("È", "E").replace("-", " ")
    for p in _MFG_FOCUS_RAW.replace(";", ",").split(",")
    if p.strip()
)

poc.LANG_ID = int(os.getenv("RAPIDAPI_LANG_ID", "6"))
poc.COUNTRY_ID = int(os.getenv("RAPIDAPI_COUNTRY_ID", "34"))
poc.MAX_PRODUCTS = MAX_PRODUCTS
poc.MAX_CATEGORIES_PER_VEHICLE = MAX_CATEGORIES_PER_VEHICLE


def _norm(s: str) -> str:
    return (
        (s or "")
        .upper()
        .replace("Ë", "E")
        .replace("É", "E")
        .replace("È", "E")
        .replace("-", " ")
        .strip()
    )


def _name_matches_focus(name: str, focus: str) -> bool:
    n = _norm(name)
    f = _norm(focus)
    if not n or not f:
        return False
    if n == f:
        return True
    if n.startswith(f + " ") or f.startswith(n + " "):
        return True
    # alias courants
    aliases = {
        "VW": "VOLKSWAGEN",
        "VOLKSWAGEN": "VW",
        "MERCEDES": "MERCEDES BENZ",
        "MERCEDES BENZ": "MERCEDES",
    }
    alt = aliases.get(f)
    if alt and (n == alt or n.startswith(alt + " ")):
        return True
    return False


def fleet_map() -> Dict[str, List[str]]:
    out: Dict[str, List[str]] = {}
    for brand, models in MOROCCO_FLEET:
        key = _norm(brand)
        out.setdefault(key, [])
        for m in models:
            nm = _norm(m)
            if nm not in out[key]:
                out[key].append(nm)
    return out


def pick_morocco_manufacturers(all_mfg: List[Dict]) -> List[Dict]:
    fmap = fleet_map()
    by_priority: List[Dict] = []
    seen_ids: Set[int] = set()
    seen_names: Set[str] = set()

    brand_iter = MOROCCO_FLEET
    if MFG_FOCUS:
        # Ne garder que les marques demandées (ordre = ordre du focus)
        brand_iter = []
        for f in MFG_FOCUS:
            matched = False
            for brand, models in MOROCCO_FLEET:
                if _name_matches_focus(brand, f):
                    brand_iter.append((brand, models))
                    matched = True
                    break
            if not matched:
                # Marque hors liste parc : on tente quand même le matching API
                brand_iter.append((f, []))

    for brand, _ in brand_iter:
        target = _norm(brand)
        if target == "CITROEN" and any(n.startswith("CITROEN") for n in seen_names):
            continue
        if target in ("VW", "VOLKSWAGEN") and any(
            n.startswith("VOLKSWAGEN") or n == "VW" for n in seen_names
        ):
            continue
        if target.startswith("MERCEDES") and any(n.startswith("MERCEDES") for n in seen_names):
            continue

        for m in all_mfg:
            name = _norm(str(m.get("manufacturerName") or ""))
            mid = int(m.get("manufacturerId") or 0)
            if not mid or mid in seen_ids:
                continue
            if "DF-" in name or "PSA" in name:
                continue
            if _name_matches_focus(name, target) or name == target or name.startswith(target + " "):
                by_priority.append(m)
                seen_ids.add(mid)
                seen_names.add(name)
                break

    if not by_priority:
        for m in all_mfg:
            name = _norm(str(m.get("manufacturerName") or ""))
            mid = int(m.get("manufacturerId") or 0)
            if not mid or mid in seen_ids:
                continue
            if MFG_FOCUS:
                if not any(_name_matches_focus(name, f) for f in MFG_FOCUS):
                    continue
            elif name not in fmap:
                continue
            by_priority.append(m)
            seen_ids.add(mid)

    limit = MAX_MANUFACTURERS
    if MFG_FOCUS:
        limit = max(limit, len(MFG_FOCUS))
    return by_priority[:limit]


def pick_morocco_models(models: List[Dict], brand_name: str) -> List[Dict]:
    """1 modèle par mot-clé (Sandero + Logan + Duster…), pas 4 Logan."""
    preferred = fleet_map().get(_norm(brand_name), [])
    if not preferred:
        return models[:MAX_MODELS_PER_MFG]

    by_pref: List[Tuple[int, Dict]] = []
    used_ids: Set[int] = set()
    for i, pref in enumerate(preferred):
        candidates = []
        for model in models:
            mid = int(model.get("modelId") or 0)
            if not mid or mid in used_ids:
                continue
            mname = _norm(str(model.get("modelName") or ""))
            if pref in mname or mname in pref:
                penalty = 0
                if any(x in mname for x in ("PICK", "CAMION", "EXPRESS", "FOURGON", "AUTOBUS")):
                    penalty = 10
                candidates.append((penalty, len(mname), model, mid))
        if not candidates:
            continue
        candidates.sort(key=lambda x: (x[0], x[1]))
        _, _, model, mid = candidates[0]
        used_ids.add(mid)
        by_pref.append((i, model))
        if len(by_pref) >= MAX_MODELS_PER_MFG:
            break

    if by_pref:
        return [m for _, m in by_pref]
    return models[:MAX_MODELS_PER_MFG]


def vehicle_year_ok(vehicle: Dict) -> bool:
    end = vehicle.get("constructionIntervalEnd") or vehicle.get("modelYearTo") or ""
    start = vehicle.get("constructionIntervalStart") or vehicle.get("modelYearFrom") or ""
    year = None
    for raw in (end, start):
        if not raw:
            continue
        try:
            year = int(str(raw)[:4])
            break
        except ValueError:
            continue
    if year is None:
        return True
    return year >= YEAR_FROM_MIN


def run() -> None:
    api_key, host = poc.load_api_key()
    client = poc.RapidClient(api_key, host)
    client.lang_id = poc.LANG_ID
    client.country_id = poc.COUNTRY_ID

    focus = ",".join(poc.CAT_FOCUS) if poc.CAT_FOCUS else "(toutes familles)"
    mfg_focus = ",".join(MFG_FOCUS) if MFG_FOCUS else "(parc Maroc complet)"
    print("=== Sync RapidAPI Maroc (FR) ===")
    print(
        f"Profile={_PROFILE} Host={host} lang={client.lang_id} country={client.country_id} "
        f"max_products={MAX_PRODUCTS} dry_run={DRY_RUN} cat_focus={focus}"
    )
    print(f"mfg_focus={mfg_focus}")
    print(
        f"Limites: mfg={MAX_MANUFACTURERS} models={MAX_MODELS_PER_MFG} "
        f"vehicles={MAX_VEHICLES_PER_MODEL} cats={MAX_CATEGORIES_PER_VEHICLE} "
        f"year>={YEAR_FROM_MIN}"
    )

    db: Optional[poc.DbWriter] = None
    if not DRY_RUN:
        db = poc.DbWriter(poc.load_db_config())
        print(f"DB={db.conn.database}@{db.conn.server_host}")

    try:
        all_mfg = client.manufacturers()
        mfgs = pick_morocco_manufacturers(all_mfg)
        print(f"Constructeurs Maroc ({len(mfgs)}): {[m.get('manufacturerName') for m in mfgs]}")
        if not mfgs:
            raise SystemExit("Aucun constructeur Maroc trouvé dans /manufacturers/list")

        fetched_ids: Set[int] = set()
        if db is not None:
            known = db.load_existing_article_ids()
            print(
                f"Articles déjà en base (skip détail): {len(known)} "
                f"refresh={poc.REFRESH_EXISTING}"
            )

        for mfg in mfgs:
            if len(fetched_ids) >= MAX_PRODUCTS:
                break
            mid = int(mfg["manufacturerId"])
            mname = str(mfg.get("manufacturerName") or "?")
            print(f"\n=== {mname} (id={mid}) ===")
            try:
                models = pick_morocco_models(client.models(mid), mname)
            except Exception as e:
                print(f"  models ERR: {e}")
                continue
            print(f"  modèles retenus: {[m.get('modelName') for m in models]}")

            if DRY_RUN:
                for model in models:
                    model_id = int(model["modelId"])
                    try:
                        vehicles = [
                            v for v in client.vehicles(model_id) if vehicle_year_ok(v)
                        ][:MAX_VEHICLES_PER_MODEL]
                    except Exception as e:
                        print(f"  vehicles ERR {model.get('modelName')}: {e}")
                        continue
                    print(
                        f"    {model.get('modelName')}: {len(vehicles)} véhicules (échantillon)"
                    )
                    for v in vehicles[:2]:
                        print(
                            f"      - {v.get('vehicleId')} {v.get('typeEngineName') or ''} "
                            f"{v.get('constructionIntervalStart')}-{v.get('constructionIntervalEnd')}"
                        )
                continue

            assert db is not None
            for model in models:
                if len(fetched_ids) >= MAX_PRODUCTS:
                    break
                model_id = int(model["modelId"])
                model_name = str(model.get("modelName") or "")
                print(f"  modèle: {model_name}")
                try:
                    vehicles = [
                        v for v in client.vehicles(model_id) if vehicle_year_ok(v)
                    ][:MAX_VEHICLES_PER_MODEL]
                except Exception as e:
                    print(f"  vehicles ERR: {e}")
                    continue

                for vehicle in vehicles:
                    if len(fetched_ids) >= MAX_PRODUCTS:
                        break
                    vid = int(vehicle["vehicleId"])
                    eng = vehicle.get("typeEngineName", "")
                    print(f"    véhicule {vid} {eng}")
                    try:
                        cats = poc.pick_leaf_categories(
                            client.categories(vid), MAX_CATEGORIES_PER_VEHICLE
                        )
                    except Exception as e:
                        print(f"    categories ERR: {e}")
                        continue
                    if not cats:
                        print("      (aucune catégorie retenue — focus trop strict ?)")
                        continue

                    for cat_id, cat_name in cats:
                        if len(fetched_ids) >= MAX_PRODUCTS:
                            break
                        try:
                            arts = client.articles(vid, cat_id)
                        except Exception as e:
                            print(f"    articles {cat_name} ERR: {e}")
                            continue
                        print(f"      cat {cat_name} ({cat_id}): {len(arts)} articles")

                        for art in arts:
                            if len(fetched_ids) >= MAX_PRODUCTS:
                                break
                            try:
                                poc.process_article(
                                    db,
                                    client,
                                    art,
                                    vehicle,
                                    mname,
                                    model_name,
                                    cat_name,
                                    fetched_ids,
                                )
                            except Exception as e:
                                db.stats["errors"] += 1
                                print(f"        ERR article: {e}")

        if DRY_RUN:
            print("\nDRY RUN terminé — aucune écriture BDD / aucun détail article.")
        else:
            assert db is not None
            print("\nSTATS", db.stats)
            print(f"Nouveaux/rafraîchis (API détail): {len(fetched_ids)}")
            print(f"Total RAPID en base: {len(db.load_existing_article_ids())}")
    finally:
        if db is not None:
            db.close()


if __name__ == "__main__":
    run()
