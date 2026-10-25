"""Modèles d'entrée (validation). Les messages d'erreur sont en français."""
from typing import Literal

from pydantic import BaseModel, Field, field_validator

from .security import normalize_identifier


class RegisterIn(BaseModel):
    full_name: str = Field(min_length=2, max_length=80)
    identifier: str
    password: str = Field(min_length=8, max_length=128)
    confirm_password: str | None = None
    accept_terms: bool = False

    @field_validator("full_name")
    @classmethod
    def _name(cls, v):
        v = " ".join(v.split())
        if len(v) < 2:
            raise ValueError("Le nom complet est obligatoire.")
        return v

    @field_validator("identifier")
    @classmethod
    def _ident(cls, v):
        return normalize_identifier(v)

    @field_validator("password")
    @classmethod
    def _pwd(cls, v):
        if len(v) < 8:
            raise ValueError("Le mot de passe doit contenir au moins 8 caractères.")
        return v


class LoginIn(BaseModel):
    identifier: str
    password: str


class ProfileIn(BaseModel):
    full_name: str | None = Field(default=None, min_length=2, max_length=80)
    theme: Literal["light", "dark", "system"] | None = None


class ChangePasswordIn(BaseModel):
    current_password: str
    new_password: str = Field(min_length=8, max_length=128)


class ForgotIn(BaseModel):
    identifier: str


class ResetIn(BaseModel):
    identifier: str
    code: str = Field(min_length=6, max_length=6)
    new_password: str = Field(min_length=8, max_length=128)


class ColocationIn(BaseModel):
    name: str = Field(min_length=2, max_length=80)
    city: str = Field(min_length=2, max_length=80)
    occupants: int = Field(ge=2, le=30)

    @field_validator("name", "city")
    @classmethod
    def _strip(cls, v):
        return " ".join(v.split())


class ColocationPatch(BaseModel):
    name: str | None = Field(default=None, min_length=2, max_length=80)
    city: str | None = Field(default=None, min_length=2, max_length=80)
    occupants: int | None = Field(default=None, ge=2, le=30)


class JoinIn(BaseModel):
    code: str = Field(min_length=4, max_length=12)


class RolePatch(BaseModel):
    role: Literal["admin", "member"]


class MeterIn(BaseModel):
    number: str = Field(min_length=1, max_length=40)
    type: Literal["prepaid", "postpaid"]
    initial_index: float = Field(ge=0)
    tariff_fcfa: float = Field(gt=0)


class ReadingIn(BaseModel):
    index: float = Field(ge=0)
    read_at: str | None = None


class SplitSettingsIn(BaseModel):
    mode: Literal["consumption", "presence", "equal"]


class ConsumptionEntry(BaseModel):
    user_id: int
    kwh: float = Field(ge=0)


class ConsumptionsIn(BaseModel):
    period: str | None = None
    entries: list[ConsumptionEntry] = Field(min_length=1)


class PresenceEntry(BaseModel):
    user_id: int
    days: int = Field(ge=0, le=31)


class PresenceIn(BaseModel):
    period: str | None = None
    entries: list[PresenceEntry] = Field(min_length=1)


class PaymentIn(BaseModel):
    kind: Literal["recharge", "contribution", "reimbursement"] = "recharge"
    amount_fcfa: int = Field(gt=0, le=100_000_000)
    method: Literal["mtn_momo", "orange_money", "cash"]
    reference: str | None = Field(default=None, max_length=40)
    payer_id: int | None = None          # défaut : l'utilisateur connecté
    beneficiary_id: int | None = None    # obligatoire pour un remboursement


class DecisionIn(BaseModel):
    action: Literal["confirm", "reject"]


class ReminderIn(BaseModel):
    title: str = Field(min_length=2, max_length=120)
    subtitle: str = Field(default="", max_length=200)
    due_date: str | None = None


class ReminderPatch(BaseModel):
    title: str | None = Field(default=None, min_length=2, max_length=120)
    subtitle: str | None = Field(default=None, max_length=200)
    due_date: str | None = None
    active: bool | None = None
