using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PulseAdmin.Notifications.Migrations;

/// <inheritdoc />
public partial class AdminPushDevices : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "admin_push_devices",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Endpoint = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                P256dh = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Auth = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastDigestUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_push_devices", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_admin_push_devices_Endpoint",
            table: "admin_push_devices",
            column: "Endpoint",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "admin_push_devices");
    }
}
