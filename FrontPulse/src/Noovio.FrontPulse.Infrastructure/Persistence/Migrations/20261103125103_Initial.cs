using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Noovio.FrontPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
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
                name: "tank_profiles",
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
                    table.PrimaryKey("PK_tank_profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dungeon_routes_Season_DungeonSlug",
                table: "dungeon_routes",
                columns: new[] { "Season", "DungeonSlug" });

            migrationBuilder.CreateIndex(
                name: "IX_spec_metas_Season_Spec",
                table: "spec_metas",
                columns: new[] { "Season", "Spec" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tank_profiles_Season_Spec_Rank",
                table: "tank_profiles",
                columns: new[] { "Season", "Spec", "Rank" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dungeon_routes");

            migrationBuilder.DropTable(
                name: "spec_metas");

            migrationBuilder.DropTable(
                name: "tank_profiles");
        }
    }
}
