# Correction finale - Trim des valeurs SKU/EAN/Description

## Problème identifié

Les valeurs **SKU**, **EAN** et **Description** n'étaient **pas trimées** lors du stockage dans la base de données, mais la comparaison utilisait des valeurs trimées. Cela causait des problèmes de correspondance.

**Exemple** :
- Facture : SKU stocké comme `" ABC123 "` (avec espaces)
- BL : SKU stocké comme `"ABC123"` (sans espaces)
- **Résultat** : Ne correspondaient pas même si c'est le même produit

## Corrections appliquées

### 1. ✅ Trim dans OcrParsingService.cs

**Fichier** : `D:\GitHub\EuroBrico.Web.Api\EuroBrico.Web.Api.Server\Brokers\Parsing\OcrParsingService.cs`

**AVANT** :
```csharp
Sku = line.Sku ?? line.SupplierSku,
Ean = line.Ean,
ProductName = line.Description,
```

**APRÈS** :
```csharp
var sku = line.Sku ?? line.SupplierSku;
Sku = !string.IsNullOrWhiteSpace(sku) ? sku.Trim() : null,
Ean = !string.IsNullOrWhiteSpace(line.Ean) ? line.Ean.Trim() : null,
ProductName = !string.IsNullOrWhiteSpace(line.Description) ? line.Description.Trim() : null,
```

### 2. ✅ Trim dans SupplierDocumentService.cs

**Fichier** : `D:\GitHub\EuroBrico.Web.Api\backend\ERP.Application\Purchases\SupplierDocumentService.cs`

**AVANT** :
```csharp
SupplierSku = lineItem.Sku,
Ean = lineItem.Ean,
Description = lineItem.ProductName ?? string.Empty,
```

**APRÈS** :
```csharp
SupplierSku = !string.IsNullOrWhiteSpace(lineItem.Sku) ? lineItem.Sku.Trim() : null,
Ean = !string.IsNullOrWhiteSpace(lineItem.Ean) ? lineItem.Ean.Trim() : null,
Description = !string.IsNullOrWhiteSpace(lineItem.ProductName) ? lineItem.ProductName.Trim() : string.Empty,
```

### 3. ✅ Trim déjà présent dans DocumentMatchingService.cs

Le `DocumentMatchingService` trimme déjà les valeurs lors de l'indexation (lignes 175, 181, 189, etc.), donc maintenant les valeurs stockées et les valeurs utilisées pour la comparaison sont cohérentes.

## Impact

Maintenant, toutes les valeurs sont trimées :
1. **Lors du parsing** (OcrParsingService)
2. **Lors du stockage** (SupplierDocumentService)
3. **Lors de la comparaison** (DocumentMatchingService - déjà fait)

Cela garantit que :
- `" ABC123 "` = `"ABC123"` = `"abc123"` (après normalisation)
- Les produits sont correctement appariés même si les espaces diffèrent

## Fichiers modifiés

1. `D:\GitHub\EuroBrico.Web.Api\EuroBrico.Web.Api.Server\Brokers\Parsing\OcrParsingService.cs`
   - Trim des SKU, EAN, et Description lors de la conversion

2. `D:\GitHub\EuroBrico.Web.Api\backend\ERP.Application\Purchases\SupplierDocumentService.cs`
   - Trim des SKU, EAN, et Description lors de la création des lignes

## Prochaines étapes

1. ✅ Recompiler le projet EuroBrico.Web.Api
2. ✅ Redéployer le backend
3. ✅ **Important** : Re-parser les documents existants pour que les valeurs soient trimées dans la base de données
   - Ou créer un script de migration pour trimmer les valeurs existantes
4. ✅ Tester dans pulse.desktop WPF

## Note importante

Si des documents ont déjà été parsés et stockés **avant** cette correction, leurs valeurs peuvent encore contenir des espaces. Deux options :

1. **Re-parser les documents** : Supprimer et re-uploader les documents pour qu'ils soient re-parsés avec le nouveau code
2. **Script de migration** : Créer un script SQL pour trimmer les valeurs existantes :
   ```sql
   UPDATE SupplierDocumentLines 
   SET SupplierSku = TRIM(SupplierSku) 
   WHERE SupplierSku IS NOT NULL;
   
   UPDATE SupplierDocumentLines 
   SET Ean = TRIM(Ean) 
   WHERE Ean IS NOT NULL;
   
   UPDATE SupplierDocumentLines 
   SET Description = TRIM(Description) 
   WHERE Description IS NOT NULL;
   ```
