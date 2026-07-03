# Configuration des Logs avec Serilog

## Modifications effectuées

1. **Ajout des packages Serilog** dans `EuroBrico.Web.Api.Server.csproj` :
   - `Serilog.AspNetCore` (version 9.0.0)
   - `Serilog.Sinks.File` (version 6.0.0)

2. **Configuration de Serilog** dans `Program.cs` :
   - Les logs sont écrits dans la **console** (pour le développement)
   - Les logs sont écrits dans un **fichier** dans le dossier `logs/`
   - Format du fichier : `eurobrico-YYYY-MM-DD.log` (un fichier par jour)
   - Conservation : 30 jours de logs maximum

3. **Configuration du niveau de log** dans `appsettings.Development.json` :
   - Niveau `Debug` activé pour `DocumentMatchingService` pour voir tous les détails

## Où trouver les logs

### Emplacement des fichiers de logs

Les fichiers de logs sont créés dans le dossier **`logs/`** à la racine du projet, c'est-à-dire :

```
D:\GitHub\EuroBrico.Web.Api\logs\eurobrico-2026-01-24.log
```

**Note importante** : Le dossier `logs/` est créé **automatiquement** lors du premier démarrage de l'application. Il ne sera pas dans le dossier `bin/` mais à la **racine du projet** (même niveau que `Program.cs`).

### Structure des fichiers

- **Format** : `eurobrico-YYYY-MM-DD.log`
- **Exemple** : `eurobrico-2026-01-24.log` pour les logs du 24 janvier 2026
- **Rotation** : Un nouveau fichier est créé chaque jour à minuit
- **Rétention** : Les fichiers sont conservés pendant 30 jours, puis supprimés automatiquement

## Format des logs

Chaque ligne de log contient :
```
2026-01-24 21:30:45.123 +01:00 [INF] ========== CompareQuantitiesAsync START ==========
```

Format : `{Timestamp} [{Level}] {Message}`

## Rechercher les logs de DocumentMatchingService

Pour trouver rapidement les logs de comparaison dans le fichier, recherchez :
- `DocumentMatchingService`
- `CompareQuantitiesAsync`
- `COMPARISON RESULTS`
- `Comparison:`

## Prochaines étapes

1. **Restaurer les packages NuGet** :
   ```bash
   dotnet restore
   ```

2. **Recompiler le projet** :
   ```bash
   dotnet build
   ```

3. **Démarrer l'application** :
   - Le dossier `logs/` sera créé automatiquement
   - Les logs commenceront à être écrits dans `logs/eurobrico-YYYY-MM-DD.log`

4. **Exécuter une comparaison** dans votre application WPF `pulse.desktop`

5. **Vérifier le fichier de logs** dans `D:\GitHub\EuroBrico.Web.Api\logs\eurobrico-YYYY-MM-DD.log`

## Notes importantes

- Les logs sont écrits **en temps réel** dans le fichier
- Les logs `Debug` ne seront visibles que si le niveau de log est configuré sur `Debug` (déjà configuré dans `appsettings.Development.json`)
- En production, vous pouvez ajuster le niveau de log dans `appsettings.json` pour réduire le volume de logs
- Le dossier `logs/` doit être ajouté au `.gitignore` si vous ne voulez pas versionner les logs

## Exemple de contenu du fichier de logs

```
2026-01-24 21:30:45.123 +01:00 [INF] ========== CompareQuantitiesAsync START ==========
2026-01-24 21:30:45.124 +01:00 [INF] Invoice ID: 73a0f794-62b2-4672-9824-1c8df91002fd, Invoice Lines count: 27
2026-01-24 21:30:45.125 +01:00 [INF] DeliveryNote ID: 886a8b34-4b3c-4683-883a-81ef614b971b, DeliveryNote Lines count: 27
2026-01-24 21:30:45.126 +01:00 [INF] 📋 Indexing 27 invoice lines...
2026-01-24 21:30:45.127 +01:00 [DBG] Invoice Line: SKU='545753', EAN='5413503590100', Desc='Flex-voegmortel beige 2kg (360)', Qty=8
2026-01-24 21:30:45.128 +01:00 [DBG]   SKU '545753': NEW = 8
2026-01-24 21:30:45.129 +01:00 [INF] ✅ Invoice indexed: 27 SKUs, 27 EANs, 27 Descriptions
2026-01-24 21:30:45.130 +01:00 [INF] 📊 Comparison: Key=SKU:545753, InvoiceQty=8, DeliveryQty=8, Diff=0, MatchMethod=SKU
2026-01-24 21:30:45.131 +01:00 [INF] ========== COMPARISON RESULTS ==========
2026-01-24 21:30:45.132 +01:00 [INF] Product: 'Flex-voegmortel beige 2kg (360)', SKU: '545753', InvoiceQty: 8, DeliveryQty: 8, Diff: 0, HasDiff: False
```
