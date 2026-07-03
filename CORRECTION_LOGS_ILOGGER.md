# Correction des Logs pour DocumentMatchingService

## Problème identifié

Les logs ajoutés précédemment utilisaient `System.Diagnostics.Debug.WriteLine` et `System.Console.WriteLine`, qui ne sont **pas capturés** dans le fichier `log.txt` standard d'ASP.NET Core. Ces logs apparaissent uniquement dans :
- La console de débogage de Visual Studio (Debug Output Window)
- La console de l'application (si elle est lancée en mode console)

## Solution appliquée

Tous les logs ont été migrés vers `ILogger<DocumentMatchingService>` qui est le système de logging standard d'ASP.NET Core. Les logs apparaîtront maintenant dans le fichier `log.txt` standard.

### Changements effectués

1. **Ajout de `ILogger<DocumentMatchingService>`** dans le constructeur
2. **Remplacement de tous les logs** :
   - `System.Diagnostics.Debug.WriteLine` → `_logger.LogDebug()` ou `_logger.LogInformation()`
   - `System.Console.WriteLine` → `_logger.LogInformation()`

### Niveaux de logs utilisés

- **`LogInformation`** : Pour les événements importants (début/fin de comparaison, résultats finaux)
- **`LogDebug`** : Pour les détails de chaque ligne traitée (peut être désactivé en production)

## Structure des logs dans log.txt

Les logs apparaîtront maintenant avec le format standard ASP.NET Core :

```
info: ERP.Application.Purchases.DocumentMatchingService[0]
      ========== CompareQuantitiesAsync START ==========
info: ERP.Application.Purchases.DocumentMatchingService[0]
      Invoice ID: {guid}, Invoice Lines count: {count}
info: ERP.Application.Purchases.DocumentMatchingService[0]
      📋 Indexing {count} invoice lines...
debug: ERP.Application.Purchases.DocumentMatchingService[0]
      Invoice Line: SKU='{sku}', EAN='{ean}', Desc='{desc}', Qty={qty}
info: ERP.Application.Purchases.DocumentMatchingService[0]
      📊 Comparison: Key={key}, InvoiceQty={qty}, DeliveryQty={qty}, Diff={diff}, MatchMethod={method}
info: ERP.Application.Purchases.DocumentMatchingService[0]
      ========== COMPARISON RESULTS ==========
```

## Prochaines étapes

1. **Recompiler** le projet `EuroBrico.Web.Api`
2. **Redémarrer** l'API backend
3. **Exécuter une comparaison** dans votre application WPF `pulse.desktop`
4. **Vérifier le fichier `log.txt`** pour voir les logs détaillés de la comparaison

## Recherche des logs dans log.txt

Pour trouver rapidement les logs de comparaison, recherchez :
- `DocumentMatchingService`
- `CompareQuantitiesAsync`
- `Comparison:`
- `COMPARISON RESULTS`

## Notes importantes

- Les logs `LogDebug` peuvent ne pas apparaître si le niveau de log est configuré sur `Information` ou supérieur dans `appsettings.json`
- Pour activer les logs Debug, assurez-vous que le niveau de log est configuré sur `Debug` pour `ERP.Application.Purchases.DocumentMatchingService` dans votre configuration de logging

## Exemple de configuration pour activer les logs Debug

Dans `appsettings.json` ou `appsettings.Development.json` :

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "ERP.Application.Purchases.DocumentMatchingService": "Debug"
    }
  }
}
```
