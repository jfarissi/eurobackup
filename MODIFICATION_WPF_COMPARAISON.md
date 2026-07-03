# Modification effectuée : Utilisation de l'endpoint backend pour la comparaison

## Fichier modifié

**`D:\GitHub\Pulse.Desktop\ViewModels\SupplierDocumentViewModel.cs`**

## Modification effectuée

### Avant (Comparaison côté client)
La méthode `LoadComparisons()` effectuait la comparaison des quantités **côté client** en itérant sur les lignes de la facture et du BL, ce qui :
- Ne générait pas de logs dans le backend
- Utilisait une logique de matching simplifiée
- Ne bénéficiait pas des améliorations du backend (grouping, normalisation robuste, etc.)

### Après (Appel à l'endpoint backend)
La méthode `LoadComparisons()` appelle maintenant l'endpoint backend :
```csharp
var quantityComparisons = await _supplierDocumentsApi.CompareQuantitiesAsync(
    invoiceDoc.Id, 
    deliveryDoc.Id);
```

## Avantages

1. **Logs détaillés** : Les logs de `DocumentMatchingService` apparaîtront maintenant dans `logs/eurobrico-YYYY-MM-DD.log`
2. **Logique centralisée** : Utilise la même logique robuste que le backend :
   - Grouping des quantités par SKU/EAN/Description
   - Normalisation robuste des noms de produits
   - Trimming des valeurs SKU/EAN
   - Gestion de la précision décimale
3. **Cohérence** : Garantit que tous les clients utilisent la même logique de comparaison

## Endpoint utilisé

**`POST /api/supplierdocuments/compare-quantities`**

Body :
```json
{
  "InvoiceId": "guid",
  "DeliveryNoteId": "guid?"
}
```

Retourne : `List<QuantityComparisonDto>`

## Prochaines étapes

1. **Recompiler** le projet `Pulse.Desktop`
2. **Tester** la comparaison dans l'application WPF
3. **Vérifier les logs** dans `D:\GitHub\EuroBrico.Web.Api\EuroBrico.Web.Api.Server\logs\eurobrico-YYYY-MM-DD.log`

## Logs attendus

Une fois que vous exécuterez une comparaison, vous devriez voir dans les logs :

```
[INF] ========== CompareQuantitiesAsync START ==========
[INF] Invoice ID: {guid}, Invoice Lines count: {count}
[INF] DeliveryNote ID: {guid}, DeliveryNote Lines count: {count}
[INF] 📋 Indexing {count} invoice lines...
[DBG] Invoice Line: SKU='{sku}', EAN='{ean}', Desc='{desc}', Qty={qty}
[INF] 📊 Comparison: Key={key}, InvoiceQty={qty}, DeliveryQty={qty}, Diff={diff}
[INF] ========== COMPARISON RESULTS ==========
```

## Note importante

La comparaison des **prix** (facture vs base de données) reste côté client car elle ne nécessite pas de logs détaillés et utilise une logique différente (comparaison avec les prix en base, pas entre documents).

## Vérification

Pour vérifier que la modification fonctionne :

1. Ouvrez l'application WPF `Pulse.Desktop`
2. Sélectionnez une facture avec un BL lié
3. Allez dans l'onglet "Quantités"
4. Les logs devraient apparaître dans `logs/eurobrico-YYYY-MM-DD.log`

Si les logs n'apparaissent pas, vérifiez :
- Que l'application WPF est bien connectée au backend `EuroBrico.Web.Api`
- Que le niveau de log est configuré sur `Debug` pour `DocumentMatchingService` dans `appsettings.Development.json`
