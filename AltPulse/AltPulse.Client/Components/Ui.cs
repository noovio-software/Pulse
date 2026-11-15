using AltPulse.Client.Localization;
using AltPulse.Client.Models;
using AltPulse.Client.Resources;
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
    /// <summary>Libellés dans la langue de l'interface : recalculés à chaque lecture. Le code court sert aussi de classe CSS (diff-n, diff-h…).</summary>
    public static (RaidDifficulty Difficulty, string Label, string Short, string Css)[] Difficulties =>
    [
        (RaidDifficulty.Lfr, AltTexts.Difficulty_Lfr, AltTexts.Difficulty_LfrShort, "rdr"),
        (RaidDifficulty.Normal, AltTexts.Difficulty_Normal, "N", "n"),
        (RaidDifficulty.Heroic, AltTexts.Difficulty_Heroic, "H", "h"),
        (RaidDifficulty.Mythic, AltTexts.Difficulty_Mythic, "M", "m")
    ];

    public static (string Value, string Label)[] SortOptions =>
    [
        ("recent", AltTexts.Sort_Recent),
        ("name", AltTexts.Sort_Name),
        ("ilvl", AltTexts.Sort_Ilvl),
        ("rating", AltTexts.Sort_Rating),
        ("class", AltTexts.Sort_Class)
    ];

    /// <summary>Code court d'une difficulté (vault de raid) : « RdR »/« LFR », « N », « H », « M ».</summary>
    public static string DifficultyShort(RaidDifficulty difficulty) => difficulty switch
    {
        RaidDifficulty.Lfr => AltTexts.Difficulty_LfrShort,
        RaidDifficulty.Normal => "N",
        RaidDifficulty.Heroic => "H",
        RaidDifficulty.Mythic => "M",
        _ => "✓"
    };

    /// <summary>Emplacement d'équipement dans la langue de l'interface, d'après son code Blizzard (HEAD, FINGER_1…).</summary>
    public static string SlotName(EquippedItem item) => SlotNameOrNull(item.Slot) ?? item.SlotName;

    public static string SlotName(string slot) => SlotNameOrNull(slot) ?? slot;

    private static string? SlotNameOrNull(string slot) => AltTexts.ResourceManager.GetString($"Slot_{slot}", AltTexts.Culture);

    public static string ItemUrl(EquippedItem item) => $"{AltLocalization.WowheadSite}/item={item.ItemId}";

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
            return AltTexts.Time_Imminent;
        }

        return left.TotalDays >= 1
            ? AltLocalization.F(AltTexts.Time_DaysHours, (int)left.TotalDays, left.Hours)
            : AltLocalization.F(AltTexts.Time_HoursMinutes, left.Hours, left.Minutes);
    }

    public static string Ago(DateTimeOffset at)
    {
        var elapsed = DateTimeOffset.UtcNow - at;
        return elapsed.TotalMinutes < 1 ? AltTexts.Time_JustNow
            : elapsed.TotalHours < 1 ? AltLocalization.F(AltTexts.Time_MinutesAgo, (int)elapsed.TotalMinutes)
            : elapsed.TotalDays < 1 ? AltLocalization.F(AltTexts.Time_HoursAgo, (int)elapsed.TotalHours)
            : AltLocalization.F(AltTexts.Time_DaysAgo, (int)elapsed.TotalDays);
    }

    public static string WowheadData(EquippedItem item) =>
        (item.Bonuses is null ? $"item={item.ItemId}&ilvl={item.ItemLevel}" : $"item={item.ItemId}&bonus={item.Bonuses}&ilvl={item.ItemLevel}")
        + AltLocalization.WowheadDomain;

    public static IEnumerable<CharacterColumn> Sort(IEnumerable<CharacterColumn> columns, string sortBy) => sortBy switch
    {
        "name" => columns.OrderBy(c => c.Entry.Name, StringComparer.CurrentCultureIgnoreCase),
        "ilvl" => columns.OrderByDescending(c => c.Data?.ItemLevelEquipped ?? 0),
        "rating" => columns.OrderByDescending(c => c.Data?.Rating ?? 0),
        "class" => columns.OrderBy(c => c.Data?.ClassName ?? "~", StringComparer.CurrentCultureIgnoreCase).ThenBy(c => c.Entry.Name),
        _ => columns.OrderByDescending(c => c.Entry.LastSearchedUtc)
    };
}
