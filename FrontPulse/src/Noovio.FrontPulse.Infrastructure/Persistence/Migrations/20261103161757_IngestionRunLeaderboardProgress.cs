using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Noovio.FrontPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IngestionRunLeaderboardProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeaderboardQueries",
                table: "ingestion_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LeaderboardQueriesDone",
                table: "ingestion_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeaderboardQueries",
                table: "ingestion_runs");

            migrationBuilder.DropColumn(
                name: "LeaderboardQueriesDone",
                table: "ingestion_runs");
        }
    }
}
