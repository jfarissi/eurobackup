# Guide de test de connexion Angular → Python

## Problème : "0 Unknown Error"

Cette erreur indique généralement un problème de connexion réseau ou CORS.

## Solutions

### 1. Vérifier que le service Python écoute sur toutes les interfaces

Lors du démarrage, utilisez `--host 0.0.0.0` :

```bash
cd python-service
.venv\Scripts\uvicorn.exe app.main:app --reload --host 0.0.0.0 --port 8000
```

### 2. Vérifier le port Angular

Par défaut Angular tourne sur `http://localhost:4200`. Vérifiez dans la console Angular.

### 3. Tester directement dans le navigateur

Ouvrez dans votre navigateur : `http://localhost:8000/health`

Vous devriez voir : `{"status":"healthy","service":"python-extractor"}`

### 4. Vérifier les erreurs dans la console du navigateur

Ouvrez les DevTools (F12) → Console et onglet Network pour voir les détails de l'erreur.

### 5. Vérifier le firewall Windows

Le firewall peut bloquer les connexions. Vérifiez que le port 8000 n'est pas bloqué.

### 6. Tester avec curl ou Postman

```bash
curl http://localhost:8000/health
```

### 7. Configuration CORS

Le CORS est maintenant configuré pour autoriser toutes les origines en développement (`allow_origins=["*"]`).

## Commandes de démarrage

### Service Python
```bash
cd python-service
.venv\Scripts\uvicorn.exe app.main:app --reload --host 0.0.0.0 --port 8000
```

### Angular
```bash
cd backup.web.api.client
ng serve
```

## Debug

Si le problème persiste, vérifiez :
1. Le service Python est bien démarré
2. Le port 8000 n'est pas utilisé par un autre service
3. Pas de proxy dans Angular qui redirige les requêtes
4. Les DevTools du navigateur montrent l'erreur exacte

