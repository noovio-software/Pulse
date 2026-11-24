using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Meta;
using Noovio.Pulse.Application.Resources;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Entities;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;

namespace Noovio.Pulse.Application.Analysis;

/// <summary>
/// Règles de l'analyse, sans E/S : un personnage est comparé au snapshot méta et aux profils du top
/// de sa spécialisation. Seuils volontairement simples et explicités dans les textes affichés.
/// </summary>
public static class CharacterAnalyzer
{
    /// <summary>Écart de niveau d'objet toléré sous la médiane du top (global) avant de signaler.</summary>
    private const double IlvlTolerance = 3;
    private const double IlvlProblem = 10;

    /// <summary>Écart toléré sur un emplacement avant de proposer un remplacement.</summary>
    private const int SlotIlvlGap = 6;

    /// <summary>Talent « incontournable » / « atypique » selon la part du top qui le prend.</summary>
    private const double CoreShare = 0.9;
    private const double AtypicalShare = 0.1;

    /// <summary>Un enchantement est attendu sur un emplacement quand la majorité du top en porte un.</summary>
    private const double EnchantExpectedShare = 0.5;

    /// <summary>Bijou considéré comme marginal en dessous de cette part du top.</summary>
    private const double RareTrinketShare = 0.05;

    public static CharacterAnalysis Analyze(
        PulseRole role,
        SpecInfo spec,
        SpecMetaSnapshot meta,
        IReadOnlyList<PlayerProfile> top,
        CharacterDetails character,
        IReadOnlyDictionary<int, string> itemIcons,
        DateTimeOffset now,
        EncounterJournal? journal = null,
        Func<GearPiece, TrinketOrientation>? trinketOrientation = null)
    {
        var findings = new List<AnalysisFinding>();
        var areas = new List<AnalysisArea>();
        var playsSpec = character.ActiveSpecId == (int)spec.Spec;
        var gear = character.Loadout.Gear;

        if (!playsSpec)
        {
            findings.Add(new(AnalysisAreaKind.ItemLevel, Verdict.Improve,
                Fmt.Of(AnalysisTexts.OffSpec_Title, PlayedSpecName(character)),
                Fmt.Of(AnalysisTexts.OffSpec_Detail, spec.SpecName),
                Fmt.Of(AnalysisTexts.OffSpec_Advice, spec.SpecName, role.Wording.Noun)));
        }

        var gearReviews = ReviewGear(gear, meta, top, itemIcons);
        areas.Add(ItemLevel(role, character.Loadout.ItemLevel, meta, gearReviews, findings));
        var stats = ReviewStats(character.Loadout.Stats, meta, areas, findings);
        var talents = ReviewTalents(spec, character.Loadout, meta, areas, findings);
        areas.Add(Trinkets(gear, meta, top, itemIcons, findings));
        areas.Add(Embellishments(gear, meta, findings));
        areas.Add(Enchantments(gearReviews, findings));
        var gemsAndEnchants = GemEnchantOptimizer.Review(gear, character.Loadout.Stats, meta, top, itemIcons);
        areas.Add(Gems(gemsAndEnchants, findings));
        areas.Add(MythicPlus(character.MythicPlus, findings));

        // Note sur 100 : chaque domaine vaut 1 (bon), 0,5 (à améliorer) ou 0 (problème).
        var score = (int)Math.Round(areas.Average(a => a.Verdict switch { Verdict.Good => 1.0, Verdict.Improve => 0.5, _ => 0 }) * 100);

        var ordered = findings
            .OrderBy(f => f.Verdict switch { Verdict.Problem => 0, Verdict.Improve => 1, _ => 2 })
            .ThenBy(f => f.Area)
            .ToList();

        var roadmap = FarmRoadmapBuilder.Build(gear, meta, top, stats, character.MythicPlus, journal, itemIcons);
        var trinketPlan = TrinketPlanner.Build(gear, meta, top, journal, trinketOrientation, itemIcons, role.BossHits);

        return new CharacterAnalysis(spec, character, meta, playsSpec, score, areas, ordered, gearReviews, stats, talents, gemsAndEnchants, roadmap, trinketPlan, now);
    }

    // --- Équipement et niveau d'objet ------------------------------------

    private static List<GearReview> ReviewGear(
        IReadOnlyList<GearPiece> gear, SpecMetaSnapshot meta, IReadOnlyList<PlayerProfile> top, IReadOnlyDictionary<int, string> icons)
    {
        var n = Math.Max(1, top.Count);
        return gear.Select(piece =>
        {
            var topPieces = top.SelectMany(p => p.Gear.Where(g => g.Slot == piece.Slot)).ToList();
            var slotIlvl = (int)Math.Round(MetaAggregatorService.Median(topPieces.Select(g => (double)g.ItemLevel)));
            var share = (double)top.Count(p => p.Gear.Any(g => g.Slot == piece.Slot && g.ItemId == piece.ItemId)) / n;

            // Un emplacement compte comme enchanté pour un joueur du top dès qu'une de ses pièces l'est (anneaux).
            // Mains, cou, taille, bijoux : rien à enchanter, quoi que l'API liste dessus.
            var enchantable = GearRules.CanBeEnchanted(piece.Slot);
            var enchantRate = (double)top.Count(p => p.Gear.Any(g => g.Slot == piece.Slot && g.Enchantments.Count > 0)) / n;
            var expected = enchantable && enchantRate >= EnchantExpectedShare;
            var suggested = !enchantable ? null : topPieces.SelectMany(g => g.Enchantments)
                .GroupBy(StatText.Label)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            var hasEnchant = enchantable && piece.Enchantments.Count > 0;
            var verdict = expected && !hasEnchant ? Verdict.Problem
                : piece.ItemLevel < slotIlvl - SlotIlvlGap ? Verdict.Improve
                : Verdict.Good;

            return new GearReview(
                piece.Slot,
                piece.ItemId,
                piece.Name,
                icons.GetValueOrDefault(piece.ItemId),
                piece.ItemLevel,
                slotIlvl,
                share,
                meta.Slots.FirstOrDefault(s => s.Slot == piece.Slot)?.Items.FirstOrDefault(),
                expected,
                hasEnchant,
                suggested,
                verdict,
                piece.TemporaryEnchant is { } temporary ? StatText.ShortLabel(temporary) : null);
        }).ToList();
    }

    private static AnalysisArea ItemLevel(PulseRole role, double ilvl, SpecMetaSnapshot meta, List<GearReview> gear, List<AnalysisFinding> findings)
    {
        var gap = ilvl - meta.MedianItemLevel;
        var verdict = gap >= -IlvlTolerance ? Verdict.Good : gap >= -IlvlProblem ? Verdict.Improve : Verdict.Problem;
        var weakest = gear
            .Where(g => g.ItemLevel < g.TopItemLevel)
            .OrderBy(g => g.ItemLevel - g.TopItemLevel)
            .Take(3)
            .ToList();

        if (verdict == Verdict.Good)
        {
            findings.Add(new(AnalysisAreaKind.ItemLevel, Verdict.Good, Fmt.Of(AnalysisTexts.ItemLevel_GoodTitle, ilvl),
                Fmt.Of(AnalysisTexts.ItemLevel_TopMedian, meta.MedianItemLevel), AnalysisTexts.ItemLevel_GoodAdvice));
        }
        else
        {
            findings.Add(new(AnalysisAreaKind.ItemLevel, verdict, Fmt.Of(AnalysisTexts.ItemLevel_LowTitle, ilvl, -gap),
                Fmt.Of(AnalysisTexts.ItemLevel_LowDetail, meta.MedianItemLevel, role.Wording.ItemLevelImpact),
                weakest.Count == 0
                    ? AnalysisTexts.ItemLevel_LowAdviceGeneric
                    : Fmt.Of(AnalysisTexts.ItemLevel_LowAdvicePieces, string.Join(", ", weakest.Select(w => Fmt.Of(AnalysisTexts.ItemLevel_Piece, GameLabels.Slot(w.Slot), w.ItemLevel, w.TopItemLevel))))));
        }

        return new AnalysisArea(AnalysisAreaKind.ItemLevel, verdict, Fmt.Of("{0:0.#} / {1:0.#}", ilvl, meta.MedianItemLevel));
    }

    // --- Caractéristiques secondaires ------------------------------------

    private static List<StatReview> ReviewStats(StatSheet? sheet, SpecMetaSnapshot meta, List<AnalysisArea> areas, List<AnalysisFinding> findings)
    {
        if (sheet is null || meta.Stats.Count == 0)
        {
            areas.Add(new AnalysisArea(AnalysisAreaKind.Stats, Verdict.Improve, AnalysisTexts.Stats_Unavailable));
            return [];
        }

        var reviews = meta.Stats.Select(top =>
        {
            var mine = sheet.Get(top.Stat);
            var verdict = mine.Percent < top.P25Percent || mine.Percent > top.P75Percent ? Verdict.Improve : Verdict.Good;
            return new StatReview(top.Stat, mine.Percent, mine.Rating, top, verdict);
        }).ToList();

        var main = reviews[0];
        var low = reviews.Where(r => r.Percent < r.Top.P25Percent).ToList();
        var high = reviews.Where(r => r.Percent > r.Top.P75Percent).ToList();

        // La caractéristique prioritaire du top sous sa fourchette : c'est le point qui coûte le plus.
        var verdict = main.Percent < main.Top.P25Percent ? Verdict.Problem : low.Count + high.Count > 0 ? Verdict.Improve : Verdict.Good;

        foreach (var r in low)
        {
            findings.Add(new(AnalysisAreaKind.Stats, r == main ? Verdict.Problem : Verdict.Improve,
                Fmt.Of(AnalysisTexts.Stats_LowTitle, GameLabels.Stat(r.Stat), r.Percent),
                Fmt.Of(AnalysisTexts.Stats_Range, r.Top.P25Percent, r.Top.P75Percent, r.Top.MedianPercent),
                high.Count > 0
                    ? Fmt.Of(AnalysisTexts.Stats_LowAdviceSwap, GameLabels.Stat(r.Stat), string.Join($" {GameLabels.Or} ", high.Select(h => GameLabels.Stat(h.Stat))))
                    : Fmt.Of(AnalysisTexts.Stats_LowAdvice, GameLabels.Stat(r.Stat))));
        }

        if (low.Count == 0 && high.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.Stats, Verdict.Improve,
                Fmt.Of(AnalysisTexts.Stats_SurplusTitle, string.Join($" {GameLabels.And} ", high.Select(h => GameLabels.Stat(h.Stat)))),
                AnalysisTexts.Stats_SurplusDetail,
                Fmt.Of(AnalysisTexts.Stats_SurplusAdvice, GameLabels.Stat(main.Stat))));
        }

        if (verdict == Verdict.Good)
        {
            findings.Add(new(AnalysisAreaKind.Stats, Verdict.Good, AnalysisTexts.Stats_GoodTitle,
                AnalysisTexts.Stats_GoodDetail, AnalysisTexts.Stats_GoodAdvice));
        }

        areas.Add(new AnalysisArea(AnalysisAreaKind.Stats, verdict, Fmt.Of(AnalysisTexts.Stats_Summary, reviews.Count(r => r.Verdict == Verdict.Good))));
        return reviews;
    }

    // --- Talents ----------------------------------------------------------

    private static TalentReview ReviewTalents(SpecInfo spec, CharacterLoadout loadout, SpecMetaSnapshot meta, List<AnalysisArea> areas, List<AnalysisFinding> findings)
    {
        var topHero = meta.HeroTrees.FirstOrDefault();
        var selected = loadout.Talents.Select(t => (t.Tree, t.TalentId)).ToHashSet();

        if (selected.Count == 0)
        {
            findings.Add(new(AnalysisAreaKind.Talents, Verdict.Problem, Fmt.Of(AnalysisTexts.Talents_NoneTitle, spec.SpecName),
                Fmt.Of(AnalysisTexts.Talents_NoneDetail, spec.SpecName),
                AnalysisTexts.Talents_NoneAdvice));
            areas.Add(new AnalysisArea(AnalysisAreaKind.Talents, Verdict.Problem, AnalysisTexts.Talents_NoneSummary));
            return new TalentReview(null, null, topHero?.Name, [], [], [], 0);
        }

        // Les talents héroïques ne se comparent que si le personnage joue le même arbre que le top.
        var sameHero = loadout.HeroTree is not null && loadout.HeroTree == topHero?.Name;
        // L'API compte aussi des talents absents de l'arbre de la spécialisation : nœuds offerts aux autres spécialisations
        // de la classe (trois « Explosion de lave » pour le chaman), rangs d'un même nœud, nœuds techniques sans nom.
        // Personne ne peut les choisir : seuls les talents de l'arbre se comparent.
        var inTree = meta.TalentTree?.Sections.SelectMany(s => s.Nodes).SelectMany(n => n.Choices).Select(c => c.TalentId).ToHashSet();
        bool Comparable(TalentPickRate t) => (t.Tree != TalentTree.Hero || sameHero) && (inTree is not { Count: > 0 } || inTree.Contains(t.TalentId));

        var missing = meta.Talents.Where(t => t.Share >= CoreShare && Comparable(t) && !selected.Contains((t.Tree, t.TalentId))).ToList();
        var shares = meta.Talents.ToDictionary(t => (t.Tree, t.TalentId));
        var atypical = loadout.Talents
            .Where(t => t.Tree != TalentTree.Hero || sameHero)
            .Select(t => shares.TryGetValue((t.Tree, t.TalentId), out var rate) ? rate : new TalentPickRate(t.Tree, t.TalentId, t.SpellId, t.Name, t.Rank, 0))
            .Where(t => t.Share < AtypicalShare)
            .ToList();

        var majority = meta.Talents.Where(t => t.Share >= 0.5 && Comparable(t)).ToList();
        var similarity = majority.Count == 0 ? 1 : (double)majority.Count(t => selected.Contains((t.Tree, t.TalentId))) / majority.Count;

        var heroDiffers = topHero is not null && topHero.Share >= 0.5 && loadout.HeroTree != topHero.Name;
        var verdict = missing.Count >= 3 ? Verdict.Problem
            : missing.Count > 0 || atypical.Count >= 3 || heroDiffers ? Verdict.Improve
            : Verdict.Good;

        if (heroDiffers)
        {
            findings.Add(new(AnalysisAreaKind.Talents, Verdict.Improve, Fmt.Of(AnalysisTexts.Talents_HeroTitle, loadout.HeroTree ?? AnalysisTexts.Talents_HeroNone),
                Fmt.Of(AnalysisTexts.Talents_HeroDetail, topHero!.Share, topHero.Name),
                Fmt.Of(AnalysisTexts.Talents_HeroAdvice, topHero.Name)));
        }

        if (missing.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.Talents, missing.Count >= 3 ? Verdict.Problem : Verdict.Improve,
                Plural.Format(missing.Count, AnalysisTexts.Talents_MissingTitle_One, AnalysisTexts.Talents_MissingTitle_Other),
                Fmt.Of(AnalysisTexts.Talents_MissingDetail, Excerpt(missing.Select(t => t.Name))),
                AnalysisTexts.Talents_MissingAdvice));
        }

        if (atypical.Count >= 3)
        {
            findings.Add(new(AnalysisAreaKind.Talents, Verdict.Improve, Fmt.Of(AnalysisTexts.Talents_AtypicalTitle, atypical.Count),
                Fmt.Of(AnalysisTexts.Talents_AtypicalDetail, Excerpt(atypical.Select(t => t.Name))),
                AnalysisTexts.Talents_AtypicalAdvice));
        }

        if (verdict == Verdict.Good)
        {
            findings.Add(new(AnalysisAreaKind.Talents, Verdict.Good, Fmt.Of(AnalysisTexts.Talents_GoodTitle, similarity),
                AnalysisTexts.Talents_GoodDetail, AnalysisTexts.Talents_GoodAdvice));
        }

        areas.Add(new AnalysisArea(AnalysisAreaKind.Talents, verdict, Fmt.Of(AnalysisTexts.Talents_Summary, similarity)));
        return new TalentReview(
            loadout.LoadoutCode,
            loadout.HeroTree,
            topHero?.Name,
            missing,
            atypical,
            loadout.Talents.Select(t => t.TalentId).Distinct().ToList(),
            similarity);
    }

    // --- Bijoux, embellissements, enchantements ---------------------------

    private static AnalysisArea Trinkets(
        IReadOnlyList<GearPiece> gear, SpecMetaSnapshot meta, IReadOnlyList<PlayerProfile> top, IReadOnlyDictionary<int, string> icons, List<AnalysisFinding> findings)
    {
        var trinkets = gear.Where(g => g.Slot == GearSlot.Trinket).ToList();
        var popular = meta.Slots.FirstOrDefault(s => s.Slot == GearSlot.Trinket)?.Items ?? [];
        var n = Math.Max(1, top.Count);
        var rare = trinkets
            .Select(t => (Piece: t, Share: (double)top.Count(p => p.Gear.Any(g => g.ItemId == t.ItemId)) / n))
            .Where(t => t.Share < RareTrinketShare)
            .ToList();

        foreach (var (piece, share) in rare)
        {
            var alternatives = popular.Where(p => trinkets.All(t => t.ItemId != p.ItemId)).Take(2).ToList();
            findings.Add(new(AnalysisAreaKind.Trinkets, Verdict.Improve, Fmt.Of(AnalysisTexts.Trinkets_RareTitle, piece.Name),
                Fmt.Of(AnalysisTexts.Trinkets_RareDetail, share),
                alternatives.Count == 0
                    ? AnalysisTexts.Trinkets_RareAdviceGeneric
                    : Fmt.Of(AnalysisTexts.Trinkets_RareAdvice, string.Join(", ", alternatives.Select(a => Fmt.Of("{0} ({1:P0})", a.Name, a.Share)))),
                icons.GetValueOrDefault(piece.ItemId)));
        }

        var verdict = rare.Count == 0 ? Verdict.Good : Verdict.Improve;
        if (verdict == Verdict.Good && trinkets.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.Trinkets, Verdict.Good, AnalysisTexts.Trinkets_GoodTitle,
                Fmt.Of(AnalysisTexts.Trinkets_GoodDetail, string.Join($" {GameLabels.And} ", trinkets.Select(t => t.Name))), AnalysisTexts.Common_NothingToChange));
        }

        return new AnalysisArea(AnalysisAreaKind.Trinkets, verdict,
            rare.Count == 0 ? AnalysisTexts.Trinkets_SummaryGood : Plural.Format(rare.Count, AnalysisTexts.Trinkets_Summary_One, AnalysisTexts.Trinkets_Summary_Other));
    }

    private static AnalysisArea Embellishments(IReadOnlyList<GearPiece> gear, SpecMetaSnapshot meta, List<AnalysisFinding> findings)
    {
        var mine = gear.Where(g => g.IsEmbellished).Select(g => g.Effects.FirstOrDefault()?.Name ?? g.Name).ToList();
        var topCombo = meta.Embellishments.FirstOrDefault();
        var topNames = topCombo is null
            ? "—"
            : string.Join(" + ", topCombo.Parts.Select(p => p.Count > 1 ? $"{p.Name} ×{p.Count}" : p.Name));

        Verdict verdict;
        if (mine.Count < 2)
        {
            verdict = Verdict.Problem;
            findings.Add(new(AnalysisAreaKind.Embellishments, Verdict.Problem,
                Plural.Format(2 - mine.Count, AnalysisTexts.Embellishments_MissingTitle_One, AnalysisTexts.Embellishments_MissingTitle_Other),
                AnalysisTexts.Embellishments_MissingDetail,
                Fmt.Of(AnalysisTexts.Embellishments_MissingAdvice, topNames, topCombo?.Share ?? 0)));
        }
        else
        {
            var key = string.Join("|", mine.Order(StringComparer.Ordinal));
            var played = meta.Embellishments.FirstOrDefault(c =>
                string.Join("|", c.Parts.SelectMany(p => Enumerable.Repeat(p.Name, p.Count)).Order(StringComparer.Ordinal)) == key);
            verdict = played is null || played.Share < 0.1 ? Verdict.Improve : Verdict.Good;
            findings.Add(verdict == Verdict.Good
                ? new(AnalysisAreaKind.Embellishments, Verdict.Good, AnalysisTexts.Embellishments_GoodTitle,
                    Fmt.Of(AnalysisTexts.Embellishments_GoodDetail, string.Join(" + ", mine), played!.Share), AnalysisTexts.Common_NothingToChange)
                : new(AnalysisAreaKind.Embellishments, Verdict.Improve, AnalysisTexts.Embellishments_RareTitle,
                    Fmt.Of(AnalysisTexts.Embellishments_RareDetail, string.Join(" + ", mine)),
                    Fmt.Of(AnalysisTexts.Embellishments_RareAdvice, topNames, topCombo?.Share ?? 0)));
        }

        return new AnalysisArea(AnalysisAreaKind.Embellishments, verdict, $"{mine.Count}/2");
    }

    private static AnalysisArea Enchantments(List<GearReview> gear, List<AnalysisFinding> findings)
    {
        var missing = gear.Where(g => g.EnchantExpected && !g.HasEnchant).ToList();
        foreach (var g in missing)
        {
            // Jambes : renfort d'armure ou fil à sorts, pas un enchantement.
            var legs = GearRules.TakesLegAugment(g.Slot);
            findings.Add(new(AnalysisAreaKind.Enchantments, Verdict.Problem,
                Fmt.Of(legs ? AnalysisTexts.Enchants_MissingLegsTitle : AnalysisTexts.Enchants_MissingTitle, GameLabels.Slot(g.Slot)),
                legs ? AnalysisTexts.Enchants_MissingLegsDetail : AnalysisTexts.Enchants_MissingDetail,
                g.SuggestedEnchant is not null ? Fmt.Of(AnalysisTexts.Enchants_MissingAdvice, g.SuggestedEnchant)
                    : legs ? AnalysisTexts.Enchants_MissingLegsAdviceGeneric
                    : AnalysisTexts.Enchants_MissingAdviceGeneric,
                g.IconUrl));
        }

        if (missing.Count == 0)
        {
            findings.Add(new(AnalysisAreaKind.Enchantments, Verdict.Good, AnalysisTexts.Enchants_GoodTitle,
                AnalysisTexts.Enchants_GoodDetail, AnalysisTexts.Common_NothingToDo));
        }

        return new AnalysisArea(AnalysisAreaKind.Enchantments, missing.Count == 0 ? Verdict.Good : Verdict.Problem,
            missing.Count == 0 ? AnalysisTexts.Enchants_SummaryGood : Plural.Format(missing.Count, AnalysisTexts.Enchants_Summary_One, AnalysisTexts.Enchants_Summary_Other));
    }

    private static AnalysisArea Gems(GemEnchantReview review, List<AnalysisFinding> findings)
    {
        var empty = review.Sockets.Count(s => s.GemId is null);
        var swaps = review.Swaps.Where(s => s.Kind != SwapKind.FillSocket).ToList();

        if (empty > 0)
        {
            findings.Add(new(AnalysisAreaKind.Gems, Verdict.Problem, Plural.Format(empty, AnalysisTexts.Gems_EmptyTitle_One, AnalysisTexts.Gems_EmptyTitle_Other),
                AnalysisTexts.Gems_EmptyDetail,
                Fmt.Of(AnalysisTexts.Gems_EmptyAdvice, string.Join(", ", review.Sockets.Where(s => s.GemId is null).Select(s => s.Swap is { } fill ? $"{GameLabels.Slot(s.Slot)} → {fill.To}" : GameLabels.Slot(s.Slot))))));
        }

        if (review.MissingSockets.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.Gems, Verdict.Improve, Fmt.Of(AnalysisTexts.Gems_MissingSocketsTitle, string.Join(", ", review.MissingSockets.Select(GameLabels.Slot))),
                AnalysisTexts.Gems_MissingSocketsDetail,
                AnalysisTexts.Gems_MissingSocketsAdvice));
        }

        if (swaps.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.Gems, Verdict.Improve, Plural.Format(swaps.Count, AnalysisTexts.Gems_SwapsTitle_One, AnalysisTexts.Gems_SwapsTitle_Other),
                AnalysisTexts.Gems_SwapsDetail,
                string.Join(" ; ", swaps.Take(3).Select(s => $"{GameLabels.Slot(s.Slot)} → {s.To}")) + (swaps.Count > 3 ? "…" : "") + "."));
        }

        var verdict = empty > 0 ? Verdict.Problem : swaps.Count + review.MissingSockets.Count > 0 ? Verdict.Improve : Verdict.Good;
        if (verdict == Verdict.Good && review.Sockets.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.Gems, Verdict.Good, AnalysisTexts.Gems_GoodTitle,
                AnalysisTexts.Gems_GoodDetail, AnalysisTexts.Common_NothingToDo));
        }

        return new AnalysisArea(AnalysisAreaKind.Gems, verdict,
            empty > 0 ? Plural.Format(empty, AnalysisTexts.Gems_SummaryEmpty_One, AnalysisTexts.Gems_SummaryEmpty_Other)
            : swaps.Count > 0 ? Fmt.Of(AnalysisTexts.Gems_SummarySwaps, swaps.Count)
            : review.Sockets.Count == 0 ? AnalysisTexts.Gems_SummaryNone
            : Plural.Format(review.Sockets.Count, AnalysisTexts.Gems_SummaryFilled_One, AnalysisTexts.Gems_SummaryFilled_Other));
    }

    // --- Mythique+ --------------------------------------------------------

    private static AnalysisArea MythicPlus(MythicPlusProfile? mplus, List<AnalysisFinding> findings)
    {
        if (mplus is null || (mplus.BestRuns.Count == 0 && mplus.MissingDungeons.Count == 0))
        {
            return new AnalysisArea(AnalysisAreaKind.MythicPlus, Verdict.Improve, AnalysisTexts.MythicPlus_NoKeys);
        }

        var median = MetaAggregatorService.Median(mplus.BestRuns.Select(r => (double)r.Level));
        var weak = mplus.BestRuns.Where(r => r.Level <= median - 2).OrderBy(r => r.Level).ToList();
        var depleted = mplus.BestRuns.Where(r => r.Upgrades == 0).ToList();

        if (mplus.MissingDungeons.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.MythicPlus, Verdict.Problem,
                Plural.Format(mplus.MissingDungeons.Count, AnalysisTexts.MythicPlus_MissingTitle_One, AnalysisTexts.MythicPlus_MissingTitle_Other),
                AnalysisTexts.MythicPlus_MissingDetail, Fmt.Of(AnalysisTexts.MythicPlus_MissingAdvice, string.Join(", ", mplus.MissingDungeons))));
        }

        if (weak.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.MythicPlus, Verdict.Improve, AnalysisTexts.MythicPlus_WeakTitle,
                Fmt.Of(AnalysisTexts.MythicPlus_WeakDetail, string.Join(", ", weak.Select(r => $"{r.Dungeon} (+{r.Level})")), median),
                AnalysisTexts.MythicPlus_WeakAdvice));
        }

        if (depleted.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.MythicPlus, Verdict.Improve,
                Plural.Format(depleted.Count, AnalysisTexts.MythicPlus_DepletedTitle_One, AnalysisTexts.MythicPlus_DepletedTitle_Other),
                Fmt.Of(AnalysisTexts.MythicPlus_DepletedDetail, string.Join(", ", depleted.Select(r => $"{r.Dungeon} +{r.Level}"))),
                AnalysisTexts.MythicPlus_DepletedAdvice));
        }

        var verdict = mplus.MissingDungeons.Count > 0 ? Verdict.Problem : weak.Count + depleted.Count > 0 ? Verdict.Improve : Verdict.Good;
        if (verdict == Verdict.Good)
        {
            findings.Add(new(AnalysisAreaKind.MythicPlus, Verdict.Good, AnalysisTexts.MythicPlus_GoodTitle,
                AnalysisTexts.MythicPlus_GoodDetail, AnalysisTexts.MythicPlus_GoodAdvice));
        }

        return new AnalysisArea(AnalysisAreaKind.MythicPlus, verdict, Fmt.Of(AnalysisTexts.MythicPlus_Summary, mplus.Score));
    }

    /// <summary>La spécialisation jouée, nommée dans la langue de l'interface plutôt que dans celle de l'API.</summary>
    private static string PlayedSpecName(CharacterDetails character) =>
        character.ActiveSpecId is { } id && Enum.IsDefined((WowSpec)id) ? ((WowSpec)id).Info().SpecName
        : character.ActiveSpecName ?? AnalysisTexts.OffSpec_Other;

    /// <summary>Les cinq premiers noms, suivis de « … » s'il y en a d'autres.</summary>
    private static string Excerpt(IEnumerable<string> names)
    {
        var list = names.ToList();
        return string.Join(", ", list.Take(5)) + (list.Count > 5 ? "…" : "");
    }
}
