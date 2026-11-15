using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Noovio.Pulse.Usage.Migrations
{
    /// <inheritdoc />
    public partial class AddBenchSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "BattleNetId",
                table: "bench_events",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "bench_events",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "BattleNet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "bench_events");

            migrationBuilder.AlterColumn<long>(
                name: "BattleNetId",
                table: "bench_events",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
