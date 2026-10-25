import sqlite3

from fastapi import APIRouter, Depends, HTTPException

from .. import logic
from ..db import get_db
from ..deps import Ctx, admin_member, current_member, current_user
from ..schemas import ColocationIn, ColocationPatch, JoinIn, RolePatch

router = APIRouter(prefix="/colocation", tags=["Colocation & membres"])


def coloc_out(ctx: Ctx) -> dict:
    c, db = ctx.coloc, ctx.db
    n = db.execute("SELECT COUNT(*) FROM members WHERE colocation_id=?", (c["id"],)).fetchone()[0]
    return {
        "id": c["id"], "name": c["name"], "city": c["city"], "occupants": c["occupants"],
        "members_count": n, "split_mode": c["split_mode"], "role": ctx.role,
        "invite_code": c["invite_code"], "invite_expires_at": c["invite_expires_at"],
        "has_meter": logic.get_meter(db, c["id"]) is not None, "created_at": c["created_at"],
    }


def _ctx_for(db, user) -> Ctx:
    row = db.execute("SELECT c.*, m.role AS role FROM members m JOIN colocations c ON c.id=m.colocation_id "
                     "WHERE m.user_id=?", (user["id"],)).fetchone()
    return Ctx(db, user, row, row["role"])


def _share_text(c) -> str:
    return f"Rejoins ma colocation sur KaziWatts avec le code {c['invite_code']} (valable 7 jours)."


@router.post("", status_code=201, summary="Créer une colocation (le créateur devient admin)")
def create(body: ColocationIn, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    if db.execute("SELECT 1 FROM members WHERE user_id=?", (user["id"],)).fetchone():
        raise HTTPException(409, "Vous appartenez déjà à une colocation. Quittez-la d'abord.")
    ts = logic.iso(logic.now())
    cur = db.execute("INSERT INTO colocations(name, city, occupants, invite_code, invite_expires_at, created_at) "
                     "VALUES(?,?,?,?,?,?)",
                     (body.name, body.city, body.occupants, logic.new_invite_code(db), logic.invite_expiry(), ts))
    db.execute("INSERT INTO members(colocation_id, user_id, role, joined_at) VALUES(?,?,?,?)",
               (cur.lastrowid, user["id"], "admin", ts))
    db.commit()
    return coloc_out(_ctx_for(db, user))


@router.post("/join", summary="Rejoindre avec un code d'invitation")
def join(body: JoinIn, user=Depends(current_user), db: sqlite3.Connection = Depends(get_db)):
    if db.execute("SELECT 1 FROM members WHERE user_id=?", (user["id"],)).fetchone():
        raise HTTPException(409, "Vous appartenez déjà à une colocation.")
    c = db.execute("SELECT * FROM colocations WHERE invite_code=?", (body.code.strip().upper(),)).fetchone()
    if not c:
        raise HTTPException(404, "Code d'invitation inconnu.")
    if logic.parse(c["invite_expires_at"]) < logic.now():
        raise HTTPException(410, "Ce code a expiré. Demandez-en un nouveau à l'administrateur.")
    n = db.execute("SELECT COUNT(*) FROM members WHERE colocation_id=?", (c["id"],)).fetchone()[0]
    if n >= c["occupants"]:
        raise HTTPException(409, "Cette colocation est complète.")
    db.execute("INSERT INTO members(colocation_id, user_id, role, joined_at) VALUES(?,?,?,?)",
               (c["id"], user["id"], "member", logic.iso(logic.now())))
    db.commit()
    return coloc_out(_ctx_for(db, user))


@router.get("", summary="Ma colocation")
def get_mine(ctx: Ctx = Depends(current_member)):
    return coloc_out(ctx)


@router.patch("", summary="Modifier nom, quartier, nombre d'occupants (admin)")
def patch(body: ColocationPatch, ctx: Ctx = Depends(admin_member)):
    c, db = ctx.coloc, ctx.db
    n = db.execute("SELECT COUNT(*) FROM members WHERE colocation_id=?", (c["id"],)).fetchone()[0]
    occ = body.occupants if body.occupants is not None else c["occupants"]
    if occ < n:
        raise HTTPException(422, f"Le nombre d'occupants ne peut pas être inférieur aux membres actuels ({n}).")
    db.execute("UPDATE colocations SET name=?, city=?, occupants=? WHERE id=?",
               (" ".join((body.name or c["name"]).split()), " ".join((body.city or c["city"]).split()), occ, c["id"]))
    db.commit()
    return coloc_out(_ctx_for(db, ctx.user))


@router.get("/invite", summary="Code de partage + texte à partager")
def invite(ctx: Ctx = Depends(current_member)):
    c = ctx.coloc
    return {"code": c["invite_code"], "expires_at": c["invite_expires_at"],
            "expired": logic.parse(c["invite_expires_at"]) < logic.now(), "share_text": _share_text(c)}


@router.post("/invite/regenerate", summary="Nouveau code valable 7 jours (admin)")
def regenerate(ctx: Ctx = Depends(admin_member)):
    db = ctx.db
    db.execute("UPDATE colocations SET invite_code=?, invite_expires_at=? WHERE id=?",
               (logic.new_invite_code(db), logic.invite_expiry(), ctx.coloc["id"]))
    db.commit()
    return invite(_ctx_for(db, ctx.user))


@router.get("/members", summary="Colocataires")
def members(ctx: Ctx = Depends(current_member)):
    db, c = ctx.db, ctx.coloc
    period = logic.current_period()
    ms = logic.members_of(db, c["id"])
    bal = {b["user_id"]: b for b in logic.balances(db, c, ms)}
    pres = logic._presence_weights(db, c["id"], period, [m["user_id"] for m in ms])
    items = [{
        "user_id": m["user_id"], "full_name": m["full_name"], "initials": logic.initials(m["full_name"]),
        "role": m["role"], "is_admin": m["role"] == "admin", "joined_at": m["joined_at"],
        "presence_days": int(pres[m["user_id"]]), "balance_kwh": bal[m["user_id"]]["balance_kwh"],
        "can_remove": ctx.is_admin and m["role"] != "admin",
        "is_me": m["user_id"] == ctx.user["id"],
    } for m in ms]
    return {"count": len(items), "capacity": c["occupants"], "items": items}


@router.patch("/members/{user_id}", summary="Changer le rôle d'un membre (admin)")
def set_role(user_id: int, body: RolePatch, ctx: Ctx = Depends(admin_member)):
    db, cid = ctx.db, ctx.coloc["id"]
    target = db.execute("SELECT role FROM members WHERE colocation_id=? AND user_id=?", (cid, user_id)).fetchone()
    if not target:
        raise HTTPException(404, "Membre introuvable.")
    if target["role"] == "admin" and body.role == "member":
        admins = db.execute("SELECT COUNT(*) FROM members WHERE colocation_id=? AND role='admin'", (cid,)).fetchone()[0]
        if admins <= 1:
            raise HTTPException(409, "Il faut au moins un administrateur dans la colocation.")
    db.execute("UPDATE members SET role=? WHERE colocation_id=? AND user_id=?", (body.role, cid, user_id))
    db.commit()
    return {"user_id": user_id, "role": body.role}


@router.delete("/members/{user_id}", summary="Retirer un colocataire (admin)")
def remove_member(user_id: int, ctx: Ctx = Depends(admin_member)):
    db, cid = ctx.db, ctx.coloc["id"]
    target = db.execute("SELECT role FROM members WHERE colocation_id=? AND user_id=?", (cid, user_id)).fetchone()
    if not target:
        raise HTTPException(404, "Membre introuvable.")
    if target["role"] == "admin":
        raise HTTPException(409, "Un administrateur ne peut pas être retiré (changez d'abord son rôle).")
    db.execute("DELETE FROM members WHERE colocation_id=? AND user_id=?", (cid, user_id))
    db.commit()
    return {"removed": user_id}


@router.post("/leave", summary="Quitter la colocation")
def leave(ctx: Ctx = Depends(current_member)):
    db, cid, uid = ctx.db, ctx.coloc["id"], ctx.user["id"]
    others = db.execute("SELECT COUNT(*) FROM members WHERE colocation_id=? AND user_id<>?", (cid, uid)).fetchone()[0]
    if others == 0:
        db.execute("DELETE FROM colocations WHERE id=?", (cid,))   # dernier membre : suppression en cascade
    else:
        if ctx.is_admin:
            admins = db.execute("SELECT COUNT(*) FROM members WHERE colocation_id=? AND role='admin'", (cid,)).fetchone()[0]
            if admins <= 1:
                raise HTTPException(409, "Nommez un autre administrateur avant de quitter la colocation.")
        db.execute("DELETE FROM members WHERE colocation_id=? AND user_id=?", (cid, uid))
    db.commit()
    return {"left": True}
