using System.Text.Json;
using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class RoommatesPage : ContentPage
{
    private List<MemberDto> _members = new();

    public RoommatesPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            await Session.RefreshAsync();
            var c = Session.Coloc;
            if (c == null) { await Shell.Current.GoToAsync(nameof(ColocationPage)); return; }

            var m = await Api.Get<MembersDto>("/colocation/members");
            _members = m.Items;
            BindableLayout.SetItemsSource(ItemsHost, _members);

            Header.Subtitle = c.Name + " · " + c.City;
            CodeLabel.Text = c.InviteCode;
            var kind = Session.Meter == null ? "compteur non configuré" : Session.Meter.IsPrepaid ? "Compteur prépayé" : "Compteur postpayé";
            HomeSub.Text = $"{m.Count} colocataire{(m.Count > 1 ? "s" : "")} · {kind}";
            MembersTitle.Text = $"Membres de la colocation ({m.Count}/{m.Capacity})";
            ModeBanner.Text = "Mode de répartition actuel : " + c.SplitMode switch
            {
                "presence" => "selon les jours de présence.",
                "equal" => "parts égales entre colocataires.",
                _ => "selon la consommation de chacun."
            };
        }
        catch (ApiException ex) { await DisplayAlert("Colocataires", ex.Message, "OK"); }
    }

    private async void OnTabChanged(object? sender, EventArgs e)
    {
        if (Tabs.SelectedIndex != 1) return;

        var choice = await DisplayActionSheet("Paramètres du foyer", "Annuler", null,
                                              "Configuration du compteur", "Mode de répartition", "Rappels", "Quitter la colocation");
        Tabs.SelectedIndex = 0;
        switch (choice)
        {
            case "Configuration du compteur": await Shell.Current.GoToAsync(nameof(MeterSetupPage)); break;
            case "Mode de répartition": await Shell.Current.GoToAsync(nameof(SplitPage)); break;
            case "Rappels": await Shell.Current.GoToAsync(nameof(RemindersPage)); break;
            case "Quitter la colocation": await LeaveAsync(); break;
        }
    }

    private async Task LeaveAsync()
    {
        if (!await DisplayAlert("Quitter la colocation", "Vous ne verrez plus les données de ce foyer. Continuer ?", "Quitter", "Annuler")) return;
        try
        {
            await Api.Post<JsonElement>("/colocation/leave");
            await Session.RefreshAsync();
            await Shell.Current.GoToAsync(nameof(ColocationPage));
        }
        catch (ApiException ex) { await DisplayAlert("Quitter la colocation", ex.Message, "OK"); }
    }

    // « Gérer » : l'admin change un rôle ou retire un colocataire
    private async void OnManage(object? sender, TappedEventArgs e)
    {
        if (!Session.IsAdmin)
        {
            await DisplayAlert("Gérer les membres", "Seul l’administrateur peut retirer un colocataire ou changer les rôles.", "OK");
            return;
        }
        var others = _members.Where(m => !m.IsMe).ToList();
        if (others.Count == 0)
        {
            await DisplayAlert("Gérer les membres", "Vous êtes seul dans la colocation : invitez vos colocataires avec le code.", "OK");
            return;
        }
        var who = await DisplayActionSheet("Gérer un membre", "Annuler", null, others.Select(m => m.FullName).ToArray());
        var target = others.FirstOrDefault(m => m.FullName == who);
        if (target == null) return;

        var roleAction = target.IsAdmin ? "Retirer le rôle admin" : "Passer administrateur";
        var action = await DisplayActionSheet(target.FullName, "Annuler", target.IsAdmin ? null : "Retirer de la colocation", roleAction);
        try
        {
            if (action == roleAction)
                await Api.Patch<JsonElement>($"/colocation/members/{target.UserId}", new { role = target.IsAdmin ? "member" : "admin" });
            else if (action == "Retirer de la colocation")
            {
                if (!await DisplayAlert("Retirer", $"Retirer {target.FullName} de la colocation ?", "Retirer", "Annuler")) return;
                await Api.Delete<JsonElement>($"/colocation/members/{target.UserId}");
            }
            else return;
            await LoadAsync();
        }
        catch (ApiException ex) { await DisplayAlert("Gérer les membres", ex.Message, "OK"); }
    }

    private async void OnInvite(object? sender, EventArgs e)
    {
        try
        {
            var inv = await Api.Get<InviteDto>("/colocation/invite");
            if (inv.Expired && Session.IsAdmin)
                inv = await Api.Post<InviteDto>("/colocation/invite/regenerate");
            else if (inv.Expired)
            {
                await DisplayAlert("Code expiré", "Demandez à l’administrateur de générer un nouveau code.", "OK");
                return;
            }
            CodeLabel.Text = inv.Code;
            await Ui.ShareAsync(this, "Inviter un colocataire", inv.ShareText);
        }
        catch (ApiException ex) { await DisplayAlert("Invitation", ex.Message, "OK"); }
    }
}
