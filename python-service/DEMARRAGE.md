# Guide de démarrage du service Python

## ⚠️ Problème "0 Unknown Error" depuis Angular

Cette erreur indique que le service Python n'est pas accessible depuis Angular.

## ✅ Solution : Démarrer avec --host 0.0.0.0

Le service doit écouter sur **toutes les interfaces réseau**, pas seulement localhost.

### Commande correcte :

```bash
cd python-service
.venv\Scripts\uvicorn.exe app.main:app --reload --host 0.0.0.0 --port 8000
```

### Ou utiliser le script :

**Windows :**
```bash
python-service\start.bat
```

**Linux/Mac :**
```bash
bash python-service/start.sh
```

## 🔍 Vérifications

### 1. Vérifier que le service répond

Ouvrez dans votre navigateur : `http://localhost:8000/health`

Vous devriez voir :
```json
{"status":"healthy","service":"python-extractor"}
```

### 2. Vérifier les logs

Le service devrait afficher :
```
INFO:     Uvicorn running on http://0.0.0.0:8000 (Press CTRL+C to quit)
INFO:     Started reloader process
INFO:     Started server process
INFO:     Waiting for application startup.
INFO:     Application startup complete.
```

### 3. Vérifier le port

Assurez-vous que le port 8000 n'est pas utilisé par un autre service :
```bash
netstat -ano | findstr :8000
```

## 🐛 Dépannage

### Erreur "Address already in use"

Le port 8000 est déjà utilisé. Changez le port :
```bash
uvicorn app.main:app --reload --host 0.0.0.0 --port 8001
```

Puis modifiez dans Angular : `pythonServiceUrl = 'http://localhost:8001'`

### Erreur CORS

Le CORS est configuré pour autoriser toutes les origines (`allow_origins=["*"]`). Si le problème persiste, vérifiez que le middleware CORS est bien chargé.

### Firewall Windows

Le firewall peut bloquer les connexions. Autorisez Python dans le firewall Windows.

## 📝 Commandes utiles

### Démarrer le service
```bash
cd python-service
.venv\Scripts\uvicorn.exe app.main:app --reload --host 0.0.0.0 --port 8000
```

### Tester avec curl
```bash
curl http://localhost:8000/health
```

### Voir les logs en temps réel
Les logs s'affichent directement dans la console où vous avez démarré uvicorn.

