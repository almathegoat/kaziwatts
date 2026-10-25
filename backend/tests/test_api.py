import io

import pytest
from conftest import signup


# ------------------------------------------------------------------ authentification
def test_register_login_me(client):
    h, u = signup(client, "Ada Lovelace", "6 77 00 11 22")
    assert u["identifier"] == "+237677001122" and u["initials"] == "AL"
    r = client.post("/api/auth/login", json={"identifier": "677001122", "password": "Motdepasse1"})
    assert r.status_code == 200 and r.json()["user"]["id"] == u["id"]
    assert client.get("/api/auth/me", headers=h).json()["full_name"] == "Ada Lovelace"
    assert client.get("/api/auth/me").status_code == 401


def test_register_validation(client):
    base = {"full_name": "Ada", "identifier": "ada@x.cm", "password": "Motdepasse1", "accept_terms": True}
    r = client.post("/api/auth/register", json={**base, "confirm_password": "autre"})
    assert r.status_code == 422 and "identiques" in r.json()["detail"]
    r = client.post("/api/auth/register", json={**base, "accept_terms": False})
    assert r.status_code == 422 and "conditions" in r.json()["detail"]
    r = client.post("/api/auth/register", json={**base, "password": "court"})
    assert r.status_code == 422
    r = client.post("/api/auth/register", json={**base, "identifier": "pas-un-contact"})
    assert r.status_code == 422
    assert client.post("/api/auth/register", json=base).status_code == 201
    assert client.post("/api/auth/register", json=base).status_code == 409


def test_login_errors_and_throttle(client):
    signup(client, "Ada", "ada@x.cm")
    for _ in range(5):
        assert client.post("/api/auth/login", json={"identifier": "ada@x.cm", "password": "faux"}).status_code == 401
    assert client.post("/api/auth/login", json={"identifier": "ada@x.cm", "password": "Motdepasse1"}).status_code == 429


def test_password_reset_and_change(client):
    h, _ = signup(client, "Ada", "ada@x.cm")
    code = client.post("/api/auth/forgot-password", json={"identifier": "ada@x.cm"}).json()["dev_code"]
    assert "dev_code" not in client.post("/api/auth/forgot-password", json={"identifier": "inconnu@x.cm"}).json()
    bad = client.post("/api/auth/reset-password", json={"identifier": "ada@x.cm", "code": "000000", "new_password": "Nouveau123"})
    assert bad.status_code == 400
    ok = client.post("/api/auth/reset-password", json={"identifier": "ada@x.cm", "code": code, "new_password": "Nouveau123"})
    assert ok.status_code == 200
    assert client.post("/api/auth/reset-password", json={"identifier": "ada@x.cm", "code": code, "new_password": "Autre12345"}).status_code == 400
    assert client.post("/api/auth/login", json={"identifier": "ada@x.cm", "password": "Nouveau123"}).status_code == 200
    assert client.post("/api/auth/change-password", headers=h, json={"current_password": "x", "new_password": "Encore12345"}).status_code == 403
    assert client.post("/api/auth/change-password", headers=h, json={"current_password": "Nouveau123", "new_password": "Encore12345"}).status_code == 200
    r = client.patch("/api/auth/me", headers=h, json={"theme": "dark", "full_name": "Ada L"})
    assert r.json()["theme"] == "dark" and r.json()["full_name"] == "Ada L"


# ------------------------------------------------------------------ colocation
def test_colocation_flow(client):
    ha, a = signup(client, "Amina Ngambi", "amina@x.cm")
    hb, b = signup(client, "Brice Komdem", "brice@x.cm")
    assert client.get("/api/colocation", headers=ha).status_code == 409
    r = client.post("/api/colocation", headers=ha, json={"name": "Appart 3B", "city": "Bonapriso", "occupants": 2})
    assert r.status_code == 201 and r.json()["role"] == "admin"
    code = r.json()["invite_code"]
    assert client.post("/api/colocation", headers=ha, json={"name": "Xavier", "city": "Yaoundé", "occupants": 2}).status_code == 409
    assert client.post("/api/colocation/join", headers=hb, json={"code": "ZZZZZZ"}).status_code == 404
    j = client.post("/api/colocation/join", headers=hb, json={"code": code.lower()})
    assert j.status_code == 200 and j.json()["role"] == "member"
    hc, _ = signup(client, "Carine Mbarga", "carine@x.cm")
    assert client.post("/api/colocation/join", headers=hc, json={"code": code}).status_code == 409   # complète
    assert client.patch("/api/colocation", headers=hb, json={"name": "Hack"}).status_code == 403
    assert client.patch("/api/colocation", headers=ha, json={"occupants": 3}).status_code == 200
    assert client.post("/api/colocation/join", headers=hc, json={"code": code}).status_code == 200
    ms = client.get("/api/colocation/members", headers=ha).json()
    assert ms["count"] == 3 and ms["items"][0]["is_admin"] and ms["items"][1]["can_remove"]
    # rôles et retrait
    assert client.patch(f"/api/colocation/members/{a['id']}", headers=ha, json={"role": "member"}).status_code == 409
    assert client.patch(f"/api/colocation/members/{b['id']}", headers=hb, json={"role": "admin"}).status_code == 403
    assert client.delete(f"/api/colocation/members/{a['id']}", headers=ha).status_code == 409
    assert client.delete(f"/api/colocation/members/{b['id']}", headers=ha).status_code == 200
    assert client.get("/api/colocation", headers=hb).status_code == 409
    # nouveau code
    new = client.post("/api/colocation/invite/regenerate", headers=ha).json()["code"]
    assert new != code and client.get("/api/colocation/invite", headers=hc).json()["code"] == new
    # quitter
    assert client.post("/api/colocation/leave", headers=ha).status_code == 409        # seul admin
    assert client.patch(f"/api/colocation/members/{_id(client, hc)}", headers=ha, json={"role": "admin"}).status_code == 200
    assert client.post("/api/colocation/leave", headers=ha).status_code == 200


def _id(client, h):
    return client.get("/api/auth/me", headers=h).json()["id"]


# ------------------------------------------------------------------ scénario complet (données = maquettes)
@pytest.fixture()
def demo(client):
    from contextlib import closing
    from app import db as dbm
    import seed
    with closing(dbm.connect()) as c:
        ids = seed.seed_demo(c)
    tok = {}
    for k in ("amina", "carine", "brice"):
        r = client.post("/api/auth/login", json={"identifier": f"{k}@kazi.cm", "password": seed.PASSWORD})
        assert r.status_code == 200, r.text
        tok[k] = {"Authorization": "Bearer " + r.json()["token"]}
    return client, tok, ids


def test_demo_numbers_match_mockups(demo):
    client, h, ids = demo
    home = client.get("/api/home", headers=h["amina"]).json()
    assert home["credit_kwh"] == 42 and home["meter"]["last_index"] == 12702
    assert home["autonomy_days"] is not None and home["autonomy_days"] > 0
    bal = client.get("/api/balance", headers=h["amina"]).json()
    by = {i["name"].split()[0]: i for i in bal["items"]}
    assert (by["Amina"]["balance_kwh"], by["Carine"]["balance_kwh"], by["Brice"]["balance_kwh"]) == (56, 6, -20)
    assert by["Brice"]["status"] == "debtor" and by["Brice"]["balance_fcfa"] == -2000
    assert [i["name"].split()[0] for i in client.get("/api/balance?only_debtors=true", headers=h["amina"]).json()["items"]] == ["Brice"]
    s = bal["settlements"][0]
    assert s["from_name"].startswith("Brice") and s["to_name"].startswith("Amina") and s["amount_fcfa"] == 2000
    sp = client.get("/api/split", headers=h["carine"]).json()
    assert sp["period"] == "2026-09" and sp["total_kwh"] == 210 and sp["amount_fcfa"] == 21000
    pct = {x["name"].split()[0]: x["percent"] for x in sp["shares"]}
    assert pct == {"Amina": 44.8, "Carine": 35.2, "Brice": 20.0}
    assert client.get("/api/colocation", headers=h["brice"]).json()["invite_code"] == "KZ7T28"


def test_reimbursement_settles_debt(demo):
    client, h, ids = demo
    pend = client.get("/api/payments?status=pending", headers=h["amina"]).json()["items"]
    assert len(pend) == 1 and pend[0]["can_decide"] is True
    pid = pend[0]["id"]
    assert client.get("/api/payments?status=pending", headers=h["brice"]).json()["items"][0]["can_decide"] is False
    assert client.post(f"/api/payments/{pid}/decision", headers=h["brice"], json={"action": "confirm"}).status_code == 403
    assert client.post(f"/api/payments/{pid}/decision", headers=h["carine"], json={"action": "confirm"}).status_code == 403
    r = client.post(f"/api/payments/{pid}/decision", headers=h["amina"], json={"action": "confirm"})
    assert r.status_code == 200 and r.json()["status"] == "confirmed"
    assert client.post(f"/api/payments/{pid}/decision", headers=h["amina"], json={"action": "reject"}).status_code == 409
    by = {i["name"].split()[0]: i["balance_kwh"] for i in client.get("/api/balance", headers=h["amina"]).json()["items"]}
    assert by == {"Amina": 36, "Carine": 6, "Brice": 0}
    assert client.get("/api/home", headers=h["amina"]).json()["credit_kwh"] == 42   # un remboursement ne change pas le crédit


def test_payments_create_filters_receipt_proof(demo):
    client, h, ids = demo
    assert len(client.get("/api/payments", headers=h["amina"]).json()["items"]) == 4
    assert client.get("/api/payments?method_group=cash", headers=h["amina"]).json()["count"] == 1
    assert client.get("/api/payments?method_group=mobile_money", headers=h["amina"]).json()["count"] == 3
    assert client.get("/api/payments?period=2026-09", headers=h["amina"]).json()["count"] == 3
    assert client.get("/api/payments?period=zzz", headers=h["amina"]).status_code == 422
    # une recharge de Carine : en attente jusqu'à confirmation de l'admin
    r = client.post("/api/payments", headers=h["carine"], json={"kind": "recharge", "amount_fcfa": 5000, "method": "cash"})
    assert r.status_code == 201
    p = r.json()
    assert p["status"] == "pending" and p["kwh"] == 50 and p["reference"].startswith("TX")
    assert client.get("/api/home", headers=h["amina"]).json()["credit_kwh"] == 42          # pas encore compté
    assert client.post(f"/api/payments/{p['id']}/decision", headers=h["amina"], json={"action": "confirm"}).status_code == 200
    assert client.get("/api/home", headers=h["amina"]).json()["credit_kwh"] == 92
    # référence en double, règles de saisie
    dup = client.post("/api/payments", headers=h["carine"], json={"kind": "recharge", "amount_fcfa": 1000, "method": "cash", "reference": p["reference"]})
    assert dup.status_code == 409
    assert client.post("/api/payments", headers=h["carine"], json={"kind": "reimbursement", "amount_fcfa": 1000, "method": "cash"}).status_code == 422
    assert client.post("/api/payments", headers=h["carine"], json={"kind": "recharge", "amount_fcfa": 0, "method": "cash"}).status_code == 422
    assert client.post("/api/payments", headers=h["carine"], json={"kind": "recharge", "amount_fcfa": 1000, "method": "cash", "payer_id": ids["brice"]}).status_code == 403
    # l'admin enregistre pour Brice -> confirmé d'emblée ; sa propre recharge aussi
    r = client.post("/api/payments", headers=h["amina"], json={"kind": "recharge", "amount_fcfa": 2000, "method": "mtn_momo", "payer_id": ids["brice"]})
    assert r.json()["status"] == "confirmed"
    assert client.post("/api/payments", headers=h["amina"], json={"kind": "recharge", "amount_fcfa": 1000, "method": "cash"}).json()["status"] == "confirmed"
    # annulation d'un paiement en attente
    q = client.post("/api/payments", headers=h["brice"], json={"kind": "recharge", "amount_fcfa": 1000, "method": "cash"}).json()
    assert client.delete(f"/api/payments/{q['id']}", headers=h["carine"]).status_code == 403
    assert client.delete(f"/api/payments/{q['id']}", headers=h["brice"]).status_code == 200
    # justificatif
    rc = client.get(f"/api/payments/{p['id']}/receipt", headers=h["carine"]).json()
    assert "5 000 FCFA" in rc["text"] and "Espèces" in rc["text"] and p["reference"] in rc["share_text"]
    t = client.get(f"/api/payments/{p['id']}/receipt.txt", headers=h["carine"])
    assert t.status_code == 200 and "attachment" in t.headers["content-disposition"]
    # preuve
    pid = p["id"]
    bad = client.post(f"/api/payments/{pid}/proof", headers=h["carine"], files={"file": ("x.exe", b"abc", "application/octet-stream")})
    assert bad.status_code == 422
    big = client.post(f"/api/payments/{pid}/proof", headers=h["carine"], files={"file": ("x.png", b"0" * (5 * 1024 * 1024 + 5), "image/png")})
    assert big.status_code == 413
    assert client.post(f"/api/payments/{pid}/proof", headers=h["brice"], files={"file": ("x.png", b"abc", "image/png")}).status_code == 403
    ok = client.post(f"/api/payments/{pid}/proof", headers=h["carine"], files={"file": ("capture.png", b"\x89PNG-data", "image/png")})
    assert ok.status_code == 200 and ok.json()["has_proof"] and ok.json()["proof_name"] == "capture.png"
    assert client.get(f"/api/payments/{pid}/proof", headers=h["brice"]).content == b"\x89PNG-data"


def test_meter_and_readings(client):
    ha, _ = signup(client, "Amina Ngambi", "amina@x.cm")
    hb, _ = signup(client, "Brice Komdem", "brice@x.cm")
    code = client.post("/api/colocation", headers=ha, json={"name": "Coloc A", "city": "Douala", "occupants": 2}).json()["invite_code"]
    client.post("/api/colocation/join", headers=hb, json={"code": code})
    assert client.get("/api/meter", headers=ha).status_code == 404
    assert client.post("/api/readings", headers=ha, json={"index": 10}).status_code == 409           # compteur absent
    body = {"number": "0142", "type": "prepaid", "initial_index": 1000, "tariff_fcfa": 150}
    assert client.put("/api/meter", headers=hb, json=body).status_code == 403
    assert client.put("/api/meter", headers=ha, json={**body, "tariff_fcfa": 0}).status_code == 422
    assert client.put("/api/meter", headers=ha, json=body).json()["last_index"] == 1000
    assert client.get("/api/meter/quote?amount_fcfa=15000", headers=hb).json()["kwh"] == 100
    r = client.post("/api/readings", headers=hb, json={"index": 1000})
    assert r.status_code == 422 and "supérieur" in r.json()["detail"]
    pv = client.get("/api/readings/preview?index=1100", headers=hb).json()
    assert pv["delta_kwh"] == 100 and pv["estimated_fcfa"] == 15000
    r = client.post("/api/readings", headers=hb, json={"index": 1100})
    assert r.status_code == 201 and r.json()["delta_kwh"] == 100 and r.json()["author"] == "Brice Komdem"
    assert client.post("/api/readings", headers=ha, json={"index": 1200, "read_at": "2020-01-01"}).status_code == 422
    assert client.post("/api/readings", headers=ha, json={"index": 1200, "read_at": "2999-01-01"}).status_code == 422
    items = client.get("/api/readings", headers=ha).json()["items"]
    assert [i["index"] for i in items] == [1100, 1000] and items[1]["is_initial"] and not items[0]["is_initial"]
    assert client.put("/api/meter", headers=ha, json={**body, "initial_index": 5}).status_code == 409   # relevés existants
    assert client.put("/api/meter", headers=ha, json={**body, "tariff_fcfa": 100, "type": "postpaid"}).status_code == 200
    h = client.get("/api/home", headers=ha).json()
    assert "credit_kwh" not in h and h["meter"]["type"] == "postpaid"
    rem = client.get("/api/reminders", headers=ha).json()["items"]
    assert {r["kind"] for r in rem} == {"reading_monthly", "low_credit"}


def test_split_modes_and_inputs(demo):
    client, h, ids = demo
    # mode présence : poids 30/30/18
    assert client.put("/api/split/settings", headers=h["carine"], json={"mode": "presence"}).status_code == 403
    assert client.put("/api/split/settings", headers=h["amina"], json={"mode": "presence"}).status_code == 200
    sp = client.get("/api/split?period=2026-09", headers=h["amina"]).json()
    kw = {x["name"].split()[0]: x["kwh"] for x in sp["shares"]}
    assert kw == {"Amina": 80.77, "Carine": 80.77, "Brice": 48.46}
    # mode égal
    client.put("/api/split/settings", headers=h["amina"], json={"mode": "equal"})
    assert {x["kwh"] for x in client.get("/api/split?period=2026-09", headers=h["amina"]).json()["shares"]} == {70}
    # consommation : un membre ne saisit que ses propres kWh
    client.put("/api/split/settings", headers=h["amina"], json={"mode": "consumption"})
    assert client.put("/api/split/consumptions", headers=h["brice"], json={"period": "2026-09", "entries": [{"user_id": ids["amina"], "kwh": 1}]}).status_code == 403
    assert client.put("/api/split/consumptions", headers=h["brice"], json={"period": "2026-09", "entries": [{"user_id": ids["brice"], "kwh": 62}]}).status_code == 200
    assert client.put("/api/split/consumptions", headers=h["amina"], json={"period": "2026-09", "entries": [{"user_id": 9999, "kwh": 1}]}).status_code == 422
    assert client.put("/api/split/presence", headers=h["amina"], json={"period": "2026-09", "entries": [{"user_id": ids["brice"], "days": 31}]}).status_code == 422
    sp = client.get("/api/split?period=2026-09", headers=h["amina"]).json()
    assert round(sum(x["kwh"] for x in sp["shares"]), 1) == 210           # toute la consommation est répartie
    # période sans consommation / invalide
    assert client.get("/api/split?period=2026-01", headers=h["amina"]).json()["total_kwh"] == 0
    assert client.get("/api/split?period=2026-13", headers=h["amina"]).status_code == 422


def test_reminders_and_alerts(demo):
    client, h, ids = demo
    al = client.get("/api/alerts", headers=h["amina"]).json()
    assert [a["user_id"] for a in al["late"]] == [ids["brice"]] and "2 000 FCFA" in al["late"][0]["title"]
    assert any(a["payment_id"] for a in al["soon"] if "payment_id" in a)                # 2 000 FCFA à confirmer
    assert al["bill"] and "septembre 2026" in al["bill"][0]["title"]
    assert not any("payment_id" in a for a in client.get("/api/alerts", headers=h["brice"]).json()["soon"])
    # CRUD rappels
    r = client.post("/api/reminders", headers=h["brice"], json={"title": "Payer la facture", "due_date": "2026-11-01"})
    assert r.status_code == 201 and r.json()["automatic"] is False
    rid = r.json()["id"]
    assert client.post("/api/reminders", headers=h["brice"], json={"title": "x y", "due_date": "demain"}).status_code == 422
    assert client.patch(f"/api/reminders/{rid}", headers=h["amina"], json={"active": False}).json()["active"] is False
    auto = next(x for x in client.get("/api/reminders", headers=h["amina"]).json()["items"] if x["kind"] == "low_credit")
    assert client.patch(f"/api/reminders/{auto['id']}", headers=h["amina"], json={"title": "autre"}).status_code == 422
    assert client.patch(f"/api/reminders/{auto['id']}", headers=h["amina"], json={"active": False}).status_code == 200
    assert client.delete(f"/api/reminders/{auto['id']}", headers=h["amina"]).status_code == 422
    assert client.delete(f"/api/reminders/{rid}", headers=h["amina"]).status_code == 200
    assert client.delete(f"/api/reminders/{rid}", headers=h["amina"]).status_code == 404


def test_isolation_between_colocations(demo):
    client, h, ids = demo
    hx, _ = signup(client, "Intrus Externe", "intrus@x.cm")
    code = client.post("/api/colocation", headers=hx, json={"name": "Autre", "city": "Akwa", "occupants": 2}).json()["invite_code"]
    pid = client.get("/api/payments", headers=h["amina"]).json()["items"][0]["id"]
    assert client.get(f"/api/payments/{pid}", headers=hx).status_code == 404
    assert client.get(f"/api/payments/{pid}/receipt", headers=hx).status_code == 404
    assert client.post(f"/api/payments/{pid}/decision", headers=hx, json={"action": "confirm"}).status_code == 404
    assert client.get("/api/payments", headers=hx).json()["count"] == 0
    assert client.get("/api/readings", headers=hx).status_code == 409                       # pas de compteur chez lui
    assert client.get("/api/health").json()["status"] == "ok"
