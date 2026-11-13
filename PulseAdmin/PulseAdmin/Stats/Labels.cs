using System.Globalization;
using Noovio.Pulse.Usage;

namespace PulseAdmin.Stats;

/// <summary>Libellés et mises en forme de l'interface.</summary>
public static class Labels
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Classe CSS de l'application : sa couleur de série (validée daltonisme, voir app.css).</summary>
    public static string AppClass(string app) => app switch
    {
        "FrontPulse" => "app-front",
        "LifePulse" => "app-life",
        "BurstPulse" => "app-burst",
        _ => "app-other"
    };

    public static string AppRole(string app) => app switch
    {
        "FrontPulse" => "Tanks",
        "LifePulse" => "Soigneurs",
        "BurstPulse" => "DPS",
        _ => app
    };

    public static string Status(string status) => status switch
    {
        BenchStatus.Ok => "Abouti",
        BenchStatus.NoRoleSpec => "Classe hors rôle",
        BenchStatus.NoMeta => "Méta pas prête",
        BenchStatus.CharacterNotFound => "Perso introuvable",
        BenchStatus.Error => "API injoignable",
        _ => status
    };

    public static string Area(string area) => area switch
    {
        "ItemLevel" => "Niveau d'objet",
        "Stats" => "Caractéristiques",
        "Talents" => "Talents",
        "Trinkets" => "Bijoux",
        "Embellishments" => "Embellissements",
        "Enchantments" => "Enchantements",
        "Gems" => "Gemmes",
        "MythicPlus" => "Mythique+",
        _ => area
    };

    public static string Slot(string slot) => slot switch
    {
        "Head" => "Tête",
        "Neck" => "Cou",
        "Shoulder" => "Épaules",
        "Back" => "Dos",
        "Chest" => "Torse",
        "Wrist" => "Poignets",
        "Hands" => "Mains",
        "Waist" => "Taille",
        "Legs" => "Jambes",
        "Feet" => "Pieds",
        "Finger" => "Anneaux",
        "Trinket" => "Bijoux",
        "MainHand" => "Main droite",
        "OffHand" => "Main gauche",
        _ => slot
    };

    /// <summary>Couleurs officielles des classes : un repère d'identité à côté du nom, jamais la seule information.</summary>
    public static string ClassColor(int classId) => classId switch
    {
        1 => "#C69B6D",
        2 => "#F48CBA",
        3 => "#AAD372",
        4 => "#FFF468",
        5 => "#FFFFFF",
        6 => "#C41E3A",
        7 => "#0070DD",
        8 => "#3FC7EB",
        9 => "#8788EE",
        10 => "#00FF98",
        11 => "#FF7C0A",
        12 => "#A330C9",
        13 => "#33937F",
        _ => "#71828C"
    };

    public static string Number(double? value, int decimals = 0) =>
        value is { } v ? v.ToString("N" + decimals, Fr) : "—";

    public static string Number(int value) => value.ToString("N0", Fr);

    /// <summary>Un ratio (0 à 1) en pourcentage.</summary>
    public static string Percent(double? ratio, int decimals = 0) =>
        ratio is { } r ? (r * 100).ToString("N" + decimals, Fr) + " %" : "—";

    /// <summary>Un pourcentage déjà exprimé sur 100 (feuille de personnage).</summary>
    public static string Points(double? value, int decimals = 1) =>
        value is { } v ? v.ToString("N" + decimals, Fr) + " %" : "—";

    public static string Signed(double? value, int decimals = 1, string unit = "") =>
        value is { } v ? (v > 0 ? "+" : v < 0 ? "−" : "") + Math.Abs(v).ToString("N" + decimals, Fr) + unit : "—";

    public static string Duration(double? ms) =>
        ms is { } v ? v >= 1000 ? (v / 1000).ToString("N1", Fr) + " s" : v.ToString("N0", Fr) + " ms" : "—";

    public static DateTimeOffset Paris(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, StatsFilter.Zone);

    public static string DateTime(DateTimeOffset utc) => Paris(utc).ToString("dd/MM/yyyy HH:mm", Fr);

    public static string Ago(DateTimeOffset utc, DateTimeOffset now)
    {
        var span = now - utc;
        return span.TotalMinutes < 1 ? "à l'instant"
            : span.TotalHours < 1 ? $"il y a {(int)span.TotalMinutes} min"
            : span.TotalDays < 1 ? $"il y a {(int)span.TotalHours} h"
            : span.TotalDays < 30 ? $"il y a {(int)span.TotalDays} j"
            : DateTime(utc);
    }

    public static readonly string[] Weekdays = ["Lun", "Mar", "Mer", "Jeu", "Ven", "Sam", "Dim"];
}
