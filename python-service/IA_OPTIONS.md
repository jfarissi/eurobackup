# Options d'IA pour l'Extraction

## 🆓 Option 1 : Ollama (RECOMMANDÉ - Gratuit, Local, Sans Quota)

### Avantages
- ✅ **100% Gratuit** - Pas de coût
- ✅ **Sans quota** - Utilisation illimitée
- ✅ **Local** - Données restent sur votre machine
- ✅ **Déjà configuré** dans votre projet

### Installation

1. **Installer Ollama** :
   ```bash
   # Windows (PowerShell)
   winget install Ollama.Ollama
   
   # Ou télécharger depuis https://ollama.com/download
   ```

2. **Télécharger un modèle** :
   ```bash
   ollama pull llama3.2:latest
   # ou
   ollama pull qwen2.5:7b-instruct
   # ou
   ollama pull mistral:latest
   ```

3. **Configurer** :
   ```bash
   # Variables d'environnement (optionnel)
   export OLLAMA_HOST=http://localhost:11434
   export OLLAMA_MODEL=llama3.2:latest
   ```

4. **Tester** :
   ```bash
   ollama run llama3.2 "Bonjour"
   ```

### Utilisation
Le code essaie automatiquement Ollama en premier. Si Ollama est disponible, il l'utilise. Sinon, il passe à OpenAI/Gemini.

---

## 🆓 Option 2 : Google Gemini (Gratuit avec Quota Généreux)

### Avantages
- ✅ **Gratuit** - 15 requêtes/minute, 1500 requêtes/jour
- ✅ **Pas de carte bancaire** requise pour commencer
- ✅ **Très performant** - Modèle Gemini 1.5 Flash

### Configuration

1. **Obtenir une clé API** :
   - Aller sur https://aistudio.google.com/apikey
   - Créer une clé API (gratuite)

2. **Configurer** :
   ```bash
   export GEMINI_API_KEY="votre-clé-api"
   export GEMINI_MODEL="gemini-1.5-flash"  # ou "gemini-1.5-pro"
   ```

3. **Installer la bibliothèque** :
   ```bash
   pip install google-generativeai
   ```

### Coûts après quota gratuit
- Gemini 1.5 Flash : ~$0.0001 par requête
- Gemini 1.5 Pro : ~$0.001 par requête

---

## 💰 Option 3 : OpenAI (Payant mais Performant)

### Avantages
- ✅ **Très performant** - GPT-4o
- ✅ **Quota gratuit** - 250k tokens/jour pour nouveaux comptes
- ✅ **Stable et fiable**

### Configuration
```bash
export OPENAI_API_KEY="sk-..."
export OPENAI_MODEL="gpt-4o"  # ou "gpt-4-turbo-preview"
```

### Coûts
- GPT-4o : ~$0.01-0.02 par facture
- GPT-4 Turbo : ~$0.005-0.01 par facture

---

## 📊 Comparaison

| Option | Coût | Quota | Performance | Installation |
|--------|------|-------|-------------|--------------|
| **Ollama** | Gratuit | Illimité | ⭐⭐⭐⭐ | Facile |
| **Gemini** | Gratuit* | 1500/jour | ⭐⭐⭐⭐⭐ | Très facile |
| **OpenAI** | Payant | 250k/jour* | ⭐⭐⭐⭐⭐ | Très facile |

*Quota gratuit pour commencer

---

## 🚀 Recommandation

**Pour tester sans limite** : Utilisez **Ollama** (gratuit, local, sans quota)

**Pour production** : Utilisez **Gemini** (gratuit avec quota généreux) ou **OpenAI** (payant mais très performant)

Le code essaie automatiquement dans cet ordre :
1. Ollama (si disponible)
2. OpenAI (si clé configurée)
3. Gemini (si clé configurée)
4. Méthode classique (regex) en fallback

