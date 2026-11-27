using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PulseMetrics.Burst.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shared_analyses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BattleNetId = table.Column<long>(type: "bigint", nullable: false),
                    CharacterId = table.Column<long>(type: "bigint", nullable: false),
                    SpecSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RealmName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AnalyzedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Analysis = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_analyses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shared_analyses_BattleNetId_CharacterId_SpecSlug_AnalyzedAt~",
                table: "shared_analyses",
                columns: new[] { "BattleNetId", "CharacterId", "SpecSlug", "AnalyzedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_shared_analyses_Token",
                table: "shared_analyses",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shared_analyses");
        }
    }
}
