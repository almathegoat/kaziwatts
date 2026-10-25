using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class ColocationPage : ContentPage
{
    public ColocationPage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    // Sans colocation : le champ sert à SAISIR un code d'invitation. Avec colocation : il affiche le code à partager.
    private void Refresh()
    {
        var c = Session.Coloc;
        if (c == null)
        {
            CodeTitle.Text = "Code d’invitation reçu d’un colocataire";
            CodeField.IsReadOnly = false;
            CodeField.IconName = "";
            CodeField.Placeholder = "KZ7T28";
            MainButton.Text = "Rejoindre la colocation";
        }
        else
        {
            CodeTitle.Text = "Mon code de partage";
            CodeField.IsReadOnly = true;
            CodeField.IconName = "copy";
            CodeField.Text = c.InviteCode;
            MainButton.Text = "Continuer";
        }
    }

    private async void OnCopyCode(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CodeField.Text)) return;
        await Clipboard.Default.SetTextAsync(CodeField.Text);
        await DisplayAlert("Code copié", "Le code de partage a été copié dans le presse-papiers.", "OK");
    }

    private async void OnShareCode(object? sender, TappedEventArgs e)
    {
        var c = Session.Coloc;
        if (c == null)
        {
            await DisplayAlert("Partager mon code", "Créez d’abord une colocation pour obtenir un code à partager.", "OK");
            return;
        }
        await Ui.ShareAsync(this, "Inviter un colocataire",
            "Rejoins ma colocation sur KaziWatts avec le code " + c.InviteCode + " (valable 7 jours).");
    }

    // « Créer une nouvelle colocation » : nom, quartier, nombre d'occupants (3 questions successives)
    private async void OnCreateTapped(object? sender, TappedEventArgs e)
    {
        if (Session.Coloc != null)
        {
            await DisplayAlert("Colocation existante", "Vous appartenez déjà à « " + Session.Coloc.Name + " ».", "OK");
            return;
        }
        var name = await DisplayPromptAsync("Nouvelle colocation", "Nom de la colocation", "Suivant", "Annuler");
        if (string.IsNullOrWhiteSpace(name)) return;

        var city = await DisplayPromptAsync("Nouvelle colocation", "Quartier", "Suivant", "Annuler");
        if (string.IsNullOrWhiteSpace(city)) return;

        var countText = await DisplayPromptAsync("Nouvelle colocation", "Nombre d’occupants", "Créer", "Annuler",
                                                 keyboard: Keyboard.Numeric);
        if (countText == null) return;
        if (!int.TryParse(countText, out var n) || n < 2)
        {
            await DisplayAlert("Nombre invalide", "Le nombre d’occupants doit être d’au moins 2.", "OK");
            return;
        }

        try
        {
            var c = await Api.Post<ColocationDto>("/colocation", new { name = name.Trim(), city = city.Trim(), occupants = n });
            await Session.RefreshAsync();
            Refresh();
            await DisplayAlert("Colocation créée", $"{c.Name} · {c.City} ({c.Occupants} occupants). Partagez le code {c.InviteCode} avec vos colocataires.", "OK");
        }
        catch (ApiException ex) { await DisplayAlert("Création impossible", ex.Message, "OK"); }
    }

    private async void OnContinue(object? sender, EventArgs e)
    {
        try
        {
            if (Session.Coloc == null)
            {
                var code = (CodeField.Text ?? "").Trim();
                if (code.Length == 0)
                {
                    await DisplayAlert("Code manquant", "Saisissez le code reçu, ou créez une nouvelle colocation.", "OK");
                    return;
                }
                var c = await Api.Post<ColocationDto>("/colocation/join", new { code });
                await Session.RefreshAsync();
                await DisplayAlert("Bienvenue", "Vous avez rejoint « " + c.Name + " ».", "OK");
            }
            await Session.GoNextAsync();     // admin sans compteur → configuration ; sinon accueil
        }
        catch (ApiException ex) { await DisplayAlert("Colocation", ex.Message, "OK"); }
    }
}
