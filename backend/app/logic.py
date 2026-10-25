"""Règles métier : périodes, répartition, soldes, crédit, autonomie."""
import calendar
import random
import re
import sqlite3
from collections import defaultdict
from datetime import datetime, timedelta, timezone

from fastapi import HTTPException

from . import config

LOCAL_TZ = timezone(timedelta(hours=1))  # Cameroun : UTC+1, pas d'heure d'été
MONTHS_FR = ["janvier", "février", "mars", "avril", "mai", "juin", "juillet",
             "août", "septembre", "octobre", "novembre", "décembre"]
EPS = 0.005

KIND_LABEL = {"recharge": "Recharge", "contribution": "Contribution", "reimbursement": "Remboursement"}
METHOD_LABEL = {"mtn_momo": "MTN MoMo", "orange_money": "Orange Money", "cash": "Espèces"}
STATUS_LABEL = {"pending": "En attente", "confirmed": "Confirmé", "rejected": "Refusé"}
INVITE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"


# ---------------------------------------------------------------- temps / formats
def now() -> datetime:
    return datetime.now(LOCAL_TZ)


def iso(dt: datetime) -> str:
    return dt.isoformat(timespec="seconds")


def parse(s: str) -> datetime:
    dt = datetime.fromisoformat(s)
    return dt if dt.tzinfo else dt.replace(tzinfo=LOCAL_TZ)


def initials(name: str) -> str:
    parts = [p for p in name.split() if p]
    if not parts:
        return "?"
    if len(parts) == 1:
        return parts[0][:2].upper()
    return (parts[0][0] + parts[-1][0]).upper()


def valid_period(p: str) -> bool:
    return bool(re.fullmatch(r"\d{4}-(0[1-9]|1[0-2])", p or ""))


def current_period() -> str:
    return now().strftime("%Y-%m")


def period_label(p: str) -> str:
    y, m = p.split("-")
    return f"{MONTHS_FR[int(m) - 1]} {y}"


def days_in_period(p: str) -> int:
    y, m = map(int, p.split("-"))
    return calendar.monthrange(y, m)[1]


def shift_period(p: str, n: int) -> str:
    y, m = map(int, p.split("-"))
    idx = y * 12 + (m - 1) + n
    return f"{idx // 12:04d}-{idx % 12 + 1:02d}"


def fr_num(v: float, max_dec: int = 2) -> str:
    s = f"{v:,.{max_dec}f}"
    ip, _, dec = s.partition(".")
    dec = dec.rstrip("0")
    return ip.replace(",", " ") + ("," + dec if dec else "")


def fcfa(v: float) -> str:
    return fr_num(round(v), 0) + " FCFA"


def parse_read_at(raw: str | None) -> datetime:
    n = now()
    if not raw:
        return n
    try:
        if len(raw) == 10:
            d = datetime.fromisoformat(raw)
            if d.date() == n.date():
                return n                      # « aujourd'hui » : heure actuelle
            return d.replace(hour=12, tzinfo=LOCAL_TZ)
        return parse(raw)
    except ValueError:
        raise HTTPException(422, "Date invalide (format attendu : AAAA-MM-JJ).")


# ---------------------------------------------------------------- invitations
def new_invite_code(db: sqlite3.Connection) -> str:
    while True:
        code = "".join(random.choices(INVITE_ALPHABET, k=6))
        if not db.execute("SELECT 1 FROM colocations WHERE invite_code=?", (code,)).fetchone():
            return code


def invite_expiry() -> str:
    return iso(now() + timedelta(days=config.INVITE_DAYS))


# ---------------------------------------------------------------- accès données
def get_meter(db, cid):
    return db.execute("SELECT * FROM meters WHERE colocation_id=?", (cid,)).fetchone()


def require_meter(db, cid):
    m = get_meter(db, cid)
    if not m:
        raise HTTPException(409, "Configurez d'abord le compteur de la colocation.")
    return m


def last_reading(db, cid):
    return db.execute("SELECT * FROM readings WHERE colocation_id=? ORDER BY read_at DESC, id DESC LIMIT 1",
                      (cid,)).fetchone()


def members_of(db, cid):
    return db.execute(
        "SELECT m.user_id, m.role, m.joined_at, u.full_name FROM members m "
        "JOIN users u ON u.id=m.user_id WHERE m.colocation_id=? ORDER BY m.id", (cid,)).fetchall()


def is_member(db, cid, uid) -> bool:
    return db.execute("SELECT 1 FROM members WHERE colocation_id=? AND user_id=?", (cid, uid)).fetchone() is not None


# ---------------------------------------------------------------- consommation / répartition
def reading_periods(db, cid) -> list[str]:
    return [r["p"] for r in db.execute(
        "SELECT DISTINCT substr(read_at,1,7) p FROM readings WHERE colocation_id=? AND delta>0 ORDER BY p", (cid,))]


def period_total_kwh(db, cid, period) -> float:
    return db.execute("SELECT COALESCE(SUM(delta),0) FROM readings WHERE colocation_id=? AND substr(read_at,1,7)=?",
                      (cid, period)).fetchone()[0]


def _presence_weights(db, cid, period, uids):
    rows = {r["user_id"]: r["days"] for r in db.execute(
        "SELECT user_id, days FROM presence WHERE colocation_id=? AND period=?", (cid, period))}
    full = days_in_period(period)
    return {u: float(rows.get(u, full)) for u in uids}


def weights(db, coloc, period, uids) -> tuple[dict, str]:
    """Poids de répartition ; retourne (poids, mode réellement appliqué)."""
    cid, mode = coloc["id"], coloc["split_mode"]
    if mode == "consumption":
        rows = {r["user_id"]: r["kwh"] for r in db.execute(
            "SELECT user_id, kwh FROM consumptions WHERE colocation_id=? AND period=?", (cid, period))}
        w = {u: float(rows.get(u, 0)) for u in uids}
        if sum(w.values()) > 0:
            return w, "consumption"
        mode = "presence"          # pas de sous-compteurs saisis : repli sur la présence
    if mode == "presence":
        w = _presence_weights(db, cid, period, uids)
        if sum(w.values()) > 0:
            return w, "presence"
    return {u: 1.0 for u in uids}, "equal"


def allocate(db, coloc, period, members) -> tuple[dict, float, str]:
    """kWh attribués à chaque membre pour la période, total, mode appliqué."""
    uids = [m["user_id"] for m in members]
    total = period_total_kwh(db, coloc["id"], period)
    if not uids:
        return {}, total, coloc["split_mode"]
    w, applied = weights(db, coloc, period, uids)
    s = sum(w.values())
    return {u: round(total * w[u] / s, 3) for u in uids}, total, applied


def balances(db, coloc, members) -> list[dict]:
    cid = coloc["id"]
    consumed = defaultdict(float)
    for p in reading_periods(db, cid):
        shares, _, _ = allocate(db, coloc, p, members)
        for u, k in shares.items():
            consumed[u] += k

    def grouped(sql):
        return {r[0]: r[1] for r in db.execute(sql, (cid,))}

    brought = grouped("SELECT payer_id, SUM(kwh) FROM payments WHERE colocation_id=? AND status='confirmed' "
                      "AND kind IN ('recharge','contribution') GROUP BY payer_id")
    paid = grouped("SELECT payer_id, SUM(kwh) FROM payments WHERE colocation_id=? AND status='confirmed' "
                   "AND kind='reimbursement' GROUP BY payer_id")
    received = grouped("SELECT beneficiary_id, SUM(kwh) FROM payments WHERE colocation_id=? AND status='confirmed' "
                       "AND kind='reimbursement' GROUP BY beneficiary_id")
    out = []
    for m in members:
        u = m["user_id"]
        b = round(brought.get(u, 0) + paid.get(u, 0) - received.get(u, 0) - consumed[u], 3)
        out.append({
            "user_id": u, "name": m["full_name"], "initials": initials(m["full_name"]),
            "role": m["role"],
            "brought_kwh": round(brought.get(u, 0), 2),
            "consumed_kwh": round(consumed[u], 2),
            "balance_kwh": round(b, 2),
            "status": "creditor" if b > EPS else "debtor" if b < -EPS else "balanced",
        })
    return out


def settlements(items: list[dict], tariff: float) -> list[dict]:
    """Remboursements minimaux suggérés (débiteurs → créditeurs)."""
    debtors = sorted([[i["user_id"], i["name"], -i["balance_kwh"]] for i in items if i["status"] == "debtor"],
                     key=lambda x: -x[2])
    creditors = sorted([[i["user_id"], i["name"], i["balance_kwh"]] for i in items if i["status"] == "creditor"],
                       key=lambda x: -x[2])
    res, i, j = [], 0, 0
    while i < len(debtors) and j < len(creditors):
        k = min(debtors[i][2], creditors[j][2])
        if k > EPS:
            res.append({"from_user_id": debtors[i][0], "from_name": debtors[i][1],
                        "to_user_id": creditors[j][0], "to_name": creditors[j][1],
                        "kwh": round(k, 2), "amount_fcfa": round(k * tariff)})
        debtors[i][2] -= k
        creditors[j][2] -= k
        if debtors[i][2] <= EPS:
            i += 1
        if creditors[j][2] <= EPS:
            j += 1
    return res


def credit_kwh(db, cid) -> float:
    rech = db.execute("SELECT COALESCE(SUM(kwh),0) FROM payments WHERE colocation_id=? AND status='confirmed' "
                      "AND kind IN ('recharge','contribution')", (cid,)).fetchone()[0]
    used = db.execute("SELECT COALESCE(SUM(delta),0) FROM readings WHERE colocation_id=?", (cid,)).fetchone()[0]
    return round(rech - used, 2)


def avg_kwh_per_day(db, cid) -> float:
    """Consommation moyenne sur les 30 derniers jours couverts par les relevés."""
    rows = db.execute("SELECT read_at, delta FROM readings WHERE colocation_id=? ORDER BY read_at, id", (cid,)).fetchall()
    if len(rows) < 2:
        return 0.0
    first, last = parse(rows[0]["read_at"]), parse(rows[-1]["read_at"])
    start = max(first, last - timedelta(days=30))
    days = max((last - start).total_seconds() / 86400, 1.0)
    cons = sum(r["delta"] for r in rows[1:] if parse(r["read_at"]) > start)
    return cons / days


def autonomy(db, cid) -> tuple[float, float | None]:
    credit = credit_kwh(db, cid)
    avg = avg_kwh_per_day(db, cid)
    days = None if avg <= 0 else round(max(credit, 0) / avg, 1)
    return credit, days
