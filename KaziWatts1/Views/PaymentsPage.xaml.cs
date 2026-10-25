using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class PaymentsPage : ContentPage
{
    // 0 = Tous · 1 = Mobile Money (MTN + Orange) · 2 = Espèces
    private static readonly string[] Groups = { "all", "mobile_money", "cash" };
    private string _period = "all";

    public PaymentsPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var r = await Api.Get<PaymentsDto>($"/payments?method_group={Groups[Filters.SelectedIndex]}&period={_period}");
            BindableLayout.SetItemsSource(ItemsHost, r.Items);
            CountLabel.Text = r.Count + (r.Count > 1 ? " transactions" : " transaction");
            EmptyLabel.IsVisible = r.Count == 0;
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == 409) { BindableLayout.SetItemsSource(ItemsHost, null); EmptyLabel.IsVisible = true; CountLabel.Text = ""; return; }
            await DisplayAlert("Paiements", ex.Message, "OK");
        }
    }

    private async void OnFilterChanged(object? sender, EventArgs e) => await LoadAsync();

    private async void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: PaymentDto p })
        {
            Session.LastPaymentId = p.Id;
            await Shell.Current.GoToAsync(nameof(ReceiptPage));
        }
    }

    private async void OnFilterPeriod(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheet("Filtrer par période", "Annuler", null,
                                              "Ce mois-ci", "Le mois dernier", "3 derniers mois", "Tout l’historique");
        switch (choice)
        {
            case "Ce mois-ci": _period = "this_month"; break;
            case "Le mois dernier": _period = "last_month"; break;
            case "3 derniers mois": _period = "last_3_months"; break;
            case "Tout l’historique": _period = "all"; break;
            default: return;
        }
        PeriodLabel.Text = choice == "Tout l’historique" ? "Tous les paiements" : choice;
        await LoadAsync();
    }
}
