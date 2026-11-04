using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Noovio.FrontPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameSnapshotTrinkets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Les bijoux du snapshot ne se nomment plus défensifs / offensifs depuis le partage du moteur avec LifePulse.
            migrationBuilder.Sql("""
                UPDATE spec_metas
                SET "Snapshot" = ("Snapshot" - 'defensiveTrinkets' - 'offensiveTrinkets')
                    || jsonb_build_object('roleTrinkets', "Snapshot"->'defensiveTrinkets', 'damageTrinkets', "Snapshot"->'offensiveTrinkets')
                WHERE "Snapshot" ? 'defensiveTrinkets';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE spec_metas
                SET "Snapshot" = ("Snapshot" - 'roleTrinkets' - 'damageTrinkets')
                    || jsonb_build_object('defensiveTrinkets', "Snapshot"->'roleTrinkets', 'offensiveTrinkets', "Snapshot"->'damageTrinkets')
                WHERE "Snapshot" ? 'roleTrinkets';
                """);
        }
    }
}
