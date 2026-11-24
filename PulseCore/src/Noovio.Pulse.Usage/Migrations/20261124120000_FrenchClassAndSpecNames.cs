using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Noovio.Pulse.Usage.Migrations
{
    /// <summary>
    /// Les benchs enregistraient la classe et la spécialisation dans la langue du visiteur : « Shaman » à côté de
    /// « Chaman » dans PulseAdmin. Les sites écrivent désormais toujours le français ; on remet l'historique au même nom,
    /// par identifiant Blizzard. Pas de schéma modifié.
    /// </summary>
    [DbContext(typeof(UsageDbContext))]
    [Migration("20261124120000_FrenchClassAndSpecNames")]
    public partial class FrenchClassAndSpecNames : Migration
    {
        private static readonly (int Id, string Name)[] Classes =
        [
            (1, "Guerrier"), (2, "Paladin"), (3, "Chasseur"), (4, "Voleur"), (5, "Prêtre"), (6, "Chevalier de la mort"),
            (7, "Chaman"), (8, "Mage"), (9, "Démoniste"), (10, "Moine"), (11, "Druide"), (12, "Chasseur de démons"), (13, "Évocateur")
        ];

        private static readonly (int Id, string Name)[] Specs =
        [
            (73, "Protection"), (66, "Protection"), (250, "Sang"), (104, "Gardien"), (268, "Maître brasseur"), (581, "Vengeance"),
            (65, "Sacré"), (256, "Discipline"), (257, "Sacré"), (264, "Restauration"), (105, "Restauration"), (270, "Tisse-brume"),
            (1468, "Préservation"), (262, "Élémentaire"), (263, "Amélioration"), (253, "Maîtrise des bêtes"), (254, "Précision"),
            (255, "Survie"), (577, "Dévastation"), (1480, "Dévoration"), (251, "Givre"), (252, "Impie"), (265, "Affliction"),
            (266, "Démonologie"), (267, "Destruction"), (102, "Équilibre"), (103, "Farouche"), (1467, "Dévastation"),
            (1473, "Augmentation"), (71, "Armes"), (72, "Fureur"), (62, "Arcanes"), (63, "Feu"), (64, "Givre"),
            (269, "Marche-vent"), (70, "Vindicte"), (258, "Ombre"), (259, "Assassinat"), (260, "Hors-la-loi"), (261, "Finesse")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (id, name) in Classes)
            {
                migrationBuilder.Sql($"""UPDATE bench_events SET "ClassName" = '{Escape(name)}' WHERE "ClassId" = {id};""");
            }

            foreach (var (id, name) in Specs)
            {
                migrationBuilder.Sql($"""UPDATE bench_events SET "SpecName" = '{Escape(name)}' WHERE "SpecId" = {id};""");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La langue d'origine de chaque ligne n'est pas conservée : rien à défaire.
        }

        private static string Escape(string text) => text.Replace("'", "''");
    }
}
