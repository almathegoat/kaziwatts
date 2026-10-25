using System.Globalization;
using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class RechargePage : ContentPage
{
    private static readonly int[] Values = { 5000, 10000, 15000, 20000 };
    private double _tariff = 100;

    public RechargePage()
    {
        InitializeComponent();
        OnAmountChanged(null, EventArgs.Empty);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var m = await Api.Get<MeterDto>("/meter");
            Session.Meter = m;
            _tariff = m.TariffFcfa;
            TariffLabel.Text = "Tarif : " + Fmt.Fcfa(_tariff).Replace(" FCFA", "") + " FCFA / kWh";
            Banner.Text = (m.IsPrepaid ? "La recharge sera ajoutée au compteur " : "Le paiement sera ajouté au compteur ")
                          + m.Number + " et à votre historique.";
            OnAmountChanged(null, EventArgs.Empty);
        }
        catch (ApiException ex)
        {
            await DisplayAlert("Recharge", ex.StatusCode is 404 or 409
                ? "Le compteur n’est pas encore configuré par l’administrateur."
                : ex.Message, "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    private static string Spaced(int n) => n.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", " ");

    private void OnAmountChanged(object? sender, EventArgs e)
    {
        var amount = Values[Amounts.SelectedIndex];
        AmountLabel.Text = Spaced(amount);
        KwhLabel.Text = Math.Round(amount / _tariff, 1).ToString("0.#", Fmt.Fr);
    }

    private async void OnContinue(object? sender, EventArgs e)
    {
        Session.PendingAmount = Values[Amounts.SelectedIndex];
        Session.PendingKind = "recharge";
        await Shell.Current.GoToAsync(nameof(RecordPaymentPage));
    }
}
