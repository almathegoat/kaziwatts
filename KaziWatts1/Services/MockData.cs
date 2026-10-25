using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Graphics;

namespace KaziWatts.Services;

/// <summary>Formats français : "4 200 FCFA", "42 kWh".</summary>
public static class Fmt
{
    public static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    public static string Fcfa(double v) => v.ToString("N0", Fr) + " FCFA";
    public static string Kwh(double v) => v.ToString("0.##", Fr) + " kWh";
    public static string SignedKwh(double v) =>
        (v > 0 ? "+" : v < 0 ? "−" : "") + Math.Abs(v).ToString("0.##", Fr) + " kWh";

    private static readonly string[] Months = { "janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc." };

    /// <summary>"4 200" (sans unité).</summary>
    public static string Num(double v) => Math.Round(v).ToString("N0", Fr);
    public static string Pct(double v) => v.ToString("0.#", Fr) + " %";

    /// <summary>Date ISO de l'API → "12 sept. 2026" (ou "12 sept. 2026 · 14:32").</summary>
    public static string DateShort(string? iso, bool withTime = false)
    {
        if (string.IsNullOrEmpty(iso) || !DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return "";
        var t = d.DateTime;   // heure locale telle qu'écrite par l'API (UTC+1)
        var s = $"{t.Day} {Months[t.Month - 1]} {t.Year}";
        return withTime ? s + " · " + t.ToString("HH:mm", CultureInfo.InvariantCulture) : s;
    }

    public static bool TryParse(string? s, out double v) =>
        double.TryParse((s ?? "").Replace(" ", "").Replace("\u202f", "").Replace("\u00a0", "").Replace(',', '.'),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}

/// <summary>Petits utilitaires d'interface partagés par les pages.</summary>
public static class Ui
{
    public static void SetPrimary(Button b, bool primary)
    {
        var key = primary ? "PrimaryButton" : "SecondaryButton";
        var res = Application.Current?.Resources;
        if (res != null && res.TryGetValue(key, out var s) && s is Style st)
            b.Style = st;
    }

    /// <summary>Partage natif ; si indisponible (Windows non packagé), copie dans le presse-papiers.</summary>
    public static async Task ShareAsync(Page page, string title, string text)
    {
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest { Title = title, Text = text });
        }
        catch
        {
            await Clipboard.Default.SetTextAsync(text);
            await page.DisplayAlert("Texte copié",
                "Le message a été copié dans le presse-papiers : collez-le dans WhatsApp ou un e-mail.", "OK");
        }
    }

    public static void Error(Label l, string? msg)
    {
        l.Text = msg ?? "";
        l.IsVisible = msg != null;
    }
}

public record Occupant(string Name, string Initials, double KwhConsumed, double KwhBrought, int PresenceDays, bool IsAdmin)
{
    public double Percent => KwhConsumed / MockData.TotalKwh * 100;
    public double ProgressValue => Percent / 100;
    public string AmountText => Fmt.Fcfa(KwhConsumed * MockData.TariffFcfa);
    public string ShareText => Fmt.Kwh(KwhConsumed) + " · " + Percent.ToString("0.0", Fmt.Fr) + " %";
    public double BalanceKwh => KwhBrought - KwhConsumed;
    public string BalanceText => Fmt.SignedKwh(BalanceKwh) + " (≈ " + Fmt.Fcfa(Math.Abs(BalanceKwh) * MockData.TariffFcfa) + ")";
    public string BalanceStatus => BalanceKwh > 0 ? "Créditeur ▲" : BalanceKwh < 0 ? "Débiteur ▼" : "Équilibré";
    public Color BalanceColor => BalanceKwh > 0 ? Color.FromArgb("#1B8A5A")
                               : BalanceKwh < 0 ? Color.FromArgb("#C62828")
                               : Color.FromArgb("#6B7592");
    public string Detail => "Apporté : " + Fmt.Kwh(KwhBrought) + " · Consommé : " + Fmt.Kwh(KwhConsumed);
    public string Role => IsAdmin ? "Admin" : "Membre";
    public string PresenceText => PresenceDays + " j de présence";
    public bool CanRemove => !IsAdmin;
}

public record Payment(string Who, string Initials, string Label, int Fcfa, double Kwh, string Date, string Status)
{
    public string Title => Who + " — " + Label;
    public string AmountText => Fmt.Fcfa(Fcfa);
    public string KwhText => Kwh > 0 ? "+" + Fmt.Kwh(Kwh) : "";
    public string DateText => Date;
    public string StatusText => Status switch
    {
        "Confirmé" => "Confirmé ✓",
        "En attente" => "En attente ⏳",
        _ => "Refusé ✕"
    };
    public Color StatusColor => Status switch
    {
        "Confirmé" => Color.FromArgb("#1B8A5A"),
        "En attente" => Color.FromArgb("#E08A00"),
        _ => Color.FromArgb("#C62828")
    };
}

public record Reading(string Date, string Author, double Index, double Delta)
{
    public string Line => Date + " · " + Author;
    public string IndexText => Fmt.Kwh(Index);
    public string DeltaText => Delta > 0 ? "+" + Fmt.Kwh(Delta) : "relevé de départ";
}

public record PartShare(string Name, string AmountText, string Status);

public class Reminder
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public bool Active { get; set; } = true;
}

/// <summary>
/// Données fictives du cahier des charges (septembre 2026, prépayé, sous-compteurs).
/// Cohérence : crédit = somme des soldes (56 + 6 − 20 = 42 kWh) ; autonomie = 42 / 7 = 6 jours.
/// </summary>
public static class MockData
{
    public const int TariffFcfa = 100;          // tarif INDICATIF (RG-13)
    public const double TotalKwh = 210;
    public const double CreditKwh = 42;
    public const double AvgKwhPerDay = 7;
    public const double LastIndex = 12702;
    public const string InviteCode = "KZ7T28";
    public static double AutonomyDays => CreditKwh / AvgKwhPerDay;

    public static readonly List<Occupant> Occupants = new()
    {
        new("Amina", "AN", 94, 150, 30, true),
        new("Carine", "CM", 74, 80, 30, false),
        new("Brice", "BK", 42, 22, 18, false),
    };

    public static readonly List<Payment> Payments = new()
    {
        new("Brice → Amina", "BK", "Remboursement (MoMo)", 2000, 0, "2 oct.", "En attente"),
        new("Brice", "BK", "Recharge", 2200, 22, "25 sept.", "Confirmé"),
        new("Carine", "CM", "Recharge", 8000, 80, "18 sept.", "Confirmé"),
        new("Amina", "AN", "Recharge", 15000, 150, "12 sept.", "Confirmé"),
    };

    public static readonly List<Reading> Readings = new()
    {
        new("28 sept. 2026", "Carine", 12702, 102),
        new("15 sept. 2026", "Brice", 12600, 108),
        new("1 sept. 2026", "Amina", 12492, 0),
    };

    public static readonly ObservableCollection<Reminder> Reminders = new()
    {
        new Reminder { Title = "Relevé du compteur", Subtitle = "Chaque 1er du mois à 8 h · Tous" },
        new Reminder { Title = "Rembourser 2 000 FCFA à Amina", Subtitle = "5 oct. · Brice" },
        new Reminder { Title = "Crédit bas (automatique)", Subtitle = "Autonomie < 3 jours · Tous" },
    };
}
