import re
import sqlite3

from fastapi import APIRouter, Depends, HTTPException

from .. import config, logic
from ..deps import Ctx, current_member
from ..schemas import ReminderIn, ReminderPatch

router = APIRouter(tags=["Rappels, alertes & accueil"])
_DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")


def _check_date(d):
    if d is not None and d != "" and not _DATE.match(d):
        raise HTTPException(422, "Date invalide (format AAAA-MM-JJ).")


def reminder_out(r) -> dict:
    return {"id": r["id"], "kind": r["kind"], "title": r["title"], "subtitle": r["subtitle"],
            "due_date": r["due_date"], "active": bool(r["active"]), "automatic": r["kind"] != "custom"}


def _get(ctx: Ctx, rid: int):
    r = ctx.db.execute("SELECT * FROM reminders WHERE id=? AND colocation_id=?", (rid, ctx.coloc["id"])).fetchone()
    if not r:
        raise HTTPException(404, "Rappel introuvable.")
    return r


@router.get("/reminders", summary="Rappels de la colocation")
def list_reminders(ctx: Ctx = Depends(current_member)):
    rows = ctx.db.execute("SELECT * FROM reminders WHERE colocation_id=? ORDER BY id", (ctx.coloc["id"],)).fetchall()
    return {"count": len(rows), "items": [reminder_out(r) for r in rows]}


@router.post("/reminders", status_code=201, summary="Ajouter un rappel")
def add_reminder(body: ReminderIn, ctx: Ctx = Depends(current_member)):
    _check_date(body.due_date)
    cur = ctx.db.execute(
        "INSERT INTO reminders(colocation_id,kind,title,subtitle,due_date,created_by,created_at) VALUES(?,?,?,?,?,?,?)",
        (ctx.coloc["id"], "custom", body.title.strip(), body.subtitle.strip(), body.due_date or None,
         ctx.user["id"], logic.iso(logic.now())))
    ctx.db.commit()
    return reminder_out(_get(ctx, cur.lastrowid))


@router.patch("/reminders/{rid}", summary="Modifier / activer / désactiver un rappel")
def patch_reminder(rid: int, body: ReminderPatch, ctx: Ctx = Depends(current_member)):
    r = _get(ctx, rid)
    _check_date(body.due_date)
    if r["kind"] != "custom" and (body.title is not None or body.subtitle is not None or body.due_date is not None):
        raise HTTPException(422, "Un rappel automatique ne peut qu'être activé ou désactivé.")
    ctx.db.execute("UPDATE reminders SET title=?, subtitle=?, due_date=?, active=? WHERE id=?",
                   (body.title or r["title"], r["subtitle"] if body.subtitle is None else body.subtitle,
                    r["due_date"] if body.due_date is None else (body.due_date or None),
                    r["active"] if body.active is None else int(body.active), rid))
    ctx.db.commit()
    return reminder_out(_get(ctx, rid))


@router.delete("/reminders/{rid}", summary="Supprimer un rappel personnalisé")
def delete_reminder(rid: int, ctx: Ctx = Depends(current_member)):
    r = _get(ctx, rid)
    if r["kind"] != "custom":
        raise HTTPException(422, "Un rappel automatique ne se supprime pas : désactivez-le.")
    ctx.db.execute("DELETE FROM reminders WHERE id=?", (rid,))
    ctx.db.commit()
    return {"deleted": rid}


def compute_alerts(ctx: Ctx) -> list[dict]:
    """Alertes calculées : en retard (dettes), à venir (à confirmer / échéances), facture, crédit bas."""
    db, c, uid = ctx.db, ctx.coloc, ctx.user["id"]
    m = logic.get_meter(db, c["id"])
    if not m:
        return []
    out = []
    tariff = m["tariff_fcfa"]
    bal = logic.balances(db, c, logic.members_of(db, c["id"]))
    for b in bal:
        if b["status"] == "debtor":
            amt = round(-b["balance_kwh"] * tariff)
            out.append({"type": "late", "severity": "high", "target": "balance", "user_id": b["user_id"],
                        "title": f"{b['name']} doit {logic.fcfa(amt)}",
                        "detail": f"Solde : −{logic.fr_num(-b['balance_kwh'])} kWh"})
    pend = db.execute("SELECT p.*, pu.full_name AS payer_name FROM payments p JOIN users pu ON pu.id=p.payer_id "
                      "WHERE p.colocation_id=? AND p.status='pending' ORDER BY p.created_at", (c["id"],)).fetchall()
    for p in pend:
        if p["payer_id"] != uid and (ctx.is_admin or p["beneficiary_id"] == uid):
            out.append({"type": "soon", "severity": "medium", "target": "payments", "payment_id": p["id"],
                        "title": f"{p['payer_name']} : {logic.fcfa(p['amount_fcfa'])} à confirmer",
                        "detail": f"{logic.KIND_LABEL[p['kind']]} · {logic.METHOD_LABEL[p['method']]}"})
    today = logic.now().strftime("%Y-%m-%d")
    for r in db.execute("SELECT * FROM reminders WHERE colocation_id=? AND kind='custom' AND active=1 "
                        "AND due_date IS NOT NULL ORDER BY due_date", (c["id"],)):
        if r["due_date"] <= logic.shift_period(today[:7], 0) + "-31":   # simple : échéances du mois en cours et passées
            out.append({"type": "soon", "severity": "high" if r["due_date"] < today else "medium",
                        "target": "reminders", "reminder_id": r["id"], "title": r["title"],
                        "detail": f"Échéance : {r['due_date']}" + (" (dépassée)" if r["due_date"] < today else "")})
    periods = logic.reading_periods(db, c["id"])
    if periods:
        p = periods[-1]
        tot = logic.period_total_kwh(db, c["id"], p)
        out.append({"type": "bill", "severity": "info", "target": "split", "period": p,
                    "title": f"Répartition de {logic.period_label(p)} : {logic.fcfa(tot * tariff)}",
                    "detail": f"{logic.fr_num(tot)} kWh à répartir"})
    if m["type"] == "prepaid":
        low = db.execute("SELECT active FROM reminders WHERE colocation_id=? AND kind='low_credit'", (c["id"],)).fetchone()
        credit, days = logic.autonomy(db, c["id"])
        if (low is None or low["active"]) and days is not None and days < config.LOW_CREDIT_DAYS:
            out.append({"type": "low_credit", "severity": "high", "target": "recharge",
                        "title": "Crédit bas", "detail": f"Autonomie estimée : {logic.fr_num(days, 1)} jour(s)"})
    return out


@router.get("/alerts", summary="Alertes calculées (en retard, à venir, facture, crédit bas)")
def alerts(ctx: Ctx = Depends(current_member)):
    items = compute_alerts(ctx)
    return {"count": len(items), "late": [a for a in items if a["type"] == "late"],
            "soon": [a for a in items if a["type"] == "soon"],
            "bill": [a for a in items if a["type"] == "bill"],
            "low_credit": [a for a in items if a["type"] == "low_credit"]}


@router.get("/home", summary="Tableau de bord de l'accueil")
def home(ctx: Ctx = Depends(current_member)):
    db, c, uid = ctx.db, ctx.coloc, ctx.user["id"]
    m = logic.get_meter(db, c["id"])
    base = {"user": {"id": uid, "full_name": ctx.user["full_name"], "initials": logic.initials(ctx.user["full_name"]),
                     "role": ctx.role},
            "colocation": {"id": c["id"], "name": c["name"], "city": c["city"]}, "has_meter": m is not None}
    if not m:
        return base | {"meter": None, "alerts_count": 0}
    last = logic.last_reading(db, c["id"])
    credit, days = logic.autonomy(db, c["id"])
    ms = logic.members_of(db, c["id"])
    mine = next((b for b in logic.balances(db, c, ms) if b["user_id"] == uid), None)
    pending = db.execute("SELECT COUNT(*) FROM payments WHERE colocation_id=? AND status='pending'", (c["id"],)).fetchone()[0]
    out = base | {
        "meter": {"number": m["number"], "type": m["type"], "tariff_fcfa": m["tariff_fcfa"],
                  "last_index": last["idx"], "last_read_at": last["read_at"]},
        "avg_kwh_per_day": round(logic.avg_kwh_per_day(db, c["id"]), 2),
        "my_balance_kwh": mine["balance_kwh"] if mine else 0,
        "my_balance_fcfa": round(mine["balance_kwh"] * m["tariff_fcfa"]) if mine else 0,
        "pending_payments": pending, "alerts_count": len(compute_alerts(ctx)),
    }
    if m["type"] == "prepaid":
        out |= {"credit_kwh": credit, "credit_fcfa": round(credit * m["tariff_fcfa"]), "autonomy_days": days,
                "low_credit": days is not None and days < config.LOW_CREDIT_DAYS}
    return out
