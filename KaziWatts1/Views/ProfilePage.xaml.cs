using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class ProfilePage : ContentPage
{
    private bool _loading;

    public ProfilePage()
    {
        InitializeComponent();
        ThemeSegments.SelectedIndex = Session.IsDarkPreferred ? 1 : 0;
        UpdateThemeUi();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await Session.RefreshAsync();
            var u = Session.User;
            if (u == null) return;
            ProfileAvatar.Initials = u.Initials;
            NameLabel.Text = u.FullName;
            var since = Fmt.DateShort(u.CreatedAt);
            since = since.Length > 3 ? since[(since.IndexOf(' ') + 1)..] : since;      // « sept. 2026 »
            SubLabel.Text = (Session.Coloc != null ? Session.Coloc.Name + " · " : "") + "Membre depuis " + since;

            _loading = true;
            ThemeSegments.SelectedIndex = Session.IsDarkPreferred ? 1 : 0;
            _loading = false;
            UpdateThemeUi();
        }
        catch (ApiException) { /* on garde l'affichage précédent */ }
    }

    private void UpdateThemeUi()
    {
        ThemeValue.Text = ThemeSegments.SelectedIndex == 1 ? "Sombre" : "Clair";
        ThemeIcon.Name = ThemeSegments.SelectedIndex == 1 ? "moon" : "sun";
    }

    // Le thème choisi est appliqué tout de suite puis mémorisé sur le compte
    private async void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_loading) return;
        var theme = ThemeSegments.SelectedIndex == 1 ? "dark" : "light";
        Session.SetDark(theme == "dark");
        UpdateThemeUi();
        try { Session.User = await Api.Patch<UserDto>("/auth/me", new { theme }); }
        catch (ApiException) { /* thème appliqué localement même si le serveur est injoignable */ }
    }

    private async void OnRoommates(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("//roommates");
    private async void OnSplit(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(SplitPage));
    private async void OnMeter(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(MeterSetupPage));
    private async void OnReminders(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(RemindersPage));

    private async void OnHelp(object? sender, TappedEventArgs e) =>
        await DisplayAlert("Aide & support", "Écrivez-nous à support@kaziwatts.cm — réponse sous 24 h.", "OK");

    private async void OnAbout(object? sender, TappedEventArgs e) =>
        await DisplayAlert("À propos de KaziWatts", "KaziWatts · version 1.0\nSuivez vos kWh, partagez vos dépenses et gardez l’équilibre entre colocataires.", "OK");

    private async void OnLogout(object? sender, TappedEventArgs e)
    {
        if (!await DisplayAlert("Se déconnecter", "Voulez-vous vraiment vous déconnecter ?", "Se déconnecter", "Annuler")) return;
        Session.Clear();
        await Shell.Current.GoToAsync("//splash");
    }
}
