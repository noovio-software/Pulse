using Noovio.FrontPulse.Application.Abstractions;
using Noovio.FrontPulse.Domain;
using Noovio.FrontPulse.Domain.Entities;

namespace Noovio.FrontPulse.Application.Analysis;

/// <summary>Collecte les données d'un personnage et de la méta de sa spécialisation tank, puis applique <see cref="CharacterAnalyzer"/>.</summary>
public sealed class CharacterAnalysisService(
    ICharacterDetailsSource details,
    ISpecMetaQueries meta,
    ITankMetaStore store,
    IMediaSource media,
    TimeProvider time)
{
    public async Task<AnalysisResult> AnalyzeAsync(UserCharacter character, CancellationToken ct)
    {
        if (TankSpecs.ForClass(character.ClassId) is not { } spec)
        {
            return new AnalysisResult(AnalysisStatus.NoTankSpec, null);
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
        var icons = await media.GetItemIconsAsync(found.Loadout.Gear.Select(g => g.ItemId), ct);

        return new AnalysisResult(AnalysisStatus.Ok, CharacterAnalyzer.Analyze(spec, snapshot, top, found, icons, time.GetUtcNow()), spec);
    }
}
