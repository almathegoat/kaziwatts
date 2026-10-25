# KaziWatts – Backend (API REST)

FastAPI + SQLite, authentification JWT. Aucune base à installer : le fichier `kaziwatts.db` se crée tout seul.

## Lancer (Windows)
Double-clic sur `start.bat` (ou : `pip install -r requirements.txt`, puis `python seed.py`, puis
`python -m uvicorn app.main:app --port 8000`). Python 3.10+ requis.

- API : http://localhost:8000/api — **documentation interactive et essais : http://localhost:8000/docs**
- Données de démo (= `MockData.cs`) : `python seed.py` (ou `python seed.py --reset` pour repartir de zéro)
  - Comptes : `amina@kazi.cm` (admin), `carine@kazi.cm`, `brice@kazi.cm` — mot de passe `Kazi2026!`
  - Code d'invitation : `KZ7T28`
- Tests : `python -m pytest` (12 scénarios : auth, colocation, compteur, relevés, paiements, répartition, rappels, isolation)

## Règles métier
- Authentification : `Authorization: Bearer <token>` ; identifiant = e-mail ou téléphone camerounais (9 chiffres, préfixe 237 ajouté).
  Mot de passe ≥ 8 caractères (PBKDF2), blocage après 5 échecs / 15 min.
- Un utilisateur appartient à **une** colocation. Le créateur est admin ; code d'invitation de 6 caractères valable 7 jours ;
  nombre d'occupants = capacité maximale.
- Compteur : prépayé ou postpayé, tarif en FCFA/kWh. Le premier relevé (« relevé de départ ») est créé à la configuration.
  Un nouvel index doit être **strictement supérieur** au dernier.
- Répartition d'une période (mois) : mode `consumption` (kWh par sous-compteur, repli sur la présence si rien n'est saisi),
  `presence` (jours de présence) ou `equal`.
- Paiements : `recharge` / `contribution` (kWh apportés = montant ÷ tarif au moment du paiement) et `reimbursement`
  (transfert de kWh du payeur vers le bénéficiaire). Un paiement est « En attente » jusqu'à décision du bénéficiaire ou de
  l'admin (jamais du payeur) ; confirmé d'emblée si l'auteur est habilité (admin, ou bénéficiaire). Seuls les paiements
  **confirmés** comptent dans les soldes.
- Solde d'un membre = kWh apportés + remboursements versés − remboursements reçus − kWh consommés (répartis).
  Crédit (prépayé) = kWh rechargés − kWh relevés ; autonomie = crédit ÷ consommation moyenne des 30 derniers jours.

## Endpoints (préfixe `/api`)
| Domaine | Routes |
|---|---|
| Auth | `POST /auth/register` · `POST /auth/login` · `GET/PATCH /auth/me` · `POST /auth/change-password` · `POST /auth/forgot-password` · `POST /auth/reset-password` |
| Colocation | `POST /colocation` · `POST /colocation/join` · `GET/PATCH /colocation` · `GET /colocation/invite` · `POST /colocation/invite/regenerate` · `GET /colocation/members` · `PATCH/DELETE /colocation/members/{user_id}` · `POST /colocation/leave` |
| Compteur | `GET/PUT /meter` · `GET /meter/quote?amount_fcfa=` · `GET/POST /readings` · `GET /readings/preview?index=` |
| Répartition | `GET /split?period=AAAA-MM` · `PUT /split/settings` · `PUT /split/consumptions` · `PUT /split/presence` |
| Paiements | `GET/POST /payments` (filtres `method_group`, `period`, `status`, `kind`, `mine`) · `GET/DELETE /payments/{id}` · `POST /payments/{id}/decision` · `POST/GET /payments/{id}/proof` · `GET /payments/{id}/receipt` · `GET /payments/{id}/receipt.txt` |
| Soldes | `GET /balance?only_debtors=` (inclut remboursements suggérés) |
| Rappels | `GET/POST /reminders` · `PATCH/DELETE /reminders/{id}` · `GET /alerts` · `GET /home` |

Erreurs : JSON `{"detail": "message en français"}` prêt à afficher dans `Ui.Error(...)` côté MAUI.

## Production
Variables : `KAZI_SECRET` (clé JWT), `KAZI_DEV=0` (n'expose plus le code de réinitialisation : brancher un SMS/e-mail
dans `routers/auth.py`), `KAZI_DB`, `KAZI_UPLOADS`.
