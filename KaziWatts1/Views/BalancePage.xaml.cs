using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class BalancePage : ContentPage
{
    private BalanceDto? _b;

    public BalancePage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _b = await Api.Get<BalanceDto>("/balance");
            Render();
        }
        catch (ApiException ex)
        {
            await DisplayAlert("Soldes", ex.StatusCode == 409 ? "Configurez d’abord le compteur de la colocation." : ex.Message, "OK");
        }
    }

    private void Render()
    {
        if (_b == null) return;
        var debts = Tabs.SelectedIndex == 1;
        var items = debts ? _b.Items.Where(i => i.Status == "debtor").ToList() : _b.Items;
        BindableLayout.SetItemsSource(ItemsHost, items);

        if (_b.Settlements.Count > 0)
            Banner.Text = string.Join("\n", _b.Settlements.Select(s => $"{s.FromName} doit {Fmt.Fcfa(s.AmountFcfa)} à {s.ToName}"));
        else
            Banner.Text = debts ? "Aucune dette en cours." : "Tout est équilibré : personne ne doit rien.";
    }

    private async void OnTabChanged(object? sender, EventArgs e)
    {
        if (Tabs.SelectedIndex <= 1) { Render(); return; }       // Solde · Dettes
        await Shell.Current.GoToAsync("//payments");              // Historique
    }

    private async void OnDetails(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//payments");
}
