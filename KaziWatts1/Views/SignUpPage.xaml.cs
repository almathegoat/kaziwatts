using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class SignUpPage : ContentPage
{
    public SignUpPage() => InitializeComponent();

    private async void OnSignUp(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameField.Text) || string.IsNullOrWhiteSpace(IdField.Text) ||
            string.IsNullOrWhiteSpace(PwdField.Text))
        { Ui.Error(ErrorLabel, "Remplissez tous les champs obligatoires."); return; }

        if (PwdField.Text != ConfirmField.Text)
        { Ui.Error(ErrorLabel, "Les deux mots de passe ne sont pas identiques."); return; }

        if (!TermsCheck.IsChecked)
        { Ui.Error(ErrorLabel, "Acceptez les conditions d’utilisation pour continuer."); return; }

        Ui.Error(ErrorLabel, null);
        try
        {
            var r = await Api.Post<AuthDto>("/auth/register", new
            {
                full_name = NameField.Text.Trim(),
                identifier = IdField.Text.Trim(),
                password = PwdField.Text,
                confirm_password = ConfirmField.Text,
                accept_terms = true
            });
            Session.Token(r.Token, true);
            Session.User = r.User;
            await Session.GoNextAsync();      // compte neuf : étape « Colocation »
        }
        catch (ApiException ex) { Ui.Error(ErrorLabel, ex.Message); }
    }

    private async void OnLogin(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("//login");
}
