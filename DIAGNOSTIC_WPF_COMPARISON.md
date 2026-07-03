# Diagnostic des différences d'affichage dans pulse.desktop WPF

## Problèmes identifiés et corrigés côté Backend

### 1. Incohérence dans la génération des clés de produits ✅ CORRIGÉ
- **Problème** : `ExtractKey()` était utilisé pour les quantités, `ProductKeyHelper.GetProductKey()` pour les prix
- **Solution** : Utilisation cohérente de `ProductKeyHelper.GetProductKey()` partout

### 2. Normalisation des clés pour les ajustements ✅ CORRIGÉ
- **Problème** : Les ajustements sont normalisés avec `ProductKeyHelper.Normalize()`, mais la recherche utilisait la clé brute
- **Solution** : Normalisation de la clé avant la recherche dans les dictionnaires d'ajustements

## Points à vérifier côté WPF pulse.desktop

### 1. Comparaison des ProductKey
Le WPF doit comparer les produits en utilisant le champ `ProductKey` (pas seulement `Product`).

**Structure de données retournée par l'API :**
```json
{
  "invoiceId": 123,
  "deliveryId": 456,
  "lines": [
    {
      "product": "Nom du produit",
      "productKey": "SKU123",  // ← IMPORTANT : Utiliser ce champ pour la correspondance
      "invoiceQty": 10.0,
      "deliveryQty": 8.0,
      "diff": 2.0,
      "actualQuantity": null,
      "isValidated": false,
      "stockUpdated": false,
      "unit": "PC",
      "invoiceTotalValue": 100.0,
      "currentInvoiceUnitPrice": 10.0,
      "previousInvoiceUnitPrice": 9.5,
      "priceDiff": 0.5,
      "status": "Manquant"
    }
  ]
}
```

### 2. Correspondance des produits
**❌ MAUVAIS** : Comparer uniquement par `Product` (nom)
```csharp
// Ne pas faire ça
var match = lines.FirstOrDefault(l => l.Product == productName);
```

**✅ BON** : Comparer par `ProductKey` (normalisé, insensible à la casse)
```csharp
// Faire ça
var match = lines.FirstOrDefault(l => 
    string.Equals(l.ProductKey, productKey, StringComparison.OrdinalIgnoreCase));
```

### 3. Normalisation des clés côté WPF
Si le WPF génère ses propres clés de produits, il doit utiliser la même logique que le backend :

**Priorité de la clé :**
1. `ProductCode` (SKU, référence article) - si disponible
2. `EAN` - si ProductCode n'est pas disponible
3. Nom normalisé - en dernier recours

**Normalisation du nom (si nécessaire) :**
- Convertir en minuscules
- Supprimer les accents
- Supprimer les parenthèses/crochets
- Supprimer les unités et tailles (kg, g, l, ml, cm, mm, etc.)
- Supprimer les codes produits (abc123, ker00022, etc.)
- Supprimer les nombres isolés
- Supprimer les séparateurs (-, _, /, \)
- Garder seulement les lettres et espaces
- Réduire les espaces multiples

### 4. Groupement des lignes
Si un produit apparaît plusieurs fois dans le BL ou la facture, les quantités doivent être **sommees** par `ProductKey`.

**Exemple :**
- BL ligne 1 : ProductKey="SKU123", Quantity=5
- BL ligne 2 : ProductKey="SKU123", Quantity=3
- **Résultat attendu** : ProductKey="SKU123", DeliveryQty=8

### 5. Calcul de Diff
Le champ `Diff` est déjà calculé côté serveur :
- Si un ajustement validé existe : `Diff = InvoiceQty - ActualQuantity`
- Sinon : `Diff = InvoiceQty - DeliveryQty`

Le WPF ne doit **pas** recalculer Diff, mais utiliser directement `line.Diff`.

### 6. Affichage des quantités
- **InvoiceQty** : Quantité sur la facture
- **DeliveryQty** : Quantité sur le BL
- **ActualQuantity** : Quantité réelle saisie (peut être null)
- **Diff** : Différence calculée (utiliser ce champ, ne pas recalculer)

### 7. Statut des lignes
Le champ `Status` est calculé automatiquement :
- `"OK"` si Diff == 0
- `"Manquant"` si Diff > 0
- `"Surplus"` si Diff < 0

## Checklist de vérification WPF

- [ ] Les produits sont comparés par `ProductKey`, pas par `Product`
- [ ] La comparaison de `ProductKey` est insensible à la casse
- [ ] Les lignes avec le même `ProductKey` sont groupées et leurs quantités sommées
- [ ] Le champ `Diff` est utilisé tel quel (pas recalculé)
- [ ] Le champ `Status` est utilisé tel quel (pas recalculé)
- [ ] Les ajustements (`ActualQuantity`, `IsValidated`) sont correctement affichés
- [ ] Les prix (`CurrentInvoiceUnitPrice`, `PreviousInvoiceUnitPrice`, `PriceDiff`) sont correctement affichés

## Exemple de code C# pour le WPF

```csharp
// Classe de modèle
public class ComparisonLine
{
    public string Product { get; set; }
    public string ProductKey { get; set; }  // ← Utiliser pour la correspondance
    public decimal InvoiceQty { get; set; }
    public decimal DeliveryQty { get; set; }
    public decimal? ActualQuantity { get; set; }
    public bool IsValidated { get; set; }
    public bool StockUpdated { get; set; }
    public decimal Diff { get; set; }  // ← Utiliser tel quel
    public string Unit { get; set; }
    public decimal InvoiceTotalValue { get; set; }
    public string Status { get; set; }  // ← Utiliser tel quel
    public decimal CurrentInvoiceUnitPrice { get; set; }
    public decimal PreviousInvoiceUnitPrice { get; set; }
    public decimal PriceDiff { get; set; }
}

// Correspondance des produits
public ComparisonLine FindMatchingLine(List<ComparisonLine> lines, string productKey)
{
    return lines.FirstOrDefault(l => 
        string.Equals(l.ProductKey, productKey, StringComparison.OrdinalIgnoreCase));
}

// Groupement par ProductKey (si nécessaire)
public Dictionary<string, ComparisonLine> GroupByProductKey(List<ComparisonLine> lines)
{
    return lines
        .GroupBy(l => l.ProductKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            g => g.Key,
            g => new ComparisonLine
            {
                ProductKey = g.Key,
                Product = g.First().Product,  // Prendre le premier nom
                InvoiceQty = g.Sum(l => l.InvoiceQty),
                DeliveryQty = g.Sum(l => l.DeliveryQty),
                // ... autres champs
            },
            StringComparer.OrdinalIgnoreCase
        );
}
```

## Logs de débogage recommandés

Ajouter des logs côté WPF pour identifier les problèmes :

```csharp
// Log lors de la réception des données
foreach (var line in comparisonResult.Lines)
{
    Console.WriteLine($"ProductKey: {line.ProductKey}, Product: {line.Product}, " +
                      $"InvoiceQty: {line.InvoiceQty}, DeliveryQty: {line.DeliveryQty}, " +
                      $"Diff: {line.Diff}, Status: {line.Status}");
}

// Log lors de la correspondance
var matchingLine = FindMatchingLine(lines, productKey);
if (matchingLine == null)
{
    Console.WriteLine($"WARNING: No matching line found for ProductKey: {productKey}");
}
```

## Prochaines étapes

1. Vérifier que le WPF utilise `ProductKey` pour la correspondance
2. Vérifier que la comparaison est insensible à la casse
3. Vérifier que les lignes dupliquées sont correctement groupées
4. Ajouter des logs pour identifier les produits qui ne correspondent pas
5. Comparer les `ProductKey` retournés par l'API avec ceux utilisés côté WPF
