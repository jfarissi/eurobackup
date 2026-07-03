"""
Script de test pour vérifier la connexion à Ollama
"""
import requests
import json

OLLAMA_HOST = "http://localhost:11434"

def test_ollama():
    """Teste la connexion à Ollama"""
    try:
        # Vérifier que Ollama est accessible
        response = requests.get(f"{OLLAMA_HOST}/api/tags", timeout=5)
        if response.status_code == 200:
            data = response.json()
            models = [model.get("name", "") for model in data.get("models", [])]
            print(f"✅ Ollama est accessible")
            print(f"📦 Modèles disponibles: {models if models else 'Aucun modèle téléchargé'}")
            
            if not models:
                print("\n⚠️  Aucun modèle disponible. Téléchargez un modèle avec:")
                print("   ollama pull llama3.2:latest")
                print("   ou")
                print("   ollama pull qwen2.5:7b-instruct")
            else:
                # Tester avec le premier modèle
                model = models[0]
                print(f"\n🧪 Test avec le modèle: {model}")
                test_response = requests.post(
                    f"{OLLAMA_HOST}/api/chat",
                    json={
                        "model": model,
                        "messages": [{"role": "user", "content": "Bonjour, réponds juste 'OK'"}],
                        "stream": False
                    },
                    timeout=30
                )
                if test_response.status_code == 200:
                    result = test_response.json()
                    print(f"✅ Test réussi: {result.get('message', {}).get('content', 'N/A')[:50]}")
                else:
                    print(f"❌ Erreur lors du test: {test_response.status_code}")
        else:
            print(f"❌ Ollama répond avec le code: {response.status_code}")
    except requests.exceptions.ConnectionError:
        print("❌ Ollama n'est pas accessible. Vérifiez qu'il est démarré.")
        print("   Pour démarrer Ollama, exécutez simplement: ollama")
    except Exception as e:
        print(f"❌ Erreur: {e}")

if __name__ == "__main__":
    test_ollama()

