"""Configuration (surchargeable par variables d'environnement)."""
import os
import secrets
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent

DB_PATH = Path(os.environ.get("KAZI_DB", BASE_DIR / "kaziwatts.db"))
UPLOAD_DIR = Path(os.environ.get("KAZI_UPLOADS", BASE_DIR / "uploads"))
TOKEN_DAYS = int(os.environ.get("KAZI_TOKEN_DAYS", "7"))
INVITE_DAYS = 7
LOW_CREDIT_DAYS = 3          # alerte « crédit bas » : autonomie < 3 jours
MAX_PROOF_BYTES = 5 * 1024 * 1024

# KAZI_DEV=1 (défaut) : le code de réinitialisation du mot de passe est renvoyé dans la réponse
# (pas de SMS/e-mail branché). Mettre KAZI_DEV=0 en production.
DEV_MODE = os.environ.get("KAZI_DEV", "1") == "1"


def _load_secret() -> str:
    env = os.environ.get("KAZI_SECRET")
    if env:
        return env
    f = BASE_DIR / ".secret"
    if f.exists():
        return f.read_text().strip()
    s = secrets.token_hex(32)
    try:
        f.write_text(s)
    except OSError:
        pass
    return s


SECRET_KEY = _load_secret()
