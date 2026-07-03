@echo off
echo Demarrage du service Python Extractor...
cd /d %~dp0
set PYTHONUNBUFFERED=1
.venv\Scripts\python.exe -u -m uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
pause

