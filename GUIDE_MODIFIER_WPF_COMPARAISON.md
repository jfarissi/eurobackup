# Guide : Identifier où modifier le code WPF pour appeler l'endpoint de comparaison

## Étapes pour identifier le code à modifier

### 1. Chercher les appels API existants

Dans votre projet WPF `pulse.desktop`, recherchez les fichiers qui appellent l'API `supplierdocuments` :

**Fichiers à chercher :**
- `*Service.cs` - Services qui gèrent les appels API
- `*ViewModel.cs` - ViewModels qui utilisent les services
- `*Client.cs` - Clients HTTP
- `*Api.cs` - Classes d'API

**Rechercher dans le code :**
```csharp
// Cherchez ces patterns :
- "/api/supplierdocuments"
- "supplierdocuments"
- "GetLinkedDocuments"
- "GetSupplierDocumentById"
- HttpClient
- HttpGet, HttpPost
```

### 2. Identifier le service qui gère les documents fournisseurs

Le service devrait ressembler à quelque chose comme :

```csharp
public class SupplierDocumentService
{
    private readonly HttpClient _httpClient;
    
    public async Task<SupplierDocumentDto> GetSupplierDocumentByIdAsync(Guid id)
    {
        // Appel actuel
    }
    
    public async Task<List<SupplierDocumentDto>> GetLinkedDocumentsAsync(Guid id)
    {
        // Appel actuel
    }
}
```

### 3. Identifier où la comparaison est effectuée côté client

Cherchez dans le code WPF où les quantités sont comparées :

**Mots-clés à chercher :**
- `Compare`
- `Quantity`
- `InvoiceQuantity`
- `DeliveryNoteQuantity`
- `Difference`
- `HasDifference`

**Probablement dans :**
- Un ViewModel qui affiche la comparaison
- Un service qui calcule les différences
- Un modèle qui contient les résultats de comparaison

### 4. Exemple de structure typique

```
pulse.desktop/
├── Services/
│   └── SupplierDocumentService.cs  ← ICI : Ajouter la méthode
├── ViewModels/
│   └── DocumentComparisonViewModel.cs  ← ICI : Modifier pour utiliser le service
├── Models/
│   └── QuantityComparison.cs  ← Peut-être déjà existant
└── Views/
    └── DocumentComparisonView.xaml  ← Affiche les résultats
```

## Code à ajouter/modifier

### Étape 1 : Ajouter la méthode dans le service API

Dans votre `SupplierDocumentService.cs` (ou équivalent), ajoutez :

```csharp
/// <summary>
/// Compare les quantités entre une facture et un BL via l'API backend
/// </summary>
public async Task<DocumentMatchResultDto> MatchInvoiceWithDeliveryNoteAsync(Guid invoiceDocumentId)
{
    var response = await _httpClient.PostAsync(
        $"/api/supplierdocuments/{invoiceDocumentId}/match-with-delivery-note",
        null);
    
    response.EnsureSuccessStatusCode();
    
    var result = await response.Content.ReadFromJsonAsync<ApiResult<DocumentMatchResultDto>>();
    
    if (result?.Success == true)
    {
        return result.Data;
    }
    
    throw new Exception(result?.ErrorMessage ?? "Erreur lors de la comparaison");
}

/// <summary>
/// Compare uniquement les quantités entre facture et BL
/// </summary>
public async Task<List<QuantityComparisonDto>> CompareQuantitiesAsync(
    Guid invoiceId, 
    Guid? deliveryNoteId = null)
{
    var request = new CompareQuantitiesRequest
    {
        InvoiceId = invoiceId,
        DeliveryNoteId = deliveryNoteId
    };
    
    var response = await _httpClient.PostAsJsonAsync(
        "/api/supplierdocuments/compare-quantities",
        request);
    
    response.EnsureSuccessStatusCode();
    
    var result = await response.Content.ReadFromJsonAsync<ApiResult<List<QuantityComparisonDto>>>();
    
    if (result?.Success == true)
    {
        return result.Data;
    }
    
    throw new Exception(result?.ErrorMessage ?? "Erreur lors de la comparaison");
}
```

### Étape 2 : Créer les DTOs nécessaires

Créez ou vérifiez que ces DTOs existent dans votre projet WPF :

```csharp
public class DocumentMatchResultDto
{
    public SupplierDocumentDto Invoice { get; set; }
    public SupplierDocumentDto DeliveryNote { get; set; }
    public bool IsMatched { get; set; }
    public string MatchReason { get; set; }
    public List<QuantityComparisonDto> QuantityComparisons { get; set; }
    public List<PriceComparisonDto> PriceComparisons { get; set; }
}

public class QuantityComparisonDto
{
    public string Sku { get; set; }
    public string ProductName { get; set; }
    public decimal InvoiceQuantity { get; set; }
    public decimal? DeliveryNoteQuantity { get; set; }
    public decimal Difference { get; set; }
    public bool HasDifference => Math.Abs(Difference) > 0.01m;
}

public class CompareQuantitiesRequest
{
    public Guid InvoiceId { get; set; }
    public Guid? DeliveryNoteId { get; set; }
}

public class ApiResult<T>
{
    public bool Success { get; set; }
    public T Data { get; set; }
    public string ErrorMessage { get; set; }
}
```

### Étape 3 : Modifier le ViewModel

Dans votre ViewModel qui affiche la comparaison, remplacez la logique côté client par :

```csharp
// AVANT (comparaison côté client - à supprimer)
private void CompareQuantities()
{
    // Logique de comparaison locale
    foreach (var invoiceLine in Invoice.Lines)
    {
        var deliveryLine = DeliveryNote.Lines.FirstOrDefault(...);
        // Comparaison manuelle...
    }
}

// APRÈS (appel API backend)
private async Task CompareQuantitiesAsync()
{
    try
    {
        IsLoading = true;
        
        // Option 1 : Matching complet (recommandé)
        var result = await _supplierDocumentService
            .MatchInvoiceWithDeliveryNoteAsync(InvoiceId);
        
        // Option 2 : Comparaison de quantités uniquement
        // var comparisons = await _supplierDocumentService
        //     .CompareQuantitiesAsync(InvoiceId, DeliveryNoteId);
        
        // Mettre à jour l'UI avec les résultats
        QuantityComparisons = result.QuantityComparisons;
        OnPropertyChanged(nameof(QuantityComparisons));
    }
    catch (Exception ex)
    {
        // Gérer l'erreur
        MessageBox.Show($"Erreur : {ex.Message}");
    }
    finally
    {
        IsLoading = false;
    }
}
```

## Vérification

Une fois modifié, vérifiez que :

1. ✅ Le WPF appelle l'endpoint `/api/supplierdocuments/{id}/match-with-delivery-note`
2. ✅ Les logs apparaissent dans `logs/eurobrico-YYYY-MM-DD.log`
3. ✅ Les résultats de comparaison sont affichés correctement dans l'UI

## Recherche dans Visual Studio

Pour trouver rapidement le code à modifier dans Visual Studio :

1. **Ctrl+Shift+F** (Recherche dans tous les fichiers)
2. Recherchez : `GetLinkedDocuments` ou `supplierdocuments`
3. Ouvrez les fichiers trouvés
4. Cherchez où les quantités sont comparées (recherchez `Compare`, `Quantity`, `Difference`)

## Points d'attention

- **Authentification** : Assurez-vous que le `HttpClient` inclut le token JWT dans les headers
- **Gestion d'erreurs** : Ajoutez un try-catch pour gérer les erreurs API
- **Loading state** : Affichez un indicateur de chargement pendant l'appel API
- **Mapping** : Vérifiez que les DTOs correspondent entre le backend et le WPF

## Exemple complet de service

```csharp
public class SupplierDocumentService : ISupplierDocumentService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    
    public SupplierDocumentService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        
        // Configurer la base URL
        _httpClient.BaseAddress = new Uri(_configuration["ApiBaseUrl"]);
        
        // Ajouter le token JWT si nécessaire
        var token = GetAuthToken();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);
        }
    }
    
    // Méthodes existantes...
    
    // NOUVELLE MÉTHODE à ajouter
    public async Task<DocumentMatchResultDto> MatchInvoiceWithDeliveryNoteAsync(
        Guid invoiceDocumentId)
    {
        var response = await _httpClient.PostAsync(
            $"api/supplierdocuments/{invoiceDocumentId}/match-with-delivery-note",
            null);
        
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResult<DocumentMatchResultDto>>(
            json, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (result?.Success == true)
        {
            return result.Data;
        }
        
        throw new Exception(result?.ErrorMessage ?? "Erreur lors de la comparaison");
    }
}
```
