# Vérification des doublons avant création

## Problème identifié

Lors de la validation d'un document, le système créait des factures et réceptions **sans vérifier** si elles existaient déjà, ce qui pouvait entraîner :
- **Doublons de factures/réceptions** avec le même numéro
- **Erreurs de stock** : le stock était mis à jour plusieurs fois pour le même document
- **Incohérences dans les données** : plusieurs documents ERP pour le même document parsé

## Solution implémentée

### Modifications dans `SupplierDocumentService.cs`

1. **Injection de `IStockMovementsService`** (ligne 33)
   - Ajout de `ERP.Application.Inventory.IStockMovementsService _stockMovementsService` dans les dépendances
   - Injection dans le constructeur

2. **Vérification des factures existantes** (lignes 758-771)
   - Avant de créer une facture fournisseur, vérifie si une facture avec le même numéro existe déjà
   - Critères de vérification :
     - Même `CompanyId`
     - Même `InvoiceNumber`
     - Même `SupplierId`
   - Si une facture existe, la création est **ignorée** avec un log d'avertissement

3. **Vérification des réceptions existantes** (lignes 889-902)
   - Avant de créer une réception, vérifie si une réception avec le même numéro existe déjà
   - Critères de vérification :
     - Même `CompanyId`
     - Même `ReceiptNumber`
     - Même `SupplierId`
   - Si une réception existe, la création est **ignorée** avec un log d'avertissement

4. **Vérification des mouvements de stock existants** (lignes 904-934)
   - Avant de mettre à jour le stock, vérifie si des mouvements de stock existent déjà
   - Recherche des réceptions créées récemment (dans les 7 jours) pour le même fournisseur
   - Vérifie si ces réceptions ont déjà des mouvements de stock associés
   - Si des mouvements existent, la mise à jour du stock est **ignorée** avec un log d'avertissement

## Comportement attendu

### Scénario 1 : Validation d'un document déjà validé
- ✅ Vérifie si une facture/réception avec le même numéro existe déjà
- ✅ Si oui, **ignore la création** et log un avertissement
- ✅ **Ne met pas à jour le stock** si des mouvements existent déjà
- ✅ Met quand même à jour le statut du document à `Posted` (pour éviter de re-valider)

### Scénario 2 : Validation normale (première fois)
- ✅ Vérifie qu'aucune facture/réception n'existe avec le même numéro
- ✅ Crée la facture/réception
- ✅ Vérifie qu'aucun mouvement de stock n'existe déjà
- ✅ Met à jour le stock et crée les mouvements

### Scénario 3 : Double validation accidentelle
- ✅ La première validation crée les documents ERP et met à jour le stock
- ✅ La deuxième validation détecte les doublons et **ignore la création**
- ✅ Le stock n'est **pas mis à jour une deuxième fois**

## Critères de détection des doublons

### Factures
- **Numéro de facture** (`InvoiceNumber`)
- **Fournisseur** (`SupplierId`)
- **Société** (`CompanyId`)

### Réceptions
- **Numéro de réception** (`ReceiptNumber`)
- **Fournisseur** (`SupplierId`)
- **Société** (`CompanyId`)

### Mouvements de stock
- **Réceptions créées récemment** (dans les 7 jours) pour le même fournisseur
- **Mouvements de stock** liés à ces réceptions (`SourceDocumentId` = Receipt.Id, `SourceDocumentType` = "Receipt")

## Fichiers modifiés

- `D:\GitHub\EuroBrico.Web.Api\backend\ERP.Application\Purchases\SupplierDocumentService.cs`
  - Injection de `IStockMovementsService`
  - Vérification des factures existantes dans `CreateSupplierInvoiceFromDocumentAsync`
  - Vérification des réceptions existantes dans `CreateReceiptFromDocumentAsync`
  - Vérification des mouvements de stock existants avant mise à jour

## Logs générés

### Lorsqu'un doublon est détecté

```
[WRN] ⚠️ Invoice with number 'INV-2026-001' already exists (ID: {guid}). Skipping creation to avoid duplicate.
[INF] ⚠️ Invoice with number 'INV-2026-001' already exists (ID: {guid}). Skipping creation to avoid duplicate.
```

```
[WRN] ⚠️ Receipt with number 'REC-2026-001' already exists (ID: {guid}). Skipping creation to avoid duplicate.
[INF] ⚠️ Receipt with number 'REC-2026-001' already exists (ID: {guid}). Skipping creation to avoid duplicate.
```

```
[WRN] ⚠️ Stock movements already exist for receipt(s) related to document {guid} (5 movements found). Stock update will be skipped to avoid duplicate stock updates.
[INF] ⚠️ Stock movements already exist for receipt(s) related to document {guid} (5 movements found). Stock update will be skipped to avoid duplicate stock updates.
```

## Prochaines étapes

1. **Recompiler le projet** `EuroBrico.Web.Api`
2. **Tester le scénario** :
   - Valider un document une première fois
   - Essayer de le valider une deuxième fois
   - Vérifier que les doublons ne sont pas créés
   - Vérifier que le stock n'est pas mis à jour deux fois
3. **Vérifier les logs** dans `logs/eurobrico-YYYY-MM-DD.log` pour confirmer les avertissements de doublons

## Notes importantes

- Les vérifications sont effectuées **avant** la création des documents ERP
- Si un doublon est détecté, la méthode **retourne immédiatement** sans créer le document
- Le statut du document source est quand même mis à jour à `Posted` pour éviter les re-validations
- Les logs d'avertissement permettent de tracer les tentatives de création de doublons
- La vérification des mouvements de stock utilise une fenêtre de 7 jours pour détecter les réceptions récentes

## Améliorations possibles

1. **Retourner une exception** au lieu de simplement logger et retourner (pour informer l'utilisateur)
2. **Lier les documents ERP créés** au `SupplierDocument` source pour une meilleure traçabilité
3. **Permettre la réutilisation** d'une facture/réception existante au lieu de simplement ignorer la création
