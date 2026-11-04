using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Meta;
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
                $"Personnage joué en {character.ActiveSpecName ?? "autre spécialisation"}",
                $"L'équipement et les caractéristiques lus sont ceux de ta spécialisation actuelle, pas forcément de ton set {spec.SpecName}.",
                $"Passe en {spec.SpecName} avec ton équipement de {role.Wording.Noun} et déconnecte-toi, puis relance l'analyse pour un résultat fidèle."));
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
            var enchantRate = (double)top.Count(p => p.Gear.Any(g => g.Slot == piece.Slot && g.Enchantments.Count > 0)) / n;
            var expected = enchantRate >= EnchantExpectedShare;
            var suggested = topPieces.SelectMany(g => g.Enchantments)
                .GroupBy(StatText.Label)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            var hasEnchant = piece.Enchantments.Count > 0;
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
                verdict);
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
            findings.Add(new(AnalysisAreaKind.ItemLevel, Verdict.Good, $"Niveau d'objet {ilvl:0.#} : au niveau du top",
                $"La médiane du top est à {meta.MedianItemLevel:0.#}.", "Rien à faire ici : concentre-toi sur les autres points."));
        }
        else
        {
            findings.Add(new(AnalysisAreaKind.ItemLevel, verdict, $"Niveau d'objet {ilvl:0.#} : {-gap:0.#} sous le top",
                $"La médiane du top est à {meta.MedianItemLevel:0.#}. Le niveau d'objet pèse directement sur {role.Wording.ItemLevelImpact}.",
                weakest.Count == 0
                    ? "Améliore tes pièces les plus basses via la Grande chambre forte et les écus."
                    : "Remplace ou améliore en priorité : " + string.Join(", ", weakest.Select(w => $"{SlotName(w.Slot)} ({w.ItemLevel} contre {w.TopItemLevel})")) + "."));
        }

        return new AnalysisArea(AnalysisAreaKind.ItemLevel, verdict, $"{ilvl:0.#} / {meta.MedianItemLevel:0.#}");
    }

    // --- Caractéristiques secondaires ------------------------------------

    private static List<StatReview> ReviewStats(StatSheet? sheet, SpecMetaSnapshot meta, List<AnalysisArea> areas, List<AnalysisFinding> findings)
    {
        if (sheet is null || meta.Stats.Count == 0)
        {
            areas.Add(new AnalysisArea(AnalysisAreaKind.Stats, Verdict.Improve, "Indisponibles"));
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
                $"{StatName(r.Stat)} trop basse : {r.Percent:0} %",
                $"La moitié du top est entre {r.Top.P25Percent:0} et {r.Top.P75Percent:0} % (médiane {r.Top.MedianPercent:0} %).",
                high.Count > 0
                    ? $"Réoriente tes gemmes, enchantements et pièces craftées : prends de la {StatName(r.Stat)} à la place de {string.Join(" ou ", high.Select(h => StatName(h.Stat)))}."
                    : $"Privilégie la {StatName(r.Stat)} sur tes gemmes, enchantements et pièces craftées."));
        }

        if (low.Count == 0 && high.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.Stats, Verdict.Improve,
                $"Surplus de {string.Join(" et ", high.Select(h => StatName(h.Stat)))}",
                "Tu dépasses la fourchette du top : ces points rapporteraient davantage ailleurs.",
                $"Rééquilibre vers la {StatName(main.Stat)}, caractéristique prioritaire du top."));
        }

        if (verdict == Verdict.Good)
        {
            findings.Add(new(AnalysisAreaKind.Stats, Verdict.Good, "Caractéristiques alignées sur le top",
                "Tes quatre secondaires sont dans la fourchette de la moitié centrale du top.", "Garde cet équilibre en changeant de pièces."));
        }

        areas.Add(new AnalysisArea(AnalysisAreaKind.Stats, verdict, $"{reviews.Count(r => r.Verdict == Verdict.Good)}/4 dans la fourchette"));
        return reviews;
    }

    // --- Talents ----------------------------------------------------------

    private static TalentReview ReviewTalents(SpecInfo spec, CharacterLoadout loadout, SpecMetaSnapshot meta, List<AnalysisArea> areas, List<AnalysisFinding> findings)
    {
        var topHero = meta.HeroTrees.FirstOrDefault();
        var selected = loadout.Talents.Select(t => (t.Tree, t.TalentId)).ToHashSet();

        if (selected.Count == 0)
        {
            findings.Add(new(AnalysisAreaKind.Talents, Verdict.Problem, $"Aucune configuration {spec.SpecName} trouvée",
                $"Blizzard ne renvoie pas de talents pour ta spécialisation {spec.SpecName}.",
                "Passe en jeu dans cette spécialisation et importe la configuration la plus jouée du top (bouton Wowhead ou copie du code)."));
            areas.Add(new AnalysisArea(AnalysisAreaKind.Talents, Verdict.Problem, "Aucune configuration"));
            return new TalentReview(null, null, topHero?.Name, [], [], [], 0);
        }

        // Les talents héroïques ne se comparent que si le personnage joue le même arbre que le top.
        var sameHero = loadout.HeroTree is not null && loadout.HeroTree == topHero?.Name;
        bool Comparable(TalentPickRate t) => t.Tree != TalentTree.Hero || sameHero;

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
            findings.Add(new(AnalysisAreaKind.Talents, Verdict.Improve, $"Arbre héroïque : {loadout.HeroTree ?? "aucun"}",
                $"{topHero!.Share:P0} du top joue {topHero.Name}.",
                $"Essaie {topHero.Name} : ouvre la configuration la plus jouée dans le calculateur pour voir les différences."));
        }

        if (missing.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.Talents, missing.Count >= 3 ? Verdict.Problem : Verdict.Improve,
                $"{missing.Count} talent{(missing.Count > 1 ? "s" : "")} incontournable{(missing.Count > 1 ? "s" : "")} manquant{(missing.Count > 1 ? "s" : "")}",
                "Pris par au moins 90 % du top : " + string.Join(", ", missing.Take(5).Select(t => t.Name)) + (missing.Count > 5 ? "…" : "") + ".",
                "Récupère les points sur tes choix atypiques, ou importe directement la configuration la plus jouée."));
        }

        if (atypical.Count >= 3)
        {
            findings.Add(new(AnalysisAreaKind.Talents, Verdict.Improve, $"{atypical.Count} choix atypiques",
                "Pris par moins de 10 % du top : " + string.Join(", ", atypical.Take(5).Select(t => t.Name)) + (atypical.Count > 5 ? "…" : "") + ".",
                "Garde-les seulement s'ils répondent à un besoin précis (affixe, donjon) ; sinon, suis la méta."));
        }

        if (verdict == Verdict.Good)
        {
            findings.Add(new(AnalysisAreaKind.Talents, Verdict.Good, $"Talents proches de la méta ({similarity:P0})",
                "Tu prends les talents choisis par la majorité du top.", "Rien à changer, sauf adaptation ponctuelle à un donjon."));
        }

        areas.Add(new AnalysisArea(AnalysisAreaKind.Talents, verdict, $"{similarity:P0} de la méta"));
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
            findings.Add(new(AnalysisAreaKind.Trinkets, Verdict.Improve, $"{piece.Name} : bijou marginal",
                $"Seulement {share:P0} du top le porte.",
                alternatives.Count == 0
                    ? "Vise un des bijoux les plus portés du top."
                    : "Les plus portés : " + string.Join(", ", alternatives.Select(a => $"{a.Name} ({a.Share:P0})")) + ".",
                icons.GetValueOrDefault(piece.ItemId)));
        }

        var verdict = rare.Count == 0 ? Verdict.Good : Verdict.Improve;
        if (verdict == Verdict.Good && trinkets.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.Trinkets, Verdict.Good, "Bijoux joués par le top",
                string.Join(" et ", trinkets.Select(t => t.Name)) + " font partie des choix courants.", "Rien à changer."));
        }

        return new AnalysisArea(AnalysisAreaKind.Trinkets, verdict, rare.Count == 0 ? "Choix courants" : $"{rare.Count} marginal{(rare.Count > 1 ? "aux" : "")}");
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
            findings.Add(new(AnalysisAreaKind.Embellishments, Verdict.Problem, $"{2 - mine.Count} embellissement{(mine.Count == 0 ? "s" : "")} manquant{(mine.Count == 0 ? "s" : "")}",
                "Deux pièces embellies sont permises : c'est un gain gratuit que presque tout le top exploite.",
                $"Fais crafter une pièce embellie. Combinaison la plus jouée : {topNames} ({topCombo?.Share ?? 0:P0} du top)."));
        }
        else
        {
            var key = string.Join("|", mine.Order(StringComparer.Ordinal));
            var played = meta.Embellishments.FirstOrDefault(c =>
                string.Join("|", c.Parts.SelectMany(p => Enumerable.Repeat(p.Name, p.Count)).Order(StringComparer.Ordinal)) == key);
            verdict = played is null || played.Share < 0.1 ? Verdict.Improve : Verdict.Good;
            findings.Add(verdict == Verdict.Good
                ? new(AnalysisAreaKind.Embellishments, Verdict.Good, "Embellissements de la méta",
                    $"{string.Join(" + ", mine)} : joué par {played!.Share:P0} du top.", "Rien à changer.")
                : new(AnalysisAreaKind.Embellishments, Verdict.Improve, "Embellissements peu joués",
                    $"{string.Join(" + ", mine)} est rare dans le top.", $"Combinaison la plus jouée : {topNames} ({topCombo?.Share ?? 0:P0} du top)."));
        }

        return new AnalysisArea(AnalysisAreaKind.Embellishments, verdict, $"{mine.Count}/2");
    }

    private static AnalysisArea Enchantments(List<GearReview> gear, List<AnalysisFinding> findings)
    {
        var missing = gear.Where(g => g.EnchantExpected && !g.HasEnchant).ToList();
        foreach (var g in missing)
        {
            findings.Add(new(AnalysisAreaKind.Enchantments, Verdict.Problem, $"{SlotName(g.Slot)} sans enchantement",
                "La majorité du top enchante cet emplacement.",
                g.SuggestedEnchant is null ? "Ajoute l'enchantement adapté." : $"Le plus courant dans le top : {g.SuggestedEnchant}.",
                g.IconUrl));
        }

        if (missing.Count == 0)
        {
            findings.Add(new(AnalysisAreaKind.Enchantments, Verdict.Good, "Tous les enchantements attendus",
                "Chaque emplacement enchanté par la majorité du top l'est aussi chez toi.", "Rien à faire."));
        }

        return new AnalysisArea(AnalysisAreaKind.Enchantments, missing.Count == 0 ? Verdict.Good : Verdict.Problem,
            missing.Count == 0 ? "Complets" : $"{missing.Count} manquant{(missing.Count > 1 ? "s" : "")}");
    }

    private static AnalysisArea Gems(GemEnchantReview review, List<AnalysisFinding> findings)
    {
        var empty = review.Sockets.Count(s => s.GemId is null);
        var swaps = review.Swaps.Where(s => s.Kind != SwapKind.FillSocket).ToList();

        if (empty > 0)
        {
            findings.Add(new(AnalysisAreaKind.Gems, Verdict.Problem, $"{empty} châsse{(empty > 1 ? "s" : "")} vide{(empty > 1 ? "s" : "")}",
                "Une châsse vide, c'est de l'indice perdu pour le prix d'une gemme.",
                "À remplir : " + string.Join(", ", review.Sockets.Where(s => s.GemId is null).Select(s => s.Swap is { } fill ? $"{SlotName(s.Slot)} → {fill.To}" : SlotName(s.Slot))) + "."));
        }

        if (review.MissingSockets.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.Gems, Verdict.Improve, $"Pas de châsse sur : {string.Join(", ", review.MissingSockets.Select(SlotName))}",
                "La majorité du top a une châsse sur ces emplacements.",
                "Vise une pièce avec châsse, ou ajoutes-en une si l'emplacement le permet."));
        }

        if (swaps.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.Gems, Verdict.Improve, $"{swaps.Count} gemme{(swaps.Count > 1 ? "s" : "")} ou enchantement{(swaps.Count > 1 ? "s" : "")} à réorienter",
                "Ces changements rapprochent tes caractéristiques de la fourchette du top, sans changer de pièce.",
                string.Join(" ; ", swaps.Take(3).Select(s => $"{SlotName(s.Slot)} : {s.To}")) + (swaps.Count > 3 ? "…" : "") + "."));
        }

        var verdict = empty > 0 ? Verdict.Problem : swaps.Count + review.MissingSockets.Count > 0 ? Verdict.Improve : Verdict.Good;
        if (verdict == Verdict.Good && review.Sockets.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.Gems, Verdict.Good, "Gemmes cohérentes avec le top",
                "Aucune châsse vide, et aucun changement de gemme ne rapprocherait tes caractéristiques du top.", "Rien à faire."));
        }

        return new AnalysisArea(AnalysisAreaKind.Gems, verdict,
            empty > 0 ? $"{empty} vide{(empty > 1 ? "s" : "")}"
            : swaps.Count > 0 ? $"{swaps.Count} à revoir"
            : review.Sockets.Count == 0 ? "Aucune châsse"
            : $"{review.Sockets.Count} posée{(review.Sockets.Count > 1 ? "s" : "")}");
    }

    // --- Mythique+ --------------------------------------------------------

    private static AnalysisArea MythicPlus(MythicPlusProfile? mplus, List<AnalysisFinding> findings)
    {
        if (mplus is null || (mplus.BestRuns.Count == 0 && mplus.MissingDungeons.Count == 0))
        {
            return new AnalysisArea(AnalysisAreaKind.MythicPlus, Verdict.Improve, "Aucune clé cette saison");
        }

        var median = MetaAggregatorService.Median(mplus.BestRuns.Select(r => (double)r.Level));
        var weak = mplus.BestRuns.Where(r => r.Level <= median - 2).OrderBy(r => r.Level).ToList();
        var depleted = mplus.BestRuns.Where(r => r.Upgrades == 0).ToList();

        if (mplus.MissingDungeons.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.MythicPlus, Verdict.Problem, $"{mplus.MissingDungeons.Count} donjon{(mplus.MissingDungeons.Count > 1 ? "s" : "")} jamais fait{(mplus.MissingDungeons.Count > 1 ? "s" : "")}",
                "Chaque donjon compte dans le score : un donjon absent vaut 0.", "Fais au moins une clé dans : " + string.Join(", ", mplus.MissingDungeons) + "."));
        }

        if (weak.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.MythicPlus, Verdict.Improve, "Donjons en retard",
                string.Join(", ", weak.Select(r => $"{r.Dungeon} (+{r.Level})")) + $" : sous ton niveau habituel (+{median:0}).",
                "Pousse ces donjons en premier : c'est là que ton score progressera le plus vite."));
        }

        if (depleted.Count > 0)
        {
            findings.Add(new(AnalysisAreaKind.MythicPlus, Verdict.Improve, $"{depleted.Count} meilleure{(depleted.Count > 1 ? "s" : "")} clé{(depleted.Count > 1 ? "s" : "")} hors temps",
                string.Join(", ", depleted.Select(r => $"{r.Dungeon} +{r.Level}")) + " : une clé dans le temps rapporte plus de score.",
                "Retente ces donjons au même niveau en visant le chrono plutôt qu'un niveau plus haut."));
        }

        var verdict = mplus.MissingDungeons.Count > 0 ? Verdict.Problem : weak.Count + depleted.Count > 0 ? Verdict.Improve : Verdict.Good;
        if (verdict == Verdict.Good)
        {
            findings.Add(new(AnalysisAreaKind.MythicPlus, Verdict.Good, "Progression homogène",
                "Tous tes donjons sont dans le temps et à un niveau proche.", "Monte les clés une par une, en commençant par la plus basse."));
        }

        return new AnalysisArea(AnalysisAreaKind.MythicPlus, verdict, $"Score {mplus.Score:0}");
    }

    private static string SlotName(GearSlot slot) => slot switch
    {
        GearSlot.Head => "Tête",
        GearSlot.Neck => "Cou",
        GearSlot.Shoulder => "Épaules",
        GearSlot.Back => "Dos",
        GearSlot.Chest => "Torse",
        GearSlot.Wrist => "Poignets",
        GearSlot.Hands => "Mains",
        GearSlot.Waist => "Taille",
        GearSlot.Legs => "Jambes",
        GearSlot.Feet => "Pieds",
        GearSlot.Finger => "Anneau",
        GearSlot.Trinket => "Bijou",
        GearSlot.MainHand => "Main droite",
        _ => "Main gauche"
    };

    private static string StatName(SecondaryStat stat) => stat switch
    {
        SecondaryStat.Haste => "Hâte",
        SecondaryStat.Versatility => "Polyvalence",
        SecondaryStat.Mastery => "Maîtrise",
        _ => "Critique"
    };
}
