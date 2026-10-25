import os
import sys
import tempfile
from pathlib import Path

_tmp = tempfile.mkdtemp(prefix="kazi_test_")
os.environ["KAZI_DB"] = str(Path(_tmp) / "test.db")
os.environ["KAZI_UPLOADS"] = str(Path(_tmp) / "uploads")
os.environ["KAZI_SECRET"] = "test-secret"
os.environ["KAZI_DEV"] = "1"
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import pytest
from fastapi.testclient import TestClient


@pytest.fixture()
def client():
    from app import config, db
    from app.main import app
    from app.routers import auth
    for s in ("", "-wal", "-shm"):
        Path(str(config.DB_PATH) + s).unlink(missing_ok=True)
    auth._FAILS.clear()
    with TestClient(app) as c:     # déclenche init_db via lifespan
        yield c


def signup(client, name, ident, pwd="Motdepasse1"):
    r = client.post("/api/auth/register", json={"full_name": name, "identifier": ident, "password": pwd,
                                                "confirm_password": pwd, "accept_terms": True})
    assert r.status_code == 201, r.text
    return {"Authorization": "Bearer " + r.json()["token"]}, r.json()["user"]
