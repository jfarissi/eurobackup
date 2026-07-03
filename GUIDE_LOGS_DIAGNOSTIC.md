# Guide de diagnostic avec logs - Comparaison des quantités

## Logs ajoutés dans DocumentMatchingService.cs

Des logs détaillés ont été ajoutés à chaque étape du processus de comparaison pour identifier où se situe le problème.

## Où voir les logs

Les logs sont écrits dans deux endroits :
1. **Console** : `System.Console.WriteLine()` - visible dans la console de l'application
2. **Debug** : `System.Diagnostics.Debug.WriteLine()` - visible dans Visual Studio Output Window (Debug)

## Structure des logs

### 1. Début de la comparaison
```
[DocumentMatchingService] ========== CompareQuantitiesAsync START ==========
[DocumentMatchingService] Invoice ID: {guid}, Invoice Lines count: {count}
[DocumentMatchingService] DeliveryNote ID: {guid}, DeliveryNote Lines count: {count}
```

### 2. Indexation des lignes de la facture
```
[DocumentMatchingService] 📋 Indexing {count} invoice lines...
[DocumentMatchingService] Invoice Line: SKU='{sku}', EAN='{ean}', Desc='{description}', Qty={qty}
[DocumentMatchingService]   SKU '{sku}': {oldQty} + {lineQty} = {newQty}
[DocumentMatchingService] ✅ Invoice indexed: {skuCount} SKUs, {eanCount} EANs, {descCount} Descriptions
```

### 3. Indexation des lignes du BL
```
[DocumentMatchingService] 📋 Indexing {count} delivery note lines...
[DocumentMatchingService] DeliveryNote Line: SKU='{sku}', EAN='{ean}', Desc='{description}', Qty={qty}
[DocumentMatchingService]   SKU '{sku}': {oldQty} + {lineQty} = {newQty}
[DocumentMatchingService] ✅ DeliveryNote indexed: {skuCount} SKUs, {eanCount} EANs, {descCount} Descriptions
```

### 4. Comparaison
```
[DocumentMatchingService] 🔍 Comparing {count} unique invoice keys...
[DocumentMatchingService] 📊 Comparison: Key={key}, InvoiceQty={qty}, DeliveryQty={qty}, Diff={diff}, MatchMethod={method}
```

### 5. Résultats finaux
```
[DocumentMatchingService] ========== COMPARISON RESULTS ==========
[DocumentMatchingService] Product: '{name}', SKU: '{sku}', InvoiceQty: {qty}, DeliveryQty: {qty}, Diff: {diff}, HasDiff: {true/false}
[DocumentMatchingService] ========== CompareQuantitiesAsync END ==========
```

## Comment utiliser les logs pour diagnostiquer

### Étape 1 : Vérifier les lignes brutes
Cherchez les lignes qui commencent par `Invoice Line:` et `DeliveryNote Line:` pour voir les valeurs brutes stockées dans la base de données.

**Problèmes possibles** :
- Espaces dans les SKU/EAN : `SKU=' ABC123 '` au lieu de `SKU='ABC123'`
- Valeurs null ou vides : `SKU=''` ou `SKU='null'`
- Quantités incorrectes : `Qty=0` alors qu'il devrait y avoir une quantité

### Étape 2 : Vérifier le groupement
Cherchez les lignes qui montrent la sommation : `SKU '{sku}': {oldQty} + {lineQty} = {newQty}`

**Problèmes possibles** :
- Les quantités ne sont pas sommées correctement
- Un produit apparaît plusieurs fois mais n'est pas groupé
- Les clés ne correspondent pas (espaces, casse, etc.)

### Étape 3 : Vérifier les quantités groupées
Cherchez les lignes `✅ Invoice indexed:` et `✅ DeliveryNote indexed:` pour voir combien de clés uniques ont été créées.

**Problèmes possibles** :
- Trop de clés (produits non groupés)
- Pas assez de clés (produits manquants)
- Différence entre le nombre de lignes et le nombre de clés

### Étape 4 : Vérifier les correspondances
Cherchez les lignes `📊 Comparison:` pour voir si les produits sont correctement appariés.

**Problèmes possibles** :
- `MatchMethod=none` : Le produit n'a pas été trouvé dans l'autre document
- `DeliveryQty=null` : Le produit existe dans la facture mais pas dans le BL
- `InvoiceQty=null` : Le produit existe dans le BL mais pas dans la facture
- `Diff` non nul alors que les quantités devraient être égales

### Étape 5 : Vérifier les résultats finaux
Cherchez la section `COMPARISON RESULTS` pour voir toutes les comparaisons finales.

**Problèmes possibles** :
- `HasDiff=true` alors que `Diff=0` (problème de tolérance)
- `Diff` incorrect alors que les quantités sont égales
- Produits manquants dans les résultats

## Exemple de log attendu (quantités égales)

```
[DocumentMatchingService] ========== CompareQuantitiesAsync START ==========
[DocumentMatchingService] Invoice ID: abc-123, Invoice Lines count: 3
[DocumentMatchingService] DeliveryNote ID: def-456, DeliveryNote Lines count: 2

[DocumentMatchingService] 📋 Indexing 3 invoice lines...
[DocumentMatchingService] Invoice Line: SKU='ABC123', EAN='', Desc='Product A', Qty=5
[DocumentMatchingService]   SKU 'ABC123': NEW = 5
[DocumentMatchingService] Invoice Line: SKU='ABC123', EAN='', Desc='Product A', Qty=3
[DocumentMatchingService]   SKU 'ABC123': 5 + 3 = 8
[DocumentMatchingService] Invoice Line: SKU='XYZ789', EAN='', Desc='Product B', Qty=10
[DocumentMatchingService]   SKU 'XYZ789': NEW = 10
[DocumentMatchingService] ✅ Invoice indexed: 2 SKUs, 0 EANs, 0 Descriptions

[DocumentMatchingService] 📋 Indexing 2 delivery note lines...
[DocumentMatchingService] DeliveryNote Line: SKU='ABC123', EAN='', Desc='Product A', Qty=8
[DocumentMatchingService]   SKU 'ABC123': NEW = 8
[DocumentMatchingService] DeliveryNote Line: SKU='XYZ789', EAN='', Desc='Product B', Qty=10
[DocumentMatchingService]   SKU 'XYZ789': NEW = 10
[DocumentMatchingService] ✅ DeliveryNote indexed: 2 SKUs, 0 EANs, 0 Descriptions

[DocumentMatchingService] 🔍 Comparing 2 unique invoice keys...
[DocumentMatchingService] 📊 Comparison: Key=SKU:ABC123, InvoiceQty=8, DeliveryQty=8, Diff=0, MatchMethod=SKU
[DocumentMatchingService] 📊 Comparison: Key=SKU:XYZ789, InvoiceQty=10, DeliveryQty=10, Diff=0, MatchMethod=SKU

[DocumentMatchingService] ✅ Created 2 quantity comparisons (bidirectional)
[DocumentMatchingService] ========== COMPARISON RESULTS ==========
[DocumentMatchingService] Product: 'Product A', SKU: 'ABC123', InvoiceQty: 8, DeliveryQty: 8, Diff: 0, HasDiff: False
[DocumentMatchingService] Product: 'Product B', SKU: 'XYZ789', InvoiceQty: 10, DeliveryQty: 10, Diff: 0, HasDiff: False
[DocumentMatchingService] ========== CompareQuantitiesAsync END ==========
```

## Problèmes courants et solutions

### Problème 1 : Espaces dans les SKU
**Log** : `SKU=' ABC123 '` (avec espaces)
**Solution** : Les valeurs sont maintenant trimées lors du stockage, mais les documents existants peuvent encore avoir des espaces. Re-parser les documents ou exécuter le script SQL de migration.

### Problème 2 : Quantités non groupées
**Log** : Plusieurs lignes avec le même SKU mais pas de sommation
**Solution** : Vérifier que le code de groupement fonctionne correctement (déjà corrigé).

### Problème 3 : Correspondance non trouvée
**Log** : `MatchMethod=none` ou `DeliveryQty=null`
**Solution** : Vérifier que les SKU/EAN sont identiques (trim, casse, espaces).

### Problème 4 : Différence incorrecte
**Log** : `Diff={valeur}` alors que les quantités sont égales
**Solution** : Vérifier le calcul de la différence et l'arrondi (déjà corrigé avec `Math.Round`).

## Actions à prendre

1. **Recompiler et redéployer** le backend avec les nouveaux logs
2. **Exécuter une comparaison** dans pulse.desktop WPF
3. **Copier les logs** depuis la console ou Visual Studio Output Window
4. **Analyser les logs** en suivant les étapes ci-dessus
5. **Identifier le problème** spécifique (espaces, groupement, correspondance, calcul)
6. **Partager les logs** pour analyse plus approfondie si nécessaire

## Format de log à partager

Si vous avez besoin d'aide, partagez :
- Les lignes `Invoice Line:` et `DeliveryNote Line:` pour voir les valeurs brutes
- Les lignes `📊 Comparison:` pour voir les correspondances
- Les lignes `COMPARISON RESULTS` pour voir les résultats finaux

Cela permettra d'identifier précisément où se situe le problème.
