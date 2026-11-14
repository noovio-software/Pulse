using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Import;
using Noovio.Pulse.Application.Meta;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Roles;

namespace Noovio.Pulse.Application.Analysis;

/// <summary>
/// Collecte les données d'un personnage et de la méta de sa spécialisation dans le rôle, puis applique <see cref="CharacterAnalyzer"/>.
/// Le personnage vient du compte (API Blizzard) ou d'un export de l'addon NoovioPulse (lu en jeu).
/// </summary>
public sealed class CharacterAnalysisService(
    PulseRole role,
    ICharacterDetailsSource details,
    ICharacterExtrasSource extras,
    ISpecMetaQueries meta,
    IMetaStore store,
    IMediaSource media,
    IEncounterJournalSource journal,
    ITalentTreeSource talentTrees,
    MetaAggregatorService aggregator,
    TimeProvider time)
{
    /// <param name="requested">Spécialisation à comparer, pour une classe qui en a deux dans le rôle (prêtre) ; sinon la première de la classe.</param>
    public async Task<AnalysisResult> AnalyzeAsync(UserCharacter character, SpecInfo? requested, CancellationToken ct)
    {
        if (Pick(character.ClassId, requested, played: null) is not { } spec)
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

        return await AnalyzeAsync(spec, snapshot, found, ct);
    }

    /// <summary>
    /// Analyse un export de l'addon : équipement, talents et feuille tels qu'en jeu, à l'instant de l'export.
    /// Le portrait et la partie Mythique+ sont relus par nom ; s'ils manquent, l'analyse se fait sans.
    /// </summary>
    /// <param name="requested">Spécialisation à comparer ; par défaut celle jouée à l'export si elle est du rôle.</param>
    public async Task<AnalysisResult> AnalyzeImportAsync(CharacterImport import, SpecInfo? requested, CancellationToken ct)
    {
        var who = import.Character!;
        if (Pick(who.Class, requested, played: who.Spec) is not { } spec)
        {
            return new AnalysisResult(AnalysisStatus.NoRoleSpec, null);
        }

        if (await meta.GetLatestAsync(spec.Spec, ct) is not { } snapshot)
        {
            return new AnalysisResult(AnalysisStatus.NoMeta, null, spec);
        }

        var layout = snapshot.TalentTree ?? await talentTrees.GetLayoutAsync(spec.Spec, ct);
        var realmSlug = ImportedCharacterMapper.RealmSlug(who.Realm ?? who.RealmNormalized);
        var found = ImportedCharacterMapper.ToDetails(
            import, role, spec, snapshot, layout,
            realmSlug.Length == 0 ? CharacterExtras.None : await extras.GetExtrasAsync(ImportedCharacterMapper.RegionOf(who.Region), realmSlug, who.Name!, ct));

        var result = await AnalyzeAsync(spec, snapshot, found, ct);
        if (result.Analysis is not { } analysis || (import.Bags is null && import.Vault is null))
        {
            return result;
        }

        // Sacs et coffre : connus seulement par l'addon, analysés à part.
        var icons = await media.GetItemIconsAsync(InventoryAnalyzer.ItemIds(import), ct);
        return result with { Analysis = analysis with { Inventory = InventoryAnalyzer.Analyze(import, snapshot, role, icons) } };
    }

    /// <summary>La spécialisation demandée si la classe l'a dans le rôle, sinon celle jouée, sinon la première de la classe.</summary>
    private SpecInfo? Pick(int classId, SpecInfo? requested, int? played)
    {
        var specs = role.ForClass(classId);
        return requested is not null && specs.Contains(requested) ? requested
            : specs.FirstOrDefault(s => (int)s.Spec == played) ?? specs.FirstOrDefault();
    }

    private async Task<AnalysisResult> AnalyzeAsync(SpecInfo spec, SpecMetaSnapshot snapshot, CharacterDetails found, CancellationToken ct)
    {
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
