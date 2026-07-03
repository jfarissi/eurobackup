# Configuration de l'Extraction par IA

## Méthode 1 : Via appsettings.json (Recommandé)

1. Ajoutez votre clé OpenAI dans `Backup.Web.Api.Server/appsettings.json` :
```json
"PythonExtractor": {
  "Enabled": true,
  "Url": "http://localhost:8000",
  "OpenAiApiKey": "sk-votre-cle-api-ici",
  "UseAiForPythonParse": false,
  "DefaultAiProvider": "openai",
  "DocumentAi": {
    "OllamaHost": "http://localhost:11434",
    "ActiveProfile": "Primary",
    "Profiles": {
      "Primary": { "Model": "gemma4-rapide:latest" },
      "Quality": { "Model": "gemma4:e4b" }
    },
    "FallbackProfiles": [ "Primary", "Quality" ]
  }
}
```

- **UseAiForPythonParse** : si `true`, l’API .NET appelle `/parse` avec l’IA activée.
- **DefaultAiProvider** : `openai`, `gemini` ou `ollama`.
- **DocumentAi** : hôte Ollama, profil actif et modèles par profil (synchronisés en `OLLAMA_PROFILE_*` dans `.env`).

2. Exécutez le script de synchronisation depuis la racine du projet :
```powershell
.\sync-openai-key.ps1
```

Le script créera automatiquement le fichier `.env` dans `python-service/` avec votre clé.

## Méthode 2 : Directement dans le fichier .env

Créez manuellement un fichier `.env` dans le dossier `python-service/` :

```bash
# Configuration OpenAI (Recommandé)
OPENAI_API_KEY=sk-...
USE_OPENAI=true
OPENAI_MODEL=gpt-4o  # ou gpt-4-turbo-preview, gpt-4

# Modèles disponibles:
# - gpt-4o : Meilleur rapport qualité/prix (~$0.01-0.02 par facture)
# - gpt-4-turbo-preview : Plus rapide (~$0.005-0.01 par facture)
# - gpt-4 : Plus précis mais plus cher (~$0.03-0.05 par facture)
```

## Configuration par Défaut

✅ **L'IA est activée par défaut** (`use_ai=True`)

Si vous voulez désactiver l'IA temporairement :
```bash
# Dans appsettings.json du backend C#
"PythonExtractor": {
  "Enabled": false  # Désactive complètement le service Python
}

# Ou via l'API
curl -X POST "http://localhost:8000/extract?use_ai=false" -F "file=@facture.pdf"
```

## Fallback Automatique

Si l'extraction IA échoue (clé API manquante, erreur réseau, etc.), le système utilise **automatiquement** la méthode classique (regex).

## Logging

Les logs sont maintenant activés pour suivre :
- Les appels API OpenAI
- Le nombre de produits extraits
- Les erreurs éventuelles

Pour voir les logs :
```bash
# En développement
uvicorn app.main:app --reload --log-level debug

# En production (Docker)
docker-compose logs -f python-extractor
```

