using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage() => InitializeComponent();

    // Mot de passe oublié : identifiant → code à 6 chiffres → nouveau mot de passe
    private async void OnForgot(object? sender, TappedEventArgs e)
    {
        var ident = await DisplayPromptAsync("Mot de passe oublié", "Votre e-mail ou numéro de téléphone", "Envoyer", "Annuler",
                                             keyboard: Keyboard.Email, initialValue: IdField.Text ?? "");
        if (string.IsNullOrWhiteSpace(ident)) return;
        try
        {
            var r = await Api.Post<MessageDto>("/auth/forgot-password", new { identifier = ident.Trim() });
            var info = r.Message + (r.DevCode != null ? "\n\nMode démonstration : votre code est " + r.DevCode : "");
            await DisplayAlert("Code envoyé", info, "OK");

            var code = await DisplayPromptAsync("Code de vérification", "Code à 6 chiffres", "Suivant", "Annuler",
                                                maxLength: 6, keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(code)) return;
            var pwd = await DisplayPromptAsync("Nouveau mot de passe", "8 caractères minimum", "Valider", "Annuler");
            if (string.IsNullOrWhiteSpace(pwd)) return;

            var ok = await Api.Post<MessageDto>("/auth/reset-password",
                new { identifier = ident.Trim(), code = code.Trim(), new_password = pwd });
            await DisplayAlert("Mot de passe réinitialisé", ok.Message, "OK");
        }
        catch (ApiException ex) { await DisplayAlert("Mot de passe oublié", ex.Message, "OK"); }
    }

    private async void OnSocialTapped(object? sender, TappedEventArgs e) =>
        await DisplayAlert("Bientôt disponible", $"La connexion avec {e.Parameter} sera ajoutée prochainement.", "OK");

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(IdField.Text) || string.IsNullOrWhiteSpace(PwdField.Text))
        {
            Ui.Error(ErrorLabel, "Renseignez votre identifiant et votre mot de passe.");
            return;
        }
        Ui.Error(ErrorLabel, null);
        LoginButton.Text = "Connexion…";
        LoginButton.IsEnabled = false;
        try
        {
            var r = await Api.Post<AuthDto>("/auth/login", new { identifier = IdField.Text.Trim(), password = PwdField.Text });
            Session.Token(r.Token, RememberCheck.IsChecked);
            Session.User = r.User;
            PwdField.Text = "";
            await Session.GoNextAsync();
        }
        catch (ApiException ex) { Ui.Error(ErrorLabel, ex.Message); }
        finally
        {
            LoginButton.Text = "Se connecter";
            LoginButton.IsEnabled = true;
        }
    }

    private async void OnSignUpTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(SignUpPage));
}
