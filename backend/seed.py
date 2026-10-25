"""Données de démonstration = MockData.cs du frontend (septembre 2026, prépayé, tarif 100 FCFA/kWh).

Usage :  python seed.py            (crée la base si besoin, refuse d'écraser des données existantes)
         python seed.py --reset    (efface tout puis recharge la démo)

Comptes (mot de passe : Kazi2026!) : amina@kazi.cm (admin), carine@kazi.cm, brice@kazi.cm
Code d'invitation : KZ7T28
"""
import sys
from contextlib import closing

from app import config, db as dbm, logic
from app.security import hash_password

PASSWORD = "Kazi2026!"


def seed_demo(conn) -> dict:
    if conn.execute("SELECT COUNT(*) FROM users").fetchone()[0]:
        raise RuntimeError("La base contient déjà des données (utilisez --reset).")
    pw = hash_password(PASSWORD)
    ts = "2026-08-30T10:00:00+01:00"
    ids = {}
    for key, name, email in [("amina", "Amina Ngambi", "amina@kazi.cm"),
                             ("carine", "Carine Mbarga", "carine@kazi.cm"),
                             ("brice", "Brice Komdem", "brice@kazi.cm")]:
        ids[key] = conn.execute("INSERT INTO users(full_name,identifier,password_hash,created_at) VALUES(?,?,?,?)",
                                (name, email, pw, ts)).lastrowid
    cid = conn.execute(
        "INSERT INTO colocations(name,city,occupants,invite_code,invite_expires_at,split_mode,created_at) "
        "VALUES(?,?,?,?,?,?,?)",
        ("Appartement 3B", "Bonapriso", 3, "KZ7T28", logic.invite_expiry(), "consumption", ts)).lastrowid
    for key, role in [("amina", "admin"), ("carine", "member"), ("brice", "member")]:
        conn.execute("INSERT INTO members(colocation_id,user_id,role,joined_at) VALUES(?,?,?,?)",
                     (cid, ids[key], role, ts))
    conn.execute("INSERT INTO meters VALUES(?,?,?,?,?,?)", (cid, "0142 7788 3391", "prepaid", 12492, 100, ts))
    for who, idx, delta, at in [("amina", 12492, 0, "2026-09-01T09:00:00+01:00"),
                                ("brice", 12600, 108, "2026-09-15T18:30:00+01:00"),
                                ("carine", 12702, 102, "2026-09-28T19:10:00+01:00")]:
        conn.execute("INSERT INTO readings(colocation_id,author_id,idx,delta,read_at) VALUES(?,?,?,?,?)",
                     (cid, ids[who], idx, delta, at))
    for who, kwh, days in [("amina", 94, 30), ("carine", 74, 30), ("brice", 42, 18)]:
        conn.execute("INSERT INTO consumptions VALUES(?,?,?,?)", (cid, ids[who], "2026-09", kwh))
        conn.execute("INSERT INTO presence VALUES(?,?,?,?)", (cid, ids[who], "2026-09", days))
    pays = [  # payeur, bénéficiaire, type, FCFA, mode, réf, statut, date
        ("amina", None, "recharge", 15000, "mtn_momo", "TX100000001", "confirmed", "2026-09-12T14:32:00+01:00"),
        ("carine", None, "recharge", 8000, "orange_money", "TX100000002", "confirmed", "2026-09-18T11:05:00+01:00"),
        ("brice", None, "recharge", 2200, "cash", "TX100000003", "confirmed", "2026-09-25T17:40:00+01:00"),
        ("brice", "amina", "reimbursement", 2000, "mtn_momo", "TX100000004", "pending", "2026-10-02T09:15:00+01:00"),
    ]
    for payer, ben, kind, amt, meth, ref, st, at in pays:
        conn.execute(
            "INSERT INTO payments(colocation_id,payer_id,beneficiary_id,kind,amount_fcfa,kwh,method,reference,status,"
            "created_by,created_at,decided_at,decided_by) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)",
            (cid, ids[payer], ids[ben] if ben else None, kind, amt, round(amt / 100, 2), meth, ref, st, ids[payer], at,
             at if st == "confirmed" else None, ids["amina"] if st == "confirmed" else None))
    conn.executemany(
        "INSERT INTO reminders(colocation_id,kind,title,subtitle,due_date,created_by,created_at) VALUES(?,?,?,?,?,?,?)",
        [(cid, "reading_monthly", "Relevé du compteur", "Chaque 1er du mois à 8 h · Tous", None, ids["amina"], ts),
         (cid, "custom", "Rembourser 2 000 FCFA à Amina", "5 oct. · Brice", "2026-10-05", ids["brice"], ts),
         (cid, "low_credit", "Crédit bas (automatique)", "Autonomie < 3 jours · Tous", None, ids["amina"], ts)])
    conn.commit()
    return {"colocation_id": cid, **ids}


if __name__ == "__main__":
    if "--reset" in sys.argv:
        for suffix in ("", "-wal", "-shm"):
            p = config.DB_PATH.parent / (config.DB_PATH.name + suffix)
            p.unlink(missing_ok=True)
    dbm.init_db()
    with closing(dbm.connect()) as c:
        try:
            seed_demo(c)
        except RuntimeError as e:
            sys.exit(str(e))
    print("Démo chargée : amina@kazi.cm / carine@kazi.cm / brice@kazi.cm  — mot de passe", PASSWORD,
          "— code d'invitation KZ7T28")
