# Correction finale - Comparaison des quantités groupées

## Problème identifié

Le code comparait les **lignes individuelles** au lieu d'utiliser les **quantités groupées** créées dans les dictionnaires. Cela causait :

1. **Plusieurs comparaisons pour le même produit** : Si un produit apparaissait 3 fois dans la facture, on créait 3 comparaisons au lieu d'une seule avec la quantité totale.

2. **Quantités incorrectes** : On utilisait `invoiceLine.Qty` (quantité de la ligne individuelle) au lieu de la quantité groupée depuis le dictionnaire.

3. **Problèmes de correspondance** : Les SKU/EAN n'étaient pas trimés lors de la recherche, alors qu'ils étaient trimés lors de l'indexation.

## Corrections appliquées

### 1. ✅ Utilisation des quantités groupées pour la comparaison

**AVANT** : Itération sur les lignes individuelles
```csharp
foreach (var invoiceLine in invoice.Lines)
{
    var invoiceQty = invoiceLine.Qty; // ❌ Quantité individuelle
    // ...
}
```

**APRÈS** : Utilisation des dictionnaires groupés
```csharp
// Collecter toutes les clés uniques de la facture
foreach (var kvp in invoiceQuantitiesBySku)
{
    allInvoiceKeys.Add($"SKU:{kvp.Key}");
}
// Comparer chaque clé unique avec sa quantité groupée
foreach (var invoiceKey in allInvoiceKeys)
{
    var skuValue = invoiceKey.Substring(4);
    invoiceQty = invoiceQuantitiesBySku[skuValue]; // ✅ Quantité groupée
    // ...
}
```

### 2. ✅ Arrondi des différences

Ajout d'un arrondi à 2 décimales pour éviter les problèmes de précision :
```csharp
Difference = deliveryNoteQty.HasValue 
    ? Math.Round(invoiceQty - deliveryNoteQty.Value, 2) 
    : invoiceQty
```

### 3. ✅ Tolérance pour HasDifference

Modification de `HasDifference` pour utiliser une tolérance de 0.01 au lieu d'une comparaison exacte :
```csharp
// AVANT
public bool HasDifference => Difference != 0;

// APRÈS
public bool HasDifference => Math.Abs(Difference) > 0.01m;
```

## Impact

Maintenant, si un produit apparaît plusieurs fois :
- **Facture** : SKU="ABC123" apparaît 2 fois avec Qty=5 et Qty=3 → **Quantité totale = 8**
- **BL** : SKU="ABC123" apparaît 1 fois avec Qty=8 → **Quantité totale = 8**
- **Résultat** : **1 seule comparaison** avec InvoiceQuantity=8, DeliveryNoteQuantity=8, Difference=0 ✅

## Fichiers modifiés

1. `D:\GitHub\EuroBrico.Web.Api\backend\ERP.Application\Purchases\DocumentMatchingService.cs`
   - Réécriture de la logique de comparaison pour utiliser les quantités groupées
   - Ajout d'arrondi pour les différences

2. `D:\GitHub\EuroBrico.Web.Api\backend\ERP.Application\Purchases\IDocumentMatchingService.cs`
   - Modification de `HasDifference` pour utiliser une tolérance

## Tests recommandés

1. **Test avec produit dupliqué** :
   - Facture : 2 lignes avec SKU="ABC123", Qty=5 et Qty=3
   - BL : 1 ligne avec SKU="ABC123", Qty=8
   - **Attendu** : 1 comparaison avec Difference=0

2. **Test avec quantités égales mais lignes multiples** :
   - Facture : 3 lignes avec SKU="XYZ789", Qty=2, Qty=2, Qty=1
   - BL : 2 lignes avec SKU="XYZ789", Qty=3, Qty=2
   - **Attendu** : 1 comparaison avec InvoiceQuantity=5, DeliveryNoteQuantity=5, Difference=0

3. **Test avec précision décimale** :
   - Facture : Qty=10.0
   - BL : Qty=10.00
   - **Attendu** : Difference=0 (grâce à l'arrondi et la tolérance)

## Prochaines étapes

1. ✅ Recompiler le projet EuroBrico.Web.Api
2. ✅ Redéployer le backend
3. ✅ Tester dans pulse.desktop WPF
4. ✅ Vérifier que les différences disparaissent quand les quantités sont égales

## Note importante

Si le WPF affiche toujours des différences après ces corrections, vérifier :

1. **Le WPF utilise-t-il `HasDifference` ou compare-t-il directement `Difference` ?**
   - Utiliser `HasDifference` pour l'affichage (prend en compte la tolérance)
   - Ne pas comparer `Difference == 0` directement (problèmes de précision)

2. **Le WPF groupe-t-il les lignes avant l'affichage ?**
   - Si le WPF re-groupe les lignes, il doit utiliser la même logique que le backend

3. **Les logs de débogage** :
   - Vérifier les logs console pour voir les quantités groupées retournées par l'API
   - Comparer avec ce qui est affiché dans le WPF
