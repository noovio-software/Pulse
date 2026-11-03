using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Noovio.FrontPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IngestionRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_runs_StartedAtUtc",
                table: "ingestion_runs",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingestion_runs");
        }
    }
}
