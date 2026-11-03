using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Domain.Entities;

namespace Noovio.FrontPulse.Infrastructure.Blizzard;

/// <summary>
/// Personnages d'un compte Battle.net, toutes régions confondues : le profil de compte est régional
/// et un même compte peut avoir des personnages en Europe et en Amérique.
/// </summary>
public sealed class BlizzardAccountSource(BlizzardClient client) : IAccountCharactersSource
{
    private static readonly string[] Regions = ["eu", "us", "kr", "tw"];

    public async Task<IReadOnlyList<UserCharacter>> GetCharactersAsync(string userAccessToken, CancellationToken ct)
    {
        var profiles = await Task.WhenAll(Regions.Select(async region =>
            (Region: region, Profile: await client.GetAccountProfileAsync(region, userAccessToken, ct))));

        return profiles
            .Where(p => p.Profile is not null)
            .SelectMany(p => p.Profile!.WowAccounts.SelectMany(a => a.Characters).Select(c => new UserCharacter
            {
                BlizzardCharacterId = c.Id,
                Region = p.Region,
                RealmSlug = c.Realm.Slug,
                RealmName = c.Realm.Name ?? c.Realm.Slug,
                Name = c.Name,
                ClassId = c.PlayableClass?.Id ?? 0,
                ClassName = c.PlayableClass?.Name ?? "",
                RaceName = c.PlayableRace?.Name,
                Faction = c.Faction?.Type,
                Level = c.Level
            }))
            // Plusieurs licences WoW sur le même compte : un personnage n'apparaît qu'une fois.
            .DistinctBy(c => (c.Region, c.BlizzardCharacterId))
            .ToList();
    }
}
