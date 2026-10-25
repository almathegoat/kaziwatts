using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace KaziWatts.Services;

/// <summary>Erreur renvoyée par l'API (message déjà en français, prêt à afficher).</summary>
public class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiException(int status, string message) : base(message) => StatusCode = status;
}

/// <summary>Client HTTP du backend KaziWatts. Adresse : variable d'environnement KAZIWATTS_API, sinon http://localhost:8000/api.</summary>
public static class Api
{
    public static string BaseUrl { get; set; } =
        (Environment.GetEnvironmentVariable("KAZIWATTS_API") ?? "http://localhost:8000/api").TrimEnd('/');

    public static string? Token { get; set; }

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private static HttpRequestMessage Build(HttpMethod method, string path, HttpContent? content = null)
    {
        var req = new HttpRequestMessage(method, BaseUrl + path) { Content = content };
        if (!string.IsNullOrEmpty(Token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return req;
    }

    private static async Task<T> Send<T>(HttpRequestMessage req)
    {
        HttpResponseMessage res;
        try { res = await Http.SendAsync(req); }
        catch (Exception)
        {
            throw new ApiException(0, "Impossible de joindre le serveur. Vérifiez que l'API est lancée (start.bat) et l'adresse : " + BaseUrl);
        }
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            var msg = "Erreur " + (int)res.StatusCode;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                    msg = d.GetString() ?? msg;
            }
            catch { /* corps non JSON */ }
            if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(Token))
                Session.Clear();
            throw new ApiException((int)res.StatusCode, msg);
        }
        if (typeof(T) == typeof(string)) return (T)(object)body;
        try { return JsonSerializer.Deserialize<T>(body, Json)!; }
        catch (JsonException ex) { throw new ApiException(0, "Réponse inattendue du serveur : " + ex.Message); }
    }

    private static StringContent Body(object? o) =>
        new(JsonSerializer.Serialize(o, Json), System.Text.Encoding.UTF8, "application/json");

    public static Task<T> Get<T>(string path) => Send<T>(Build(HttpMethod.Get, path));
    public static Task<T> Post<T>(string path, object? body = null) => Send<T>(Build(HttpMethod.Post, path, Body(body ?? new { })));
    public static Task<T> Put<T>(string path, object? body) => Send<T>(Build(HttpMethod.Put, path, Body(body)));
    public static Task<T> Patch<T>(string path, object? body) => Send<T>(Build(HttpMethod.Patch, path, Body(body)));
    public static Task<T> Delete<T>(string path) => Send<T>(Build(HttpMethod.Delete, path));

    /// <summary>Envoi d'un fichier (preuve de paiement) en multipart.</summary>
    public static async Task<T> Upload<T>(string path, FileResult file)
    {
        await using var stream = await file.OpenReadAsync();
        var form = new MultipartFormDataContent();
        var part = new StreamContent(stream);
        form.Add(part, "file", file.FileName);
        return await Send<T>(Build(HttpMethod.Post, path, form));
    }
}

// ============================================================ Modèles (correspondent aux réponses de l'API)
public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Initials { get; set; } = "";
    public string Identifier { get; set; } = "";
    public string Theme { get; set; } = "system";
    public string CreatedAt { get; set; } = "";
    public int? ColocationId { get; set; }
    public string? Role { get; set; }
    public string FirstName => FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? FullName;
}

public class AuthDto { public string Token { get; set; } = ""; public UserDto User { get; set; } = new(); }
public class MessageDto { public string Message { get; set; } = ""; public string? DevCode { get; set; } }

public class ColocationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public int Occupants { get; set; }
    public int MembersCount { get; set; }
    public string SplitMode { get; set; } = "consumption";
    public string Role { get; set; } = "member";
    public string InviteCode { get; set; } = "";
    public bool HasMeter { get; set; }
    public bool IsAdmin => Role == "admin";
}

public class InviteDto { public string Code { get; set; } = ""; public bool Expired { get; set; } public string ShareText { get; set; } = ""; }

public class MeterDto
{
    public string Number { get; set; } = "";
    public string Type { get; set; } = "prepaid";
    public double InitialIndex { get; set; }
    public double TariffFcfa { get; set; } = 100;
    public double LastIndex { get; set; }
    public string? LastReadAt { get; set; }
    public bool IsPrepaid => Type == "prepaid";
}

public class ReadingDto
{
    public int Id { get; set; }
    public string ReadAt { get; set; } = "";
    public string? Author { get; set; }
    public double Index { get; set; }
    public double DeltaKwh { get; set; }
    public double EstimatedFcfa { get; set; }
    public bool IsInitial { get; set; }
    // Affichage (liaison XAML)
    public string DateText => Fmt.DateShort(ReadAt);
    public string IndexText => Fmt.Kwh(Index);
    public string Line => DateText + (string.IsNullOrEmpty(Author) ? "" : " · " + Author);
}
public class ReadingsDto { public List<ReadingDto> Items { get; set; } = new(); }

public class MemberDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Initials { get; set; } = "";
    public string Role { get; set; } = "member";
    public bool IsAdmin { get; set; }
    public int PresenceDays { get; set; }
    public double BalanceKwh { get; set; }
    public bool IsMe { get; set; }
    // Affichage
    public string DisplayName => IsMe ? FullName + " (moi)" : FullName;
    public string PresenceText => PresenceDays + " j de présence ce mois";
    public string RoleText => IsAdmin ? "Admin" : "Membre";
    [JsonIgnore] public Color BadgeBack => Theme.Pick(IsAdmin ? "PrimarySoftLight" : "StrokeLight", IsAdmin ? "PrimarySoftDark" : "StrokeDark");
    [JsonIgnore] public Color BadgeText => Theme.Pick(IsAdmin ? "Primary" : "MutedLight", IsAdmin ? "LinkDark" : "MutedDark");
}
public class MembersDto { public int Count { get; set; } public int Capacity { get; set; } public List<MemberDto> Items { get; set; } = new(); }

public class PersonDto { public int Id { get; set; } public string Name { get; set; } = ""; public string Initials { get; set; } = ""; }

public class PaymentDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = "recharge";
    public string KindLabel { get; set; } = "";
    public string Title { get; set; } = "";
    public PersonDto Payer { get; set; } = new();
    public PersonDto? Beneficiary { get; set; }
    public double AmountFcfa { get; set; }
    public double Kwh { get; set; }
    public string Method { get; set; } = "";
    public string MethodLabel { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string StatusLabel { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public bool HasProof { get; set; }
    public bool CanDecide { get; set; }
    public bool CanCancel { get; set; }
    // Affichage
    public string Initials => Payer.Initials;
    public string NameText => Beneficiary == null ? Payer.Name : Payer.Name + " → " + Beneficiary.Name;
    public string DateText => Fmt.DateShort(CreatedAt, true);
    public string MethodText => MethodLabel + " · " + KindLabel;
    public string AmountText => (Kind == "reimbursement" ? "" : "+") + Fmt.Num(AmountFcfa);
    public string StatusText => Status switch { "confirmed" => "Confirmé ✓", "pending" => "En attente ⏳", _ => "Refusé ✕" };
    [JsonIgnore] public Color StatusColor => Status switch
    {
        "confirmed" => Theme.Pick("Success", "SuccessDark"),
        "pending" => Theme.Pick("Warning", "Warning"),
        _ => Theme.Pick("Danger", "Danger")
    };
    [JsonIgnore] public Color AmountColor => Kind == "reimbursement" ? Theme.Pick("TextLight", "TextDark") : Theme.Pick("Success", "SuccessDark");
}
public class PaymentsDto { public int Count { get; set; } public List<PaymentDto> Items { get; set; } = new(); }
public class ReceiptDto
{
    public PaymentDto Payment { get; set; } = new();
    public string StatusText { get; set; } = "";
    public string DateTime { get; set; } = "";
    public string Colocation { get; set; } = "";
    public string Text { get; set; } = "";
    public string ShareText { get; set; } = "";
}

public class BalanceItemDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string Initials { get; set; } = "";
    public double BalanceKwh { get; set; }
    public double BalanceFcfa { get; set; }
    public string Status { get; set; } = "balanced";
    public bool IsMe { get; set; }
    // Affichage
    public string DisplayName => IsMe ? Name + " (moi)" : Name;
    public string SubText => Status switch { "creditor" => "Avance payée", "debtor" => "Reste à régler", _ => "À jour" };
    public string Tag => Status switch { "creditor" => "Crédit", "debtor" => "À payer", _ => "Équilibré" };
    public string AmountText => (BalanceFcfa > 0 ? "+" : BalanceFcfa < 0 ? "−" : "") + Fmt.Num(Math.Abs(BalanceFcfa));
    [JsonIgnore] public Color StatusColor => Status switch
    {
        "creditor" => Theme.Pick("Success", "SuccessDark"),
        "debtor" => Theme.Pick("Danger", "Danger"),
        _ => Theme.Pick("MutedLight", "MutedDark")
    };
}
public class SettlementDto { public string FromName { get; set; } = ""; public string ToName { get; set; } = ""; public double AmountFcfa { get; set; } }
public class BalanceDto
{
    public double TariffFcfa { get; set; }
    public double CreditKwh { get; set; }
    public List<BalanceItemDto> Items { get; set; } = new();
    public List<SettlementDto> Settlements { get; set; } = new();
}

public class ShareDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public double Kwh { get; set; }
    public double Percent { get; set; }
    public double AmountFcfa { get; set; }
    [JsonIgnore] public Color Dot { get; set; } = Colors.Gray;      // renseigné par la page
    public string PercentText => Fmt.Pct(Percent) + " · " + Fmt.Kwh(Kwh);
    public string AmountText => Fmt.Fcfa(AmountFcfa);
}
public class SplitInputDto { public int UserId { get; set; } public string Name { get; set; } = ""; public double? Kwh { get; set; } public int Days { get; set; } }
public class SplitInputsDto { public List<SplitInputDto> Consumptions { get; set; } = new(); public List<SplitInputDto> Presence { get; set; } = new(); }
public class SplitDto
{
    public string Period { get; set; } = "";
    public string PeriodLabel { get; set; } = "";
    public string Mode { get; set; } = "consumption";
    public string AppliedMode { get; set; } = "consumption";
    public double TotalKwh { get; set; }
    public double TariffFcfa { get; set; }
    public double AmountFcfa { get; set; }
    public List<ShareDto> Shares { get; set; } = new();
    public SplitInputsDto Inputs { get; set; } = new();
}

public class AlertDto
{
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "info";
    public string Target { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
}
public class AlertsDto { public List<AlertDto> Late { get; set; } = new(); public List<AlertDto> Soon { get; set; } = new(); public List<AlertDto> Bill { get; set; } = new(); public List<AlertDto> LowCredit { get; set; } = new(); }
public class ReminderDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = "custom";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string? DueDate { get; set; }
    public bool Active { get; set; }
}
public class RemindersDto { public List<ReminderDto> Items { get; set; } = new(); }

/// <summary>Ligne affichée sur l'écran Rappels (alerte calculée ou rappel enregistré).</summary>
public class ReminderRow
{
    public string Tag { get; set; } = "INFO";
    [JsonIgnore] public Color TagColor { get; set; } = Colors.Gray;
    public string RightText { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string LinkText { get; set; } = "Voir le détail →";
    public string Target { get; set; } = "";
    public int ReminderId { get; set; }
    public bool IsReminder => ReminderId > 0;
    public bool Active { get; set; } = true;
    public double RowOpacity => Active ? 1 : 0.5;
}

public class HomeMeterDto { public string Number { get; set; } = ""; public string Type { get; set; } = "prepaid"; public double TariffFcfa { get; set; } public double LastIndex { get; set; } public string? LastReadAt { get; set; } }
public class HomeDto
{
    public bool HasMeter { get; set; }
    public HomeMeterDto? Meter { get; set; }
    public double AvgKwhPerDay { get; set; }
    public double MyBalanceKwh { get; set; }
    public double MyBalanceFcfa { get; set; }
    public int PendingPayments { get; set; }
    public int AlertsCount { get; set; }
    public double? CreditKwh { get; set; }
    public double? CreditFcfa { get; set; }
    public double? AutonomyDays { get; set; }
    public bool LowCredit { get; set; }
}

// ============================================================ Session (utilisateur connecté + mémo entre écrans)
public static class Session
{
    public static UserDto? User { get; set; }
    public static ColocationDto? Coloc { get; set; }
    public static MeterDto? Meter { get; set; }

    // Mémo transmis d'un écran à l'autre (le routage Shell ne porte pas de paramètres ici)
    public static double PendingAmount { get; set; }
    public static string? PendingKind { get; set; }      // recharge | reimbursement
    public static int LastPaymentId { get; set; }

    public static bool IsAdmin => Coloc?.IsAdmin == true;

    public static void Clear()
    {
        Token(null);
        User = null; Coloc = null; Meter = null; PendingAmount = 0; PendingKind = null; LastPaymentId = 0;
    }

    public static void Token(string? token, bool persist = false)
    {
        Api.Token = token;
        try
        {
            if (persist && token != null) Preferences.Default.Set("token", token);
            else Preferences.Default.Remove("token");
        }
        catch { /* stockage indisponible : la session reste en mémoire */ }
    }

    public static string? SavedToken()
    {
        try { var t = Preferences.Default.Get("token", ""); return string.IsNullOrEmpty(t) ? null : t; }
        catch { return null; }
    }

    /// <summary>Recharge utilisateur, colocation et compteur depuis l'API. Retourne false si non connecté.</summary>
    public static async Task<bool> RefreshAsync()
    {
        if (string.IsNullOrEmpty(Api.Token)) return false;
        User = await Api.Get<UserDto>("/auth/me");
        ApplyTheme();
        Coloc = null; Meter = null;
        if (User.ColocationId != null)
        {
            Coloc = await Api.Get<ColocationDto>("/colocation");
            if (Coloc.HasMeter) Meter = await Api.Get<MeterDto>("/meter");
        }
        return true;
    }

    /// <summary>Thème choisi sur cet appareil (clair par défaut). Il ne change que par un appui sur le bouton thème.</summary>
    public static bool IsDarkPreferred
    {
        get { try { return Preferences.Default.Get("theme", "light") == "dark"; } catch { return false; } }
    }

    public static void SetDark(bool dark)
    {
        try { Preferences.Default.Set("theme", dark ? "dark" : "light"); } catch { /* stockage indisponible */ }
        if (Application.Current != null) Application.Current.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
    }

    /// <summary>Applique le thème choisi par l'utilisateur ; ne suit JAMAIS le thème de Windows ni la valeur du serveur.</summary>
    public static void ApplyTheme()
    {
        if (Application.Current != null)
            Application.Current.UserAppTheme = IsDarkPreferred ? AppTheme.Dark : AppTheme.Light;
    }

    /// <summary>Route à ouvrir après connexion / inscription selon l'avancement (colocation, compteur).</summary>
    public static async Task GoNextAsync()
    {
        await RefreshAsync();
        if (Coloc == null) await Shell.Current.GoToAsync(nameof(Views.ColocationPage));
        else if (Meter == null && Coloc.IsAdmin) await Shell.Current.GoToAsync(nameof(Views.MeterSetupPage));
        else await Shell.Current.GoToAsync("//home");
    }
}

/// <summary>Couleurs des ressources, choisies selon le thème clair/sombre courant.</summary>
public static class Theme
{
    public static Color Res(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c ? c : Colors.Gray;

    public static Color Pick(string light, string dark) =>
        Application.Current?.RequestedTheme == AppTheme.Dark ? Res(dark) : Res(light);
}
