# Parser de Catalogue Produit

Ce module permet de détecter et parser les catalogues produits PDF pour extraire les produits selon la structure des tables SQL.

## Fonctionnalités

1. **Détection automatique** : Détecte si un PDF est un catalogue produit
2. **Parsing avec IA** : Utilise OpenAI ou Gemini pour extraire les produits
3. **Structure SQL** : Retourne les données selon la structure des tables SQL :
   - `Products` : Produits principaux
   - `ProductVariants` : Variantes de produits
   - `ProductImages` : Images des produits
   - `ProductAttributeValues` : Attributs des produits

## Utilisation

### Endpoint API

#### 1. Endpoint `/parse` (détection automatique)

L'endpoint `/parse` détecte automatiquement si le document est un catalogue ou une facture :

```bash
POST /parse?use_ai=true&ai_provider=openai
Content-Type: multipart/form-data
file: catalogue.pdf
```

**Réponse pour un catalogue :**
```json
{
  "type": "catalog",
  "products": [...],
  "variants": [...],
  "images": [...],
  "attributes": [...],
  "count": 150
}
```

**Réponse pour une facture :**
```json
{
  "type": "invoice",
  "items": [...],
  "metadata": {...}
}
```

#### 2. Endpoint dédié `/catalog/parse`

Endpoint spécifique pour les catalogues :

```bash
POST /catalog/parse?use_ai=true&ai_provider=openai
Content-Type: multipart/form-data
file: catalogue.pdf
```

**Réponse :**
```json
{
  "type": "catalog",
  "products": [
    {
      "Id": 1,
      "Reference": "REF001",
      "Sku": "SKU001",
      "Barcode": "5413503555628",
      "Gtin": "5413503555628",
      "Name_EN": "Product Name English",
      "Name_FR": "Nom du produit français",
      "Name_NL": "Productnaam Nederlands",
      "Description_EN": "Full description",
      "Description_FR": "Description complète",
      "Description_NL": "Volledige beschrijving",
      "SellingPrice": 10.50,
      "CostPrice": 8.00,
      "WeightKg": 0.5,
      "LengthCm": 10.0,
      "WidthCm": 5.0,
      "HeightCm": 3.0,
      "MinOrderQuantity": 1,
      "IsActive": true
    }
  ],
  "variants": [
    {
      "Id": 1,
      "ProductId": 1,
      "Sku": "SKU001-V1",
      "Barcode": "5413503555629",
      "PriceOverride": null,
      "Weight": 0.6,
      "Length": 11.0,
      "Width": 5.0,
      "Height": 3.0,
      "IsActive": true
    }
  ],
  "images": [
    {
      "Id": 1,
      "ProductId": 1,
      "Url": "https://example.com/image.jpg",
      "AltText": "Product image",
      "IsMain": true,
      "SortOrder": 1
    }
  ],
  "attributes": [
    {
      "Id": 1,
      "ProductId": 1,
      "AttributeId": 1,
      "Value": "Red"
    }
  ],
  "count": 1,
  "method": "catalog_parser_openai"
}
```

### Utilisation en Python

```python
from app.catalog_parser import is_catalog, parse_catalog

# Détecter si c'est un catalogue
pdf_text = extract_text_from_pdf("document.pdf")
if is_catalog(pdf_text):
    # Parser le catalogue
    result = parse_catalog("document.pdf", use_ai=True, ai_provider="openai")
    
    products = result["products"]
    variants = result["variants"]
    images = result["images"]
    attributes = result["attributes"]
```

## Détection de Catalogue

La fonction `is_catalog()` détecte un catalogue en cherchant :

### Mots-clés indicateurs :
- "catalogue", "catalog"
- "product catalog", "produktkatalog"
- "productenlijst", "prijslijst"
- "price list", "prijsnota"
- "product list", "assortiment"
- "product range"

### Exclusion :
- Si le document contient des mots-clés de facture/BL ("factuur", "invoice", "leveringsbon", etc.), ce n'est pas un catalogue
- Si le document contient des métadonnées de facture (numéro, date, client), ce n'est pas un catalogue

## Structure des Données

### Table Products
Tous les champs de la table `Products` :
- `Id`, `Reference`, `Sku`, `Barcode`, `Gtin`
- `Name_EN`, `Name_FR`, `Name_NL`
- `Description_EN`, `Description_FR`, `Description_NL`
- `ShortDescription_EN`, `ShortDescription_FR`, `ShortDescription_NL`
- `SellingPrice`, `CostPrice`, `StockQuantity`
- `WeightKg`, `LengthCm`, `WidthCm`, `HeightCm`
- `MinOrderQuantity`, `IsActive`, `BrandId`, `CategoryId`
- etc.

### Table ProductVariants
- `Id`, `ProductId`, `Sku`, `Barcode`
- `PriceOverride`, `StockQuantity`
- `Weight`, `Length`, `Width`, `Height`
- `IsActive`, `AttributesJson`

### Table ProductImages
- `Id`, `ProductId`, `Url`, `AltText`
- `IsMain`, `SortOrder`

### Table ProductAttributeValues
- `Id`, `ProductId`, `AttributeId`, `Value`

## Configuration

Variables d'environnement :
- `OPENAI_API_KEY` : Clé API OpenAI
- `GEMINI_API_KEY` : Clé API Gemini
- `USE_AI_CATALOG` : "true" pour utiliser l'IA (défaut: "true")
- `OPENAI_MODEL` : Modèle OpenAI (défaut: "gpt-4o")
- `GEMINI_MODEL` : Modèle Gemini (défaut: "gemini-1.5-flash")

## Exemple Complet

```python
from app.catalog_parser import is_catalog, parse_catalog
from app.utils.pdf_extractor import extract_text_from_pdf

# 1. Extraire le texte
pdf_text = extract_text_from_pdf("catalogue.pdf")

# 2. Détecter si c'est un catalogue
if is_catalog(pdf_text):
    # 3. Parser le catalogue
    result = parse_catalog("catalogue.pdf", use_ai=True, ai_provider="openai")
    
    # 4. Utiliser les données
    for product in result["products"]:
        print(f"Produit: {product['Name_NL']}")
        print(f"SKU: {product['Sku']}")
        print(f"Prix: {product['SellingPrice']} EUR")
        print(f"Variantes: {len([v for v in result['variants'] if v['ProductId'] == product['Id']])}")
```

## Notes

- Les catalogues peuvent être très longs, le texte est limité à 50000 caractères pour éviter les limites de tokens
- Les IDs sont temporaires (1, 2, 3...) et doivent être remplacés par les vrais IDs de la base de données
- L'IA extrait les produits selon les informations disponibles dans le catalogue
- Si certains champs ne sont pas trouvés, ils sont mis à `null`
