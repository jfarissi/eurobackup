# Catalogue Produit - Guide d'utilisation

Ce système permet d'utiliser un catalogue produit pour identifier et enrichir les produits extraits des factures par l'IA et Python.

## Structure du Catalogue

Le catalogue est basé sur les tables SQL suivantes:
- `Products` : Produits principaux
- `ProductVariants` : Variantes de produits
- `ProductImages` : Images des produits
- `ProductAttributeValues` : Attributs des produits

## Format du Catalogue

Le catalogue peut être chargé depuis:
1. **Fichier JSON** : Format simple et portable
2. **Base de données SQL** : Connexion directe à la base de données

### Format JSON

Le fichier JSON doit contenir un tableau d'objets produits:

```json
[
  {
    "Id": 1,
    "Reference": "REF001",
    "Sku": "SKU001",
    "Barcode": "5413503555628",
    "Gtin": "5413503555628",
    "Name_EN": "Product Name EN",
    "Name_FR": "Nom du produit FR",
    "Name_NL": "Productnaam NL",
    "Description_EN": "Description in English",
    "Description_FR": "Description en français",
    "Description_NL": "Beschrijving in het Nederlands",
    "SellingPrice": 10.50,
    "CostPrice": 8.00,
    "StockQuantity": 100,
    "WeightKg": 0.5,
    "BrandId": 1,
    "CategoryId": 5,
    "IsActive": true,
    "variants": [
      {
        "Id": 1,
        "Sku": "SKU001-V1",
        "Barcode": "5413503555629",
        "PriceOverride": null,
        "StockQuantity": 50
      }
    ]
  }
]
```

## Utilisation

### 1. Charger le catalogue au démarrage

Dans votre code principal (ex: `main.py` ou `api/main.py`):

```python
from app.utils.product_catalog import load_catalog

# Charger depuis un fichier JSON
load_catalog(json_path='data/product_catalog.json')

# Ou charger depuis des données Python
catalog_data = [...]  # Liste de dictionnaires
load_catalog(catalog_data=catalog_data)
```

### 2. Utilisation dans les extracteurs IA

Le catalogue est automatiquement intégré dans les prompts IA pour:
- Identifier les produits extraits
- Enrichir les produits avec les informations du catalogue
- Matcher par SKU, EAN, Barcode, GTIN, ou description

Les extracteurs suivants utilisent automatiquement le catalogue:
- `extract_products_with_ai()` (OpenAI)
- `extract_products_with_gemini()` (Gemini)
- `extract_products_with_ollama()` (Ollama)

### 3. Utilisation manuelle dans Python

```python
from app.utils.product_catalog import get_catalog, enrich_products_with_catalog

# Obtenir le catalogue
catalog = get_catalog()

# Matcher un produit
matched = catalog.match_product(
    sku="00184088",
    ean="5413503555628",
    description="Spijkerplug 50 x 6 mm"
)

# Enrichir une liste de produits
products = [
    {
        "sku": "00184088",
        "ean": "5413503555628",
        "description": "Spijkerplug 50 x 6 mm",
        "qty": 10,
        "unit_price": 3.17
    }
]

enriched_products = enrich_products_with_catalog(products)
```

## Priorité de Matching

Le système utilise la priorité suivante pour matcher les produits:

1. **EAN** (13 chiffres) - Le plus fiable
2. **SKU** - Code produit
3. **Barcode** - Code-barres
4. **GTIN** - Global Trade Item Number
5. **Reference** - Référence produit
6. **Description** - Match partiel sur les noms/descriptions

## Champs Ajoutés au Produit

Quand un produit est matché avec le catalogue, les champs suivants sont ajoutés:

- `catalog_id`: ID du produit dans le catalogue
- `catalog_reference`: Référence du catalogue
- `catalog_sku`: SKU du catalogue
- `catalog_name_nl/fr/en`: Noms du produit (NL, FR, EN)
- `catalog_description_nl/fr/en`: Descriptions du produit (NL, FR, EN)
- `catalog_selling_price`: Prix de vente
- `catalog_cost_price`: Prix de revient
- `catalog_stock_quantity`: Quantité en stock
- `catalog_weight_kg`: Poids en kg
- `catalog_brand_id`: ID de la marque
- `catalog_category_id`: ID de la catégorie
- `catalog_matched`: `true` si trouvé, `false` sinon

## Exemple Complet

```python
from app.utils.product_catalog import load_catalog, enrich_products_with_catalog

# 1. Charger le catalogue au démarrage
load_catalog(json_path='data/product_catalog.json')

# 2. Extraire les produits d'une facture
products = extract_products_with_ai('invoice.pdf')

# 3. Les produits sont automatiquement enrichis avec le catalogue
# Chaque produit aura les champs catalog_* si un match est trouvé

# 4. Utiliser les informations du catalogue
for product in products:
    if product.get('catalog_matched'):
        print(f"Produit trouvé: {product['catalog_name_nl']}")
        print(f"Prix catalogue: {product['catalog_selling_price']} EUR")
        print(f"Stock: {product['catalog_stock_quantity']}")
```

## Configuration

Le catalogue peut être configuré via:
- Variable d'environnement `PRODUCT_CATALOG_PATH` pour le chemin du fichier JSON
- Fichier par défaut: `app/data/product_catalog.json`

## Notes

- Le catalogue est chargé une seule fois au démarrage pour des performances optimales
- Les index sont construits automatiquement pour une recherche rapide
- Seuls les produits actifs (`IsActive: true`) sont indexés
- Les variantes de produits sont également indexées pour le matching
