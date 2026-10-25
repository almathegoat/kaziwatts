using System.Text.Json;
using KaziWatts.Controls;
using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class SplitPage : ContentPage
{
    private static readonly string[] Modes = { "consumption", "presence", "equal" };
    private static readonly string[] ModeText = { "selon la consommation", "selon la présence", "parts égales" };
    private static readonly string[] PaletteKeys = { "Primary", "DonutLavender", "DonutYellow", "Success", "Danger", "HeroCyan" };

    private readonly DonutDrawable _donut;
    private SplitDto? _split;

    public SplitPage()
    {
        InitializeComponent();
        _donut = (DonutDrawable)Resources["DonutDrawable"];
        Donut.Drawable = _donut;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _split = await Api.Get<SplitDto>("/split");
            var s = _split;
            var applied = Math.Max(0, Array.IndexOf(Modes, s.AppliedMode));
            var wanted = Math.Max(0, Array.IndexOf(Modes, s.Mode));
            ModeTabs.SelectedIndex = wanted;
            Header.Subtitle = s.PeriodLabel + " · " + ModeText[applied] + (applied != wanted ? " (par défaut)" : "");

            TotalCaption.Text = $"Total calculé · {Fmt.Kwh(s.TotalKwh)} × {Fmt.Fcfa(s.TariffFcfa)}";
            TotalAmount.Text = Fmt.Num(s.AmountFcfa);
            DonutKwh.Text = s.TotalKwh.ToString("0.##", Fmt.Fr);

            for (var i = 0; i < s.Shares.Count; i++) s.Shares[i].Dot = Theme.Res(PaletteKeys[i % PaletteKeys.Length]);
            BindableLayout.SetItemsSource(Rows, s.Shares);

            _donut.Segments = s.TotalKwh > 0 && s.Shares.Count > 0
                ? s.Shares.Select(x => ((float)(x.Percent / 100.0), x.Dot)).ToArray()
                : new[] { (1f, Theme.Pick("StrokeLight", "StrokeDark")) };
            Donut.Invalidate();
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == 409)
            {
                await DisplayAlert("Répartition", "Configurez d’abord le compteur de la colocation.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }
            await DisplayAlert("Répartition", ex.Message, "OK");
        }
    }

    private async void OnModeChanged(object? sender, EventArgs e)
    {
        if (!Session.IsAdmin)
        {
            await DisplayAlert("Mode de répartition", "Seul l’administrateur peut changer le mode de répartition.", "OK");
            await LoadAsync();           // remet l'onglet sur le mode réel
            return;
        }
        try
        {
            await Api.Put<JsonElement>("/split/settings", new { mode = Modes[ModeTabs.SelectedIndex] });
            await LoadAsync();
        }
        catch (ApiException ex) { await DisplayAlert("Mode de répartition", ex.Message, "OK"); }
    }

    // Saisie des kWh (sous-compteurs) ou des jours de présence, membre par membre
    private async void OnInputs(object? sender, EventArgs e)
    {
        if (_split == null) return;
        if (_split.Mode == "equal")
        {
            await DisplayAlert("Parts égales", "En mode « Parts égales », aucune saisie n’est nécessaire.", "OK");
            return;
        }
        var cons = _split.Mode == "consumption";
        var list = cons ? _split.Inputs.Consumptions : _split.Inputs.Presence;
        var entries = new List<object>();
        foreach (var m in list)
        {
            if (!Session.IsAdmin && m.UserId != Session.User!.Id) continue;     // un membre ne saisit que lui-même
            var current = cons ? (m.Kwh?.ToString("0.##", Fmt.Fr) ?? "") : m.Days.ToString();
            var v = await DisplayPromptAsync(cons ? "Consommation (kWh)" : "Jours de présence",
                                             m.Name + " · " + _split.PeriodLabel, "Suivant", "Terminer",
                                             keyboard: Keyboard.Numeric, initialValue: current);
            if (v == null) break;
            if (!Fmt.TryParse(v, out var n) || n < 0)
            {
                await DisplayAlert("Valeur invalide", "Saisissez un nombre positif.", "OK");
                return;
            }
            entries.Add(cons ? new { user_id = m.UserId, kwh = n } : (object)new { user_id = m.UserId, days = (int)Math.Round(n) });
        }
        if (entries.Count == 0) return;
        try
        {
            await Api.Put<JsonElement>(cons ? "/split/consumptions" : "/split/presence", new { period = _split.Period, entries });
            await LoadAsync();
        }
        catch (ApiException ex) { await DisplayAlert("Saisie", ex.Message, "OK"); }
    }

    private async void OnDetail(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(BalancePage));
}
