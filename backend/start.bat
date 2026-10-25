@echo off
cd /d "%~dp0"
if not exist .venv ( python -m venv .venv )
call .venv\Scripts\activate
pip install -q -r requirements.txt
if not exist kaziwatts.db ( python seed.py )
echo.
echo  API KaziWatts : http://localhost:8000   (documentation : http://localhost:8000/docs)
python -m uvicorn app.main:app --host 0.0.0.0 --port 8000
