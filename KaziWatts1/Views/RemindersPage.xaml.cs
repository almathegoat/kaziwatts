using System.Text.Json;
using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class RemindersPage : ContentPage
{
    public RemindersPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var alerts = await Api.Get<AlertsDto>("/alerts");
            var reminders = await Api.Get<RemindersDto>("/reminders");
            var rows = new List<ReminderRow>();

            ReminderRow FromAlert(AlertDto a, string tag, string colorKey) => new()
            {
                Tag = tag, TagColor = Theme.Pick(colorKey, colorKey == "Primary" ? "LinkDark" : colorKey),
                Title = a.Title, Detail = a.Detail, Target = a.Target
            };

            // Alertes calculées par le serveur : retards, paiements à confirmer, facture, crédit bas
            rows.AddRange(alerts.Late.Select(a => FromAlert(a, "URGENT", "Danger")));
            rows.AddRange(alerts.LowCredit.Select(a => FromAlert(a, "URGENT", "Danger")));
            rows.AddRange(alerts.Soon.Where(a => a.Target != "reminders").Select(a => FromAlert(a, "BIENTÔT", "Warning")));
            rows.AddRange(alerts.Bill.Select(a => FromAlert(a, "INFO", "Primary")));

            // Rappels enregistrés (automatiques ou ajoutés par un colocataire)
            rows.AddRange(reminders.Items.Select(r => new ReminderRow
            {
                Tag = r.Kind == "custom" ? "RAPPEL" : "AUTO",
                TagColor = Theme.Pick("Primary", "LinkDark"),
                RightText = string.IsNullOrEmpty(r.DueDate) ? "" : "Échéance : " + Fmt.DateShort(r.DueDate),
                Title = r.Title,
                Detail = r.Subtitle,
                LinkText = r.Active ? "Gérer →" : "Désactivé · Gérer →",
                ReminderId = r.Id,
                Active = r.Active
            }));

            BindableLayout.SetItemsSource(ItemsHost, rows);
            CountBadge.Text = rows.Count.ToString();
            EmptyLabel.IsVisible = rows.Count == 0;
        }
        catch (ApiException ex)
        {
            await DisplayAlert("Rappels", ex.StatusCode == 409 ? "Rejoignez ou créez d’abord une colocation." : ex.Message, "OK");
        }
    }

    private async void OnLink(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject { BindingContext: ReminderRow row }) return;

        if (row.IsReminder) { await ManageAsync(row); return; }

        switch (row.Target)
        {
            case "balance": await Shell.Current.GoToAsync(nameof(BalancePage)); break;
            case "payments": await Shell.Current.GoToAsync("//payments"); break;
            case "split": await Shell.Current.GoToAsync(nameof(SplitPage)); break;
            case "recharge": await Shell.Current.GoToAsync(nameof(RechargePage)); break;
        }
    }

    private async Task ManageAsync(ReminderRow row)
    {
        var toggle = row.Active ? "Désactiver" : "Activer";
        var custom = row.Tag == "RAPPEL";
        var choice = custom
            ? await DisplayActionSheet(row.Title, "Annuler", "Supprimer", toggle)
            : await DisplayActionSheet(row.Title, "Annuler", null, toggle);
        try
        {
            if (choice == toggle)
                await Api.Patch<ReminderDto>($"/reminders/{row.ReminderId}", new { active = !row.Active });
            else if (choice == "Supprimer")
                await Api.Delete<JsonElement>($"/reminders/{row.ReminderId}");
            else return;
            await LoadAsync();
        }
        catch (ApiException ex) { await DisplayAlert("Rappel", ex.Message, "OK"); }
    }

    private async void OnAdd(object? sender, EventArgs e)
    {
        var title = await DisplayPromptAsync("Ajouter un rappel", "Titre du rappel", "Suivant", "Annuler");
        if (string.IsNullOrWhiteSpace(title)) return;

        var due = await DisplayPromptAsync("Échéance", "Date au format AAAA-MM-JJ (facultatif)", "Ajouter", "Annuler",
                                           placeholder: DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"));
        if (due == null) return;
        try
        {
            await Api.Post<ReminderDto>("/reminders", new
            {
                title = title.Trim(),
                subtitle = "Ajouté par " + Session.User?.FirstName,
                due_date = string.IsNullOrWhiteSpace(due) ? null : due.Trim()
            });
            await LoadAsync();
        }
        catch (ApiException ex) { await DisplayAlert("Rappel", ex.Message, "OK"); }
    }
}
