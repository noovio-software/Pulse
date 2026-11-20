using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Domain.Entities;

namespace Noovio.Pulse.Infrastructure.Blizzard;

/// <summary>
/// Recherche par région, royaume et nom (repris d'AltPulse) : la liste des royaumes est gardée 24 h,
/// un personnage trouvé 10 minutes (le formulaire le vérifie, puis la page d'analyse le relit).
/// </summary>
public sealed class BlizzardCharacterSearch(BlizzardClient client, IMemoryCache cache) : ICharacterSearchSource
{
    private static readonly TimeSpan RealmCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan CharacterCacheDuration = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlyList<RealmInfo>> GetRealmsAsync(string region, CancellationToken ct)
    {
        region = WowRegions.Normalize(region);
        var key = $"search-realms:{region}";
        if (cache.TryGetValue(key, out IReadOnlyList<RealmInfo>? cached) && cached is not null)
        {
            return cached;
        }

        BzRealmIndex? index;
        try
        {
            index = await client.GetRealmIndexAsync(region, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Sans liste, le formulaire reste utilisable : le royaume saisi est converti en slug.
            return [];
        }

        var realms = (index?.Realms ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r.Slug))
            .Select(r => new RealmInfo(r.Slug, string.IsNullOrWhiteSpace(r.Name) ? r.Slug : r.Name))
            .DistinctBy(r => r.Slug)
            .OrderBy(r => r.Name, StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), ignoreCase: true))
            .ToList();

        // Une liste vide (Blizzard indisponible) n'est pas gardée : elle se recharge au prochain essai.
        if (realms.Count > 0)
        {
            cache.Set(key, (IReadOnlyList<RealmInfo>)realms, RealmCacheDuration);
        }

        return realms;
    }

    public async Task<UserCharacter?> FindAsync(string region, string realmSlug, string name, CancellationToken ct)
    {
        region = WowRegions.Normalize(region);
        realmSlug = realmSlug.Trim().ToLowerInvariant();
        name = name.Trim();
        if (realmSlug.Length == 0 || name.Length == 0)
        {
            return null;
        }

        var key = $"search-character:{region}:{realmSlug}:{name.ToLowerInvariant()}";
        if (cache.TryGetValue(key, out UserCharacter? cached) && cached is not null)
        {
            return cached;
        }

        if (await client.GetCharacterProfileAsync(region, realmSlug, name, ct) is not { } profile)
        {
            return null;
        }

        var character = new UserCharacter
        {
            BlizzardCharacterId = profile.Id,
            Region = region,
            RealmSlug = profile.Realm?.Slug is { Length: > 0 } slug ? slug : realmSlug,
            RealmName = profile.Realm?.Name ?? realmSlug,
            Name = string.IsNullOrWhiteSpace(profile.Name) ? name : profile.Name,
            ClassId = profile.CharacterClass?.Id ?? 0,
            ClassName = profile.CharacterClass?.Name ?? "",
            RaceName = profile.Race?.Name,
            Faction = profile.Faction?.Type,
            Level = profile.Level
        };

        cache.Set(key, character, CharacterCacheDuration);
        return character;
    }
}
