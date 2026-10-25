using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class ReceiptPage : ContentPage
{
    private ReceiptDto? _r;

    public ReceiptPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (Session.LastPaymentId == 0) return;
        try
        {
            _r = await Api.Get<ReceiptDto>($"/payments/{Session.LastPaymentId}/receipt");
            var p = _r.Payment;
            AmountLabel.Text = Fmt.Num(p.AmountFcfa);
            WhoLabel.Text = (p.Beneficiary != null ? $"De {p.Payer.Name} à {p.Beneficiary.Name}" : "Par " + p.Payer.Name) + " · " + _r.DateTime;
            RefLabel.Text = p.Reference;
            ModeLabel.Text = p.MethodLabel;
            StatusValue.Text = _r.StatusText;
            StatusValue.TextColor = p.StatusColor;
            ColocLabel.Text = _r.Colocation;
            FooterLabel.Text = "Reçu KaziWatts · " + p.KindLabel;

            Headline.TextColor = p.StatusColor;
            switch (p.Status)
            {
                case "confirmed":
                    Headline.Text = "PAIEMENT CONFIRMÉ !";
                    Header.Subtitle = "Ce paiement est pris en compte dans les soldes.";
                    break;
                case "pending":
                    Headline.Text = "EN ATTENTE DE CONFIRMATION";
                    Header.Subtitle = "Votre paiement a bien été enregistré.";
                    break;
                default:
                    Headline.Text = "PAIEMENT REFUSÉ";
                    Header.Subtitle = "Ce paiement n’est pas pris en compte.";
                    break;
            }
            DecisionBox.IsVisible = p.CanDecide;
            CancelBtn.IsVisible = p.CanCancel;
        }
        catch (ApiException ex) { await DisplayAlert("Justificatif", ex.Message, "OK"); }
    }

    private async void OnTabChanged(object? sender, EventArgs e)
    {
        if (Tabs.SelectedIndex == 1) await Shell.Current.GoToAsync("//payments");
    }

    private async Task DecideAsync(string action)
    {
        try
        {
            await Api.Post<PaymentDto>($"/payments/{Session.LastPaymentId}/decision", new { action });
            await LoadAsync();
        }
        catch (ApiException ex) { await DisplayAlert("Paiement", ex.Message, "OK"); }
    }

    private async void OnConfirm(object? sender, EventArgs e) => await DecideAsync("confirm");

    private async void OnReject(object? sender, EventArgs e)
    {
        if (await DisplayAlert("Refuser le paiement", "Ce paiement ne sera pas pris en compte. Continuer ?", "Refuser", "Annuler"))
            await DecideAsync("reject");
    }

    private async void OnCancel(object? sender, EventArgs e)
    {
        if (!await DisplayAlert("Annuler le paiement", "Supprimer ce paiement en attente ?", "Supprimer", "Retour")) return;
        try
        {
            await Api.Delete<System.Text.Json.JsonElement>($"/payments/{Session.LastPaymentId}");
            await Shell.Current.GoToAsync("//payments");
        }
        catch (ApiException ex) { await DisplayAlert("Paiement", ex.Message, "OK"); }
    }

    private async void OnDownload(object? sender, EventArgs e)
    {
        if (_r == null) return;
        try
        {
            var path = Path.Combine(FileSystem.CacheDirectory, $"justificatif-KaziWatts-{_r.Payment.Reference}.txt");
            await File.WriteAllTextAsync(path, _r.Text);
            await Launcher.Default.OpenAsync(new OpenFileRequest("Justificatif", new ReadOnlyFile(path)));
        }
        catch (Exception)
        {
            await DisplayAlert("Justificatif", "Le justificatif n’a pas pu être ouvert.", "OK");
        }
    }

    private async void OnShare(object? sender, EventArgs e)
    {
        if (_r == null) return;
        await Ui.ShareAsync(this, "Justificatif de paiement", _r.ShareText);
    }
}
