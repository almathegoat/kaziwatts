import random
import sqlite3
import time
from datetime import timedelta

from fastapi import APIRouter, Depends, HTTPException

from .. import config, logic
from ..db import get_db
from ..deps import current_user
from ..schemas import (ChangePasswordIn, ForgotIn, LoginIn, ProfileIn, RegisterIn, ResetIn)
from ..security import (create_token, hash_code, hash_password, normalize_identifier, verify_password)

router = APIRouter(prefix="/auth", tags=["Authentification"])

_FAILS: dict[str, list[float]] = {}   # anti-force-brute en mémoire : 5 échecs / 15 min / identifiant
_BAD = "Identifiant ou mot de passe incorrect."


def user_out(db: sqlite3.Connection, u: sqlite3.Row) -> dict:
    m = db.execute("SELECT colocation_id, role FROM members WHERE user_id=?", (u["id"],)).fetchone()
    return {
        "id": u["id"], "full_name": u["full_name"], "initials": logic.initials(u["full_name"]),
        "identifier": u["identifier"], "theme": u["theme"], "created_at": u["created_at"],
        "colocation_id": m["colocation_id"] if m else None,
        "role": m["role"] if m else None,
    }


def _throttled(key: str) -> bool:
    t = time.time()
    _FAILS[key] = [x for x in _FAILS.get(key, []) if t - x < 900]
    return len(_FAILS[key]) >= 5


@router.post("/register", status_code=201, summary="Créer un compte")
def register(body: RegisterIn, db: sqlite3.Connection = Depends(get_db)):
    if body.confirm_password is not None and body.confirm_password != body.password:
        raise HTTPException(422, "Les deux mots de passe ne sont pas identiques.")
    if not body.accept_terms:
        raise HTTPException(422, "Acceptez les conditions d'utilisation pour continuer.")
    if db.execute("SELECT 1 FROM users WHERE identifier=?", (body.identifier,)).fetchone():
        raise HTTPException(409, "Un compte existe déjà avec cet identifiant.")
    cur = db.execute("INSERT INTO users(full_name, identifier, password_hash, created_at) VALUES(?,?,?,?)",
                     (body.full_name, body.identifier, hash_password(body.password), logic.iso(logic.now())))
    db.commit()
    u = db.execute("SELECT * FROM users WHERE id=?", (cur.lastrowid,)).fetchone()
    return {"token": create_token(u["id"]), "user": user_out(db, u)}


@router.post("/login", summary="Se connecter")
def login(body: LoginIn, db: sqlite3.Connection = Depends(get_db)):
    try:
        ident = normalize_identifier(body.identifier)
    except ValueError:
        raise HTTPException(401, _BAD)
    if _throttled(ident):
        raise HTTPException(429, "Trop de tentatives. Réessayez dans quelques minutes.")
    u = db.execute("SELECT * FROM users WHERE identifier=?", (ident,)).fetchone()
    if not u or not verify_password(body.password, u["password_hash"]):
        _FAILS.setdefault(ident, []).append(time.time())
        raise HTTPException(401, _BAD)
    _FAILS.pop(ident, None)
    return {"token": create_token(u["id"]), "user": user_out(db, u)}


@router.get("/me", summary="Profil de l'utilisateur connecté")
def me(user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    return user_out(db, user)


@router.patch("/me", summary="Modifier nom ou thème")
def update_me(body: ProfileIn, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    name = " ".join(body.full_name.split()) if body.full_name else user["full_name"]
    db.execute("UPDATE users SET full_name=?, theme=? WHERE id=?", (name, body.theme or user["theme"], user["id"]))
    db.commit()
    return user_out(db, db.execute("SELECT * FROM users WHERE id=?", (user["id"],)).fetchone())


@router.post("/change-password", summary="Changer le mot de passe")
def change_password(body: ChangePasswordIn, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    if not verify_password(body.current_password, user["password_hash"]):
        raise HTTPException(403, "Mot de passe actuel incorrect.")
    db.execute("UPDATE users SET password_hash=? WHERE id=?", (hash_password(body.new_password), user["id"]))
    db.commit()
    return {"message": "Mot de passe modifié."}


@router.post("/forgot-password", summary="Demander un code à 6 chiffres")
def forgot_password(body: ForgotIn, db: sqlite3.Connection = Depends(get_db)):
    msg = "Si ce compte existe, un code à 6 chiffres a été envoyé."
    try:
        ident = normalize_identifier(body.identifier)
    except ValueError:
        return {"message": msg}
    u = db.execute("SELECT id FROM users WHERE identifier=?", (ident,)).fetchone()
    out = {"message": msg}
    if u:
        code = f"{random.SystemRandom().randrange(10**6):06d}"
        db.execute("UPDATE reset_codes SET used=1 WHERE user_id=?", (u["id"],))
        db.execute("INSERT INTO reset_codes(user_id, code_hash, expires_at) VALUES(?,?,?)",
                   (u["id"], hash_code(code), logic.iso(logic.now() + timedelta(minutes=15))))
        db.commit()
        if config.DEV_MODE:           # pas de SMS/e-mail branché : code renvoyé en mode développement
            out["dev_code"] = code
    return out


@router.post("/reset-password", summary="Définir un nouveau mot de passe avec le code")
def reset_password(body: ResetIn, db: sqlite3.Connection = Depends(get_db)):
    bad = HTTPException(400, "Code invalide ou expiré.")
    try:
        ident = normalize_identifier(body.identifier)
    except ValueError:
        raise bad
    u = db.execute("SELECT id FROM users WHERE identifier=?", (ident,)).fetchone()
    if not u:
        raise bad
    rc = db.execute("SELECT * FROM reset_codes WHERE user_id=? AND used=0 ORDER BY id DESC LIMIT 1",
                    (u["id"],)).fetchone()
    if not rc or logic.parse(rc["expires_at"]) < logic.now() or rc["attempts"] >= 5:
        raise bad
    if rc["code_hash"] != hash_code(body.code):
        db.execute("UPDATE reset_codes SET attempts=attempts+1 WHERE id=?", (rc["id"],))
        db.commit()
        raise bad
    db.execute("UPDATE reset_codes SET used=1 WHERE id=?", (rc["id"],))
    db.execute("UPDATE users SET password_hash=? WHERE id=?", (hash_password(body.new_password), u["id"]))
    db.commit()
    return {"message": "Mot de passe réinitialisé. Vous pouvez vous connecter."}
