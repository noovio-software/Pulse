using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Noovio.LifePulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dungeon_routes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Season = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DungeonSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DungeonName = table.Column<string>(type: "text", nullable: false),
                    MinKeyLevel = table.Column<int>(type: "integer", nullable: false),
                    MaxKeyLevel = table.Column<int>(type: "integer", nullable: false),
                    AffixIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: true),
                    MdtString = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dungeon_routes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_requests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_runs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Forced = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Phase = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LeaderboardQueriesDone = table.Column<int>(type: "integer", nullable: false),
                    LeaderboardQueries = table.Column<int>(type: "integer", nullable: false),
                    ProfilesStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentSpec = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SpecsDone = table.Column<int>(type: "integer", nullable: false),
                    Candidates = table.Column<int>(type: "integer", nullable: false),
                    ProfilesRead = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "player_profiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Season = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Spec = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    RaiderIoCharacterId = table.Column<long>(type: "bigint", nullable: false),
                    Region = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    RealmSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProfileUrl = table.Column<string>(type: "text", nullable: true),
                    BestRunScore = table.Column<double>(type: "double precision", nullable: false),
                    BestKeyLevel = table.Column<int>(type: "integer", nullable: false),
                    LoadoutCode = table.Column<string>(type: "text", nullable: true),
                    HeroTree = table.Column<string>(type: "text", nullable: true),
                    ItemLevel = table.Column<double>(type: "double precision", nullable: false),
                    Gear = table.Column<string>(type: "jsonb", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: true),
                    Talents = table.Column<string>(type: "jsonb", nullable: false),
                    FetchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "spec_metas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Season = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Spec = table.Column<int>(type: "integer", nullable: false),
                    ComputedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SampleSize = table.Column<int>(type: "integer", nullable: false),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spec_metas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_accounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BattleNetId = table.Column<long>(type: "bigint", nullable: false),
                    BattleTag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastLoginAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSyncAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_characters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    BlizzardCharacterId = table.Column<long>(type: "bigint", nullable: false),
                    Region = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    RealmSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RealmName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClassId = table.Column<int>(type: "integer", nullable: false),
                    ClassName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RaceName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Faction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    SyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_characters_user_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dungeon_routes_Season_DungeonSlug",
                table: "dungeon_routes",
                columns: new[] { "Season", "DungeonSlug" });

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_requests_Status",
                table: "ingestion_requests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_runs_StartedAtUtc",
                table: "ingestion_runs",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_player_profiles_Season_Spec_Rank",
                table: "player_profiles",
                columns: new[] { "Season", "Spec", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_spec_metas_Season_Spec",
                table: "spec_metas",
                columns: new[] { "Season", "Spec" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_BattleNetId",
                table: "user_accounts",
                column: "BattleNetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_characters_AccountId_Region_BlizzardCharacterId",
                table: "user_characters",
                columns: new[] { "AccountId", "Region", "BlizzardCharacterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dungeon_routes");

            migrationBuilder.DropTable(
                name: "ingestion_requests");

            migrationBuilder.DropTable(
                name: "ingestion_runs");

            migrationBuilder.DropTable(
                name: "player_profiles");

            migrationBuilder.DropTable(
                name: "spec_metas");

            migrationBuilder.DropTable(
                name: "user_characters");

            migrationBuilder.DropTable(
                name: "user_accounts");
        }
    }
}
