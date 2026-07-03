# Mise à jour du stock lors de la validation

## Problème identifié

Lors du clic sur le bouton "Valider et comptabiliser", la réception était créée mais **le stock n'était pas mis à jour**. Le message dans le WPF indiquait que le stock serait mis à jour, mais cela n'était pas implémenté.

## Solution implémentée

### Modifications dans `SupplierDocumentService.cs`

1. **Injection de `IStockUpdateService`** (ligne 32)
   - Ajout de `ERP.Application.Inventory.IStockUpdateService _stockUpdateService` dans les dépendances
   - Injection dans le constructeur

2. **Appel à la mise à jour du stock** (lignes 910-930)
   - Après la création réussie d'une réception via `AddReceiptAsync`
   - Appel à `_stockUpdateService.UpdateStockFromPurchaseReceiptAsync(createdReceipt)`
   - Logging détaillé des résultats (succès, warnings, erreurs)

### Modifications dans `SupplierDocumentViewModel.cs` (WPF)

1. **Amélioration du message de confirmation** (lignes 1426-1450)
   - Le message prend maintenant en compte les documents liés
   - Si on valide une **facture avec BL lié** :
     - Création de la facture fournisseur
     - Création de la réception depuis le BL
     - Mise à jour du stock
   - Si on valide un **BL avec facture liée** :
     - Création de la réception
     - Mise à jour du stock
     - Création de la facture fournisseur

## Comportement attendu

### Scénario 1 : Validation d'une facture avec BL lié
- ✅ Crée une facture fournisseur
- ✅ Crée une réception depuis le BL lié
- ✅ **Met à jour le stock** pour chaque produit de la réception
- ✅ **Crée les mouvements de stock** correspondants
- ✅ Met à jour le statut des deux documents à `Posted`

### Scénario 2 : Validation d'un BL avec facture liée
- ✅ Crée une réception
- ✅ **Met à jour le stock** pour chaque produit
- ✅ **Crée les mouvements de stock** correspondants
- ✅ Crée une facture fournisseur depuis la facture liée
- ✅ Met à jour le statut des deux documents à `Posted`

### Scénario 3 : Validation d'un BL seul
- ✅ Crée une réception
- ✅ **Met à jour le stock** pour chaque produit
- ✅ **Crée les mouvements de stock** correspondants

## Détails techniques

### Service utilisé : `StockUpdateService.UpdateStockFromPurchaseReceiptAsync`

Cette méthode :
1. Parcourt toutes les lignes de la réception
2. Pour chaque ligne avec un `ProductId` :
   - Récupère le produit depuis la base de données
   - Calcule le stock avant et après (stock avant + quantité reçue)
   - Met à jour `product.StockQuantity`
   - Crée un `StockMovement` de type `In` (entrée)
   - Enregistre le mouvement dans la base de données

### Gestion des erreurs

- Les lignes sans `ProductId` génèrent un warning mais n'empêchent pas la mise à jour du stock pour les autres produits
- Les erreurs sont loggées mais n'interrompent pas le processus (sauf erreur critique)
- Le résultat de la mise à jour est loggé avec le nombre de produits mis à jour et de mouvements créés

## Fichiers modifiés

- `D:\GitHub\EuroBrico.Web.Api\backend\ERP.Application\Purchases\SupplierDocumentService.cs`
  - Injection de `IStockUpdateService`
  - Appel à `UpdateStockFromPurchaseReceiptAsync` après création de réception
  
- `D:\GitHub\Pulse.Desktop\ViewModels\SupplierDocumentViewModel.cs`
  - Amélioration du message de confirmation pour tenir compte des documents liés

## Prochaines étapes

1. **Recompiler le projet** `EuroBrico.Web.Api`
2. **Tester le scénario** :
   - Valider un BL (seul ou avec facture liée)
   - Vérifier que le stock est bien mis à jour dans la base de données
   - Vérifier que les mouvements de stock sont créés
3. **Vérifier les logs** dans `logs/eurobrico-YYYY-MM-DD.log` pour confirmer la mise à jour du stock

## Notes importantes

- Seules les lignes avec un `ProductId` (produits matchés) génèrent une mise à jour du stock
- Les lignes non matchées sont ignorées (avec un warning dans les logs)
- La mise à jour du stock se fait **après** la création de la réception, donc si la réception échoue, le stock n'est pas modifié
- Les mouvements de stock sont traçables et permettent de suivre l'historique des entrées/sorties
