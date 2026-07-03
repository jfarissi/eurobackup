# Diagnostic : Logs de comparaison non visibles

## Problème identifié

Les logs de `DocumentMatchingService.CompareQuantitiesAsync` n'apparaissent pas dans le fichier de logs car **l'endpoint de comparaison n'est pas appelé** par le projet WPF.

## Endpoints disponibles pour la comparaison

Le backend `EuroBrico.Web.Api` expose **2 endpoints** pour la comparaison :

### 1. Endpoint de matching complet (recommandé)
```
POST /api/supplierdocuments/{invoiceDocumentId}/match-with-delivery-note
```
- **Fonction** : Trouve automatiquement le BL correspondant et compare les quantités + prix
- **Appelle** : `DocumentMatchingService.MatchInvoiceWithDeliveryNoteAsync()`
- **Retourne** : `DocumentMatchResultDto` avec :
  - Facture et BL trouvé
  - Comparaisons de quantités
  - Comparaisons de prix

### 2. Endpoint de comparaison de quantités uniquement
```
POST /api/supplierdocuments/compare-quantities
Body: { "InvoiceId": "guid", "DeliveryNoteId": "guid?" }
```
- **Fonction** : Compare uniquement les quantités entre facture et BL
- **Appelle** : `DocumentMatchingService.CompareQuantitiesAsync()`
- **Retourne** : Liste de `QuantityComparisonDto`

## Ce que le WPF appelle actuellement

D'après les logs, le WPF appelle :
- `GET /api/supplierdocuments/{id}/linked-documents` - Récupère les documents liés
- `GET /api/supplierdocuments/{id}` - Récupère un document avec ses lignes

**Conclusion** : Le WPF fait probablement la comparaison **côté client** en utilisant les données retournées, au lieu d'utiliser l'endpoint de comparaison du backend.

## Solution : Modifier le WPF pour appeler l'endpoint de comparaison

Pour voir les logs de `DocumentMatchingService`, le projet WPF doit appeler l'un des endpoints ci-dessus.

### Option 1 : Utiliser l'endpoint de matching complet (recommandé)

```csharp
// Dans votre service WPF
public async Task<DocumentMatchResultDto> MatchInvoiceWithDeliveryNoteAsync(Guid invoiceDocumentId)
{
    var response = await _httpClient.PostAsync(
        $"/api/supplierdocuments/{invoiceDocumentId}/match-with-delivery-note",
        null);
    
    var result = await response.Content.ReadFromJsonAsync<ApiResult<DocumentMatchResultDto>>();
    return result.Data;
}
```

### Option 2 : Utiliser l'endpoint de comparaison de quantités

```csharp
// Dans votre service WPF
public async Task<List<QuantityComparisonDto>> CompareQuantitiesAsync(
    Guid invoiceId, 
    Guid? deliveryNoteId)
{
    var request = new CompareQuantitiesRequest
    {
        InvoiceId = invoiceId,
        DeliveryNoteId = deliveryNoteId
    };
    
    var response = await _httpClient.PostAsJsonAsync(
        "/api/supplierdocuments/compare-quantities",
        request);
    
    var result = await response.Content.ReadFromJsonAsync<ApiResult<List<QuantityComparisonDto>>>();
    return result.Data;
}
```

## Avantages d'utiliser l'endpoint backend

1. **Logs détaillés** : Vous verrez tous les logs de `DocumentMatchingService` dans `logs/eurobrico-YYYY-MM-DD.log`
2. **Logique centralisée** : La comparaison se fait côté serveur avec la même logique pour tous les clients
3. **Performance** : Le serveur peut optimiser les requêtes à la base de données
4. **Cohérence** : Garantit que tous les clients utilisent la même logique de comparaison

## Vérification après modification

Une fois que le WPF appelle l'endpoint, vous devriez voir dans les logs :

```
[INF] ========== CompareQuantitiesAsync START ==========
[INF] Invoice ID: {guid}, Invoice Lines count: {count}
[INF] DeliveryNote ID: {guid}, DeliveryNote Lines count: {count}
[INF] 📋 Indexing {count} invoice lines...
[DBG] Invoice Line: SKU='{sku}', EAN='{ean}', Desc='{desc}', Qty={qty}
[INF] 📊 Comparison: Key={key}, InvoiceQty={qty}, DeliveryQty={qty}, Diff={diff}
[INF] ========== COMPARISON RESULTS ==========
```

## Prochaines étapes

1. **Identifier dans le code WPF** où la comparaison est actuellement effectuée
2. **Remplacer la logique côté client** par un appel à l'endpoint backend
3. **Tester** et vérifier que les logs apparaissent dans `logs/eurobrico-YYYY-MM-DD.log`
4. **Analyser les logs** pour diagnostiquer les problèmes de quantités
