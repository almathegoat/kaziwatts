using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class MeterSetupPage : ContentPage
{
    public MeterSetupPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await Session.RefreshAsync();
            var m = Session.Meter;
            if (m != null)
            {
                TypeSegments.SelectedIndex = m.IsPrepaid ? 0 : 1;
                MeterField.Text = m.Number;
                IndexField.Text = m.InitialIndex.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                TariffField.Text = m.TariffFcfa.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            if (Session.Coloc != null) KeySegments.SelectedIndex = Session.Coloc.SplitMode == "equal" ? 0 : 1;
            OnTypeChanged(null, EventArgs.Empty);
        }
        catch (ApiException) { /* champs laissés vides */ }
    }

    private void OnTypeChanged(object? sender, EventArgs e) =>
        TypeInfo.Text = TypeSegments.SelectedIndex == 0
            ? "Prépayé : rechargez par code et suivez votre crédit restant en temps réel."
            : "Postpayé : répartissez la facture du mois entre colocataires et suivez les paiements.";

    private async void OnNext(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MeterField.Text))
        { Ui.Error(ErrorLabel, "Saisissez le numéro de votre compteur."); return; }

        if (!Fmt.TryParse(IndexField.Text, out var index) || index < 0)
        { Ui.Error(ErrorLabel, "L’index initial doit être un nombre positif."); return; }

        if (!Fmt.TryParse(TariffField.Text, out var tariff) || tariff <= 0)
        { Ui.Error(ErrorLabel, "Le tarif doit être supérieur à 0."); return; }

        Ui.Error(ErrorLabel, null);
        try
        {
            await Api.Put<MeterDto>("/meter", new
            {
                number = MeterField.Text.Trim(),
                type = TypeSegments.SelectedIndex == 0 ? "prepaid" : "postpaid",
                initial_index = index,
                tariff_fcfa = tariff
            });
            // Clé de répartition : « Parts égales » → equal ; « Pondérée » → selon la consommation individuelle
            await Api.Put<System.Text.Json.JsonElement>("/split/settings", new { mode = KeySegments.SelectedIndex == 0 ? "equal" : "consumption" });
            await Session.RefreshAsync();
            await Shell.Current.GoToAsync("//home");
        }
        catch (ApiException ex) { Ui.Error(ErrorLabel, ex.Message); }
    }
}
