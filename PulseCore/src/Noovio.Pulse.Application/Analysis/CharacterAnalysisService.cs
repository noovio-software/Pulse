using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Meta;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Roles;

namespace Noovio.Pulse.Application.Analysis;

/// <summary>Collecte les données d'un personnage et de la méta de sa spécialisation dans le rôle, puis applique <see cref="CharacterAnalyzer"/>.</summary>
public sealed class CharacterAnalysisService(
    PulseRole role,
    ICharacterDetailsSource details,
    ISpecMetaQueries meta,
    IMetaStore store,
    IMediaSource media,
    IEncounterJournalSource journal,
    MetaAggregatorService aggregator,
    TimeProvider time)
{
    /// <param name="requested">Spécialisation à comparer, pour une classe qui en a deux dans le rôle (prêtre) ; sinon la première de la classe.</param>
    public async Task<AnalysisResult> AnalyzeAsync(UserCharacter character, SpecInfo? requested, CancellationToken ct)
    {
        var specs = role.ForClass(character.ClassId);
        if ((requested is not null && specs.Contains(requested) ? requested : specs.FirstOrDefault()) is not { } spec)
        {
            return new AnalysisResult(AnalysisStatus.NoRoleSpec, null);
        }

        if (await meta.GetLatestAsync(spec.Spec, ct) is not { } snapshot)
        {
            return new AnalysisResult(AnalysisStatus.NoMeta, null, spec);
        }

        if (await details.GetAsync(character.Region, character.RealmSlug, character.Name, spec.Spec, ct) is not { } found)
        {
            return new AnalysisResult(AnalysisStatus.CharacterNotFound, null, spec);
        }

        var top = await store.GetProfilesAsync(snapshot.Season, spec.Spec, ct);
        var gems = found.Loadout.Gear.Concat(top.SelectMany(p => p.Gear))
            .SelectMany(g => g.Sockets ?? [])
            .Select(s => s.GemId)
            .OfType<int>();
        var icons = await media.GetItemIconsAsync(found.Loadout.Gear.Select(g => g.ItemId).Concat(gems).Distinct(), ct);
        var encounters = await journal.GetAsync(snapshot.Season, ct);

        return new AnalysisResult(
            AnalysisStatus.Ok,
            CharacterAnalyzer.Analyze(role, spec, snapshot, top, found, icons, time.GetUtcNow(), encounters, aggregator.Orientation),
            spec);
    }
}
