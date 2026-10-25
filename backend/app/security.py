"""Mots de passe (PBKDF2) et jetons JWT (HS256)."""
import hashlib
import hmac
import os
import re
from datetime import datetime, timedelta, timezone

import jwt

from . import config

_ITER = 200_000
_EMAIL = re.compile(r"^[^@\s]+@[^@\s]+\.[^@\s]+$")


def hash_password(password: str) -> str:
    salt = os.urandom(16)
    dk = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, _ITER)
    return f"pbkdf2_sha256${_ITER}${salt.hex()}${dk.hex()}"


def verify_password(password: str, stored: str) -> bool:
    try:
        _, it, salt, dk = stored.split("$")
        calc = hashlib.pbkdf2_hmac("sha256", password.encode(), bytes.fromhex(salt), int(it))
        return hmac.compare_digest(calc.hex(), dk)
    except Exception:
        return False


def hash_code(code: str) -> str:
    return hashlib.sha256((code + config.SECRET_KEY).encode()).hexdigest()


def create_token(user_id: int) -> str:
    now = datetime.now(timezone.utc)
    payload = {"sub": str(user_id), "iat": now, "exp": now + timedelta(days=config.TOKEN_DAYS)}
    return jwt.encode(payload, config.SECRET_KEY, algorithm="HS256")


def decode_token(token: str) -> int | None:
    try:
        data = jwt.decode(token, config.SECRET_KEY, algorithms=["HS256"])
        return int(data["sub"])
    except Exception:
        return None


def normalize_identifier(raw: str) -> str:
    """E-mail (minuscules) ou téléphone camerounais (chiffres, préfixe 237)."""
    s = (raw or "").strip()
    if not s:
        raise ValueError("Renseignez votre téléphone ou votre e-mail.")
    if "@" in s:
        s = s.lower()
        if not _EMAIL.match(s):
            raise ValueError("Adresse e-mail invalide.")
        return s
    digits = re.sub(r"\D", "", s)
    if digits.startswith("00"):
        digits = digits[2:]
    if len(digits) == 9:
        digits = "237" + digits
    if not (digits.startswith("237") and len(digits) == 12):
        raise ValueError("Numéro de téléphone invalide (9 chiffres, ex. 6 77 00 00 00).")
    return "+" + digits
