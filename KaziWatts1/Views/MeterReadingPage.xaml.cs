using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class MeterReadingPage : ContentPage
{
    private double _last;
    private double _tariff = 100;

    public MeterReadingPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private static string FrNum(double v) => v.ToString("#,0.##", Fmt.Fr);

    private async Task LoadAsync()
    {
        try
        {
            var meter = await Api.Get<MeterDto>("/meter");
            Session.Meter = meter;
            _tariff = meter.TariffFcfa;
            _last = meter.LastIndex;
            MeterNumber.Text = meter.Number;
            MeterKind.Text = meter.IsPrepaid ? "Compteur prépayé" : "Compteur postpayé";
            LastIndexLabel.Text = FrNum(_last);
            LastDateLabel.Text = Fmt.DateShort(meter.LastReadAt, true);
            DeltaInfo.Text = "Saisissez l’index affiché sur votre compteur.";

            var r = await Api.Get<ReadingsDto>("/readings?limit=10");
            BindableLayout.SetItemsSource(History, r.Items);
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == 404 || ex.StatusCode == 409)
            {
                await DisplayAlert("Compteur", "Le compteur n’est pas encore configuré par l’administrateur.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }
            await DisplayAlert("Relevé", ex.Message, "OK");
        }
    }

    private void OnIndexChanged(object? sender, TextChangedEventArgs e)
    {
        if (!Fmt.TryParse(e.NewTextValue, out var idx)) return;
        if (idx <= _last)
        {
            DeltaInfo.Text = "Le nouvel index doit dépasser " + FrNum(_last) + " kWh.";
            return;
        }
        var delta = idx - _last;
        DeltaInfo.Text = $"+{FrNum(delta)} kWh depuis le dernier relevé · ≈ {Fmt.Fcfa(delta * _tariff)}";
    }

    private async void OnSave(object? sender, EventArgs e)
    {
        if (!Fmt.TryParse(IndexField.Text, out var idx) || idx <= _last)
        { Ui.Error(ErrorLabel, $"Le nouvel index doit être supérieur au dernier relevé ({FrNum(_last)} kWh)."); return; }

        Ui.Error(ErrorLabel, null);
        try
        {
            await Api.Post<ReadingDto>("/readings", new { index = idx });
            await DisplayAlert("Relevé enregistré", "Votre relevé a bien été ajouté à l’historique.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException ex) { Ui.Error(ErrorLabel, ex.Message); }
    }
}
