# Configuration Ollama - Guide Rapide

## ✅ Ollama est déjà installé

Maintenant, il faut :

### 1. Vérifier qu'Ollama est démarré

Ollama doit être en cours d'exécution. Vérifiez dans le gestionnaire de tâches ou démarrez-le :
```bash
# Ollama devrait démarrer automatiquement, sinon :
ollama serve
```

### 2. Télécharger un modèle (si pas déjà fait)

```bash
# Option 1 : Llama 3.2 (recommandé, ~2GB)
ollama pull llama3.2:latest

# Option 2 : Qwen 2.5 (plus performant, ~4.5GB)
ollama pull qwen2.5:7b-instruct

# Option 3 : Mistral (bon compromis, ~4GB)
ollama pull mistral:latest
```

### 3. Vérifier les modèles disponibles

```bash
ollama list
```

### 4. Tester Ollama

```bash
# Test simple
ollama run llama3.2 "Bonjour"

# Ou tester avec le script Python
cd python-service
python test_ollama.py
```

### 5. Configurer (optionnel)

Si vous utilisez un modèle différent ou un autre port :
```bash
export OLLAMA_HOST=http://localhost:11434
export OLLAMA_MODEL=llama3.2:latest
```

## 🚀 Utilisation

Une fois Ollama configuré, le service Python utilisera **automatiquement Ollama en priorité** (gratuit, sans quota).

Le code essaie dans cet ordre :
1. **Ollama** (si disponible) ← **GRATUIT, SANS QUOTA**
2. OpenAI (si clé configurée)
3. Gemini (si clé configurée)
4. Méthode classique (regex) en fallback

## 📝 Notes

- Ollama fonctionne **localement** - vos données ne quittent pas votre machine
- **Aucun quota** - utilisation illimitée
- **Gratuit** - pas de coût
- Performance : ⭐⭐⭐⭐ (très bon pour l'extraction de documents)

