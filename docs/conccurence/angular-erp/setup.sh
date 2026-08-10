#!/bin/bash
# ============================================================
# setup.sh — Crée le projet Angular et copie les fichiers générés
# ============================================================

set -e

echo "🚀 Création du projet Angular ERP..."

# 1. Créer le projet Angular (sans routing ni CSS, on gère nous-mêmes)
npx @angular/cli@17 new erp-client --routing --style=css --ssr=false --skip-git --skip-install

cd erp-client

# 2. Copier les fichiers générés
echo "📁 Copie des fichiers sources..."
cp -r ../src/app/* src/app/
cp ../src/main.ts src/
cp ../src/index.html src/
cp ../package.json .
cp ../proxy.conf.json .

# 3. Installer les dépendances
echo "📦 Installation des dépendances..."
npm install

# 4. Lancer
echo "✅ Projet prêt !"
echo ""
echo "Pour démarrer :"
echo "  cd erp-client"
echo "  npm start"
echo ""
echo "Le frontend sera accessible sur http://localhost:4200"
echo "(avec proxy vers l'API sur http://localhost:8080)"
