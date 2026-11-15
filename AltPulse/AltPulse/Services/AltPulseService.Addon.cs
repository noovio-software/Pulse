using AltPulse.Client.Models;

namespace AltPulse.Services;

/// <summary>Import de l'export « alts » de l'addon NoovioPulse.</summary>
public sealed partial class AltPulseService
{
    public async Task<AddonImportResult> ImportAddonAsync(string text, CancellationToken cancellationToken = default)
    {
        var parsed = AddonExportParser.Parse(text);
        if (parsed.Error is not null)
        {
            return new AddonImportResult([], parsed.Error);
        }

        // Le jeu donne le nom affiché du royaume (« Conseil des Ombres ») : son identifiant vient de la liste Blizzard.
        var realmsByRegion = new Dictionary<string, IReadOnlyList<RealmInfo>>();
        foreach (var region in parsed.Characters.Select(c => NormalizeRegion(c.Region ?? "eu")).Distinct())
        {
            realmsByRegion[region] = await SafeRealmsAsync(region, cancellationToken);
        }

        var characters = parsed.Characters.Select(c =>
        {
            var region = NormalizeRegion(c.Region ?? "eu");
            var realmName = c.Realm!.Trim();
            var realm = realmsByRegion[region].FirstOrDefault(r => string.Equals(r.Name, realmName, StringComparison.OrdinalIgnoreCase)
                                                                   || string.Equals(r.Slug, Slugify(realmName), StringComparison.OrdinalIgnoreCase));
            return new AddonCharacter
            {
                Region = region,
                RealmSlug = realm?.Slug ?? Slugify(realmName),
                RealmName = realm?.Name ?? realmName,
                Name = c.Name!.Trim(),
                ClassId = c.Class,
                Level = c.Level,
                SpecName = c.SpecName,
                Role = c.Role,
                RaceName = c.Race,
                Faction = c.Faction,
                ItemLevelEquipped = c.Ilvl?.Equipped > 0 ? Math.Floor(c.Ilvl.Equipped) : null, // comme la fiche du personnage en jeu
                ItemLevelAverage = c.Ilvl?.Overall > 0 ? Math.Floor(c.Ilvl.Overall) : null,
                Rating = c.Rating,
                RatingColor = c.RatingColor,
                Keystone = c.Keystone is { Level: > 0 } k ? new AddonKeystone(k.Map, k.MapName, k.Level) : null,
                WeekStartUtc = c.WeekStart > 0 ? DateTimeOffset.FromUnixTimeSeconds(c.WeekStart.Value) : null,
                HasRewards = c.HasRewards,
                Vault = (c.Vault ?? []).Select(v => new AddonVaultActivity(v.Type, v.Index, v.Threshold, v.Progress, v.Level)).ToList(),
                Runs = (c.Runs ?? []).Select(r => new AddonRun(r.Map, r.MapName, r.Level, r.Completed)).ToList(),
                SavedAtUtc = c.SavedAt > 0 ? DateTimeOffset.FromUnixTimeSeconds(c.SavedAt) : DateTimeOffset.UtcNow
            };
        })
        .GroupBy(c => c.Key)
        .Select(g => g.MaxBy(c => c.SavedAtUtc)!)
        .OrderByDescending(c => c.SavedAtUtc)
        .ToList();

        return new AddonImportResult(characters, null);
    }

    private async Task<IReadOnlyList<RealmInfo>> SafeRealmsAsync(string region, CancellationToken cancellationToken)
    {
        try
        {
            return await GetRealmsAsync(region, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Liste des royaumes {Region} indisponible pour l'import de l'addon", region);
            return [];
        }
    }
}
