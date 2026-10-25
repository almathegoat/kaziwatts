"""Accès SQLite : schéma et connexion par requête."""
import sqlite3
from contextlib import closing

from . import config

SCHEMA = """
CREATE TABLE IF NOT EXISTS users(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  full_name TEXT NOT NULL,
  identifier TEXT NOT NULL UNIQUE,
  password_hash TEXT NOT NULL,
  theme TEXT NOT NULL DEFAULT 'system' CHECK(theme IN ('light','dark','system')),
  created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS reset_codes(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  code_hash TEXT NOT NULL,
  expires_at TEXT NOT NULL,
  attempts INTEGER NOT NULL DEFAULT 0,
  used INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS colocations(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  city TEXT NOT NULL,
  occupants INTEGER NOT NULL CHECK(occupants >= 2),
  invite_code TEXT NOT NULL UNIQUE,
  invite_expires_at TEXT NOT NULL,
  split_mode TEXT NOT NULL DEFAULT 'consumption' CHECK(split_mode IN ('consumption','presence','equal')),
  created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS members(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  colocation_id INTEGER NOT NULL REFERENCES colocations(id) ON DELETE CASCADE,
  user_id INTEGER NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
  role TEXT NOT NULL CHECK(role IN ('admin','member')),
  joined_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS meters(
  colocation_id INTEGER PRIMARY KEY REFERENCES colocations(id) ON DELETE CASCADE,
  number TEXT NOT NULL,
  type TEXT NOT NULL CHECK(type IN ('prepaid','postpaid')),
  initial_index REAL NOT NULL,
  tariff_fcfa REAL NOT NULL CHECK(tariff_fcfa > 0),
  updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS readings(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  colocation_id INTEGER NOT NULL REFERENCES colocations(id) ON DELETE CASCADE,
  author_id INTEGER REFERENCES users(id),
  idx REAL NOT NULL,
  delta REAL NOT NULL,
  read_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_readings ON readings(colocation_id, read_at);
CREATE TABLE IF NOT EXISTS consumptions(
  colocation_id INTEGER NOT NULL REFERENCES colocations(id) ON DELETE CASCADE,
  user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  period TEXT NOT NULL,
  kwh REAL NOT NULL CHECK(kwh >= 0),
  PRIMARY KEY(colocation_id, user_id, period)
);
CREATE TABLE IF NOT EXISTS presence(
  colocation_id INTEGER NOT NULL REFERENCES colocations(id) ON DELETE CASCADE,
  user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  period TEXT NOT NULL,
  days INTEGER NOT NULL CHECK(days >= 0 AND days <= 31),
  PRIMARY KEY(colocation_id, user_id, period)
);
CREATE TABLE IF NOT EXISTS payments(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  colocation_id INTEGER NOT NULL REFERENCES colocations(id) ON DELETE CASCADE,
  payer_id INTEGER NOT NULL REFERENCES users(id),
  beneficiary_id INTEGER REFERENCES users(id),
  kind TEXT NOT NULL CHECK(kind IN ('recharge','contribution','reimbursement')),
  amount_fcfa INTEGER NOT NULL CHECK(amount_fcfa > 0),
  kwh REAL NOT NULL,
  method TEXT NOT NULL CHECK(method IN ('mtn_momo','orange_money','cash')),
  reference TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'pending' CHECK(status IN ('pending','confirmed','rejected')),
  proof_file TEXT,
  proof_name TEXT,
  created_by INTEGER REFERENCES users(id),
  created_at TEXT NOT NULL,
  decided_at TEXT,
  decided_by INTEGER REFERENCES users(id)
);
CREATE INDEX IF NOT EXISTS ix_payments ON payments(colocation_id, created_at);
CREATE TABLE IF NOT EXISTS reminders(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  colocation_id INTEGER NOT NULL REFERENCES colocations(id) ON DELETE CASCADE,
  kind TEXT NOT NULL DEFAULT 'custom' CHECK(kind IN ('custom','reading_monthly','low_credit')),
  title TEXT NOT NULL,
  subtitle TEXT NOT NULL DEFAULT '',
  due_date TEXT,
  active INTEGER NOT NULL DEFAULT 1,
  created_by INTEGER REFERENCES users(id),
  created_at TEXT NOT NULL
);
"""


def connect() -> sqlite3.Connection:
    conn = sqlite3.connect(config.DB_PATH, check_same_thread=False, timeout=15)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys=ON")
    return conn


def init_db() -> None:
    config.DB_PATH.parent.mkdir(parents=True, exist_ok=True)
    with closing(connect()) as conn:
        conn.execute("PRAGMA journal_mode=WAL")
        conn.executescript(SCHEMA)
        conn.commit()


def get_db():
    """Dépendance FastAPI : une connexion par requête.
    Les routes qui écrivent appellent db.commit() elles-mêmes (sinon rollback à la fermeture)."""
    conn = connect()
    try:
        yield conn
    finally:
        conn.close()
