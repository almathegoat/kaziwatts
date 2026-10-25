# KaziWatts — application complète (frontend MAUI + backend API)

```
KaziWatts_complet/
├── backend/      API Python (FastAPI + SQLite)   → à lancer EN PREMIER
└── KaziWatts1/   Application .NET MAUI (Windows)  → les 16 écrans, reliés à l'API
```

## Lancer
1. **Backend** : double-clic sur `backend\start.bat` (Python 3.10+). Laisser la fenêtre ouverte.
   L'API écoute sur http://localhost:8000 (documentation : http://localhost:8000/docs).
   Au premier lancement, des données de démonstration sont chargées.
2. **Application** : ouvrir `KaziWatts1` dans Visual Studio (ou `dotnet run` dans ce dossier) et lancer.

## Comptes de démonstration (mot de passe `Kazi2026!`)
| Compte | Rôle |
|---|---|
| amina@kazi.cm | Administrateur (« Appartement 3B ») |
| carine@kazi.cm, brice@kazi.cm | Membres |

Code d'invitation de la colocation de démo : `KZ7T28`.
Pour repartir de zéro : supprimer `backend\kaziwatts.db` puis relancer `start.bat`, ou créer un nouveau compte.

## Parcours à tester
- **Inscription** → créer une colocation (ou saisir un code) → configurer le compteur → accueil.
- **Accueil** : crédit, autonomie, dernière conso, dernière recharge — calculés par le serveur.
- **Relevé** : l'index doit dépasser le dernier ; l'estimation en kWh et FCFA s'affiche en direct.
- **Répartir** : onglets Consommation / Présence / Parts égales (admin) ; « Saisir les données du mois » pour les kWh ou les jours.
- **Recharger** → **Enregistrer paiement** (recharge ou remboursement, preuve JPG/PNG/PDF) → **Justificatif**.
  Un paiement reste « en attente » tant que l'admin (ou le bénéficiaire d'un remboursement) ne l'a pas confirmé ; il s'ouvre depuis **Paiements**.
- **Soldes** : dettes et remboursements suggérés. **Rappels** : alertes calculées + rappels perso.
- **Colocataires** : « Gérer » (rôles, retrait), invitation. **Profil** : thème enregistré sur le compte, déconnexion.

## Adresse de l'API
Par défaut `http://localhost:8000/api`. Pour une autre adresse : variable d'environnement `KAZIWATTS_API`
(ex. `setx KAZIWATTS_API http://192.168.1.20:8000/api`, puis relancer l'application).

## Fichiers ajoutés / modifiés dans l'application
- `Services/Api.cs` : client HTTP, modèles, session. `Services/MockData.cs` : seuls les formats (`Fmt`, `Ui`) sont encore utilisés.
- `Views/*.xaml` et `Views/*.xaml.cs` : chaque écran lit et écrit via l'API (plus de données fictives).
