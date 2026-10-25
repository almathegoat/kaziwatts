from fastapi import APIRouter, Depends, HTTPException, Query

from .. import logic
from ..deps import Ctx, admin_member, current_member
from ..schemas import MeterIn, ReadingIn

router = APIRouter(tags=["Compteur & relevés"])


def meter_out(db, cid) -> dict | None:
    m = logic.get_meter(db, cid)
    if not m:
        return None
    last = logic.last_reading(db, cid)
    return {"number": m["number"], "type": m["type"], "initial_index": m["initial_index"],
            "tariff_fcfa": m["tariff_fcfa"], "last_index": last["idx"] if last else m["initial_index"],
            "last_read_at": last["read_at"] if last else None, "updated_at": m["updated_at"]}


def reading_out(r, name, tariff) -> dict:
    return {"id": r["id"], "read_at": r["read_at"], "author_id": r["author_id"], "author": name,
            "index": r["idx"], "delta_kwh": round(r["delta"], 2), "estimated_fcfa": round(r["delta"] * tariff),
            "is_initial": False}


@router.get("/meter", summary="Configuration du compteur")
def get_meter(ctx: Ctx = Depends(current_member)):
    m = meter_out(ctx.db, ctx.coloc["id"])
    if not m:
        raise HTTPException(404, "Aucun compteur configuré pour cette colocation.")
    return m


@router.put("/meter", summary="Configurer le compteur (admin) : numéro, type, index initial, tarif")
def put_meter(body: MeterIn, ctx: Ctx = Depends(admin_member)):
    db, cid = ctx.db, ctx.coloc["id"]
    ts = logic.iso(logic.now())
    existing = logic.get_meter(db, cid)
    if not existing:
        db.execute("INSERT INTO meters(colocation_id, number, type, initial_index, tariff_fcfa, updated_at) "
                   "VALUES(?,?,?,?,?,?)", (cid, body.number.strip(), body.type, body.initial_index, body.tariff_fcfa, ts))
        db.execute("INSERT INTO readings(colocation_id, author_id, idx, delta, read_at) VALUES(?,?,?,?,?)",
                   (cid, ctx.user["id"], body.initial_index, 0, ts))
        db.executemany(
            "INSERT INTO reminders(colocation_id, kind, title, subtitle, created_by, created_at) VALUES(?,?,?,?,?,?)",
            [(cid, "reading_monthly", "Relevé du compteur", "Chaque 1er du mois à 8 h · Tous", ctx.user["id"], ts),
             (cid, "low_credit", "Crédit bas (automatique)", "Autonomie < 3 jours · Tous", ctx.user["id"], ts)])
    else:
        n = db.execute("SELECT COUNT(*) FROM readings WHERE colocation_id=?", (cid,)).fetchone()[0]
        if body.initial_index != existing["initial_index"]:
            if n > 1:
                raise HTTPException(409, "L'index initial ne peut plus être modifié : des relevés existent déjà.")
            db.execute("UPDATE readings SET idx=? WHERE colocation_id=?", (body.initial_index, cid))
        db.execute("UPDATE meters SET number=?, type=?, initial_index=?, tariff_fcfa=?, updated_at=? WHERE colocation_id=?",
                   (body.number.strip(), body.type, body.initial_index, body.tariff_fcfa, ts, cid))
    db.commit()
    return meter_out(db, cid)


@router.get("/meter/quote", summary="Conversion montant → kWh au tarif du compteur")
def quote(amount_fcfa: int = Query(gt=0), ctx: Ctx = Depends(current_member)):
    m = logic.require_meter(ctx.db, ctx.coloc["id"])
    return {"amount_fcfa": amount_fcfa, "tariff_fcfa": m["tariff_fcfa"], "kwh": round(amount_fcfa / m["tariff_fcfa"], 2)}


@router.get("/readings", summary="Historique des relevés (du plus récent au plus ancien)")
def list_readings(limit: int = Query(50, ge=1, le=500), ctx: Ctx = Depends(current_member)):
    db, cid = ctx.db, ctx.coloc["id"]
    m = logic.require_meter(db, cid)
    rows = db.execute("SELECT r.*, u.full_name FROM readings r LEFT JOIN users u ON u.id=r.author_id "
                      "WHERE r.colocation_id=? ORDER BY r.read_at DESC, r.id DESC LIMIT ?", (cid, limit)).fetchall()
    first_id = db.execute("SELECT MIN(id) FROM readings WHERE colocation_id=?", (cid,)).fetchone()[0]
    items = []
    for r in rows:
        o = reading_out(r, r["full_name"], m["tariff_fcfa"])
        o["is_initial"] = r["id"] == first_id
        items.append(o)
    return {"count": len(items), "items": items}


@router.get("/readings/preview", summary="Consommation estimée pour un index saisi (sans enregistrer)")
def preview(index: float = Query(ge=0), ctx: Ctx = Depends(current_member)):
    db, cid = ctx.db, ctx.coloc["id"]
    m = logic.require_meter(db, cid)
    last = logic.last_reading(db, cid)
    if index <= last["idx"]:
        raise HTTPException(422, f"Le nouvel index doit être supérieur au dernier relevé ({logic.fr_num(last['idx'])} kWh).")
    d = index - last["idx"]
    return {"last_index": last["idx"], "delta_kwh": round(d, 2), "estimated_fcfa": round(d * m["tariff_fcfa"])}


@router.post("/readings", status_code=201, summary="Enregistrer un relevé de compteur")
def add_reading(body: ReadingIn, ctx: Ctx = Depends(current_member)):
    db, cid = ctx.db, ctx.coloc["id"]
    m = logic.require_meter(db, cid)
    last = logic.last_reading(db, cid)
    if body.index <= last["idx"]:
        raise HTTPException(422, f"Le nouvel index doit être supérieur au dernier relevé ({logic.fr_num(last['idx'])} kWh).")
    at = logic.parse_read_at(body.read_at)
    if at > logic.now() + logic.timedelta(minutes=5):
        raise HTTPException(422, "La date du relevé ne peut pas être dans le futur.")
    if at < logic.parse(last["read_at"]):
        raise HTTPException(422, "La date ne peut pas être antérieure au dernier relevé.")
    delta = round(body.index - last["idx"], 3)
    cur = db.execute("INSERT INTO readings(colocation_id, author_id, idx, delta, read_at) VALUES(?,?,?,?,?)",
                     (cid, ctx.user["id"], body.index, delta, logic.iso(at)))
    db.commit()
    r = db.execute("SELECT * FROM readings WHERE id=?", (cur.lastrowid,)).fetchone()
    o = reading_out(r, ctx.user["full_name"], m["tariff_fcfa"])
    o["is_initial"] = False
    return o
