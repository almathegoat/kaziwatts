using KaziWatts.Services;

namespace KaziWatts.Views;

public partial class RecordPaymentPage : ContentPage
{
    private static readonly string[] Methods = { "mtn_momo", "orange_money", "cash" };

    private List<MemberDto> _members = new();
    private MemberDto? _person;
    private FileResult? _proof;
    private bool _init;

    public RecordPaymentPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_init) return;
        _init = true;
        try
        {
            if (Session.Meter == null) await Session.RefreshAsync();
            var postpaid = Session.Meter is { IsPrepaid: false };
            KindTabs.Items = (postpaid ? "Contribution" : "Recharge") + "|Remboursement";
            KindTabs.SelectedIndex = Session.PendingKind == "reimbursement" ? 1 : 0;
            if (Session.PendingAmount > 0) AmountField.Text = Math.Round(Session.PendingAmount).ToString("0");

            _members = (await Api.Get<MembersDto>("/colocation/members")).Items;
            _person = _members.FirstOrDefault(m => m.IsMe);
            if (KindTabs.SelectedIndex == 1) _person = _members.FirstOrDefault(m => !m.IsMe);
            ApplyKind();
        }
        catch (ApiException ex)
        {
            await DisplayAlert("Paiement", ex.Message, "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    private bool IsReimbursement => KindTabs.SelectedIndex == 1;

    private void ApplyKind()
    {
        PersonCaption.Text = IsReimbursement ? "À qui remboursez-vous ?" : "Qui a payé ?";
        Header.Subtitle = IsReimbursement ? "Remboursement entre colocataires" : "Nouvel enregistrement";
        ShowPerson();
    }

    private void ShowPerson()
    {
        PersonName.Text = _person?.FullName ?? "Choisir une personne";
        PersonAvatar.Initials = _person?.Initials ?? "?";
    }

    private void OnKindChanged(object? sender, EventArgs e)
    {
        // Remboursement : la personne choisie est le bénéficiaire (jamais soi-même) ; recharge : le payeur
        if (IsReimbursement && (_person == null || _person.IsMe)) _person = _members.FirstOrDefault(m => !m.IsMe);
        if (!IsReimbursement && !Session.IsAdmin) _person = _members.FirstOrDefault(m => m.IsMe);
        ApplyKind();
    }

    private async void OnChangePerson(object? sender, TappedEventArgs e)
    {
        var candidates = _members.Where(m => IsReimbursement ? !m.IsMe : (Session.IsAdmin || m.IsMe)).ToList();
        if (candidates.Count == 0)
        {
            await DisplayAlert("Personne", IsReimbursement ? "Aucun autre colocataire." : "Seul l’administrateur peut enregistrer le paiement d’un autre colocataire.", "OK");
            return;
        }
        var choice = await DisplayActionSheet(IsReimbursement ? "Bénéficiaire" : "Payeur", "Annuler", null,
                                              candidates.Select(m => m.FullName).ToArray());
        if (string.IsNullOrEmpty(choice) || choice == "Annuler") return;
        _person = candidates.FirstOrDefault(m => m.FullName == choice) ?? _person;
        ShowPerson();
    }

    private async void OnPickProof(object? sender, TappedEventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choisir une capture ou une photo",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.WinUI] = new[] { ".jpg", ".jpeg", ".png", ".pdf" }
                })
            });
            if (file != null)
            {
                _proof = file;
                ProofLabel.Text = "Preuve : " + file.FileName;
            }
        }
        catch (Exception)
        {
            await DisplayAlert("Fichier", "Impossible d’ouvrir le sélecteur de fichiers.", "OK");
        }
    }

    private async void OnSave(object? sender, EventArgs e)
    {
        if (!Fmt.TryParse(AmountField.Text, out var amountD) || amountD <= 0)
        { Ui.Error(ErrorLabel, "Saisissez un montant valide en FCFA."); return; }
        if (_person == null)
        { Ui.Error(ErrorLabel, "Choisissez une personne."); return; }

        Ui.Error(ErrorLabel, null);
        var amount = (int)Math.Round(amountD);
        var method = Methods[Modes.SelectedIndex];
        var reference = string.IsNullOrWhiteSpace(RefField.Text) ? null : RefField.Text.Trim();
        try
        {
            object body;
            if (IsReimbursement)
                body = new { kind = "reimbursement", amount_fcfa = amount, method, reference, beneficiary_id = _person.UserId };
            else
                body = new { kind = Session.Meter is { IsPrepaid: false } ? "contribution" : "recharge", amount_fcfa = amount, method, reference, payer_id = _person.UserId };

            var p = await Api.Post<PaymentDto>("/payments", body);

            if (_proof != null)
            {
                try { await Api.Upload<PaymentDto>($"/payments/{p.Id}/proof", _proof); }
                catch (ApiException ex)
                { await DisplayAlert("Preuve non envoyée", "Le paiement est enregistré, mais la preuve a été refusée : " + ex.Message, "OK"); }
            }

            Session.PendingAmount = 0;
            Session.PendingKind = null;
            Session.LastPaymentId = p.Id;
            await Shell.Current.GoToAsync(nameof(ReceiptPage));
        }
        catch (ApiException ex) { Ui.Error(ErrorLabel, ex.Message); }
    }
}
