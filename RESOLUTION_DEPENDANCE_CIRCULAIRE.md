# Résolution de la dépendance circulaire

## Problème identifié

Une dépendance circulaire empêchait le démarrage de l'application :

```
SupplierDocumentService → IStockUpdateService → ISupplierDocumentService → SupplierDocumentService
```

## Solution implémentée

### Modification dans `StockUpdateService.cs`

**Avant** :
- `StockUpdateService` dépendait de `ISupplierDocumentService` pour récupérer les documents via `GetByIdAsync()`

**Après** :
- `StockUpdateService` utilise directement `IStorageBroker` pour récupérer les documents via `SelectSupplierDocumentByIdAsync()`
- Suppression de la dépendance `ISupplierDocumentService` du constructeur

### Changements effectués

1. **Retrait de `ISupplierDocumentService`** du constructeur de `StockUpdateService`
2. **Utilisation directe de `IStorageBroker`** dans `UpdateStockFromDeliveryNoteAsync` :
   ```csharp
   // Avant
   var deliveryNote = await _supplierDocumentService.GetByIdAsync(deliveryNoteDocumentId);
   
   // Après
   var deliveryNote = await _storageBroker.SelectSupplierDocumentByIdAsync(deliveryNoteDocumentId);
   ```

## Chaîne de dépendances résolue

**Avant** (circulaire) :
```
SupplierDocumentService
  → IStockUpdateService
    → ISupplierDocumentService
      → SupplierDocumentService ❌ (boucle)
```

**Après** (linéaire) :
```
SupplierDocumentService
  → IStockUpdateService
    → IStorageBroker ✅ (pas de boucle)
```

## Fichiers modifiés

- `D:\GitHub\EuroBrico.Web.Api\backend\ERP.Application\Inventory\StockUpdateService.cs`
  - Retrait de `ISupplierDocumentService` du constructeur
  - Utilisation directe de `IStorageBroker.SelectSupplierDocumentByIdAsync()` dans `UpdateStockFromDeliveryNoteAsync`

## Notes importantes

- `IStorageBroker` est une couche d'accès aux données de bas niveau, donc elle ne crée pas de dépendance circulaire
- La méthode `UpdateStockFromPurchaseReceiptAsync` n'était pas affectée car elle reçoit directement l'objet `Receipt` en paramètre
- La méthode `UpdateStockFromSalesDeliveryNoteAsync` n'était pas affectée car elle reçoit directement l'objet `DeliveryNote` en paramètre

## Prochaines étapes

1. **Recompiler le projet** `EuroBrico.Web.Api`
2. **Vérifier que l'application démarre** sans erreur de dépendance circulaire
3. **Tester la validation** d'un document pour confirmer que tout fonctionne

La dépendance circulaire est maintenant résolue et l'application devrait démarrer correctement.
