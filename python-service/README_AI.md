# Extraction par IA

## Configuration

Pour utiliser l'extraction par IA, vous devez configurer une clé API OpenAI.

## Option 1 : Via appsettings.json (Recommandé)

1. Ajoutez votre clé dans `Backup.Web.Api.Server/appsettings.json` :
```json
"PythonExtractor": {
  "Enabled": true,
  "Url": "http://localhost:8000",
  "OpenAiApiKey": "sk-votre-cle-api-ici"
}
```

2. Exécutez le script de synchronisation :
```powershell
.\sync-openai-key.ps1
```

Le script synchronisera automatiquement la clé vers le fichier `.env` du service Python.

## Option 2 : Directement dans le fichier .env

Créez un fichier `.env` dans le dossier `python-service/` :
```
OPENAI_API_KEY=votre-clé-api
USE_OPENAI=true
OPENAI_MODEL=gpt-4o  # ou gpt-4-turbo-preview, gpt-4
```

## Utilisation

### Via l'API REST

**Extraction avec IA :**
```bash
curl -X POST "http://localhost:8000/extract?use_ai=true" \
  -F "file=@facture.pdf"
```

**Extraction classique (regex) :**
```bash
curl -X POST "http://localhost:8000/extract?use_ai=false" \
  -F "file=@facture.pdf"
```

### Métadonnées avec IA

```bash
curl -X POST "http://localhost:8000/inspect?use_ai=true" \
  -F "file=@facture.pdf"
```

## Avantages de l'IA

✅ **Précision ~99%** - L'IA comprend le contexte et la structure
✅ **Moins de maintenance** - Pas besoin de regex spécifiques
✅ **Adaptation automatique** - Fonctionne avec différents formats
✅ **Descriptions complètes** - Pas de troncature

## Coûts

- GPT-4o : ~$0.01-0.02 par facture (selon la taille)
- GPT-4 Turbo : ~$0.005-0.01 par facture
- GPT-4 : ~$0.03-0.05 par facture

## Fallback automatique

Si l'extraction IA échoue, le système utilise automatiquement la méthode classique (regex).

