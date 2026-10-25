"""Point d'entrée FastAPI : uvicorn app.main:app"""
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse, RedirectResponse

from . import config
from .db import init_db
from .routers import auth, colocation, finance, meter, reminders


@asynccontextmanager
async def lifespan(app: FastAPI):
    init_db()
    config.UPLOAD_DIR.mkdir(parents=True, exist_ok=True)
    yield


app = FastAPI(title="KaziWatts API", version="1.0.0", lifespan=lifespan,
              description="Backend de KaziWatts : répartition de la consommation et des paiements d'électricité "
                          "entre colocataires. Authentification : en-tête `Authorization: Bearer <token>`.")
app.add_middleware(CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"])

for r in (auth.router, colocation.router, meter.router, finance.router, reminders.router):
    app.include_router(r, prefix="/api")


@app.exception_handler(RequestValidationError)
async def validation_handler(request: Request, exc: RequestValidationError):
    errors = exc.errors()
    first = errors[0] if errors else {}
    field = ".".join(str(x) for x in first.get("loc", ()) if x not in ("body", "query", "path"))
    t, msg = first.get("type", ""), first.get("msg", "")
    if t == "value_error":
        detail = msg.removeprefix("Value error, ")
    elif t == "missing":
        detail = f"Champ obligatoire manquant : {field}."
    elif t.startswith("string_too_short") or t.startswith("greater_than") or t.startswith("less_than"):
        detail = f"Valeur hors limites pour « {field} »."
    else:
        detail = f"Valeur invalide pour « {field} »."
    return JSONResponse(status_code=422, content={
        "detail": detail,
        "errors": [{"field": ".".join(str(x) for x in e.get("loc", ()) if x not in ("body", "query", "path")),
                    "type": e.get("type")} for e in errors]})


@app.get("/api/health", tags=["Divers"])
def health():
    return {"status": "ok", "service": "KaziWatts API", "version": app.version}


@app.get("/", include_in_schema=False)
def root():
    return RedirectResponse("/docs")
