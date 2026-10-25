# KaziWatts – frontend .NET MAUI pour Windows (16 écrans)

Projet complet et autonome : il cible **uniquement Windows**. Aucun SDK Android, iOS ou macOS n'est nécessaire.

## Prérequis
- Windows 10 (1809 ou plus récent) ou Windows 11
- Un SDK .NET 8, 9 ou 10 (`dotnet --version` pour vérifier). Le projet s'adapte à la version installée.
- La charge de travail MAUI pour Windows. Dans PowerShell (administrateur) :

      dotnet workload install maui-windows

  (Avec Visual Studio 2022, la charge « Développement d'applications multiplateformes .NET MAUI » convient aussi,
  mais n'est pas obligatoire : la commande ci-dessus suffit.)

## Lancer le projet
**Option A : Visual Studio 2022 (17.8 ou plus)**
1. Dézippez, puis ouvrez `KaziWatts/KaziWatts.csproj`.
2. Dans la barre d'outils, choisissez **Windows Machine**, puis F5.

**Option B : ligne de commande**

      cd KaziWatts
      dotnet run

La première restauration télécharge les packages NuGet (connexion Internet nécessaire) et peut durer quelques minutes.

## Si ça ne compile pas
- **Erreur sur le TargetFramework** : ouvrez `KaziWatts.csproj` et remplacez la ligne `<TargetFramework>` par la valeur
  qui correspond à votre SDK : `net8.0-windows10.0.19041.0`, `net9.0-windows10.0.19041.0` ou `net10.0-windows10.0.19041.0`.
- **Message « workload » manquant** : relancez `dotnet workload install maui-windows`, puis `dotnet workload restore`.
- **Autre erreur** : copiez le message complet, il suffit pour trouver la correction.

## Contenu
Les 16 écrans sont dans `Views/` (Splash, Connexion, Inscription, Colocation, Configuration compteur, Accueil, Relevé,
Répartition, Recharger en 3 étapes, Historique paiements, Solde, Enregistrer un paiement, Justificatif, Rappels,
Colocataires, Profil). Les données fictives sont dans `Services/MockData.cs`, la palette dans `Resources/Styles/`.

## Particularités sur Windows
- La fenêtre a un format de téléphone (420 x 860) pour rappeler le cadre des maquettes.
- Les onglets (Accueil, Paiements, Colocs, Profil) s'affichent en haut de la fenêtre, c'est le comportement de MAUI sur Windows.
- Le partage ouvre le menu Windows ; s'il n'est pas disponible, le texte est copié dans le presse-papiers.
- Thème clair/sombre : suit Windows, ou se choisit dans l'onglet Profil.
