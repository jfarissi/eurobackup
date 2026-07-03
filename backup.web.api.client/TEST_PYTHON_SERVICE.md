# Guide de test du service Python depuis Angular

## 📋 Fichiers créés

1. **`src/app/services/python-extractor.service.ts`**
   - Service Angular pour appeler directement le service Python
   - Méthodes: `parsePdf()`, `extractProducts()`, `inspectMetadata()`, `healthCheck()`

2. **`src/app/components/python-test/python-test.component.ts`**
   - Composant de test avec interface utilisateur
   - Permet de tester tous les endpoints Python

3. **`src/app/components/python-test/python-test.component.html`**
   - Template avec formulaire de test

4. **`src/app/components/python-test/python-test.component.css`**
   - Styles pour l'interface de test

## 🚀 Configuration

### 1. Ajouter la route dans votre fichier de routing

Ajoutez cette route dans votre fichier de routing (ex: `app-routing.module.ts` ou `app.routes.ts`):

```typescript
import { PythonTestComponent } from './components/python-test/python-test.component';

// Dans vos routes:
{
  path: 'python-test',
  component: PythonTestComponent
}
```

### 2. Configurer l'URL du service Python

Par défaut, le service utilise `http://localhost:8000`. 

Pour changer l'URL, modifiez `python-extractor.service.ts`:

```typescript
private pythonServiceUrl = 'http://localhost:8000'; // Changez selon votre configuration
```

Ou ajoutez dans `environment.ts`:

```typescript
export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:7157/api',
  pythonServiceUrl: 'http://localhost:8000' // Ajoutez cette ligne
};
```

Puis dans le service:

```typescript
private pythonServiceUrl = environment.pythonServiceUrl || 'http://localhost:8000';
```

### 3. Ajouter un lien dans votre menu (optionnel)

```html
<a routerLink="/python-test">Test Service Python</a>
```

## 🧪 Utilisation

### Étape 1: Démarrer le service Python

```bash
cd python-service
uvicorn app.main:app --reload --port 8000
```

### Étape 2: Accéder à la page de test

Naviguez vers: `http://localhost:4200/python-test`

### Étape 3: Tester

1. **Health Check**: Vérifie que le service Python est accessible
2. **Sélectionner un PDF**: Choisissez un fichier PDF à tester
3. **Tester les endpoints**:
   - **Test /parse**: Nouveau endpoint (items + metadata en une fois)
   - **Test /extract**: Endpoint existant (produits uniquement)
   - **Test /inspect**: Endpoint existant (métadonnées uniquement)

## 📊 Résultats affichés

- **Métadonnées**: Type, numéro, client, fournisseur, date
- **Statistiques**: Total items, items avec SKU/EAN, valeur totale
- **Table des items**: SKU, EAN, description, quantité, prix, total
- **JSON brut**: Pour debug

## 🔧 Dépannage

### Erreur CORS

Si vous avez une erreur CORS, ajoutez dans `python-service/app/main.py`:

```python
from fastapi.middleware.cors import CORSMiddleware

app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:4200"],  # URL de votre app Angular
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)
```

### Service Python non accessible

Vérifiez:
1. Le service Python est démarré (`uvicorn app.main:app --reload --port 8000`)
2. L'URL dans `python-extractor.service.ts` est correcte
3. Pas de firewall bloquant le port 8000

## 📝 Exemple d'utilisation dans votre code

```typescript
import { PythonExtractorService } from './services/python-extractor.service';

constructor(private pythonService: PythonExtractorService) {}

testPdf(file: File) {
  this.pythonService.parsePdf(file).subscribe({
    next: (result) => {
      console.log('Items:', result.items);
      console.log('Metadata:', result.metadata);
    },
    error: (err) => {
      console.error('Erreur:', err);
    }
  });
}
```

## ✅ Tests à effectuer

1. ✅ Health check fonctionne
2. ✅ `/parse` retourne items + metadata
3. ✅ `/extract` retourne les produits
4. ✅ `/inspect` retourne les métadonnées
5. ✅ Détection automatique du fournisseur (Knauf, FF Group, etc.)
6. ✅ Extraction correcte des SKU, EAN, descriptions, prix

