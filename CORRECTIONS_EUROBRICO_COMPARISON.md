# Corrections apportées à EuroBrico.Web.Api - DocumentMatchingService

## Problèmes identifiés

### 1. ❌ Pas de groupement des quantités
**Problème** : Si un produit apparaissait plusieurs fois dans le BL ou la facture, seule la dernière quantité était conservée (écrasement).

**Exemple** :
- BL ligne 1 : SKU="ABC123", Qty=5
- BL ligne 2 : SKU="ABC123", Qty=3
- **Avant** : Seulement Qty=3 était conservée
- **Après** : Qty=8 (somme des deux lignes)

### 2. ❌ Normalisation insuffisante des descriptions
**Problème** : La normalisation utilisait seulement `Trim().ToUpperInvariant()`, ce qui ne suffisait pas pour faire correspondre des descriptions similaires mais avec des variations.

**Exemple** :
- Facture : "Betonmortel C20/25 25 kg"
- BL : "Betonmortel C20/25 - 25kg"
- **Avant** : Ne correspondaient pas (normalisation trop simple)
- **Après** : Correspondent (normalisation robuste)

### 3. ❌ Pas de trim sur SKU/EAN
**Problème** : Les SKU et EAN n'étaient pas trimés avant la comparaison, ce qui pouvait causer des problèmes avec des espaces.

## Corrections appliquées

### 1. ✅ Groupement et sommation des quantités
Les quantités sont maintenant **sommées** si plusieurs lignes ont la même clé (SKU, EAN, ou description normalisée).

```csharp
// AVANT (écrasement)
invoiceQuantitiesBySku[line.SupplierSku] = line.Qty;

// APRÈS (somme)
var sku = line.SupplierSku.Trim();
if (invoiceQuantitiesBySku.ContainsKey(sku))
    invoiceQuantitiesBySku[sku] += line.Qty;
else
    invoiceQuantitiesBySku[sku] = line.Qty;
```

### 2. ✅ Normalisation robuste des descriptions
Ajout d'une méthode `NormalizeProductName()` qui :
- Convertit en minuscules
- Supprime les accents
- Supprime les parenthèses/crochets/accolades
- Supprime les unités et tailles (kg, g, l, ml, cm, mm, etc.)
- Supprime les codes produits (abc123, ker00022, etc.)
- Supprime les nombres isolés
- Supprime les séparateurs (-, _, /, \)
- Garde seulement les lettres et espaces
- Réduit les espaces multiples
- Limite à 120 caractères

```csharp
// AVANT
var normalizedDesc = line.Description.Trim().ToUpperInvariant();

// APRÈS
var normalizedDesc = NormalizeProductName(line.Description);
```

### 3. ✅ Trim sur SKU et EAN
Les SKU et EAN sont maintenant trimés avant la comparaison pour éviter les problèmes d'espaces.

```csharp
// AVANT
if (!string.IsNullOrEmpty(line.SupplierSku))
{
    invoiceQuantitiesBySku[line.SupplierSku] = line.Qty;
}

// APRÈS
if (!string.IsNullOrEmpty(line.SupplierSku))
{
    var sku = line.SupplierSku.Trim();
    // ...
}
```

## Impact sur pulse.desktop WPF

Ces corrections devraient résoudre les différences d'affichage dans `pulse.desktop` car :

1. **Les quantités sont maintenant correctement groupées** : Si un produit apparaît plusieurs fois, les quantités sont sommées au lieu d'être écrasées.

2. **Les descriptions sont mieux normalisées** : Les produits avec des descriptions similaires mais avec des variations (espaces, accents, unités) sont maintenant correctement appariés.

3. **Les SKU/EAN sont normalisés** : Les espaces en début/fin sont supprimés, ce qui améliore la correspondance.

## Tests recommandés

1. **Test avec produits dupliqués** :
   - Créer un BL avec 2 lignes ayant le même SKU mais des quantités différentes
   - Vérifier que la quantité totale est correctement affichée

2. **Test avec descriptions similaires** :
   - Créer un BL et une facture avec des descriptions légèrement différentes
   - Vérifier qu'elles sont correctement appariées

3. **Test avec SKU/EAN avec espaces** :
   - Créer des documents avec des SKU/EAN ayant des espaces en début/fin
   - Vérifier qu'ils sont correctement appariés

## Fichiers modifiés

- `D:\GitHub\EuroBrico.Web.Api\backend\ERP.Application\Purchases\DocumentMatchingService.cs`

## Méthodes ajoutées

- `NormalizeProductName(string productName)` : Normalise un nom de produit pour la correspondance
- `RemoveDiacritics(string text)` : Supprime les accents d'une chaîne

## Prochaines étapes

1. Recompiler le projet EuroBrico.Web.Api
2. Redéployer le backend
3. Tester dans pulse.desktop WPF
4. Vérifier que les différences d'affichage sont corrigées
