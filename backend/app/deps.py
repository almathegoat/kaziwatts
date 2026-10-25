"""Dépendances d'authentification et de contexte (utilisateur + colocation)."""
import sqlite3
from dataclasses import dataclass

from fastapi import Depends, HTTPException
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

from .db import get_db
from .security import decode_token

bearer = HTTPBearer(auto_error=False)


def current_user(creds: HTTPAuthorizationCredentials | None = Depends(bearer),
                 db: sqlite3.Connection = Depends(get_db)):
    if creds is None:
        raise HTTPException(401, "Connexion requise.", headers={"WWW-Authenticate": "Bearer"})
    uid = decode_token(creds.credentials)
    user = db.execute("SELECT * FROM users WHERE id=?", (uid,)).fetchone() if uid else None
    if not user:
        raise HTTPException(401, "Session expirée, reconnectez-vous.", headers={"WWW-Authenticate": "Bearer"})
    return user


@dataclass
class Ctx:
    db: sqlite3.Connection
    user: sqlite3.Row
    coloc: sqlite3.Row
    role: str

    @property
    def is_admin(self) -> bool:
        return self.role == "admin"


def current_member(user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)) -> Ctx:
    row = db.execute("SELECT c.*, m.role AS role FROM members m JOIN colocations c ON c.id=m.colocation_id "
                     "WHERE m.user_id=?", (user["id"],)).fetchone()
    if not row:
        raise HTTPException(409, "Vous n'appartenez à aucune colocation.")
    return Ctx(db, user, row, row["role"])


def admin_member(ctx: Ctx = Depends(current_member)) -> Ctx:
    if not ctx.is_admin:
        raise HTTPException(403, "Action réservée à l'administrateur de la colocation.")
    return ctx
