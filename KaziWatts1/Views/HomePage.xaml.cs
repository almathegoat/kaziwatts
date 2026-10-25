using KaziWatts.Controls;
using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class HomePage : ContentPage
{
    private readonly RingDrawable _ring;
    private readonly Border[] _bars;

    public HomePage()
    {
        InitializeComponent();
        _ring = (RingDrawable)Resources["RingDrawable"];
        Ring.Drawable = _ring;
        _bars = new[] { Bar0, Bar1, Bar2, Bar3, Bar4, Bar5, Bar6 };
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
            if (string.IsNullOrEmpty(Api.Token)) { await Shell.Current.GoToAsync("//splash"); return; }
            await Session.RefreshAsync();
            var c = Session.Coloc;
            if (c == null) { await Shell.Current.GoToAsync(nameof(ColocationPage)); return; }

            Greeting.Text = "Bonjour " + Session.User!.FirstName;
            SubLabel.Text = c.Name + " · " + c.City;

            var h = await Api.Get<HomeDto>("/home");
            if (!h.HasMeter || h.Meter == null) { ShowNoMeter(); return; }

            SplitDto? split = null;
            try { split = await Api.Get<SplitDto>("/split"); } catch (ApiException) { /* pas encore de relevé */ }

            TypeLabel.Text = h.Meter.Type == "prepaid" ? "Prépayé" : "Postpayé";
            FillCredit(h, split);
            FillConso(split);
            await FillBarsAsync();
            await FillRechargeAsync();
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == 401) { await Shell.Current.GoToAsync("//splash"); return; }
            await DisplayAlert("Accueil", ex.Message, "OK");
        }
    }

    private void ShowNoMeter()
    {
        TypeLabel.Text = "";
        CreditLabel.Text = "–";
        CreditFcfaLabel.Text = "";
        AutonomyLabel.Text = Session.IsAdmin ? "Configurez le compteur (Profil)" : "Compteur non configuré par l’admin";
        ThresholdLabel.Text = "";
        SetGauge(0);
    }

    private void SetGauge(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        _ring.SweepDeg = (float)(282 * fraction);
        Ring.Invalidate();
        GaugeBar.WidthRequest = 142 * fraction;
    }

    private void FillCredit(HomeDto h, SplitDto? split)
    {
        if (h.Meter!.Type == "prepaid")
        {
            CardTitle.Text = "CRÉDIT RESTANT";
            var credit = h.CreditKwh ?? 0;
            CreditLabel.Text = credit.ToString("0.##", Fmt.Fr);
            CreditFcfaLabel.Text = "≈ " + Fmt.Fcfa(h.CreditFcfa ?? 0);
            double frac;
            if (h.AutonomyDays is double d)
            {
                AutonomyLabel.Text = "Il vous reste ≈ " + d.ToString("0.#", Fmt.Fr) + (d >= 2 ? " jours" : " jour");
                frac = d / 7.0;                    // jauge pleine = 7 jours d'autonomie
            }
            else
            {
                AutonomyLabel.Text = credit > 0 ? "Autonomie : en attente de relevés" : "Crédit épuisé";
                frac = credit > 0 ? 1 : 0;
            }
            ThresholdLabel.Text = h.AvgKwhPerDay > 0
                ? "Alerte sous " + Fmt.Kwh(Math.Round(h.AvgKwhPerDay * 3, 1))
                : "Alerte : autonomie < 3 jours";
            SetGauge(frac);
        }
        else
        {
            CardTitle.Text = "À RÉPARTIR";
            CreditLabel.Text = (split?.TotalKwh ?? 0).ToString("0.##", Fmt.Fr);
            CreditFcfaLabel.Text = "≈ " + Fmt.Fcfa(split?.AmountFcfa ?? 0);
            AutonomyLabel.Text = "Facture à partager entre colocataires";
            ThresholdLabel.Text = "Moyenne : " + Fmt.Kwh(h.AvgKwhPerDay) + " / jour";
            SetGauge(1);
        }
    }

    private void FillConso(SplitDto? split)
    {
        if (split == null || split.TotalKwh <= 0)
        {
            ConsoPeriod.Text = split?.PeriodLabel ?? "";
            ConsoKwh.Text = "–";
            ConsoFcfa.Text = "Aucune consommation relevée";
            SharesLabel.Text = "";
            return;
        }
        ConsoPeriod.Text = split.PeriodLabel;
        ConsoKwh.Text = Fmt.Kwh(split.TotalKwh);
        ConsoFcfa.Text = "≈ " + Fmt.Fcfa(split.AmountFcfa);
        SharesLabel.Text = string.Join("  ·  ", split.Shares.Select(s =>
            (s.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? s.Name) + " " + Fmt.Pct(s.Percent)));
    }

    // Mini histogramme : consommation des 7 derniers relevés
    private async Task FillBarsAsync()
    {
        var r = await Api.Get<ReadingsDto>("/readings?limit=12");
        var deltas = r.Items.Where(x => !x.IsInitial).OrderBy(x => x.ReadAt).TakeLast(7).Select(x => x.DeltaKwh).ToList();
        var max = deltas.Count > 0 ? deltas.Max() : 0;
        var offset = _bars.Length - deltas.Count;
        var soft = (Theme.Res("PrimarySoftLight"), Theme.Res("PrimarySoftDark"));
        var primary = Theme.Res("Primary");
        for (var i = 0; i < _bars.Length; i++)
        {
            var k = i - offset;
            var hasData = k >= 0 && max > 0;
            _bars[i].HeightRequest = hasData ? Math.Max(6, 56 * deltas[k] / max) : 4;
            if (hasData && k == deltas.Count - 1)
                _bars[i].SetAppThemeColor(VisualElement.BackgroundColorProperty, primary, primary);
            else
                _bars[i].SetAppThemeColor(VisualElement.BackgroundColorProperty, soft.Item1, soft.Item2);
        }
    }

    private async Task FillRechargeAsync()
    {
        var p = await Api.Get<PaymentsDto>("/payments?status=confirmed");
        var last = p.Items.FirstOrDefault(x => x.Kind != "reimbursement");
        RechargeRow.IsVisible = last != null;
        if (last == null) return;
        RechargeWho.Text = last.Payer.Name.Split(' ').First() + " · " + Fmt.DateShort(last.CreatedAt);
        RechargeAmount.Text = "+" + Fmt.Fcfa(last.AmountFcfa);
    }

    private async void OnReading(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(MeterReadingPage));
    private async void OnSplit(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(SplitPage));
    private async void OnRecharge(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(RechargePage));
    private async void OnReminders(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(RemindersPage));
}
