using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Noovio.Pulse.Usage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bench_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    App = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    FromCache = table.Column<bool>(type: "boolean", nullable: false),
                    Forced = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    BattleNetId = table.Column<long>(type: "bigint", nullable: false),
                    Region = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    RealmSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RealmName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CharacterName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClassId = table.Column<int>(type: "integer", nullable: false),
                    ClassName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Race = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Faction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    SpecId = table.Column<int>(type: "integer", nullable: true),
                    SpecSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SpecName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PlaysSpec = table.Column<bool>(type: "boolean", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    ItemLevel = table.Column<double>(type: "double precision", nullable: true),
                    TopItemLevel = table.Column<double>(type: "double precision", nullable: true),
                    Haste = table.Column<double>(type: "double precision", nullable: true),
                    Crit = table.Column<double>(type: "double precision", nullable: true),
                    Mastery = table.Column<double>(type: "double precision", nullable: true),
                    Versatility = table.Column<double>(type: "double precision", nullable: true),
                    HasteRating = table.Column<double>(type: "double precision", nullable: true),
                    CritRating = table.Column<double>(type: "double precision", nullable: true),
                    MasteryRating = table.Column<double>(type: "double precision", nullable: true),
                    VersatilityRating = table.Column<double>(type: "double precision", nullable: true),
                    TopHaste = table.Column<double>(type: "double precision", nullable: true),
                    TopCrit = table.Column<double>(type: "double precision", nullable: true),
                    TopMastery = table.Column<double>(type: "double precision", nullable: true),
                    TopVersatility = table.Column<double>(type: "double precision", nullable: true),
                    GearPieces = table.Column<int>(type: "integer", nullable: true),
                    GearGood = table.Column<int>(type: "integer", nullable: true),
                    GearImprove = table.Column<int>(type: "integer", nullable: true),
                    GearProblem = table.Column<int>(type: "integer", nullable: true),
                    GearTopShare = table.Column<double>(type: "double precision", nullable: true),
                    EnchantsExpected = table.Column<int>(type: "integer", nullable: true),
                    EnchantsMissing = table.Column<int>(type: "integer", nullable: true),
                    Sockets = table.Column<int>(type: "integer", nullable: true),
                    SocketsEmpty = table.Column<int>(type: "integer", nullable: true),
                    Embellishments = table.Column<int>(type: "integer", nullable: true),
                    TalentSimilarity = table.Column<double>(type: "double precision", nullable: true),
                    HeroTree = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TopHeroTree = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MythicScore = table.Column<double>(type: "double precision", nullable: true),
                    FindingsGood = table.Column<int>(type: "integer", nullable: true),
                    FindingsImprove = table.Column<int>(type: "integer", nullable: true),
                    FindingsProblem = table.Column<int>(type: "integer", nullable: true),
                    Areas = table.Column<string>(type: "jsonb", nullable: true),
                    SlotItemLevels = table.Column<string>(type: "jsonb", nullable: true),
                    Error = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bench_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bench_events_App_OccurredAtUtc",
                table: "bench_events",
                columns: new[] { "App", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bench_events_BattleNetId",
                table: "bench_events",
                column: "BattleNetId");

            migrationBuilder.CreateIndex(
                name: "IX_bench_events_OccurredAtUtc",
                table: "bench_events",
                column: "OccurredAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bench_events");
        }
    }
}
