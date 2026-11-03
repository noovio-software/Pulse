using AltPulse.Client.Models;
using AltPulse.Client.Services;

namespace AltPulse.Client.Components;

/// <summary>État d'une colonne de la grille (un personnage de l'historique).</summary>
public sealed class CharacterColumn(HistoryEntry entry)
{
    public HistoryEntry Entry { get; set; } = entry;
    public CharacterSnapshot? Data { get; set; }
    public bool Loading { get; set; }
    public string? Error { get; set; }
    public string Key => Entry.Key;
}

public static class Ui
{
    public static readonly (RaidDifficulty Difficulty, string Label, string Short)[] Difficulties =
    [
        (RaidDifficulty.Lfr, "Recherche de raid", "RdR"),
        (RaidDifficulty.Normal, "Normal", "N"),
        (RaidDifficulty.Heroic, "Héroïque", "H"),
        (RaidDifficulty.Mythic, "Mythique", "M")
    ];

    public static readonly (string Value, string Label)[] SortOptions =
    [
        ("recent", "Recherche récente"),
        ("name", "Nom"),
        ("ilvl", "Niveau d'objet"),
        ("rating", "Score M+"),
        ("class", "Classe")
    ];

    public static string ClassCss(int classId) => classId is >= 1 and <= 13 ? $"class-{classId}" : "class-0";

    public static string QualityCss(string quality) => $"q-{quality.ToLowerInvariant()}";

    public static string Ilvl(double? value) => value is { } v and > 0 ? v.ToString("0.#") : "-";

    public static string Rating(double value) => value > 0 ? value.ToString("0") : "-";

    public static string Stars(int upgrades) => upgrades > 0 ? new string('★', Math.Min(upgrades, 3)) : "";

    public static string RunCss(int upgrades) => upgrades switch
    {
        <= 0 => "run-depleted",
        1 => "run-timed",
        2 => "run-timed2",
        _ => "run-timed3"
    };

    public static string Duration(long ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");
    }

    public static string Countdown(DateTimeOffset target)
    {
        var left = target - DateTimeOffset.UtcNow;
        if (left <= TimeSpan.Zero)
        {
            return "imminente";
        }

        return left.TotalDays >= 1
            ? $"{(int)left.TotalDays} j {left.Hours} h"
            : $"{left.Hours} h {left.Minutes:00} min";
    }

    public static string Ago(DateTimeOffset at)
    {
        var elapsed = DateTimeOffset.UtcNow - at;
        return elapsed.TotalMinutes < 1 ? "à l'instant"
            : elapsed.TotalHours < 1 ? $"il y a {(int)elapsed.TotalMinutes} min"
            : elapsed.TotalDays < 1 ? $"il y a {(int)elapsed.TotalHours} h"
            : $"il y a {(int)elapsed.TotalDays} j";
    }

    public static string WowheadData(EquippedItem item) =>
        item.Bonuses is null ? $"item={item.ItemId}&ilvl={item.ItemLevel}" : $"item={item.ItemId}&bonus={item.Bonuses}&ilvl={item.ItemLevel}";

    public static IEnumerable<CharacterColumn> Sort(IEnumerable<CharacterColumn> columns, string sortBy) => sortBy switch
    {
        "name" => columns.OrderBy(c => c.Entry.Name, StringComparer.CurrentCultureIgnoreCase),
        "ilvl" => columns.OrderByDescending(c => c.Data?.ItemLevelEquipped ?? 0),
        "rating" => columns.OrderByDescending(c => c.Data?.Rating ?? 0),
        "class" => columns.OrderBy(c => c.Data?.ClassName ?? "~").ThenBy(c => c.Entry.Name),
        _ => columns.OrderByDescending(c => c.Entry.LastSearchedUtc)
    };
}
