# Correction : Validation et Comptabilisation des Documents Liés

## Problème Identifié

Le bouton "Valider et comptabiliser" ne créait qu'un seul type de document ERP selon le type du document validé :
- Si on validait une **facture**, seule la facture fournisseur était créée
- Si on validait un **bon de livraison (BL)**, seule la réception était créée

**Résultat attendu** : Quand une facture est liée à un BL (ou vice versa), la validation devrait créer **les deux** documents ERP (facture fournisseur ET réception).

## Solution Implémentée

### Modifications dans `SupplierDocumentService.cs`

1. **Récupération des documents liés** (ligne 442-455)
   - La méthode `ValidateAndPostAsync` récupère maintenant les documents liés via `GetLinkedDocumentsAsync`
   - Filtre les documents déjà postés pour éviter les doublons
   - Vérifie que les lignes des documents liés sont chargées

2. **Création conditionnelle des deux types de documents** (lignes 616-648)
   - **Facture fournisseur** : Créée si le document actuel est une facture OU si une facture liée existe
   - **Réception** : Créée si le document actuel est un BL OU si un BL lié existe

3. **Méthodes helper créées** (lignes 658-850)
   - `CreateSupplierInvoiceFromDocumentAsync` : Extrait la logique de création de facture fournisseur
   - `CreateReceiptFromDocumentAsync` : Extrait la logique de création de réception
   - Ces méthodes gèrent le matching des produits, le calcul des totaux, et la création des lignes

4. **Mise à jour du statut des documents liés** (lignes 650-666)
   - Après création réussie, le statut des documents liés est aussi mis à jour à `Posted`
   - Évite de re-traiter les mêmes documents

## Comportement Attendu

### Scénario 1 : Validation d'une facture avec BL lié
- ✅ Crée une **facture fournisseur** depuis la facture
- ✅ Crée une **réception** depuis le BL lié
- ✅ Met à jour le statut des deux documents à `Posted`

### Scénario 2 : Validation d'un BL avec facture liée
- ✅ Crée une **réception** depuis le BL
- ✅ Crée une **facture fournisseur** depuis la facture liée
- ✅ Met à jour le statut des deux documents à `Posted`

### Scénario 3 : Validation d'un document sans lien
- ✅ Crée uniquement le document ERP correspondant au type du document validé

## Fichiers Modifiés

- `D:\GitHub\EuroBrico.Web.Api\backend\ERP.Application\Purchases\SupplierDocumentService.cs`
  - Méthode `ValidateAndPostAsync` : Ajout de la logique pour traiter les documents liés
  - Nouvelle méthode `CreateSupplierInvoiceFromDocumentAsync` : Création de facture fournisseur
  - Nouvelle méthode `CreateReceiptFromDocumentAsync` : Création de réception

## Prochaines Étapes

1. **Recompiler le projet** `EuroBrico.Web.Api`
2. **Tester le scénario** :
   - Associer une facture et un BL
   - Valider la facture (ou le BL)
   - Vérifier que les deux documents ERP sont créés
3. **Vérifier les logs** dans `logs/eurobrico-YYYY-MM-DD.log` pour confirmer la création des deux documents

## Notes Techniques

- Les documents liés doivent être dans un statut `Matched` ou `Validated` pour être traités
- Les documents déjà `Posted` sont ignorés pour éviter les doublons
- Les lignes des documents liés sont automatiquement rechargées si elles ne sont pas déjà en mémoire
- Le matching des produits est effectué pour chaque document avant création
