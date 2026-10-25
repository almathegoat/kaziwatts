using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class SplashPage : ContentPage
{
    private bool _autoLoginTried;

    public SplashPage() => InitializeComponent();

    // Reconnexion automatique si l'utilisateur avait coché « Se souvenir de moi »
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_autoLoginTried) return;
        _autoLoginTried = true;

        var token = Session.SavedToken();
        if (token == null) return;
        try
        {
            await Task.Delay(400);                 // laisse le Shell finir son initialisation
            Api.Token = token;
            if (await Session.RefreshAsync()) await Session.GoNextAsync();
        }
        catch (ApiException) { Api.Token = null; } // serveur éteint ou session expirée : on reste sur l'accueil
    }

    private async void OnStart(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(SignUpPage));
    private async void OnLogin(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("//login");
}
