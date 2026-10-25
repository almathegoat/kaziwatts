import random
import re
import sqlite3
import uuid
from pathlib import Path

from fastapi import APIRouter, Depends, File, HTTPException, Query, UploadFile
from fastapi.responses import FileResponse, PlainTextResponse

from .. import config, logic
from ..deps import Ctx, admin_member, current_member
from ..schemas import ConsumptionsIn, DecisionIn, PaymentIn, PresenceIn, SplitSettingsIn

router = APIRouter(tags=["Répartition, paiements & soldes"])

MONTHS_ABBR = ["janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc."]
STATUS_ARROW = {"creditor": "Créditeur ▲", "debtor": "Débiteur ▼", "balanced": "Équilibré"}


def _fmt_date(ts: str, with_time=False) -> str:
    d = logic.parse(ts)
    s = f"{d.day} {MONTHS_ABBR[d.month - 1]} {d.year}"
    return s + (f" · {d:%H:%M}" if with_time else "")


# ====================================================================== SOLDES
@router.get("/balance", summary="Soldes par colocataire (kWh et FCFA), dettes et remboursements suggérés")
def balance(only_debtors: bool = False, ctx: Ctx = Depends(current_member)):
    db, c = ctx.db, ctx.coloc
    m = logic.require_meter(db, c["id"])
    tariff = m["tariff_fcfa"]
    items = logic.balances(db, c, logic.members_of(db, c["id"]))
    sett = logic.settlements(items, tariff)
    for i in items:
        i["balance_fcfa"] = round(i["balance_kwh"] * tariff)
        i["status_label"] = STATUS_ARROW[i["status"]]
        i["is_me"] = i["user_id"] == ctx.user["id"]
    shown = [i for i in items if i["status"] == "debtor"] if only_debtors else items
    credit = logic.credit_kwh(db, c["id"])
    return {"tariff_fcfa": tariff, "meter_type": m["type"], "credit_kwh": credit,
            "credit_fcfa": round(credit * tariff), "items": shown, "settlements": sett}


# ====================================================================== RÉPARTITION
def _split_period(db, cid, period):
    if period:
        if not logic.valid_period(period):
            raise HTTPException(422, "Période invalide (format AAAA-MM).")
        return period
    ps = logic.reading_periods(db, cid)
    return ps[-1] if ps else logic.current_period()


def _clean_entries(ctx: Ctx, entries, period):
    if period is not None and not logic.valid_period(period):
        raise HTTPException(422, "Période invalide (format AAAA-MM).")
    uids = {m["user_id"] for m in logic.members_of(ctx.db, ctx.coloc["id"])}
    for e in entries:
        if e.user_id not in uids:
            raise HTTPException(422, f"L'utilisateur {e.user_id} n'est pas membre de la colocation.")
        if e.user_id != ctx.user["id"] and not ctx.is_admin:
            raise HTTPException(403, "Vous ne pouvez saisir que vos propres données (sauf administrateur).")
    return period or logic.current_period()


@router.get("/split", summary="Répartition de la consommation d'une période")
def split(period: str | None = None, ctx: Ctx = Depends(current_member)):
    db, c = ctx.db, ctx.coloc
    m = logic.require_meter(db, c["id"])
    period = _split_period(db, c["id"], period)
    ms = logic.members_of(db, c["id"])
    shares, total, applied = logic.allocate(db, c, period, ms)
    bal = {b["user_id"]: b for b in logic.balances(db, c, ms)}
    out = []
    for mm in ms:
        u = mm["user_id"]
        k = shares.get(u, 0)
        out.append({"user_id": u, "name": mm["full_name"], "initials": logic.initials(mm["full_name"]),
                    "kwh": round(k, 2), "percent": round(k / total * 100, 1) if total else 0.0,
                    "amount_fcfa": round(k * m["tariff_fcfa"]), "status": bal[u]["status"],
                    "status_label": STATUS_ARROW[bal[u]["status"]]})
    cons = {r["user_id"]: r["kwh"] for r in db.execute(
        "SELECT user_id, kwh FROM consumptions WHERE colocation_id=? AND period=?", (c["id"], period))}
    pres = logic._presence_weights(db, c["id"], period, [mm["user_id"] for mm in ms])
    return {"period": period, "period_label": logic.period_label(period), "mode": c["split_mode"],
            "applied_mode": applied, "total_kwh": round(total, 2), "tariff_fcfa": m["tariff_fcfa"],
            "amount_fcfa": round(total * m["tariff_fcfa"]), "shares": out,
            "inputs": {"consumptions": [{"user_id": mm["user_id"], "name": mm["full_name"],
                                         "kwh": cons.get(mm["user_id"])} for mm in ms],
                       "presence": [{"user_id": mm["user_id"], "name": mm["full_name"],
                                     "days": int(pres[mm["user_id"]])} for mm in ms]}}


@router.put("/split/settings", summary="Mode de répartition : consumption | presence | equal (admin)")
def split_settings(body: SplitSettingsIn, ctx: Ctx = Depends(admin_member)):
    ctx.db.execute("UPDATE colocations SET split_mode=? WHERE id=?", (body.mode, ctx.coloc["id"]))
    ctx.db.commit()
    return {"mode": body.mode}


@router.put("/split/consumptions", summary="Saisir les kWh par colocataire (sous-compteurs) pour une période")
def put_consumptions(body: ConsumptionsIn, ctx: Ctx = Depends(current_member)):
    period = _clean_entries(ctx, body.entries, body.period)
    for e in body.entries:
        ctx.db.execute("INSERT INTO consumptions(colocation_id,user_id,period,kwh) VALUES(?,?,?,?) "
                       "ON CONFLICT(colocation_id,user_id,period) DO UPDATE SET kwh=excluded.kwh",
                       (ctx.coloc["id"], e.user_id, period, e.kwh))
    ctx.db.commit()
    return {"period": period, "saved": len(body.entries)}


@router.put("/split/presence", summary="Saisir les jours de présence par colocataire pour une période")
def put_presence(body: PresenceIn, ctx: Ctx = Depends(current_member)):
    period = _clean_entries(ctx, body.entries, body.period)
    for e in body.entries:
        if e.days > logic.days_in_period(period):
            raise HTTPException(422, f"Maximum {logic.days_in_period(period)} jours pour {logic.period_label(period)}.")
        ctx.db.execute("INSERT INTO presence(colocation_id,user_id,period,days) VALUES(?,?,?,?) "
                       "ON CONFLICT(colocation_id,user_id,period) DO UPDATE SET days=excluded.days",
                       (ctx.coloc["id"], e.user_id, period, e.days))
    ctx.db.commit()
    return {"period": period, "saved": len(body.entries)}


# ====================================================================== PAIEMENTS
_BASE = ("SELECT p.*, pu.full_name AS payer_name, bu.full_name AS ben_name FROM payments p "
         "JOIN users pu ON pu.id=p.payer_id LEFT JOIN users bu ON bu.id=p.beneficiary_id ")


def _can_decide(p, uid, is_admin) -> bool:
    return p["status"] == "pending" and uid != p["payer_id"] and (is_admin or uid == p["beneficiary_id"])


def payment_out(p, ctx: Ctx) -> dict:
    uid = ctx.user["id"]
    names = p["payer_name"] + (f" → {p['ben_name']}" if p["ben_name"] else "")
    return {
        "id": p["id"], "kind": p["kind"], "kind_label": logic.KIND_LABEL[p["kind"]],
        "title": f"{names} — {logic.KIND_LABEL[p['kind']]}",
        "payer": {"id": p["payer_id"], "name": p["payer_name"], "initials": logic.initials(p["payer_name"])},
        "beneficiary": ({"id": p["beneficiary_id"], "name": p["ben_name"], "initials": logic.initials(p["ben_name"])}
                        if p["beneficiary_id"] else None),
        "amount_fcfa": p["amount_fcfa"], "kwh": p["kwh"],
        "method": p["method"], "method_label": logic.METHOD_LABEL[p["method"]],
        "reference": p["reference"], "status": p["status"], "status_label": logic.STATUS_LABEL[p["status"]],
        "created_at": p["created_at"], "date_label": _fmt_date(p["created_at"]), "decided_at": p["decided_at"],
        "has_proof": bool(p["proof_file"]), "proof_name": p["proof_name"],
        "can_decide": _can_decide(p, uid, ctx.is_admin),
        "can_cancel": p["status"] == "pending" and uid in (p["payer_id"], p["created_by"]),
    }


def _get_payment(ctx: Ctx, pid: int):
    p = ctx.db.execute(_BASE + "WHERE p.id=? AND p.colocation_id=?", (pid, ctx.coloc["id"])).fetchone()
    if not p:
        raise HTTPException(404, "Paiement introuvable.")
    return p


@router.post("/payments", status_code=201, summary="Enregistrer une recharge, une contribution ou un remboursement")
def create_payment(body: PaymentIn, ctx: Ctx = Depends(current_member)):
    db, cid, uid = ctx.db, ctx.coloc["id"], ctx.user["id"]
    meter = logic.require_meter(db, cid)
    payer = body.payer_id or uid
    ben = body.beneficiary_id if body.kind == "reimbursement" else None
    if not logic.is_member(db, cid, payer):
        raise HTTPException(422, "Le payeur n'est pas membre de la colocation.")
    if body.kind == "reimbursement":
        if not ben:
            raise HTTPException(422, "Choisissez le bénéficiaire du remboursement.")
        if ben == payer:
            raise HTTPException(422, "Le payeur et le bénéficiaire doivent être différents.")
        if not logic.is_member(db, cid, ben):
            raise HTTPException(422, "Le bénéficiaire n'est pas membre de la colocation.")
    if payer != uid and not (ctx.is_admin or (body.kind == "reimbursement" and ben == uid)):
        raise HTTPException(403, "Seul l'administrateur peut enregistrer un paiement pour un autre colocataire.")

    ref = (body.reference or "").strip() or "TX" + "".join(random.choices("0123456789", k=9))
    if db.execute("SELECT 1 FROM payments WHERE colocation_id=? AND reference=?", (cid, ref)).fetchone():
        raise HTTPException(409, "Cette référence de transaction existe déjà.")

    # confirmé d'emblée si l'auteur est habilité à confirmer (admin ou bénéficiaire, et pas le payeur),
    # ou si l'admin enregistre sa propre recharge/contribution (personne d'autre ne peut la valider)
    entitled = ctx.is_admin or (ben is not None and ben == uid)
    auto = (uid != payer and entitled) or (uid == payer and ctx.is_admin and body.kind != "reimbursement")
    ts = logic.iso(logic.now())
    cur = db.execute(
        "INSERT INTO payments(colocation_id,payer_id,beneficiary_id,kind,amount_fcfa,kwh,method,reference,status,"
        "created_by,created_at,decided_at,decided_by) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)",
        (cid, payer, ben, body.kind, body.amount_fcfa, round(body.amount_fcfa / meter["tariff_fcfa"], 2),
         body.method, ref, "confirmed" if auto else "pending", uid, ts, ts if auto else None, uid if auto else None))
    db.commit()
    return payment_out(_get_payment(ctx, cur.lastrowid), ctx)


def _periods(period: str | None) -> list[str] | None:
    if not period or period == "all":
        return None
    cur = logic.current_period()
    if period == "this_month":
        return [cur]
    if period == "last_month":
        return [logic.shift_period(cur, -1)]
    if period == "last_3_months":
        return [logic.shift_period(cur, -i) for i in range(3)]
    if logic.valid_period(period):
        return [period]
    raise HTTPException(422, "Période invalide (this_month, last_month, last_3_months, all ou AAAA-MM).")


@router.get("/payments", summary="Historique des paiements (filtres : mode, période, statut, type)")
def list_payments(method_group: str = Query("all", pattern="^(all|mobile_money|cash)$"),
                  period: str | None = None, status: str | None = Query(None, pattern="^(pending|confirmed|rejected)$"),
                  kind: str | None = Query(None, pattern="^(recharge|contribution|reimbursement)$"),
                  mine: bool = False, ctx: Ctx = Depends(current_member)):
    sql, args = _BASE + "WHERE p.colocation_id=?", [ctx.coloc["id"]]
    if method_group == "mobile_money":
        sql += " AND p.method IN ('mtn_momo','orange_money')"
    elif method_group == "cash":
        sql += " AND p.method='cash'"
    ps = _periods(period)
    if ps:
        sql += f" AND substr(p.created_at,1,7) IN ({','.join('?' * len(ps))})"
        args += ps
    if status:
        sql += " AND p.status=?"; args.append(status)
    if kind:
        sql += " AND p.kind=?"; args.append(kind)
    if mine:
        sql += " AND (p.payer_id=? OR p.beneficiary_id=?)"; args += [ctx.user["id"]] * 2
    rows = ctx.db.execute(sql + " ORDER BY p.created_at DESC, p.id DESC", args).fetchall()
    return {"count": len(rows), "items": [payment_out(r, ctx) for r in rows]}


@router.get("/payments/{pid}", summary="Détail d'un paiement")
def get_payment(pid: int, ctx: Ctx = Depends(current_member)):
    return payment_out(_get_payment(ctx, pid), ctx)


@router.post("/payments/{pid}/decision", summary="Confirmer ou refuser un paiement en attente")
def decide(pid: int, body: DecisionIn, ctx: Ctx = Depends(current_member)):
    p = _get_payment(ctx, pid)
    if p["status"] != "pending":
        raise HTTPException(409, "Ce paiement a déjà été traité.")
    if not _can_decide(p, ctx.user["id"], ctx.is_admin):
        raise HTTPException(403, "Seul le bénéficiaire ou l'administrateur (autre que le payeur) peut décider.")
    ctx.db.execute("UPDATE payments SET status=?, decided_at=?, decided_by=? WHERE id=?",
                   ("confirmed" if body.action == "confirm" else "rejected", logic.iso(logic.now()), ctx.user["id"], pid))
    ctx.db.commit()
    return payment_out(_get_payment(ctx, pid), ctx)


@router.delete("/payments/{pid}", summary="Annuler un paiement en attente")
def cancel(pid: int, ctx: Ctx = Depends(current_member)):
    p = _get_payment(ctx, pid)
    if p["status"] != "pending":
        raise HTTPException(409, "Seul un paiement en attente peut être annulé.")
    if ctx.user["id"] not in (p["payer_id"], p["created_by"]):
        raise HTTPException(403, "Seul l'auteur du paiement peut l'annuler.")
    if p["proof_file"]:
        (config.UPLOAD_DIR / p["proof_file"]).unlink(missing_ok=True)
    ctx.db.execute("DELETE FROM payments WHERE id=?", (pid,))
    ctx.db.commit()
    return {"deleted": pid}


# ---- preuve de paiement (capture / photo / PDF)
_EXT = {".jpg", ".jpeg", ".png", ".pdf"}


@router.post("/payments/{pid}/proof", summary="Joindre une preuve (JPG, PNG ou PDF, 5 Mo max)")
async def upload_proof(pid: int, file: UploadFile = File(...), ctx: Ctx = Depends(current_member)):
    p = _get_payment(ctx, pid)
    if not (ctx.is_admin or ctx.user["id"] in (p["payer_id"], p["created_by"])):
        raise HTTPException(403, "Vous ne pouvez pas modifier ce paiement.")
    ext = Path(file.filename or "").suffix.lower()
    if ext not in _EXT:
        raise HTTPException(422, "Format non pris en charge (JPG, PNG ou PDF).")
    data = await file.read(config.MAX_PROOF_BYTES + 1)
    if len(data) > config.MAX_PROOF_BYTES:
        raise HTTPException(413, "Fichier trop volumineux (5 Mo maximum).")
    if not data:
        raise HTTPException(422, "Fichier vide.")
    config.UPLOAD_DIR.mkdir(parents=True, exist_ok=True)
    name = f"{ctx.coloc['id']}_{pid}_{uuid.uuid4().hex[:10]}{ext}"
    (config.UPLOAD_DIR / name).write_bytes(data)
    if p["proof_file"]:
        (config.UPLOAD_DIR / p["proof_file"]).unlink(missing_ok=True)
    safe = re.sub(r"[^\w.\- ]", "_", Path(file.filename).name)[:80]
    ctx.db.execute("UPDATE payments SET proof_file=?, proof_name=? WHERE id=?", (name, safe, pid))
    ctx.db.commit()
    return payment_out(_get_payment(ctx, pid), ctx)


@router.get("/payments/{pid}/proof", summary="Télécharger la preuve jointe")
def get_proof(pid: int, ctx: Ctx = Depends(current_member)):
    p = _get_payment(ctx, pid)
    f = config.UPLOAD_DIR / p["proof_file"] if p["proof_file"] else None
    if not f or not f.exists():
        raise HTTPException(404, "Aucune preuve jointe.")
    return FileResponse(f, filename=p["proof_name"] or f.name)


# ---- justificatif
def _receipt(p, ctx: Ctx) -> dict:
    status = {"confirmed": "Réussi", "pending": "En attente de confirmation", "rejected": "Refusé"}[p["status"]]
    who = f"De : {p['payer_name']}" + (f"\nÀ : {p['ben_name']}" if p["ben_name"] else "")
    when = _fmt_date(p["created_at"], with_time=True)
    text = (f"Reçu KaziWatts · {logic.KIND_LABEL[p['kind']]}\n"
            f"Montant : {logic.fcfa(p['amount_fcfa'])}\n{who}\nDate : {when}\n"
            f"Référence : {p['reference']}\nMode : {logic.METHOD_LABEL[p['method']]}\n"
            f"Statut : {status}\nColocation : {ctx.coloc['name']}\n")
    short = (f"Paiement : {logic.fcfa(p['amount_fcfa'])} · {p['payer_name']}"
             + (f" → {p['ben_name']}" if p["ben_name"] else "")
             + f" · {when} · Réf. {p['reference']} · {logic.METHOD_LABEL[p['method']]} · {status}")
    return {"payment": payment_out(p, ctx), "status_text": status, "date_time": when,
            "colocation": ctx.coloc["name"], "text": text, "share_text": short}


@router.get("/payments/{pid}/receipt", summary="Justificatif (JSON) : champs + texte à partager")
def receipt(pid: int, ctx: Ctx = Depends(current_member)):
    return _receipt(_get_payment(ctx, pid), ctx)


@router.get("/payments/{pid}/receipt.txt", summary="Justificatif en fichier texte", response_class=PlainTextResponse)
def receipt_txt(pid: int, ctx: Ctx = Depends(current_member)):
    p = _get_payment(ctx, pid)
    return PlainTextResponse(_receipt(p, ctx)["text"], headers={
        "Content-Disposition": f'attachment; filename="justificatif-KaziWatts-{p["reference"]}.txt"'})
