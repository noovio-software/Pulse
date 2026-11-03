using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Noovio.FrontPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Accounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "user_characters");

            migrationBuilder.DropTable(
                name: "user_accounts");
        }
    }
}
