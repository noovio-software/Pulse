using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Noovio.FrontPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeasonDungeons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dungeon_routes");

            migrationBuilder.CreateTable(
                name: "season_dungeons",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Season = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ShortName = table.Column<string>(type: "text", nullable: false),
                    TimerSeconds = table.Column<int>(type: "integer", nullable: false),
                    IconUrl = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Routes = table.Column<string>(type: "jsonb", nullable: false),
                    FetchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_season_dungeons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_season_dungeons_Season_Slug",
                table: "season_dungeons",
                columns: new[] { "Season", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "season_dungeons");

            migrationBuilder.CreateTable(
                name: "dungeon_routes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AffixIds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: true),
                    DungeonName = table.Column<string>(type: "text", nullable: false),
                    DungeonSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    MaxKeyLevel = table.Column<int>(type: "integer", nullable: false),
                    MdtString = table.Column<string>(type: "text", nullable: false),
                    MinKeyLevel = table.Column<int>(type: "integer", nullable: false),
                    Season = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dungeon_routes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dungeon_routes_Season_DungeonSlug",
                table: "dungeon_routes",
                columns: new[] { "Season", "DungeonSlug" });
        }
    }
}
